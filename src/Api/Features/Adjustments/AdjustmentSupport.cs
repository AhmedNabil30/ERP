using Kaff.Api.Common.Results;
using Kaff.Api.Features.Extracts;
using Kaff.Domain.Adjustments;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Adjustments;

/// <summary>An adjustment as read. The postings it wrote trace to it through their source document.</summary>
/// <param name="DisputeReason">For a credit note raised on a disputed extract: the client's dispute reason. Filled on the reads.</param>
/// <param name="WaitingOn">Who a Raised adjustment waits on, and for what. Null once decided, and on the write routes' answers.</param>
/// <param name="ExtractDueAfter">
/// For a credit note on an issued extract: what the extract expects after this note («بعد الإشعار», D-260 item 4). While
/// Raised it is the extract's expected figure less this note; once Approved the expected figure already counts it. Derived
/// from the same collection position the extract's read shows. Null otherwise, and on the write routes' answers.
/// </param>
public sealed record AdjustmentResponse(
    Guid Id,
    Guid ProjectId,
    int Number,
    AdjustmentType Type,
    AdjustmentStatus Status,
    string Reason,
    SourceDocumentType SourceDocumentType,
    Guid SourceDocumentId,
    string? SourceDocumentReference,
    Guid TargetAccountId,
    Money Amount,
    Guid RaisedByUserId,
    DateTimeOffset RaisedAt,
    Guid? DecidedByUserId,
    DateTimeOffset? DecidedAt,
    string? DecisionReason,
    string? DisputeReason = null,
    WaitingLineResponse? WaitingOn = null,
    AdjustmentActions? Actions = null,
    Money? ExtractDueAfter = null)
{
    internal static AdjustmentResponse From(Adjustment adjustment) => new(
        adjustment.Id,
        adjustment.ProjectId,
        adjustment.Number,
        adjustment.Type,
        adjustment.Status,
        adjustment.Reason,
        adjustment.SourceDocumentType,
        adjustment.SourceDocumentId,
        adjustment.SourceDocumentReference,
        adjustment.TargetAccountId,
        adjustment.Amount,
        adjustment.RaisedByUserId,
        adjustment.RaisedAt,
        adjustment.DecidedByUserId,
        adjustment.DecidedAt,
        adjustment.DecisionReason);
}

/// <summary>
/// What this caller may decide on the adjustment now (D-246). Each flag is <see cref="Adjustment.EnsureMayDecide"/>, the
/// check the decision itself makes, for a <c>FinancialMovementApprove</c> holder on the route's project.
/// </summary>
public sealed record AdjustmentActions(bool Approve, bool Reject, bool Reverse);

/// <summary>The adjustment reads: each adjustment with its extract's dispute reason, its waiting line and the caller's flags.</summary>
internal static class AdjustmentReads
{
    /// <param name="viewerId">Who is reading.</param>
    /// <param name="mayApprove">Whether the viewer holds <c>FinancialMovementApprove</c> on the route's project: role and assignment.</param>
    public static async Task<IReadOnlyList<AdjustmentResponse>> BuildAsync(
        KaffDbContext database,
        Guid projectId,
        IReadOnlyList<Adjustment> adjustments,
        Guid viewerId,
        bool mayApprove,
        CancellationToken cancellationToken)
    {
        Guid[] extractIds =
        [
            .. adjustments.Where(adjustment => adjustment.SourceDocumentType == SourceDocumentType.Extract)
                .Select(adjustment => adjustment.SourceDocumentId).Distinct(),
        ];
        Dictionary<Guid, Extract> extracts = await database.Extracts
            .AsNoTracking()
            .Include(extract => extract.GateHistory)
            .Include(extract => extract.Figures)
            .Where(extract => extractIds.Contains(extract.Id))
            .ToDictionaryAsync(extract => extract.Id, cancellationToken);

        // ponytail: one position read per extract with a standing credit note; batch it if a project carries dozens.
        Dictionary<Guid, Money> expected = [];
        foreach (Guid extractId in adjustments
            .Where(adjustment => adjustment.Status is AdjustmentStatus.Raised or AdjustmentStatus.Approved)
            .Select(adjustment => adjustment.SourceDocumentId)
            .Distinct())
        {
            if (extracts.GetValueOrDefault(extractId) is { } extract
                && await ExtractCollections.StateAsync(database, extract, cancellationToken) is { } state)
            {
                expected[extractId] = state.Position.Expected;
            }
        }
        IReadOnlyList<StaffMember> staff = adjustments.Any(adjustment => adjustment.Status == AdjustmentStatus.Raised)
            ? await ExtractWaitingOnView.StaffAsync(database, projectId, cancellationToken)
            : [];

        return
        [
            .. adjustments.Select(adjustment =>
            {
                Extract? extract = adjustment.SourceDocumentType == SourceDocumentType.Extract
                    ? extracts.GetValueOrDefault(adjustment.SourceDocumentId)
                    : null;
                IReadOnlyCollection<Guid> disputeActors = extract?.DisputeActors() ?? [];

                // The Owner decides a Raised adjustment; whoever raised it or the dispute behind it may not (Adjustment.Approve).
                WaitingLineResponse? waiting = adjustment.Status == AdjustmentStatus.Raised
                    ? WaitingLine.ForCreditNote(adjustment, extract, staff)
                    : null;

                bool May(AdjustmentStatus target) => mayApprove
                    && adjustment.EnsureMayDecide(target, viewerId, disputeActors, extract?.Status == ExtractStatus.Collected).IsSuccess;

                return AdjustmentResponse.From(adjustment) with
                {
                    DisputeReason = extract?.GateHistory.LastOrDefault(record => record.Gate == ExtractGate.Dispute)?.Reason,
                    WaitingOn = waiting,
                    Actions = new AdjustmentActions(May(AdjustmentStatus.Approved), May(AdjustmentStatus.Rejected), May(AdjustmentStatus.Reversed)),
                    ExtractDueAfter = expected.TryGetValue(adjustment.SourceDocumentId, out Money due)
                        ? adjustment.Status switch
                        {
                            AdjustmentStatus.Raised => due - adjustment.Amount,
                            AdjustmentStatus.Approved => due,
                            _ => null,
                        }
                        : null,
                };
            }),
        ];
    }
}

/// <summary>
/// The spine shared by every adjustment endpoint and by the dispute resolution that raises a credit note: one place
/// that raises, so the dispute route and the adjustment route cannot disagree on the shape.
/// </summary>
internal static class AdjustmentSupport
{
    private const string NumberIndex = "ux_adjustments_project_number";

    public static DateOnly Today(TimeProvider clock) => SiteTime.Today(clock);

    /// <summary>
    /// Raises a client credit note against <paramref name="extract"/>. Adds it to the context unsaved on success. The
    /// caller holds the transaction; the project row is locked here so two concurrent raises cannot take one number.
    /// </summary>
    public static async Task<Result<Adjustment>> RaiseCreditNoteAsync(
        KaffDbContext database,
        Project project,
        Extract extract,
        Guid? targetAccountId,
        Money amount,
        string? reason,
        RulingSet rulings,
        Guid actor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM projects WHERE id = {project.Id} FOR UPDATE", cancellationToken);

        Account? target = targetAccountId is { } id
            ? await database.Accounts.FirstOrDefaultAsync(account => account.Id == id, cancellationToken)
            : null;

        int number = (await database.Adjustments
            .Where(candidate => candidate.ProjectId == project.Id)
            .Select(candidate => (int?)candidate.Number)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        Result<Adjustment> raised = Adjustment.RaiseCreditNote(project, number, extract, target, amount, reason, rulings, actor, now);
        if (raised.IsSuccess)
        {
            database.Adjustments.Add(raised.Value);
        }

        return raised;
    }

    /// <summary>Whoever raised or resolved the dispute behind the adjustment, so they cannot decide on its credit note.</summary>
    public static async Task<IReadOnlyCollection<Guid>> DisputeActorsAsync(
        KaffDbContext database, Adjustment adjustment, CancellationToken cancellationToken)
    {
        if (adjustment.SourceDocumentType != SourceDocumentType.Extract)
        {
            return [];
        }

        Extract? extract = await database.Extracts
            .Include(candidate => candidate.GateHistory)
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == adjustment.SourceDocumentId, cancellationToken);
        return extract?.DisputeActors() ?? [];
    }

    /// <summary>
    /// Locks the adjustment row, runs one decision and commits the postings it produced and the status change in one
    /// save. Role and assignment were checked on the route. Audit records come from the interceptor on the same save.
    /// </summary>
    public static async Task<IResult> RunAsync(
        Guid projectId,
        Guid id,
        KaffDbContext database,
        Func<Adjustment, Project, CancellationToken, Task<Result<IReadOnlyList<Posting>>>> decide,
        CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM adjustments WHERE id = {id} AND project_id = {projectId} FOR UPDATE", cancellationToken);

        Adjustment? adjustment = await database.Adjustments
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.ProjectId == projectId, cancellationToken);
        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);
        if (adjustment is null || project is null)
        {
            return ResultExtensions.Problem(AdjustmentErrors.NotFound);
        }

        Result<IReadOnlyList<Posting>> decided = await decide(adjustment, project, cancellationToken);
        if (decided.IsFailure)
        {
            return ResultExtensions.Problem(decided.Error);
        }

        database.Postings.AddRange(decided.Value);

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);
        if (refused is null)
        {
            try
            {
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception exception) when (DatabaseGuardTranslation.Translate(exception) is { } violation)
            {
                refused = violation;
            }
        }

        return refused is not null
            ? ResultExtensions.Problem(refused)
            : Microsoft.AspNetCore.Http.Results.Ok(AdjustmentResponse.From(adjustment));
    }

    /// <summary>Saves a newly raised adjustment, turning a lost number race into its own conflict.</summary>
    public static async Task<Error?> SaveNewAsync(KaffDbContext database, CancellationToken cancellationToken)
    {
        try
        {
            return await GuardedSave.SaveAsync(database, cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { ConstraintName: NumberIndex })
        {
            return AdjustmentErrors.NumberTaken;
        }
    }
}
