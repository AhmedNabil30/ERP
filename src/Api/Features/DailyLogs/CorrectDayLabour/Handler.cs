using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.CorrectDayLabour;

/// <summary>
/// Corrects saved crew hours with a new signed delta entry that names the entry it corrects (D-218, KAFF-615 AC-11). No entry
/// is updated, and no money moves: hours carry no rate. The interceptor audits the new entry.
/// </summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid entryId,
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

        if (request.CorrectionId is not { } correctionId || correctionId == Guid.Empty)
        {
            return ResultExtensions.Problem(DailyLogErrors.EntryIdRequired);
        }

        Guid caller = currentUser.UserId ?? Guid.Empty;
        DateTimeOffset now = clock.GetUtcNow();

        DayLabourEntry? original = await database.DayLabourEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == entryId && entry.ProjectId == projectId, cancellationToken);

        if (original is null)
        {
            return ResultExtensions.Problem(DailyLogErrors.EntryNotFound);
        }

        DailyLog log = await database.DailyLogs.FirstAsync(candidate => candidate.Id == original.DailyLogId, cancellationToken);

        Result allowed = (await DailyLogAccess.CorrectionRightsAsync(database, rulings, log, currentUser, now, cancellationToken)).Crew;

        if (allowed.IsFailure)
        {
            return ResultExtensions.Problem(allowed.Error);
        }

        // ponytail: two corrections of the same entry racing could both read the same current figure. Crew hours move no
        // money, so there is no row lock here; add one as the quantity path has if that ever matters.
        DayLabourEntry? replayed = await database.DayLabourEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == correctionId, cancellationToken);

        decimal currentFigure = original.Hours + await database.DayLabourEntries.AsNoTracking()
            .Where(entry => entry.CorrectsEntryId == original.Id)
            .SumAsync(entry => entry.Hours, cancellationToken);

        if (replayed is not null)
        {
            return replayed.CorrectsEntryId == original.Id
                ? Microsoft.AspNetCore.Http.Results.Ok(new Response(
                    replayed.Id, original.Id, currentFigure, replayed.CorrectedByUserId ?? Guid.Empty, replayed.CreatedAt))
                : ResultExtensions.Problem(DailyLogErrors.EntryIdTaken);
        }

        // What this man other entries in the log count for, so the day total stays within the limit.
        decimal totalForMan = await database.DayLabourEntries.AsNoTracking()
            .Where(entry => entry.DailyLogId == log.Id && entry.EmployeeId == original.EmployeeId)
            .SumAsync(entry => entry.Hours, cancellationToken);

        Result<DayLabourEntry> corrected = log.CorrectDayLabour(
            correctionId, original, currentFigure, request.Hours, totalForMan - currentFigure, rulings.DailyLogEntry, caller, now);

        if (corrected.IsFailure)
        {
            return ResultExtensions.Problem(corrected.Error);
        }

        database.DayLabourEntries.Add(corrected.Value);

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);

        return refused is not null
            ? ResultExtensions.Problem(refused)
            : Microsoft.AspNetCore.Http.Results.Created(
                $"/api/projects/{projectId}/daily-logs/mine?date={log.LogDate:yyyy-MM-dd}",
                new Response(correctionId, original.Id, request.Hours, caller, now));
    }
}
