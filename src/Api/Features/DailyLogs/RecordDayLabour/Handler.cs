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

        return await DailyLogAccess.RecordAsync(
            projectId,
            request.EntryId,
            request.OccurredAt,
            database,
            currentUser,
            rulings,
            clock,
            (log, now) => log.RecordDayLabour(request.EntryId!.Value, request.EmployeeId!.Value, request.Hours, request.OccurredAt!.Value, now),
            cancellationToken);
    }
}
