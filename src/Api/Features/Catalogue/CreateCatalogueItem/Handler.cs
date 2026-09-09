using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Catalogue.CreateCatalogueItem;

/// <summary>
/// Adds one catalogue item by hand. KAFF-202.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every guard the entity already has is the entity's, not this handler's.</b>
/// <c>CatalogueItem.Create</c> refuses a blank code, a blank Arabic description, a blank unit and a
/// negative price (rule 1, rule 8, <c>AC-202-D</c>) — none of that is repeated here.
/// </para>
/// <para>
/// <b>The باب existence check is the one thing the entity cannot see.</b> <c>BabId</c> is a bare
/// <c>Guid</c>, so an absent one binds to <c>Guid.Empty</c> and the entity has no database to ask
/// whether it names a real row — rule 9. Checked here, before <c>Create</c> runs, so a caller pointing
/// at a باب that does not exist gets a translatable refusal rather than a foreign-key violation with no
/// <c>messageKey</c> behind it.
/// </para>
/// <para>
/// <b>The code's uniqueness is the database's, not a read-then-write here</b> — rule 3, <c>AC-202-B</c>:
/// "the refusal survives two requests arriving at the same instant." The friendly path is
/// <c>ux_catalogue_items_code</c> itself; <see cref="IsCodeCollision"/> is what turns the loser of a
/// race into the same refusal everyone else gets, rather than a 500 (the same shape
/// <c>CreateUser.Handler</c> uses for <c>ux_users_user_name</c>).
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> The item is an entity change, so
/// <c>AuditSaveChangesInterceptor</c> writes the <c>Created</c> record in the same transaction, with
/// every stored field — including both prices — in the after-state. <c>GrantPath</c> stays null because
/// <c>CatalogueManage</c> is company-wide: no project, no access policy, no path to name.
/// </para>
/// <para>
/// No <c>Money</c> moves and none is stored beyond the item's own two prices. spec.md §4.1: a catalogue
/// is a price list, not a ledger.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool babExists = await database.Babs
            .AnyAsync(bab => bab.Id == request.BabId, cancellationToken);

        if (!babExists)
        {
            return ResultExtensions.Problem(MasterDataErrors.BabNotFound);
        }

        Result<CatalogueItem> created = CatalogueItem.Create(
            request.Code ?? string.Empty,
            request.DescriptionAr ?? string.Empty,
            request.Unit ?? string.Empty,
            request.BabId,
            Money.From(request.CostPrice),
            Money.From(request.BaseSellRate),
            request.DescriptionEn);

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        CatalogueItem item = created.Value;

        database.CatalogueItems.Add(item);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsCodeCollision(exception))
        {
            return ResultExtensions.Problem(MasterDataErrors.CatalogueItemCodeTaken);
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/catalogue-items/{item.Id}",
            new Response(
                item.Id,
                item.Code,
                item.DescriptionAr,
                item.DescriptionEn,
                item.Unit,
                item.BabId,
                item.CostPrice.Amount,
                item.BaseSellRate.Amount,
                item.Status));
    }

    /// <summary>A unique-violation on the catalogue item code index, and nothing else.</summary>
    private static bool IsCodeCollision(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
           && string.Equals(postgres.ConstraintName, "ux_catalogue_items_code", StringComparison.Ordinal);
}
