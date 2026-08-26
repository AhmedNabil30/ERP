using Kaff.Api.Common.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaff.Api.Features.Auth.SignOut;

/// <summary>
/// <c>POST /api/auth/sign-out</c> — ends this device's session. KAFF-102.
/// </summary>
/// <remarks>
/// <para>
/// <b>Anonymous by construction, and that is rule 7, not an oversight.</b> "Signing out when already
/// signed out is not an error worth a refusal — the outcome the caller asked for already holds."
/// Behind the shipped fallback policy an unauthenticated caller never reaches a handler at all — it
/// is refused <c>401</c> / <c>errors.auth.not_authenticated</c> before this method runs (D-071). Only
/// <c>AllowAnonymous</c> lets a caller who already holds no session get the <c>204</c> the rule asks
/// for instead. It is the fifth member of <c>EndpointPermissionCoverageTests</c>'s allow-list (D-069).
/// </para>
/// <para>
/// <b>Clearing the cookie is the whole mechanism (rule 2, decisions.md D-051 N5).</b> There is no
/// session table and nothing here revokes a token — a caller who kept the old cookie value can still
/// use it until it expires, which <c>AC-102-B</c> asserts on purpose rather than hiding.
/// </para>
/// <para>
/// <b>The route is a sibling of <c>POST /api/auth/sign-in</c></b>, which decisions.md D-084 recorded
/// as a wire contract KAFF-102 must match.
/// </para>
/// </remarks>
public sealed class Endpoint : IEndpoint
{
    public const string Route = "/api/auth/sign-out";

    public void Map(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(Route, Handler.HandleAsync)
            .AllowAnonymous()
            .WithName("SignOut")
            .WithTags("Auth");
    }
}
