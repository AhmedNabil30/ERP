using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Results;
using Kaff.Domain.Accounting;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.ReleaseAccrual;

/// <summary>The real cost has landed: Finance asks to clear the accrual so the cost is not counted twice. Posts on the Owner's approval. KAFF-701 AC3.</summary>
public sealed class Endpoint : IEndpoint
{
    public const string CompanyRoute = "/api/treasury/accruals/{accrualId:guid}/release";
    public const string ProjectRoute = "/api/projects/{projectId:guid}/treasury/accruals/{accrualId:guid}/release";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(CompanyRoute, (
                [FromRoute] Guid accrualId, Request request, KaffDbContext database, ICurrentUser currentUser, RulingSet rulings, TimeProvider clock, CancellationToken cancellationToken)
                => Handler.HandleAsync(null, accrualId, request, database, currentUser, rulings, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostCompany)
            .WithName("ReleaseCompanyAccrual")
            .WithTags("Accounting");

        app.MapPost(ProjectRoute, (
                [FromRoute] Guid projectId, [FromRoute] Guid accrualId, Request request, KaffDbContext database, ICurrentUser currentUser, RulingSet rulings, TimeProvider clock, CancellationToken cancellationToken)
                => Handler.HandleAsync(projectId, accrualId, request, database, currentUser, rulings, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostProject, ProjectScope.FromRoute())
            .WithName("ReleaseProjectAccrual")
            .WithTags("Accounting");
    }
}

/// <param name="ReleasedOn">The accounting date of the release. Required.</param>
public sealed record Request(DateOnly? ReleasedOn);

internal static class Handler
{
    public static Task<IResult> HandleAsync(
        Guid? projectId,
        Guid accrualId,
        Request request,
        KaffDbContext database,
        ICurrentUser currentUser,
        RulingSet rulings,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return AccountingEntries.SubmitAsync(
            AccountingRequestKind.AccrualRelease, projectId, new AccrualReleasePayload(accrualId, request.ReleasedOn),
            database, currentUser, rulings, clock, cancellationToken);
    }
}
