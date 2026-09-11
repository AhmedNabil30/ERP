namespace Kaff.Api.Features.Babs.MoveBab;

/// <summary>
/// Re-parents one باب. KAFF-205.
/// </summary>
/// <param name="ParentBabId">The new parent, or <c>null</c> to make the باب a root (<c>AC-205-B</c>).</param>
public sealed record Request(Guid? ParentBabId);
