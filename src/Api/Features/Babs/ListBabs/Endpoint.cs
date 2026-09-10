using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Babs.ListBabs;

/// <summary>
/// <c>GET /api/babs</c> — a flat list of أبواب, so any catalogue screen can render a باب name and
/// group next to a bare <c>BabId</c>. Unblocks <c>AC-203-I</c>'s باب-then-code ordering; the tree
/// itself is <c>KAFF-204</c>, which is not Ready.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b>
/// <c>Permission.BabManage</c> — company-wide, granted to <c>Role.Owner</c> and
/// <c>Role.TechnicalOffice</c> [Verified: 2026-09-10 @ <c>PermissionCatalogue.cs</c> -&gt; the
/// <c>Permission.BabManage</c> row] — spec.md §2, KAFF-204 rule 9, D-129 §1. **Not
/// <c>CatalogueManage</c>**: أبواب already carry their own permission row, granted to the identical
/// pair of roles, so borrowing the catalogue's row would have worked by coincidence and be wrong the
/// day the two grants diverge. Every other role is refused <c>403</c>.
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> أبواب belong to no project (KAFF-204 rule 9), so declaring one would
/// make the evaluator look for an assignment that does not exist and refuse every caller including
/// the Owner.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/babs";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.BabManage)
            .WithName("ListBabs")
            .WithTags("Babs");
    }
}
