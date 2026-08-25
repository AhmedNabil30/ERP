using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Users.ChangeUserRole;

/// <summary>
/// Changes one user's role and severs their direct link to every project it revokes. KAFF-109.
/// </summary>
/// <remarks>
/// <para>
/// <b>D-051 (Q27), not D-049 ruling 6 — the reversal.</b> The role change is never refused because
/// the user supervises a project. It <b>always</b> revokes every active <c>ProjectAssignment</c> the
/// user holds, Supervisor and Junior and Standard alike, and lets the change through. Re-assignment,
/// if the person is still needed in the new role, is a deliberate later act through
/// <c>AssignUserToProject</c> (KAFF-113) — never automatic here.
/// </para>
/// <para>
/// <b>The whole act is one <c>SaveChangesAsync</c>,</b> the same shape KAFF-111 already established
/// inside <c>DeactivateUser</c>'s handler (decisions.md D-074 §2: "one request, one correlation id").
/// The role change and every revocation it causes commit or roll back together — <c>AC-109-K</c> —
/// so there is no state where the role moved and an assignment did not.
/// </para>
/// <para>
/// <b>Rule 8 lives here, not in the entity.</b> <c>User.ChangeRole</c> re-validates unconditionally
/// and would succeed trivially on a same-role request, because state that was already valid cannot
/// fail re-validation. What makes a same-role request a no-op is that nothing is revoked — decided by
/// comparing the role before the call to the role after it, exactly once, before the revocation loop
/// runs at all (<c>AC-109-H</c>).
/// </para>
/// <para>
/// <b>No audit record is written here.</b> The role change and every revocation are entity changes,
/// so <c>AuditSaveChangesInterceptor</c> writes one <c>Modified</c> record per entity in the same
/// transaction — one <c>User</c> record carrying <c>Role</c> in <c>ChangedProperties</c>, and one
/// <c>ProjectAssignment</c> record per revoked row, each carrying its <c>ProjectId</c>
/// (<c>AC-109-J</c>). A hand-written record is what decisions.md D-031 and KAFF-118 rule 2 forbid.
/// <c>GrantPath</c> stays null throughout: <c>UserManage</c> is company-wide, so no project access
/// policy ran and there is no path to name — the same reasoning <c>DeactivateUser</c> carries.
/// </para>
/// <para>
/// No <c>Money</c> moves. It moves a person into and out of every role that does — a user becoming
/// <c>Role.Owner</c> acquires <c>FinancialMovementApprove</c> on every project on their very next
/// request (D-048) — which is why the record above is not optional.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid userId,
        Request request,
        KaffDbContext database,
        ICurrentUser currentUser,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(currentUser);

        User? user = await database.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultExtensions.Problem(IdentityErrors.UserNotFound);
        }

        Role previousRole = user.Role;

        Result changed = user.ChangeRole(request.Role);

        if (changed.IsFailure)
        {
            return ResultExtensions.Problem(changed.Error);
        }

        List<Guid> revokedProjectIds = [];

        // Rule 8 — a change to the role already held revokes nothing. Comparing before the loop runs
        // is what keeps AC-109-H's still-active assignment still active: the loop below never starts.
        if (user.Role != previousRole)
        {
            Guid actorId = currentUser.UserId ?? Guid.Empty;
            DateTimeOffset occurredAt = clock.GetUtcNow();

            List<ProjectAssignment> active = await database.ProjectAssignments
                .Where(assignment => assignment.UserId == user.Id && assignment.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (ProjectAssignment assignment in active)
            {
                // Discarded deliberately: every row here has RevokedAt == null, so
                // AssignmentAlreadyRevoked is unreachable — the same reasoning DeactivateUser's
                // handler already carries for the identical loop.
                _ = assignment.Revoke(actorId, occurredAt);
                revokedProjectIds.Add(assignment.ProjectId);
            }
        }

        await database.SaveChangesAsync(cancellationToken);

        return Microsoft.AspNetCore.Http.Results.Ok(
            new Response(user.Id, user.Role, revokedProjectIds));
    }
}
