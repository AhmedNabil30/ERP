using Kaff.Api.Common.Results;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Features.Assignments.AssignUserToProject;

/// <summary>
/// Creates one <c>ProjectAssignment</c>. KAFF-113.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every rule about who may be assigned and at what level goes through
/// <c>ProjectAssignment.Create</c>, and nothing here reproduces one.</b> The external-role refusal
/// (<c>AC-113-F</c>) and both halves of the seniority rule (<c>AC-113-E</c>) live there
/// [Verified: 2026-08-24 @ <c>ProjectAssignment.cs</c> -&gt; <c>Create</c>], and this handler passes
/// the request's level through untouched and returns whatever the domain says. A handler that
/// "helpfully" coerced a Finance user's <c>Supervisor</c> to <c>Standard</c> would compile clean,
/// keep every Domain test green, and create the row anyway — decisions.md D-066 §2 recorded exactly
/// that mutation on the create-user path.
/// </para>
/// <para>
/// <b>The project is not checked here, and that is the gate's answer rather than an omission.</b>
/// <c>Permission.ProjectAssignmentManage</c> is project-scoped, so a caller only reaches this method
/// after <c>IProjectAccessPolicy</c> granted access to the project the route names — and every one
/// of its four paths requires the project to exist, including the two global ones
/// [Verified: 2026-08-24 @ <c>ProjectAccessPolicy.cs</c> -&gt; <c>GlobalReachAsync</c>]. That is
/// <c>AC-113-C</c>: HR naming a project id that does not exist is refused with a 403, not a 500 and
/// not a foreign-key violation.
/// </para>
/// <para>
/// <b>No audit record is written here.</b> The assignment is an entity change, so the change tracker
/// sees it and <c>AuditSaveChangesInterceptor</c> writes the <c>Created</c> record in the same
/// transaction. A hand-written record is what decisions.md D-031 and KAFF-118 rule 2 forbid. Unlike
/// the user slices, this record <b>does</b> carry a <c>GrantPath</c>: <c>ProjectAssignment</c> has a
/// <c>ProjectId</c>, so the interceptor pairs it with the path the gate granted on
/// [Verified: 2026-08-24 @ <c>AuditSaveChangesInterceptor.cs</c> -&gt; <c>ExtractProjectId</c>] —
/// <c>HrGlobal</c> when HR staffed a project it holds no row on, <c>OwnerGlobal</c> for the Owner,
/// which is the distinction KAFF-116 exists to make (decisions.md D-070).
/// </para>
/// <para>
/// No <c>Money</c> moves. It decides who can later prepare, gate, approve and disburse on this
/// project, which is why the record above is not optional.
/// </para>
/// </remarks>
internal static class Handler
{
    public static async Task<IResult> HandleAsync(
        Guid projectId,
        Request request,
        KaffDbContext database,
        ICurrentUser currentUser,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(currentUser);

        User? user = await database.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == request.UserId, cancellationToken);

        if (user is null)
        {
            return ResultExtensions.Problem(IdentityErrors.UserNotFound);
        }

        // KAFF-113 rule 8. A leaver's row would say they are on a team they left, and KAFF-111
        // revokes exactly these rows on deactivation — re-creating one is the same defect arriving
        // from the other direction.
        if (!user.IsActive)
        {
            return ResultExtensions.Problem(IdentityErrors.UserIsInactive);
        }

        // KAFF-113 rule 9, the friendly path. ux_project_assignments_active is the enforcement; this
        // exists so the ordinary case is a 409 with a key rather than a caught constraint violation.
        // Revoked rows are excluded by the same filter the index carries, so re-assignment after a
        // revocation is legal.
        bool alreadyOnTheProject = await database.ProjectAssignments
            .AnyAsync(
                assignment => assignment.ProjectId == projectId
                              && assignment.UserId == user.Id
                              && assignment.RevokedAt == null,
                cancellationToken);

        if (alreadyOnTheProject)
        {
            return ResultExtensions.Problem(IdentityErrors.UserAlreadyAssignedToProject);
        }

        Result<ProjectAssignment> created = ProjectAssignment.Create(
            projectId,
            user,
            request.Level,

            // KAFF-113 rule 10. The gate has already established this caller exists, is active and
            // holds the permission, so the id is present by construction — but Guid.Empty here would
            // be a row claiming nobody made it, on a table whose whole purpose is answering who could
            // act on the day a movement was approved.
            currentUser.UserId ?? Guid.Empty,
            clock.GetUtcNow());

        if (created.IsFailure)
        {
            return ResultExtensions.Problem(created.Error);
        }

        ProjectAssignment assignment = created.Value;
        database.ProjectAssignments.Add(assignment);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateActiveAssignment(exception))
        {
            // The check above is not the enforcement: two requests can both pass it. The loser of the
            // race must get the same refusal as everyone else rather than a 500.
            return ResultExtensions.Problem(IdentityErrors.UserAlreadyAssignedToProject);
        }

        return Microsoft.AspNetCore.Http.Results.Created(
            $"/api/projects/{projectId}/assignments/{assignment.Id}",
            new Response(
                assignment.Id,
                assignment.ProjectId,
                assignment.UserId,
                assignment.Level,
                assignment.AssignedByUserId,
                assignment.AssignedAt));
    }

    /// <summary>A unique-violation on the active-assignment index, and nothing else.</summary>
    private static bool IsDuplicateActiveAssignment(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
           && string.Equals(postgres.ConstraintName, "ux_project_assignments_active", StringComparison.Ordinal);
}
