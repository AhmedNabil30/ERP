using Kaff.Api.Common.Results;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Suppliers.ListSuppliers;

/// <summary>Lists suppliers for S-030, filtered by <see cref="SupplierListFilter"/>. KAFF-212.</summary>
/// <remarks>
/// Same shape as <c>ListSubcontractors</c>: an unknown <c>status</c> is refused rather than defaulted,
/// and archived suppliers are excluded unless asked for.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        KaffDbContext database,
        CancellationToken cancellationToken,
        string? status = null)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (!SupplierListFilterParsing.TryParse(status, out SupplierListFilter filter))
        {
            return ResultExtensions.Problem(MasterDataErrors.SupplierListFilterUnknown);
        }

        IQueryable<Supplier> query = database.Suppliers;

        query = filter switch
        {
            SupplierListFilter.Active => query.Where(supplier => supplier.IsActive),
            SupplierListFilter.Archived => query.Where(supplier => !supplier.IsActive),
            _ => query,
        };

        List<SupplierSummary> suppliers = await query
            .OrderBy(supplier => supplier.Code)
            .Select(supplier => new SupplierSummary(
                supplier.Id,
                supplier.Code,
                supplier.Name,
                supplier.PhoneEntered,
                supplier.Address,
                supplier.TaxRegistrationNumber,
                supplier.IsActive))
            .ToListAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(suppliers));
    }
}
