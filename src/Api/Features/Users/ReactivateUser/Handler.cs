using Kaff.Api.Common.Results;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Users.ReactivateUser;

/// <summary>
/// Switches one account back on, with a fresh credential and no restored assignments. KAFF-112.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rule 5 is the one thing this handler must not do: touch <c>ProjectAssignment</c>.</b> D-049
/// ruling 5, verbatim: "a returning employee gets a new password and zero project assignments —
/// nothing is restored automatically." KAFF-111's revocations, written by <c>DeactivateUser</c>'s
/// handler, stay exactly as they are — this handler reads no <c>ProjectAssignment</c> row and writes
/// none. Whoever needs the person back on a project makes that a deliberate, separately-audited act
/// through <c>AssignUserToProject</c> (rule 10, <c>AC-112-I</c>).
/// </para>
/// <para>
/// <b>Rules 3 and 4, in order, built under the readiness waiver of decisions.md D-062 §1 — Q50 stays
/// open.</b> <c>User.ClearPassword()</c> runs unconditionally: the stored credential never survives a
/// reactivation, whether or not the Owner issues a new one in the same request. When
/// <c>TemporaryPassword</c> is supplied, <c>User.SetTemporaryPassword</c> runs afterwards — never
/// <c>SetOwnPassword</c>, for the same reason <c>CreateUser</c> never uses it: a credential somebody
/// else chose must be replaced on first sign-in.
/// </para>
/// <para>
/// <b>Rule 7 refuses before either of those runs.</b> <c>User.Reactivate()</c> is called first and its
/// failure short-circuits the handler, so reactivating an already-active account touches no credential
/// field — sourced to slice-0 code and to nothing Karim said (Q51, readiness waiver).
/// </para>
/// <para>
/// <b>No audit record is written here.</b> The user is the only entity this handler changes, so the
/// change tracker sees it and <c>AuditSaveChangesInterceptor</c> writes the <c>Modified</c> record in
/// the same transaction — <c>IsActive</c>, <c>DeactivatedAt</c> and the redacted credential fields in
/// <c>ChangedProperties</c>, actor the Owner, <c>GrantPath</c> null because <c>UserManage</c> is
/// company-wide (same reasoning as <c>DeactivateUser</c> and <c>CreateUser</c>).
/// </para>
/// <para>
/// No <c>Money</c> moves. It restores an identity, not the reach that identity used to have — reach
/// comes back only through a fresh, deliberate assignment.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid userId,
        Request request,
        KaffDbContext database,
        IAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);

        User? user = await database.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultExtensions.Problem(IdentityErrors.UserNotFound);
        }

        // KAFF-112 rule 7 — reactivating an active user is refused, not silently accepted. Refused
        // BEFORE any credential is touched. Sourced to slice-0 code and to nothing Karim said; built
        // under the readiness waiver of decisions.md D-062 §1, Q51 stays open.
        Result reactivated = user.Reactivate();

        if (reactivated.IsFailure)
        {
            return ResultExtensions.Problem(reactivated.Error);
        }

        // Rule 3 — unconditional. The old password does not come back, whether or not the Owner
        // issues a new one below.
        user.ClearPassword();

        // Rule 4 — optional, mirroring CreateUser.Request.TemporaryPassword: some accounts legitimately
        // hold no credential (KAFF-106 rule 10), and nothing here forces one on every reactivation.
        if (!string.IsNullOrWhiteSpace(request.TemporaryPassword))
        {
            Result issued = user.SetTemporaryPassword(PasswordHasher.Hash(request.TemporaryPassword));

            if (issued.IsFailure)
            {
                // Today this is only Role.Subcontractor — "record only, no login" (spec.md §9).
                return ResultExtensions.Problem(issued.Error);
            }
        }

        // AC-112 audit bullet — a reason is recorded when supplied, exactly as KAFF-110 (Q35 open).
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            audit.SetReason(request.Reason);
        }

        await database.SaveChangesAsync(cancellationToken);

        // 204. Rule 10 says role and department come back as they were — there is nothing new to
        // shape a response around, and no credential ever belongs in one (AC-103-G's reasoning holds
        // here too).
        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
