using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Api.Features.Treasury.PostMovement;

/// <summary>
/// The created posting, exactly as stored. KAFF-301, <c>AC-301-A</c>.
/// </summary>
/// <remarks>
/// <b>The amount echoed is the stored value, never a recomputed one</b> — D-044 §6, and this record's
/// own reason for being cased at <c>AC-301-B</c>: a caller must be able to confirm the fourth decimal
/// place actually persisted, not one the handler recalculated on the way out.
/// </remarks>
/// <param name="Id">The new posting.</param>
/// <param name="PostingDate">The accounting date it was booked against.</param>
/// <param name="FromAccountId">The account the value left.</param>
/// <param name="ToAccountId">The account the value landed in.</param>
/// <param name="Amount">As stored — <c>decimal(18,4)</c>, never a <c>float</c>/<c>double</c>.</param>
/// <param name="Type">The posting type.</param>
/// <param name="ProjectId">The project this movement is tagged to, or <c>null</c> for a company-level movement.</param>
/// <param name="IsReversal">Always <c>false</c> here — this endpoint creates originals, never reversals (<c>KAFF-303</c>).</param>
public sealed record Response(
    Guid Id,
    DateOnly PostingDate,
    Guid FromAccountId,
    Guid ToAccountId,
    Money Amount,
    PostingType Type,
    Guid? ProjectId,
    bool IsReversal);
