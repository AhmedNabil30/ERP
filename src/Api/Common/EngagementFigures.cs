using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Common;

/// <summary>A worker's times engaged and average rating. Neither is money (D-153 §1 point 5, D-232 item 4).</summary>
/// <param name="TimesEngaged">A count of engagements (D-139 §3), not days and not projects.</param>
/// <param name="AverageRating">The mean of the recorded ratings, or <c>null</c> when none is rated (KAFF-210 rule 8).</param>
public sealed record EngagementFigure(int TimesEngaged, decimal? AverageRating);

/// <summary>
/// The one computation of the two company-wide worker figures, read by the project pool
/// (<c>DayLabour/ListPool</c>) and by the people register (<c>Employees/ListEmployees</c>). Derived from
/// <c>Engagements</c> at read time, never stored (KAFF-210 rule 2).
/// </summary>
internal static class EngagementFigures
{
    /// <summary>One grouped query over every engagement. A worker with none is absent from the dictionary.</summary>
    public static async Task<Dictionary<Guid, EngagementFigure>> ReadAsync(
        KaffDbContext database, CancellationToken cancellationToken)
    {
        var rows = await database.Engagements
            .GroupBy(engagement => engagement.WorkerId)
            .Select(group => new
            {
                WorkerId = group.Key,
                Frequency = group.Count(),
                RatingSum = group.Sum(engagement => engagement.Rating ?? 0),
                RatingCount = group.Count(engagement => engagement.Rating != null),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.WorkerId,
            row => new EngagementFigure(
                row.Frequency, row.RatingCount == 0 ? null : (decimal)row.RatingSum / row.RatingCount));
    }
}
