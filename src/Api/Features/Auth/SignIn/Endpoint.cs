using Kaff.Api.Common.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Auth.SignIn;

/// <summary>
/// <c>POST /api/auth/sign-in</c> — the staff door. KAFF-101a.
/// </summary>
/// <remarks>
/// <para>
/// <b>Anonymous by construction.</b> There is no identity to check: producing one is the whole job.
/// It is the fourth member of <c>EndpointPermissionCoverageTests</c>'s allow-list, and D-069 makes
/// adding a member the visible act rather than a formality.
/// </para>
/// <para>
/// <b>The route is a sibling of <c>GET /api/auth/me</c></b>, which is fixed by KAFF-105a and already
/// named by the frontend's <c>AuthService</c>. Nothing in the story, the test cases or the UX flows
/// names this one; <c>/api/auth/sign-in</c> keeps the door and the profile call under one prefix.
/// </para>
/// <para>
/// <b>No <c>AddEndpointFilter&lt;ValidationFilter&lt;Request&gt;&gt;()</c>.</b> See <see cref="Request"/>:
/// a shape check in front of this handler answers before the hash runs and re-opens the timing
/// oracle rule 14a closes.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/auth/sign-in";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .AllowAnonymous()
            .WithName("SignIn")
            .WithTags("Auth");
    }
}
