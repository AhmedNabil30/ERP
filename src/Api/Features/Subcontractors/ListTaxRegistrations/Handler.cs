using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Subcontractors.ListTaxRegistrations;

/// <summary>
/// Lists every subcontractor for Finance, projected to the fields D-147 point 2 allows. All firms,
/// active and archived — Finance needs to find one to set its number regardless of status, and
/// <see cref="SubcontractorTaxRegistration.IsActive"/> is carried so the caller can tell them apart.
/// </summary>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        List<SubcontractorTaxRegistration> subcontractors = await database.Subcontractors
            .OrderBy(subcontractor => subcontractor.Code)
            .Select(subcontractor => new SubcontractorTaxRegistration(
                subcontractor.Id,
                subcontractor.Code,
                subcontractor.Name,
                subcontractor.TaxRegistrationNumber,
                subcontractor.IsActive))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(subcontractors));
    }
}
