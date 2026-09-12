using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Babs.EditBab;

/// <summary>
/// Corrects one باب's names or its default markup, and leaves the before-state in the trail.
/// KAFF-204.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every guard is the entity's.</b> <c>Rename</c> refuses what <c>Create</c> would have refused;
/// none of it is repeated here.
/// </para>
/// <para>
/// <b>A markup change moves no existing BOQ line</b> — rule 4, <c>AC-204-D</c>. There is no BOQ or
/// estimate entity yet for this handler to touch even if it tried; the freeze rule is enforced where
/// the BOQ is built, in a later slice. <b>The correct implementation here is to add nothing</b> beyond
/// this row's own change.
/// </para>
/// <para>
/// <b>No audit record is hand-written.</b> <c>AuditSaveChangesInterceptor</c> writes the
/// <c>Modified</c> record with <c>ChangedProperties</c> naming exactly what moved and the before/after
/// of each — <c>AC-204-H</c>. <c>GrantPath</c> stays null: <c>BabManage</c> is company-wide.
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

        Result renamed = bab.Rename(request.NameAr ?? string.Empty, request.NameEn ?? string.Empty);

        if (renamed.IsFailure)
        {
            return ResultExtensions.Problem(renamed.Error);
        }

        if (request.DefaultMarkup is null)
        {
            return ResultExtensions.Problem(MasterDataErrors.DefaultMarkupRequired);
        }

        // D-151: DefaultMarkup is already Percentage — a negative value is refused by the type's own
        // constructor during deserialisation (ApiErrors.MalformedBody).
        bab.SetDefaultMarkup(request.DefaultMarkup.Value);

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response(bab.Id, bab.Code, bab.NameAr, bab.NameEn, bab.ParentBabId, bab.DefaultMarkup, bab.SortOrder, bab.IsActive));
    }
}
