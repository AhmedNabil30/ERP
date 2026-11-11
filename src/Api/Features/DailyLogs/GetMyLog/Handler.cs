using Kaff.Api.Common.Results;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.GetMyLog;

/// <summary>
/// <c>GET /api/projects/{projectId}/daily-logs/mine?date=</c>. The caller's own log for one day. Another
/// engineer's log on the same project is not reachable: the lookup is by the caller's id. The back-dated flag is
/// included only when the caller is the supervising site engineer on the project (D-217 item 8). No audit record: it
/// is a read.
/// </summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        DateOnly date,
        KaffDbContext database,
        ICurrentUser currentUser,
        RulingSet rulings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(rulings);

        Guid engineerId = currentUser.UserId ?? Guid.Empty;

        Guid? logId = await database.DailyLogs
            .AsNoTracking()
            .Where(log => log.ProjectId == projectId && log.EngineerId == engineerId && log.LogDate == date)
            .Select(log => (Guid?)log.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (logId is not { } id)
        {
            return ResultExtensions.Problem(DailyLogErrors.NotFound);
        }

        bool supervising = await DailyLogAccess.IsSupervisingEngineerAsync(database, projectId, engineerId, cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(
            await LogView.BuildAsync(database, rulings, id, date, supervising, cancellationToken));
    }
}
