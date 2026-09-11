using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Employees.ListEmployees;

/// <summary>
/// <c>GET /api/employees</c> — a flat list of employees, for <c>S-023</c> (staff) and the day-labour
/// pool alike. KAFF-207.
/// </summary>
/// <remarks>
/// Gated <c>EmployeeManage</c>, company-wide, no assignment — spec.md §2, §10, D-129 §1. Every other
/// role refused <c>403</c>. No <c>ProjectScope</c> — an employee belongs to no project.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/employees";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.EmployeeManage)
            .WithName("ListEmployees")
            .WithTags("Employees");
    }
}
