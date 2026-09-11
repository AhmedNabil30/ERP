namespace Kaff.Api.Features.Babs.MoveBab;

/// <summary>The moved باب, with its new parent. KAFF-205.</summary>
public sealed record Response(Guid Id, Guid? ParentBabId);
