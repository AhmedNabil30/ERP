using Kaff.Api.Common.Results;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.RecordCheckIn;

/// <summary>
/// Records a check-in or check-out against the caller's log. Never refuses for location (Q-207-3, Q-207-4): the
/// entry carries a flag instead. The first located check-in on a project sets its reference point in the same
/// save, so the project change is audited by the interceptor with the entry. The audit record for the entry itself
/// is written by the interceptor too.
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
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(rulings);

        // Tracked, so setting the reference point below saves and audits with the entry.
        // ponytail: two first check-ins racing could each set a reference; the last save wins. Lock the row if that matters.
        Project? project = await database.Projects.FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        return await DailyLogAccess.RecordAsync(
            projectId,
            request.EntryId,
            request.OccurredAt,
            database,
            currentUser,
            rulings,
            clock,
            (log, now) =>
            {
                var recorded = log.RecordCheckIn(
                    request.EntryId!.Value,
                    request.Kind,
                    request.Latitude,
                    request.Longitude,
                    request.AccuracyMetres,
                    project?.SiteReference,
                    rulings.CheckInDistance,
                    request.OccurredAt!.Value,
                    now);

                if (recorded.IsSuccess
                    && request.Kind == DailyLogField.CheckIn
                    && recorded.Value is { Latitude: { } latitude, Longitude: { } longitude })
                {
                    project?.SetSiteReferenceIfUnset(latitude, longitude);
                }

                return recorded;
            },
            cancellationToken);
    }
}
