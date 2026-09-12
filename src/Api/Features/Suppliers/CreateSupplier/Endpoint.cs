using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Suppliers.CreateSupplier;

/// <summary><c>POST /api/suppliers</c> — Finance (or the Owner, D-129 §1) registers one supplier. KAFF-212.</summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b>
/// <c>Permission.SupplierManage</c> is <c>CompanyWide</c>, granted to <c>Role.Owner</c> and
/// <c>Role.Finance</c> [Verified: 2026-09-12 @ <c>PermissionCatalogue.cs</c> -&gt; the
/// <c>Permission.SupplierManage</c> row] — spec.md §2, D-044 ruling 4, D-129 §1. **The Technical
/// Office is deliberately absent** — it owns the subcontractor, not the supplier (rule 1).
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> A supplier belongs to no project (rule 9), so declaring one would
/// make the evaluator look for an assignment that does not exist and refuse every caller, Owner
/// included.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/suppliers";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SupplierManage)
            .WithName("CreateSupplier")
            .WithTags("Suppliers");
    }
}
