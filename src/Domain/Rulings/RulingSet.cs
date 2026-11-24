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

    /// <summary>The window an entry may be dated in and the crew-hours limits (D-217).</summary>
    public DailyLogEntryRule DailyLogEntry { get; init; } = DailyLogEntryRule.Default;

    public SiteExpenseDraftAmount SiteExpenseDraftAmount { get; init; } = SiteExpenseDraftAmount.Default;

    /// <summary>What a partial Technical Office approval scales and how its held remainder is billed. Registered, not resolved.</summary>
    public ExtractPartialApprovalScope ExtractPartialApprovalScope { get; init; } = ExtractPartialApprovalScope.Default;

    /// <summary>How the next extract's evidence is assembled. Registered, not resolved.</summary>
    public ExtractPreparationScope ExtractPreparationScope { get; init; } = ExtractPreparationScope.Default;

    /// <summary>Who submits an extract's weekly QC report. Enforced in the permission catalogue.</summary>
    public ExtractQcReportAuthor ExtractQcReportAuthor { get; init; } = ExtractQcReportAuthor.Default;

    /// <summary>Who closes an NCR and so releases a split gate's held portion (D-250 item 1).</summary>
    public NcrCloser NcrCloser { get; init; } = NcrCloser.Default;

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

    /// <summary>Who declares, hands over, releases the hold, raises snags and callbacks, and closes (Q109).</summary>
    public ClosureActors ClosureActors { get; init; } = ClosureActors.Default;

    /// <summary>What practical completion requires before it may be declared. Registered, not resolved.</summary>
    public PracticalCompletionPrerequisites PracticalCompletionPrerequisites { get; init; } = PracticalCompletionPrerequisites.Default;

    /// <summary>Which snags block handover and whether a failed fix reopens (Q115).</summary>
    public SnagHandoverBlock SnagHandoverBlock { get; init; } = SnagHandoverBlock.Default;

    /// <summary>When the hold releases, what blocks it, and whether it can be undone (Q110, Q111).</summary>
    public HoldReleaseGate HoldReleaseGate { get; init; } = HoldReleaseGate.Default;

    /// <summary>What moves Handover to UnderWarranty (Q110).</summary>
    public WarrantyStart WarrantyStart { get; init; } = WarrantyStart.Default;

    /// <summary>When a callback is accepted and what it books (Q116, Q112).</summary>
    public WarrantyCallbackWindow WarrantyCallbackWindow { get; init; } = WarrantyCallbackWindow.Default;

    /// <summary>What resolving a snag books (Q112).</summary>
    public SnagResolutionCost SnagResolutionCost { get; init; } = SnagResolutionCost.Default;

    /// <summary>When subcontractor retention releases (Q26). Nothing is built.</summary>
    public SubcontractorRetentionTiming SubcontractorRetentionTiming { get; init; } = SubcontractorRetentionTiming.Default;

    /// <summary>What closure requires (Q113, Q116).</summary>
    public ClosureConditions ClosureConditions { get; init; } = ClosureConditions.Default;

    /// <summary>What a Closed project forbids that an Active one allowed.</summary>
    public ClosedStateEffect ClosedStateEffect { get; init; } = ClosedStateEffect.Default;

    /// <summary>How a terminated project ends (Q108). The transition refuses.</summary>
    public TerminationSettlement TerminationSettlement { get; init; } = TerminationSettlement.Default;

    /// <summary>Who raises a dispute and against which extract states (Q106).</summary>
    public ExtractDisputeActors ExtractDisputeActors { get; init; } = ExtractDisputeActors.Default;

    /// <summary>Whether a disputed extract's postings stand while it is open (Q106).</summary>
    public ExtractDisputeEffect ExtractDisputeEffect { get; init; } = ExtractDisputeEffect.Default;

    /// <summary>How a dispute ends: credit note built, revised extract refused (Q106).</summary>
    public ExtractDisputeResolution ExtractDisputeResolution { get; init; } = ExtractDisputeResolution.Default;

    /// <summary>The shape of the one Adjustment object and what it may not touch (Q107, Q108).</summary>
    public AdjustmentShape AdjustmentShape { get; init; } = AdjustmentShape.Default;

    /// <summary>Who raises, approves and reverses an adjustment (Q107).</summary>
    public AdjustmentApproval AdjustmentApproval { get; init; } = AdjustmentApproval.Default;

    /// <summary>Where subcontractor retention sits and how it releases (Q26, Q109).</summary>
    public SubcontractorRetentionRelease SubcontractorRetentionRelease { get; init; } = SubcontractorRetentionRelease.Default;

    /// <summary>What the Lump Sum progress bar is measured from. Registered, not resolved.</summary>
    public LumpSumCompletionBasis LumpSumCompletionBasis { get; init; } = LumpSumCompletionBasis.Default;

    /// <summary>What makes a باب active. Registered, not resolved.</summary>
    public BabActivity BabActivity { get; init; } = BabActivity.Default;

    /// <summary>What counts as Cost Plus cost-to-date. Registered, not resolved.</summary>
    public CostPlusCostBasis CostPlusCostBasis { get; init; } = CostPlusCostBasis.Default;

    /// <summary>Where a target completion date would come from. Registered, not resolved.</summary>
    public ProjectTargetCompletion ProjectTargetCompletion { get; init; } = ProjectTargetCompletion.Default;

    /// <summary>How long an unsigned project is followed up before it is archived (Q-208-1, Q-208-2).</summary>
    public FollowUpPeriod FollowUp { get; init; } = FollowUpPeriod.Default;

    /// <summary>How far from the reference point a check-in may be before it is flagged (Q-207-3, Q-207-3a).</summary>
    public CheckInDistanceRule CheckInDistance { get; init; } = CheckInDistanceRule.Default;

    /// <summary>KAFF-415: what a design project hands its execution project (Q-207-5).</summary>
    public DesignQuantitySeed DesignQuantitySeed { get; init; } = DesignQuantitySeed.Default;

    /// <summary>KAFF-415: what the 30% design credit is taken of (Q-207-6).</summary>
    public DesignCreditBase DesignCreditBase { get; init; } = DesignCreditBase.Default;

    /// <summary>KAFF-415: one execution project per design project (Q-207-7).</summary>
    public DesignConversionCount DesignConversionCount { get; init; } = DesignConversionCount.Default;

    /// <summary>KAFF-415: the design credit posts on approval by a different user (Q-207-8).</summary>
    public DesignCreditApproval DesignCreditApproval { get; init; } = DesignCreditApproval.Default;

    /// <summary>KAFF-415: the execution types a design project may convert into (Q-207-9).</summary>
    public DesignConversionTargets DesignConversionTargets { get; init; } = DesignConversionTargets.Default;

    /// <summary>Every entry, each describing itself. A test pins that none is missing a question.</summary>
    public FinanceStaffing FinanceStaffing { get; init; } = FinanceStaffing.Default;

    public SupervisorSeesProjectMoney SupervisorSeesProjectMoney { get; init; } = SupervisorSeesProjectMoney.Default;

    public PortalDeployment PortalDeployment { get; init; } = PortalDeployment.Default;

    public ClientCredentialHandover ClientCredentialHandover { get; init; } = ClientCredentialHandover.Default;

    public PortalExtractVisibility PortalExtractVisibility { get; init; } = PortalExtractVisibility.Default;

    public PhotoPublishers PhotoPublishers { get; init; } = PhotoPublishers.Default;

    public ClientDecidesChangeOrders ClientDecidesChangeOrders { get; init; } = ClientDecidesChangeOrders.Default;

    /// <summary>R3-03: the quiet-day counts of the sales pipeline (R3-Q5).</summary>
    public OpportunityInactivity OpportunityInactivity { get; init; } = OpportunityInactivity.Default;

    /// <summary>R2-Q7 item 4: a payment the safe refuses is repeated by the user after the injection. Registered, not resolved.</summary>
    public RefusedPaymentAfterInjection RefusedPaymentAfterInjection { get; init; } = RefusedPaymentAfterInjection.Default;

    /// <summary>R2-Q10: the weekly QC report belongs to the client extract chain only. Registered, not resolved.</summary>
    public QcReportScope QcReportScope { get; init; } = QcReportScope.Default;

    /// <summary>R2-Q8: the supervising engineer enters the stockpiled value at the QC report. Registered, not resolved.</summary>
    public StockpileMarker StockpileMarker { get; init; } = StockpileMarker.Default;

    /// <summary>R4-Q7: how an asset depreciates and from which month. Honoured by the depreciation run.</summary>
    public AssetDepreciationRule AssetDepreciation { get; init; } = AssetDepreciationRule.Default;

    /// <summary>R4-Q6: the tolerance a budget baseline takes when the Owner names none. A placeholder.</summary>
    public BudgetTolerance BudgetTolerance { get; init; } = BudgetTolerance.Default;

    /// <summary>R4-Q10: the trial balance export format. Registered, not resolved.</summary>
    public TrialBalanceExport TrialBalanceExport { get; init; } = TrialBalanceExport.Default;

    /// <summary>R4-Q9: which projects take a share of the overhead spread, and by what size.</summary>
    public OverheadSpreadBasis OverheadSpreadBasis { get; init; } = OverheadSpreadBasis.Default;

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
        DailyLogEntry.Note,
        SiteExpenseDraftAmount.Note,
        ExtractPartialApprovalScope.Note,
        ExtractPreparationScope.Note,
        ExtractQcReportAuthor.Note,
        NcrCloser.Note,
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
        ClosureActors.Note,
        PracticalCompletionPrerequisites.Note,
        SnagHandoverBlock.Note,
        HoldReleaseGate.Note,
        WarrantyStart.Note,
        WarrantyCallbackWindow.Note,
        SnagResolutionCost.Note,
        SubcontractorRetentionTiming.Note,
        ClosureConditions.Note,
        ClosedStateEffect.Note,
        TerminationSettlement.Note,
        ExtractDisputeActors.Note,
        ExtractDisputeEffect.Note,
        ExtractDisputeResolution.Note,
        AdjustmentShape.Note,
        AdjustmentApproval.Note,
        SubcontractorRetentionRelease.Note,
        LumpSumCompletionBasis.Note,
        BabActivity.Note,
        CostPlusCostBasis.Note,
        ProjectTargetCompletion.Note,
        FollowUp.Note,
        CheckInDistance.Note,
        DesignQuantitySeed.Note,
        DesignCreditBase.Note,
        DesignConversionCount.Note,
        DesignCreditApproval.Note,
        DesignConversionTargets.Note,
        FinanceStaffing.Note,
        SupervisorSeesProjectMoney.Note,
        PortalDeployment.Note,
        ClientCredentialHandover.Note,
        PortalExtractVisibility.Note,
        PhotoPublishers.Note,
        ClientDecidesChangeOrders.Note,
        OpportunityInactivity.Note,
        RefusedPaymentAfterInjection.Note,
        QcReportScope.Note,
        StockpileMarker.Note,
        AssetDepreciation.Note,
        BudgetTolerance.Note,
        TrialBalanceExport.Note,
        OverheadSpreadBasis.Note,
    ];
}
