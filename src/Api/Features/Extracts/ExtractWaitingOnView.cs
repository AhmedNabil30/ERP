using Kaff.Domain.Authorization;
using Kaff.Domain.Extracts;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Features.Extracts;

/// <summary>A person on a project's staff, as the waiting-on read needs them.</summary>
public sealed record StaffMember(Guid UserId, string FullName, Role Role, AssignmentLevel? Level);

/// <summary>
/// Who an extract is waiting on. <see cref="Roles"/> and <see cref="Permission"/> come from the catalogue through
/// <see cref="ExtractWaiting"/>. <see cref="Holders"/> are the people who could take the step now.
/// <see cref="BarredHolders"/> hold the role but already acted on this extract in the current cycle, so separation
/// of duties bars them from a decision gate: the screen can say who is waited for and who cannot be.
/// </summary>
public sealed record WaitingOnResponse(
    ExtractStatus Status,
    IReadOnlyList<ExtractStatus> NextStatuses,
    Permission? Permission,
    IReadOnlyList<WaitingRole> Roles,
    bool IsDecisionGate,
    bool IsTerminal,
    IReadOnlyList<StaffMember> Holders,
    IReadOnlyList<StaffMember> BarredHolders);

internal static class ExtractWaitingOnView
{
    /// <summary>
    /// The project's assigned staff plus every active Owner (the Owner reaches every project without an assignment
    /// row). Names only: no contact data.
    /// </summary>
    public static async Task<IReadOnlyList<StaffMember>> StaffAsync(
        KaffDbContext database, Guid projectId, CancellationToken cancellationToken)
    {
        List<StaffMember> assigned = await database.ProjectAssignments
            .AsNoTracking()
            .Where(assignment => assignment.ProjectId == projectId && assignment.RevokedAt == null)
            .Join(database.Users.Where(user => user.IsActive), assignment => assignment.UserId, user => user.Id,
                (assignment, user) => new StaffMember(user.Id, user.FullName, user.Role, assignment.Level))
            .ToListAsync(cancellationToken);

        List<StaffMember> owners = await database.Users
            .AsNoTracking()
            .Where(user => user.IsActive && user.Role == Role.Owner)
            .Select(user => new StaffMember(user.Id, user.FullName, user.Role, null))
            .ToListAsync(cancellationToken);

        return [.. assigned.Concat(owners.Where(owner => assigned.All(member => member.UserId != owner.UserId)))];
    }

    public static WaitingOnResponse Build(Extract extract, IReadOnlyList<StaffMember> staff)
    {
        ArgumentNullException.ThrowIfNull(extract);
        ArgumentNullException.ThrowIfNull(staff);

        ExtractWaitingOn waiting = ExtractWaiting.For(extract);

        List<StaffMember> eligible =
        [
            .. staff.Where(member => waiting.Roles.Any(role =>
                role.Role == member.Role && (member.Level is null || member.Level >= role.MinimumLevel))),
        ];

        // Same call the gate itself makes, so the list and the refusal cannot disagree.
        bool Barred(StaffMember member) => waiting.IsDecisionGate && extract.EnsureMayDecide(member.UserId).IsFailure;

        return new WaitingOnResponse(
            waiting.Status,
            waiting.NextStatuses,
            waiting.Permission,
            waiting.Roles,
            waiting.IsDecisionGate,
            waiting.IsTerminal,
            [.. eligible.Where(member => !Barred(member))],
            [.. eligible.Where(Barred)]);
    }
}
