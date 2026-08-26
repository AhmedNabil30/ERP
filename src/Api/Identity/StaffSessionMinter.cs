using System.Security.Claims;
using System.Text;
using Kaff.Api.Options;
using Kaff.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kaff.Api.Identity;

/// <summary>
/// The one place a staff session comes into existence.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every present and future staff door goes through here</b> — this sign-in endpoint, KAFF-103's
/// forced password change, KAFF-104's reset link, anything slice 8 adds. A staff session is one
/// thing: a token for <see cref="JwtOptions.Audience"/> carried in <see cref="JwtOptions.CookieName"/>.
/// decisions.md D-063 §1 put the guarantee here rather than in a handler precisely so that a future
/// endpoint which forgets the rule cannot mint a session anyway.
/// </para>
/// <para>
/// <b>The token is never handed to JavaScript.</b> decisions.md D-050: it travels in an
/// <c>HttpOnly; Secure; SameSite=Strict</c> cookie, path <c>/</c>, no <c>Domain</c>, and
/// <c>localStorage</c> and <c>sessionStorage</c> are prohibited for it. The response body carries no
/// token in any field under any name — which is why this method writes a header and returns nothing.
/// </para>
/// <para>
/// <b>The <c>__Host-</c> prefix is a constraint, not a name.</b> A browser accepts a cookie carrying
/// it only when it is <c>Secure</c>, path <c>/</c> and has no <c>Domain</c>, so a neighbouring
/// subdomain cannot set it. Getting any of the three wrong does not degrade the session — the
/// browser rejects the cookie outright and sign-in silently stops working.
/// </para>
/// </remarks>
public sealed class StaffSessionMinter
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;

    public StaffSessionMinter(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    /// <summary>
    /// Mints a staff session for <paramref name="user"/> and sets it on <paramref name="response"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="user"/> holds a role that may never hold a staff session. <b>This is a
    /// programmer-error guard and it throws</b> — it is not the user-facing refusal path.
    /// decisions.md D-063 §1: two places, two jobs. The minter guarantees no such session can exist;
    /// the handler decides what the caller is told, and what the caller is told is the same generic
    /// 401 every other refusal gets (KAFF-101a rule 16). Deliberately not the same rule twice.
    /// </exception>
    public void Issue(HttpResponse response, User user, DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(user);

        // Role.Client: D-062 §2, Nabil — "strictly forbidden from a security standpoint for any user
        // holding the Role.Client to sign in or authenticate through the staff portal". The old
        // reading of KAFF-101a rule 16 rested on what PermissionCatalogue happens to contain today;
        // one company-wide row a client happens to hold would have re-opened it. This is a property
        // of the door and survives any catalogue change.
        //
        // Role.Subcontractor: spec.md §9, "record only, no login". The entity already refuses the
        // credential in the private StorePasswordHash and the database refuses it in
        // ck_users_subcontractor_cannot_log_in, so this is the third lock on a door with no key —
        // and it is here because "no staff session exists for this role" is exactly the guarantee
        // this method is the single point of.
        if (user.Role is Role.Client or Role.Subcontractor)
        {
            throw new InvalidOperationException(
                $"No staff session may be minted for {user.Role}. The caller must refuse the request "
                + "before reaching here — see KAFF-101a rule 16 and decisions.md D-063 §1.");
        }

        Mint(response, ClaimsFor(user), issuedAt);
    }

    /// <summary>
    /// Re-mints an existing session's token with a fresh inactivity window, keeping its claims.
    /// </summary>
    /// <remarks>
    /// The sliding half of rule 5. It carries the validated principal's own claims forward rather
    /// than re-reading the user, deliberately: the claims this token carries are identity, not
    /// authority — <c>PermissionAuthorizationHandler</c> re-reads role, department and the active
    /// flag from the users table on every authorized request (D-048), and
    /// <c>PermissionSubjectReader</c> compares the security stamp there (D-053). Re-reading here
    /// would be a second source of truth for facts the gate has already established.
    /// </remarks>
    public void Renew(HttpResponse response, ClaimsPrincipal principal, DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(principal);

        Mint(response, principal.Claims.Where(claim => IssuedClaimTypes.Contains(claim.Type)), issuedAt);
    }

    /// <summary>Builds the token, signs it, and writes the cookie. The one path both callers take.</summary>
    /// <remarks>
    /// KAFF-101a rule 5 / D-049 ruling 2: thirty minutes of inactivity, sliding on activity. The
    /// number is <see cref="JwtOptions.InactivityMinutes"/> and is never a literal in a handler.
    /// </remarks>
    private void Mint(HttpResponse response, IEnumerable<Claim> claims, DateTimeOffset issuedAt)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = _signingCredentials,
            IssuedAt = issuedAt.UtcDateTime,

            // exp and nothing else. No `nbf`: a token is valid from the moment it is minted, so a
            // not-before would only ever be a second clock to disagree with the first — and it does
            // disagree, because the framework validates lifetimes against its own clock rather than
            // against this TimeProvider. Measured: with `nbf` set, a session renewed by a host whose
            // clock is ahead is refused as not-yet-valid on the very next request.
            Expires = issuedAt.UtcDateTime.AddMinutes(_options.InactivityMinutes),
            Subject = new ClaimsIdentity(claims),
        };

        response.Cookies.Append(
            _options.CookieName,
            new JsonWebTokenHandler().CreateToken(descriptor),
            CookieAttributes());
    }

    /// <summary>
    /// What the token says the caller is. <b>Identity only.</b>
    /// </summary>
    /// <remarks>
    /// KAFF-101a rule 12: the user id, the display name and the role, and <b>no permission list and
    /// no assignment list</b> — those are re-evaluated server-side per request against
    /// <c>PermissionCatalogue</c> and <c>ProjectAssignment</c>. The security stamp is the fourth and
    /// is the revocation hook (rule 11a, D-051 N5): <c>PermissionSubjectReader</c> compares it to the
    /// stored one in the <c>WHERE</c> clause of every authorized request and refuses on mismatch —
    /// and on absence, so a token issued without it authenticates nothing.
    /// <para>
    /// Department and sub-department are deliberately absent: the gate re-reads both from the users
    /// table (D-048) and no decision anywhere consults the claimed value, so putting them here would
    /// create a copy that can only go stale.
    /// </para>
    /// </remarks>
    private static Claim[] ClaimsFor(User user) =>
    [
        new(KaffClaimTypes.UserId, user.Id.ToString()),
        new(KaffClaimTypes.DisplayName, user.FullName),
        new(KaffClaimTypes.Role, user.Role.ToString()),
        new(KaffClaimTypes.SecurityStamp, user.SecurityStamp),
    ];

    /// <summary>
    /// What <see cref="Renew"/> carries forward — the same four <see cref="ClaimsFor"/> issues,
    /// named once so a claim added to one and forgotten in the other cannot happen.
    /// </summary>
    private static readonly string[] IssuedClaimTypes =
    [
        KaffClaimTypes.UserId,
        KaffClaimTypes.DisplayName,
        KaffClaimTypes.Role,
        KaffClaimTypes.SecurityStamp,
    ];

    /// <summary>
    /// The five attributes <c>AC-101a-A</c> and <c>TC-1-220</c> assert, in one place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No <c>Expires</c> and no <c>Max-Age</c>: the token's own <c>exp</c> is the lifetime, and a
    /// cookie carrying a second one would be a second clock to disagree with it. <c>Domain</c> is
    /// left unset rather than set to anything — the <c>__Host-</c> prefix forbids it.
    /// </para>
    /// <para>
    /// <b>Internal, not private — KAFF-102's sign-out reuses this exact set</b> to build the
    /// <c>CookieOptions</c> it hands <c>Response.Cookies.Delete</c>. Rule 3 / D-050: "a cookie cleared
    /// with different attributes is not cleared at all." A second literal in the sign-out handler
    /// could drift from this one the day either changes; one shared source cannot.
    /// </para>
    /// </remarks>
    internal static CookieOptions CookieAttributes() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
    };
}
