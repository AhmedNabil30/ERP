using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Employees.ListBabOptions;

/// <summary>
/// Returns every باب as a picker option for the employee form. decisions.md D-137.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both active and archived أبواب come back</b>, so the edit form can still name an employee's
/// archived trade — D-137 §"What Backend builds".
/// </para>
/// <para>
/// <b>Projects straight into <see cref="BabOption"/> in the EF query, so <c>DefaultMarkup</c> is never
/// loaded from the database at all</b> — not merely omitted from the response after being read.
/// </para>
/// <para>
/// <b>No audit record.</b> It is a read; there is nothing to log.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<BabOption> items = await database.Babs
            .AsNoTracking()
            .OrderBy(bab => bab.SortOrder)
            .ThenBy(bab => bab.Code)
            .Select(bab => new BabOption(
                bab.Id,
                bab.Code,
                bab.NameAr,
                bab.NameEn,
                bab.ParentBabId,
                bab.IsActive))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(items));
    }
}
