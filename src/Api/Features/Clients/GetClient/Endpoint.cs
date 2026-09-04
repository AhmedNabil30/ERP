using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Clients.GetClient;

/// <summary>
/// <c>GET /api/clients/{clientId}</c> — one client's file, for S-014. KAFF-126.
/// </summary>
/// <remarks>
/// <para>
/// <b>Gated <c>ClientManage</c>, company-wide</b> [Verified: 2026-09-04 @
/// <c>PermissionCatalogue.cs</c> -&gt; the <c>Permission.ClientManage</c> row]. It is the one payload
/// in the slice carrying <c>Notes</c>, which spec.md §12 forbids a client ever seeing — and
/// <c>Role.Client</c> does not hold this row. <b>On a read there is no audit backstop</b>: an ungated
/// version of this route would hand every client's internal notes to anybody who asked, and nothing
/// else would notice (decisions.md D-110 §2).
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> A client is project-independent (spec.md §2) and the catalogue row
/// is company-wide, so declaring one would refuse every caller including the Owner.
/// </para>
/// <para>
/// <b>No audit record.</b> It is a read.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/clients/{clientId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.ClientManage)
            .WithName("GetClient")
            .WithTags("Clients");
    }
}
