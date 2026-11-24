using Kaff.Domain.MasterData;
using Kaff.Domain.Rulings;
using Kaff.Domain.Sales;

namespace Kaff.Domain.Tests;

/// <summary>R3-02 and R3-03 and R3-04 — the opportunity pipeline rules of spec.md §3 and §13.</summary>
public sealed class OpportunityPipelineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly OpportunityInactivity Rule = OpportunityInactivity.Default;

    private static Opportunity Make() => Opportunity.Create("o-1", Guid.NewGuid(), "فيلا", Start).Value;

    [Fact]
    public void A_new_opportunity_is_a_Lead_and_Active()
    {
        Opportunity opportunity = Make();

        opportunity.Stage.Should().Be(OpportunityStage.Lead);
        opportunity.Status.Should().Be(OpportunityStatus.Active);
    }

    [Fact]
    public void The_stage_moves_forward_one_step_at_a_time_up_to_Negotiation_and_no_further()
    {
        Opportunity opportunity = Make();

        OpportunityStage[] expected =
        [
            OpportunityStage.Meeting,
            OpportunityStage.SiteVisit,
            OpportunityStage.Quotation,
            OpportunityStage.Negotiation,
        ];

        foreach (OpportunityStage next in expected)
        {
            opportunity.AdvanceStage(Start).IsSuccess.Should().BeTrue();
            opportunity.Stage.Should().Be(next);
        }

        opportunity.AdvanceStage(Start).Error.Should().Be(SalesErrors.AlreadyAtLastStage);
        opportunity.Stage.Should().Be(OpportunityStage.Negotiation, "Contract comes only from conversion to a project");
    }

    [Fact]
    public void A_row_already_saved_at_Contract_cannot_advance_past_it()
    {
        Opportunity opportunity = Make();
        typeof(Opportunity).GetProperty(nameof(Opportunity.Stage))!.SetValue(opportunity, OpportunityStage.Contract);

        opportunity.AdvanceStage(Start).Error.Should().Be(SalesErrors.AlreadyAtLastStage);
        opportunity.Stage.Should().Be(OpportunityStage.Contract);
    }

    [Fact]
    public void Hold_keeps_the_stage_and_resume_returns_to_it()
    {
        Opportunity opportunity = Make();
        opportunity.AdvanceStage(Start);
        opportunity.AdvanceStage(Start);

        opportunity.PutOnHold(Start).IsSuccess.Should().BeTrue();
        opportunity.Status.Should().Be(OpportunityStatus.OnHold);
        opportunity.Stage.Should().Be(OpportunityStage.SiteVisit);

        opportunity.AdvanceStage(Start).Error.Should().Be(SalesErrors.OpportunityNotLive, "a held opportunity does not move");
        opportunity.PutOnHold(Start).Error.Should().Be(SalesErrors.OpportunityNotLive);

        opportunity.Resume(Start.AddDays(3)).IsSuccess.Should().BeTrue();
        opportunity.Status.Should().Be(OpportunityStatus.Active);
        opportunity.Stage.Should().Be(OpportunityStage.SiteVisit);
        opportunity.LastActivityAt.Should().Be(Start.AddDays(3), "resuming restarts the quiet clock");
    }

    [Fact]
    public void Resume_is_refused_unless_the_opportunity_is_on_hold()
    {
        Make().Resume(Start).Error.Should().Be(SalesErrors.OpportunityNotOnHold);
    }

    [Theory]
    [InlineData(1, InactivityLevel.None)]
    [InlineData(2, InactivityLevel.FirstAlert)]
    [InlineData(3, InactivityLevel.FirstAlert)]
    [InlineData(4, InactivityLevel.SecondAlert)]
    [InlineData(6, InactivityLevel.SecondAlert)]
    [InlineData(7, InactivityLevel.Stalled)]
    [InlineData(30, InactivityLevel.Stalled)]
    public void Quiet_days_flag_on_day_2_and_day_4_and_stall_on_day_7(int quietDays, InactivityLevel expected)
    {
        Opportunity opportunity = Make();

        opportunity.InactivityAt(Start.AddDays(quietDays), Rule).Should().Be(expected);
        opportunity.StatusAt(Start.AddDays(quietDays), Rule).Should().Be(
            expected == InactivityLevel.Stalled ? OpportunityStatus.Stalled : OpportunityStatus.Active);
        opportunity.Status.Should().Be(OpportunityStatus.Active, "Stalled is computed when read, never stored");
    }

    [Fact]
    public void The_day_counts_are_settings()
    {
        Opportunity opportunity = Make();
        var rule = new OpportunityInactivity(1, 2, 3);

        opportunity.InactivityAt(Start.AddDays(1), rule).Should().Be(InactivityLevel.FirstAlert);
        opportunity.InactivityAt(Start.AddDays(3), rule).Should().Be(InactivityLevel.Stalled);
    }

    [Fact]
    public void Activity_revives_a_stalled_opportunity_at_the_same_stage()
    {
        Opportunity opportunity = Make();
        opportunity.AdvanceStage(Start);
        DateTimeOffset later = Start.AddDays(10);

        opportunity.StatusAt(later, Rule).Should().Be(OpportunityStatus.Stalled);

        opportunity.RecordActivity(later);

        opportunity.StatusAt(later, Rule).Should().Be(OpportunityStatus.Active);
        opportunity.Stage.Should().Be(OpportunityStage.Meeting);
    }

    [Fact]
    public void A_stage_move_resets_the_clock_and_so_revives_a_quiet_opportunity()
    {
        Opportunity opportunity = Make();
        DateTimeOffset later = Start.AddDays(10);

        opportunity.AdvanceStage(later).IsSuccess.Should().BeTrue();

        opportunity.InactivityAt(later, Rule).Should().Be(InactivityLevel.None);
    }

    [Fact]
    public void An_opportunity_on_hold_or_closed_never_flags_or_stalls()
    {
        Opportunity held = Make();
        held.PutOnHold(Start);
        held.InactivityAt(Start.AddDays(30), Rule).Should().Be(InactivityLevel.None);
        held.StatusAt(Start.AddDays(30), Rule).Should().Be(OpportunityStatus.OnHold);

        Opportunity lost = Make();
        lost.CloseLost("السعر", Start);
        lost.InactivityAt(Start.AddDays(30), Rule).Should().Be(InactivityLevel.None);
        lost.StatusAt(Start.AddDays(30), Rule).Should().Be(OpportunityStatus.ClosedLost);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Close_lost_is_refused_without_a_reason(string? reason)
    {
        Opportunity opportunity = Make();

        opportunity.CloseLost(reason!, Start).Error.Should().Be(MasterDataErrors.ClosedLostRequiresReason);
        opportunity.Status.Should().Be(OpportunityStatus.Active);
    }

    [Fact]
    public void Close_lost_stores_the_reason_and_reopen_returns_to_the_same_stage_and_client()
    {
        Opportunity opportunity = Make();
        Guid client = opportunity.ClientId;
        opportunity.AdvanceStage(Start);
        opportunity.AdvanceStage(Start);

        opportunity.CloseLost("  العميل اختار منافس  ", Start.AddDays(1)).IsSuccess.Should().BeTrue();
        opportunity.Status.Should().Be(OpportunityStatus.ClosedLost);
        opportunity.ClosedLostReason.Should().Be("العميل اختار منافس");

        opportunity.AdvanceStage(Start).Error.Should().Be(SalesErrors.OpportunityNotLive);
        opportunity.CloseLost("مرة أخرى", Start).Error.Should().Be(SalesErrors.OpportunityAlreadyClosed);

        opportunity.Reopen(Start.AddDays(5)).IsSuccess.Should().BeTrue();
        opportunity.Status.Should().Be(OpportunityStatus.Active);
        opportunity.Stage.Should().Be(OpportunityStage.SiteVisit);
        opportunity.ClientId.Should().Be(client, "reopening attaches to the same client (spec.md §3)");
        opportunity.ClosedLostReason.Should().BeNull();
        opportunity.LastActivityAt.Should().Be(Start.AddDays(5));
    }

    [Fact]
    public void Reopen_is_refused_unless_the_opportunity_is_closed_lost()
    {
        Make().Reopen(Start).Error.Should().Be(SalesErrors.OpportunityNotClosedLost);
    }

    [Fact]
    public void The_inactivity_ruling_describes_itself_and_is_registered()
    {
        RulingSet.Default.Notes.Should().Contain(OpportunityInactivity.Default.Note);
        OpportunityInactivity.Default.Note.Status.Should().Be(RulingStatus.Assumed);
        OpportunityInactivity.Default.Note.Question.Should().Contain("R3-Q5");
    }
}
