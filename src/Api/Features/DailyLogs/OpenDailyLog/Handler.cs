using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.DailyLogs.OpenDailyLog;

/// <summary>
/// Opens the caller's log for the day the given time falls on, or reaches the one already open (KAFF-600 rule 1).
/// The log is a new row only the first time; the opening is audited by the interceptor, not by hand.
/// </summary>
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
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(clock);

        if (request.OccurredAt is not { } occurredAt)
        {
            return ResultExtensions.Problem(DailyLogErrors.OccurredAtRequired);
        }

        Result<(DailyLog Log, bool Created)> opened = await DailyLogAccess.OpenAsync(
            database, rulings, projectId, currentUser.UserId ?? Guid.Empty, occurredAt, clock.GetUtcNow(), cancellationToken);

        if (opened.IsFailure)
        {
            return ResultExtensions.Problem(opened.Error);
        }

        (DailyLog log, bool created) = opened.Value;
        var response = new Response(log.Id, log.LogDate);

        return created
            ? Microsoft.AspNetCore.Http.Results.Created($"/api/projects/{projectId}/daily-logs/mine?date={log.LogDate:yyyy-MM-dd}", response)
            : Microsoft.AspNetCore.Http.Results.Ok(response);
    }
}
