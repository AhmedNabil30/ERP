using Kaff.Api.Features.DailyLogs.GetMyLog;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.ListDayLogs;

/// <summary>Every engineer log of one day on the project. A read: no audit record.</summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        DateOnly date,
        KaffDbContext database,
        ICurrentUser currentUser,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(rulings);
        ArgumentNullException.ThrowIfNull(clock);

        var logs = await (
            from log in database.DailyLogs.AsNoTracking()
            join engineer in database.Users.AsNoTracking() on log.EngineerId equals engineer.Id
            where log.ProjectId == projectId && log.LogDate == date
            orderby engineer.FullName
            select new { log.Id, log.EngineerId, engineer.FullName })
            .ToListAsync(cancellationToken);

        List<EngineerLog> result = [];

        foreach (var log in logs)
        {
            result.Add(new EngineerLog(
                log.EngineerId, log.FullName, await LogView.BuildAsync(database, rulings, currentUser, clock, log.Id, date, true, cancellationToken)));
        }

        return Microsoft.AspNetCore.Http.Results.Ok(result);
    }
}
