using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        ICurrentUser currentUser,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(currentUser);

        bool clientExists = await database.Clients
            .AnyAsync(client => client.Id == request.ClientId, cancellationToken);

        if (!clientExists)
        {
            return ResultExtensions.Problem(MasterDataErrors.ClientNotFound);
        }

        // KAFF-419, D-217 item 34: one name per client. Trimmed and case-insensitive, every project of the client counts. The
        // unique index ux_projects_client_name is the enforcement; this check turns the ordinary case into a keyed 409.
        string trimmedName = (request.Name ?? string.Empty).Trim();
        string lowered = trimmedName.ToLowerInvariant();

#pragma warning disable CA1304, CA1311, CA1862 // The comparison must run in PostgreSQL as lower(name) = @lowered, the expression ux_projects_client_name indexes. Only ToLower() translates; OrdinalIgnoreCase and ToLowerInvariant do not. The SQL lower() ignores the .NET culture.
        if (trimmedName.Length > 0
            && await database.Projects.AnyAsync(
                other => other.ClientId == request.ClientId && other.Name.ToLower() == lowered, cancellationToken))
        {
            return ResultExtensions.Problem(ProjectErrors.NameTakenForClient);
        }
#pragma warning restore CA1304, CA1311, CA1862

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

        // KAFF-419 AC-5, D-217 item 36 and D-227 item 1: Finance and the creator are staffed in the same save.
        Result staffed = await NewProjectStaffing.StaffAsync(database, currentUser, project.Id, now, cancellationToken);

        if (staffed.IsFailure)
        {
            return ResultExtensions.Problem(staffed.Error);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_projects_client_name",
        })
        {
            // Two requests passed the check above together; the loser gets the same refusal as everyone else.
            return ResultExtensions.Problem(ProjectErrors.NameTakenForClient);
        }

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
