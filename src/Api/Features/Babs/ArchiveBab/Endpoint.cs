using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Babs.ArchiveBab;

/// <summary>
/// <c>POST /api/babs/{babId}/archive</c> — take a باب out of new work without deleting it. KAFF-213.
/// </summary>
/// <remarks>
/// Same shape as <c>ArchiveCatalogueItem.Endpoint</c> — a verb on a sub-resource, no
/// <c>DELETE /api/babs/{id}</c> anywhere (rule 2). Gated <c>BabManage</c>, company-wide, no
/// assignment — spec.md §2, D-129 §1. Every other role refused <c>403</c> (<c>AC-213-H</c>).
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/babs/{babId:guid}/archive";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.BabManage)
            .WithName("ArchiveBab")
            .WithTags("Babs");
    }
}
