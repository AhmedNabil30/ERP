using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Catalogue.MoveCatalogueItem;

/// <summary>
/// <c>PUT /api/catalogue-items/{catalogueItemId}/bab</c> — move an item to a different باب. KAFF-205.
/// </summary>
/// <remarks>
/// Gated <c>CatalogueManage</c>, company-wide, no assignment (KAFF-205 rule 8, D-129 §1). Every other
/// role refused <c>403</c> (<c>AC-205-H</c>).
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/catalogue-items/{catalogueItemId:guid}/bab";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.CatalogueManage)
            .WithName("MoveCatalogueItem")
            .WithTags("Catalogue");
    }
}
