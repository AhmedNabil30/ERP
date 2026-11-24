using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Results;
using Kaff.Domain.Accounting;
using Kaff.Domain.Authorization;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.SetBudgetBaseline;

/// <summary>
/// The Owner sets a project's budget baseline from its signed, frozen BOQ and names the tolerance. Once only: there is no edit and no delete
/// route, because nothing else may change a baseline (spec.md section 6.8). KAFF-709 AC1, AC3, AC4.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/budget-baseline";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, ([FromRoute] Guid projectId, Request request, KaffDbContext database, ICurrentUser currentUser, RulingSet rulings, TimeProvider clock, CancellationToken cancellationToken)
                => Handler.HandleAsync(projectId, request, database, currentUser, rulings, clock, cancellationToken))
            .RequirePermission(Permission.BudgetBaselineSet, ProjectScope.FromRoute())
            .WithName("SetBudgetBaseline")
            .WithTags("Accounting");
    }
}

/// <param name="TolerancePercent">Optional. 0 to 100. When missing the placeholder in <c>BudgetTolerance</c> applies.</param>
public sealed record Request(decimal? TolerancePercent);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Request request,
        KaffDbContext database,
        ICurrentUser currentUser,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(rulings);

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        Boq? boq = await database.Boqs
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, cancellationToken);

        bool alreadySet = await database.BudgetBaselines.AnyAsync(baseline => baseline.ProjectId == projectId, cancellationToken);

        Result<BudgetBaseline> set = BudgetBaseline.Set(
            project, boq, alreadySet, request.TolerancePercent, rulings, currentUser.UserId ?? Guid.Empty, clock.GetUtcNow());

        if (set.IsFailure)
        {
            return ResultExtensions.Problem(set.Error);
        }

        database.BudgetBaselines.Add(set.Value);

        Error? guard = await GuardedSave.SaveAsync(database, cancellationToken);
        if (guard is not null)
        {
            return ResultExtensions.Problem(guard);
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{projectId}/budget-baseline",
            GetBudgetBaseline.Handler.ToResponse(set.Value));
    }
}
