using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.ChangeOrders;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Extracts.PrepareExtract;

/// <summary>
/// Prepares an extract from executed quantities.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cumulative certified work is the sum, over the frozen BOQ's lines, of the line's rate times its executed
/// quantity</b>, rounded as <c>ContractValueSource</c> rules for a line total. Nothing is typed. The executed
/// quantity is the running total the daily logs maintain from period deltas.
/// </para>
/// <para>
/// <b>Previously certified work</b> is what the project's standing extracts billed plus what an open NCR still holds
/// (<c>Extract.PreviouslyCertifiedFor</c>, D-253), so a released remainder is billed here. That is only
/// meaningful when no other extract is in flight, so a second one is refused until the first is issued
/// (<c>extract.another_extract_open</c>). That refusal is ASSUMED, not ruled (D-194).
/// </para>
/// <para>
/// <b>The number</b> is the project's highest plus one. The unique index on (project, number) is the enforcement
/// of record: two simultaneous prepares get <c>extract.number_taken</c> for the loser, who retries.
/// </para>
/// </remarks>
internal static class Handler
{
    private const string NumberIndex = "ux_extracts_project_number";
    private const string ChangeOrderBilledIndex = "ux_extract_change_order_lines_change_order";

    /// <summary>
    /// The refusal a normal prepare (not a revision) would give on this project now, before anything is derived, or null.
    /// The project read asks it for its <c>prepareExtract</c> flag, so the control and the route cannot disagree.
    /// </summary>
    internal static async Task<Error?> RefusalAsync(KaffDbContext database, Project project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(project);

        if (!Extract.Prepares(project.ContractType))
        {
            return ExtractErrors.ContractTypeNotSupported;
        }

        Boq? boq = await database.Boqs.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.ProjectId == project.Id, cancellationToken);
        if (boq is null)
        {
            return BoqErrors.NotFound;
        }

        if (!boq.IsFrozen)
        {
            return DailyLogErrors.BoqNotFrozen;
        }

        List<(Guid Id, ExtractStatus Status, Guid? RevisesExtractId)> existing =
        [
            .. (await database.Extracts.AsNoTracking()
                    .Where(candidate => candidate.ProjectId == project.Id)
                    .Select(candidate => new { candidate.Id, candidate.Status, candidate.RevisesExtractId })
                    .ToListAsync(cancellationToken))
                .Select(candidate => (candidate.Id, candidate.Status, candidate.RevisesExtractId)),
        ];

        return AnotherExtractOpen(existing) ? ExtractErrors.AnotherExtractOpen : null;
    }

    /// <summary>
    /// A normal extract waits while another one is in flight. A revision in flight does not block it (D-242 item 4). A
    /// cancelled extract still waiting for its revision does: until the revision exists, the previously certified work of
    /// the next extract is not known.
    /// </summary>
    private static bool AnotherExtractOpen(IReadOnlyCollection<(Guid Id, ExtractStatus Status, Guid? RevisesExtractId)> existing)
    {
        HashSet<Guid> revised = [.. existing.Where(candidate => candidate.RevisesExtractId != null).Select(candidate => candidate.RevisesExtractId!.Value)];
        return existing.Any(candidate => candidate.RevisesExtractId == null && candidate.Status is not (
                ExtractStatus.Issued or ExtractStatus.Collected or ExtractStatus.Disputed or ExtractStatus.Void))
            || existing.Any(candidate => candidate.Status == ExtractStatus.Void && !revised.Contains(candidate.Id));
    }

    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Request request,
        KaffDbContext database,
        RulingSet rulings,
        TimeProvider clock,
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(currentUser);

        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (project is null)
        {
            return ResultExtensions.Problem(ProjectErrors.NotFound);
        }

        Boq? boq = await database.Boqs
            .AsNoTracking()
            .Include(candidate => candidate.Lines)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, cancellationToken);
        if (boq is null)
        {
            return ResultExtensions.Problem(BoqErrors.NotFound);
        }

        if (!boq.IsFrozen)
        {
            return ResultExtensions.Problem(DailyLogErrors.BoqNotFrozen);
        }

        var existing = await database.Extracts
            .AsNoTracking()
            .Where(candidate => candidate.ProjectId == projectId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Number,
                candidate.Status,
                candidate.PreviouslyCertifiedWork,
                candidate.PeriodStart,
                candidate.PeriodEnd,
                candidate.RevisesExtractId,
            })
            .ToListAsync(cancellationToken);

        HashSet<Guid> alreadyRevised = [.. existing.Where(candidate => candidate.RevisesExtractId != null).Select(candidate => candidate.RevisesExtractId!.Value)];
        int number = existing.Count == 0 ? 1 : existing.Max(candidate => candidate.Number) + 1;
        DateOnly periodStart = request.PeriodStart;
        DateOnly periodEnd = request.PeriodEnd;
        Money previous;

        if (request.RevisesExtractId is { } originalId)
        {
            // D-241 item 2: the revision of a cancelled extract covers the same period and starts where the original did.
            // D-242 item 4: it is the one exception to the one-extract-in-flight rule.
            var original = existing.FirstOrDefault(candidate => candidate.Id == originalId);
            if (original is null || original.Status != ExtractStatus.Void || alreadyRevised.Contains(originalId))
            {
                return ResultExtensions.Problem(ExtractErrors.RevisionTargetInvalid);
            }

            periodStart = original.PeriodStart;
            periodEnd = original.PeriodEnd;
            previous = original.PreviouslyCertifiedWork;
        }
        else
        {
            if (AnotherExtractOpen([.. existing.Select(candidate => (candidate.Id, candidate.Status, candidate.RevisesExtractId))]))
            {
                return ResultExtensions.Problem(ExtractErrors.AnotherExtractOpen);
            }

            // D-253: what the standing extracts billed, plus what an open NCR still holds. A released remainder is
            // left out, so this extract bills it. A Void extract's work was never certified and is not counted.
            previous = Extract.PreviouslyCertifiedFor(
                await database.Extracts
                    .AsNoTracking()
                    .Where(candidate => candidate.ProjectId == projectId && candidate.Status != ExtractStatus.Void)
                    .ToListAsync(cancellationToken));
        }

        Money cumulative = ExtractSupport.CumulativeCertifiedWork(boq, rulings);

        // KAFF-508: every approved change order not yet billed is offered, whole, at its approved price (ruling
        // ChangeOrderBillingBasis). Nothing is typed, and a change order in any earlier state is never offered.
        List<ChangeOrder> approved = await database.ChangeOrders
            .AsNoTracking()
            .Include(candidate => candidate.Lines)
            .Where(candidate => candidate.ProjectId == projectId && candidate.Status == ChangeOrderStatus.Approved)
            .OrderBy(candidate => candidate.Number)
            .ToListAsync(cancellationToken);
        Guid[] approvedIds = [.. approved.Select(candidate => candidate.Id)];
        HashSet<Guid> alreadyBilled =
        [
            .. await database.ExtractChangeOrderLines
                .AsNoTracking()
                .Where(line => approvedIds.Contains(line.ChangeOrderId))
                .Select(line => line.ChangeOrderId)
                .ToListAsync(cancellationToken),
        ];
        List<ExtractChangeOrderBilling> changeOrders =
        [
            .. approved.Where(candidate => !alreadyBilled.Contains(candidate.Id))
                .Select(candidate => new ExtractChangeOrderBilling(candidate.Id, candidate.Total)),
        ];

        Guid actor = currentUser.UserId ?? Guid.Empty;
        DateTimeOffset now = clock.GetUtcNow();

        // D-235 item 4: the material values are money, and the junior site engineer who prepares never enters money. The
        // supervising engineer enters them with the QC report; until then the extract carries none.
        // A revision bills no change order: the original billed none (Extract.CancelForRevision refuses one that did).
        Result<Extract> prepared = Extract.Prepare(
            project, number, periodStart, periodEnd, cumulative, previous,
            Money.Zero, Money.Zero, actor, now, request.RevisesExtractId is null ? changeOrders : [], request.RevisesExtractId);
        if (prepared.IsFailure)
        {
            return ResultExtensions.Problem(prepared.Error);
        }

        Extract extract = prepared.Value;
        database.Extracts.Add(extract);

        // D-248: each BOQ line's executed quantity is copied onto the extract, beside the same line on the previous one.
        Func<Guid, decimal?> previousOf = await ExtractQuantities.PreviousOfAsync(database, projectId, request.RevisesExtractId, cancellationToken);
        database.ExtractQuantityLines.AddRange(ExtractQuantityLine.Capture(extract.Id, take: 1, boq.Lines, previousOf));

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
            return ResultExtensions.Problem(ExtractErrors.NumberTaken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { ConstraintName: ChangeOrderBilledIndex })
        {
            return ResultExtensions.Problem(ExtractErrors.ChangeOrderAlreadyBilled);
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{projectId}/extracts/{extract.Id}",
            await ExtractSupport.ViewAsync(
                new StepServices(database, rulings, clock, currentUser, authorization, user), extract, project, cancellationToken));
    }
}
