using System.Collections.Frozen;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;

namespace Kaff.Domain.Extracts;

/// <summary>A role that may take the next step, with the lowest assignment level the catalogue accepts for it.</summary>
public sealed record WaitingRole(Role Role, AssignmentLevel MinimumLevel);

/// <summary>
/// What an extract in a given status is waiting for, and on whom. <see cref="Roles"/> is read from the permission
/// catalogue, never written here, so a regrant moves it. Empty <see cref="Roles"/> with a null
/// <see cref="Permission"/> means nothing is waited for: the chain has ended.
/// </summary>
/// <param name="IsDecisionGate">
/// True when the step runs <see cref="Extract.EnsureMayDecide"/>, so a user who already acted on the extract in
/// this cycle cannot be the one to take it: the role holds the step, but not every holder of the role may act.
/// </param>
public sealed record ExtractWaitingOn(
    ExtractStatus Status,
    IReadOnlyList<ExtractStatus> NextStatuses,
    Permission? Permission,
    IReadOnlyList<WaitingRole> Roles,
    bool IsDecisionGate)
{
    public bool IsTerminal => Permission is null;
}

/// <summary>
/// Derives the next step of an extract from the state machine and the permission catalogue.
/// </summary>
/// <remarks>
/// <para>
/// The forward targets of a status are <see cref="Extract.LegalTargetsFrom"/> less the two exits that are not
/// a step forward: a rejection back to Draft, a dispute, and a disputed extract's cancellation to Void (D-241 item 2).
/// The only thing written here is which permission
/// performs the step that <i>enters</i> a status; who holds that permission comes from the catalogue. A new
/// transition into a status this table does not know throws, and a test walks every transition to prove it.
/// </para>
/// <para>
/// <see cref="ExtractStatus.Disputed"/> has no transition out in the machine (resolution checks its own state), so its step is
/// named here as an out-of-machine step, pinned by a test.
/// </para>
/// </remarks>
public static class ExtractWaiting
{
    /// <summary>The permission that performs the step entering each status.</summary>
    public static readonly FrozenDictionary<ExtractStatus, Permission> PermissionToEnter =
        new Dictionary<ExtractStatus, Permission>
        {
            [ExtractStatus.QcSubmitted] = Permission.ExtractQcReportSubmit,
            [ExtractStatus.TechnicalApproved] = Permission.QuantityGateApprove,
            [ExtractStatus.PartiallyApproved] = Permission.QuantityGateApprove,
            [ExtractStatus.DeductionsApplied] = Permission.ExtractDeductionsApply,
            [ExtractStatus.OwnerApproved] = Permission.FinancialMovementApprove,
            [ExtractStatus.Issued] = Permission.FinancialMovementApprove,
            [ExtractStatus.Collected] = Permission.ExtractCollect,
        }.ToFrozenDictionary();

    /// <summary>The statuses whose entering step is a decision, so separation of duties applies (pinned by a test).</summary>
    public static readonly FrozenSet<ExtractStatus> DecisionGates =
        new HashSet<ExtractStatus>
        {
            ExtractStatus.TechnicalApproved,
            ExtractStatus.PartiallyApproved,
            ExtractStatus.DeductionsApplied,
            ExtractStatus.OwnerApproved,
        }.ToFrozenSet();

    /// <summary>Statuses whose next step is not a transition of the machine.</summary>
    public static readonly FrozenDictionary<ExtractStatus, Permission> OutOfMachine =
        new Dictionary<ExtractStatus, Permission>
        {
            [ExtractStatus.Disputed] = Permission.ExtractDisputeResolve,
        }.ToFrozenDictionary();

    /// <summary>
    /// The next step of this extract. A Draft returned for wrong quantities waits for the site engineer to revise the
    /// evidence (D-242 item 2), not for the QC report; every other extract waits as its status says.
    /// </summary>
    public static ExtractWaitingOn For(Extract extract)
    {
        ArgumentNullException.ThrowIfNull(extract);

        return extract.AwaitsEvidenceRevision
            ? Build(ExtractStatus.Draft, [ExtractStatus.Draft], Permission.ExtractPrepare, isDecisionGate: false)
            : For(extract.Status);
    }

    public static ExtractWaitingOn For(ExtractStatus status)
    {
        if (OutOfMachine.TryGetValue(status, out Permission resolver))
        {
            return Build(status, [], resolver, isDecisionGate: false);
        }

        ExtractStatus[] forward =
        [
            .. Extract.LegalTargetsFrom(status).Where(target => target is not (ExtractStatus.Draft or ExtractStatus.Disputed or ExtractStatus.Void)),
        ];

        if (forward.Length == 0)
        {
            return new ExtractWaitingOn(status, [], null, [], IsDecisionGate: false);
        }

        HashSet<Permission> permissions = [];
        foreach (ExtractStatus target in forward)
        {
            if (!PermissionToEnter.TryGetValue(target, out Permission permission))
            {
                throw new InvalidOperationException(
                    $"The step into {target} has no permission in ExtractWaiting.PermissionToEnter.");
            }

            permissions.Add(permission);
        }

        if (permissions.Count != 1)
        {
            throw new InvalidOperationException($"The forward steps out of {status} are held by different permissions.");
        }

        return Build(status, forward, permissions.Single(), forward.Any(DecisionGates.Contains));
    }

    private static ExtractWaitingOn Build(
        ExtractStatus status, ExtractStatus[] next, Permission permission, bool isDecisionGate) =>
        new(
            status,
            next,
            permission,
            [.. PermissionCatalogue.Of(permission).Grants
                .Where(grant => grant.Role is not null)
                .Select(grant => new WaitingRole(grant.Role!.Value, grant.MinimumAssignmentLevel))],
            isDecisionGate);
}
