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
/// <para>No audit record. It is a read.</para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<PoolWorker> items = await database.Employees
            .Where(employee => employee.Kind == EmployeeKind.DayLabour)
            .OrderBy(employee => employee.Code)
            .Select(employee => new PoolWorker(
                employee.Id,
                employee.Code,
                employee.FullName,
                employee.PhoneEntered,
                employee.BabId,
                employee.Specialty,
                employee.IsActive))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(items));
    }
}
