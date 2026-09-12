using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Catalogue.ReimportCatalogue;

/// <summary>
/// <c>POST /api/catalogue-items/import/preview</c> and <c>POST /api/catalogue-items/import/confirm</c>.
/// KAFF-201 — S-019's confirm dialog. Neither route is a sync, a schedule or a watcher: both run once,
/// against one uploaded file, only when a human calls them.
/// </summary>
/// <remarks>
/// Gated <c>CatalogueManage</c>, company-wide — the same row as <c>KAFF-200</c>'s import
/// (spec.md §2, D-129 §1 / <c>Q12</c>). <c>.DisableAntiforgery()</c> for the same reason KAFF-200's
/// route needs it: a minimal-API endpoint binding <c>IFormFile</c> gets antiforgery metadata by
/// convention, and the pipeline's CSRF control is the <c>SameSite=Strict</c> cookie (D-050), not
/// <c>UseAntiforgery</c>.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string PreviewRoute = "/api/catalogue-items/import/preview";
    public const string ConfirmRoute = "/api/catalogue-items/import/confirm";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(PreviewRoute, Handler.PreviewAsync)
            .DisableAntiforgery()
            .RequirePermission(Permission.CatalogueManage)
            .WithName("PreviewCatalogueReimport")
            .WithTags("Catalogue");

        app.MapPost(ConfirmRoute, Handler.ConfirmAsync)
            .DisableAntiforgery()
            .RequirePermission(Permission.CatalogueManage)
            .WithName("ConfirmCatalogueReimport")
            .WithTags("Catalogue");
    }
}
