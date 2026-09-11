using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Employees.ListBabOptions;

/// <summary>
/// <c>GET /api/employees/babs</c> — a markup-free باب picker for the employee form. decisions.md D-137.
/// </summary>
/// <remarks>
/// <para>
/// <b>Gated <c>EmployeeManage</c>, company-wide — the same row every other employees endpoint uses.</b>
/// No new catalogue row is added. HR's permission set does not widen: picking a trade is part of
/// managing an employee (spec.md §9, §10), so it uses the permission for managing employees.
/// <c>GET /api/babs</c> stays gated <c>BabManage</c> and keeps its markup — this route exists because
/// HR is refused there, not to replace it.
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> أبواب belong to no project, so declaring one would refuse every
/// caller, Owner included.
/// </para>
/// <para>
/// <b>The literal <c>babs</c> segment does not collide with <c>GET /api/employees/{employeeId}</c></b>
/// — that route's id segment is guid-constrained.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/employees/babs";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.EmployeeManage)
            .WithName("ListBabOptions")
            .WithTags("Employees");
    }
}
