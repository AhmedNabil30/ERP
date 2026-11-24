using Kaff.Domain.Authorization;
using Kaff.Domain.Closure;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;

namespace Kaff.Domain.Accounting;

/// <summary>The accounting entries that wait for the Owner before they post (Nabil, 2026-11-24).</summary>
public enum AccountingRequestKind
{
    /// <summary>Record an accrual (KAFF-701).</summary>
    Accrual = 1,

    /// <summary>Release an accrual once the real cost has landed (KAFF-701).</summary>
    AccrualRelease = 2,

    /// <summary>Move one month of the prepayment schedules to expense (KAFF-701).</summary>
    PrepaymentAmortisation = 3,

    /// <summary>Charge one month of depreciation on the asset register (KAFF-700).</summary>
    Depreciation = 4,
}

public enum AccountingRequestStatus
{
    Requested = 1,

    Approved = 2,

    Rejected = 3,
}

/// <summary>
/// Finance asks, the Owner approves or returns the request with a reason (spec.md section 9: the Owner approves all financial
/// movements, and nobody creates and approves the same movement). Nothing posts until the Owner approves: the request stores what
/// was asked and the postings are built from it at approval. Same shape as the hold release request (KAFF-802).
/// </summary>
public sealed class AccountingRequest : Entity
{
    private AccountingRequest()
    {
    }

    private AccountingRequest(AccountingRequestKind kind, Guid? projectId, string payload, string? subjectKey, Guid requestedByUserId, DateTimeOffset requestedAt, Money? amount, int? lineCount)
        : base(NewId())
    {
        SubjectKey = subjectKey;
        Kind = kind;
        ProjectId = projectId;
        Payload = payload;
        Status = AccountingRequestStatus.Requested;
        RequestedByUserId = requestedByUserId;
        RequestedAt = requestedAt;
        Amount = amount;
        LineCount = lineCount;
    }

    public AccountingRequestKind Kind { get; private set; }

    /// <summary>The project tag; null is the company tag.</summary>
    public Guid? ProjectId { get; private set; }

    /// <summary>What Finance asked for, as JSON. The postings are built from it at approval.</summary>
    public string Payload { get; private set; } = null!;

    /// <summary>
    /// What the request acts on (the month and project, or the accrual), so two waiting requests for the same subject cannot both post.
    /// Null for an accrual, which has no earlier subject. A partial unique index on waiting requests enforces it (F-R4-2).
    /// </summary>
    public string? SubjectKey { get; private set; }

    /// <summary>
    /// The total of the postings the dry run built at ask time, so the Owner sees what he approves. Approval rebuilds and refuses if the
    /// total moved. Null only on rows asked before the amount was stored; those cannot be approved and must be asked again.
    /// </summary>
    public Money? Amount { get; private set; }

    /// <summary>How many postings the dry run built at ask time. Null on rows asked before the amount was stored.</summary>
    public int? LineCount { get; private set; }

    public AccountingRequestStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? RejectionReason { get; private set; }

    public static Result<AccountingRequest> Create(
        AccountingRequestKind kind, Guid? projectId, string payload, string? subjectKey, Guid actor, DateTimeOffset now, Money? amount = null, int? lineCount = null) =>
        Enum.IsDefined(kind) && !string.IsNullOrWhiteSpace(payload)
            ? Result.Success(new AccountingRequest(kind, projectId, payload, subjectKey, actor, now, amount, lineCount))
            : Result.Failure<AccountingRequest>(AccountingErrors.AccountWrongType);

    /// <summary>Whether <paramref name="actor"/> may decide now: the request still waits, and the actor is not the user who asked.</summary>
    public Result EnsureMayDecide(Guid actor) =>
        Status != AccountingRequestStatus.Requested
            ? Result.Failure(AccountingErrors.RequestNotWaiting)
            : SeparationOfDuties.EnsureDifferentActor(RequestedByUserId, actor);

    public Result Approve(Guid actor, DateTimeOffset now)
    {
        Result gate = EnsureMayDecide(actor);
        if (gate.IsFailure)
        {
            return gate;
        }

        Decide(AccountingRequestStatus.Approved, actor, now, null);
        return Result.Success();
    }

    /// <summary>The Owner returns the request with a stored reason. It returns to Finance, who may ask again. Nothing is posted.</summary>
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

        Decide(AccountingRequestStatus.Rejected, actor, now, reason.Trim());
        return Result.Success();
    }

    private void Decide(AccountingRequestStatus status, Guid actor, DateTimeOffset now, string? reason)
    {
        Status = status;
        DecidedByUserId = actor;
        DecidedAt = now;
        RejectionReason = reason;
    }
}
