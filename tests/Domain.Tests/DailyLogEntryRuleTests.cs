using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.Rulings;

namespace Kaff.Domain.Tests;

/// <summary>
/// KAFF-614 and KAFF-615 domain behaviour under D-217 and D-218: the three-day window, 16 hours in half-hour steps, who may
/// correct which day, and that a correction is a new signed delta.
/// </summary>
public sealed class DailyLogEntryRuleTests
{
    private static readonly DailyLogEntryRule Rule = RulingSet.Default.DailyLogEntry;
    private static readonly DateOnly Today = new(2026, 11, 11);
    private static readonly DateTimeOffset Noon = new(2026, 11, 11, 12, 0, 0, TimeSpan.FromHours(2));

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(-3, true)]
    [InlineData(-4, false)]
    [InlineData(1, false)]
    public void An_entry_is_dated_today_or_up_to_three_days_back_and_no_further(int daysFromToday, bool allowed) =>
        Rule.InWindow(Today.AddDays(daysFromToday), Today).Should().Be(allowed);

    [Theory]
    [InlineData("0.5", true)]
    [InlineData("8", true)]
    [InlineData("15.5", true)]
    [InlineData("16", true)]
    [InlineData("16.5", false)]
    [InlineData("17", false)]
    [InlineData("24", false)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("8.25", false)]
    [InlineData("7.1", false)]
    public void Hours_are_above_zero_at_most_16_and_a_multiple_of_half_an_hour(string hours, bool allowed) =>
        Rule.HoursAllowed(decimal.Parse(hours, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(allowed);

    [Fact]
    public void Today_is_the_calendar_day_on_site_not_in_utc()
    {
        // 23:30 UTC on the 10th is already the 11th in Cairo, both in winter (+02:00) and in summer (+03:00).
        Rule.SiteDate(new DateTimeOffset(2026, 11, 10, 23, 30, 0, TimeSpan.Zero)).Should().Be(new DateOnly(2026, 11, 11));
        Rule.SiteDate(new DateTimeOffset(2026, 7, 10, 21, 30, 0, TimeSpan.Zero)).Should().Be(new DateOnly(2026, 7, 11));
        Rule.SiteDate(new DateTimeOffset(2026, 11, 10, 21, 30, 0, TimeSpan.Zero)).Should().Be(new DateOnly(2026, 11, 10));
    }

    [Fact]
    public void The_entry_window_rule_is_listed_as_ruled_and_cites_the_decisions()
    {
        RulingNote note = Rule.Note;

        note.Status.Should().Be(RulingStatus.Ruled);
        note.Question.Should().Contain("D-217");
        RulingSet.Default.Notes.Should().Contain(note);
    }

    // ---- crew hours ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("16.5")]
    [InlineData("24")]
    [InlineData("7.25")]
    [InlineData("0")]
    public void Crew_hours_outside_the_rule_are_refused_and_nothing_is_created(string hours)
    {
        DailyLog log = OpenLog();

        Result<DayLabourEntry> result = log.RecordDayLabour(
            Guid.NewGuid(), Guid.NewGuid(), decimal.Parse(hours, System.Globalization.CultureInfo.InvariantCulture), 0m, Rule, Noon, Noon);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DailyLogErrors.HoursOutOfRange);
    }

    [Fact]
    public void One_man_cannot_pass_16_hours_in_a_day_across_entries()
    {
        DailyLog log = OpenLog();

        log.RecordDayLabour(Guid.NewGuid(), Guid.NewGuid(), 8m, 8m, Rule, Noon, Noon).IsSuccess.Should().BeTrue("8 and 8 make 16");
        log.RecordDayLabour(Guid.NewGuid(), Guid.NewGuid(), 8.5m, 8m, Rule, Noon, Noon).Error.Should().Be(DailyLogErrors.HoursOutOfRange);
    }

    // ---- corrections --------------------------------------------------------------------------------------------

    [Fact]
    public void A_crew_correction_is_a_new_signed_delta_that_names_the_entry_and_leaves_it_unchanged()
    {
        DailyLog log = OpenLog();
        Guid worker = Guid.NewGuid();
        Guid corrector = Guid.NewGuid();
        DayLabourEntry original = log.RecordDayLabour(Guid.NewGuid(), worker, 8m, 0m, Rule, Noon, Noon).Value;

        DayLabourEntry correction = log.CorrectDayLabour(Guid.NewGuid(), original, 8m, 6m, 0m, Rule, corrector, Noon.AddHours(1)).Value;

        correction.Hours.Should().Be(-2m, "the stored figure is the signed difference");
        correction.CorrectsEntryId.Should().Be(original.Id);
        correction.CorrectedByUserId.Should().Be(corrector);
        correction.EmployeeId.Should().Be(worker);
        correction.DailyLogId.Should().Be(original.DailyLogId);
        original.Hours.Should().Be(8m, "the saved entry is never updated");
        original.CorrectsEntryId.Should().BeNull();
    }

    [Fact]
    public void A_crew_correction_obeys_the_hours_rule_and_must_change_something()
    {
        DailyLog log = OpenLog();
        DayLabourEntry original = log.RecordDayLabour(Guid.NewGuid(), Guid.NewGuid(), 8m, 0m, Rule, Noon, Noon).Value;

        log.CorrectDayLabour(Guid.NewGuid(), original, 8m, 17m, 0m, Rule, Guid.NewGuid(), Noon).Error.Should().Be(DailyLogErrors.HoursOutOfRange);
        log.CorrectDayLabour(Guid.NewGuid(), original, 8m, 7.3m, 0m, Rule, Guid.NewGuid(), Noon).Error.Should().Be(DailyLogErrors.HoursOutOfRange);
        log.CorrectDayLabour(Guid.NewGuid(), original, 8m, 8m, 0m, Rule, Guid.NewGuid(), Noon).Error.Should().Be(DailyLogErrors.NothingToCorrect);
    }

    [Fact]
    public void A_correction_cannot_be_corrected_and_cannot_cross_logs()
    {
        DailyLog log = OpenLog();
        DayLabourEntry original = log.RecordDayLabour(Guid.NewGuid(), Guid.NewGuid(), 8m, 0m, Rule, Noon, Noon).Value;
        DayLabourEntry correction = log.CorrectDayLabour(Guid.NewGuid(), original, 8m, 6m, 0m, Rule, Guid.NewGuid(), Noon).Value;

        log.CorrectDayLabour(Guid.NewGuid(), correction, 6m, 5m, 0m, Rule, Guid.NewGuid(), Noon)
            .Error.Should().Be(DailyLogErrors.CannotCorrectACorrection);
        OpenLog().CorrectDayLabour(Guid.NewGuid(), original, 8m, 5m, 0m, Rule, Guid.NewGuid(), Noon)
            .Error.Should().Be(DailyLogErrors.EntryNotFound);
    }

    [Fact]
    public void The_weather_kind_is_refused_on_the_fields_route_and_the_other_kinds_still_work()
    {
        DailyLog log = OpenLog();

        log.RecordField(Guid.NewGuid(), DailyLogField.Weather, "sunny", Noon, Noon).Error.Should().Be(DailyLogErrors.WeatherRetired);
        log.RecordField(Guid.NewGuid(), DailyLogField.Note, "a note", Noon, Noon).IsSuccess.Should().BeTrue();
        log.RecordField(Guid.NewGuid(), DailyLogField.StoppageReason, "rain", Noon, Noon).IsSuccess.Should().BeTrue();
    }

    // ---- who may correct ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, true, false, true)]
    [InlineData(0, false, true, false)]
    [InlineData(0, false, false, false)]
    [InlineData(-1, false, true, true)]
    [InlineData(-3, false, true, true)]
    [InlineData(-1, true, false, false)]
    [InlineData(-1, false, false, false)]
    public void A_quantity_is_corrected_today_by_its_author_and_an_earlier_day_by_the_supervising_engineer(
        int daysBack, bool isAuthor, bool isSupervisor, bool allowed)
    {
        Result result = CorrectionAccess.ForQuantity(Today.AddDays(daysBack), Today, Rule, isAuthor, isSupervisor);

        result.IsSuccess.Should().Be(allowed);

        if (!allowed)
        {
            result.Error.Should().Be(DailyLogErrors.CorrectionNotAllowed);
        }
    }

    [Fact]
    public void A_quantity_of_a_day_outside_the_open_days_is_closed_to_everyone()
    {
        CorrectionAccess.ForQuantity(Today.AddDays(-4), Today, Rule, isAuthor: true, isSupervisingEngineer: true)
            .Error.Should().Be(DailyLogErrors.CorrectionWindowClosed);
    }

    [Theory]
    [InlineData(0, true, false, false, true)]
    [InlineData(0, false, true, false, false)]
    [InlineData(0, false, false, true, true)]
    [InlineData(-2, false, true, false, true)]
    [InlineData(-2, true, false, false, false)]
    [InlineData(-2, false, false, true, true)]
    [InlineData(-3, false, false, true, true)]
    [InlineData(-2, false, false, false, false)]
    public void Crew_is_edited_today_by_its_author_an_earlier_day_by_the_supervisor_and_any_open_day_by_the_owner(
        int daysBack, bool isAuthor, bool isSupervisor, bool isOwner, bool allowed) =>
        CorrectionAccess.ForCrew(Today.AddDays(daysBack), Today, Rule, isAuthor, isSupervisor, isOwner).IsSuccess.Should().Be(allowed);

    [Fact]
    public void Not_even_the_owner_edits_the_crew_of_a_day_outside_the_open_days() =>
        CorrectionAccess.ForCrew(Today.AddDays(-4), Today, Rule, false, false, isOwner: true)
            .Error.Should().Be(DailyLogErrors.CorrectionWindowClosed);

    // ---- permissions --------------------------------------------------------------------------------------------

    [Fact]
    public void Crew_correction_is_the_owner_and_assigned_site_engineers_and_nobody_else()
    {
        PermissionDefinition definition = PermissionCatalogue.Of(Permission.DailyLogCrewCorrect);

        definition.Scope.Should().Be(PermissionScope.ProjectScoped);
        definition.Grants.Select(grant => grant.Role).Should().BeEquivalentTo([Role.Owner, Role.SiteEngineer]);
    }

    [Fact]
    public void Reviewing_the_days_logs_is_the_owner_and_the_supervising_engineer_and_nobody_else()
    {
        PermissionDefinition definition = PermissionCatalogue.Of(Permission.DailyLogReview);

        definition.Scope.Should().Be(PermissionScope.ProjectScoped);
        definition.Grants.Select(grant => grant.Role).Should().BeEquivalentTo([Role.Owner, Role.SiteEngineer]);
        definition.Grants.Single(grant => grant.Role == Role.SiteEngineer).MinimumAssignmentLevel.Should().Be(AssignmentLevel.Supervisor);
    }

    private static DailyLog OpenLog() => DailyLog.Open(Guid.NewGuid(), Guid.NewGuid(), Noon, Noon);
}
