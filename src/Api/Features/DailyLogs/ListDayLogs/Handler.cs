using Kaff.Api.Features.DailyLogs.GetMyLog;
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
        RulingSet rulings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);

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
                log.EngineerId, log.FullName, await LogView.BuildAsync(database, rulings, log.Id, date, true, cancellationToken)));
        }

        return Microsoft.AspNetCore.Http.Results.Ok(result);
    }
}
