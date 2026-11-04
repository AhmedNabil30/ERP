using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Extracts.ReverseCollection;

/// <summary>
/// <c>Post /api/projects/{projectId}/extracts/{id}/collection-reversal</c>. Finance corrects a mistaken collection
/// with a stored reason (KAFF-316 AC-316-F). Gated ExtractCollect, holder unresolved (Q97): reversing a collection
/// is the same act, with the same holder, as recording one. Role and assignment are both required (spec.md section 9).
/// </summary>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/extracts/{id:guid}/collection-reversal";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ExtractCollect, ProjectScope.FromRoute())
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("ReverseExtractCollection")
            .WithTags("Extracts");
    }
}
