using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Suppliers.PhoneCheck;

/// <summary><c>POST /api/suppliers/phone-check</c> — whose number is this? decisions.md D-141.</summary>
/// <remarks>
/// Gated <c>SupplierManage</c>, same as registration and editing. POST, not GET. No
/// <c>ProjectScope</c> — the catalogue row is company-wide.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/suppliers/phone-check";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SupplierManage)
            .WithName("SupplierPhoneCheck")
            .WithTags("Suppliers");
    }
}
