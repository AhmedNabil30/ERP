namespace Kaff.Api.Features.Portal.ListPhotos;

/// <summary>
/// A site photo Kaff chose to show the client (R5-B AC-B-6, R5-C AC-C-4). The photo's id, caption and the day it was taken. No
/// location, no author and no log reference travel with it.
/// </summary>
public sealed record PortalPhoto(Guid Id, string? Caption, DateTimeOffset TakenAt);
