using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Employees.EditEmployee;

/// <summary>
/// Corrects one employee's file, refuses a change of population, and leaves the before-state in the
/// trail. KAFF-207, KAFF-208.
/// </summary>
/// <remarks>
/// <para>
/// <b>The immutability check happens before anything else is applied</b> — <c>AC-208-C</c>: "the
/// population does not change" is a statement about the whole request, so a request naming a
/// different <see cref="Request.Kind"/> is refused before <c>Employee.Edit</c> touches a single field,
/// never partially applied and then rolled back.
/// </para>
/// <para>
/// <b>Every other guard is the entity's.</b> <c>Employee.Edit</c> refuses what <c>Create</c> would
/// have refused (a blank name, day labour with no باب); none of it is repeated here.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>AuditSaveChangesInterceptor</c> writes the
/// <c>Modified</c> record with <c>ChangedProperties</c> naming exactly what moved and the before/after
/// of each (<c>AC-207-I</c>). A refused edit never reaches <c>SaveChangesAsync</c>, so it writes
/// nothing. <c>GrantPath</c> stays null — <c>EmployeeManage</c> is company-wide.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid employeeId,
        Request request,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Employee? employee = await database.Employees
            .FirstOrDefaultAsync(candidate => candidate.Id == employeeId, cancellationToken);

        if (employee is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.EmployeeNotFound);
        }

        if (request.Kind != employee.Kind)
        {
            // AC-208-C. Checked before any other field is touched — a population change is refused
            // whole, not partially applied.
            return ResultExtensions.Problem(MasterDataErrors.EmployeeKindIsImmutable);
        }

        Result<PhoneNumber> phone = PhoneNumber.Create(request.Phone);

        if (phone.IsFailure)
        {
            return ResultExtensions.Problem(phone.Error);
        }

        if (request.BabId is not null)
        {
            bool babExists = await database.Babs
                .AnyAsync(bab => bab.Id == request.BabId, cancellationToken);

            if (!babExists)
            {
                return ResultExtensions.Problem(MasterDataErrors.BabNotFound);
            }
        }

        Result edited = employee.Edit(request.FullName ?? string.Empty, phone.Value, request.BabId, request.Specialty);

        if (edited.IsFailure)
        {
            return ResultExtensions.Problem(edited.Error);
        }

        employee.SetStaffDetails(request.NationalId, request.Department, request.JobTitle, request.HiredOn);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsPhoneCollision(exception))
        {
            return ResultExtensions.Problem(MasterDataErrors.EmployeePhoneTaken);
        }

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response(
                employee.Id,
                employee.Code,
                employee.FullName,
                employee.PhoneEntered,
                employee.Kind,
                employee.BabId,
                employee.Specialty,
                employee.NationalId,
                employee.Department,
                employee.JobTitle,
                employee.HiredOn,
                employee.IsActive));
    }

    private static bool IsPhoneCollision(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
           && string.Equals(postgres.ConstraintName, "ux_employees_phone", StringComparison.Ordinal);
}
