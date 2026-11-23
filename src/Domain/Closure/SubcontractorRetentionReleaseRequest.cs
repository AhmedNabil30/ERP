using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Closure;

/// <summary>
/// The subcontractor retention release as a financial movement in two steps (KAFF-806 AC-806-E, spec.md section 9:
/// "Owner approves all financial movements" and "nobody creates and approves the same movement"). Finance asks, the Owner
/// approves, and the single <see cref="PostingType.SubcontractorRetentionRelease"/> posting is written only at approval,
/// by <see cref="SubcontractorRetention.Release"/>, for the retention balance read at that moment. Same shape as
/// <see cref="HoldReleaseRequest"/> and the same <see cref="HoldReleaseRequestStatus"/> values.
/// </summary>
/// <remarks>
/// The request stores no amount. The amount is the retention account summed from postings when the Owner approves, and a
/// stored figure would be a stored balance.
/// </remarks>
public sealed class SubcontractorRetentionReleaseRequest : Entity
{
    private SubcontractorRetentionReleaseRequest()
    {
    }

    private SubcontractorRetentionReleaseRequest(Guid projectId, Guid subcontractorId, Guid requestedByUserId, DateTimeOffset requestedAt)
        : base(NewId())
    {
        ProjectId = projectId;
        SubcontractorId = subcontractorId;
        Status = HoldReleaseRequestStatus.Requested;
        RequestedByUserId = requestedByUserId;
        RequestedAt = requestedAt;
    }

    public Guid ProjectId { get; private set; }

    public Guid SubcontractorId { get; private set; }

    public HoldReleaseRequestStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Why the Owner rejected it. Null unless Rejected.</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Finance asks for the release. Every check the release posting will face is asked now (project state, warranty end,
    /// accounts, something retained), so a request that could never be approved is refused at once. A second request for
    /// the same subcontractor while one waits is refused.
    /// </summary>
    public static Result<SubcontractorRetentionReleaseRequest> Request(
        Project project,
        Guid subcontractorId,
        Account? retention,
        Account? payable,
        Money retained,
        bool requestWaiting,
        PostingProjectStatePolicy statePolicy,
        Guid actor,
        DateTimeOffset now,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(project);

        Result<PostingSet> wouldPost = SubcontractorRetention.Release(
            project, subcontractorId, retention, payable, retained, actor, now, today, statePolicy);
        if (wouldPost.IsFailure)
        {
            return Result.Failure<SubcontractorRetentionReleaseRequest>(wouldPost.Error);
        }

        return requestWaiting
            ? Result.Failure<SubcontractorRetentionReleaseRequest>(ClosureErrors.RetentionReleaseAlreadyRequested)
            : new SubcontractorRetentionReleaseRequest(project.Id, subcontractorId, actor, now);
    }

    /// <summary>
    /// The Owner approves: the release posting is built by <see cref="SubcontractorRetention.Release"/> with the retention
    /// balance read now, and the request is marked Approved. The caller commits both in one save. The user who asked may
    /// not approve.
    /// </summary>
    public Result<PostingSet> Approve(
        Guid actor,
        Project project,
        Account? retention,
        Account? payable,
        Money retained,
        PostingProjectStatePolicy statePolicy,
        DateTimeOffset now,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(project);

        Result gate = EnsureMayDecide(actor);
        if (gate.IsFailure)
        {
            return Result.Failure<PostingSet>(gate.Error);
        }

        if (project.Id != ProjectId)
        {
            return Result.Failure<PostingSet>(ClosureErrors.RetentionReleaseNotRequested);
        }

        Result<PostingSet> built = SubcontractorRetention.Release(
            project, SubcontractorId, retention, payable, retained, actor, now, today, statePolicy);
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
            ? Result.Failure(ClosureErrors.RetentionReleaseNotRequested)
            : SeparationOfDuties.EnsureDifferentActor(RequestedByUserId, actor);

    private void Decide(HoldReleaseRequestStatus status, Guid actor, DateTimeOffset now, string? reason)
    {
        Status = status;
        DecidedByUserId = actor;
        DecidedAt = now;
        RejectionReason = reason;
    }
}
