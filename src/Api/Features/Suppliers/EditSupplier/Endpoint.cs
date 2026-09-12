using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Suppliers.EditSupplier;

/// <summary><c>PUT /api/suppliers/{supplierId}</c> — Finance corrects a supplier's whole profile. KAFF-212.</summary>
/// <remarks>
/// Gated <c>Permission.SupplierManage</c>, company-wide, the same row <c>CreateSupplier</c> uses — the
/// Technical Office is refused here too (rule 1). No <c>ProjectScope</c>: a supplier belongs to no
/// project (rule 9).
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/suppliers/{supplierId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SupplierManage)
            .WithName("EditSupplier")
            .WithTags("Suppliers");
    }
}
