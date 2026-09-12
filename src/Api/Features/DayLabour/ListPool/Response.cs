namespace Kaff.Api.Features.DayLabour.ListPool;

/// <summary>One worker in the day-labour pool, for a Site Engineer picking a worker. KAFF-209/210.</summary>
/// <remarks>
/// <b>The allow-list decisions.md D-140 point 4 names, and nothing else.</b> No salaried row and no
/// staff-only field (<c>NationalId</c>, <c>JobTitle</c>, <c>HiredOn</c>, any money member) appears here
/// under any name — D-140 point 5. Never projected in the query, so never loaded.
/// </remarks>
public sealed record PoolWorker(
    Guid Id,
    string Code,
    string FullName,
    string Phone,
    Guid? BabId,
    string? Specialty,
    bool IsActive);

/// <summary>The company-wide day-labour pool. Empty when none exist — never null.</summary>
/// <param name="Items">Ordered by <c>Code</c>.</param>
public sealed record Response(IReadOnlyList<PoolWorker> Items);
