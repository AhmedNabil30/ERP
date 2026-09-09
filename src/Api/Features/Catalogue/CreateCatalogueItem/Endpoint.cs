using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Catalogue.CreateCatalogueItem;

/// <summary>
/// <c>POST /api/catalogue-items</c> — the Technical Office adds one catalogue item by hand. KAFF-202.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b> It names
/// <c>Permission.CatalogueManage</c>, whose catalogue row is <c>CompanyWide</c> and granted to
/// <c>Role.Owner</c> and <c>Role.TechnicalOffice</c> [Verified: 2026-09-09 @
/// <c>PermissionCatalogue.cs</c> -&gt; the <c>Permission.CatalogueManage</c> row] — spec.md §2, §4.1,
/// and D-129 §1. Every other role is refused <c>403</c> (<c>AC-202-H</c>).
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> The catalogue row is company-wide — a catalogue belongs to no project
/// (rule 10) — so declaring a scope would make the evaluator look for an assignment to a project that
/// does not exist and refuse every caller including the Owner.
/// </para>
/// <para>
/// <b>No <c>ValidationFilter</c>.</b> Every shape rule this request carries — a required code, a
/// required Arabic description, a required unit, a non-negative price — is the domain's, already
/// enforced by <c>CatalogueItem.Create</c>. There is nothing left for a validator to check that the
/// domain cannot see, unlike <c>CreateClient.Validator</c>'s enum-default case: nothing here binds to a
/// value the type system admits but the domain rejects.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/catalogue-items";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.CatalogueManage)
            .WithName("CreateCatalogueItem")
            .WithTags("Catalogue");
    }
}
