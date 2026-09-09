using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Catalogue.EditCatalogueItem;

/// <summary>
/// <c>PUT /api/catalogue-items/{catalogueItemId}</c> — corrects one catalogue item. KAFF-202.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> Same row
/// as <c>CreateCatalogueItem</c> — <c>Permission.CatalogueManage</c>, company-wide, granted to
/// <c>Role.Owner</c> and <c>Role.TechnicalOffice</c> (spec.md §2, §4.1, D-129 §1). Every other role is
/// refused <c>403</c> (<c>AC-202-H</c>).
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>, and the route's <c>{catalogueItemId}</c> is not one.</b> The catalogue
/// row is company-wide; a catalogue belongs to no project (rule 10).
/// </para>
/// <para>
/// <b>No <c>ValidationFilter</c></b> — the same reasoning as <c>CreateCatalogueItem.Endpoint</c>.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/catalogue-items/{catalogueItemId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.CatalogueManage)
            .WithName("EditCatalogueItem")
            .WithTags("Catalogue");
    }
}
