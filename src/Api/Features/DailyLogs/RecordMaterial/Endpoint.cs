using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DailyLogs.RecordMaterial;

/// <summary>
/// <c>/api/projects/{projectId}/daily-logs/materials</c>. Records material on site, by type, with a stockpile (تشوينات) flag. A count, never a value. Gated <c>DailyLogWrite</c> with the project from
/// the route, so a Site Engineer who is not assigned to the project is refused (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/daily-logs/materials";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DailyLogWrite, ProjectScope.FromRoute())
            .WithName("RecordMaterial")
            .WithTags("DailyLogs");
    }
}
