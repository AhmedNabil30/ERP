using Kaff.Api.Common.Results;
using Kaff.Domain.Adjustments;
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
    string? DecisionReason)
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
/// The spine shared by every adjustment endpoint and by the dispute resolution that raises a credit note: one place
/// that raises, so the dispute route and the adjustment route cannot disagree on the shape.
/// </summary>
internal static class AdjustmentSupport
{
    private const string NumberIndex = "ux_adjustments_project_number";

    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

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

        // Money is a complex type EF cannot sum in SQL, so the amounts are read and added in memory.
        List<Money> credited = await database.Adjustments
            .Where(candidate => candidate.SourceDocumentType == SourceDocumentType.Extract
                && candidate.SourceDocumentId == extract.Id
                && (candidate.Status == AdjustmentStatus.Raised || candidate.Status == AdjustmentStatus.Approved))
            .Select(candidate => candidate.Amount)
            .ToListAsync(cancellationToken);

        int number = (await database.Adjustments
            .Where(candidate => candidate.ProjectId == project.Id)
            .Select(candidate => (int?)candidate.Number)
            .MaxAsync(cancellationToken) ?? 0) + 1;

        Result<Adjustment> raised = Adjustment.RaiseCreditNote(
            project, number, extract, target, amount, Money.Sum(credited), reason, rulings, actor, now);
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
