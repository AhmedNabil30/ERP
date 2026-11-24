using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts.GetMaterialAdvance;

/// <summary>
/// The تشوينات of one project, summed from its postings on every read (nothing is stored). A reversal subtracts.
/// </summary>
/// <param name="Issued">تشوينات issued, net of reversals.</param>
/// <param name="Recovered">تشوينات recovered through extracts, net of reversals.</param>
/// <param name="Outstanding">Issued less recovered: what the client has yet to be charged for. Never below zero in a healthy project.</param>
/// <param name="OpenExtracts">Extracts not yet issued, collected, disputed or void. While any is open the difference may still close.</param>
/// <param name="Reconciled">True when every extract is past issue and issued equals recovered. A false with no open extract is a difference to chase.</param>
public sealed record MaterialAdvanceResponse(
    Guid ProjectId, Money Issued, Money Recovered, Money Outstanding, int OpenExtracts, bool Reconciled);

internal static class Handler
{
    private static readonly ExtractStatus[] Open =
    [
        ExtractStatus.Draft,
        ExtractStatus.QcSubmitted,
        ExtractStatus.PartiallyApproved,
        ExtractStatus.TechnicalApproved,
        ExtractStatus.DeductionsApplied,
        ExtractStatus.OwnerApproved,
    ];

    public static async Task<IResult> HandleAsync(Guid projectId, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var legs = await database.Postings
            .AsNoTracking()
            .Where(posting => posting.ProjectId == projectId
                && (posting.Type == PostingType.MaterialAdvanceIssue || posting.Type == PostingType.MaterialAdvanceRecovery))
            .Select(posting => new { posting.Type, posting.Amount, IsReversal = posting.ReversesId != null })
            .ToListAsync(cancellationToken);

        Money Net(PostingType type) =>
            Money.Sum(legs.Where(leg => leg.Type == type).Select(leg => leg.IsReversal ? -leg.Amount : leg.Amount));

        Money issued = Net(PostingType.MaterialAdvanceIssue);
        Money recovered = Net(PostingType.MaterialAdvanceRecovery);
        int open = await database.Extracts.CountAsync(
            extract => extract.ProjectId == projectId && Open.Contains(extract.Status), cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(new MaterialAdvanceResponse(
            projectId, issued, recovered, issued - recovered, open, open == 0 && issued == recovered));
    }
}
