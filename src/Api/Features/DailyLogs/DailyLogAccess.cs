using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.DailyLogs;

/// <summary>What every entry endpoint answers: which entry, on which log, at what site time. No money.</summary>
public sealed record EntryResponse(Guid Id, Guid LogId, DateTimeOffset OccurredAt);

/// <summary>
/// The steps every daily log endpoint shares: check the request's envelope, find or open the caller's log for
/// the entry's day, and recognise a replayed entry. The permission gate (role and assignment) has already run.
/// </summary>
internal static class DailyLogAccess
{
    public static Result CheckEnvelope(Guid? entryId, DateTimeOffset? occurredAt)
    {
        if (entryId is null || entryId == Guid.Empty)
        {
            return Result.Failure(DailyLogErrors.EntryIdRequired);
        }

        return occurredAt is null ? Result.Failure(DailyLogErrors.OccurredAtRequired) : Result.Success();
    }

    /// <summary>
    /// Finds the caller's log for the day <paramref name="occurredAt"/> falls on, opening it if absent. The day
    /// comes from the entry's own time, never from the server clock (KAFF-600 rule 11).
    /// </summary>
    public static async Task<Result<(DailyLog Log, bool Created)>> OpenAsync(
        KaffDbContext database,
        RulingSet rulings,
        Guid projectId,
        Guid engineerId,
        DateTimeOffset occurredAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ProjectStatus? status = await database.Projects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => (ProjectStatus?)project.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (status is null)
        {
            return Result.Failure<(DailyLog, bool)>(ProjectErrors.NotFound);
        }

        if (!rulings.DailyLogProjectStates.Accepts(status.Value))
        {
            return Result.Failure<(DailyLog, bool)>(DailyLogErrors.ProjectStateClosedToLogs);
        }

        DateOnly day = DailyLog.DayOf(occurredAt);

        DailyLog? existing = await FindAsync(database, projectId, engineerId, day, cancellationToken);

        if (existing is not null)
        {
            return Result.Success((existing, false));
        }

        DailyLog opened = DailyLog.Open(projectId, engineerId, occurredAt, now);
        database.DailyLogs.Add(opened);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return Result.Success((opened, true));
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two syncs of the same engineer's day raced; the other one opened it. It is the same log.
            database.Entry(opened).State = EntityState.Detached;
            DailyLog? winner = await FindAsync(database, projectId, engineerId, day, cancellationToken);

            return winner is null
                ? throw new InvalidOperationException("The unique daily log row vanished after a unique violation.", exception)
                : Result.Success((winner, false));
        }
    }

    /// <summary>The entry already stored under this client-generated id, when it is this log's own.</summary>
    public static async Task<Result<TEntry?>> ReplayAsync<TEntry>(
        KaffDbContext database, Guid entryId, DailyLog log, CancellationToken cancellationToken)
        where TEntry : LogEntry
    {
        TEntry? stored = await database.Set<TEntry>().AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == entryId, cancellationToken);

        if (stored is null)
        {
            return Result.Success<TEntry?>(null);
        }

        return stored.DailyLogId == log.Id
            ? Result.Success<TEntry?>(stored)
            : Result.Failure<TEntry?>(DailyLogErrors.EntryIdTaken);
    }

    /// <summary>
    /// The whole write for an entry that touches nothing but itself: open the log, recognise a replay, create
    /// the entry through the log, save. Created on first receipt, OK on a replay.
    /// </summary>
    public static async Task<IResult> RecordAsync<TEntry>(
        Guid projectId,
        Guid? entryId,
        DateTimeOffset? occurredAt,
        KaffDbContext database,
        ICurrentUser currentUser,
        RulingSet rulings,
        TimeProvider clock,
        Func<DailyLog, DateTimeOffset, Result<TEntry>> create,
        CancellationToken cancellationToken)
        where TEntry : LogEntry
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(create);

        Result envelope = CheckEnvelope(entryId, occurredAt);

        if (envelope.IsFailure)
        {
            return ResultExtensions.Problem(envelope.Error);
        }

        DateTimeOffset now = clock.GetUtcNow();

        Result<(DailyLog Log, bool Created)> opened = await OpenAsync(
            database, rulings, projectId, currentUser.UserId ?? Guid.Empty, occurredAt!.Value, now, cancellationToken);

        if (opened.IsFailure)
        {
            return ResultExtensions.Problem(opened.Error);
        }

        DailyLog log = opened.Value.Log;

        Result<TEntry?> replay = await ReplayAsync<TEntry>(database, entryId!.Value, log, cancellationToken);

        if (replay.IsFailure)
        {
            return ResultExtensions.Problem(replay.Error);
        }

        if (replay.Value is { } stored)
        {
            return Microsoft.AspNetCore.Http.Results.Ok(new EntryResponse(stored.Id, log.Id, stored.OccurredAt));
        }

        Result<TEntry> created = create(log, now);

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        database.Add(created.Value);

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);

        return refused is not null
            ? ResultExtensions.Problem(refused)
            : Microsoft.AspNetCore.Http.Results.Created(
                $"/api/projects/{projectId}/daily-logs/mine?date={log.LogDate:yyyy-MM-dd}",
                new EntryResponse(created.Value.Id, log.Id, created.Value.OccurredAt));
    }

    private static Task<DailyLog?> FindAsync(
        KaffDbContext database, Guid projectId, Guid engineerId, DateOnly day, CancellationToken cancellationToken) =>
        database.DailyLogs.FirstOrDefaultAsync(
            log => log.ProjectId == projectId && log.EngineerId == engineerId && log.LogDate == day,
            cancellationToken);
}
