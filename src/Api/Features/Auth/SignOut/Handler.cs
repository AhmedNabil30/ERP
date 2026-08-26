using System.Security.Claims;
using Kaff.Api.Authorization;
using Kaff.Api.Identity;
using Kaff.Api.Options;
using Kaff.Domain.Auditing;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kaff.Api.Features.Auth.SignOut;

/// <summary>
/// Ends this device's session. KAFF-102 rules 1–7.
/// </summary>
/// <remarks>
/// <para>
/// <b>No stamp rotation, ever.</b> Rotating <see cref="User.SecurityStamp"/> is the *global* kill —
/// a password change, a deactivation (rule 6, D-049 ruling 2) — and doing it here would sign the
/// caller out on every other device too, which rule 1 forbids. This handler touches nothing on
/// <see cref="User"/> at all.
/// </para>
/// <para>
/// <b>The caller's own identity is read before it is discarded</b> — the same shape KAFF-101a's
/// <c>SignIn.Handler</c> uses and for the same reason: <c>IAuditContext.AttributeTo</c> is legal only
/// on a request that carries no identity, because an authenticated request naming a different actor
/// is impersonation written into a table nobody can correct afterwards
/// (<c>AuditSaveChangesInterceptor.ResolveActor</c>). No permission gate runs on this
/// <c>AllowAnonymous</c> endpoint to populate <c>VerifiedActor</c>, so this is the one place that
/// reads the actor's row fresh from the table — never the token's claims, decisions.md D-075 — before
/// naming itself the actor.
/// </para>
/// <para>
/// <b>The audit row follows a session the rest of the system would still honour, and nothing less.</b>
/// This handler used to look the caller up by the token's id claim alone and write an
/// <see cref="AuditEventKind.SignedOut"/> row if the row existed — re-checking neither
/// <c>IsActive</c> nor the security stamp. So a captured cookie for an account the global kill of
/// D-053 had already ended was refused <c>403</c> by every other route in the system and accepted
/// here, writing a permanent row into an append-only, trigger-protected table that names that person
/// as having signed out at a time they did not, an unbounded number of times
/// (qa/slice-1/verification-2026-08-26.md, <c>V-26-C</c>). It now asks <c>LiveSession</c> the same
/// question every other exempt route asks (decisions.md D-089); a caller it does not recognise gets
/// the cookie cleared and the same <c>204</c>, which is rule 7 unchanged and discloses nothing.
/// </para>
/// <para>
/// 🟡 <b>An already-unauthenticated caller writes no audit record.</b> Rule 7 says signing out twice
/// is not an error; nothing in the story or CLAUDE.md's "every state change writes an audit record"
/// says whether a call that changes nothing — no cookie existed to clear, no actor to name — is a
/// "state change" that still deserves one. The story's own audit paragraph describes only the
/// signed-in case. Writing an <see cref="AuditEventKind.SignedOut"/> row with a null actor was
/// considered and rejected here: it is legal at the database (D-063 §3), but it would record "somebody
/// signed out" with no way to say who, which nothing asks for and which risks reading as a fabricated
/// fact rather than an honest absence. Flagged for Nabil rather than decided silently.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        HttpContext http,
        KaffDbContext database,
        IAuditContext auditContext,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);

        // The same three checks every RequirePermission route gets, and every other exempt route now
        // gets by construction — active, current stamp, a role that may hold a staff session at all.
        // A null answer here is not a refusal: rule 7 gives this caller the same 204 either way. It is
        // the difference between clearing a cookie and writing a row nobody can ever correct.
        User? user = await LiveSession.ResolveAsync(http, database, cancellationToken);

        if (user is not null)
        {
            // Discard the inbound identity before naming an actor — see the class remarks.
            http.User = new ClaimsPrincipal(new ClaimsIdentity());

            auditContext.AttributeTo(new AuditActor(user.Id, user.FullName, user.Role));
            auditContext.Record<User>(AuditEventKind.SignedOut, user.Id);

            await database.SaveChangesAsync(cancellationToken);
        }

        // Rule 3 / D-050: the same name, same attributes StaffSessionMinter minted it with, expired.
        // A clear with different attributes is not a clear at all for a __Host- prefixed cookie.
        http.Response.Cookies.Delete(jwtOptions.Value.CookieName, StaffSessionMinter.CookieAttributes());

        return Results.NoContent();
    }
}
