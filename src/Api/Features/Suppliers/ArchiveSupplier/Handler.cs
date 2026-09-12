using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Suppliers.ArchiveSupplier;

/// <summary>Archives one supplier. KAFF-212 rule 8.</summary>
/// <remarks>
/// Same shape as <c>ArchiveSubcontractor</c>: the refusal on an already-archived supplier is the
/// entity's (<c>Supplier.Archive</c>), not repeated here. No audit record is hand-written —
/// <c>IsActive</c> moves, so <c>AuditSaveChangesInterceptor</c> writes the <c>Modified</c> record.
/// <c>GrantPath</c> stays null — <c>SupplierManage</c> is company-wide.
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid supplierId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Supplier? supplier = await database.Suppliers
            .FirstOrDefaultAsync(candidate => candidate.Id == supplierId, cancellationToken);

        if (supplier is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.SupplierNotFound);
        }

        Result archived = supplier.Archive();

        if (archived.IsFailure)
        {
            return ResultExtensions.Problem(archived.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
