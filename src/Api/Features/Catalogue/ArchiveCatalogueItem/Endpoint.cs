using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Catalogue.ArchiveCatalogueItem;

/// <summary>
/// <c>POST /api/catalogue-items/{catalogueItemId}/archive</c> — take a catalogue item out of new work
/// without deleting it. KAFF-206.
/// </summary>
/// <remarks>
/// <para>
/// <b>A verb on a sub-resource, and deliberately not <c>DELETE /api/catalogue-items/{id}</c></b> — the
/// same shape as <c>ArchiveClient.Endpoint</c> and for the same reason: rule 2, KAFF-123's precedent.
/// spec.md §4.4 makes a signed BOQ line a copy with no foreign key back to this row, and a price list
/// that can be deleted from breaks every document that already quoted the deleted row.
/// <c>AC-206-B</c> asserts no delete route exists on the whole surface, by enumerating what the host
/// actually mapped.
/// </para>
/// <para>
/// <b>No body.</b> One act, no options — the confirm dialog is the operator's check, not a flag
/// (<c>ux/screen-inventory.md</c> -&gt; <c>S-017</c>).
/// </para>
/// <para>
/// <b>Gated <c>CatalogueManage</c>, company-wide</b> [Verified: 2026-09-09 @
/// <c>PermissionCatalogue.cs</c> -&gt; the <c>Permission.CatalogueManage</c> row] — spec.md §2, §4.1,
/// D-129 §1 (<c>Q12</c>). <c>AC-206-G</c>.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/catalogue-items/{catalogueItemId:guid}/archive";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.CatalogueManage)
            .WithName("ArchiveCatalogueItem")
            .WithTags("Catalogue");
    }
}
