using Kaff.Domain.Adjustments;
using Kaff.Domain.Authorization;
using Kaff.Domain.Extracts;

namespace Kaff.Api.Features.Extracts;

/// <summary>
/// The waiting line of a document that is not an extract (journey gap 7): «بانتظار {person}: {task}». The task is an
/// i18n key; the holders are the people on the project who could take the step now, read from the permission
/// catalogue the way <see cref="WaitingOnResponse"/> reads it. <see cref="BarredHolders"/> hold the role but acted on the
/// document already, so separation of duties bars them from the decision.
/// </summary>
public sealed record WaitingLineResponse(
    string TaskKey,
    Permission Permission,
    IReadOnlyList<StaffMember> Holders,
    IReadOnlyList<StaffMember> BarredHolders);

internal static class WaitingLine
{
    /// <param name="barred">Who may not take this step on this document; empty when the step is not a decision.</param>
    public static WaitingLineResponse Build(
        string taskKey, Permission permission, IReadOnlyList<StaffMember> staff, IReadOnlyCollection<Guid> barred)
    {
        ArgumentNullException.ThrowIfNull(staff);
        ArgumentNullException.ThrowIfNull(barred);

        List<StaffMember> eligible =
        [
            .. staff.Where(member => PermissionCatalogue.Of(permission).Grants.Any(grant =>
                grant.Role == member.Role && (member.Level is null || member.Level >= grant.MinimumAssignmentLevel))),
        ];

        return new WaitingLineResponse(
            taskKey,
            permission,
            [.. eligible.Where(member => !barred.Contains(member.UserId))],
            [.. eligible.Where(member => barred.Contains(member.UserId))]);
    }

    /// <summary>
    /// The line of a Raised credit note: the Owner approves it, and whoever raised it or the dispute behind it may not
    /// (<c>Adjustment.Approve</c>). The one build behind the adjustment's read, the extract's read and the extract list's
    /// row, so the three cannot name different people or steps (D-260 item 5).
    /// </summary>
    public static WaitingLineResponse ForCreditNote(Adjustment raised, Extract? extract, IReadOnlyList<StaffMember> staff)
    {
        ArgumentNullException.ThrowIfNull(raised);

        return Build(
            "waiting.task.adjustment_approval",
            Permission.FinancialMovementApprove,
            staff,
            raised.PriorActors(extract?.DisputeActors() ?? []));
    }
}
