using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Accounting;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.ListAssets;

/// <summary>The asset register. Accumulated depreciation and book value are derived from each asset's postings, never stored. KAFF-700 AC2.</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/treasury/assets";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, async (KaffDbContext database, CancellationToken cancellationToken) =>
            {
                List<AssetRegisterEntry> assets = await database.AssetRegisterEntries
                    .OrderBy(asset => asset.PurchaseDate)
                    .ThenBy(asset => asset.Id)
                    .ToListAsync(cancellationToken);

                List<Guid> ids = [.. assets.Select(asset => asset.Id)];

                List<Posting> postings = await database.Postings
                    .Where(posting => posting.Type == PostingType.Depreciation
                                      && posting.SourceDocumentType == SourceDocumentType.AssetRegisterEntry
                                      && ids.Contains(posting.SourceDocumentId))
                    .ToListAsync(cancellationToken);

                List<Row> rows =
                [
                    .. assets.Select(asset =>
                    {
                        Money accumulated = ScheduleLedger.Total(ScheduleLedger.Active(postings.Where(posting => posting.SourceDocumentId == asset.Id)));

                        // Derived on every read, never stored: the next month's charge, capped at what is left to depreciate.
                        Money remaining = asset.Cost - accumulated;
                        Money monthly = !remaining.IsPositive ? Money.Zero : asset.MonthlyCharge < remaining ? asset.MonthlyCharge : remaining;

                        return new Row(
                            asset.Id, asset.Name, asset.Category, asset.Cost, asset.Rate.Percent, asset.PurchaseDate, accumulated, remaining, monthly);
                    }),
                ];

                return Microsoft.AspNetCore.Http.Results.Ok(rows);
            })
            .RequirePermission(Permission.TreasuryReadCompany)
            .WithName("ListAssets")
            .WithTags("Accounting");
    }
}

public sealed record Row(
    Guid Id, string Name, AssetCategory Category, Money Cost, decimal RatePercent, DateOnly PurchaseDate, Money Accumulated, Money BookValue, Money MonthlyDepreciation);
