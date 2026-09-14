using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Departments.EditDepartment;

/// <summary>
/// Corrects one department's Arabic and English names, and leaves the before-state in the trail.
/// KAFF-321, AC-321-C.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every guard is the entity's.</b> <c>Department.Rename</c> refuses what <c>Create</c> would have
/// refused; none of it is repeated here — same shape as <c>EditBab.Handler</c>.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>AuditSaveChangesInterceptor</c> writes the
/// <c>Modified</c> record with <c>ChangedProperties</c> naming exactly what moved and the before/after
/// of each — AC-321-C. <c>GrantPath</c> stays null — <c>DepartmentManage</c> is company-wide.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid departmentId,
        Request request,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Department? department = await database.Departments
            .FirstOrDefaultAsync(candidate => candidate.Id == departmentId, cancellationToken);

        if (department is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.DepartmentNotFound);
        }

        Result renamed = department.Rename(request.NameAr ?? string.Empty, request.NameEn ?? string.Empty);

        if (renamed.IsFailure)
        {
            return ResultExtensions.Problem(renamed.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response(department.Id, department.NameAr, department.NameEn, department.IsActive));
    }
}
