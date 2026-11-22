using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.RejectHoldRelease;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/hold-release/rejection</c>. The Owner rejects the waiting hold release request
/// with a stored reason. It returns to Finance, who may ask again. Nothing is posted. Gated
/// <c>FinancialMovementApprove</c> with the project from the route: role and assignment are both required.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/hold-release/rejection";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.FinancialMovementApprove, ProjectScope.FromRoute())
            .WithName("RejectHoldRelease")
            .WithTags("Closure");
    }
}
