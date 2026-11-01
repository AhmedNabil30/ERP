using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DailyLogs.RecordQuantity;

/// <summary>
/// <c>/api/projects/{projectId}/daily-logs/quantities</c>. Records an executed quantity delta against a BOQ line. KAFF-600 rules 3 to 6. Gated <c>DailyLogWrite</c> with the project from
/// the route, so a Site Engineer who is not assigned to the project is refused (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/daily-logs/quantities";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DailyLogWrite, ProjectScope.FromRoute())
            .WithName("RecordQuantity")
            .WithTags("DailyLogs");
    }
}
