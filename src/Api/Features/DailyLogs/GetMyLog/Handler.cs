using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.GetMyLog;

/// <summary>
/// <c>GET /api/projects/{projectId}/daily-logs/mine?date=</c>. The caller's own log for one day. Another
/// engineer's log on the same project is not reachable: the lookup is by the caller's id. No audit record: it
/// is a read.
/// </summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        DateOnly date,
        KaffDbContext database,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(currentUser);

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

        List<FieldEntry> fields = await database.FieldEntries.AsNoTracking()
            .Where(entry => entry.DailyLogId == id)
            .ToListAsync(cancellationToken);

        // The value in force is the latest OccurredAt; the entry id breaks a tie so arrival order never decides.
        FieldEntry? Current(DailyLogField field) => fields
            .Where(entry => entry.Field == field)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .FirstOrDefault();

        List<QuantityLine> quantities = await (
            from entry in database.ExecutedQuantityEntries.AsNoTracking()
            join line in database.Set<BoqLine>().AsNoTracking() on entry.BoqLineId equals line.Id
            where entry.DailyLogId == id
            orderby entry.OccurredAt
            select new QuantityLine(entry.Id, line.Id, line.ItemCode, line.DescriptionAr, line.Unit, entry.Quantity, entry.OccurredAt))
            .ToListAsync(cancellationToken);

        List<DayLabourLine> labour = await (
            from entry in database.DayLabourEntries.AsNoTracking()
            join worker in database.Employees.AsNoTracking() on entry.EmployeeId equals worker.Id
            where entry.DailyLogId == id
            orderby entry.OccurredAt
            select new DayLabourLine(entry.Id, worker.Id, worker.FullName, entry.Hours, entry.OccurredAt))
            .ToListAsync(cancellationToken);

        List<MaterialLine> materials = await database.MaterialEntries.AsNoTracking()
            .Where(entry => entry.DailyLogId == id)
            .OrderBy(entry => entry.OccurredAt)
            .Select(entry => new MaterialLine(entry.Id, entry.MaterialType, entry.Quantity, entry.Unit, entry.IsStockpile, entry.OccurredAt))
            .ToListAsync(cancellationToken);

        List<PhotoLine> photos = await database.PhotoEntries.AsNoTracking()
            .Where(entry => entry.DailyLogId == id)
            .OrderBy(entry => entry.OccurredAt)
            .Select(entry => new PhotoLine(entry.Id, entry.FileReference, entry.Caption, entry.IsPublished, entry.OccurredAt))
            .ToListAsync(cancellationToken);

        // Description and time only. The amount column is never selected.
        List<ExpenseDraftLine> drafts = await database.SiteExpenseDrafts.AsNoTracking()
            .Where(entry => entry.DailyLogId == id)
            .OrderBy(entry => entry.OccurredAt)
            .Select(entry => new ExpenseDraftLine(entry.Id, entry.Description, entry.OccurredAt))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(
            id,
            date,
            Current(DailyLogField.CheckIn)?.OccurredAt,
            Current(DailyLogField.CheckOut)?.OccurredAt,
            Current(DailyLogField.Weather)?.Text,
            Current(DailyLogField.Note)?.Text,
            Current(DailyLogField.StoppageReason)?.Text,
            quantities,
            labour,
            materials,
            photos,
            drafts));
    }
}
