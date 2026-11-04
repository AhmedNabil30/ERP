namespace Kaff.Domain.Rulings;

/// <summary>
/// Every business rule this project has not had ruled, in one place. Nothing else in the codebase may
/// hard-code one of these values: a handler asks the <see cref="RulingSet"/>.
/// </summary>
/// <remarks>
/// Registered as a singleton by the Api. When Karim rules, change <see cref="Default"/> (or one entry's
/// own <c>Default</c>) and run the tests; the ones that fail are exactly what moved. Tests that need a
/// different ruling build their own set with <c>with</c>.
/// </remarks>
public sealed record RulingSet
{
    public static RulingSet Default { get; } = new();

    public IMaterialAdvanceRecoveryRule MaterialAdvanceRecovery { get; init; } = PercentOfInstalledValueRecovery.Default;

    public ExtractPostingMoment ExtractPostingMoment { get; init; } = ExtractPostingMoment.Default;

    public WithholdingBaseRule WithholdingBase { get; init; } = WithholdingBaseRule.Default;

    public DeductionRounding DeductionRounding { get; init; } = DeductionRounding.Default;

    public ContractValueSource ContractValueSource { get; init; } = ContractValueSource.Default;

    public PostingProjectStatePolicy PostingProjectStates { get; init; } = PostingProjectStatePolicy.Default;

    public DailyLogQuantityEntry DailyLogQuantityEntry { get; init; } = DailyLogQuantityEntry.Default;

    public DailyLogProjectStates DailyLogProjectStates { get; init; } = DailyLogProjectStates.Default;

    public SiteExpenseDraftAmount SiteExpenseDraftAmount { get; init; } = SiteExpenseDraftAmount.Default;

    /// <summary>What a partial Technical Office approval scales and how its held remainder is billed. Registered, not resolved.</summary>
    public ExtractPartialApprovalScope ExtractPartialApprovalScope { get; init; } = ExtractPartialApprovalScope.Default;

    /// <summary>How the next extract's evidence is assembled. Registered, not resolved.</summary>
    public ExtractPreparationScope ExtractPreparationScope { get; init; } = ExtractPreparationScope.Default;

    /// <summary>Who submits an extract's weekly QC report. Enforced in the permission catalogue.</summary>
    public ExtractQcReportAuthor ExtractQcReportAuthor { get; init; } = ExtractQcReportAuthor.Default;

    /// <summary>What a preparer may change on an extract a rejection returned to Draft.</summary>
    public ExtractRevisionScope ExtractRevisionScope { get; init; } = ExtractRevisionScope.Default;

    /// <summary>Who records a collection (Q97). Registered, not resolved.</summary>
    public CollectionApproval CollectionApproval { get; init; } = CollectionApproval.Default;

    /// <summary>How collections settle extracts (Q98). Registered, not resolved.</summary>
    public CollectionSettlement CollectionSettlement { get; init; } = CollectionSettlement.Default;

    /// <summary>A payment that differs from the net (Q100). Registered, not resolved.</summary>
    public CollectionShortfall CollectionShortfall { get; init; } = CollectionShortfall.Default;

    /// <summary>What a bounced cheque does to a collected extract (Q101). Registered, not resolved.</summary>
    public ChequeBounceEffect ChequeBounceEffect { get; init; } = ChequeBounceEffect.Default;

    /// <summary>What a reversed collection does to the extract and to a cheque already moved (D-200).</summary>
    public CollectionReversalEffect CollectionReversalEffect { get; init; } = CollectionReversalEffect.Default;

    /// <summary>Which contract value a project read leads with once change orders are approved. Registered, not resolved.</summary>
    public ChangeOrderDisplayedValue ChangeOrderDisplayedValue { get; init; } = ChangeOrderDisplayedValue.Default;

    /// <summary>The project states a change order may be raised and approved in. Registered, not resolved.</summary>
    public ChangeOrderProjectStates ChangeOrderProjectStates { get; init; } = ChangeOrderProjectStates.Default;

    /// <summary>Whether an approved change order reprices issued extracts. Registered, not resolved.</summary>
    public ChangeOrderRepricing ChangeOrderRepricing { get; init; } = ChangeOrderRepricing.Default;

    /// <summary>Who creates, reviews and withdraws a change order, and records the client's approval (Q103, Q105).</summary>
    public ChangeOrderApprovers ChangeOrderApprovers { get; init; } = ChangeOrderApprovers.Default;

    /// <summary>How a change-order line is priced (Q104). Registered, not resolved.</summary>
    public ChangeOrderLinePricing ChangeOrderLinePricing { get; init; } = ChangeOrderLinePricing.Default;

    /// <summary>Whether hold and advance recovery apply to change-order value (Q102). Registered, not resolved.</summary>
    public ChangeOrderBillingTreatment ChangeOrderBillingTreatment { get; init; } = ChangeOrderBillingTreatment.Default;

    /// <summary>How much of an approved change order an extract bills. Registered, not resolved.</summary>
    public ChangeOrderBillingBasis ChangeOrderBillingBasis { get; init; } = ChangeOrderBillingBasis.Default;

    /// <summary>Where a rejected change order goes. Registered, not resolved.</summary>
    public ChangeOrderRejection ChangeOrderRejection { get; init; } = ChangeOrderRejection.Default;

    /// <summary>Every entry, each describing itself. A test pins that none is missing a question.</summary>
    public IReadOnlyList<RulingNote> Notes =>
    [
        MaterialAdvanceRecovery.Note,
        ExtractPostingMoment.Note,
        WithholdingBase.Note,
        DeductionRounding.Note,
        ContractValueSource.Note,
        PostingProjectStates.Note,
        DailyLogQuantityEntry.Note,
        DailyLogProjectStates.Note,
        SiteExpenseDraftAmount.Note,
        ExtractPartialApprovalScope.Note,
        ExtractPreparationScope.Note,
        ExtractQcReportAuthor.Note,
        ExtractRevisionScope.Note,
        CollectionApproval.Note,
        CollectionSettlement.Note,
        CollectionShortfall.Note,
        ChequeBounceEffect.Note,
        CollectionReversalEffect.Note,
        ChangeOrderDisplayedValue.Note,
        ChangeOrderProjectStates.Note,
        ChangeOrderRepricing.Note,
        ChangeOrderApprovers.Note,
        ChangeOrderLinePricing.Note,
        ChangeOrderBillingTreatment.Note,
        ChangeOrderBillingBasis.Note,
        ChangeOrderRejection.Note,
    ];
}
