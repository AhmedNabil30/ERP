using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DailyLogs.RecordPhoto;

/// <summary>Records a site photo reference against the caller's log.</summary>
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

        return await DailyLogAccess.RecordAsync(
            projectId,
            request.EntryId,
            request.OccurredAt,
            database,
            currentUser,
            rulings,
            clock,
            (log, now) => log.RecordPhoto(request.EntryId!.Value, request.FileReference, request.Caption, request.OccurredAt!.Value, now),
            cancellationToken);
    }
}
