using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Assignments.RevokeProjectAssignment;

/// <summary>
/// Closes one <c>ProjectAssignment</c> row. KAFF-114.
/// </summary>
/// <remarks>
/// <para>
/// <b>The only rule here is the domain's, inside <c>ProjectAssignment.Revoke</c>, and this handler
/// does not restate it.</b> Stamping <c>RevokedAt</c> / <c>RevokedByUserId</c> and refusing a second
/// revocation both live there
/// [Verified: 2026-08-25 @ <c>src/Domain/Identity/ProjectAssignment.cs</c> -&gt; <c>Revoke</c>]. No
/// <c>Validator.cs</c> exists for the same reason <c>AssignUserToProject</c> and
/// <c>MoveUserDepartment</c> have none — a validator here would be a second place for a rule that must
/// have exactly one (decisions.md D-074 §1).
/// </para>
/// <para>
/// <b>The row is not deleted, ever.</b> The query below fetches it, <c>Revoke</c> stamps it, and
/// <c>SaveChangesAsync</c> persists the same row with two more columns filled in — there is no
/// <c>Remove</c> call anywhere in this file, which is the whole of "revocation is not deletion"
/// (<c>AC-114-F</c>) from the handler's side; the route side is that no delete-shaped endpoint exists
/// at all.
/// </para>
/// <para>
/// <b>Found by <c>Id</c> and <c>ProjectId</c> together, whether or not it is already revoked.</b> An
/// already-revoked row must still be found so <c>Revoke</c> can refuse it with
/// <c>AssignmentAlreadyRevoked</c> (<c>AC-114-D</c>) rather than the handler reporting "not found" for
/// a row that exists but is closed — those are different facts and must not collapse into one
/// refusal. An id that names no row on this project at all, revoked or active, is the one case that
/// is genuinely absent, and gets <see cref="IdentityErrors.ProjectAssignmentNotFound"/>.
/// </para>
/// <para>
/// <b>No audit record is written here.</b> The assignment is an entity change, so
/// <c>AuditSaveChangesInterceptor</c> writes the <c>Modified</c> record in the same transaction,
/// naming <c>RevokedAt</c> and <c>RevokedByUserId</c> in <c>ChangedProperties</c> and carrying the
/// <c>ProjectId</c> and <c>GrantPath</c> the gate granted on — the same mechanism
/// <c>AssignUserToProject</c>'s <c>Created</c> record uses, for the reverse action.
/// </para>
/// <para>
/// No <c>Money</c> moves. Revocation withdraws nothing the person already did (KAFF-114 rule 5) — it
/// only ends what they may do next.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Guid assignmentId,
        KaffDbContext database,
        ICurrentUser currentUser,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        ProjectAssignment? assignment = await database.ProjectAssignments
            .FirstOrDefaultAsync(
                candidate => candidate.Id == assignmentId && candidate.ProjectId == projectId,
                cancellationToken);

        if (assignment is null)
        {
            return ResultExtensions.Problem(IdentityErrors.ProjectAssignmentNotFound);
        }

        Result revoked = assignment.Revoke(currentUser.UserId ?? Guid.Empty, clock.GetUtcNow());

        if (revoked.IsFailure)
        {
            return ResultExtensions.Problem(revoked.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        // 204. Nothing to report — the act's only effect is observable on the next request the
        // revoked user makes, exactly as KAFF-110's deactivation and KAFF-108's department move are.
        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
