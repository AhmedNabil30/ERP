using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Subcontractors.EditSubcontractor;

/// <summary>
/// <c>PUT /api/subcontractors/{subcontractorId}</c> — the Technical Office corrects a firm's profile
/// and retention rate. KAFF-211.
/// </summary>
/// <remarks>
/// <para>
/// Gated <c>Permission.SubcontractorManage</c>, company-wide, the same row <c>CreateSubcontractor</c>
/// uses — Finance is refused here too (AC-211-J), because this is the whole profile including the
/// retention rate, not the tax registration number (D-147, its own endpoint below).
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> A firm belongs to no project (rule 11).
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/subcontractors/{subcontractorId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SubcontractorManage)
            .WithName("EditSubcontractor")
            .WithTags("Subcontractors");
    }
}
