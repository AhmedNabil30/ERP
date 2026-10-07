using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Babs.UnarchiveBab;

/// <summary>
/// <c>POST /api/babs/{babId}/unarchive</c> — bring an archived باب back into new work. KAFF-943, D-130 §4 (<c>Q66</c>).
/// </summary>
/// <remarks>
/// Same verb-on-a-sub-resource shape as <c>ArchiveBab</c> and <c>UnarchiveCatalogueItem</c>: no body, no
/// delete. Gated <c>BabManage</c>, company-wide, no assignment — exactly as archiving is.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/babs/{babId:guid}/unarchive";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.BabManage)
            .WithName("UnarchiveBab")
            .WithTags("Babs");
    }
}
