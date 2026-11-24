namespace Kaff.Api.Features.Opportunities.CreateOpportunity;

/// <summary>An opportunity opens on an existing client. The code is generated, never sent.</summary>
public sealed record Request(Guid ClientId, string? Title);
