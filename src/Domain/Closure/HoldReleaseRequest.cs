using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Closure;

/// <summary>Where a hold release request stands.</summary>
public enum HoldReleaseRequestStatus
{
    /// <summary>Asked for and waiting for the Owner. Nothing is posted.</summary>
    Requested = 1,

    /// <summary>The Owner approved and the release posting was written in the same save.</summary>
    Approved = 2,

    /// <summary>The Owner rejected it, with a stored reason. Nothing was posted; it may be asked for again.</summary>
    Rejected = 3,
}

/// <summary>
/// The hold release as a financial movement in two steps (KAFF-802 AC-802-L, spec.md section 9: "Owner approves all
/// financial movements" and "nobody creates and approves the same movement"). Finance asks, the Owner approves, and the
/// single <see cref="PostingType.HoldRelease"/> posting is written only at approval, by
/// <see cref="ProjectClosure.ReleaseHold"/>, for the hold balance read at that moment.
/// </summary>
/// <remarks>
/// The request stores no amount. The amount is the hold ledger summed from postings when the Owner approves, because the
/// hold may still grow between the two steps and a stored figure would be a stored balance.
/// </remarks>
public sealed class HoldReleaseRequest : Entity
{
    private HoldReleaseRequest()
    {
    }

    private HoldReleaseRequest(Guid projectId, Guid requestedByUserId, DateTimeOffset requestedAt)
        : base(NewId())
    {
        ProjectId = projectId;
        Status = HoldReleaseRequestStatus.Requested;
        RequestedByUserId = requestedByUserId;
        RequestedAt = requestedAt;
    }

    public Guid ProjectId { get; private set; }

    public HoldReleaseRequestStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Why the Owner rejected it. Null unless Rejected.</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Finance asks for the release. Every check the release posting will face is asked now (contract type, project state,
    /// already released, nothing to release), so a request that could never be approved is refused at once. A second
    /// request while one is waiting is refused.
    /// </summary>
    public static Result<HoldReleaseRequest> Request(
        Project project,
        Account? hold,
        Account? receivable,
        Money holdBalance,
        bool alreadyReleased,
        bool requestWaiting,
        PostingProjectStatePolicy statePolicy,
        Guid actor,
        DateTimeOffset now,
        DateOnly postingDate)
    {
        Result<PostingSet> wouldPost = ProjectClosure.ReleaseHold(
            project, hold, receivable, holdBalance, alreadyReleased, actor, now, postingDate, statePolicy);
        if (wouldPost.IsFailure)
        {
            return Result.Failure<HoldReleaseRequest>(wouldPost.Error);
        }

        return requestWaiting
            ? Result.Failure<HoldReleaseRequest>(ClosureErrors.HoldReleaseAlreadyRequested)
            : new HoldReleaseRequest(project.Id, actor, now);
    }

    /// <summary>
    /// The Owner approves: the release posting is built by <see cref="ProjectClosure.ReleaseHold"/> with the hold balance
    /// read now, and the request is marked Approved. The caller commits both in one save. The user who asked may not approve.
    /// </summary>
    public Result<PostingSet> Approve(
        Guid actor,
        Project project,
        Account? hold,
        Account? receivable,
        Money holdBalance,
        bool alreadyReleased,
        PostingProjectStatePolicy statePolicy,
        DateTimeOffset now,
        DateOnly postingDate)
    {
        ArgumentNullException.ThrowIfNull(project);

        Result gate = EnsureMayDecide(actor);
        if (gate.IsFailure)
        {
            return Result.Failure<PostingSet>(gate.Error);
        }

        if (project.Id != ProjectId)
        {
            return Result.Failure<PostingSet>(ClosureErrors.HoldReleaseNotRequested);
        }

        Result<PostingSet> built = ProjectClosure.ReleaseHold(
            project, hold, receivable, holdBalance, alreadyReleased, actor, now, postingDate, statePolicy);
        if (built.IsFailure)
        {
            return built;
        }

        Decide(HoldReleaseRequestStatus.Approved, actor, now, null);
        return built;
    }

    /// <summary>The Owner rejects with a stored reason. It returns to Finance, who may ask again. Nothing is posted.</summary>
    public Result Reject(Guid actor, string? reason, DateTimeOffset now)
    {
        Result gate = EnsureMayDecide(actor);
        if (gate.IsFailure)
        {
            return gate;
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > Project.MaxReasonLength)
        {
            return Result.Failure(ClosureErrors.ReasonRequired);
        }

        Decide(HoldReleaseRequestStatus.Rejected, actor, now, reason.Trim());
        return Result.Success();
    }

    /// <summary>Whether <paramref name="actor"/> may approve or reject now: still waiting, and not the user who asked.</summary>
    public Result EnsureMayDecide(Guid actor) =>
        Status != HoldReleaseRequestStatus.Requested
            ? Result.Failure(ClosureErrors.HoldReleaseNotRequested)
            : SeparationOfDuties.EnsureDifferentActor(RequestedByUserId, actor);

    private void Decide(HoldReleaseRequestStatus status, Guid actor, DateTimeOffset now, string? reason)
    {
        Status = status;
        DecidedByUserId = actor;
        DecidedAt = now;
        RejectionReason = reason;
    }
}
