using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DailyLogs.DraftExpense;

/// <summary>
/// <c>/api/projects/{projectId}/daily-logs/expense-drafts</c>. Stores an engineer's site expense as a draft. Writes no posting; Accounts confirms and posts it later. Gated <c>SiteExpenseDraft</c> with the project from
/// the route, so a Site Engineer who is not assigned to the project is refused (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/daily-logs/expense-drafts";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SiteExpenseDraft, ProjectScope.FromRoute())
            .WithName("DraftExpense")
            .WithTags("DailyLogs");
    }
}
