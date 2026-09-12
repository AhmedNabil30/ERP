using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Subcontractors.PhoneCheck;

/// <summary><c>POST /api/subcontractors/phone-check</c> — whose number is this? decisions.md D-141.</summary>
/// <remarks>
/// Gated <c>SubcontractorManage</c>, same as registration and editing — same reasoning as
/// <c>Clients.PhoneCheck.Endpoint</c> and <c>Employees.PhoneCheck.Endpoint</c>. POST, not GET. No
/// <c>ProjectScope</c> — the catalogue row is company-wide.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/subcontractors/phone-check";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.SubcontractorManage)
            .WithName("SubcontractorPhoneCheck")
            .WithTags("Subcontractors");
    }
}
