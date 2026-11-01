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
    ];
}
