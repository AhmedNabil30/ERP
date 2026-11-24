namespace Kaff.Api.Features.Portal.ListProjects;

/// <summary>One of the client's own projects. Name, state and contract type, and nothing else: no code, no value, no team.</summary>
public sealed record PortalProjectSummary(Guid Id, string Name, string Status, string ContractType);
