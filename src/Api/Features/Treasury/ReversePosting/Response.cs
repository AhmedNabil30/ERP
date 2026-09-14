using Kaff.Domain.Common;
using Kaff.Domain.Treasury;

namespace Kaff.Api.Features.Treasury.ReversePosting;

/// <summary>
/// The reversal posting created, exactly as stored. KAFF-303, <c>AC-303-A</c>.
/// </summary>
/// <param name="Id">The new reversal posting's own id.</param>
/// <param name="PostingDate">The accounting date it was booked against.</param>
/// <param name="FromAccountId">The account the value left — the original's <c>ToAccountId</c>.</param>
/// <param name="ToAccountId">The account the value landed in — the original's <c>FromAccountId</c>.</param>
/// <param name="Amount">As stored — <c>decimal(18,4)</c>, never a <c>float</c>/<c>double</c>. Same as the original.</param>
/// <param name="Type">The posting type. Same as the original.</param>
/// <param name="ProjectId">The project this movement is tagged to, or <c>null</c> for a company-level movement.</param>
/// <param name="ReversesId">The original posting this reversal corrects.</param>
public sealed record Response(
    Guid Id,
    DateOnly PostingDate,
    Guid FromAccountId,
    Guid ToAccountId,
    Money Amount,
    PostingType Type,
    Guid? ProjectId,
    Guid ReversesId);
