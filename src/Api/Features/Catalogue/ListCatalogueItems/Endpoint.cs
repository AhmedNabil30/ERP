using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Catalogue.ListCatalogueItems;

/// <summary>
/// <c>GET /api/catalogue-items</c> — find a catalogue item by code or description. KAFF-203.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> Same row
/// as <c>CreateCatalogueItem</c> and <c>EditCatalogueItem</c> — <c>Permission.CatalogueManage</c>,
/// company-wide, granted to <c>Role.Owner</c> and <c>Role.TechnicalOffice</c> [Verified: 2026-09-09 @
/// <c>PermissionCatalogue.cs</c> -&gt; the <c>Permission.CatalogueManage</c> row] — spec.md §2, §4.1,
/// D-129 §1. Every other role, including <c>Role.Client</c>, is refused <c>403</c> — rule 6, rule 9,
/// <c>AC-203-E</c>, <c>AC-203-F</c>.
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> The catalogue row is company-wide (rule 9), so declaring one would
/// make the evaluator look for an assignment to a project that does not exist and refuse every caller
/// including the Owner.
/// </para>
/// <para>
/// <b>Same route as <c>CreateCatalogueItem</c>, a different verb.</b> <c>POST /api/catalogue-items</c>
/// creates one item; <c>GET /api/catalogue-items?search=…</c> finds them — the ordinary REST shape,
/// matching <c>ListClients</c> next to <c>CreateClient</c>.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/catalogue-items";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.CatalogueManage)
            .WithName("ListCatalogueItems")
            .WithTags("Catalogue");
    }
}
