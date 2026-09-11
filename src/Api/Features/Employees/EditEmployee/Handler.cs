using Kaff.Api.Common;
using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
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
/// <b>The salaried phone refusal, then the warn-and-acknowledge rule (decisions.md D-146 point 3).</b>
/// Because <c>Kind</c> cannot change, an edit can never move a record into the other population — the
/// salaried check below only ever runs for a record that was already salaried.
/// </para>
/// <para>
/// <b>Every other guard is the entity's.</b> <c>Employee.Edit</c> refuses what <c>Create</c> would
/// have refused (a blank name, day labour with no باب); none of it is repeated here.
/// </para>
/// <para>
/// <b>No audit record is hand-written for the edit itself.</b> <c>AuditSaveChangesInterceptor</c>
/// writes the <c>Modified</c> record with <c>ChangedProperties</c> naming exactly what moved and the
/// before/after of each (<c>AC-207-I</c>). A refused edit never reaches <c>SaveChangesAsync</c>, so it
/// writes nothing. The acknowledgement is declared through <c>IAuditContext.Record</c>, same as
/// <c>CreateEmployee</c>. <c>GrantPath</c> stays null — <c>EmployeeManage</c> is company-wide.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid employeeId,
        Request request,
        KaffDbContext database,
        IAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(audit);

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

        if (employee.Kind == EmployeeKind.Salaried)
        {
            // D-146 point 3. Checked first, whatever AcknowledgedDuplicatePhone says, and nothing is
            // written when it fires.
            bool salariedPhoneTaken = await database.Employees.AnyAsync(
                candidate => candidate.PhoneNormalised == phone.Value.Normalised
                             && candidate.Kind == EmployeeKind.Salaried
                             && candidate.Id != employeeId,
                cancellationToken);

            if (salariedPhoneTaken)
            {
                return ResultExtensions.Problem(MasterDataErrors.EmployeePhoneTaken);
            }
        }

        List<PhoneMatch> matches = await PhoneMatches.EmployeesAsync(
            database, phone.Value.Normalised, cancellationToken, excluding: employeeId);

        if (matches.Count > 0 && !request.AcknowledgedDuplicatePhone)
        {
            // 409, no match data — the names belong to the 200 from phone-check. D-141 §5.
            return ResultExtensions.Problem(MasterDataErrors.DuplicatePhoneNotAcknowledged);
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

        // One event per match, subject is the record that was MATCHED — same shape as EditClient
        // (D-141 §4/§5). Empty when there was no match.
        foreach (PhoneMatch match in matches)
        {
            audit.Record<Employee>(AuditEventKind.DuplicatePhoneAcknowledged, match.Id);
        }

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
           && string.Equals(postgres.ConstraintName, "ux_employees_salaried_phone", StringComparison.Ordinal);
}
