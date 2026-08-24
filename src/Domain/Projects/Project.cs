using System.Collections.Frozen;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.MasterData;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Projects;

/// <summary>
/// A project. One entity for all three contract types (spec.md §5, CLAUDE.md).
/// </summary>
/// <remarks>
/// <para>
/// <b>One entity, three types.</b> The contract type selects an <c>IBillingCalculator</c> and an
/// <c>IProgressMetric</c>. It does not select a different table, a different treasury or a different
/// approval chain. The type-specific terms below are nullable columns on this one row, guarded so
/// that Cost Plus terms cannot be written onto a Design project.
/// </para>
/// <para>
/// <b>Not modelled here.</b> BOQ, extracts, change orders, daily logs, snags, design stages and
/// linked-project credit posting are later slices. This entity carries identity, contract terms and
/// the spec.md §13 state machine — the things every other slice needs to exist before it can start.
/// </para>
/// </remarks>
public sealed class Project : Entity
{
    public const int MaxCodeLength = 32;
    public const int MaxNameLength = 200;
    public const int MaxReasonLength = 1000;

    private static readonly FrozenSet<(ProjectStatus From, ProjectStatus To)> ExecutionTransitions =
        new HashSet<(ProjectStatus, ProjectStatus)>
        {
            (ProjectStatus.Setup, ProjectStatus.Active),
            (ProjectStatus.Active, ProjectStatus.HandoverPending),
            (ProjectStatus.HandoverPending, ProjectStatus.Handover),
            (ProjectStatus.Handover, ProjectStatus.UnderWarranty),
            (ProjectStatus.UnderWarranty, ProjectStatus.Closed),

            (ProjectStatus.Setup, ProjectStatus.Stopped),
            (ProjectStatus.Active, ProjectStatus.Stopped),
            (ProjectStatus.HandoverPending, ProjectStatus.Stopped),

            // 🟡 spec.md §13 names Stopped but does not say how a project leaves it.
            // Resuming to Active is the only reading that does not strand the project.
            // See decisions.md D-015.
            (ProjectStatus.Stopped, ProjectStatus.Active),

            (ProjectStatus.Setup, ProjectStatus.Terminated),
            (ProjectStatus.Active, ProjectStatus.Terminated),
            (ProjectStatus.HandoverPending, ProjectStatus.Terminated),
            (ProjectStatus.Stopped, ProjectStatus.Terminated),
        }.ToFrozenSet();

    // spec.md §11: "Design closure differs: final documents delivered, last 10% collected, IP
    // transfers. No snag list, no handover, no hold." A design project therefore never enters
    // HandoverPending, Handover or UnderWarranty.
    private static readonly FrozenSet<(ProjectStatus From, ProjectStatus To)> DesignTransitions =
        new HashSet<(ProjectStatus, ProjectStatus)>
        {
            (ProjectStatus.Setup, ProjectStatus.Active),
            (ProjectStatus.Active, ProjectStatus.Closed),

            (ProjectStatus.Setup, ProjectStatus.Stopped),
            (ProjectStatus.Active, ProjectStatus.Stopped),
            (ProjectStatus.Stopped, ProjectStatus.Active),

            (ProjectStatus.Setup, ProjectStatus.Terminated),
            (ProjectStatus.Active, ProjectStatus.Terminated),
            (ProjectStatus.Stopped, ProjectStatus.Terminated),
        }.ToFrozenSet();

    private Project()
    {
    }

    private Project(
        Guid id,
        string code,
        string name,
        Guid clientId,
        ContractType contractType,
        Currency currency,
        Guid? opportunityId,
        DateTimeOffset createdAt)
        : base(id)
    {
        Code = code;
        Name = name;
        ClientId = clientId;
        ContractType = contractType;
        Currency = currency;
        OpportunityId = opportunityId;
        Status = ProjectStatus.Setup;
        CreatedAt = createdAt;
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public Guid ClientId { get; private set; }

    /// <summary>The opportunity this came from, when it came from one (spec.md §3).</summary>
    public Guid? OpportunityId { get; private set; }

    /// <summary>Immutable. spec.md §5.4: "A project MUST NOT mutate from one type into another."</summary>
    public ContractType ContractType { get; private set; }

    public ProjectStatus Status { get; private set; }

    public Currency Currency { get; private set; }

    /// <summary>
    /// Signed contract value. Null for Cost Plus, which has no fixed value (spec.md §5.2), and for a
    /// Design project until area and rate are set.
    /// </summary>
    public Money? ContractValue { get; private set; }

    // ---- Lump Sum terms (spec.md §5.1). Null on other types. ----

    public Percentage? AdvanceRate { get; private set; }

    public Percentage? HoldRate { get; private set; }

    public Percentage? AdvanceRecoveryRate { get; private set; }

    public Percentage? MaterialAdvanceRate { get; private set; }

    /// <summary>spec.md §5.1: optional, off by default 🟡.</summary>
    public bool DelayPenaltyEnabled { get; private set; }

    /// <summary>
    /// Withholding at source for this contract — 1% contracting and supplies, 3% services, 5%
    /// professional fees (spec.md §6.7).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>On the contract, not the client.</b> Karim, 2026-08-21: "The same client (e.g. a government
    /// body) might sign a design contract (one rate) and an execution contract (another rate).
    /// Storing it on the client profile breaks this reality." It lived on <c>Client</c> until then,
    /// which spec.md §6.7 appeared to require — but §6.7 sets the rate by *what is supplied* and §5.4
    /// explicitly links a design project to its execution project for one client, so one value per
    /// client could never have been right. See decisions.md D-049.
    /// </para>
    /// <para>
    /// Set by Finance, never by Marketing (Karim, same ruling): the rate dictates ledger entries and
    /// how much cash a collection is expected to carry, so it is an accounting parameter.
    /// </para>
    /// </remarks>
    public WithholdingCategory WithholdingCategory { get; private set; }

    // ---- Cost Plus terms (spec.md §5.2). Null on other types. ----

    public Percentage? SupervisionRate { get; private set; }

    // ---- Design terms (spec.md §5.3). Null on other types. ----

    public decimal? AreaSquareMetres { get; private set; }

    public Money? DesignRatePerSquareMetre { get; private set; }

    // ---- Dates ----

    public DateOnly? SignedOn { get; private set; }

    public DateOnly? StartedOn { get; private set; }

    public DateOnly? HandoverOn { get; private set; }

    /// <summary>Four months after handover. spec.md §11: "Warranty starts automatically on the handover date."</summary>
    public DateOnly? WarrantyEndsOn { get; private set; }

    public DateOnly? ClosedOn { get; private set; }

    public DateOnly? StoppedOn { get; private set; }

    /// <summary>spec.md §8: a stopped project records the stoppage and its reason.</summary>
    public string? StoppageReason { get; private set; }

    public DateOnly? TerminatedOn { get; private set; }

    public string? TerminationReason { get; private set; }

    // ---- Linking (spec.md §5.4) ----

    public Guid? LinkedProjectId { get; private set; }

    public ProjectLinkType? LinkType { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsSigned => SignedOn is not null;

    /// <summary>spec.md §7: "A stopped project MUST NOT issue extracts."</summary>
    public bool CanIssueExtracts => Status is ProjectStatus.Active or ProjectStatus.HandoverPending;

    public static Result<Project> Create(
        string code,
        string name,
        Guid clientId,
        ContractType contractType,
        DateTimeOffset createdAt,
        Currency currency = Currency.Egp,
        Guid? opportunityId = null)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength)
        {
            return Result.Failure<Project>(ProjectErrors.CodeRequired);
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            return Result.Failure<Project>(ProjectErrors.NameRequired);
        }

        return Result.Success(new Project(
            NewId(),
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            clientId,
            contractType,
            currency,
            opportunityId,
            createdAt));
    }

    /// <summary>Sets the Lump Sum commercial terms of spec.md §5.1. Owner-adjustable per project (🟡 assumption 1).</summary>
    public Result SetLumpSumTerms(
        Money contractValue,
        Percentage advanceRate,
        Percentage holdRate,
        Percentage advanceRecoveryRate,
        Percentage materialAdvanceRate,
        bool delayPenaltyEnabled)
    {
        if (ContractType != ContractType.LumpSum)
        {
            return Result.Failure(ProjectErrors.TermsDoNotMatchContractType);
        }

        if (IsSigned)
        {
            return Result.Failure(ProjectErrors.AlreadySigned);
        }

        if (!contractValue.IsPositive)
        {
            return Result.Failure(ProjectErrors.ContractValueRequired);
        }

        ContractValue = contractValue;
        AdvanceRate = advanceRate;
        HoldRate = holdRate;
        AdvanceRecoveryRate = advanceRecoveryRate;
        MaterialAdvanceRate = materialAdvanceRate;
        DelayPenaltyEnabled = delayPenaltyEnabled;
        return Result.Success();
    }

    /// <summary>
    /// Sets the withholding category for this contract (spec.md §6.7).
    /// </summary>
    /// <param name="category">The supply type, which fixes the rate at 1%, 3% or 5%.</param>
    /// <param name="clientKind">
    /// The kind of the client this project belongs to. Passed in rather than looked up because the
    /// domain holds only <see cref="ClientId"/> — and the rule cannot be left to the caller, since
    /// spec.md §6.7's whole justification is that a wrong flag makes collections irreconcilable:
    /// "collections will never match issued extracts and staff will invent adjustments to close the
    /// gap."
    /// </param>
    /// <remarks>
    /// Deliberately not gated on <see cref="IsSigned"/>, unlike the commercial terms above. Karim,
    /// 2026-08-21, put this in Finance's hands "during contract creation/approval", and approval
    /// comes after signature in more than one flow. 🟡 Whether it may change after the first extract
    /// has been issued is a slice 5 question nobody has asked.
    /// </remarks>
    public Result SetWithholding(WithholdingCategory category, ClientKind clientKind)
    {
        // spec.md §6.7: "Individual clients do not withhold."
        if (category != WithholdingCategory.None && clientKind == ClientKind.Individual)
        {
            return Result.Failure(MasterDataErrors.IndividualDoesNotWithhold);
        }

        WithholdingCategory = category;
        return Result.Success();
    }

    /// <summary>Sets the Cost Plus supervision rate of spec.md §5.2.</summary>
    public Result SetCostPlusTerms(Percentage supervisionRate)
    {
        if (ContractType != ContractType.CostPlus)
        {
            return Result.Failure(ProjectErrors.TermsDoNotMatchContractType);
        }

        if (IsSigned)
        {
            return Result.Failure(ProjectErrors.AlreadySigned);
        }

        SupervisionRate = supervisionRate;
        return Result.Success();
    }

    /// <summary>
    /// Sets the Design terms of spec.md §5.3: <c>fee = area × rate per m²</c>. There is no lump-sum
    /// option for a Design contract.
    /// </summary>
    public Result SetDesignTerms(decimal areaSquareMetres, Money ratePerSquareMetre)
    {
        if (ContractType != ContractType.Design)
        {
            return Result.Failure(ProjectErrors.TermsDoNotMatchContractType);
        }

        if (IsSigned)
        {
            return Result.Failure(ProjectErrors.AlreadySigned);
        }

        if (areaSquareMetres <= 0m)
        {
            return Result.Failure(ProjectErrors.AreaRequired);
        }

        AreaSquareMetres = areaSquareMetres;
        DesignRatePerSquareMetre = ratePerSquareMetre;
        ContractValue = ratePerSquareMetre * areaSquareMetres;
        return Result.Success();
    }

    public Result Sign(DateOnly signedOn)
    {
        if (IsSigned)
        {
            return Result.Failure(ProjectErrors.AlreadySigned);
        }

        if (ContractType != ContractType.CostPlus && ContractValue is null)
        {
            return Result.Failure(ProjectErrors.ContractValueRequired);
        }

        SignedOn = signedOn;
        return Result.Success();
    }

    /// <summary>spec.md §5.4. Two link semantics, both keeping separate accounts and billing.</summary>
    public Result LinkTo(Guid otherProjectId, ProjectLinkType linkType)
    {
        if (otherProjectId == Id)
        {
            return Result.Failure(ProjectErrors.ProjectCannotLinkToItself);
        }

        LinkedProjectId = otherProjectId;
        LinkType = linkType;
        return Result.Success();
    }

    /// <summary>
    /// Moves the project through the spec.md §13 state machine. Illegal transitions are refused;
    /// the legal set differs for Design because spec.md §11 gives it no handover and no warranty.
    /// </summary>
    public Result TransitionTo(ProjectStatus target, DateOnly occurredOn, string? reason = null)
    {
        FrozenSet<(ProjectStatus From, ProjectStatus To)> allowed =
            ContractType == ContractType.Design ? DesignTransitions : ExecutionTransitions;

        if (!allowed.Contains((Status, target)))
        {
            return Result.Failure(ProjectErrors.IllegalTransition);
        }

        if ((target is ProjectStatus.Stopped or ProjectStatus.Terminated) && string.IsNullOrWhiteSpace(reason))
        {
            // spec.md §8 requires the stoppage reason; §13 makes termination a settlement event.
            return Result.Failure(ProjectErrors.ReasonRequired);
        }

        Status = target;

        switch (target)
        {
            case ProjectStatus.Active:
                StartedOn ??= occurredOn;
                StoppedOn = null;
                StoppageReason = null;
                break;

            case ProjectStatus.Handover:
                HandoverOn = occurredOn;
                // spec.md §11: warranty starts automatically on the handover date, four months long.
                WarrantyEndsOn = occurredOn.AddMonths(ContractDefaults.WarrantyMonths);
                break;

            case ProjectStatus.Closed:
                ClosedOn = occurredOn;
                break;

            case ProjectStatus.Stopped:
                StoppedOn = occurredOn;
                StoppageReason = reason!.Trim();
                break;

            case ProjectStatus.Terminated:
                TerminatedOn = occurredOn;
                TerminationReason = reason!.Trim();
                break;

            case ProjectStatus.Setup:
            case ProjectStatus.HandoverPending:
            case ProjectStatus.UnderWarranty:
            default:
                break;
        }

        return Result.Success();
    }
}
