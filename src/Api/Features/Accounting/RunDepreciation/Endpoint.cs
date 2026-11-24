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

namespace Kaff.Api.Features.Accounting.RunDepreciation;

/// <summary>
/// Finance asks to charge one month of straight-line depreciation on every asset that is due. Posts on the Owner's approval; approving a month already
/// charged writes nothing, and a fully depreciated asset writes nothing. KAFF-700 AC2, AC3, AC5.
/// </summary>
/// <remarks>
/// ponytail: two runs started at the same instant could both read "not yet charged" and both post. Add a unique index over the
/// month's source reference if runs ever overlap.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/treasury/depreciation-runs";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, (Request request, KaffDbContext database, ICurrentUser currentUser, RulingSet rulings, TimeProvider clock, CancellationToken cancellationToken)
                => Handler.HandleAsync(request, database, currentUser, rulings, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostCompany)
            .WithName("RunDepreciation")
            .WithTags("Accounting");
    }
}

/// <param name="Month">Any date in the month to charge. Required; the month must have started.</param>
public sealed record Request(DateOnly? Month);

internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Request request,
        KaffDbContext database,
        ICurrentUser currentUser,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return AccountingEntries.SubmitAsync(
            AccountingRequestKind.Depreciation, null, new MonthPayload(request.Month),
            database, currentUser, rulings, clock, cancellationToken);
    }
}
