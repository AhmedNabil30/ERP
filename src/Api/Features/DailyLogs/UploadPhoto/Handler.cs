using Kaff.Api.Common.Results;
using Kaff.Domain.DailyLogs;
using Kaff.Infrastructure.SitePhotos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace Kaff.Api.Features.DailyLogs.UploadPhoto;

/// <summary>What the client keeps and sends to <c>RecordPhoto</c> as the file reference, and where it can fetch the photo.</summary>
public sealed record Response(string Reference, string Url);

/// <summary>
/// Reads the body up to the configured cap, decides the type from the bytes (never from the client's content type or name) and
/// stores it under a server-generated id.
/// </summary>
/// <remarks>
/// ponytail: a file uploaded and never recorded stays on disk. It is unreachable (download needs an entry that names it) and
/// harmless; a cleanup job is added when the folder's size says so.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        HttpRequest request,
        ISitePhotoStore store,
        IOptions<SitePhotoOptions> options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);

        long max = options.Value.MaxBytes;

        // The body limit of this route only: the cap plus one byte, so reading past the cap is what Kestrel refuses.
        IHttpMaxRequestBodySizeFeature? limit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (limit is { IsReadOnly: false })
        {
            limit.MaxRequestBodySize = max + 1;
        }

        if (request.ContentLength > max)
        {
            return ResultExtensions.Problem(DailyLogErrors.PhotoTooLarge);
        }

        using var buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        int read;

        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > max)
            {
                return ResultExtensions.Problem(DailyLogErrors.PhotoTooLarge);
            }

            buffer.Write(chunk, 0, read);
        }

        ReadOnlyMemory<byte> bytes = buffer.GetBuffer().AsMemory(0, (int)buffer.Length);
        string? contentType = SitePhotoFormat.Sniff(bytes.Span);

        if (contentType is null)
        {
            return ResultExtensions.Problem(DailyLogErrors.PhotoNotAnImage);
        }

        // No location, device or author travels with the photo (R5-C AC-C-4): strip before it is stored.
        Guid photoId = await store.SaveAsync(projectId, SitePhotoMetadata.Strip(bytes.Span, contentType), contentType, cancellationToken);
        string reference = photoId.ToString();
        string url = PhotoRoute.Of(projectId, reference);

        return Microsoft.AspNetCore.Http.Results.Created(url, new Response(reference, url));
    }
}
