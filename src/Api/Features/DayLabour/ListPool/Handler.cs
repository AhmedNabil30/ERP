using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DayLabour.ListPool;

/// <summary>
/// The company-wide day-labour pool, for a Site Engineer picking a worker. decisions.md D-140 point 4.
/// </summary>
/// <remarks>
/// <para>
/// <b>Company-wide, not filtered to the route's project.</b> D-140 point 3: the project is what
/// authorises the act; it does not own the worker. A worker registered on project A can be engaged on
/// project B by an engineer assigned to B.
/// </para>
/// <para>
/// <b><c>Kind == DayLabour</c> is filtered in the EF query</b>, not after loading — D-140's own wording
/// for this route.
/// </para>
/// <para>
/// <b>Frequency and average rating, joined from <c>Engagements</c>, company-wide</b> — decisions.md
/// D-153 §1 point 5's last bullet: not money, so they belong here rather than gated behind
/// <c>DayLabourRateManage</c>. Derived at read time (KAFF-210 rule 2); a worker with none is
/// <c>Frequency: 0</c> and <c>AverageRating: null</c>, never a fabricated average.
/// </para>
/// <para>No audit record. It is a read.</para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Dictionary<Guid, (int Frequency, int RatingSum, int RatingCount)> stats = await database.Engagements
            .GroupBy(engagement => engagement.WorkerId)
            .Select(group => new
            {
                WorkerId = group.Key,
                Frequency = group.Count(),
                RatingSum = group.Sum(engagement => engagement.Rating ?? 0),
                RatingCount = group.Count(engagement => engagement.Rating != null),
            })
            .ToDictionaryAsync(
                row => row.WorkerId, row => (row.Frequency, row.RatingSum, row.RatingCount), cancellationToken);

        var employees = await database.Employees
            .Where(employee => employee.Kind == EmployeeKind.DayLabour)
            .OrderBy(employee => employee.Code)
            .Select(employee => new
            {
                employee.Id,
                employee.Code,
                employee.FullName,
                employee.PhoneEntered,
                employee.BabId,
                employee.Specialty,
                employee.IsActive,
            })
            .ToListAsync(cancellationToken);

        List<PoolWorker> items = [.. employees.Select(employee =>
        {
            (int frequency, int ratingSum, int ratingCount) = stats.GetValueOrDefault(employee.Id);

            return new PoolWorker(
                employee.Id,
                employee.Code,
                employee.FullName,
                employee.PhoneEntered,
                employee.BabId,
                employee.Specialty,
                employee.IsActive,
                frequency,
                ratingCount == 0 ? null : (decimal)ratingSum / ratingCount);
        })];

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(items));
    }
}
