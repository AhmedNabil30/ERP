using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Suppliers.GetSupplier;

/// <summary><c>GET /api/suppliers/{supplierId}</c> — one supplier's file, for S-030. KAFF-212.</summary>
/// <remarks>Gated <c>Permission.SupplierManage</c>. No <c>ProjectScope</c>. No audit record — a read.</remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/suppliers/{supplierId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SupplierManage)
            .WithName("GetSupplier")
            .WithTags("Suppliers");
    }
}
