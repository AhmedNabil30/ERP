namespace Kaff.Api.Features.Portal.RejectChangeOrder;

/// <summary>The client's reason for rejecting. Required and stored with the rejection (CLAUDE.md: rejections carry a reason).</summary>
public sealed record Request(string? Reason);
