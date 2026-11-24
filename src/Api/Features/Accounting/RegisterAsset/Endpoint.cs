using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Results;
using Kaff.Domain.Accounting;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.RegisterAsset;

/// <summary>
/// Finance puts a purchased asset in the register. The rate starts at the category default and may be overridden per asset.
/// KAFF-700 AC1, AC4, AC6. The purchase posting is not written: its account pair is pending (R4-A2).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/treasury/assets";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, (Request request, KaffDbContext database, ICurrentUser currentUser, TimeProvider clock, CancellationToken cancellationToken)
                => Handler.HandleAsync(request, database, currentUser, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostCompany)
            .WithName("RegisterAsset")
            .WithTags("Accounting");
    }
}

/// <param name="Name">What the asset is.</param>
/// <param name="Category">Sets the default rate (spec.md section 6.6).</param>
/// <param name="Cost">Always positive.</param>
/// <param name="RatePercent">Optional override of the category's annual rate, above 0 and at most 100.</param>
/// <param name="PurchaseDate">Required. A date inside a closed month is refused.</param>
/// <param name="AccumulatedDepreciationAccountId">The AccumulatedDepreciation account its charges credit.</param>
/// <param name="DepreciationExpenseAccountId">The DepreciationExpense account its charges debit.</param>
public sealed record Request(
    string? Name,
    AssetCategory Category,
    Money Cost,
    decimal? RatePercent,
    DateOnly? PurchaseDate,
    Guid AccumulatedDepreciationAccountId,
    Guid DepreciationExpenseAccountId);

public sealed record Response(Guid Id, string Name, AssetCategory Category, Money Cost, decimal RatePercent, DateOnly PurchaseDate);

internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        ICurrentUser currentUser,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(currentUser);

        Result dated = TypedPostingDate.Check(request.PurchaseDate, SiteTime.Today(clock));
        if (dated.IsFailure)
        {
            return ResultExtensions.Problem(dated.Error);
        }

        DateOnly purchaseDate = request.PurchaseDate!.Value;

        Account? accumulated = await database.Accounts.FirstOrDefaultAsync(account => account.Id == request.AccumulatedDepreciationAccountId, cancellationToken);
        Account? expense = await database.Accounts.FirstOrDefaultAsync(account => account.Id == request.DepreciationExpenseAccountId, cancellationToken);

        if (accumulated is null || expense is null)
        {
            return ResultExtensions.Problem(TreasuryErrors.AccountNotFound);
        }

        // spec.md section 6.6: a closed month is immutable, so an asset dated inside one is refused.
        Error? closed = await AccountingSupport.ClosedPeriodAsync(database, purchaseDate, cancellationToken);
        if (closed is not null)
        {
            return ResultExtensions.Problem(closed);
        }

        Result<AssetRegisterEntry> created = AssetRegisterEntry.Register(
            request.Name, request.Category, request.Cost, request.RatePercent, purchaseDate,
            accumulated, expense, currentUser.UserId ?? Guid.Empty, clock.GetUtcNow());

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        database.AssetRegisterEntries.Add(created.Value);

        Error? guard = await GuardedSave.SaveAsync(database, cancellationToken);
        if (guard is not null)
        {
            return ResultExtensions.Problem(guard);
        }

        AssetRegisterEntry asset = created.Value;

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/treasury/assets/{asset.Id}",
            new Response(asset.Id, asset.Name, asset.Category, asset.Cost, asset.Rate.Percent, asset.PurchaseDate));
    }
}
