using Kaff.Api.Common.Results;
using Kaff.Domain.Boqs;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.CorrectQuantity;

/// <summary>
/// Corrects a saved quantity with a new signed delta entry that names the entry it corrects, and moves
/// <c>BoqLine.QuantityExecuted</c> by that delta under the same row lock a new quantity takes (D-218). No entry is updated.
/// The interceptor audits the new entry and the line.
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

        ExecutedQuantityEntry? original = await database.ExecutedQuantityEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == entryId && entry.ProjectId == projectId, cancellationToken);

        if (original is null)
        {
            return ResultExtensions.Problem(DailyLogErrors.EntryNotFound);
        }

        ProjectStatus status = await database.Projects.AsNoTracking()
            .Where(project => project.Id == projectId).Select(project => project.Status).FirstAsync(cancellationToken);

        if (!rulings.DailyLogProjectStates.Accepts(status))
        {
            return ResultExtensions.Problem(DailyLogErrors.ProjectStateClosedToLogs);
        }

        DailyLog log = await database.DailyLogs.FirstAsync(candidate => candidate.Id == original.DailyLogId, cancellationToken);

        Result allowed = CorrectionAccess.ForQuantity(
            log.LogDate,
            rulings.DailyLogEntry.SiteDate(now),
            rulings.DailyLogEntry,
            isAuthor: log.EngineerId == caller,
            isSupervisingEngineer: await DailyLogAccess.IsSupervisingEngineerAsync(database, projectId, caller, cancellationToken));

        if (allowed.IsFailure)
        {
            return ResultExtensions.Problem(allowed.Error);
        }

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);

        // The row lock, as for a new quantity: the corrected figure and the line move together.
        List<BoqLine> locked = await database.Set<BoqLine>()
            .FromSqlInterpolated($"SELECT * FROM boq_lines WHERE id = {original.BoqLineId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        BoqLine line = locked.Single();

        ExecutedQuantityEntry? replayed = await database.ExecutedQuantityEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == correctionId, cancellationToken);

        decimal currentFigure = original.Quantity + await database.ExecutedQuantityEntries.AsNoTracking()
            .Where(entry => entry.CorrectsEntryId == original.Id)
            .SumAsync(entry => entry.Quantity, cancellationToken);

        if (replayed is not null)
        {
            return replayed.CorrectsEntryId == original.Id
                ? Microsoft.AspNetCore.Http.Results.Ok(new Response(
                    replayed.Id, original.Id, currentFigure, replayed.CorrectedByUserId ?? Guid.Empty, replayed.CreatedAt))
                : ResultExtensions.Problem(DailyLogErrors.EntryIdTaken);
        }

        Result<ExecutedQuantityEntry> corrected =
            log.CorrectQuantity(correctionId, original, currentFigure, request.Quantity, caller, now);

        if (corrected.IsFailure)
        {
            return ResultExtensions.Problem(corrected.Error);
        }

        Result moved = line.ApplyExecutedChange(corrected.Value.Quantity);

        if (moved.IsFailure)
        {
            return ResultExtensions.Problem(moved.Error);
        }

        database.ExecutedQuantityEntries.Add(corrected.Value);

        Error? refused = await GuardedSave.SaveAsync(database, cancellationToken);

        if (refused is not null)
        {
            return ResultExtensions.Problem(refused);
        }

        await transaction.CommitAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{projectId}/daily-logs/mine?date={log.LogDate:yyyy-MM-dd}",
            new Response(correctionId, original.Id, request.Quantity, caller, now));
    }
}
