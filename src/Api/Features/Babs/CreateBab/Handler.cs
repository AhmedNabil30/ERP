using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Babs.CreateBab;

/// <summary>
/// Adds one باب. KAFF-204.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every guard the entity already has is the entity's, not this handler's.</b> <c>Bab.Create</c>
/// refuses a blank code and a blank name (rule 2's required markup is the parameter's own type — a
/// <see cref="Percentage"/> cannot be absent once constructed, so the null check below is what stands
/// in for a missing value on the wire).
/// </para>
/// <para>
/// <b>A new باب cannot create a cycle</b> (KAFF-204 rule 5's own note) — it has no children yet, so a
/// real parent can never make it its own ancestor. <c>Bab.SetParent</c>'s walk is not called here;
/// KAFF-205 is where re-parenting an <i>existing</i> باب runs through it.
/// </para>
/// <para>
/// <b>The parent's existence is the one thing the entity cannot see</b> — checked here the same way
/// <c>CreateCatalogueItem.Handler</c> checks <c>BabId</c>.
/// </para>
/// <para>
/// <b>The code's uniqueness is the database's</b> — <c>ux_babs_code</c> — not a read-then-write here,
/// the same race-proof shape <c>CreateCatalogueItem.Handler</c> uses for
/// <c>ux_catalogue_items_code</c>.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>AuditSaveChangesInterceptor</c> writes the
/// <c>Created</c> record in the same transaction. <c>GrantPath</c> stays null — <c>BabManage</c> is
/// company-wide.
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

        if (request.ParentBabId is not null)
        {
            bool parentExists = await database.Babs
                .AnyAsync(bab => bab.Id == request.ParentBabId, cancellationToken);

            if (!parentExists)
            {
                return ResultExtensions.Problem(MasterDataErrors.BabNotFound);
            }
        }

        if (request.DefaultMarkup is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.DefaultMarkupRequired);
        }

        // D-151: DefaultMarkup is already Percentage — a negative value is refused by the type's own
        // constructor during deserialisation (ApiErrors.MalformedBody).
        Result<Bab> created = Bab.Create(
            request.Code ?? string.Empty,
            request.NameAr ?? string.Empty,
            request.NameEn ?? string.Empty,
            request.DefaultMarkup.Value,
            request.ParentBabId,
            request.SortOrder);

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        Bab bab = created.Value;

        database.Babs.Add(bab);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsCodeCollision(exception))
        {
            return ResultExtensions.Problem(MasterDataErrors.BabCodeTaken);
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/babs/{bab.Id}",
            new Response(
                bab.Id, bab.Code, bab.NameAr, bab.NameEn, bab.ParentBabId, bab.DefaultMarkup,
                bab.SortOrder, bab.IsActive));
    }

    private static bool IsCodeCollision(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
           && string.Equals(postgres.ConstraintName, "ux_babs_code", StringComparison.Ordinal);
}
