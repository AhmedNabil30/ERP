using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Treasury.ReversePosting;

/// <summary>
/// Two routes, one handler — the correction primitive for <c>KAFF-301</c>'s posting endpoint. KAFF-303.
/// </summary>
/// <remarks>
/// <para>
/// Same route/permission shape as <c>PostMovement.Endpoint</c>, for the same reason: a reversal is a
/// posting like any other (story rule, "Permissions"), so it is gated by the same catalogue rows —
/// <c>Permission.TreasuryPostCompany</c> (<c>CompanyWide</c>) and <c>Permission.TreasuryPostProject</c>
/// (<c>ProjectScoped</c>) — checked before the body or the target posting is ever touched.
/// </para>
/// <para>
/// <b>Which route to call is decided by the original posting's own tag, not by the caller.</b> A
/// company-level posting is reversed through the company route; a project-tagged one through the
/// project route naming that same project. The handler still checks the loaded posting's
/// <c>ProjectId</c> against the route before it does anything else, so calling the wrong route names
/// the same <c>errors.treasury.project_tag_required</c>/<c>_forbidden</c> refusal
/// <c>Posting.Reverse</c>'s own validation would give — a caller cannot use a company-wide grant to
/// reach a project-tagged posting by routing around <c>ProjectScope</c>.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string CompanyRoute = "/api/treasury/postings/{id:guid}/reverse";
    public const string ProjectRoute = "/api/projects/{projectId:guid}/treasury/postings/{id:guid}/reverse";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(CompanyRoute, (
                [FromRoute] Guid id,
                Request request,
                KaffDbContext database,
                ICurrentUser currentUser,
                TimeProvider clock,
                CancellationToken cancellationToken)
                => Handler.HandleAsync(null, id, request, database, currentUser, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostCompany)
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("ReverseCompanyPosting")
            .WithTags("Treasury");

        app.MapPost(ProjectRoute, (
                [FromRoute] Guid? projectId,
                [FromRoute] Guid id,
                Request request,
                KaffDbContext database,
                ICurrentUser currentUser,
                TimeProvider clock,
                CancellationToken cancellationToken)
                => Handler.HandleAsync(projectId, id, request, database, currentUser, clock, cancellationToken))
            .RequirePermission(Permission.TreasuryPostProject, ProjectScope.FromRoute())
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("ReverseProjectPosting")
            .WithTags("Treasury");
    }
}
