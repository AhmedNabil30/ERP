using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Closure;

/// <summary>
/// KAFF-806 AC-806-D. The Owner-approved reversal of a posted subcontractor retention release (Nabil, 2026-11-24). Finance
/// asks, with a reason, naming the release posting; the Owner approves or returns it with a reason; the one reversing
/// posting is written only at approval, pointing at the original through <c>ReversesId</c>. Same shape as
/// <see cref="SubcontractorRetentionReleaseRequest"/> (D-265). The release posting is a flow posting, so the generic
/// reversal refuses it (<c>treasury.flow_posting_not_reversible</c>); this is the flow that reverses it, and the database
/// guard <c>KAFF_FLOW_REVERSAL</c> accepts the reversal only when an approved request of this kind decided at the same instant.
/// </summary>
public sealed class SubcontractorRetentionReversalRequest : Entity
{
    private SubcontractorRetentionReversalRequest()
    {
    }

    private SubcontractorRetentionReversalRequest(
        Guid projectId, Guid subcontractorId, Guid originalPostingId, string reason, Guid requestedByUserId, DateTimeOffset requestedAt)
        : base(NewId())
    {
        ProjectId = projectId;
        SubcontractorId = subcontractorId;
        OriginalPostingId = originalPostingId;
        Reason = reason;
        Status = HoldReleaseRequestStatus.Requested;
        RequestedByUserId = requestedByUserId;
        RequestedAt = requestedAt;
    }

    public Guid ProjectId { get; private set; }

    public Guid SubcontractorId { get; private set; }

    /// <summary>The release posting to reverse.</summary>
    public Guid OriginalPostingId { get; private set; }

    /// <summary>Why Finance asks for the reversal.</summary>
    public string Reason { get; private set; } = string.Empty;

    public HoldReleaseRequestStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Why the Owner returned it. Null unless Rejected.</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Finance asks. The posting must be a release of this subcontractor on this project, not itself a reversal, and not
    /// already reversed. A second request for the same posting while one waits is refused.
    /// </summary>
    public static Result<SubcontractorRetentionReversalRequest> Request(
        Project project,
        Guid subcontractorId,
        Posting? original,
        bool alreadyReversed,
        bool requestWaiting,
        string? reason,
        Guid actor,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > Project.MaxReasonLength)
        {
            return Result.Failure<SubcontractorRetentionReversalRequest>(ClosureErrors.ReasonRequired);
        }

        if (!IsReleaseOf(original, project.Id, subcontractorId))
        {
            return Result.Failure<SubcontractorRetentionReversalRequest>(ClosureErrors.RetentionReleasePostingNotFound);
        }

        if (alreadyReversed)
        {
            return Result.Failure<SubcontractorRetentionReversalRequest>(ClosureErrors.RetentionReleaseAlreadyReversed);
        }

        return requestWaiting
            ? Result.Failure<SubcontractorRetentionReversalRequest>(ClosureErrors.RetentionReversalAlreadyRequested)
            : new SubcontractorRetentionReversalRequest(project.Id, subcontractorId, original!.Id, reason.Trim(), actor, now);
    }

    /// <summary>
    /// The Owner approves: the mirror posting is built by <see cref="Posting.Reverse"/> and the request is marked Approved.
    /// The caller commits both in one save, and <paramref name="now"/> stamps the decision and the posting alike, which is
    /// what the database guard matches. The user who asked may not approve.
    /// </summary>
    public Result<Posting> Approve(
        Guid actor, Posting? original, bool alreadyReversed, Account? from, Account? to, DateTimeOffset now, DateOnly today)
    {
        Result gate = EnsureMayDecide(actor);
        if (gate.IsFailure)
        {
            return Result.Failure<Posting>(gate.Error);
        }

        if (original is null || original.Id != OriginalPostingId || from is null || to is null)
        {
            return Result.Failure<Posting>(ClosureErrors.RetentionReleasePostingNotFound);
        }

        if (alreadyReversed)
        {
            return Result.Failure<Posting>(ClosureErrors.RetentionReleaseAlreadyReversed);
        }

        Result<Posting> mirror = Posting.Reverse(original, from, to, today, actor, now);
        if (mirror.IsFailure)
        {
            return mirror;
        }

        Decide(HoldReleaseRequestStatus.Approved, actor, now, null);
        return mirror;
    }

    /// <summary>The Owner returns it to Finance with a stored reason. Nothing is posted.</summary>
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
            ? Result.Failure(ClosureErrors.RetentionReversalNotRequested)
            : SeparationOfDuties.EnsureDifferentActor(RequestedByUserId, actor);

    private static bool IsReleaseOf(Posting? posting, Guid projectId, Guid subcontractorId) =>
        posting is { Type: PostingType.SubcontractorRetentionRelease, ReversesId: null }
        && posting.ProjectId == projectId
        && posting.SourceDocument.Type == SourceDocumentType.SubcontractorRetentionRelease
        && posting.SourceDocument.Id == subcontractorId;

    private void Decide(HoldReleaseRequestStatus status, Guid actor, DateTimeOffset now, string? reason)
    {
        Status = status;
        DecidedByUserId = actor;
        DecidedAt = now;
        RejectionReason = reason;
    }
}
