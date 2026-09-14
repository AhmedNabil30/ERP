using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Departments.UnarchiveDepartment;

/// <summary>
/// Un-archives one department. KAFF-321, AC-321-F (revised — Nabil's ruling 2026-09-15, decisions.md:
/// archive-never-delete for departments, replacing the earlier zero-staff hard-delete path).
/// </summary>
/// <remarks>
/// <para>
/// <b>The refusal is the entity's</b>, same shape as every other archive/unarchive handler in this
/// codebase: <c>Department.Unarchive</c> refuses a row that is not archived with
/// <c>errors.master.not_archived</c>, and this handler returns exactly that rather than checking
/// <c>IsActive</c> itself first.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>IsActive</c> moves, so <c>AuditSaveChangesInterceptor</c>
/// writes the <c>Modified</c> record in the same transaction — same mechanism as
/// <c>ArchiveDepartment.Handler</c>.
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

        Result unarchived = department.Unarchive();

        if (unarchived.IsFailure)
        {
            return ResultExtensions.Problem(unarchived.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
