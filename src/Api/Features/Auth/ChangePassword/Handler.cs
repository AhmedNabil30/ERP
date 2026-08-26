using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Api.Identity;
using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Auth.ChangePassword;

/// <summary>
/// Replaces the caller's own password. KAFF-103 — the forced first-sign-in change and a later
/// voluntary change are the same act, through the same endpoint: <c>User.SetOwnPassword</c> clears
/// <c>MustChangePassword</c> unconditionally, whether it was set or not.
/// </summary>
/// <remarks>
/// <para>
/// <b>Self-only, and that is the whole of its permission shape.</b> The story's own line: "authenticated
/// as the user themselves. Not <c>UserManage</c> — only the person changes it." There is no catalogue
/// permission for "act on your own row alone", so this endpoint carries no <c>RequirePermission</c> and
/// relies on the fallback policy (<c>RequireAuthenticatedUser</c>) the same way KAFF-102's sign-out
/// does — see <c>EndpointPermissionCoverageTests.SelfOnlyEndpoints</c> for the narrow exemption this
/// requires and why it is not a gap the blanket rule should widen to cover.
/// </para>
/// <para>
/// <b>The row is re-read fresh and re-checked for the same reasons D-048 checks it everywhere else</b>
/// — no permission gate runs here to do it for us. A deactivated account, or a stamp a later change
/// elsewhere has already superseded, is refused exactly as <c>PermissionSubjectReader</c> would refuse
/// it, with the same generic <c>errors.auth.forbidden</c> a stale token gets everywhere else in this
/// system (D-071, D-080) — not a status this handler invents.
/// </para>
/// <para>
/// <b>No audit record is hand-written here.</b> <c>SetOwnPassword</c> changes <c>PasswordHash</c>,
/// <c>SecurityStamp</c> and <c>MustChangePassword</c>; the change tracker sees all three and
/// <c>AuditSaveChangesInterceptor</c> writes the <c>Modified</c> record in the same transaction, with
/// the two credential fields redacted by <c>[AuditRedacted]</c> exactly as every other password-writing
/// path already redacts them.
/// </para>
/// <para>
/// <b>The actor is declared, not read from a grant.</b> No permission gate ran to populate
/// <c>VerifiedActor</c>, so this handler discards the inbound identity and calls
/// <c>IAuditContext.AttributeTo</c> itself — the same shape KAFF-101a's sign-in and KAFF-102's sign-out
/// use, and for the same reason: an authenticated request may not attribute its own save to a claim
/// the gate never verified (decisions.md D-075).
/// </para>
/// <para>
/// <b>A fresh session cookie is minted on success</b>, carrying the new security stamp
/// <c>SetOwnPassword</c> just rotated. Rule 4 ends every <i>other</i> session immediately; without a
/// new cookie here, the device that just changed the password would also be signed out by its own act,
/// which <c>AC-103-A</c> ("I can use the rest of the system") refuses to accept.
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
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(http);

        Guid? userId = ReadUserId(http.User);
        string? securityStamp = http.User.FindFirst(KaffClaimTypes.SecurityStamp)?.Value;

        if (userId is null)
        {
            return ResultExtensions.Problem(AuthorizationErrors.Forbidden);
        }

        User? user = await database.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        // The same freshness check PermissionSubjectReader applies everywhere else (D-048, D-053) —
        // no permission gate ran here to apply it for us, and this endpoint must not be the one place
        // a deactivated account or a superseded token still works.
        if (user is null || !user.IsActive || user.SecurityStamp != securityStamp)
        {
            return ResultExtensions.Problem(AuthorizationErrors.Forbidden);
        }

        bool currentIsCorrect = PasswordHasher.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash);

        if (!currentIsCorrect)
        {
            // AC-103-D. Nothing about the stored hash changes on this path.
            return ResultExtensions.Problem(AuthorizationErrors.CurrentPasswordIncorrect);
        }

        Result changed = user.SetOwnPassword(PasswordHasher.Hash(request.NewPassword ?? string.Empty));

        if (changed.IsFailure)
        {
            // Today only Role.Subcontractor (AC-103-H) — unreachable through this door in practice,
            // since StorePasswordHash already refuses that role a credential, so no subcontractor can
            // ever hold the session this endpoint requires. Handled anyway: the refusal lives in one
            // place and every caller of SetOwnPassword gets it.
            return ResultExtensions.Problem(changed.Error);
        }

        DateTimeOffset now = clock.GetUtcNow();

        // Discard the inbound identity before naming the actor — the same shape KAFF-101a's SignIn and
        // KAFF-102's SignOut use, and for the same reason (decisions.md D-075): no permission gate ran
        // on this self-only endpoint to populate VerifiedActor, and an authenticated request may not
        // AttributeTo a different actor.
        http.User = new ClaimsPrincipal(new ClaimsIdentity());

        auditContext.AttributeTo(new AuditActor(user.Id, user.FullName, user.Role));

        await database.SaveChangesAsync(cancellationToken);

        // Rule 4: SetOwnPassword already rotated the stamp, which ends every other session. This
        // device gets a fresh cookie in the same response so AC-103-A holds — the caller is not logged
        // out by their own change.
        minter.Issue(http.Response, user, now);

        return Results.NoContent();
    }

    private static Guid? ReadUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(KaffClaimTypes.UserId)?.Value, out Guid id) ? id : null;
}
