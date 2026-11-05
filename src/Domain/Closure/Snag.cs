using System.Collections.Frozen;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;

namespace Kaff.Domain.Closure;

/// <summary>Who owns the defect (spec.md section 11).</summary>
public enum SnagOwnerKind
{
    Kaff = 1,
    Subcontractor = 2,
}

/// <summary>How serious the snag is. <see cref="Major"/> blocks handover, <see cref="Minor"/> does not (spec.md section 11).</summary>
public enum SnagPriority
{
    Minor = 1,
    Major = 2,
}

/// <summary>spec.md section 11: <c>open, fixed, verified</c>.</summary>
public enum SnagStatus
{
    Open = 1,
    Fixed = 2,
    Verified = 3,
}

/// <summary>spec.md section 11: snag resolution is a dual toggle. Neither option touches the hold.</summary>
public enum SnagResolutionKind
{
    /// <summary>Deducted from the subcontractor who owned the snag.</summary>
    DebitNote = 1,

    /// <summary>Absorbed by Kaff as internal cost.</summary>
    Absorbed = 2,
}

/// <summary>
/// One defect found at practical completion (spec.md section 11). Carries no money: <see cref="Resolve"/> records which way
/// the cost goes and nothing is posted, because the cost, the account and the debit-note object are unruled (Q112, KAFF-512).
/// A snag never takes anything out of the hold.
/// </summary>
public sealed class Snag : Entity
{
    public const int MaxDescriptionLength = 1000;
    public const int MaxLocationLength = 200;

    private static readonly FrozenSet<(SnagStatus From, SnagStatus To)> LegalTransitions =
        new HashSet<(SnagStatus, SnagStatus)>
        {
            (SnagStatus.Open, SnagStatus.Fixed),
            (SnagStatus.Fixed, SnagStatus.Verified),
        }.ToFrozenSet();

    private Snag()
    {
    }

    private Snag(
        Guid projectId,
        int number,
        string description,
        string location,
        SnagOwnerKind ownerKind,
        Guid? subcontractorId,
        SnagPriority priority,
        Guid raisedBy,
        DateTimeOffset raisedAt)
        : base(NewId())
    {
        ProjectId = projectId;
        Number = number;
        Description = description;
        Location = location;
        OwnerKind = ownerKind;
        SubcontractorId = subcontractorId;
        Priority = priority;
        Status = SnagStatus.Open;
        RaisedByUserId = raisedBy;
        RaisedAt = raisedAt;
    }

    public Guid ProjectId { get; private set; }

    public int Number { get; private set; }

    public string Description { get; private set; } = null!;

    public string Location { get; private set; } = null!;

    public SnagOwnerKind OwnerKind { get; private set; }

    public Guid? SubcontractorId { get; private set; }

    public SnagPriority Priority { get; private set; }

    public SnagStatus Status { get; private set; }

    public Guid RaisedByUserId { get; private set; }

    public DateTimeOffset RaisedAt { get; private set; }

    public Guid? FixedByUserId { get; private set; }

    public DateTimeOffset? FixedAt { get; private set; }

    public Guid? VerifiedByUserId { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    public SnagResolutionKind? ResolutionKind { get; private set; }

    public string? ResolutionReason { get; private set; }

    public Guid? ResolvedByUserId { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>
    /// RULING: Q115. A major snag blocks handover until it is verified; a fixed one is not yet confirmed. See
    /// <c>Rulings.SnagHandoverBlock</c>.
    /// </summary>
    public bool BlocksHandover => Priority == SnagPriority.Major && Status != SnagStatus.Verified;

    public static bool IsLegalTransition(SnagStatus from, SnagStatus to) => LegalTransitions.Contains((from, to));

    public static Result<Snag> Raise(
        Project project,
        int number,
        string? description,
        string? location,
        SnagOwnerKind? ownerKind,
        Guid? subcontractorId,
        SnagPriority? priority,
        Guid raisedBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.ContractType == ContractType.Design)
        {
            return Result.Failure<Snag>(ClosureErrors.NotApplicableToDesign);
        }

        if (project.Status != ProjectStatus.HandoverPending)
        {
            return Result.Failure<Snag>(ClosureErrors.SnagListNotOpen);
        }

        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > MaxDescriptionLength)
        {
            return Result.Failure<Snag>(ClosureErrors.SnagDescriptionRequired);
        }

        if (string.IsNullOrWhiteSpace(location) || location.Trim().Length > MaxLocationLength)
        {
            return Result.Failure<Snag>(ClosureErrors.SnagLocationRequired);
        }

        if (ownerKind is null || !Enum.IsDefined(ownerKind.Value)
            || (ownerKind == SnagOwnerKind.Subcontractor) != (subcontractorId is not null)
            || subcontractorId == Guid.Empty)
        {
            return Result.Failure<Snag>(ClosureErrors.SnagOwnerRequired);
        }

        if (priority is null || !Enum.IsDefined(priority.Value))
        {
            return Result.Failure<Snag>(ClosureErrors.SnagPriorityRequired);
        }

        return Result.Success(new Snag(
            project.Id, number, description.Trim(), location.Trim(), ownerKind.Value, subcontractorId, priority.Value, raisedBy, now));
    }

    public Result Fix(Guid actor, DateTimeOffset now)
    {
        if (!IsLegalTransition(Status, SnagStatus.Fixed))
        {
            return Result.Failure(ClosureErrors.SnagIllegalTransition);
        }

        Status = SnagStatus.Fixed;
        FixedByUserId = actor;
        FixedAt = now;
        return Result.Success();
    }

    public Result Verify(Guid actor, DateTimeOffset now)
    {
        if (!IsLegalTransition(Status, SnagStatus.Verified))
        {
            return Result.Failure(ClosureErrors.SnagIllegalTransition);
        }

        Status = SnagStatus.Verified;
        VerifiedByUserId = actor;
        VerifiedAt = now;
        return Result.Success();
    }

    /// <summary>
    /// Records the dual toggle of spec.md section 11 with who and why. A debit note names a subcontractor, so the snag's owner
    /// must be one. Posts nothing: see the class remarks.
    /// </summary>
    public Result Resolve(SnagResolutionKind? kind, string? reason, Guid actor, DateTimeOffset now)
    {
        if (kind is null || !Enum.IsDefined(kind.Value))
        {
            return Result.Failure(ClosureErrors.ResolutionRequired);
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > Project.MaxReasonLength)
        {
            return Result.Failure(ClosureErrors.ReasonRequired);
        }

        if (Status == SnagStatus.Open)
        {
            return Result.Failure(ClosureErrors.SnagNotFixed);
        }

        if (ResolutionKind is not null)
        {
            return Result.Failure(ClosureErrors.SnagAlreadyResolved);
        }

        if (kind == SnagResolutionKind.DebitNote && OwnerKind != SnagOwnerKind.Subcontractor)
        {
            return Result.Failure(ClosureErrors.DebitNeedsSubcontractorOwner);
        }

        ResolutionKind = kind;
        ResolutionReason = reason.Trim();
        ResolvedByUserId = actor;
        ResolvedAt = now;
        return Result.Success();
    }
}
