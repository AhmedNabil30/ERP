using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Closure.ApproveHoldRelease;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/hold-release/approval</c>. The Owner approves the waiting hold release request and
/// the one <c>HoldRelease</c> posting is written in the same save, for the hold balance summed from postings at that moment.
/// Gated <c>FinancialMovementApprove</c> with the project from the route (spec.md section 9: the Owner approves all
/// financial movements). The user who asked may not approve.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/hold-release/approval";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.FinancialMovementApprove, ProjectScope.FromRoute())
            .WithName("ApproveHoldRelease")
            .WithTags("Closure");
    }
}
