namespace Kaff.Api.Features.Projects.SignProject;

/// <summary>
/// There is deliberately no contract value here. It is derived at signature from the BOQ, never typed.
/// </summary>
/// <param name="SignedOn">The signature date. Defaults to today when omitted.</param>
public sealed record Request(DateOnly? SignedOn);
