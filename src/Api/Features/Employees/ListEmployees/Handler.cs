using Kaff.Api.Common.Results;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Employees.ListEmployees;

/// <summary>
/// Returns employees, flat, ordered by <c>Code</c>. KAFF-207.
/// </summary>
/// <remarks>
/// <para>
/// <b>Archived employees are excluded by default and reachable on request</b> — <c>AC-207-F</c>, the
/// same three-state shape <c>ListBabs</c> and <c>ListCatalogueItems</c> use.
/// </para>
/// <para>
/// <b>An optional <c>kind</c> filter</b> lets a caller ask for one population alone — the salaried
/// staff register (<c>S-023</c>) and the day-labour pool are two screens over one store (KAFF-207 rule
/// 2), and this is the one query both read from.
/// </para>
/// <para>
/// <b>No audit record.</b> It is a read; there is nothing to log.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        CancellationToken cancellationToken,
        string? status = null,
        EmployeeKind? kind = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (!EmployeeListFilterParsing.TryParse(status, out EmployeeListFilter filter))
        {
            return ResultExtensions.Problem(MasterDataErrors.EmployeeListFilterUnknown);
        }

        IQueryable<Employee> query = database.Employees;

        query = filter switch
        {
            EmployeeListFilter.Active => query.Where(employee => employee.IsActive),
            EmployeeListFilter.Archived => query.Where(employee => !employee.IsActive),
            _ => query,
        };

        if (kind is not null)
        {
            query = query.Where(employee => employee.Kind == kind);
        }

        List<EmployeeSummary> items = await query
            .OrderBy(employee => employee.Code)
            .Select(employee => new EmployeeSummary(
                employee.Id,
                employee.Code,
                employee.FullName,
                employee.PhoneEntered,
                employee.Kind,
                employee.BabId,
                employee.Specialty,
                employee.IsActive))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(items));
    }
}
