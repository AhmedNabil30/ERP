using Kaff.Api.Common.Results;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Suppliers.GetSupplier;

/// <summary>Reads one supplier. S-030 loads its edit form from here. KAFF-212.</summary>
/// <remarks>Archived suppliers are returned — the list hides them by default, this does not.</remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid supplierId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Response? supplier = await database.Suppliers
            .Where(candidate => candidate.Id == supplierId)
            .Select(candidate => new Response(
                candidate.Id,
                candidate.Code,
                candidate.Name,
                candidate.PhoneEntered,
                candidate.Address,
                candidate.TaxRegistrationNumber,
                candidate.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return supplier is null
            ? ResultExtensions.Problem(MasterDataErrors.SupplierNotFound)
            : Microsoft.AspNetCore.Http.Results.Ok(supplier);
    }
}
