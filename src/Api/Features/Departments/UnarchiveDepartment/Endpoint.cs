using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Departments.UnarchiveDepartment;

/// <summary>
/// <c>POST /api/departments/{departmentId}/unarchive</c> — bring an archived department back into new
/// assignment. KAFF-321, AC-321-F (revised — Nabil's ruling 2026-09-15, decisions.md).
/// </summary>
/// <remarks>
/// Same shape as <c>UnarchiveCatalogueItem.Endpoint</c> — a verb on a sub-resource, no body, no
/// delete. Gated <c>DepartmentManage</c>, company-wide, same as every other department endpoint.
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/departments/{departmentId:guid}/unarchive";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DepartmentManage)
            .WithName("UnarchiveDepartment")
            .WithTags("Departments");
    }
}
