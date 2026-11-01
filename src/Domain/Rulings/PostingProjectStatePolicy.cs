using System.Collections.Frozen;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Rulings;

/// <summary>
/// Which posting types a project in each state may receive. Enforced by
/// <see cref="PostingProjectStateLegality"/>, which sits beside <see cref="PostingAccountLegality"/>.
/// </summary>
/// <remarks>
/// Nothing in spec.md says whether Finance may post to a Stopped, Closed or Terminated project; the
/// project journey study put it to Nabil as decision 6. spec.md fixes only two cells: a stopped project
/// cannot issue extracts (§7) and the hold releases at handover (§5.1). Every other cell is ASSUMED.
/// Raw SQL can still bypass this table; that is accepted (the study's own answer).
/// </remarks>
public sealed record PostingProjectStatePolicy : IRuling
{
    /// <summary>
    /// Postings an extract produces. Billing: not allowed on a project that cannot issue extracts
    /// (Setup, Stopped, Handover onwards).
    /// </summary>
    private static readonly FrozenSet<PostingType> ExtractDerived = new[]
    {
        PostingType.RevenueRecognition,
        PostingType.HoldAccrual,
        PostingType.MaterialAdvanceIssue,
        PostingType.MaterialAdvanceRecovery,
        PostingType.ClientAdvanceRecovery,
        PostingType.TaxWithheldAtSource,
        PostingType.TaxWithholdingRetained,
        PostingType.SubcontractorRetentionAccrual,
    }.ToFrozenSet();

    private static readonly FrozenSet<PostingType> HoldRelease = new[] { PostingType.HoldRelease }.ToFrozenSet();

    private static readonly FrozenSet<PostingType> RetentionRelease =
        new[] { PostingType.SubcontractorRetentionRelease }.ToFrozenSet();

    /// <summary>What is still collectable or correctable once work has stopped being billed.</summary>
    private static readonly FrozenSet<PostingType> Collections = new[]
    {
        PostingType.ClientCollection,
        PostingType.ChequeDeposit,
        PostingType.ChequeClearance,
        PostingType.ChequeBounce,
        PostingType.Adjustment,
    }.ToFrozenSet();

    private readonly FrozenDictionary<ProjectStatus, FrozenSet<PostingType>> _allowed;

    public PostingProjectStatePolicy(
        IReadOnlyDictionary<ProjectStatus, FrozenSet<PostingType>> allowed,
        bool reversalsFollowTheTable)
    {
        ArgumentNullException.ThrowIfNull(allowed);

        _allowed = allowed.ToFrozenDictionary();
        ReversalsFollowTheTable = reversalsFollowTheTable;
    }

    /// <summary>
    /// False means a reversal is allowed in every state: it corrects a posting that was legal when made,
    /// it is not new activity. True applies the table to reversals exactly as to forward postings.
    /// </summary>
    public bool ReversalsFollowTheTable { get; }

    public static PostingProjectStatePolicy Default { get; } = BuildDefault();

    public RulingNote Note => new(
        "PostingProjectStatePolicy",
        "Journey study decision 6: may Finance post to a Stopped, Closed or Terminated project",
        "A per-posting-type allowed-status table, enforced in the domain.",
        "Active and HandoverPending accept everything except the two release types. Setup, Stopped, Handover "
        + "and UnderWarranty refuse extract-derived postings. HoldRelease only at Handover or UnderWarranty. "
        + "Closed accepts collections and adjustments. Terminated accepts collections, payments of what is owed, "
        + "credit and debit notes and adjustments. Reversals are exempt. ASSUMED except the stopped-project "
        + "extract ban (spec.md 7) and hold-at-handover (spec.md 5.1).",
        "Edit BuildDefault, one line per cell. Set reversalsFollowTheTable to true to make corrections obey the "
        + "same table.",
        RulingStatus.Assumed);

    public bool Allows(ProjectStatus status, PostingType type) =>
        _allowed.TryGetValue(status, out FrozenSet<PostingType>? allowed) && allowed.Contains(type);

    /// <summary>
    /// Whether a project in <paramref name="status"/> may issue extracts (spec.md §7). Derived from the
    /// table, so the answer and the postings the table accepts cannot disagree.
    /// </summary>
    public bool AllowsExtracts(ProjectStatus status) => Allows(status, PostingType.RevenueRecognition);

    private static PostingProjectStatePolicy BuildDefault()
    {
        FrozenSet<PostingType> all = PostingTypes.AllDefined.ToFrozenSet();

        FrozenSet<PostingType> Without(params FrozenSet<PostingType>[] groups) =>
            all.Where(type => !groups.Any(group => group.Contains(type))).ToFrozenSet();

        var table = new Dictionary<ProjectStatus, FrozenSet<PostingType>>
        {
            [ProjectStatus.Setup] = Without(ExtractDerived, HoldRelease, RetentionRelease),
            [ProjectStatus.Active] = Without(HoldRelease, RetentionRelease),
            [ProjectStatus.HandoverPending] = Without(HoldRelease, RetentionRelease),
            [ProjectStatus.Handover] = Without(ExtractDerived, RetentionRelease),
            [ProjectStatus.UnderWarranty] = Without(ExtractDerived),
            [ProjectStatus.Stopped] = Without(ExtractDerived, HoldRelease, RetentionRelease),
            [ProjectStatus.Closed] = Collections,
            [ProjectStatus.Terminated] = new[]
            {
                PostingType.ClientCollection,
                PostingType.ChequeDeposit,
                PostingType.ChequeClearance,
                PostingType.ChequeBounce,
                PostingType.SupplierPayment,
                PostingType.SubcontractorPayment,
                PostingType.DayLabourPayment,
                PostingType.PayrollPayment,
                PostingType.CreditNote,
                PostingType.DebitNote,
                PostingType.Adjustment,
            }.ToFrozenSet(),
        };

        return new PostingProjectStatePolicy(table, reversalsFollowTheTable: false);
    }
}
