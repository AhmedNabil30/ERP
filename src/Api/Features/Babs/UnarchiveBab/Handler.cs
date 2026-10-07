using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Babs.UnarchiveBab;

/// <summary>
/// Un-archives one باب. KAFF-943, D-130 §4.
/// </summary>
/// <remarks>
/// <para>
/// <b>No cascade, in either direction.</b> Archiving never cascaded (D-130 §5, KAFF-213 rules 4 and 5),
/// so restoring must not either. This handler writes exactly one row — the باب's own — through
/// <c>Bab.Unarchive()</c>. It does not read or touch any <c>CatalogueItem</c> or any other <c>Bab</c>.
/// </para>
/// <para>
/// <b>The refusal is the entity's</b>: <c>Bab.Unarchive()</c> returns <c>errors.master.not_archived</c>
/// for an active باب, the same shape as <c>UnarchiveCatalogueItem.Handler</c>.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>IsActive</c> moves, so <c>AuditSaveChangesInterceptor</c>
/// writes the <c>Modified</c> record in the same transaction. A refused call never reaches
/// <c>SaveChangesAsync</c>, so it writes nothing.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid babId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        Bab? bab = await database.Babs.FirstOrDefaultAsync(candidate => candidate.Id == babId, cancellationToken);

        if (bab is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.BabNotFound);
        }

        Result unarchived = bab.Unarchive();

        if (unarchived.IsFailure)
        {
            return ResultExtensions.Problem(unarchived.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
