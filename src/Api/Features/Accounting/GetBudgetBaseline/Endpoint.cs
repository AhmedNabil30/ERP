using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Results;
using Kaff.Domain.Accounting;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Accounting.GetBudgetBaseline;

/// <summary>Reads a project's budget baseline. Owner and Finance only, with the assignment; a site engineer is refused (Nabil, 2026-11-24). KAFF-709.</summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/budget-baseline";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, async ([FromRoute] Guid projectId, KaffDbContext database, CancellationToken cancellationToken) =>
            {
                BudgetBaseline? baseline = await database.BudgetBaselines.FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, cancellationToken);

                return baseline is null
                    ? ResultExtensions.Problem(AccountingErrors.BaselineNotSet)
                    : Microsoft.AspNetCore.Http.Results.Ok(Handler.ToResponse(baseline));
            })
            .RequirePermission(Permission.BudgetBaselineRead, ProjectScope.FromRoute())
            .WithName("GetBudgetBaseline")
            .WithTags("Accounting");
    }
}

public sealed record Response(Guid ProjectId, Money CostTotal, decimal TolerancePercent, Money Limit, DateTimeOffset SetAt);

internal static class Handler
{
    public static Response ToResponse(BudgetBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        return new Response(baseline.ProjectId, baseline.CostTotal, baseline.Tolerance.Percent, baseline.Limit, baseline.SetAt);
    }
}
