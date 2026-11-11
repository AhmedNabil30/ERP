namespace Kaff.Api.Features.BoqBases.UpdateBoqBase;

/// <summary>The whole base as the editor shows it: the name and every line. Lines not listed are removed.</summary>
public sealed record Request(string? Name, IReadOnlyList<BoqBaseLineRequest>? Lines);
