using Kaff.Domain.Common;

namespace Kaff.Domain.Accounting;

public sealed record ProjectSize(Guid ProjectId, Money Size);

public sealed record OverheadShare(Guid ProjectId, Money Size, Money Share);

/// <summary>
/// The analytical spread of company overhead across projects in proportion to size (spec.md section 6.10). It is a report:
/// nothing here writes a posting, so a project's actual cost stays clean. KAFF-708.
/// </summary>
public static class OverheadSpread
{
    /// <summary>
    /// Each project's share is its size over the total size, rounded to four decimals. The rounding leftover goes to the
    /// largest project, so the shares add up to the overhead exactly.
    /// </summary>
    public static IReadOnlyList<OverheadShare> Spread(Money overhead, IEnumerable<ProjectSize> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        List<ProjectSize> sized = [.. projects.Where(project => project.Size.IsPositive).OrderBy(project => project.ProjectId)];
        Money totalSize = Money.Sum(sized.Select(project => project.Size));

        if (sized.Count == 0)
        {
            return [];
        }

        List<OverheadShare> shares =
        [
            .. sized.Select(project => new OverheadShare(
                project.ProjectId,
                project.Size,
                new Money(overhead.Amount * project.Size.Amount / totalSize.Amount))),
        ];

        Money leftover = overhead - Money.Sum(shares.Select(share => share.Share));

        if (leftover.IsZero)
        {
            return shares;
        }

        OverheadShare largest = shares.MaxBy(share => share.Size)!;

        return [.. shares.Select(share => share == largest ? share with { Share = share.Share + leftover } : share)];
    }
}
