using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Babs.MoveBab;

/// <summary>
/// Re-parents one باب, taking its whole subtree with it. KAFF-205 rules 1-3.
/// </summary>
/// <remarks>
/// <para>
/// <b>The cycle guard is the entity's — <c>Bab.SetParent</c>.</b> This handler's own job is the one
/// query the entity cannot run itself: reading every باب's parent pointer so the walk can see the
/// tree (KAFF-205 rule 3). It refuses with <see cref="MasterDataErrors.BabCannotBeItsOwnAncestor"/>
/// at any depth — decisions.md D-128, SM-33's rename.
/// </para>
/// <para>
/// <b>Children stay children</b> (rule 2) because <c>SetParent</c> only ever writes this باب's own
/// <c>ParentBabId</c> column — nothing here walks or touches any other row, so nothing re-parents a
/// child to a grandparent as a side effect.
/// </para>
/// <para>
/// <b>No markup moves</b> (rule 6) — this handler never reads or writes <c>DefaultMarkup</c>.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>AuditSaveChangesInterceptor</c> writes the before/after
/// of <c>ParentBabId</c> — rule 7, <c>AC-205-I</c>. <c>GrantPath</c> stays null: <c>BabManage</c> is
/// company-wide.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid babId,
        Request request,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Bab? bab = await database.Babs.FirstOrDefaultAsync(candidate => candidate.Id == babId, cancellationToken);

        if (bab is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.BabNotFound);
        }

        if (request.ParentBabId is not null)
        {
            bool parentExists = await database.Babs
                .AnyAsync(candidate => candidate.Id == request.ParentBabId, cancellationToken);

            if (!parentExists)
            {
                return ResultExtensions.Problem(MasterDataErrors.BabNotFound);
            }
        }

        Dictionary<Guid, Guid?> parentByBabId = await database.Babs
            .Select(candidate => new { candidate.Id, candidate.ParentBabId })
            .ToDictionaryAsync(candidate => candidate.Id, candidate => candidate.ParentBabId, cancellationToken);

        Result moved = bab.SetParent(request.ParentBabId, parentByBabId);

        if (moved.IsFailure)
        {
            return ResultExtensions.Problem(moved.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(bab.Id, bab.ParentBabId));
    }
}
