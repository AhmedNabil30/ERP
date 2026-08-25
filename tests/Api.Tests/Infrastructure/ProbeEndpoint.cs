using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Domain.Authorization;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Infrastructure;

/// <summary>
/// Endpoints that exist only for the test host, declaring permissions the way a real slice will.
/// </summary>
/// <remarks>
/// The permission mechanism has to be tested through a route, because half of it — resolving the
/// project from the URL, the policy provider, the fallback policy — only exists in the pipeline.
/// Registering probes through the same <see cref="IEndpoint"/> convention keeps them out of the
/// shipped application while exercising the real gate.
/// </remarks>
public sealed class ProbeEndpoint : IEndpoint
{
    public const string CompanyRoute = "/probe/company";
    public const string ProjectRoute = "/probe/projects/{projectId:guid}";
    public const string ApproveRoute = "/probe/projects/{projectId:guid}/approve";
    public const string SubmitRoute = "/probe/projects/{projectId:guid}/submit";
    public const string AnonymousRoute = "/probe/anonymous";

    /// <summary>
    /// Project-scoped, but reachable by HR without an assignment. The one route where global reach
    /// and a project scope meet, which is where Karim's 2026-08-20 HR ruling actually lands.
    /// </summary>
    public const string AssignRoute = "/probe/projects/{projectId:guid}/assign";

    /// <summary>Company-wide, Owner only. Karim, 2026-08-20. See decisions.md D-044.</summary>
    public const string UserAdminRoute = "/probe/users";

    /// <summary>
    /// The one permission a department move can reach — and only for a caller who already holds the
    /// role beside it.
    /// </summary>
    /// <remarks>
    /// KAFF-108 <c>AC-108-A</c>, <c>AC-108-B</c> and <c>AC-108-G</c> are each "the same token, a
    /// different answer, after a move", and none of them is observable without a route behind
    /// <c>Permission.SiteExpenseConfirm</c>. The real one is slice 6; this declares the permission
    /// exactly as that slice will — project-scoped, so the assignment rule still applies. See
    /// decisions.md D-052 §1 and D-053 §2 for why the grant names a role as well as the
    /// sub-department (finding F-04).
    /// </remarks>
    public const string SiteExpenseConfirmRoute = "/probe/projects/{projectId:guid}/site-expense-confirm";

    /// <summary>
    /// Finance, project-scoped. KAFF-109 <c>AC-109-F</c> needs a route behind a permission Finance
    /// actually holds, so a role change taking effect mid-session is observable on more than
    /// <c>UserManage</c> — the same permission every other probe route in this file already exists to
    /// avoid over-using.
    /// </summary>
    public const string TreasuryPostRoute = "/probe/projects/{projectId:guid}/treasury-post";

    /// <summary>
    /// The portal's own surface. A client reaches a project through this and nothing else —
    /// see decisions.md D-035.
    /// </summary>
    public const string PortalRoute = "/probe/portal/projects/{projectId:guid}";

    /// <summary>
    /// One write route per grant path, each behind the permission the role that uses that path
    /// actually holds.
    /// </summary>
    /// <remarks>
    /// KAFF-116 is only observable on a record the gate admitted, and the four routes above write
    /// nothing. Each of these performs the same real, audited write, so the record the request leaves
    /// behind can be read back and its <c>GrantPath</c> asserted.
    /// </remarks>
    public const string WriteAssignedRoute = "/probe/projects/{projectId:guid}/write";

    /// <summary>Reachable by the Owner without an assignment — the path with no row to point at.</summary>
    public const string WriteOwnerRoute = "/probe/projects/{projectId:guid}/write-owner";

    /// <summary>Reachable by HR without an assignment, which is how HR staffs a new project.</summary>
    public const string WriteHrRoute = "/probe/projects/{projectId:guid}/write-hr";

    /// <summary>The portal client's own project, matched on <c>ClientId</c> rather than an assignment.</summary>
    public const string WritePortalRoute = "/probe/portal/projects/{projectId:guid}/write";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(AnonymousRoute, () => Results.Ok("open"))
            .AllowAnonymous();

        app.MapGet(CompanyRoute, () => Results.Ok("company"))
            .RequirePermission(Permission.ClientManage);

        app.MapGet(ProjectRoute, (Guid projectId) => Results.Ok(projectId))
            .RequirePermission(Permission.ProjectRead, ProjectScope.FromRoute());

        app.MapGet(ApproveRoute, (Guid projectId) => Results.Ok(projectId))
            .RequirePermission(Permission.FinancialMovementApprove, ProjectScope.FromRoute());

        app.MapGet(SubmitRoute, (Guid projectId) => Results.Ok(projectId))
            .RequirePermission(Permission.DraftSubmit, ProjectScope.FromRoute());

        app.MapGet(PortalRoute, (Guid projectId) => Results.Ok(projectId))
            .RequirePermission(Permission.PortalRead, ProjectScope.FromRoute());

        app.MapGet(AssignRoute, (Guid projectId) => Results.Ok(projectId))
            .RequirePermission(Permission.ProjectAssignmentManage, ProjectScope.FromRoute());

        app.MapGet(UserAdminRoute, () => Results.Ok("users"))
            .RequirePermission(Permission.UserManage);

        app.MapGet(SiteExpenseConfirmRoute, (Guid projectId) => Results.Ok(projectId))
            .RequirePermission(Permission.SiteExpenseConfirm, ProjectScope.FromRoute());

        app.MapGet(TreasuryPostRoute, (Guid projectId) => Results.Ok(projectId))
            .RequirePermission(Permission.TreasuryPostProject, ProjectScope.FromRoute());

        app.MapGet(WriteAssignedRoute, WriteAsync)
            .RequirePermission(Permission.ProjectRead, ProjectScope.FromRoute());

        app.MapGet(WriteOwnerRoute, WriteAsync)
            .RequirePermission(Permission.FinancialMovementApprove, ProjectScope.FromRoute());

        app.MapGet(WriteHrRoute, WriteAsync)
            .RequirePermission(Permission.ProjectAssignmentManage, ProjectScope.FromRoute());

        app.MapGet(WritePortalRoute, WriteAsync)
            .RequirePermission(Permission.PortalRead, ProjectScope.FromRoute());
    }

    /// <summary>
    /// Changes one thing on the project, and creates one company-level record in the same save.
    /// </summary>
    /// <remarks>
    /// The pair is the point. One request, one transaction, two audit records: the project change was
    /// reached through a grant path and must name it; the client belongs to no project and must name
    /// none — not the caller's path by default. Nothing here is a real endpoint; the shipped slices
    /// write through their own handlers.
    /// </remarks>
    private static async Task<IResult> WriteAsync(Guid projectId, KaffDbContext context, CancellationToken cancellationToken)
    {
        Project project = await context.Projects.SingleAsync(candidate => candidate.Id == projectId, cancellationToken);

        // Always a change: every seeded project starts at WithholdingCategory.None, and a modification
        // that changes nothing deliberately leaves no record.
        _ = project.SetWithholding(WithholdingCategory.Services, ClientKind.Corporate);

        context.Clients.Add(Client.Create(
            UniqueNames.Code("WRITE"),
            "عميل الكتابة",
            UniqueNames.Phone(),
            ClientKind.Corporate,
            DateTimeOffset.UtcNow).Value);

        await context.SaveChangesAsync(cancellationToken);

        return Results.Ok(projectId);
    }
}
