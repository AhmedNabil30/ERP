using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.DayLabour.PhoneCheck;

/// <summary>
/// <c>POST /api/projects/{projectId:guid}/day-labour/phone-check</c> — whose number is this, from a
/// Site Engineer's point of view. decisions.md D-140 point 6.
/// </summary>
/// <remarks>
/// Gated <c>DayLabourSiteManage</c> + <c>ProjectScope.FromRoute()</c>, the same as every route in this
/// slice. POST, not GET — the entire input is a phone number, same reasoning as
/// <c>Employees.PhoneCheck.Endpoint</c>.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/projects/{projectId:guid}/day-labour/phone-check";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DayLabourSiteManage, ProjectScope.FromRoute())
            .WithName("DayLabourPhoneCheck")
            .WithTags("DayLabour");
    }
}
