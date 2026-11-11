using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.RecordDayLabour;

/// <summary>Records day labour hours against the caller's log.</summary>
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

        bool workerExists = request.EmployeeId is { } workerId
            && await database.Employees.AnyAsync(
                employee => employee.Id == workerId && employee.Kind == Kaff.Domain.MasterData.EmployeeKind.DayLabour,
                cancellationToken);

        if (!workerExists)
        {
            return ResultExtensions.Problem(DailyLogErrors.WorkerNotFound);
        }

        // What this man already counts for in the caller's log of that day, corrections included. The log may not exist yet.
        Guid engineerId = currentUser.UserId ?? Guid.Empty;
        DateOnly day = DailyLog.DayOf(request.OccurredAt ?? DateTimeOffset.UtcNow);
        decimal alreadyRecorded = await database.DayLabourEntries.AsNoTracking()
            .Where(entry => entry.EmployeeId == request.EmployeeId
                && entry.ProjectId == projectId
                && database.DailyLogs.Any(log => log.Id == entry.DailyLogId && log.EngineerId == engineerId && log.LogDate == day))
            .SumAsync(entry => entry.Hours, cancellationToken);

        return await DailyLogAccess.RecordAsync(
            projectId,
            request.EntryId,
            request.OccurredAt,
            database,
            currentUser,
            rulings,
            clock,
            DailyLogAccess.EntryGate.Entry,
            (log, now) => log.RecordDayLabour(
                request.EntryId!.Value, request.EmployeeId!.Value, request.Hours, alreadyRecorded, rulings.DailyLogEntry, request.OccurredAt!.Value, now),
            cancellationToken);
    }
}
