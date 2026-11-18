namespace Kaff.Api.Features.Health.GetHealth;

/// <param name="Status">"healthy", "degraded" or "unhealthy". Machine-readable, not for display.</param>
/// <param name="DatabaseReachable">Whether PostgreSQL answered.</param>
/// <param name="GuardsInstalled">Whether the append-only and non-negative-balance guards are present.</param>
/// <param name="MissingGuards">Names of any guards that are absent. Empty when healthy.</param>
/// <param name="LeastPrivilege">
/// Whether the application's own database role cannot switch the guards off: no superuser, not the owner of the
/// postings or the audit trail, no grant that changes or removes either (qa/slice-3/verification-2026-11-18.md F-1).
/// </param>
/// <param name="ExcessPrivileges">What the application's role holds that it must not. Empty when healthy.</param>
public sealed record Response(
    string Status,
    bool DatabaseReachable,
    bool GuardsInstalled,
    IReadOnlyList<string> MissingGuards,
    bool LeastPrivilege,
    IReadOnlyList<string> ExcessPrivileges);
