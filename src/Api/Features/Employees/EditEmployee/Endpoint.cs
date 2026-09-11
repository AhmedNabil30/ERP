using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Employees.EditEmployee;

/// <summary>
/// <c>PUT /api/employees/{employeeId}</c> — correct an employee's file. KAFF-207, KAFF-208.
/// </summary>
/// <remarks>
/// Gated <c>EmployeeManage</c>, company-wide, no assignment — spec.md §2, §10, D-129 §1. Every other
/// role refused <c>403</c> (<c>AC-207-H</c>, <c>AC-208-F</c>). No <c>ProjectScope</c> — an employee
/// belongs to no project.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/employees/{employeeId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.EmployeeManage)
            .WithName("EditEmployee")
            .WithTags("Employees");
    }
}
