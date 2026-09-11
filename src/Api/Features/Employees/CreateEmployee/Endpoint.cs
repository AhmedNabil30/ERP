using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Employees.CreateEmployee;

/// <summary>
/// <c>POST /api/employees</c> — HR (or the Owner, D-129 §1) registers one costed person. KAFF-207.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the <c>RequirePermission</c> line below and nowhere else.</b>
/// <c>Permission.EmployeeManage</c> — company-wide, granted to <c>Role.Owner</c> and <c>Role.Hr</c>
/// [Verified: 2026-09-09 @ <c>PermissionCatalogue.cs</c> -&gt; the <c>Permission.EmployeeManage</c>
/// row] — spec.md §2, §10, D-129 §1. Every other role, including the Technical Office and the Site
/// Engineer, is refused <c>403</c> (<c>AC-207-H</c>).
/// </para>
/// <para>
/// <b>No <c>ProjectScope</c>.</b> An employee belongs to no project (KAFF-207 rule 9), so declaring
/// one would make the evaluator look for an assignment that does not exist and refuse every caller,
/// Owner included.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/employees";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .RequirePermission(Permission.EmployeeManage)
            .WithName("CreateEmployee")
            .WithTags("Employees");
    }
}
