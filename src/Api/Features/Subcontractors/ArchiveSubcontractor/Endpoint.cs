using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Subcontractors.ArchiveSubcontractor;

/// <summary>
/// <c>POST /api/subcontractors/{subcontractorId}/archive</c> — take a firm off the working list.
/// KAFF-211 rule 10.
/// </summary>
/// <remarks>
/// A verb on a sub-resource, not <c>DELETE</c> — CLAUDE.md, KAFF-123's precedent: archiving replaces
/// deletion, and there is no delete path. Gated <c>Permission.SubcontractorManage</c>, company-wide.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/subcontractors/{subcontractorId:guid}/archive";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SubcontractorManage)
            .WithName("ArchiveSubcontractor")
            .WithTags("Subcontractors");
    }
}
