using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Infrastructure.Persistence;
using Kaff.Infrastructure.SitePhotos;
using Microsoft.AspNetCore.Http;

namespace Kaff.Api.Features.DailyLogs.RecordPhoto;

/// <summary>Records a site photo reference against the caller's log.</summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Request request,
        KaffDbContext database,
        ISitePhotoStore store,
        ICurrentUser currentUser,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(store);

        return await DailyLogAccess.RecordAsync(
            projectId,
            request.EntryId,
            request.OccurredAt,
            database,
            currentUser,
            rulings,
            clock,
            DailyLogAccess.EntryGate.Entry,
            (log, now) =>
            {
                Result<string?> reference = ReferenceOf(projectId, request.FileReference, store);

                return reference.IsFailure
                    ? Result.Failure<PhotoEntry>(reference.Error)
                    : log.RecordPhoto(request.EntryId!.Value, reference.Value, request.Caption, rulings.DailyLogEntry, request.OccurredAt!.Value, now);
            },
            cancellationToken);
    }

    /// <summary>
    /// A reference is the id <c>UploadPhoto</c> returned, and it must name a file this project holds. Anything else, a path
    /// included, is refused here, so the stored text is always a canonical id and never client-chosen text. A missing reference
    /// is left for the domain to refuse as required.
    /// </summary>
    private static Result<string?> ReferenceOf(Guid projectId, string? fileReference, ISitePhotoStore store)
    {
        if (string.IsNullOrWhiteSpace(fileReference))
        {
            return Result.Success<string?>(fileReference);
        }

        return Guid.TryParseExact(fileReference.Trim(), "D", out Guid id) && store.Exists(projectId, id)
            ? Result.Success<string?>(id.ToString())
            : Result.Failure<string?>(DailyLogErrors.FileReferenceInvalid);
    }
}
