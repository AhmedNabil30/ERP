using Kaff.Domain.Common;

namespace Kaff.Domain.Identity;

/// <summary>
/// Links a user to a project. The second half of "permission = role × assignment" (spec.md §9).
/// </summary>
/// <remarks>
/// <para>
/// spec.md §9: "A user MUST be assigned to a project to open it or act on it. Role alone is
/// insufficient." Without a row here, a correctly-roled user is refused.
/// </para>
/// <para>
/// Revocation is a soft close, not a delete: the row keeps <see cref="RevokedAt"/> so the audit
/// trail can answer "who could act on this project on the day that extract was approved". A unique
/// index covers active rows only, so a user may be re-assigned after revocation.
/// </para>
/// <para>
/// Clients are never assigned. A portal user reaches a project through <c>Project.ClientId</c>
/// matching <c>User.ClientId</c> — see <c>IProjectAccessPolicy</c>.
/// </para>
/// </remarks>
public sealed class ProjectAssignment : Entity
{
    private ProjectAssignment()
    {
    }

    private ProjectAssignment(
        Guid id,
        Guid projectId,
        Guid userId,
        AssignmentLevel level,
        Guid assignedByUserId,
        DateTimeOffset assignedAt)
        : base(id)
    {
        ProjectId = projectId;
        UserId = userId;
        Level = level;
        AssignedByUserId = assignedByUserId;
        AssignedAt = assignedAt;
    }

    public Guid ProjectId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Seniority on this project. Meaningful for site engineers (spec.md §9).</summary>
    public AssignmentLevel Level { get; private set; }

    public Guid AssignedByUserId { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    /// <summary>
    /// Computed, never stored as an independent flag — a stored copy would be a second source of
    /// truth that could disagree with <see cref="RevokedAt"/>.
    /// </summary>
    public bool IsActive => RevokedAt is null;

    public static Result<ProjectAssignment> Create(
        Guid projectId,
        User user,
        AssignmentLevel level,
        Guid assignedByUserId,
        DateTimeOffset assignedAt)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.Role is Role.Client or Role.Subcontractor)
        {
            // spec.md §12 scopes the client through Project.ClientId; §9 gives subcontractors no login.
            return Result.Failure<ProjectAssignment>(IdentityErrors.ClientIsNotAssignable);
        }

        if (level != AssignmentLevel.Standard && user.Role != Role.SiteEngineer)
        {
            // spec.md §9 attaches Junior/Supervisor to the site engineer role only.
            return Result.Failure<ProjectAssignment>(IdentityErrors.AssignmentLevelNotApplicable);
        }

        if (level == AssignmentLevel.Standard && user.Role == Role.SiteEngineer)
        {
            return Result.Failure<ProjectAssignment>(IdentityErrors.AssignmentLevelNotApplicable);
        }

        return Result.Success(new ProjectAssignment(
            NewId(),
            projectId,
            user.Id,
            level,
            assignedByUserId,
            assignedAt));
    }

    public Result Revoke(Guid revokedByUserId, DateTimeOffset revokedAt)
    {
        if (RevokedAt is not null)
        {
            return Result.Failure(IdentityErrors.AssignmentAlreadyRevoked);
        }

        RevokedAt = revokedAt;
        RevokedByUserId = revokedByUserId;
        return Result.Success();
    }
}
