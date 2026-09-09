using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Catalogue.UnarchiveCatalogueItem;

/// <summary>
/// <c>POST /api/catalogue-items/{catalogueItemId}/unarchive</c> — bring an archived item back into new
/// work. KAFF-206, D-130 §4 (<c>Q66</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists at all.</b> Codes are unique (spec.md §4.5, <c>ux_catalogue_items_code</c>), so
/// without an un-archive path a mis-archived item can only come back under a new code — the catalogue
/// then carries two codes for one real thing, permanently. <c>MasterDataErrors.NotArchived</c> was
/// declared for exactly this shape and, before this endpoint, was returned by nothing.
/// </para>
/// <para>
/// <b>Placement.</b> KAFF-206's own <i>"Not in this story"</i> section left which story builds this to
/// the Scrum Master; it is built here, in this story, per the brief that placed it. The mechanism
/// itself lives once on <c>CatalogueItem</c> in <c>Domain/</c>, per CLAUDE.md — the same shape `Q66`
/// answers for a client (`Q39`), not generalised into a shared archive service (ponytail: two similar
/// handlers beat one clever abstraction for two call sites).
/// </para>
/// <para>
/// <b>Same verb-on-a-sub-resource shape as <c>ArchiveCatalogueItem</c>, no body, no delete.</b> Gated
/// <c>CatalogueManage</c>, company-wide, same as every other catalogue endpoint.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/catalogue-items/{catalogueItemId:guid}/unarchive";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.CatalogueManage)
            .WithName("UnarchiveCatalogueItem")
            .WithTags("Catalogue");
    }
}
