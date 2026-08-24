namespace Kaff.Api.Features.Health.GetHealth;

/// <param name="Status">"healthy", "degraded" or "unhealthy". Machine-readable, not for display.</param>
/// <param name="DatabaseReachable">Whether PostgreSQL answered.</param>
/// <param name="GuardsInstalled">Whether the append-only and non-negative-balance guards are present.</param>
/// <param name="MissingGuards">Names of any guards that are absent. Empty when healthy.</param>
public sealed record Response(
    string Status,
    bool DatabaseReachable,
    bool GuardsInstalled,
    IReadOnlyList<string> MissingGuards);
