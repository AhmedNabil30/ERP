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

namespace Kaff.Api.Features.Portal.SignIn;

/// <summary>
/// Decides whether a credential opens a portal session. The mirror of <c>Auth.SignIn.Handler</c>, and it keeps that handler's
/// order of checks for the same reasons (KAFF-101a rule 14a, D-072 section 1): the password is verified first, for every
/// caller, so a username that does not exist, a staff account, a deactivated client and a wrong password all cost the same
/// and answer the same 401 / errors.auth.invalid_credentials. Nothing the caller typed is written anywhere (D-062 section 3).
/// </summary>
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

        // This door acts for nobody; see the staff handler for why the inbound identity is discarded.
        http.User = new ClaimsPrincipal(new ClaimsIdentity());

        string userName = (request.UserName ?? string.Empty).Trim().ToLowerInvariant();

        User? user = await database.Users
            .FirstOrDefaultAsync(candidate => candidate.UserName == userName, cancellationToken);

        bool passwordIsCorrect = PasswordHasher.Verify(request.Password ?? string.Empty, user?.PasswordHash);

        DateTimeOffset now = clock.GetUtcNow();

        if (user is null)
        {
            auditContext.Record<User>(AuditEventKind.SignInFailedUnknownUser, subjectId: null);
            await database.SaveChangesAsync(cancellationToken);

            return ResultExtensions.Problem(AuthorizationErrors.InvalidCredentials);
        }

        if (!passwordIsCorrect)
        {
            // R5-A AC-A-4 (proposal): the staff lockout numbers apply to a client too.
            user.RecordFailedSignIn(now, lockout.Value.MaxFailedAttempts, lockout.Value.LockoutDuration);

            auditContext.Record<User>(AuditEventKind.SignInFailed, user.Id);

            if (user.IsLockedOut(now))
            {
                auditContext.Record<User>(AuditEventKind.AccountLockedOut, user.Id);
            }

            await database.SaveChangesAsync(cancellationToken);

            return ResultExtensions.Problem(AuthorizationErrors.InvalidCredentials);
        }

        // A staff user at the client's door, or a deactivated client: the generic 401, indistinguishable from a wrong password.
        if (!user.Role.MayHoldPortalSession() || !user.IsActive)
        {
            auditContext.Record<User>(AuditEventKind.SignInFailed, user.Id);
            await database.SaveChangesAsync(cancellationToken);

            return ResultExtensions.Problem(AuthorizationErrors.InvalidCredentials);
        }

        if (user.IsLockedOut(now))
        {
            auditContext.Record<User>(AuditEventKind.SignInFailed, user.Id);
            await database.SaveChangesAsync(cancellationToken);

            return ResultExtensions.Problem(AuthorizationErrors.AccountLocked);
        }

        user.RecordSuccessfulSignIn();

        auditContext.AttributeTo(new AuditActor(user.Id, user.FullName, user.Role));
        auditContext.Record<User>(AuditEventKind.SignedIn, user.Id);

        await database.SaveChangesAsync(cancellationToken);

        // The session leaves as a Set-Cookie header and the body carries no token. MustChangePassword is not consulted here: the
        // sign-in succeeds, and the gate then refuses every portal read until the client has set his own password (AC-A-3).
        minter.IssuePortal(http.Response, user, now);

        return Results.NoContent();
    }
}
