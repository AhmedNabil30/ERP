using Kaff.Api.Identity;
using Kaff.Api.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Kaff.Api.Common.Middleware;

/// <summary>
/// Moves the session's inactivity window forward on every authenticated request that used the cookie.
/// </summary>
/// <remarks>
/// <para>
/// KAFF-101a rule 5, spec.md §9 amendment (Karim, 2026-08-21, decisions.md D-049 ruling 2):
/// <i>"Sessions auto-expire after 30 minutes of inactivity."</i> <b>Inactivity, not age</b> — an
/// absolute expiry signs a site engineer out in the middle of a daily log, which is the failure
/// <c>AC-101a-J</c>'s second half is written against. The token's own <c>exp</c> is the whole
/// mechanism; this is what keeps moving it.
/// </para>
/// <para>
/// <b>Only for a cookie-borne session.</b> KAFF-101a rule 4 keeps the <c>Authorization</c> header
/// open for service-to-service callers and the integration suite. Those callers hold their token
/// themselves and have nowhere to put a refreshed cookie, so sliding one at them would be a
/// <c>Set-Cookie</c> nobody asked for on a response nobody stores.
/// </para>
/// <para>
/// <b>No threshold.</b> Re-minting only after half the window has elapsed would save one HMAC-SHA256
/// signature per request and add a constant somebody has to justify. The signature is microseconds
/// against a database round trip on the same request.
/// </para>
/// <para>
/// Registered after <c>UseAuthentication</c>, because it needs a principal the framework has already
/// validated — an expired or forged token never reaches here with an authenticated identity, so
/// nothing this writes can extend a session that had already ended.
/// </para>
/// </remarks>
public sealed class SlidingSessionMiddleware
{
    private readonly RequestDelegate _next;

    public SlidingSessionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        StaffSessionMinter minter,
        IOptions<JwtOptions> options,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(minter);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        // An anonymous endpoint is not activity on a session. Health is polled by a monitor that
        // holds nobody's cookie, and sign-in would otherwise renew the session it is about to
        // replace — two Set-Cookie headers for one name on one response, where the winner is
        // whichever the browser happens to process last.
        bool requiresTheSession =
            context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is null;

        if (requiresTheSession
            && context.User.Identity?.IsAuthenticated == true
            && context.Request.Cookies.ContainsKey(options.Value.CookieName)
            && !context.Request.Headers.ContainsKey(HeaderNames.Authorization))
        {
            minter.Renew(context.Response, context.User, clock.GetUtcNow());
        }

        await _next(context);
    }
}
