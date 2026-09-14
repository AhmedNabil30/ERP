using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Departments.EditDepartment;

/// <summary>
/// <c>PUT /api/departments/{departmentId}</c> — correct a department's names. KAFF-321, AC-321-C.
/// </summary>
/// <remarks>
/// Gated <c>DepartmentManage</c>, company-wide, no assignment — decisions.md D-162 (Q85). Every other
/// role refused <c>403</c>. No <c>ProjectScope</c> — departments belong to no project.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/departments/{departmentId:guid}";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DepartmentManage)
            .WithName("EditDepartment")
            .WithTags("Departments");
    }
}
