using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;

namespace Kaff.Domain.PettyCash;

/// <summary>The states of a عهدة request before Finance pays it.</summary>
public enum PettyCashRequestStatus
{
    /// <summary>A junior saved it; nothing is posted (spec.md section 9: a junior raises requests as drafts).</summary>
    Draft = 1,

    /// <summary>The supervisor submitted it. Its amount can no longer change.</summary>
    Submitted = 2,

    /// <summary>The supervisor sent it back to the junior with a stored reason.</summary>
    Returned = 3,
}

/// <summary>
/// A عهدة request: a junior drafts it, the supervisor submits or returns it (KAFF-609b, spec.md section 9: "a junior
/// engineer raises requests as drafts; the supervisor submits them"). It writes no posting. Paying it is Finance's step
/// and is not built (KAFF-311 waits on R2-Q3 and R2-Q6).
/// </summary>
/// <remarks>
/// There is no edit method: once drafted the amount never changes, so a submitted request cannot be edited. The ceiling
/// of 10,000 and the holder of the عهدة are R2-Q6 and are not enforced here.
/// </remarks>
public sealed class PettyCashRequest : Entity
{
    public const int MaxPurposeLength = 500;

    private PettyCashRequest()
    {
    }

    private PettyCashRequest(Guid projectId, Guid requestedByUserId, Money amount, string purpose, DateTimeOffset now)
        : base(NewId())
    {
        ProjectId = projectId;
        RequestedByUserId = requestedByUserId;
        Amount = amount;
        Purpose = purpose;
        Status = PettyCashRequestStatus.Draft;
        CreatedAt = now;
    }

    public Guid ProjectId { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public Money Amount { get; private set; }

    public string Purpose { get; private set; } = string.Empty;

    public PettyCashRequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? SubmittedByUserId { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public Guid? ReturnedByUserId { get; private set; }

    public DateTimeOffset? ReturnedAt { get; private set; }

    /// <summary>Why the supervisor sent it back. Null unless Returned.</summary>
    public string? ReturnReason { get; private set; }

    public static Result<PettyCashRequest> Draft(Guid projectId, Guid requestedByUserId, Money amount, string? purpose, DateTimeOffset now)
    {
        if (!amount.IsPositive)
        {
            return Result.Failure<PettyCashRequest>(PettyCashErrors.AmountInvalid);
        }

        return string.IsNullOrWhiteSpace(purpose) || purpose.Trim().Length > MaxPurposeLength
            ? Result.Failure<PettyCashRequest>(PettyCashErrors.PurposeRequired)
            : new PettyCashRequest(projectId, requestedByUserId, amount, purpose.Trim(), now);
    }

    /// <summary>The supervisor submits a draft. The user who drafted it may not submit it (spec.md section 9).</summary>
    public Result Submit(Guid actor, DateTimeOffset now)
    {
        Result gate = EnsureMayDecide(actor);
        if (gate.IsFailure)
        {
            return gate;
        }

        Status = PettyCashRequestStatus.Submitted;
        SubmittedByUserId = actor;
        SubmittedAt = now;
        return Result.Success();
    }

    /// <summary>The supervisor returns a draft to the junior with a stored reason.</summary>
    public Result Return(Guid actor, string? reason, DateTimeOffset now)
    {
        Result gate = EnsureMayDecide(actor);
        if (gate.IsFailure)
        {
            return gate;
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > Project.MaxReasonLength)
        {
            return Result.Failure(PettyCashErrors.ReasonRequired);
        }

        Status = PettyCashRequestStatus.Returned;
        ReturnedByUserId = actor;
        ReturnedAt = now;
        ReturnReason = reason.Trim();
        return Result.Success();
    }

    private Result EnsureMayDecide(Guid actor) =>
        Status != PettyCashRequestStatus.Draft
            ? Result.Failure(PettyCashErrors.NotDraft)
            : SeparationOfDuties.EnsureDifferentActor(RequestedByUserId, actor);
}
