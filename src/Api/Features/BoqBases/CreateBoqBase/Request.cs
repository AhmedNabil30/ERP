namespace Kaff.Api.Features.BoqBases.CreateBoqBase;

/// <summary>A name and, optionally, the first lines. No rate, hold or advance is accepted.</summary>
public sealed record Request(string? Name, IReadOnlyList<BoqBaseLineRequest>? Lines);
