using Kaff.Api.Authorization;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;
using Microsoft.AspNetCore.Http;

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
/// so answering from the claim would be the exact defect the brief for this story named. The row this
/// handler projects is the one <see cref="LiveSession"/> loaded and checked for this request.
/// </para>
/// <para>
/// <b>The freshness checks are <see cref="LiveSession.RequireLiveSession"/>'s, not this handler's —
/// and that is the fix, not a tidy-up.</b> They lived here as a hand-copy of what
/// <c>PermissionSubjectReader</c> and <c>PermissionEvaluator</c> do together on a gated route, and the
/// copy carried two of the three: <see cref="User.IsActive"/> and the stamp, but not the role bar. So
/// this endpoint answered a <see cref="Role.Subcontractor"/> — spec.md §9, "record only, no login" —
/// with a <c>200</c> and their name (qa/slice-1/verification-2026-08-26.md, <c>V-26-B</c>). All three
/// now live in one place that every exempt route shares. The bar is here because no staff session may
/// exist for that role at all, which is a property of the door — not because a path to it is open
/// today; see decisions.md D-089 on why the report's "reachable in production" is one step longer than
/// recorded, and D-082 §4 on what arguing from reachability costs.
/// </para>
/// <para>
/// <b>Not gated by <c>MustChangePassword</c>, on purpose — <c>AC-105a-C</c>.</b> The endpoint carries no
/// <c>RequirePermission</c>, so <c>PermissionEvaluator</c>'s <c>PasswordChangeRequired</c> short-circuit
/// (D-086) never runs on this route at all; a caller who has not yet replaced a temporary credential
/// still gets a <c>200</c> and this profile (decisions.md D-072 §2). The company-wide permission set
/// such a caller sees is still empty, because <see cref="PermissionEvaluator.CompanyWidePermissionsHeld"/>
/// runs the ordinary evaluator per permission and that evaluator refuses everything while the flag is
/// set — an honest answer to "what can you do right now", not a second rule invented here.
/// <see cref="LiveSession"/> does not consult the flag either, for the same reason: it applies the
/// three session facts and nothing about what a session may reach.
/// </para>
/// <para>
/// <b>No audit record.</b> A read changes nothing; CLAUDE.md requires a record on a state change, and
/// this is not one.
/// </para>
/// </remarks>
internal static class Handler
{
    /// <remarks>
    /// Returns a completed <see cref="ValueTask{TResult}"/> rather than being <c>async</c>: the one
    /// database read this endpoint needs has already happened in <c>RequireLiveSession</c>'s filter,
    /// so nothing here awaits. <b>The name stays <c>HandleAsync</c> deliberately</b> — it is a cited
    /// identifier (decisions.md D-087, qa/slice-1/verification-2026-08-26.md), and SM-31's whole
    /// point is that a citation names something stable.
    /// </remarks>
    public static ValueTask<IResult> HandleAsync(HttpContext http)
    {
        User user = LiveSession.Caller(http);

        var subject = new PermissionSubject(
            user.Id,
            user.Role,
            user.Department,
            user.OperationsSubDepartment,
            user.ClientId,
            user.FullName,
            user.MustChangePassword);

        IReadOnlyList<Permission> permissions = PermissionEvaluator.CompanyWidePermissionsHeld(subject);

        return ValueTask.FromResult<IResult>(Results.Ok(new Response(
            user.Id,
            user.FullName,
            user.Role,
            user.Department,
            user.OperationsSubDepartment,
            user.MustChangePassword,
            permissions)));
    }
}
