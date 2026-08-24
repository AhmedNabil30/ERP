using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Users.MoveUserDepartment;

/// <summary>
/// Moves one user between departments. KAFF-108.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every rule about who may hold what department goes through <c>User.MoveToDepartment</c>, and
/// nothing here reproduces one.</b> That method calls the same <c>ValidateDepartment</c> that
/// <c>User.Create</c> calls, so there is one rule and not two (KAFF-108 rule 2)
/// [Verified: 2026-08-23 @ <c>User.cs</c> -&gt; <c>MoveToDepartment</c>, calling
/// <c>ValidateDepartment</c>].
/// </para>
/// <para>
/// <b><c>AC-108-D</c> is the move-path half of <c>AC-106-K</c>, and it is earned the same way.</b>
/// The domain guard binding <c>Role.Hr</c> to <c>Department.Hr</c> can only be bypassed by a handler
/// that never calls <c>MoveToDepartment</c>, or by one that "helpfully" corrects the department
/// before it does. This handler passes the request through untouched and returns whatever the domain
/// says, so the refusal reaches the caller as a 400 and the stored department is unchanged. See
/// decisions.md D-066 §2 for the create-path mutation that proved a Domain test cannot see this.
/// </para>
/// <para>
/// <b>The move writes two columns and stops</b> (<c>AC-108-F</c>, KAFF-108 rule 6). No assignment is
/// touched — <c>ProjectAssignment</c> constrains the role, not the department
/// [Verified: 2026-08-23 @ <c>ProjectAssignment.cs</c> -&gt; <c>Create</c>] — and the security stamp
/// is deliberately not rotated: <c>AC-108-A</c> and <c>AC-108-B</c> require the user's existing token
/// to keep working and to carry the new authority on its very next request (D-048). Rotating it here
/// would sign them out instead, which is a different act and belongs to the role change (D-051 Q27).
/// </para>
/// <para>
/// <b>No audit record is written here.</b> The department move is an entity change, so the change
/// tracker sees it and <c>AuditSaveChangesInterceptor</c> writes the <c>Modified</c> record in the
/// same transaction, with the before and after states and <c>ChangedProperties</c> naming the two
/// columns. A hand-written record is what decisions.md D-031 and KAFF-118 rule 2 forbid.
/// <c>GrantPath</c> stays null because <c>UserManage</c> is company-wide: no project, no access
/// policy, no path to name.
/// </para>
/// <para>
/// No <c>Money</c> moves. It can move somebody into Operations / Administrative, which is
/// <b>half</b> of what <c>SiteExpenseConfirm</c> requires — the other half is
/// <c>Role.TechnicalOffice</c>, and the evaluator discards any role-less grant on a money-touching
/// permission outright
/// [Verified: 2026-08-23 @ <c>PermissionCatalogue.cs</c> -&gt; the <c>Permission.SiteExpenseConfirm</c>
/// row; @ <c>PermissionEvaluator.cs</c> -&gt; <c>TouchesMoney</c>]. That is <c>AC-108-G</c>, and it is
/// why the audit record above is not optional.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid userId,
        Request request,
        KaffDbContext database,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User? user = await database.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return ResultExtensions.Problem(IdentityErrors.UserNotFound);
        }

        Result moved = user.MoveToDepartment(request.Department, request.OperationsSubDepartment);

        if (moved.IsFailure)
        {
            return ResultExtensions.Problem(moved.Error);
        }

        await database.SaveChangesAsync(cancellationToken);

        // 204. The move has no result of its own to report: S-008 re-reads the user it is showing,
        // and the authority the move actually changes is never in a response body — it is re-read
        // from the database on the moved user's next request.
        return Microsoft.AspNetCore.Http.Results.NoContent();
    }
}
