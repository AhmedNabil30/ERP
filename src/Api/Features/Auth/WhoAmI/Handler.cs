using System.Security.Claims;
using Kaff.Api.Common.Results;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Auth.WhoAmI;

/// <summary>
/// Reports the caller's own identity and the company-wide permissions their role and department hold
/// today. KAFF-105a.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every field is read fresh from <c>Users</c>, never from the token's claims.</b>
/// <c>StaffSessionMinter.ClaimsFor</c> issues only the user id, the display name, the role at mint time
/// and the security stamp — department is not a claim at all. A role changed after the token was minted
/// (KAFF-109, decisions.md D-051 Q27) does not rotate the stamp, so the claim would still read the old
/// role. This is the one endpoint the frontend trusts to say who it is talking to (decisions.md D-050),
/// so answering from the claim would be the exact defect the brief for this story named.
/// </para>
/// <para>
/// <b>Self-only, like KAFF-103's endpoint and for the same reason.</b> No permission gate runs here, so
/// the handler re-applies the freshness <c>PermissionSubjectReader</c> gives every
/// <c>RequirePermission</c> route by hand: <c>IsActive</c> and the security stamp are re-checked
/// (decisions.md D-048, D-053), refusing with the same generic <c>errors.auth.forbidden</c> a stale
/// token gets everywhere else in this system (D-071, D-080) rather than answering with a profile built
/// from a row that no longer describes a live session.
/// </para>
/// <para>
/// <b>Not gated by <c>MustChangePassword</c>, on purpose — <c>AC-105a-C</c>.</b> The endpoint carries no
/// <c>RequirePermission</c>, so <c>PermissionEvaluator</c>'s <c>PasswordChangeRequired</c> short-circuit
/// (D-086) never runs on this route at all; a caller who has not yet replaced a temporary credential
/// still gets a <c>200</c> and this profile (decisions.md D-072 §2). The company-wide permission set
/// such a caller sees is still empty, because <see cref="PermissionEvaluator.CompanyWidePermissionsHeld"/>
/// runs the ordinary evaluator per permission and that evaluator refuses everything while the flag is
/// set — an honest answer to "what can you do right now", not a second rule invented here.
/// </para>
/// <para>
/// <b>No audit record.</b> A read changes nothing; CLAUDE.md requires a record on a state change, and
/// this is not one.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        HttpContext http,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);

        Guid? userId = ReadUserId(http.User);
        string? securityStamp = http.User.FindFirst(KaffClaimTypes.SecurityStamp)?.Value;

        if (userId is null)
        {
            return ResultExtensions.Problem(AuthorizationErrors.Forbidden);
        }

        User? user = await database.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        // The same freshness PermissionSubjectReader applies on every RequirePermission route (D-048,
        // D-053) — no gate ran here to apply it for us. A deactivated account, or a token a later stamp
        // rotation already superseded, must not be answered with a profile as if the session were live.
        if (user is null || !user.IsActive || user.SecurityStamp != securityStamp)
        {
            return ResultExtensions.Problem(AuthorizationErrors.Forbidden);
        }

        var subject = new PermissionSubject(
            user.Id,
            user.Role,
            user.Department,
            user.OperationsSubDepartment,
            user.ClientId,
            user.FullName,
            user.MustChangePassword);

        IReadOnlyList<Permission> permissions = PermissionEvaluator.CompanyWidePermissionsHeld(subject);

        return Results.Ok(new Response(
            user.Id,
            user.FullName,
            user.Role,
            user.Department,
            user.OperationsSubDepartment,
            user.MustChangePassword,
            permissions));
    }

    private static Guid? ReadUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(KaffClaimTypes.UserId)?.Value, out Guid id) ? id : null;
}
