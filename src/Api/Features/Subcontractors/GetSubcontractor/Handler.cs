using Kaff.Api.Common.Results;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Subcontractors.GetSubcontractor;

/// <summary>Reads one subcontractor. S-029 loads its edit form from here. KAFF-211.</summary>
/// <remarks>Archived firms are returned — the list hides them by default, this does not.</remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid subcontractorId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Response? subcontractor = await database.Subcontractors
            .Where(candidate => candidate.Id == subcontractorId)
            .Select(candidate => new Response(
                candidate.Id,
                candidate.Code,
                candidate.Name,
                candidate.PhoneEntered,
                candidate.TradeBabId,
                candidate.RetentionRate.Fraction,
                candidate.TaxRegistrationNumber,
                candidate.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return subcontractor is null
            ? ResultExtensions.Problem(MasterDataErrors.SubcontractorNotFound)
            : Microsoft.AspNetCore.Http.Results.Ok(subcontractor);
    }
}
