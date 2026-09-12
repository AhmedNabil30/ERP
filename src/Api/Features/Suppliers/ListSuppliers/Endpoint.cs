using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Suppliers.ListSuppliers;

/// <summary><c>GET /api/suppliers</c> — S-030's list. KAFF-212.</summary>
/// <remarks>
/// Gated <c>Permission.SupplierManage</c>, company-wide — the Technical Office is refused (rule 1). No
/// <c>ProjectScope</c>: a supplier belongs to no project. No audit record — a read.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/suppliers";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SupplierManage)
            .WithName("ListSuppliers")
            .WithTags("Suppliers");
    }
}
