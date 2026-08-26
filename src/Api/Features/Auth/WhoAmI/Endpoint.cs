using Kaff.Api.Authorization;
using Kaff.Api.Common.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Auth.WhoAmI;

/// <summary>
/// <c>GET /api/auth/me</c> — the caller learns who they are and what they may do. KAFF-105a.
/// </summary>
/// <remarks>
/// <para>
/// <b>Self-only, the same shape as KAFF-103's <c>change-password</c>, and for the same reason.</b>
/// There is no catalogue <c>Permission</c> for "read your own profile" — it is not a grant any role
/// holds over anyone — so this carries no <c>RequirePermission</c>. It is the second member of
/// <c>EndpointPermissionCoverageTests.SelfOnlyEndpoints</c>, beside KAFF-103's: the fallback policy alone
/// still refuses an unauthenticated caller <c>401</c> (<c>AC-105a-D</c>), and the absence of
/// <c>RequirePermission</c> is exactly what keeps this route out from under
/// <c>PermissionAuthorizationHandler</c>'s <c>PasswordChangeRequired</c> gate.
/// </para>
/// <para>
/// <b>That absence is load-bearing, not incidental — <c>AC-105a-C</c>.</b> Decisions.md D-072 §2: a
/// caller who has not yet replaced a temporary password still gets a <c>200</c> and a full profile,
/// with <c>mustChangePassword: true</c> as a field for the SPA to route on. If this endpoint carried a
/// <c>RequirePermission</c>, <c>PermissionEvaluator.Evaluate</c>'s <c>MustChangePassword</c> check
/// (D-086) would refuse it before the catalogue is even consulted — the dead-end loop D-072 §2 exists
/// to close.
/// </para>
/// <para>
/// <b>The route is fixed by decisions.md D-084</b> as the sibling of <c>POST /api/auth/sign-in</c> —
/// <c>AuthService</c> already calls it by this exact path.
/// </para>
/// <para>
/// <b><c>RequireLiveSession()</c> is what the exemption costs.</b> No permission gate runs here, so the
/// three checks one would have applied — active, current stamp, and a role that may hold a staff
/// session at all — are applied by <see cref="LiveSession"/> instead. They were hand-copied here once
/// and came out one short: this endpoint answered a <c>Role.Subcontractor</c> with a <c>200</c> and
/// their name (qa/slice-1/verification-2026-08-26.md, <c>V-26-B</c>). Declaring the exemption and
/// paying for it are now the same act.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/auth/me";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(Route, Handler.HandleAsync)
            .RequireLiveSession()
            .WithName("Me")
            .WithTags("Auth");
    }
}
