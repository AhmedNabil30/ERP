using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Departments.ArchiveDepartment;

/// <summary>
/// <c>POST /api/departments/{departmentId}/archive</c> — take a department out of new assignment
/// without deleting it. KAFF-321, AC-321-D.
/// </summary>
/// <remarks>
/// Same shape as <c>ArchiveBab.Endpoint</c> — a verb on a sub-resource. Gated <c>DepartmentManage</c>,
/// company-wide, no assignment — decisions.md D-162 (Q85). Every other role refused <c>403</c>.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/departments/{departmentId:guid}/archive";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DepartmentManage)
            .WithName("ArchiveDepartment")
            .WithTags("Departments");
    }
}
