using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Departments.ArchiveDepartment;

/// <summary>
/// Archives one department — the path for a department staff are still assigned to. KAFF-321,
/// AC-321-D.
/// </summary>
/// <remarks>
/// <para>
/// <b>No cascade.</b> This handler writes exactly one row — the department's own — through
/// <c>Department.Archive()</c>. It does not touch <see cref="User"/> or any other <c>Department</c>
/// row; an archived department stays on every user record that already carries it (AC-321-E).
/// </para>
/// <para>
/// <b>The already-archived refusal is the entity's</b> — <c>Department.Archive()</c> returns
/// <see cref="MasterDataErrors.AlreadyArchived"/>, the same shape <c>ArchiveBab.Handler</c> uses.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>AuditSaveChangesInterceptor</c> writes the
/// <c>Modified</c> record with the before/after of <c>IsActive</c>. <c>GrantPath</c> stays null —
/// <c>DepartmentManage</c> is company-wide.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid departmentId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Department? department = await database.Departments
            .FirstOrDefaultAsync(candidate => candidate.Id == departmentId, cancellationToken);

        if (department is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.DepartmentNotFound);
        }

        Result archived = department.Archive();

        if (archived.IsFailure)
        {
            return ResultExtensions.Problem(archived.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
