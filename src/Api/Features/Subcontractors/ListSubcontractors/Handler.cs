using Kaff.Api.Common.Results;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Subcontractors.ListSubcontractors;

/// <summary>Lists subcontractors for S-028, filtered by <see cref="SubcontractorListFilter"/>. KAFF-211.</summary>
/// <remarks>
/// Same shape as <c>ListClients</c>: an unknown <c>status</c> is refused rather than defaulted, and
/// archived firms are excluded unless asked for. No search term yet — a search box is a screen
/// concern nobody has asked for on this list; <c>ListClients</c>'s own comment about not inventing a
/// contract before a screen needs it applies here the same way.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        CancellationToken cancellationToken,
        string? status = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (!SubcontractorListFilterParsing.TryParse(status, out SubcontractorListFilter filter))
        {
            return ResultExtensions.Problem(MasterDataErrors.SubcontractorListFilterUnknown);
        }

        IQueryable<Subcontractor> query = database.Subcontractors;

        query = filter switch
        {
            SubcontractorListFilter.Active => query.Where(subcontractor => subcontractor.IsActive),
            SubcontractorListFilter.Archived => query.Where(subcontractor => !subcontractor.IsActive),
            _ => query,
        };

        List<SubcontractorSummary> subcontractors = await query
            .OrderBy(subcontractor => subcontractor.Code)
            .Select(subcontractor => new SubcontractorSummary(
                subcontractor.Id,
                subcontractor.Code,
                subcontractor.Name,
                subcontractor.PhoneEntered,
                subcontractor.TradeBabId,
                subcontractor.RetentionRate,
                subcontractor.IsActive))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(subcontractors));
    }
}
