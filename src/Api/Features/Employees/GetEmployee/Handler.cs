using Kaff.Api.Common.Results;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Employees.GetEmployee;

/// <summary>Reads one employee, including the four staff fields the list omits.</summary>
/// <remarks>
/// <b>An archived employee is still readable by id</b> — same reasoning as <c>GetClient.Handler</c>: a
/// screen reached by id was asked for that employee specifically. <b>No audit record.</b> It is a read.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid employeeId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Response? employee = await database.Employees
            .AsNoTracking()
            .Where(candidate => candidate.Id == employeeId)
            .Select(candidate => new Response(
                candidate.Id,
                candidate.Code,
                candidate.FullName,
                candidate.PhoneEntered,
                candidate.Kind,
                candidate.BabId,
                candidate.Specialty,
                candidate.NationalId,
                candidate.Department,
                candidate.JobTitle,
                candidate.HiredOn,
                candidate.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return employee is null
            ? ResultExtensions.Problem(MasterDataErrors.EmployeeNotFound)
            : Microsoft.AspNetCore.Http.Results.Ok(employee);
    }
}
