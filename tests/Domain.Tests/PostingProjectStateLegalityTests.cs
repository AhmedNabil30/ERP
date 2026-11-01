using System.Collections.Frozen;
using Kaff.Domain.Common;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;
using Kaff.Domain.Treasury;

namespace Kaff.Domain.Tests;

/// <summary>
/// The posting-type × project-state table (<see cref="PostingProjectStateLegality"/>). Finance could post
/// to a Closed or Terminated project before this existed, and the project's own extract rule
/// (spec.md §7) had no caller.
/// </summary>
public sealed class PostingProjectStateLegalityTests
{
    private static readonly PostingProjectStatePolicy Policy = RulingSet.Default.PostingProjectStates;

    private static Result Check(ProjectStatus status, PostingType type, bool isReversal = false) =>
        PostingProjectStateLegality.Validate(Policy, status, type, isReversal);

    // ---- the two cells spec.md itself fixes -------------------------------------------------------

    [Theory]
    [InlineData(ProjectStatus.Stopped)]
    [InlineData(ProjectStatus.Setup)]
    [InlineData(ProjectStatus.Closed)]
    [InlineData(ProjectStatus.Terminated)]
    public void A_project_that_cannot_issue_extracts_refuses_every_posting_an_extract_produces(ProjectStatus status)
    {
        foreach (PostingType type in new[]
                 {
                     PostingType.RevenueRecognition, PostingType.HoldAccrual, PostingType.MaterialAdvanceIssue,
                     PostingType.MaterialAdvanceRecovery, PostingType.ClientAdvanceRecovery,
                 })
        {
            Check(status, type).Error.Should().Be(TreasuryErrors.PostingNotAllowedInProjectState, $"{type} on {status}");
        }
    }

    [Theory]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.HandoverPending)]
    public void An_active_project_accepts_what_an_extract_produces(ProjectStatus status)
    {
        Check(status, PostingType.RevenueRecognition).IsSuccess.Should().BeTrue();
        Check(status, PostingType.HoldAccrual).IsSuccess.Should().BeTrue();
        Check(status, PostingType.ClientCollection).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Can_issue_extracts_is_read_from_the_same_table_so_the_two_cannot_disagree()
    {
        foreach (ProjectStatus status in Enum.GetValues<ProjectStatus>())
        {
            Policy.AllowsExtracts(status).Should().Be(
                status is ProjectStatus.Active or ProjectStatus.HandoverPending,
                $"spec.md section 7 and the table must agree for {status}");
        }
    }

    [Theory]
    [InlineData(ProjectStatus.Active, false)]
    [InlineData(ProjectStatus.HandoverPending, false)]
    [InlineData(ProjectStatus.Stopped, false)]
    [InlineData(ProjectStatus.Setup, false)]
    [InlineData(ProjectStatus.Handover, true)]
    [InlineData(ProjectStatus.UnderWarranty, true)]
    [InlineData(ProjectStatus.Closed, false)]
    [InlineData(ProjectStatus.Terminated, false)]
    public void The_hold_releases_only_once_the_project_has_been_handed_over(ProjectStatus status, bool allowed)
    {
        Check(status, PostingType.HoldRelease).IsSuccess.Should().Be(allowed);
    }

    // ---- the cells that are assumptions ---------------------------------------------------------------

    [Fact]
    public void A_stopped_project_still_pays_what_it_owes()
    {
        Check(ProjectStatus.Stopped, PostingType.SupplierPayment).IsSuccess.Should().BeTrue();
        Check(ProjectStatus.Stopped, PostingType.SubcontractorPayment).IsSuccess.Should().BeTrue();
        Check(ProjectStatus.Stopped, PostingType.ClientCollection).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_closed_project_accepts_only_collections_and_adjustments()
    {
        Check(ProjectStatus.Closed, PostingType.ClientCollection).IsSuccess.Should().BeTrue();
        Check(ProjectStatus.Closed, PostingType.Adjustment).IsSuccess.Should().BeTrue();
        Check(ProjectStatus.Closed, PostingType.SupplierPayment).IsFailure.Should().BeTrue();
        Check(ProjectStatus.Closed, PostingType.PettyCashIssue).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_terminated_project_settles_but_does_not_spend_on_new_work()
    {
        Check(ProjectStatus.Terminated, PostingType.ClientCollection).IsSuccess.Should().BeTrue();
        Check(ProjectStatus.Terminated, PostingType.SupplierPayment).IsSuccess.Should().BeTrue();
        Check(ProjectStatus.Terminated, PostingType.CreditNote).IsSuccess.Should().BeTrue();
        Check(ProjectStatus.Terminated, PostingType.PettyCashIssue).IsFailure.Should().BeTrue();
        Check(ProjectStatus.Terminated, PostingType.HoldRelease).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Every_status_has_a_row_so_a_new_state_cannot_silently_accept_everything()
    {
        foreach (ProjectStatus status in Enum.GetValues<ProjectStatus>())
        {
            PostingTypes.AllDefined.Any(type => Policy.Allows(status, type))
                .Should().BeTrue($"{status} must accept at least one posting type or have been left out of the table");
        }
    }

    // ---- reversals --------------------------------------------------------------------------------

    [Fact]
    public void A_reversal_is_a_correction_and_is_allowed_in_any_state_by_default()
    {
        Check(ProjectStatus.Closed, PostingType.HoldAccrual, isReversal: true).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Ruling_that_corrections_obey_the_table_is_one_flag()
    {
        var strict = new PostingProjectStatePolicy(CurrentTable(), reversalsFollowTheTable: true);

        PostingProjectStateLegality.Validate(strict, ProjectStatus.Closed, PostingType.HoldAccrual, isReversal: true)
            .Error.Should().Be(TreasuryErrors.PostingNotAllowedInProjectState);
    }

    private static Dictionary<ProjectStatus, FrozenSet<PostingType>> CurrentTable() =>
        Enum.GetValues<ProjectStatus>().ToDictionary(
            status => status,
            status => PostingTypes.AllDefined.Where(type => Policy.Allows(status, type)).ToFrozenSet());

    // ---- the table is data, so one cell is one line -------------------------------------------------

    [Fact]
    public void Ruling_a_cell_differently_changes_only_that_cell()
    {
        Dictionary<ProjectStatus, FrozenSet<PostingType>> table = CurrentTable();

        table[ProjectStatus.Closed] = table[ProjectStatus.Closed].Append(PostingType.SupplierPayment).ToFrozenSet();

        var ruled = new PostingProjectStatePolicy(table, reversalsFollowTheTable: false);

        ruled.Allows(ProjectStatus.Closed, PostingType.SupplierPayment).Should().BeTrue();
        ruled.Allows(ProjectStatus.Closed, PostingType.PettyCashIssue).Should().BeFalse();
        ruled.Allows(ProjectStatus.Stopped, PostingType.SupplierPayment).Should().BeTrue();
    }
}
