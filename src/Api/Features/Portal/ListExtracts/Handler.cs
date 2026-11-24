using Kaff.Api.Features.Extracts;
using Kaff.Domain.Common;
using Kaff.Domain.Extracts;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Portal.ListExtracts;

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        // PortalExtractVisibility (R5-Q8 default): Issued and Collected (paid) only. Every earlier step is Kaff's own business.
        ExtractStatus[] visible = [ExtractStatus.Issued, ExtractStatus.Collected];

        List<Extract> extracts = await database.Extracts.AsNoTracking()
            .Where(extract => extract.ProjectId == projectId && visible.Contains(extract.Status))
            .OrderBy(extract => extract.Number)
            .ToListAsync(cancellationToken);

        IReadOnlyDictionary<Guid, (Money Received, Money Withheld)> cash =
            await ExtractMoneyQueries.ReceivedAndWithheldAsync(database, [.. extracts.Select(extract => extract.Id)], cancellationToken);

        return Results.Ok(extracts.Select(extract => new PortalExtract(
            extract.Id,
            extract.Number,
            extract.PeriodStart,
            extract.PeriodEnd,
            PortalFormat.Amount(extract.CertifiedValue ?? Money.Zero),
            PortalFormat.Amount(extract.NetPayable ?? Money.Zero),
            PortalFormat.Amount(cash.TryGetValue(extract.Id, out (Money Received, Money Withheld) value) ? value.Received : Money.Zero),
            extract.Status == ExtractStatus.Collected)).ToList());
    }
}
