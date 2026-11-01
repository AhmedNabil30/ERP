using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.PrepareExtract;

/// <summary>
/// <c>POST /api/projects/{projectId}/extracts</c>. Gated <c>ExtractPrepare</c> (site engineer, Junior level up),
/// project-scoped: an engineer not assigned to the project is refused. The response is money-free for this
/// caller; the figures go to those who hold the document, deduction or approval permissions.
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractPrepare, ProjectScope.FromRoute())
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("PrepareExtract")
            .WithTags("Extracts");
    }
}
