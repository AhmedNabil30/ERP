using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Suppliers.ArchiveSupplier;

/// <summary><c>POST /api/suppliers/{supplierId}/archive</c> — take a supplier off the working list. KAFF-212 rule 8.</summary>
/// <remarks>
/// A verb on a sub-resource, not <c>DELETE</c> — CLAUDE.md, KAFF-123's precedent: archiving replaces
/// deletion, and there is no delete path. Gated <c>Permission.SupplierManage</c>, company-wide.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/suppliers/{supplierId:guid}/archive";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SupplierManage)
            .WithName("ArchiveSupplier")
            .WithTags("Suppliers");
    }
}
