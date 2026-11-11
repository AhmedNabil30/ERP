using Kaff.Domain.Authorization;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.Tests;

/// <summary>KAFF-600 domain behaviour: the day an entry belongs to, and the rule-5 ruling.</summary>
public sealed class DailyLogTests
{
    private static readonly Guid Low = Guid.Parse("00000000-0000-7000-8000-000000000001");
    private static readonly Guid High = Guid.Parse("00000000-0000-7000-8000-000000000002");

    [Fact]
    public void An_entry_at_2350_local_belongs_to_that_local_day_whatever_the_utc_date_is()
    {
        var occurred = new DateTimeOffset(2026, 9, 25, 23, 50, 0, TimeSpan.FromHours(-3));

        DailyLog.DayOf(occurred).Should().Be(new DateOnly(2026, 9, 25));
        occurred.UtcDateTime.Date.Should().Be(new DateTime(2026, 9, 26), "the instant is already the next day in UTC");
    }

    [Fact]
    public void A_quantity_adds_by_default_because_it_is_a_delta()
    {
        var entries = new List<(DateTimeOffset, Guid, decimal)>
        {
            (new DateTimeOffset(2026, 9, 25, 9, 0, 0, TimeSpan.Zero), Low, 40m),
            (new DateTimeOffset(2026, 9, 25, 15, 0, 0, TimeSpan.Zero), High, 25m),
        };

        RulingSet.Default.DailyLogQuantityEntry.Effective(entries).Should().Be(65m);
    }

    [Fact]
    public void Ruled_the_other_way_the_latest_timestamp_wins_whatever_the_arrival_order()
    {
        RulingSet ruled = RulingSet.Default with
        {
            DailyLogQuantityEntry = new DailyLogQuantityEntry(QuantityEntryMode.LatestWins),
        };
        var later = (new DateTimeOffset(2026, 9, 25, 15, 0, 0, TimeSpan.Zero), Low, 25m);
        var earlier = (new DateTimeOffset(2026, 9, 25, 9, 0, 0, TimeSpan.Zero), High, 40m);

        ruled.DailyLogQuantityEntry.Effective([earlier, later]).Should().Be(25m);
        ruled.DailyLogQuantityEntry.Effective([later, earlier]).Should().Be(25m);
    }

    [Fact]
    public void The_log_project_states_ruling_takes_a_stopped_project_and_refuses_a_terminated_one()
    {
        DailyLogProjectStates states = RulingSet.Default.DailyLogProjectStates;

        states.Accepts(ProjectStatus.Stopped).Should().BeTrue("spec.md section 8 fixes this cell");
        states.Accepts(ProjectStatus.Active).Should().BeTrue();
        states.Accepts(ProjectStatus.Terminated).Should().BeFalse();
        states.Accepts(ProjectStatus.Closed).Should().BeFalse();
    }

    [Fact]
    public void The_daily_log_rulings_describe_themselves_and_only_the_ruled_ones_are_marked_ruled()
    {
        var notes = RulingSet.Default.Notes.Where(note => note.Name.StartsWith("DailyLog", StringComparison.Ordinal)).ToList();

        notes.Should().HaveCount(3)
            .And.OnlyContain(note => note.Question.Length > 0 && note.IfRuledDifferently.Length > 0);
        notes.Where(note => note.Status == RulingStatus.Ruled).Select(note => note.Name)
            .Should().BeEquivalentTo("DailyLogQuantityEntry", "DailyLogEntryRule");
        notes.Single(note => note.Name == "DailyLogProjectStates").Status.Should().Be(RulingStatus.Assumed);
    }

    [Fact]
    public void Only_an_assigned_site_engineer_writes_a_log_and_drafts_an_expense()
    {
        foreach (Permission permission in new[] { Permission.DailyLogWrite, Permission.SiteExpenseDraft })
        {
            PermissionDefinition definition = PermissionCatalogue.Of(permission);

            definition.Scope.Should().Be(PermissionScope.ProjectScoped);
            definition.Grants.Should().ContainSingle().Which.Role.Should().Be(Role.SiteEngineer);
        }
    }
}
