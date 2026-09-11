using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Employees.PhoneCheck;

/// <summary>
/// <c>POST /api/employees/phone-check</c> — whose number is this? decisions.md D-141.
/// </summary>
/// <remarks>
/// Gated <c>EmployeeManage</c>, same as registration and editing — this returns employee names, and a
/// route called "check" is exactly where a permission gets forgotten (same reasoning as
/// <c>Clients.PhoneCheck.Endpoint</c>). POST, not GET — the entire input is a phone number. No
/// <c>ProjectScope</c> — the catalogue row is company-wide.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/employees/phone-check";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.EmployeeManage)
            .WithName("EmployeePhoneCheck")
            .WithTags("Employees");
    }
}
