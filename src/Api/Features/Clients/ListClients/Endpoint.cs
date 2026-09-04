using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Clients.ListClients;

/// <summary>
/// <c>GET /api/clients</c> — find a client by name, code or phone. KAFF-124.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b>
/// <c>Permission.ClientManage</c> is <c>CompanyWide</c> and granted to <c>Role.Owner</c> and
/// <c>Role.MarketingSales</c> [Verified: 2026-09-04 @ <c>PermissionCatalogue.cs</c> -&gt; the
/// <c>Permission.ClientManage</c> row] — spec.md §2, D-044 ruling 4. <b>This is the widest-reaching
/// payload in the slice</b>: it returns every client in Kaff, so KAFF-124 rule 4 says a
/// <c>Role.Client</c> user cannot reach it "under any circumstances", and §12 is absolute — a client
/// must never see any other client's data.
/// </para>
/// <para>
/// <b>GET with a query string, and the reasoning is not the same as phone-check's.</b>
/// <c>POST /api/clients/phone-check</c> is a POST because its <i>entire</i> input is a phone number,
/// so every call would put one into an access log and a cached warning would be a stale one. A search
/// box is a name as often as a number, and a list screen that cannot be linked, bookmarked or
/// returned to is a worse screen. <b>Neither endpoint puts a search term into the audit trail</b>:
/// <c>AuditCorrelationMiddleware</c> records <c>Request.Path.Value</c>, which excludes the query
/// string [Verified: 2026-09-04 @
/// <c>src/Api/Common/Middleware/AuditCorrelationMiddleware.cs</c> -&gt; <c>InvokeAsync</c>].
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> The catalogue row is company-wide and a client is
/// project-independent (spec.md §2), so declaring one would make the evaluator look for an assignment
/// to a project that does not exist and refuse every caller including the Owner.
/// <c>EndpointPermissionCoverageTests</c> asserts the endpoint and the row agree.
/// </para>
/// <para>
/// <b>No audit record.</b> It is a read — KAFF-124's audit note in as many words.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/clients";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ClientManage)
            .WithName("ListClients")
            .WithTags("Clients");
    }
}
