using Kaff.Domain.Common;
using Kaff.Domain.MasterData;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.Sales;

/// <summary>
/// Position in the pipeline. 🟡 spec.md §3 and assumption 6 mark the stage names as unconfirmed.
/// </summary>
public enum OpportunityStage
{
    Lead = 1,
    Meeting = 2,

    /// <summary>معاينة — the paid site visit (spec.md §3, §14).</summary>
    SiteVisit = 3,

    Quotation = 4,
    Negotiation = 5,
    Contract = 6,
}

/// <summary>
/// Whether the opportunity is live, dormant or finished.
/// </summary>
/// <remarks>
/// Separate from <see cref="OpportunityStage"/> because spec.md §3 describes Stalled as something
/// that happens *to* an opportunity while it keeps its stage: "day 7 status becomes Stalled …
/// Activity revives it." Collapsing the two would lose the stage a stalled opportunity reverts to.
///
/// OPEN QUESTION — see decisions.md D-017. spec.md §13 lists Stalled, OnHold, ClosedLost and
/// Reopened alongside the stages without saying which are stages and which are statuses.
/// </remarks>
public enum OpportunityStatus
{
    Active = 1,

    /// <summary>Set automatically after the inactivity window; revives on activity (spec.md §3).</summary>
    Stalled = 2,

    OnHold = 3,

    /// <summary>Converted to a project (spec.md §3).</summary>
    ClosedWon = 4,

    /// <summary>MUST carry a reason (spec.md §3).</summary>
    ClosedLost = 5,
}

/// <summary>
/// How quiet a live opportunity is, derived on read from <see cref="Opportunity.LastActivityAt"/> and the
/// <see cref="OpportunityInactivity"/> day counts (spec.md §3). Never stored.
/// </summary>
public enum InactivityLevel
{
    None = 0,

    /// <summary>Day-2 flag.</summary>
    FirstAlert = 1,

    /// <summary>Day-4 flag.</summary>
    SecondAlert = 2,

    /// <summary>Day-7: shown as <see cref="OpportunityStatus.Stalled"/>.</summary>
    Stalled = 3,
}

/// <summary>
/// A sales opportunity. Owned by Sales (spec.md §2); becomes a Project at Closed Won.
/// </summary>
/// <remarks>
/// <b>Deliberately thin.</b> Slice 0 models the entity because spec.md §2 lists it as a core record
/// and the Project links to it. The pipeline behaviour — inactivity alerts, the معاينة deposit,
/// pre-contract expenses, quotation and conversion — is slice 4 and is NOT implemented here. Do not
/// assume any of it exists.
/// </remarks>
public sealed class Opportunity : Entity
{
    public const int MaxCodeLength = 32;
    public const int MaxTitleLength = 200;
    public const int MaxReasonLength = 1000;

    private Opportunity()
    {
    }

    private Opportunity(Guid id, string code, Guid clientId, string title, DateTimeOffset createdAt)
        : base(id)
    {
        Code = code;
        ClientId = clientId;
        Title = title;
        Stage = OpportunityStage.Lead;
        Status = OpportunityStatus.Active;
        CreatedAt = createdAt;
        LastActivityAt = createdAt;
    }

    public string Code { get; private set; } = null!;

    /// <summary>spec.md §3: "Reopening attaches to the same Client. Never create a duplicate client."</summary>
    public Guid ClientId { get; private set; }

    public string Title { get; private set; } = null!;

    public OpportunityStage Stage { get; private set; }

    public OpportunityStatus Status { get; private set; }

    /// <summary>spec.md §3: "Closed Lost MUST record a reason."</summary>
    public string? ClosedLostReason { get; private set; }

    /// <summary>Drives the inactivity alerts and the automatic Stalled status (spec.md §3).</summary>
    public DateTimeOffset LastActivityAt { get; private set; }

    /// <summary>Set at Closed Won. spec.md §3 converts Opportunity → Project{type}.</summary>
    public Guid? ConvertedProjectId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Opportunity> Create(string code, Guid clientId, string title, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength)
        {
            return Result.Failure<Opportunity>(MasterDataErrors.CodeRequired);
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > MaxTitleLength)
        {
            return Result.Failure<Opportunity>(MasterDataErrors.NameRequired);
        }

        return Result.Success(new Opportunity(NewId(), code.Trim().ToUpperInvariant(), clientId, title.Trim(), createdAt));
    }

    /// <summary>spec.md §3: activity revives a stalled opportunity.</summary>
    public void RecordActivity(DateTimeOffset occurredAt)
    {
        LastActivityAt = occurredAt;

        if (Status == OpportunityStatus.Stalled)
        {
            Status = OpportunityStatus.Active;
        }
    }

    /// <summary>True while the opportunity can still move: Active, or Stalled by the stored flag.</summary>
    public bool IsLive => Status is OpportunityStatus.Active or OpportunityStatus.Stalled;

    /// <summary>
    /// How quiet the opportunity is at <paramref name="now"/>. Computed on read, never stored (R3-03 AC-C).
    /// An OnHold or closed opportunity never flags (R3-03 AC-E).
    /// </summary>
    public InactivityLevel InactivityAt(DateTimeOffset now, OpportunityInactivity rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (!IsLive)
        {
            return InactivityLevel.None;
        }

        TimeSpan quiet = now - LastActivityAt;

        if (Status == OpportunityStatus.Stalled || quiet >= TimeSpan.FromDays(rule.StalledDays))
        {
            return InactivityLevel.Stalled;
        }

        if (quiet >= TimeSpan.FromDays(rule.SecondAlertDays))
        {
            return InactivityLevel.SecondAlert;
        }

        return quiet >= TimeSpan.FromDays(rule.FirstAlertDays) ? InactivityLevel.FirstAlert : InactivityLevel.None;
    }

    /// <summary>The status to show: a quiet Active opportunity reads as Stalled once the window has passed.</summary>
    public OpportunityStatus StatusAt(DateTimeOffset now, OpportunityInactivity rule) =>
        InactivityAt(now, rule) == InactivityLevel.Stalled ? OpportunityStatus.Stalled : Status;

    /// <summary>spec.md §3: one stage forward at a time, Lead to Negotiation. Contract is reached only by converting to a project (R3-09, Nabil 2026-11-24). A move is activity.</summary>
    public Result AdvanceStage(DateTimeOffset occurredAt)
    {
        if (!IsLive)
        {
            return Result.Failure(SalesErrors.OpportunityNotLive);
        }

        if (Stage == OpportunityStage.Negotiation)
        {
            return Result.Failure(SalesErrors.AlreadyAtLastStage);
        }

        Stage++;
        RecordActivity(occurredAt);
        return Result.Success();
    }

    /// <summary>spec.md §13: OnHold. The stage is untouched, so resuming returns to the same stage.</summary>
    public Result PutOnHold(DateTimeOffset occurredAt)
    {
        if (!IsLive)
        {
            return Result.Failure(SalesErrors.OpportunityNotLive);
        }

        Status = OpportunityStatus.OnHold;
        LastActivityAt = occurredAt;
        return Result.Success();
    }

    /// <summary>Leaves OnHold at the same stage. The clock restarts, or it would stall at once.</summary>
    public Result Resume(DateTimeOffset occurredAt)
    {
        if (Status != OpportunityStatus.OnHold)
        {
            return Result.Failure(SalesErrors.OpportunityNotOnHold);
        }

        Status = OpportunityStatus.Active;
        LastActivityAt = occurredAt;
        return Result.Success();
    }

    public Result CloseLost(string reason, DateTimeOffset occurredAt)
    {
        if (Status is OpportunityStatus.ClosedLost or OpportunityStatus.ClosedWon)
        {
            return Result.Failure(SalesErrors.OpportunityAlreadyClosed);
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > MaxReasonLength)
        {
            return Result.Failure(MasterDataErrors.ClosedLostRequiresReason);
        }

        Status = OpportunityStatus.ClosedLost;
        ClosedLostReason = reason.Trim();
        LastActivityAt = occurredAt;
        return Result.Success();
    }

    /// <summary>
    /// spec.md §3: reopening attaches to the same client; nothing here can create one. The stage it was
    /// closed at is kept. The row drops the old reason; the audit record of the close keeps it.
    /// </summary>
    public Result Reopen(DateTimeOffset occurredAt)
    {
        if (Status != OpportunityStatus.ClosedLost)
        {
            return Result.Failure(SalesErrors.OpportunityNotClosedLost);
        }

        Status = OpportunityStatus.Active;
        ClosedLostReason = null;
        LastActivityAt = occurredAt;
        return Result.Success();
    }
}
