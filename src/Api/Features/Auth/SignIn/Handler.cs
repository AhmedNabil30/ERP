using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Api.Identity;
using Kaff.Api.Options;
using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kaff.Api.Features.Auth.SignIn;

/// <summary>
/// Decides whether a credential opens a staff session, and what the caller is told when it does not.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>THE ORDER OF THE CHECKS BELOW IS THE FEATURE. Read this before moving one.</b>
/// </para>
/// <para>
/// <b>The password is verified before anything else decides the response</b> — before the lockout,
/// before the role, before the active flag. KAFF-101a rules 14a and 16a, decisions.md D-072 §1 and
/// D-063 §1. Two reasons, and the second is the one that gets forgotten:
/// </para>
/// <para>
/// ① It is the only ordering that can tell <i>"correct password, locked"</i> from <i>"wrong
/// password, locked"</i>, which is the whole of what D-072 §1 turns on.
/// </para>
/// <para>
/// ② <b>It keeps the timing envelope even.</b> The obvious implementation — refuse the unknown
/// username, the subcontractor, the client or the locked account before hashing — returns in
/// microseconds while every other path pays for 600,000 PBKDF2 iterations, so the door stops leaking
/// which usernames exist through its status code and starts leaking it through a clock. <b>"Check
/// that first" is not an optimisation here, it is the defect</b>, and the tidied version passes every
/// test that asserts a status code. <c>PasswordHasher.Verify</c> takes a nullable stored hash for
/// exactly this reason, so there is no branch here to tidy.
/// </para>
/// <para>
/// <b>One refusal, five cases.</b> A wrong password, a username that does not exist, a
/// <see cref="Role.Client"/> at the staff origin, a <see cref="Role.Subcontractor"/> and a locked
/// account given the wrong password all produce <c>401</c> /
/// <c>errors.auth.invalid_credentials</c>, identical in status, body and <c>messageKey</c> — D-065
/// cases 1, 2, 4 and 5, and D-072 §1 for the fifth. <b>The single exception is a locked account given
/// the correct password</b>: <c>423</c> / <c>errors.auth.account_locked</c>, which leaks nothing
/// because only somebody who already holds the credential can see it.
/// </para>
/// <para>
/// <b>Nothing the caller typed is written anywhere.</b> D-062 §3, Nabil: <i>"Log the attempt as a
/// security event, but strictly FORBID storing the typed input. Users frequently type their password
/// into the username/email field by mistake."</i> <c>audit_records</c> is append-only by trigger, so
/// a plaintext password written into it could never be removed — not by an admin, not by a migration.
/// The failure records here carry an <see cref="AuditEventKind"/>, the connection address and the
/// timestamp, and no string from the request.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Request request,
        HttpContext http,
        KaffDbContext database,
        IAuditContext auditContext,
        StaffSessionMinter minter,
        IOptions<LockoutOptions> lockout,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(http);

        // This door acts for nobody. A caller who already holds a cookie is authenticated by the
        // time the request reaches here — the endpoint is anonymous, not unauthenticated — and the
        // audit interceptor would then attribute the row it is about to write to whoever that token
        // names, with no role beside it, which the database refuses outright
        // (ck_audit_records_actor_is_named_completely). Discarding the identity is also what the act
        // means: signing in replaces whatever session was there, it does not extend it.
        http.User = new ClaimsPrincipal(new ClaimsIdentity());

        string userName = (request.UserName ?? string.Empty).Trim().ToLowerInvariant();

        User? user = await database.Users
            .FirstOrDefaultAsync(candidate => candidate.UserName == userName, cancellationToken);

        // ⚠️ FIRST, ALWAYS, FOR EVERY CALLER — see the class remarks. A null stored hash is not a
        // fast path: PasswordHasher.Verify falls back to a dummy at the shipped iteration count, so
        // an account that does not exist costs what one that does costs.
        bool passwordIsCorrect = PasswordHasher.Verify(request.Password ?? string.Empty, user?.PasswordHash);

        DateTimeOffset now = clock.GetUtcNow();

        if (user is null)
        {
            // AC-101a-O. No subject, because a sign-in was attempted against a User that does not
            // exist — D-063 §3 built the nullable subject for this row. The address arrives from
            // AuditCorrelationMiddleware, which reads the connection and no header (D-063 §2, D-079).
            auditContext.Record<User>(AuditEventKind.SignInFailedUnknownUser, subjectId: null);
            await database.SaveChangesAsync(cancellationToken);

            return ResultExtensions.Problem(AuthorizationErrors.InvalidCredentials);
        }

        if (!passwordIsCorrect)
        {
            // Rule 7 / D-049 ruling 3. The two numbers are Karim's and are passed in from
            // LockoutOptions rather than written in the entity or here.
            user.RecordFailedSignIn(now, lockout.Value.MaxFailedAttempts, lockout.Value.LockoutDuration);

            auditContext.Record<User>(AuditEventKind.SignInFailed, user.Id);

            if (user.IsLockedOut(now))
            {
                auditContext.Record<User>(AuditEventKind.AccountLockedOut, user.Id);
            }

            await database.SaveChangesAsync(cancellationToken);

            return ResultExtensions.Problem(AuthorizationErrors.InvalidCredentials);
        }

        // ---- The password is correct from here down, and only now does anything else decide. ----

        // Rule 16 (D-062 §2, D-063 §1, D-065 case 4), rule 9 (D-065 case 5) and rule 10 (§9, D-048).
        // All three answer with the generic 401 and nothing distinguishes them from a wrong password:
        // a distinct answer here fires only on a real credential, which is the most informative thing
        // an anonymous door can say. Role.Subcontractor cannot reach this line — the entity refuses
        // it a credential — and is named anyway, because the rule is about the door rather than about
        // what the users table happens to hold.
        //
        // 🟡 An inactive account is folded into this set rather than given
        // errors.auth.account_inactive, which the story's i18n bullet names but no criterion reaches.
        // See decisions.md D-084 and the question raised there: a distinct answer is the same oracle
        // D-065 closed for the subcontractor, and inventing one is not this handler's to do.
        if (user.Role is Role.Client or Role.Subcontractor || !user.IsActive)
        {
            auditContext.Record<User>(AuditEventKind.SignInFailed, user.Id);
            await database.SaveChangesAsync(cancellationToken);

            return ResultExtensions.Problem(AuthorizationErrors.InvalidCredentials);
        }

        // Rule 14 / D-072 §1. The one answer that is not the generic 401, and it is reachable only
        // by somebody who has already proved they hold the password — which is exactly the
        // legitimate locked-out user the ruling's UX argument is about.
        if (user.IsLockedOut(now))
        {
            auditContext.Record<User>(AuditEventKind.SignInFailed, user.Id);
            await database.SaveChangesAsync(cancellationToken);

            return ResultExtensions.Problem(AuthorizationErrors.AccountLocked);
        }

        user.RecordSuccessfulSignIn();

        // The actor is the person who just signed in. Legal only because the identity was discarded
        // above and this request now carries none — the same guard KAFF-100's bootstrap relies on
        // (decisions.md D-061). On an account that was already clean nothing is modified, so the
        // event below is the only record the save produces, which is the case D-061 built ForEvent
        // for.
        auditContext.AttributeTo(new AuditActor(user.Id, user.FullName, user.Role));
        auditContext.Record<User>(AuditEventKind.SignedIn, user.Id);

        await database.SaveChangesAsync(cancellationToken);

        // Rules 1 and 2 (D-050): the session leaves as a Set-Cookie header and the body carries no
        // token in any field under any name. 204 rather than 200 with an empty object, so there is
        // no body for one to be added to later.
        //
        // 🟡 MustChangePassword is deliberately not consulted. D-072 §2 rules that sign-in succeeds
        // and issues a FULL token whose flag travels in GET /api/auth/me's payload. What any other
        // endpoint does with that token is a rule nobody has stated — see AC-101a-F and D-084.
        minter.Issue(http.Response, user, now);

        return Results.NoContent();
    }
}
