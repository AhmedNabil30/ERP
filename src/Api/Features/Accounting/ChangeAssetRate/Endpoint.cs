using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Results;
using Kaff.Domain.Accounting;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.ChangeAssetRate;

/// <summary>Finance changes one asset's annual rate. Refused once the asset has depreciation posted. KAFF-700 AC1.</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/treasury/assets/{assetId:guid}/rate";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, ([FromRoute] Guid assetId, Request request, KaffDbContext database, CancellationToken cancellationToken)
                => Handler.HandleAsync(assetId, request, database, cancellationToken))
            .RequirePermission(Permission.TreasuryPostCompany)
            .WithName("ChangeAssetRate")
            .WithTags("Accounting");
    }
}

/// <param name="RatePercent">The new annual rate, above 0 and at most 100.</param>
public sealed record Request(decimal RatePercent);

public sealed record Response(Guid Id, decimal RatePercent);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(Guid assetId, Request request, KaffDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);

        AssetRegisterEntry? asset = await database.AssetRegisterEntries.FirstOrDefaultAsync(candidate => candidate.Id == assetId, cancellationToken);
        if (asset is null)
        {
            return ResultExtensions.Problem(AccountingErrors.AssetNotFound);
        }

        bool posted = await database.Postings.AnyAsync(
            posting => posting.SourceDocumentType == SourceDocumentType.AssetRegisterEntry && posting.SourceDocumentId == asset.Id,
            cancellationToken);

        Result changed = asset.ChangeRate(request.RatePercent, posted);
        if (changed.IsFailure)
        {
            return ResultExtensions.Problem(changed.Error);
        }

        Error? guard = await GuardedSave.SaveAsync(database, cancellationToken);
        if (guard is not null)
        {
            return ResultExtensions.Problem(guard);
        }

        return Microsoft.AspNetCore.Http.Results.Ok(new Response(asset.Id, asset.Rate.Percent));
    }
}
