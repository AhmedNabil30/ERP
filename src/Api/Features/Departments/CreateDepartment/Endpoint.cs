using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Departments.CreateDepartment;

/// <summary>
/// <c>POST /api/departments</c> — an admin adds one department. KAFF-321, AC-321-B.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b>
/// <c>Permission.DepartmentManage</c> — company-wide, granted to <c>Role.Owner</c> alone
/// [Verified @ <c>PermissionCatalogue.cs</c> -&gt; the <c>Permission.DepartmentManage</c> row] —
/// decisions.md D-162 (<c>Q85</c>). Every other role is refused <c>403</c>.
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> Departments belong to no project, so declaring one would make the
/// evaluator look for an assignment that does not exist and refuse every caller including the Owner.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/departments";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.DepartmentManage)
            .WithName("CreateDepartment")
            .WithTags("Departments");
    }
}
