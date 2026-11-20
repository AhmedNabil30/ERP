using Kaff.Domain.Common;
using Kaff.Domain.Extracts;

namespace Kaff.Api.Features.Ncrs;

/// <summary>What the caller may do to the NCR now. A false flag leaves the control off the screen.</summary>
/// <param name="Close">The NCR is open and the caller holds the gate that closes it (<c>NcrCloser</c>, the Technical Office).</param>
public sealed record NcrActions(bool Close);

/// <summary>
/// One NCR (AC-500-E). <paramref name="HeldAmount"/> is the period work value the split gate held back, derived from the
/// extract on the read. It is null for a viewer who may not see extract money, and once a rejection has returned the
/// extract to Draft, because the extract then holds nothing against this NCR.
/// </summary>
public sealed record NcrResponse(
    Guid Id,
    Guid ExtractId,
    int ExtractNumber,
    NcrStatus Status,
    DateTimeOffset OpenedAt,
    Guid OpenedByUserId,
    DateTimeOffset? ClosedAt,
    Guid? ClosedByUserId,
    string? CloseNote,
    Money? HeldAmount,
    NcrActions Actions)
{
    public static NcrResponse From(Ncr ncr, Extract extract, bool seesMoney, bool mayClose)
    {
        ArgumentNullException.ThrowIfNull(ncr);
        ArgumentNullException.ThrowIfNull(extract);

        return new NcrResponse(
            ncr.Id,
            ncr.ExtractId,
            extract.Number,
            ncr.Status,
            ncr.OpenedAt,
            ncr.OpenedByUserId,
            ncr.ClosedAt,
            ncr.ClosedByUserId,
            ncr.CloseNote,
            seesMoney && extract.HeldForNcrId == ncr.Id ? extract.HeldWorkValue() : null,
            new NcrActions(mayClose && ncr.Status == NcrStatus.Open));
    }
}
