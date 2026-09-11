using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Catalogue.ImportCatalogue;

/// <summary>
/// <c>POST /api/catalogue-items/import</c> and <c>GET /api/catalogue-items/import-template</c>.
/// KAFF-200. Both live in this one feature — decisions.md D-136: "the template is part of S-019's
/// feature, not a separate one."
/// </summary>
/// <remarks>
/// <para>
/// <b>Both gated <c>CatalogueManage</c>, company-wide</b> — spec.md §2, §4.1, D-129 §1 (<c>Q12</c>).
/// Every other role is refused <c>403</c> before either handler runs (<c>AC-200-F</c>).
/// </para>
/// <para>
/// <b><c>.DisableAntiforgery()</c> on the import route.</b> A minimal-API endpoint that binds
/// <c>IFormFile</c> gets antiforgery metadata by convention, and this pipeline has no
/// <c>UseAntiforgery</c> — the cookie is <c>SameSite=Strict</c>, which is the CSRF control
/// (decisions.md D-050). Framework behaviour, confirmed by this slice's first upload test rather than
/// only asserted here (decisions.md D-136).
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string ImportRoute = "/api/catalogue-items/import";
    public const string TemplateRoute = "/api/catalogue-items/import-template";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(ImportRoute, Handler.ImportAsync)
            .DisableAntiforgery()
            .RequirePermission(Permission.CatalogueManage)
            .WithName("ImportCatalogue")
            .WithTags("Catalogue");

        app.MapGet(TemplateRoute, Handler.DownloadTemplate)
            .RequirePermission(Permission.CatalogueManage)
            .WithName("DownloadCatalogueImportTemplate")
            .WithTags("Catalogue");
    }
}
