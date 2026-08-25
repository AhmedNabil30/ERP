using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Users.DeactivateUser;

/// <summary>
/// Switches one account off, and takes the holder off the teams they were on. KAFF-110, KAFF-111.
/// </summary>
/// <remarks>
/// <para>
/// <b>The whole act is one <c>SaveChangesAsync</c>.</b> KAFF-110 rule 7: the deactivation and the
/// revocations are "one request, one correlation id". They are also one transaction, so there is no
/// half-deactivated user — a user switched off whose assignments survived would read, to the access
/// policy and to a future team panel alike, as somebody who is still on the team.
/// </para>
/// <para>
/// <b>The revocation is handler work rather than entity work</b> (D-049, KAFF-111): <c>User</c>
/// cannot reach the assignment rows, and giving it a way to would put a query inside an entity to
/// satisfy one rule. <c>ProjectAssignment.Revoke</c> already stamps <c>RevokedAt</c> and
/// <c>RevokedByUserId</c> and keeps the row, and <c>IsActive</c> is computed from <c>RevokedAt</c>
/// rather than stored [Verified: 2026-08-24 @ <c>ProjectAssignment.cs</c> -&gt; <c>Revoke</c>,
/// <c>IsActive</c>] — so "removed from the active team" and "kept on the historical team" are one
/// mechanism, not two (D-049 ruling 5). <b>Nothing called it on deactivation until this handler.</b>
/// </para>
/// <para>
/// <b>Nothing here ends a session, and nothing here needs to.</b> <c>User.Deactivate</c> rotates the
/// security stamp [Verified: 2026-08-24 @ <c>User.cs</c> -&gt; <c>Deactivate</c>], which is the
/// global sign-out of D-053 — the subject read compares the stamp in the <c>WHERE</c> clause, so
/// every token in existence for that user stops matching at once (<c>AC-110-C</c>). And the same read
/// filters <c>user.IsActive</c>, so the very next request is refused even on a device whose stamp
/// were somehow current (<c>AC-110-A</c>, <c>AC-110-B</c>). Two independent refusals, neither of them
/// written here.
/// </para>
/// <para>
/// <b>No audit record is written here either.</b> Both the user and the assignments are entity
/// changes, so the interceptor writes one <c>Modified</c> record each in the same transaction, all
/// sharing the request's correlation id — one <c>User</c> record and one per revoked assignment
/// (<c>AC-110-F</c>, KAFF-111 rule 5). A single summary record cannot carry eight, and a hand-written
/// record is what decisions.md D-031 and KAFF-118 rule 2 forbid.
/// </para>
/// <para>
/// <b>The user's own <c>GrantPath</c> is null and each assignment's is too.</b> <c>UserManage</c> is
/// company-wide: the request names no project, so no access policy ran and there is no path to name
/// (D-070 §1). The assignment records carry a <c>ProjectId</c> and would take the granted path if
/// there were one [Verified: 2026-08-24 @ <c>AuditSaveChangesInterceptor.cs</c> -&gt;
/// <c>ExtractProjectId</c>] — there is not, which is honest: these revocations happened under the
/// Owner's company-wide authority, not through any project (KAFF-111's permissions bullet).
/// </para>
/// <para>
/// No <c>Money</c> moves. It removes somebody's ability to move it, which must be observable in the
/// trail at a timestamp.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid userId,
        Request? request,
        KaffDbContext database,
        IAuditContext audit,
        ICurrentUser currentUser,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(currentUser);

        User? user = await database.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultExtensions.Problem(IdentityErrors.UserNotFound);
        }

        DateTimeOffset occurredAt = clock.GetUtcNow();

        // KAFF-110 rule 5 — a second deactivation is refused, not quietly accepted. Refused BEFORE
        // anything else, so AC-110-J's "no assignment is touched a second time" is structural: the
        // revocation loop below is never reached.
        //
        // Sourced to slice-0 code and to nothing Karim said. Built under the readiness waiver of
        // decisions.md D-062 §1; Q51 stays open.
        Result deactivated = user.Deactivate(occurredAt);

        if (deactivated.IsFailure)
        {
            return ResultExtensions.Problem(deactivated.Error);
        }

        // AC-110-G. Set on the unit of work, so it lands on every record this save writes — the user
        // and each revoked assignment — rather than being threaded through entity methods that a
        // later state transition could bypass. Whitespace is not a reason: SetReason refuses it, and
        // "supplied" has to mean something for the criterion to be assertable.
        if (!string.IsNullOrWhiteSpace(request?.Reason))
        {
            audit.SetReason(request.Reason);
        }

        Guid actorId = currentUser.UserId ?? Guid.Empty;

        // KAFF-111 rules 1 and 3. Every active row, revoked by the Owner performing the deactivation
        // — not by nobody, and not by the user themselves. Rule 9: a leaver with none is not an
        // error and writes no assignment records, which is this loop running zero times.
        List<ProjectAssignment> active = await database.ProjectAssignments
            .Where(assignment => assignment.UserId == user.Id && assignment.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (ProjectAssignment assignment in active)
        {
            // The Result is discarded deliberately: every row in this list has RevokedAt == null, so
            // AssignmentAlreadyRevoked is unreachable, and failing the whole deactivation on it would
            // make a leaver un-removable because of a row that is already in the state we want.
            _ = assignment.Revoke(actorId, occurredAt);
        }

        await database.SaveChangesAsync(cancellationToken);

        // 204. The act has no result of its own to report, and the thing it actually changes — what
        // the holder's next request reaches — is never in a response body.
        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
