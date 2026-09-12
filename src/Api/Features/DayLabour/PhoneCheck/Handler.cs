using Kaff.Api.Common;
using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.DayLabour.PhoneCheck;

/// <summary>
/// Warns a Site Engineer before registering a worker whose phone already exists. decisions.md D-140
/// point 6.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one exception to <c>PhoneMatches</c>' shared shape</b> — D-141 §5's own text names it as
/// such. A day-labour match returns the full <see cref="PhoneMatch"/>; a salaried match is masked to
/// <see cref="RestrictedPhoneMatch"/>. Telling the two apart needs the matched row's <c>Kind</c>, which
/// <c>PhoneMatches.EmployeesAsync</c> deliberately never surfaces (D-146 point 3: "not added: a kind on
/// PhoneMatch"). So this handler runs its own query rather than the shared one, and the exception lives
/// here and nowhere else.
/// </para>
/// <para>Side-effect free, same reasoning as <c>Employees.PhoneCheck.Handler</c>. No audit record —
/// nothing changed. The save path re-runs the match itself and does not trust this response.</para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PhoneNumber> phone = PhoneNumber.Create(request.Phone);

        if (phone.IsFailure)
        {
            return ResultExtensions.Problem(phone.Error);
        }

        var rows = await database.Employees
            .Where(employee => employee.PhoneNormalised == phone.Value.Normalised)
            .OrderBy(employee => employee.Code)
            .Select(employee => new
            {
                employee.Id,
                employee.Code,
                employee.FullName,
                employee.IsActive,
                employee.Kind,
            })
            .ToListAsync(cancellationToken);

        List<object> matches = rows
            .Select(row => row.Kind == EmployeeKind.Salaried
                ? (object)new RestrictedPhoneMatch(true)
                : new PhoneMatch(row.Id, row.Code, row.FullName, !row.IsActive))
            .ToList();

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(matches));
    }
}
