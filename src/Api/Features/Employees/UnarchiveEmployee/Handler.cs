using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Employees.UnarchiveEmployee;

/// <summary>
/// Restores one archived employee. D-233 item 1; same shape as <c>UnarchiveBab.Handler</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The not-archived refusal is the entity's</b> (<c>errors.master.not_archived</c>).
/// </para>
/// <para>
/// <b>The phone rule is the create rule, not a new one.</b> A salaried record whose phone another
/// ACTIVE salaried record now holds is refused <see cref="MasterDataErrors.EmployeePhoneTaken"/>
/// (D-144, D-153 §3): the partial unique index <c>ux_employees_salaried_phone</c> covers active
/// salaried rows, so restoring would otherwise collide. Day labour has no refusing rule, only the
/// warn-and-acknowledge match, so a worker is restored whatever the phone matches.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>IsActive</c> moves, so <c>AuditSaveChangesInterceptor</c>
/// writes the <c>Modified</c> record. A refused call never reaches <c>SaveChangesAsync</c>.
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

        Result unarchived = employee.Unarchive();

        if (unarchived.IsFailure)
        {
            return ResultExtensions.Problem(unarchived.Error);
        }

        if (employee.Kind == EmployeeKind.Salaried)
        {
            bool salariedPhoneTaken = await database.Employees.AnyAsync(
                candidate => candidate.PhoneNormalised == employee.PhoneNormalised
                             && candidate.Kind == EmployeeKind.Salaried
                             && candidate.IsActive
                             && candidate.Id != employeeId,
                cancellationToken);

            if (salariedPhoneTaken)
            {
                return ResultExtensions.Problem(MasterDataErrors.EmployeePhoneTaken);
            }
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
                  && string.Equals(postgres.ConstraintName, "ux_employees_salaried_phone", StringComparison.Ordinal))
        {
            return ResultExtensions.Problem(MasterDataErrors.EmployeePhoneTaken);
        }

        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
