using Kaff.Domain.Boqs;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.GetMyLog;

/// <summary>
/// Builds the money-free read model of one daily log. Shared by the engineer's own read and the review read, so the two cannot
/// drift apart. Corrections are folded in: one row per saved quantity or crew entry, carrying the corrected figure.
/// </summary>
internal static class LogView
{
    public static async Task<Response> BuildAsync(
        KaffDbContext database,
        RulingSet rulings,
        ICurrentUser currentUser,
        TimeProvider clock,
        Guid logId,
        DateOnly date,
        bool includeBackDated,
        CancellationToken cancellationToken)
    {
        DailyLogEntryRule rule = rulings.DailyLogEntry;

        DailyLog log = await database.DailyLogs.AsNoTracking().FirstAsync(candidate => candidate.Id == logId, cancellationToken);
        DailyLogAccess.CorrectionRights rights = await DailyLogAccess.CorrectionRightsAsync(
            database, rulings, log, currentUser, clock.GetUtcNow(), cancellationToken);

        // A line is back-dated when the server received it on a later site day than the day it belongs to.
        bool? Flag(DateTimeOffset receivedAt) => includeBackDated ? date < rule.SiteDate(receivedAt) : null;

        List<FieldEntry> fields = await database.FieldEntries.AsNoTracking()
            .Where(entry => entry.DailyLogId == logId)
            .ToListAsync(cancellationToken);

        // Check-in and check-out are single-valued: the value in force is the latest OccurredAt, and the entry id breaks
        // a tie so arrival order never decides.
        DateTimeOffset? Current(DailyLogField field) => fields
            .Where(entry => entry.Field == field)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => (DateTimeOffset?)entry.OccurredAt)
            .FirstOrDefault();

        List<TextLine> TextLines(DailyLogField field) => [.. fields
            .Where(entry => entry.Field == field && !string.IsNullOrEmpty(entry.Text))
            .OrderBy(entry => entry.OccurredAt)
            .ThenBy(entry => entry.Id)
            .Select(entry => new TextLine(entry.Id, entry.Text!, entry.OccurredAt, Flag(entry.CreatedAt)))];

        var quantityRows = await (
            from entry in database.ExecutedQuantityEntries.AsNoTracking()
            join line in database.Set<BoqLine>().AsNoTracking() on entry.BoqLineId equals line.Id
            where entry.DailyLogId == logId
            orderby entry.OccurredAt, entry.CreatedAt
            select new { Entry = entry, line.ItemCode, line.DescriptionAr, line.Unit })
            .ToListAsync(cancellationToken);

        var labourRows = await (
            from entry in database.DayLabourEntries.AsNoTracking()
            join worker in database.Employees.AsNoTracking() on entry.EmployeeId equals worker.Id
            where entry.DailyLogId == logId
            orderby entry.OccurredAt, entry.CreatedAt
            select new { Entry = entry, worker.FullName })
            .ToListAsync(cancellationToken);

        List<Guid> correctors = [.. quantityRows.Select(row => row.Entry.CorrectedByUserId)
            .Concat(labourRows.Select(row => row.Entry.CorrectedByUserId))
            .Where(id => id is not null).Select(id => id!.Value).Distinct()];
        Dictionary<Guid, string> names = await database.Users.AsNoTracking()
            .Where(user => correctors.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        string? NameOf(Guid? id) => id is { } known && names.TryGetValue(known, out string? name) ? name : null;

        List<QuantityLine> quantities = [.. quantityRows
            .Where(row => row.Entry.CorrectsEntryId is null)
            .Select(row =>
            {
                List<ExecutedQuantityEntry> deltas = [.. quantityRows
                    .Where(other => other.Entry.CorrectsEntryId == row.Entry.Id).Select(other => other.Entry)];
                ExecutedQuantityEntry? last = deltas.OrderByDescending(delta => delta.CreatedAt).FirstOrDefault();
                return new QuantityLine(
                    row.Entry.Id, row.Entry.BoqLineId, row.ItemCode, row.DescriptionAr, row.Unit,
                    row.Entry.Quantity + deltas.Sum(delta => delta.Quantity), row.Entry.OccurredAt,
                    last is not null, last?.CorrectedByUserId, NameOf(last?.CorrectedByUserId), last?.CreatedAt,
                    rights.Quantity.IsSuccess, Flag(row.Entry.CreatedAt));
            })];

        List<DayLabourLine> labour = [.. labourRows
            .Where(row => row.Entry.CorrectsEntryId is null)
            .Select(row =>
            {
                List<DayLabourEntry> deltas = [.. labourRows
                    .Where(other => other.Entry.CorrectsEntryId == row.Entry.Id).Select(other => other.Entry)];
                DayLabourEntry? last = deltas.OrderByDescending(delta => delta.CreatedAt).FirstOrDefault();
                return new DayLabourLine(
                    row.Entry.Id, row.Entry.EmployeeId, row.FullName,
                    row.Entry.Hours + deltas.Sum(delta => delta.Hours), row.Entry.OccurredAt,
                    last is not null, last?.CorrectedByUserId, NameOf(last?.CorrectedByUserId), last?.CreatedAt,
                    rights.Crew.IsSuccess, Flag(row.Entry.CreatedAt));
            })];

        List<MaterialLine> materials = [.. (await database.MaterialEntries.AsNoTracking()
            .Where(entry => entry.DailyLogId == logId)
            .OrderBy(entry => entry.OccurredAt)
            .ToListAsync(cancellationToken))
            .Select(entry => new MaterialLine(
                entry.Id, entry.MaterialType, entry.Quantity, entry.Unit, entry.IsStockpile, entry.OccurredAt, Flag(entry.CreatedAt)))];

        List<PhotoLine> photos = [.. (await database.PhotoEntries.AsNoTracking()
            .Where(entry => entry.DailyLogId == logId)
            .OrderBy(entry => entry.OccurredAt)
            .ToListAsync(cancellationToken))
            .Select(entry => new PhotoLine(
                entry.Id, entry.FileReference, entry.Caption, entry.IsPublished, entry.OccurredAt, Flag(entry.CreatedAt)))];

        // Description and time only. The amount is never read into the model.
        List<ExpenseDraftLine> drafts = [.. (await database.SiteExpenseDrafts.AsNoTracking()
            .Where(entry => entry.DailyLogId == logId)
            .OrderBy(entry => entry.OccurredAt)
            .Select(entry => new { entry.Id, entry.Description, entry.OccurredAt, entry.CreatedAt })
            .ToListAsync(cancellationToken))
            .Select(entry => new ExpenseDraftLine(entry.Id, entry.Description, entry.OccurredAt, Flag(entry.CreatedAt)))];

        List<TextLine> notes = TextLines(DailyLogField.Note);
        List<TextLine> stoppages = TextLines(DailyLogField.StoppageReason);

        bool? anyBackDated = includeBackDated
            ? notes.Concat(stoppages).Any(line => line.BackDated == true)
                || quantities.Any(line => line.BackDated == true)
                || labour.Any(line => line.BackDated == true)
                || materials.Any(line => line.BackDated == true)
                || photos.Any(line => line.BackDated == true)
                || drafts.Any(line => line.BackDated == true)
            : null;

        return new Response(
            logId,
            date,
            Current(DailyLogField.CheckIn),
            Current(DailyLogField.CheckOut),
            notes,
            stoppages,
            quantities,
            labour,
            materials,
            photos,
            drafts,
            [.. fields
                .Where(entry => entry.Field is DailyLogField.CheckIn or DailyLogField.CheckOut)
                .OrderBy(entry => entry.OccurredAt)
                .Select(entry => new CheckInLine(
                    entry.Id, entry.Field, entry.OccurredAt, entry.CreatedAt,
                    entry.Latitude, entry.Longitude, entry.AccuracyMetres, entry.FlagReason))],
            anyBackDated);
    }
}
