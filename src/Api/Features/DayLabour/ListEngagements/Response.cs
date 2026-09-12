using Kaff.Domain.Common;

namespace Kaff.Api.Features.DayLabour.ListEngagements;

/// <summary>One engagement in a worker's history, on the route's project. KAFF-210.</summary>
public sealed record EngagementEntry(
    Guid Id,
    Guid ProjectId,
    DateOnly OpenedOn,
    DateOnly? ClosedOn,
    Money? DayRate,
    int? Rating);

/// <summary>
/// One worker's engagement history and the pool's three figures for him. KAFF-210 rule 2 — every
/// figure below is computed from <see cref="Items"/> at read time, never a stored column.
/// </summary>
/// <param name="AverageDayRate">
/// The average of the recorded rates among <see cref="Items"/>, or <c>null</c> when none carries one
/// — decisions.md D-153 §1 point 5: a money figure, gated the same as the rate itself.
/// </param>
/// <param name="Frequency">A count of engagements (D-139 §3), not days and not projects.</param>
/// <param name="AverageRating">The average of the recorded ratings, or <c>null</c> when none is rated.</param>
public sealed record Response(
    Guid WorkerId,
    IReadOnlyList<EngagementEntry> Items,
    Money? AverageDayRate,
    int Frequency,
    decimal? AverageRating);
