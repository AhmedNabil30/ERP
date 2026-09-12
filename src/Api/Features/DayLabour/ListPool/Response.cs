namespace Kaff.Api.Features.DayLabour.ListPool;

/// <summary>One worker in the day-labour pool, for a Site Engineer picking a worker. KAFF-209/210.</summary>
/// <remarks>
/// <para>
/// <b>The allow-list decisions.md D-140 point 4 names, plus the two figures D-153 §1 point 5's last
/// bullet keeps money-free.</b> No salaried row and no staff-only field (<c>NationalId</c>,
/// <c>JobTitle</c>, <c>HiredOn</c>) appears here under any name — D-140 point 5. <b>No money member
/// either</b>: the average day rate is a pool figure and stays on the rate-gated
/// <c>ListEngagements</c> read alone (D-153 §1 point 5); <see cref="Frequency"/> and
/// <see cref="AverageRating"/> are not money, so they stay available to any assigned engineer here.
/// </para>
/// <para>
/// Both are derived from <c>Engagements</c> at read time, never a stored column — KAFF-210 rule 2.
/// </para>
/// </remarks>
/// <param name="Frequency">A count of engagements (D-139 §3), not days and not projects.</param>
/// <param name="AverageRating">
/// The average of the recorded ratings across every engagement, or <c>null</c> when none is rated —
/// KAFF-210 rule 8: an unrated man and a rating of zero are different facts.
/// </param>
public sealed record PoolWorker(
    Guid Id,
    string Code,
    string FullName,
    string Phone,
    Guid? BabId,
    string? Specialty,
    bool IsActive,
    int Frequency,
    decimal? AverageRating);

/// <summary>The company-wide day-labour pool. Empty when none exist — never null.</summary>
/// <param name="Items">Ordered by <c>Code</c>.</param>
public sealed record Response(IReadOnlyList<PoolWorker> Items);
