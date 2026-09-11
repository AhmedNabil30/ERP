using System.Globalization;
using Kaff.Api.Common.Results;
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
/// <b>The phone's uniqueness is the database's</b> — <c>ux_employees_phone</c> — not a read-then-write
/// here. This is spec.md §2/§10's "exactly one record" mechanism (AC-207-D) and, at the same time,
/// KAFF-208's "nobody appears in both populations" mechanism (AC-208-B): one index does both jobs,
/// because there is exactly one table for both populations.
/// </para>
/// <para>
/// <b>The باب's existence is the one thing the entity cannot see</b> — checked here the same way
/// <c>CreateCatalogueItem.Handler</c> and <c>CreateBab.Handler</c> check a <c>BabId</c>.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>AuditSaveChangesInterceptor</c> writes the
/// <c>Created</c> record in the same transaction. <c>GrantPath</c> stays null — <c>EmployeeManage</c>
/// is company-wide.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(clock);

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
           && string.Equals(postgres.ConstraintName, "ux_employees_phone", StringComparison.Ordinal);
}
