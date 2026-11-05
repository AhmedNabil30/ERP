using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.RaiseDispute;

/// <summary>
/// <c>POST /api/projects/{projectId}/extracts/{id}/dispute</c>. Finance records that the client disputes an issued extract,
/// with a stored reason (KAFF-511). Gated <c>ExtractDisputeRaise</c>, holder unresolved (Q106). Role and assignment are
/// both required (spec.md section 9). Posts nothing.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/dispute";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractDisputeRaise, ProjectScope.FromRoute())
            .WithName("RaiseExtractDispute")
            .WithTags("Extracts");
    }
}
