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

    [Theory]
    [InlineData("2026-09-01T22:30:00+00:00", "2026-10-02")] // summer, Cairo +03:00: 30 days on is 01:30 on 2 Oct
    [InlineData("2026-12-01T22:30:00+00:00", "2027-01-01")] // winter, Cairo +02:00: 30 days on is 00:30 on 1 Jan
    public void The_follow_up_end_is_a_site_day_not_a_utc_day(string startedAt, string endsOn)
    {
        DateTimeOffset start = DateTimeOffset.Parse(startedAt, System.Globalization.CultureInfo.InvariantCulture);
        Project project = Project.Create("FU-2", "Follow up", Guid.NewGuid(), ContractType.LumpSum, start).Value;

        project.FollowUpEndsOn(Period).Should().Be(DateOnly.Parse(endsOn, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void The_site_day_helper_gives_cairo_days_in_summer_and_winter()
    {
        SiteTime.DateOf(new DateTimeOffset(2026, 7, 10, 22, 30, 0, TimeSpan.Zero)).Should().Be(new DateOnly(2026, 7, 11));
        SiteTime.DateOf(new DateTimeOffset(2026, 12, 10, 22, 30, 0, TimeSpan.Zero)).Should().Be(new DateOnly(2026, 12, 11));
        SiteTime.DateOf(new DateTimeOffset(2026, 12, 10, 21, 30, 0, TimeSpan.Zero)).Should().Be(new DateOnly(2026, 12, 10));
    }
}
