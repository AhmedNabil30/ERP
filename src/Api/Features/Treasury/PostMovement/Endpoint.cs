using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Treasury.PostMovement;

/// <summary>
/// Two routes, one handler — the first Treasury endpoint. KAFF-301.
/// </summary>
/// <remarks>
/// <para>
/// <b>The permission check is the two <c>RequirePermission</c> lines below and nowhere else.</b>
/// <c>Permission.TreasuryPostCompany</c>'s catalogue row is <c>CompanyWide</c>; <c>Permission.TreasuryPostProject</c>'s
/// is <c>ProjectScoped</c>, both granted to Finance
/// [Verified: 2026-09-14 @ <c>PermissionCatalogue.cs</c> lines 464-465]. spec.md §6.10: a posting names
/// its project or is company-level, never both, never neither — which is exactly the two routes below,
/// not one route with an optional body field.
/// </para>
/// <para>
/// <b>Why two routes and not one with an optional <c>projectId</c> in the body.</b>
/// <c>ProjectScope</c>'s own remarks say the project identifier for a project-scoped permission must
/// come from the route or the query, never the body — authorization has to run before the body is
/// parsed, or an unauthorised request would be read before it is refused. Naming the project in the
/// path is also what makes <c>AC-301-I</c> hold: a Finance user with <c>TreasuryPostProject</c> but no
/// assignment row on <c>{projectId}</c> is refused with a 403 by <c>PermissionAuthorizationHandler</c>
/// before this handler — or the request body — is ever touched, exactly like
/// <c>AssignUserToProject</c>'s identical use of <c>ProjectScope.FromRoute()</c>.
/// </para>
/// <para>
/// <c>projectId</c> is bound with <c>[FromRoute]</c> on the shared handler so that a caller cannot
/// smuggle a project id past the company-wide route's scope through the query string — the company
/// route carries no <c>{projectId}</c> segment, so the parameter is always <c>null</c> there regardless
/// of what the query string says, and the permission evaluated for that route stays
/// <c>TreasuryPostCompany</c> with no project consulted.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string CompanyRoute = "/api/treasury/postings";
    public const string ProjectRoute = "/api/projects/{projectId:guid}/treasury/postings";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(CompanyRoute, (
                Request request,
                KaffDbContext database,
                ICurrentUser currentUser,
                TimeProvider clock,
                CancellationToken cancellationToken)
                => Handler.HandleAsync(null, request, database, currentUser, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostCompany)
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("PostCompanyMovement")
            .WithTags("Treasury");

        app.MapPost(ProjectRoute, (
                [FromRoute] Guid? projectId,
                Request request,
                KaffDbContext database,
                ICurrentUser currentUser,
                TimeProvider clock,
                CancellationToken cancellationToken)
                => Handler.HandleAsync(projectId, request, database, currentUser, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostProject, ProjectScope.FromRoute())
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("PostProjectMovement")
            .WithTags("Treasury");
    }
}
