using Kaff.Domain.Common;

namespace Kaff.Domain.Extracts;

/// <summary>
/// One act at one gate of an extract's chain: who, when, and for a rejection why. Append-only and stored on the
/// extract, not reconstructed from the audit trail, because the chain's own rules read it: "nobody creates and
/// approves the same movement" needs the actors to be on the record before anyone tries to act, so a screen can
/// leave the control out instead of refusing the click.
/// </summary>
public sealed class ExtractGateRecord : Entity
{
    private ExtractGateRecord()
    {
    }

    internal ExtractGateRecord(
        Guid extractId,
        ExtractGate gate,
        ExtractGateOutcome outcome,
        Guid actorUserId,
        DateTimeOffset occurredAt,
        string? reason,
        Percentage? approvedPortion,
        ExtractRejectionKind? rejectionKind = null)
        : base(NewId())
    {
        RejectionKind = rejectionKind;
        ExtractId = extractId;
        Gate = gate;
        Outcome = outcome;
        ActorUserId = actorUserId;
        OccurredAt = occurredAt;
        Reason = reason;
        ApprovedPortion = approvedPortion;
    }

    public Guid ExtractId { get; private set; }

    public ExtractGate Gate { get; private set; }

    public ExtractGateOutcome Outcome { get; private set; }

    public Guid ActorUserId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Required and non-blank when <see cref="Outcome"/> is <see cref="ExtractGateOutcome.Rejected"/>.</summary>
    public string? Reason { get; private set; }

    /// <summary>The fraction of the period's work approved for payment, on a Technical Office record.</summary>
    public Percentage? ApprovedPortion { get; private set; }

    /// <summary>On a rejection, what the rejecter said was wrong and so who fixes it (D-242 item 2).</summary>
    public ExtractRejectionKind? RejectionKind { get; private set; }
}
