using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Subcontractors.GetSubcontractor;

/// <summary><c>GET /api/subcontractors/{subcontractorId}</c> — one firm's file, for S-029. KAFF-211.</summary>
/// <remarks>
/// Gated <c>Permission.SubcontractorManage</c>. Carries the tax registration number read-only (rule
/// 8) — see <c>Response</c>. No <c>ProjectScope</c>. No audit record — a read.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/subcontractors/{subcontractorId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SubcontractorManage)
            .WithName("GetSubcontractor")
            .WithTags("Subcontractors");
    }
}
