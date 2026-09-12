using System.Globalization;
using Kaff.Api.Common;
using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DayLabour.RegisterFromSite;

/// <summary>
/// Registers one day labourer from site. KAFF-209.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reuses <c>Employee.Create</c> with no change to the entity</b> — decisions.md D-140 "What
/// Backend builds". <see cref="Kaff.Domain.MasterData.EmployeeKind.DayLabour"/> is passed by the
/// handler; <see cref="Request"/> carries no <c>Kind</c> member at all.
/// </para>
/// <para>
/// <b>Nothing on this route ever returns <c>MasterDataErrors.EmployeePhoneTaken</c>.</b> D-146 point
/// 3: that refusal exists only for a salaried record matching another salaried record, and this route
/// can never create a salaried record. Every match here — day labour or a cross-population match
/// against a salaried record (D-140 point 6) — is D-141 §5's warn-and-acknowledge, the same mechanism
/// <c>CreateEmployee.Handler</c> uses.
/// </para>
/// <para>
/// <b>No audit record is hand-written for the create itself.</b> <c>AuditSaveChangesInterceptor</c>
/// writes the <c>Created</c> record in the same transaction. The acknowledgement is declared through
/// <c>IAuditContext.Record</c>, one <c>DuplicatePhoneAcknowledged</c> event per match — against the
/// real matched id even when that match was a salaried record the response never named (D-140 point 6,
/// KAFF-209 rule 9).
/// </para>
/// <para>
/// <b>The project is not checked here.</b> <c>Permission.DayLabourSiteManage</c> is project-scoped, so
/// a caller only reaches this handler once the access policy has granted the route's project — the
/// project authorises the act, it does not own the worker (D-140 point 3). No <c>RegisteredOnProjectId</c>
/// column exists on <see cref="Employee"/>.
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

        List<PhoneMatch> matches =
            await PhoneMatches.EmployeesAsync(database, phone.Value.Normalised, cancellationToken);

        if (matches.Count > 0 && !request.AcknowledgedDuplicatePhone)
        {
            // 409, no match data — the names belong to the 200 from phone-check. D-141 §5.
            return ResultExtensions.Problem(MasterDataErrors.DuplicatePhoneNotAcknowledged);
        }

        if (request.BabId is not null)
        {
            bool babExists = await database.Babs.AnyAsync(bab => bab.Id == request.BabId, cancellationToken);

            if (!babExists)
            {
                return ResultExtensions.Problem(MasterDataErrors.BabNotFound);
            }
        }

        Result<Employee> created = Employee.Create(
            await NextCodeAsync(database, cancellationToken),
            request.FullName ?? string.Empty,
            phone.Value,
            EmployeeKind.DayLabour,
            clock.GetUtcNow(),
            request.BabId,
            request.Specialty);

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        Employee employee = created.Value;

        // One event per match, subject is the record that was MATCHED — same shape as CreateClient
        // and CreateEmployee (D-141 §4/§5). Empty when there was no match.
        foreach (PhoneMatch match in matches)
        {
            audit.Record<Employee>(AuditEventKind.DuplicatePhoneAcknowledged, match.Id);
        }

        database.Employees.Add(employee);
        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/employees/{employee.Id}",
            new Response(
                employee.Id,
                employee.Code,
                employee.FullName,
                employee.PhoneEntered,
                employee.BabId,
                employee.Specialty,
                employee.IsActive));
    }

    /// <summary>The next employee code, of the form <c>E-10001</c>. Same mechanism as <c>CreateEmployee</c>'s.</summary>
    private static async Task<string> NextCodeAsync(KaffDbContext database, CancellationToken cancellationToken)
    {
#pragma warning disable EF1002 // The sequence name is a compile-time constant of this assembly, never user input.
        long number = await database.Database
            .SqlQueryRaw<long>($"SELECT nextval('{KaffDbContext.EmployeeCodeSequence}') AS \"Value\"")
            .SingleAsync(cancellationToken);
#pragma warning restore EF1002

        return string.Create(CultureInfo.InvariantCulture, $"E-{number}");
    }
}
