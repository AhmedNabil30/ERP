using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Employees.ArchiveEmployee;

/// <summary>
/// <c>POST /api/employees/{employeeId}/archive</c> — take an employee off the active register without
/// deleting them. KAFF-207.
/// </summary>
/// <remarks>
/// Same shape as <c>ArchiveBab.Endpoint</c> — a verb on a sub-resource, no
/// <c>DELETE /api/employees/{id}</c> anywhere. Gated <c>EmployeeManage</c>, company-wide, no
/// assignment — spec.md §2, §10, D-129 §1. Every other role refused <c>403</c> (<c>AC-207-H</c>).
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/employees/{employeeId:guid}/archive";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.EmployeeManage)
            .WithName("ArchiveEmployee")
            .WithTags("Employees");
    }
}
