using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.RequestSubcontractorRetentionReversal;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/subcontractors/{subcontractorId:guid}/retention-release/reversal</c>. Finance asks, with a reason, for the reversal of a posted retention release (KAFF-806 AC-806-D). Nothing is posted: the Owner approves at <c>.../reversal/approval</c>. Gated
/// <c>SubcontractorRetentionRelease</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/subcontractors/{subcontractorId:guid}/retention-release/reversal";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SubcontractorRetentionRelease, ProjectScope.FromRoute())
            .WithName("RequestSubcontractorRetentionReversal")
            .WithTags("Closure");
    }
}
