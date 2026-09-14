using Kaff.Api.Common.Results;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Departments.ListDepartments;

/// <summary>
/// Returns departments, flat, ordered by <c>NameEn</c>. KAFF-321.
/// </summary>
/// <remarks>
/// <para>
/// <b>Archived departments are excluded by default and reachable on request</b> — AC-321-E, same shape
/// as <c>ListBabs.Handler</c>. A user-assignment dropdown calls this with the default filter; the
/// settings screen's own archive tab asks for <c>status=archived</c> or <c>status=all</c>.
/// </para>
/// <para><b>No audit record.</b> It is a read; there is nothing to log.</para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        CancellationToken cancellationToken,
        string? status = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (!DepartmentListFilterParsing.TryParse(status, out DepartmentListFilter filter))
        {
            return ResultExtensions.Problem(MasterDataErrors.DepartmentListFilterUnknown);
        }

        IQueryable<Department> query = database.Departments;

        query = filter switch
        {
            DepartmentListFilter.Active => query.Where(department => department.IsActive),
            DepartmentListFilter.Archived => query.Where(department => !department.IsActive),
            _ => query,
        };

        List<DepartmentSummary> items = await query
            .OrderBy(department => department.NameEn)
            .Select(department => new DepartmentSummary(
                department.Id, department.NameAr, department.NameEn, department.IsActive))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(items));
    }
}
