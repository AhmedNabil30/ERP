using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Projects.CreateProject;

/// <summary>
/// Opens a project and, in the same save, opens its account set. KAFF-400.
/// </summary>
/// <remarks>
/// <para>
/// <b>The account set opens in the same transaction as the project</b> — the story's own rule, and
/// spec.md §6.1's spirit: a project that exists with nowhere for its money to post is a project a
/// later request would 500 against. Both <c>Project</c> and its <see cref="Account"/> rows are added
/// to the same <c>KaffDbContext</c> before the single <c>SaveChangesAsync</c> call, so EF Core's
/// implicit transaction covers both — no second save, no window where one exists without the other.
/// <see cref="ProjectOpening.Open"/> builds the project and, through <c>ProjectAccountSetFactory.Create</c>, its rows (KAFF-302); nothing here bypasses
/// <see cref="Account.Create"/>'s own validation.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> Both the project and every account it opens are entity
/// changes the change tracker sees, so <c>AuditSaveChangesInterceptor</c> writes a <c>Created</c>
/// record for each, in the same transaction, under one correlation id — the same arrangement
/// <c>CreateClient</c> and <c>AssignUserToProject</c> already rely on.
/// </para>
/// <para>
/// <b>Lump Sum terms only</b>. Cost Plus has no fixed contract value (spec.md §5.2) and Design
/// computes its value from area × rate at contract signature — a later story. This endpoint opens the
/// project shell for every type and sets the Lump Sum commercial terms when that is the type, using
/// the request's <c>HoldPercentage</c>/<c>ClientAdvance</c> where given and spec.md's own stated
/// defaults for the rest (see <c>Request</c>'s remarks).
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);

        bool clientExists = await database.Clients
            .AnyAsync(client => client.Id == request.ClientId, cancellationToken);

        if (!clientExists)
        {
            return ResultExtensions.Problem(MasterDataErrors.ClientNotFound);
        }

        DateTimeOffset now = clock.GetUtcNow();
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);

        Result<OpenedProject> opened = ProjectOpening.Open(
            request.Name,
            request.ClientId,
            request.ContractType,
            request.ClientAdvance is { } advance ? Percentage.FromPercent(advance) : null,
            request.HoldPercentage,
            now);

        if (opened.IsFailure)
        {
            return ResultExtensions.Problem(opened.Error);
        }

        Project project = opened.Value.Project;

        if (request.Status == ProjectStatus.Active)
        {
            Result activated = project.TransitionTo(ProjectStatus.Active, today);

            if (activated.IsFailure)
            {
                return ResultExtensions.Problem(activated.Error);
            }
        }

        database.Projects.Add(project);
        database.Accounts.AddRange(opened.Value.Accounts);

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{project.Id}",
            new Response(
                project.Id,
                project.Code,
                project.Name,
                project.ClientId,
                project.ContractType,
                project.Status,
                project.ContractValue,
                project.HoldRate,
                project.AdvanceRate,
                project.CreatedAt));
    }
}
