using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Departments.ListDepartments;

/// <summary>
/// <c>GET /api/departments</c> — the flat department list, for the settings screen and for any user
/// form's department picker. KAFF-321.
/// </summary>
/// <remarks>
/// Gated <c>DepartmentManage</c>, company-wide, no assignment — decisions.md D-162 (Q85). Every other
/// role is refused <c>403</c>. No <c>ProjectScope</c> — departments belong to no project.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/departments";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DepartmentManage)
            .WithName("ListDepartments")
            .WithTags("Departments");
    }
}
