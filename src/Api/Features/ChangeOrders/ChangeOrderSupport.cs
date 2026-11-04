using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Api.Common.Results;
using Kaff.Domain.Authorization;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.ChangeOrders;

/// <summary>Everything a step needs from the request, bundled so each endpoint's handler stays one signature.</summary>
internal sealed record ChangeOrderServices(
    KaffDbContext Database,
    RulingSet Rulings,
    TimeProvider Clock,
    ICurrentUser CurrentUser,
    IAuthorizationService Authorization,
    ClaimsPrincipal User);

/// <summary>One step of the chain: calls a <see cref="ChangeOrder"/> method.</summary>
internal delegate Result ChangeOrderStep(ChangeOrder order, Project project, ChangeOrderServices services);

/// <summary>
/// The shared spine of every step endpoint: lock the change-order row, load, run the aggregate method and commit
/// the status change and its gate record in one save. The permission gate (role and assignment) has already run on
/// the route. Audit records come from the interceptor on the same save.
/// </summary>
internal static class ChangeOrderSupport
{
    private const string NumberIndex = "ux_change_orders_project_number";

    public static DateTimeOffset Now(ChangeOrderServices services) => services.Clock.GetUtcNow();

    public static Guid Actor(ChangeOrderServices services) => services.CurrentUser.UserId ?? Guid.Empty;

    public static IQueryable<ChangeOrder> WithChildren(KaffDbContext database) =>
        database.ChangeOrders.Include(order => order.Lines).Include(order => order.GateHistory);

    public static async Task<ChangeOrderViewer> ViewerAsync(ChangeOrderServices services)
    {
        HashSet<Permission> held = [];
        foreach (Permission permission in ChangeOrderView.Permissions)
        {
            AuthorizationResult result = await services.Authorization.AuthorizeAsync(
                services.User, resource: null, PermissionRequirement.ToPolicyName(permission, ProjectScope.FromRoute()));
            if (result.Succeeded)
            {
                held.Add(permission);
            }
        }

        return new ChangeOrderViewer(services.CurrentUser.UserId ?? Guid.Empty, held);
    }

    public static async Task<IResult> RunAsync(
        Guid projectId, Guid id, ChangeOrderServices services, ChangeOrderStep step, CancellationToken cancellationToken)
    {
        KaffDbContext database = services.Database;

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM change_orders WHERE id = {id} AND project_id = {projectId} FOR UPDATE", cancellationToken);

        ChangeOrder? order = await WithChildren(database)
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);
        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (order is null || project is null)
        {
            return ResultExtensions.Problem(ChangeOrderErrors.NotFound);
        }

        Result stepped = step(order, project, services);
        if (stepped.IsFailure)
        {
            return ResultExtensions.Problem(stepped.Error);
        }

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        await transaction.CommitAsync(cancellationToken);

        ChangeOrderViewer viewer = await ViewerAsync(services);
        return Microsoft.AspNetCore.Http.Results.Ok(ChangeOrderView.Build(order, project, services.Rulings, viewer));
    }

    public static async Task<IResult> CreateAsync(
        Guid projectId,
        string? title,
        IReadOnlyList<ChangeOrderLineDraft> lines,
        ChangeOrderServices services,
        CancellationToken cancellationToken)
    {
        KaffDbContext database = services.Database;

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        int last = await database.ChangeOrders
            .Where(candidate => candidate.ProjectId == projectId)
            .Select(candidate => (int?)candidate.Number)
            .MaxAsync(cancellationToken) ?? 0;

        Result<ChangeOrder> created = ChangeOrder.Create(
            project, last + 1, title, lines, services.Rulings, Actor(services), Now(services));
        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        database.ChangeOrders.Add(created.Value);

        try
        {
            Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
            if (refused is not null)
            {
                return ResultExtensions.Problem(refused);
            }
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { ConstraintName: NumberIndex })
        {
            return ResultExtensions.Problem(ChangeOrderErrors.NumberTaken);
        }

        ChangeOrderViewer viewer = await ViewerAsync(services);
        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{projectId}/change-orders/{created.Value.Id}",
            ChangeOrderView.Build(created.Value, project, services.Rulings, viewer));
    }
}
