using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Subcontractors.ListTaxRegistrations;

/// <summary>
/// <c>GET /api/subcontractors/tax-registrations</c> — Finance's own reach into the firms it does not
/// otherwise own the record of. decisions.md D-147.
/// </summary>
/// <remarks>
/// <para>
/// Gated <c>Permission.SubcontractorTaxRegistrationEdit</c> — Finance holds no <c>SubcontractorManage</c>
/// row and so has no other route into this table. <b>The projection is the control</b> (D-055 §2): the
/// <c>Response</c> below carries only <c>Id, Code, Name, TaxRegistrationNumber, IsActive</c> — no
/// retention rate, no trade, no phone reaches Finance through this route.
/// </para>
/// <para>No <c>ProjectScope</c> — a firm belongs to no project. No audit record — a read.</para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/subcontractors/tax-registrations";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SubcontractorTaxRegistrationEdit)
            .WithName("ListSubcontractorTaxRegistrations")
            .WithTags("Subcontractors");
    }
}
