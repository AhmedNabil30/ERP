using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Employees.ArchiveEmployee;

/// <summary>
/// Archives one employee. KAFF-207.
/// </summary>
/// <remarks>
/// <para>
/// <b>A leaver is deactivated, never deleted</b> — D-049 ruling 5, the same ruling that governs a
/// user. The row still exists with every field intact; only <see cref="Employee.IsActive"/> moves
/// (<c>AC-207-F</c>).
/// </para>
/// <para>
/// <b>This is also the first half of KAFF-208's "archive and re-register" shape</b> (D-130 §7,
/// <c>AC-208-E</c>): a day labourer going onto the payroll is archived here, unchanged, and a new
/// employee record is registered separately through <c>CreateEmployee</c> — never a move, because
/// there is no <c>SetKind</c> anywhere in this codebase for this handler, or any other, to call.
/// </para>
/// <para>
/// <b>The already-archived refusal is the entity's</b> — <c>Employee.Archive()</c> returns
/// <see cref="MasterDataErrors.AlreadyArchived"/>, the same shape <c>ArchiveBab.Handler</c> and
/// <c>ArchiveCatalogueItem.Handler</c> use.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>AuditSaveChangesInterceptor</c> writes the
/// <c>Modified</c> record with the before/after of <c>IsActive</c>. A refused archive never reaches
/// <c>SaveChangesAsync</c>, so it writes nothing.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid employeeId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Employee? employee = await database.Employees
            .FirstOrDefaultAsync(candidate => candidate.Id == employeeId, cancellationToken);

        if (employee is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.EmployeeNotFound);
        }

        Result archived = employee.Archive();

        if (archived.IsFailure)
        {
            return ResultExtensions.Problem(archived.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
