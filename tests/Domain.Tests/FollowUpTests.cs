using Kaff.Domain.Contracts;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.Tests;

/// <summary>KAFF-417 AC-417-J: the follow-up period and the derived archive (D-208).</summary>
public sealed class FollowUpTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly FollowUpPeriod Period = new RulingSet().FollowUp;

    private static Project NewProject() =>
        Project.Create("FU-1", "Follow up", Guid.NewGuid(), ContractType.LumpSum, Start).Value;

    [Fact]
    public void The_default_period_is_30_days()
    {
        Period.Days.Should().Be(30);
    }

    [Fact]
    public void A_project_is_archived_from_day_30_and_not_before()
    {
        Project project = NewProject();

        project.IsArchived(Start.AddDays(30).AddTicks(-1), Period).Should().BeFalse();
        project.IsArchived(Start.AddDays(30), Period).Should().BeTrue();
    }

    [Fact]
    public void Reopen_is_refused_before_the_period_ends_and_restarts_it_after()
    {
        Project project = NewProject();

        project.Reopen(Start.AddDays(5), Period).IsFailure.Should().BeTrue();

        DateTimeOffset reopenedAt = Start.AddDays(31);
        project.Reopen(reopenedAt, Period).IsSuccess.Should().BeTrue();
        project.FollowUpStartedOn.Should().Be(reopenedAt);
        project.IsArchived(reopenedAt.AddDays(29), Period).Should().BeFalse();
        project.IsArchived(reopenedAt.AddDays(30), Period).Should().BeTrue();
    }

    [Fact]
    public void A_project_that_has_left_setup_is_never_archived()
    {
        Project project = NewProject();
        project.TransitionTo(ProjectStatus.Terminated, new DateOnly(2026, 11, 2), "client withdrew").IsSuccess.Should().BeTrue();

        project.IsArchived(Start.AddDays(400), Period).Should().BeFalse();
        project.FollowUpEndsOn(Period).Should().BeNull();
    }
}
