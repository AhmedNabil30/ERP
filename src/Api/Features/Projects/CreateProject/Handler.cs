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
/// <see cref="ProjectAccountSetFactory.Create"/> only builds the rows (KAFF-302); nothing here bypasses
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

        Result<Project> created = Project.Create(
            GenerateCode(),
            request.Name ?? string.Empty,
            request.ClientId,
            request.ContractType,
            now);

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        Project project = created.Value;

        if (request.ContractType == ContractType.LumpSum)
        {
            // spec.md §5.1, §15 and §16 assumption 1 — the two figures the request may adjust, and
            // spec.md's own stated defaults for the three it does not carry.
            Result termsSet = project.SetLumpSumTerms(
                new Money(request.ContractValue!.Value),
                Percentage.FromPercent(request.ClientAdvance ?? 25m),
                Percentage.FromPercent(request.HoldPercentage ?? 20m),
                Percentage.FromPercent(25m),
                Percentage.FromPercent(75m),
                delayPenaltyEnabled: false);

            if (termsSet.IsFailure)
            {
                return ResultExtensions.Problem(termsSet.Error);
            }
        }

        if (request.Status == ProjectStatus.Active)
        {
            Result activated = project.TransitionTo(ProjectStatus.Active, today);

            if (activated.IsFailure)
            {
                return ResultExtensions.Problem(activated.Error);
            }
        }

        Result<IReadOnlyList<Account>> accounts = ProjectAccountSetFactory.Create(
            project, existingTypes: new HashSet<AccountType>(), today);

        if (accounts.IsFailure)
        {
            return ResultExtensions.Problem(accounts.Error);
        }

        database.Projects.Add(project);
        database.Accounts.AddRange(accounts.Value);

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

    /// <summary>
    /// <c>PRJ-</c> plus eight hex characters from a fresh <see cref="Guid"/>.
    /// </summary>
    /// <remarks>
    /// <b>A named, deliberate simplification, not the house pattern.</b> <c>Client</c>, <c>Employee</c>,
    /// <c>Subcontractor</c> and <c>Supplier</c> all draw a sequential code from a PostgreSQL sequence —
    /// <c>KaffDbContext.ClientCodeSequence</c> and its siblings — because a read-max-and-increment loses
    /// a race between two concurrent registrations (decisions.md D-107 §1). There is no
    /// <c>project_code_seq</c> today, and this story ships no migration: CLAUDE.md and the dispatch
    /// brief both say to stop and report rather than write one. A <see cref="Guid"/>-derived code needs
    /// no sequence and cannot lose that race — collision odds are astronomically below a sequence's
    /// exhaustion — at the cost of a code that is not sequential and not customer-facing-readable the
    /// way <c>C-10001</c> is. Upgrade path: add <c>project_code_seq</c> in a migration once this story's
    /// codes need to read the same way.
    /// </remarks>
    private static string GenerateCode() =>
        "PRJ-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
