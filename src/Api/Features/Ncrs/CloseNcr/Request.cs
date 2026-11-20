namespace Kaff.Api.Features.Ncrs.CloseNcr;

/// <summary>Closes an NCR.</summary>
/// <param name="Note">Why the NCR closes: what was put right. Required, and stored on the NCR and its audit record.</param>
public sealed record Request(string? Note);
