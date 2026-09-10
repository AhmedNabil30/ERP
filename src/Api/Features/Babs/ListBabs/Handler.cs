using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Babs.ListBabs;

/// <summary>
/// Returns every باب, flat, ordered by <c>SortOrder</c> then <c>Code</c>. No tree-building, no
/// filter, no search — the smallest read that lets a catalogue screen show a name instead of a guid.
/// </summary>
/// <remarks>
/// <b>No audit record.</b> It is a read; there is nothing to log.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<BabSummary> items = await database.Babs
            .OrderBy(bab => bab.SortOrder)
            .ThenBy(bab => bab.Code)
            .Select(bab => new BabSummary(
                bab.Id,
                bab.Code,
                bab.NameAr,
                bab.NameEn,
                bab.ParentBabId,
                bab.DefaultMarkup.Fraction,
                bab.IsActive))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(items));
    }
}
