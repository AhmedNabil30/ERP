using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.RecordQuantity;

/// <summary>
/// Records an executed quantity and moves <c>BoqLine.QuantityExecuted</c> (KAFF-600 rules 3 to 6).
/// </summary>
/// <remarks>
/// <para>
/// <b>The line is locked, not just read.</b> The executed quantity is a read-modify-write on a shared row, and
/// two engineers sync the same line at the same moment. Without <c>FOR UPDATE</c> the second writer overwrites
/// the first and a quantity is lost silently. The lock, the entry and the new total commit together.
/// </para>
/// <para>
/// What a second entry on the same line does is <c>RulingSet.DailyLogQuantityEntry</c>; this handler moves the
/// line by the difference between what the engineer's entries counted for before and after, so it needs no
/// edit when that is ruled.
/// </para>
/// </remarks>
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
        ArgumentNullException.ThrowIfNull(clock);

        Result envelope = DailyLogAccess.CheckEnvelope(request.EntryId, request.OccurredAt);

        if (envelope.IsFailure)
        {
            return ResultExtensions.Problem(envelope.Error);
        }

        if (request.BoqLineId is not { } lineId)
        {
            return ResultExtensions.Problem(DailyLogErrors.BoqLineNotFound);
        }

        Guid entryId = request.EntryId!.Value;
        DateTimeOffset occurredAt = request.OccurredAt!.Value;
        DateTimeOffset now = clock.GetUtcNow();

        Result<(DailyLog Log, bool Created)> opened = await DailyLogAccess.OpenAsync(
            database, rulings, projectId, currentUser.UserId ?? Guid.Empty, occurredAt, now, DailyLogAccess.EntryGate.Entry, cancellationToken);

        if (opened.IsFailure)
        {
            return ResultExtensions.Problem(opened.Error);
        }

        DailyLog log = opened.Value.Log;

        Boq? boq = await database.Boqs.FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, cancellationToken);

        if (boq is null)
        {
            return ResultExtensions.Problem(BoqErrors.NotFound);
        }

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        // The row lock. Everything after this line sees the line as the last committed writer left it.
        List<BoqLine> locked = await database.Set<BoqLine>()
            .FromSqlInterpolated($"SELECT * FROM boq_lines WHERE id = {lineId} AND boq_id = {boq.Id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        BoqLine? line = locked.SingleOrDefault();

        if (line is null)
        {
            return ResultExtensions.Problem(DailyLogErrors.BoqLineNotFound);
        }

        Result<ExecutedQuantityEntry?> replay =
            await DailyLogAccess.ReplayAsync<ExecutedQuantityEntry>(database, entryId, log, cancellationToken);

        if (replay.IsFailure)
        {
            return ResultExtensions.Problem(replay.Error);
        }

        if (replay.Value is { } stored)
        {
            return Microsoft.AspNetCore.Http.Results.Ok(new EntryResponse(stored.Id, log.Id, stored.OccurredAt));
        }

        Result<ExecutedQuantityEntry> recorded = log.RecordQuantity(entryId, boq, line, request.Quantity, rulings.DailyLogEntry, occurredAt, now);

        if (recorded.IsFailure)
        {
            return ResultExtensions.Problem(recorded.Error);
        }

        ExecutedQuantityEntry entry = recorded.Value;

        List<(DateTimeOffset OccurredAt, Guid EntryId, decimal Quantity)> earlier = await database.ExecutedQuantityEntries
            .AsNoTracking()
            .Where(candidate => candidate.DailyLogId == log.Id && candidate.BoqLineId == line.Id)
            .Select(candidate => new ValueTuple<DateTimeOffset, Guid, decimal>(candidate.OccurredAt, candidate.Id, candidate.Quantity))
            .ToListAsync(cancellationToken);

        decimal before = rulings.DailyLogQuantityEntry.Effective(earlier);
        decimal after = rulings.DailyLogQuantityEntry.Effective([.. earlier, (entry.OccurredAt, entry.Id, entry.Quantity)]);

        Result moved = line.ApplyExecutedChange(after - before);

        if (moved.IsFailure)
        {
            return ResultExtensions.Problem(moved.Error);
        }

        database.ExecutedQuantityEntries.Add(entry);

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);

        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        await transaction.CommitAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{projectId}/daily-logs/mine?date={log.LogDate:yyyy-MM-dd}",
            new EntryResponse(entry.Id, log.Id, entry.OccurredAt));
    }
}
