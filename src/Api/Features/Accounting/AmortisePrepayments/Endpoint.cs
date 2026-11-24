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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.AmortisePrepayments;

/// <summary>
/// Finance asks to move one month of every prepayment of one tag to expense. Posts on the Owner's approval; approving a month already charged writes nothing. KAFF-701 AC2.
/// </summary>
/// <remarks>
/// ponytail: two runs started at the same instant could both read "not yet charged" and both post. A unique index over the
/// month's source reference would close it, at the cost of making a reversed month hard to charge again. Add it if runs ever overlap.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string CompanyRoute = "/api/treasury/prepayment-amortisations";
    public const string ProjectRoute = "/api/projects/{projectId:guid}/treasury/prepayment-amortisations";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(CompanyRoute, (Request request, KaffDbContext database, ICurrentUser currentUser, RulingSet rulings, TimeProvider clock, CancellationToken cancellationToken)
                => Handler.HandleAsync(null, request, database, currentUser, rulings, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostCompany)
            .WithName("AmortiseCompanyPrepayments")
            .WithTags("Accounting");

        app.MapPost(ProjectRoute, ([FromRoute] Guid projectId, Request request, KaffDbContext database, ICurrentUser currentUser, RulingSet rulings, TimeProvider clock, CancellationToken cancellationToken)
                => Handler.HandleAsync(projectId, request, database, currentUser, rulings, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostProject, ProjectScope.FromRoute())
            .WithName("AmortiseProjectPrepayments")
            .WithTags("Accounting");
    }
}

/// <param name="Month">Any date in the month to charge. Required; the month must have started.</param>
public sealed record Request(DateOnly? Month);

internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid? projectId,
        Request request,
        KaffDbContext database,
        ICurrentUser currentUser,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return AccountingEntries.SubmitAsync(
            AccountingRequestKind.PrepaymentAmortisation, projectId, new MonthPayload(request.Month),
            database, currentUser, rulings, clock, cancellationToken);
    }
}
