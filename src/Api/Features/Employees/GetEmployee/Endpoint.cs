using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Employees.GetEmployee;

/// <summary>
/// <c>GET /api/employees/{employeeId}</c> — one employee's whole editable file, so the edit screen's
/// full-body <c>PUT</c> round-trips <c>NationalId</c>, <c>JobTitle</c> and <c>HiredOn</c> without loss.
/// </summary>
/// <remarks>
/// <para>
/// <b>Gated <c>EmployeeManage</c>, company-wide</b> — the same row every other employees endpoint uses.
/// Every other role, including an executed <c>Role.Client</c> call, is refused <c>403</c>.
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> An employee belongs to no project, so declaring one would refuse
/// every caller, Owner included.
/// </para>
/// <para>
/// <b>No audit record.</b> It is a read.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/employees/{employeeId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.EmployeeManage)
            .WithName("GetEmployee")
            .WithTags("Employees");
    }
}
