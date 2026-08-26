using Kaff.Api.Common.Endpoints;
using Kaff.Api.Common.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Auth.ChangePassword;

/// <summary>
/// <c>POST /api/auth/change-password</c> — the caller replaces their own password. KAFF-103.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>RequirePermission</c>, and that is a decision, not an omission.</b> The story's own line:
/// "authenticated as the user themselves. Not <c>UserManage</c> — only the person changes it." There is
/// no catalogue permission for "act on your own row alone" and adding one would misstate the rule: this
/// is not a grant any role holds over anyone, it is what every signed-in caller may do to themselves.
/// The fallback policy (<c>RequireAuthenticatedUser</c>) is the whole of the gate, and it is recorded
/// narrowly in <c>EndpointPermissionCoverageTests.SelfOnlyEndpoints</c> so a future endpoint that
/// forgets a real permission still fails the build (D-067, D-069) — this is the one exemption, named
/// and reasoned, not a hole the rule quietly grew.
/// </para>
/// <para>
/// <b>Also exempt from the <c>MustChangePassword</c> gate</b>
/// (<c>PermissionDecision.PasswordChangeRequired</c>, <c>PermissionAuthorizationHandler</c>) — but not
/// because anything here says so. That gate lives inside the same <c>RequirePermission</c> pipeline
/// this endpoint deliberately does not use, so it never runs here at all. <c>AC-103-B</c>'s "every one
/// except the change-password endpoint" holds by construction, not by a second rule layered on top of
/// the first.
/// </para>
/// <para>
/// <b>The route is a sibling of <c>/api/auth/sign-in</c> and <c>/api/auth/sign-out</c></b>, all three
/// under the one prefix decisions.md D-063 §1 names as every present and future staff door.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/auth/change-password";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .AddEndpointFilter<ValidationFilter<Request>>()
            .WithName("ChangePassword")
            .WithTags("Auth");
    }
}
