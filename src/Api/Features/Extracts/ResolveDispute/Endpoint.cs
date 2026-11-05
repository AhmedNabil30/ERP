using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.ResolveDispute;

/// <summary>
/// <c>POST /api/projects/{projectId}/extracts/{id}/dispute-resolution</c>. Finance resolves a disputed extract as a credit
/// note, which is raised for the Owner to approve (KAFF-511). A revised-extract resolution is refused: it is unruled
/// (Q106). Gated <c>ExtractDisputeResolve</c>, holder unresolved. Role and assignment are both required. Posts nothing.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/dispute-resolution";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractDisputeResolve, ProjectScope.FromRoute())
            .WithName("ResolveExtractDispute")
            .WithTags("Extracts");
    }
}
