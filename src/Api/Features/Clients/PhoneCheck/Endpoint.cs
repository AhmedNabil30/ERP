using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Clients.PhoneCheck;

/// <summary>
/// <c>POST /api/clients/phone-check</c> — whose number is this? KAFF-119.
/// </summary>
/// <remarks>
/// <para>
/// <b>Gated exactly as hard as the client master itself, and for the same reason.</b> This endpoint
/// returns client <i>names</i>. A route called "check" reads as innocuous and is precisely where
/// <c>Role.Client</c> gets forgotten — a portal user who could call it would learn the name of every
/// client whose number they can guess, which spec.md §12 forbids absolutely.
/// <c>Permission.ClientManage</c> is <c>CompanyWide</c> and granted to <c>Role.Owner</c> and
/// <c>Role.MarketingSales</c> alone [Verified: 2026-09-04 @ <c>PermissionCatalogue.cs</c> -&gt; the
/// <c>Permission.ClientManage</c> row], and <c>Role.Client</c> is not among them
/// (<c>AC-119-G</c>, decisions.md D-044 ruling 4).
/// </para>
/// <para>
/// <b>POST, not GET.</b> A phone number in a query string reaches the request path, which is written
/// onto every audit record this request produces [Verified: 2026-09-04 @
/// <c>src/Domain/Auditing/IAuditContext.cs</c> -&gt; <c>RequestPath</c>], and into every access log
/// on the way. A POST body reaches neither, and a stale warning cannot be served from a cache.
/// </para>
/// <para>
/// No <c>ProjectScope</c>: the catalogue row is company-wide, so declaring one would refuse every
/// caller including the Owner, and <c>EndpointPermissionCoverageTests</c> asserts the two agree.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/clients/phone-check";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ClientManage)
            .WithName("ClientPhoneCheck")
            .WithTags("Clients");
    }
}
