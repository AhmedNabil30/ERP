using System.Globalization;
using Kaff.Api.Common;
using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Employees.CreateEmployee;

/// <summary>
/// Registers one costed person. KAFF-207.
/// </summary>
/// <remarks>
/// <para>
/// <b>The code is drawn from a PostgreSQL sequence, the same shape as <c>CreateClient</c>'s</b>
/// (decisions.md D-130 §6) — generated, never typed, never editable.
/// </para>
/// <para>
/// <b>Two phone rules, checked in order (decisions.md D-144 §1, D-146, narrowed by D-153 §3/Q83).</b>
/// A salaried record whose phone matches another ACTIVE salaried record is refused outright —
/// <c>MasterDataErrors.EmployeePhoneTaken</c>, enforced by the partial unique index
/// <c>ux_employees_salaried_phone</c> (now filtered <c>is_active</c> too), never bypassed by
/// <see cref="Request.AcknowledgedDuplicatePhone"/>. Every other match — day labour, an archived
/// salaried leaver's phone included — is warn-and-acknowledge, the same mechanism <c>CreateClient</c>
/// uses (D-141).
/// </para>
/// <para>
/// <b>The باب's existence is the one thing the entity cannot see</b> — checked here the same way
/// <c>CreateCatalogueItem.Handler</c> and <c>CreateBab.Handler</c> check a <c>BabId</c>.
/// </para>
/// <para>
/// <b>No audit record is hand-written for the create itself.</b> <c>AuditSaveChangesInterceptor</c>
/// writes the <c>Created</c> record in the same transaction. The acknowledgement is the one fact the
/// change tracker cannot see, so it is declared through <c>IAuditContext.Record</c>, one
/// <c>DuplicatePhoneAcknowledged</c> event per match (D-141 §5). <c>GrantPath</c> stays null —
/// <c>EmployeeManage</c> is company-wide.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        IAuditContext audit,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);

        Result<PhoneNumber> phone = PhoneNumber.Create(request.Phone);

        if (phone.IsFailure)
        {
            return ResultExtensions.Problem(phone.Error);
        }

        if (request.Kind == EmployeeKind.Salaried)
        {
            // D-146 point 3, narrowed by D-153 §3 (Q83) to another ACTIVE salaried record — an
            // archived leaver's phone is free. Checked first, whatever AcknowledgedDuplicatePhone
            // says, and nothing is written when it fires. The index is the guarantee against a race;
            // this query gives the clean 409 for the ordinary case.
            bool salariedPhoneTaken = await database.Employees.AnyAsync(
                employee => employee.PhoneNormalised == phone.Value.Normalised
                            && employee.Kind == EmployeeKind.Salaried
                            && employee.IsActive,
                cancellationToken);

            if (salariedPhoneTaken)
            {
                return ResultExtensions.Problem(MasterDataErrors.EmployeePhoneTaken);
            }
        }

        List<PhoneMatch> matches =
            await PhoneMatches.EmployeesAsync(database, phone.Value.Normalised, cancellationToken);

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

        Result<Employee> created = Employee.Create(
            await NextCodeAsync(database, cancellationToken),
            request.FullName ?? string.Empty,
            phone.Value,
            request.Kind,
            clock.GetUtcNow(),
            request.BabId,
            request.Specialty);

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        Employee employee = created.Value;

        employee.SetStaffDetails(request.NationalId, request.Department, request.JobTitle, request.HiredOn);

        // One event per match, subject is the record that was MATCHED — same shape as CreateClient
        // (D-141 §4/§5). Empty when there was no match, which is how the flag is ignored rather than
        // believed.
        foreach (PhoneMatch match in matches)
        {
            audit.Record<Employee>(AuditEventKind.DuplicatePhoneAcknowledged, match.Id);
        }

        database.Employees.Add(employee);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsPhoneCollision(exception))
        {
            return ResultExtensions.Problem(MasterDataErrors.EmployeePhoneTaken);
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/employees/{employee.Id}",
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

    /// <summary>
    /// The next employee code, of the form <c>E-10001</c>. Same mechanism as <c>CreateClient</c>'s
    /// <c>NextCodeAsync</c> — decisions.md D-130 §6, D-107 §1.
    /// </summary>
    private static async Task<string> NextCodeAsync(KaffDbContext database, CancellationToken cancellationToken)
    {
#pragma warning disable EF1002 // The sequence name is a compile-time constant of this assembly, never user input.
        long number = await database.Database
            .SqlQueryRaw<long>($"SELECT nextval('{KaffDbContext.EmployeeCodeSequence}') AS \"Value\"")
            .SingleAsync(cancellationToken);
#pragma warning restore EF1002

        return string.Create(CultureInfo.InvariantCulture, $"E-{number}");
    }

    private static bool IsPhoneCollision(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
           && string.Equals(postgres.ConstraintName, "ux_employees_salaried_phone", StringComparison.Ordinal);
}
