using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.Tests;

/// <summary>
/// The rulings module (<c>Kaff.Domain.Rulings</c>): every business rule the project has not had ruled,
/// in one place. These tests pin what each default does, so that when Karim rules differently the failing
/// tests are exactly what moved.
/// </summary>
public sealed class RulingsTests
{
    private readonly RulingSet _rulings = RulingSet.Default;

    // ---- the registry ---------------------------------------------------------------------------

    [Fact]
    public void Every_entry_names_its_question_the_default_it_runs_on_and_what_changes_if_ruled_differently()
    {
        _rulings.Notes.Should().HaveCountGreaterThanOrEqualTo(6);

        foreach (RulingNote note in _rulings.Notes)
        {
            note.Name.Should().NotBeNullOrWhiteSpace();
            note.Question.Should().NotBeNullOrWhiteSpace($"{note.Name} must say which question it answers");
            note.Rule.Should().NotBeNullOrWhiteSpace();
            note.RunningOn.Should().NotBeNullOrWhiteSpace($"{note.Name} must state its default");
            note.IfRuledDifferently.Should().NotBeNullOrWhiteSpace($"{note.Name} must say what a different ruling changes");
        }
    }

    [Fact]
    public void Every_entry_is_assumed_until_the_business_has_ruled_and_the_seeded_names_are_present()
    {
        // Ruled by Nabil in D-217: the daily-log entry window and crew hours, and that a second quantity adds.
        string[] ruled = ["DailyLogEntryRule", "DailyLogQuantityEntry"];

        _rulings.Notes.Where(note => !ruled.Contains(note.Name)).Should().OnlyContain(note => note.Status == RulingStatus.Assumed);
        _rulings.Notes.Where(note => ruled.Contains(note.Name)).Should().OnlyContain(note => note.Status == RulingStatus.Ruled);

        _rulings.Notes.Select(note => note.Name).Should().Contain(
        [
            "MaterialAdvanceRecoveryRule",
            "ExtractPostingMoment",
            "WithholdingBaseRule",
            "DeductionRounding",
            "ContractValueSource",
            "PostingProjectStatePolicy",
        ]);
    }

    [Fact]
    public void The_Q58_rule_says_in_so_many_words_that_it_is_an_assumption()
    {
        RulingNote note = _rulings.MaterialAdvanceRecovery.Note;

        note.Question.Should().Contain("Q58");
        note.RunningOn.Should().Contain("ASSUMED");
        note.Status.Should().Be(RulingStatus.Assumed);
    }

    // ---- MaterialAdvanceRecoveryRule (Q58) ------------------------------------------------------

    [Fact]
    public void Q58_default_reproduces_section_15_extract_2_and_3_recoveries_exactly()
    {
        Money outstanding = new(75_000m);

        Money second = _rulings.MaterialAdvanceRecovery.RecoveryFor(new Money(60_000m), outstanding);
        second.Should().Be(new Money(45_000m));

        Money third = _rulings.MaterialAdvanceRecovery.RecoveryFor(new Money(40_000m), outstanding - second);
        third.Should().Be(new Money(30_000m));

        (outstanding - second - third).Should().Be(Money.Zero, "spec.md section 15: تشوينات in equals تشوينات recovered");
    }

    [Fact]
    public void Q58_default_recovers_nothing_in_an_extract_where_nothing_was_installed()
    {
        _rulings.MaterialAdvanceRecovery.RecoveryFor(Money.Zero, new Money(75_000m)).Should().Be(Money.Zero);
    }

    [Fact]
    public void The_recovery_never_exceeds_what_is_outstanding()
    {
        _rulings.MaterialAdvanceRecovery.RecoveryFor(new Money(1_000_000m), new Money(10_000m))
            .Should().Be(new Money(10_000m));
        _rulings.MaterialAdvanceRecovery.RecoveryFor(new Money(60_000m), Money.Zero).Should().Be(Money.Zero);
    }

    [Fact]
    public void Ruling_Q58_differently_is_one_replacement_and_the_figures_follow()
    {
        RulingSet other = _rulings with
        {
            MaterialAdvanceRecovery = new PercentOfInstalledValueRecovery(Percentage.FromPercent(50m)),
        };

        other.MaterialAdvanceRecovery.RecoveryFor(new Money(60_000m), new Money(75_000m)).Should().Be(new Money(30_000m));
    }

    // ---- ExtractPostingMoment ---------------------------------------------------------------------

    [Fact]
    public void Extracts_accrue_at_owner_approval_by_default()
    {
        _rulings.ExtractPostingMoment.AccruesAt.Should().Be(ExtractStage.OwnerApproval);
        _rulings.ExtractPostingMoment.AccruesOn(ExtractStage.OwnerApproval).Should().BeTrue();
        _rulings.ExtractPostingMoment.AccruesOn(ExtractStage.TechnicalOfficeApproval).Should().BeFalse();
        _rulings.ExtractPostingMoment.AccruesOn(ExtractStage.Issued).Should().BeFalse();
    }

    [Theory]
    [InlineData(ExtractStage.TechnicalOfficeApproval, false)]
    [InlineData(ExtractStage.AccountsDeductions, false)]
    [InlineData(ExtractStage.OwnerApproval, true)]
    [InlineData(ExtractStage.Issued, true)]
    [InlineData(ExtractStage.Collected, true)]
    public void A_rejection_needs_reversing_postings_only_from_a_stage_that_has_already_accrued(
        ExtractStage stage, bool postingsExist)
    {
        _rulings.ExtractPostingMoment.HasAccruedBy(stage).Should().Be(postingsExist);
    }

    [Fact]
    public void Ruling_the_moment_differently_moves_every_dependent_answer_with_it()
    {
        ExtractPostingMoment atIssue = new(ExtractStage.Issued);

        atIssue.AccruesOn(ExtractStage.OwnerApproval).Should().BeFalse();
        atIssue.AccruesOn(ExtractStage.Issued).Should().BeTrue();
        atIssue.HasAccruedBy(ExtractStage.OwnerApproval).Should().BeFalse();
    }

    // ---- WithholdingBase ----------------------------------------------------------------------------

    [Fact]
    public void Withholding_defaults_to_net_payable_and_carries_on_the_hold_release()
    {
        _rulings.WithholdingBase.Kind.Should().Be(WithholdingBaseKind.NetPayable);
        _rulings.WithholdingBase.HoldReleaseCarriesWithholding.Should().BeTrue();
    }

    [Theory]
    [InlineData(WithholdingBaseKind.NetPayable, 120_000)]
    [InlineData(WithholdingBaseKind.PeriodWorkValue, 300_000)]
    [InlineData(WithholdingBaseKind.CertifiedLessHold, 240_000)]
    public void Each_withholding_base_reads_its_own_figure_from_section_15_extract_2(
        WithholdingBaseKind kind, int expected)
    {
        var figures = new ExtractFigures(new Money(300_000m), new Money(60_000m), new Money(120_000m));

        new WithholdingBaseRule(kind, HoldReleaseCarriesWithholding: false).BaseFor(figures)
            .Should().Be(new Money(expected));
    }

    // ---- DeductionRounding -------------------------------------------------------------------------

    [Fact]
    public void Deductions_round_to_two_decimals_away_from_zero()
    {
        // 20% of 100.125 = 20.025, which rounds up to 20.03 away from zero (banker's rounding gives 20.02).
        _rulings.DeductionRounding.Deduct(new Money(100.125m), Percentage.FromPercent(20m))
            .Should().Be(new Money(20.03m));
    }

    [Fact]
    public void The_visible_column_foots_because_net_payable_is_taken_from_the_rounded_figures()
    {
        Money total = new(1_000.01m);
        Money hold = _rulings.DeductionRounding.Deduct(total, Percentage.FromPercent(20m));
        Money advance = _rulings.DeductionRounding.Deduct(total, Percentage.FromPercent(25m));

        Money net = _rulings.DeductionRounding.NetPayable(total, [hold, advance]);

        (total - hold - advance).Should().Be(net, "the printed total less the printed deductions is the printed net");
    }

    [Fact]
    public void Section_15_extract_2_through_the_rounding_ruling_is_120000()
    {
        DeductionRounding rounding = _rulings.DeductionRounding;
        Money work = new(300_000m);

        Money hold = rounding.Deduct(work, ContractDefaults.HoldRate);
        Money advance = rounding.Deduct(work, ContractDefaults.AdvanceRecoveryRate);
        Money tashwinat = _rulings.MaterialAdvanceRecovery.RecoveryFor(new Money(60_000m), new Money(75_000m));

        rounding.NetPayable(work, [hold, advance, tashwinat]).Should().Be(new Money(120_000m));
    }

    // ---- ContractValueSource ----------------------------------------------------------------------

    [Theory]
    [InlineData(ContractType.LumpSum, ContractValueOrigin.BoqTotal)]
    [InlineData(ContractType.CostPlus, ContractValueOrigin.None)]
    [InlineData(ContractType.Design, ContractValueOrigin.AreaTimesRate)]
    public void Each_contract_type_derives_its_value_from_its_own_source(ContractType type, ContractValueOrigin origin)
    {
        ContractValueSource.OriginFor(type).Should().Be(origin);
    }

    [Fact]
    public void Only_lump_sum_needs_a_boq_to_sign_and_design_may_not_have_one()
    {
        ContractValueSource.RequiresBoq(ContractType.LumpSum).Should().BeTrue();
        ContractValueSource.RequiresBoq(ContractType.CostPlus).Should().BeFalse();
        ContractValueSource.RequiresBoq(ContractType.Design).Should().BeFalse();

        ContractValueSource.AllowsBoq(ContractType.Design).Should().BeFalse();
        ContractValueSource.AllowsBoq(ContractType.LumpSum).Should().BeTrue();
        ContractValueSource.AllowsBoq(ContractType.CostPlus).Should().BeTrue();
    }

    [Fact]
    public void A_boq_line_is_priced_on_the_surveyed_quantity_and_rounded_to_two_decimals()
    {
        _rulings.ContractValueSource.QuantityBasis.Should().Be(BoqQuantityBasis.Surveyed);
        _rulings.ContractValueSource.LineTotal(new Money(10.005m), 3m).Should().Be(new Money(30.02m));
    }
}
