using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Subcontractors.ListSubcontractors;

/// <summary><c>GET /api/subcontractors</c> — S-028's list. KAFF-211.</summary>
/// <remarks>
/// Gated <c>Permission.SubcontractorManage</c>, company-wide — Finance is refused (AC-211-J); its own
/// reach into this table is the separate, narrower <c>GET /api/subcontractors/tax-registrations</c>
/// (D-147). No <c>ProjectScope</c>: a firm belongs to no project. No audit record — a read.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/subcontractors";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SubcontractorManage)
            .WithName("ListSubcontractors")
            .WithTags("Subcontractors");
    }
}
