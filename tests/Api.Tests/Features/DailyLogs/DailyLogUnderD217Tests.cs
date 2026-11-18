using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Domain.Auditing;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.DailyLogs;

/// <summary>
/// KAFF-614 (the daily log) and KAFF-615 (the crew screen) under D-217 and D-218. The server clock is fixed on 2026-09-25 by
/// the shared fixture, so "today" is the 25th and "yesterday" the 24th.
/// </summary>
public sealed partial class DailyLogTests
{
    private const string Yesterday = "2026-09-24";

    private static string Dated(int daysBack, int hour = 9) =>
        $"{new DateOnly(2026, 9, 25).AddDays(-daysBack):yyyy-MM-dd}T{hour:00}:00:00+00:00";

    private Task<HttpResponseMessage> NoCheckIn(string kind, Guid project, Guid line, Guid actor, string occurredAt) => kind switch
    {
        "quantity" => SendAsync(HttpMethod.Post, $"{Logs(project)}/quantities", actor,
            new { entryId = Guid.NewGuid(), boqLineId = line, quantity = 1m, occurredAt }, autoCheckIn: false),
        "day-labour" => SendAsync(HttpMethod.Post, $"{Logs(project)}/day-labour", actor,
            new { entryId = Guid.NewGuid(), employeeId = _workerId, hours = 8m, occurredAt }, autoCheckIn: false),
        "materials" => SendAsync(HttpMethod.Post, $"{Logs(project)}/materials", actor,
            new { entryId = Guid.NewGuid(), materialType = "Cement", quantity = 1m, unit = "bag", isStockpile = false, occurredAt }, autoCheckIn: false),
        "photos" => SendAsync(HttpMethod.Post, $"{Logs(project)}/photos", actor,
            new { entryId = Guid.NewGuid(), fileReference = "p.jpg", occurredAt }, autoCheckIn: false),
        "fields" => SendAsync(HttpMethod.Post, $"{Logs(project)}/fields", actor,
            new { entryId = Guid.NewGuid(), field = "Note", text = "x", occurredAt }, autoCheckIn: false),
        "expense-drafts" => SendAsync(HttpMethod.Post, $"{Logs(project)}/expense-drafts", actor,
            new { entryId = Guid.NewGuid(), description = "x", amount = "1", occurredAt }, autoCheckIn: false),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private Task<HttpResponseMessage> CorrectQty(Guid project, Guid entryId, decimal quantity, Guid actor, Guid? correctionId = null) =>
        SendAsync(
            HttpMethod.Post, $"{Logs(project)}/quantities/{entryId}/corrections", actor,
            new { correctionId = correctionId ?? Guid.NewGuid(), quantity });

    private Task<HttpResponseMessage> CorrectCrew(Guid project, Guid entryId, decimal hours, Guid actor, Guid? correctionId = null) =>
        SendAsync(
            HttpMethod.Post, $"{Logs(project)}/day-labour/{entryId}/corrections", actor,
            new { correctionId = correctionId ?? Guid.NewGuid(), hours });

    private Task<HttpResponseMessage> Crew(Guid project, decimal hours, string occurredAt, Guid actor, Guid? employee = null, Guid? entryId = null) =>
        SendAsync(
            HttpMethod.Post, $"{Logs(project)}/day-labour", actor,
            new { entryId = entryId ?? Guid.NewGuid(), employeeId = employee ?? _workerId, hours, occurredAt });

    private async Task<Guid> SavedQuantityAsync(Guid project, Guid line, decimal quantity, string occurredAt, Guid? actor = null)
    {
        Guid id = Guid.NewGuid();
        (await Quantity(project, line, quantity, occurredAt, id, actor)).StatusCode.Should().Be(HttpStatusCode.Created);
        return id;
    }

    private async Task<Guid> SavedCrewAsync(Guid project, decimal hours, string occurredAt, Guid actor, Guid? employee = null)
    {
        Guid id = Guid.NewGuid();
        (await Crew(project, hours, occurredAt, actor, employee, id)).StatusCode.Should().Be(HttpStatusCode.Created);
        return id;
    }

    // Decimals are sent as strings, so no precision is lost on the way to the screen.
    private static decimal Num(JsonElement element) =>
        element.ValueKind == JsonValueKind.String ? decimal.Parse(element.GetString()!, System.Globalization.CultureInfo.InvariantCulture) : element.GetDecimal();

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private async Task<JsonElement> ReviewAsync(Guid project, string date, Guid actor, Guid engineer)
    {
        JsonElement list = await ReadAsync(await SendAsync(HttpMethod.Get, $"{Logs(project)}?date={date}", actor, null));
        return list.EnumerateArray().Single(item => item.GetProperty("engineerId").GetGuid() == engineer).GetProperty("log");
    }

    private async Task<int> RowsAsync(string table, Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
#pragma warning disable EF1002 // The table name is a literal from this file, never user input.
        return await reader.Database
            .SqlQueryRaw<int>($"SELECT count(*)::int AS \"Value\" FROM {table} WHERE project_id = '{project}'")
            .SingleAsync(Ct);
#pragma warning restore EF1002
    }

    // ---- KAFF-614 AC-1: check-in first --------------------------------------------------------------------------

    [Theory]
    [InlineData("quantity", "executed_quantity_entries")]
    [InlineData("day-labour", "day_labour_entries")]
    [InlineData("materials", "material_entries")]
    [InlineData("photos", "photo_entries")]
    [InlineData("fields", "field_entries")]
    [InlineData("expense-drafts", "site_expense_drafts")]
    public async Task AC_614_1_no_entry_of_any_kind_is_saved_before_the_engineer_has_checked_in_today(string kind, string table)
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        HttpResponseMessage response = await NoCheckIn(kind, project, line, _engineerA, At(9, 0));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.daily_log.check_in_required");
        (await RowsAsync(table, project)).Should().Be(0);
        (await CountAsync("daily_logs", project)).Should().Be(0, "a refused entry opens no log");
        (await ExecutedAsync(line)).Should().Be(0m);
    }

    [Fact]
    public async Task AC_614_1_after_the_check_in_the_same_entry_is_saved()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        (await NoCheckIn("quantity", project, line, _engineerA, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CheckIn(project, "CheckIn", 30.0444, 31.2357, At(8, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await NoCheckIn("quantity", project, line, _engineerA, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AC_614_1_a_check_in_on_another_project_or_by_another_engineer_does_not_count()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        (Guid other, _) = await NewActiveProjectAsync();

        (await CheckIn(other, "CheckIn", 30.0444, 31.2357, At(8, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, $"{Logs(project)}/check-ins", _engineerB,
            new { entryId = Guid.NewGuid(), kind = "CheckIn", latitude = 30.0, longitude = 31.0, occurredAt = At(8, 0) }, autoCheckIn: false))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await NoCheckIn("quantity", project, line, _engineerA, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await NoCheckIn("quantity", project, line, _engineerB, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AC_614_1_a_check_out_is_not_a_check_in()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        (await CheckIn(project, "CheckOut", 30.0444, 31.2357, At(8, 0))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await NoCheckIn("quantity", project, line, _engineerA, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---- KAFF-614 AC-2: three days back, no check-in of its own -------------------------------------------------

    [Theory]
    [InlineData(0, HttpStatusCode.Created)]
    [InlineData(1, HttpStatusCode.Created)]
    [InlineData(3, HttpStatusCode.Created)]
    [InlineData(4, HttpStatusCode.Conflict)]
    [InlineData(-1, HttpStatusCode.Conflict)]
    public async Task AC_614_2_an_entry_is_dated_today_or_up_to_three_days_back_and_no_further(int daysBack, HttpStatusCode expected)
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        HttpResponseMessage response = await Quantity(project, line, 5m, Dated(daysBack));

        response.StatusCode.Should().Be(expected);

        if (expected == HttpStatusCode.Conflict)
        {
            (await MessageKeyAsync(response)).Should().Be("errors.daily_log.entry_outside_window");
            (await ExecutedAsync(line)).Should().Be(0m);
        }
    }

    [Fact]
    public async Task AC_614_2_a_past_day_needs_the_engineer_to_be_checked_in_today_and_gets_no_check_in_of_its_own()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        // No check-in today: the past day is refused for want of one.
        HttpResponseMessage refused = await NoCheckIn("quantity", project, line, _engineerA, Dated(1));
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(refused)).Should().Be("errors.daily_log.check_in_required");

        // A check-in dated on the past day is refused: a past day gets none, and it would not count as today's.
        HttpResponseMessage backCheckIn = await CheckIn(project, "CheckIn", 30.0444, 31.2357, Dated(1, 8));
        backCheckIn.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(backCheckIn)).Should().Be("errors.daily_log.check_in_only_today");
        (await NoCheckIn("quantity", project, line, _engineerA, Dated(1))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Checked in today: the past day is accepted.
        (await CheckIn(project, "CheckIn", 30.0444, 31.2357, At(8, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await NoCheckIn("quantity", project, line, _engineerA, Dated(1))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await MineAsync(project, Yesterday)).GetProperty("quantities").GetArrayLength().Should().Be(1);
    }

    // ---- KAFF-614 AC-3 and KAFF-615 AC-8: the back-dated flag ---------------------------------------------------

    [Fact]
    public async Task AC_614_3_the_back_dated_flag_goes_to_the_supervising_engineer_and_the_owner_only()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        await SavedQuantityAsync(project, line, 5m, Dated(1));
        await SavedQuantityAsync(project, line, 2m, At(9, 0));
        await SavedCrewAsync(project, 8m, Dated(1), _engineerA);

        // The engineer who entered it never sees the flag: the member is absent, not false.
        string own = await (await SendAsync(HttpMethod.Get, $"{Logs(project)}/mine?date={Yesterday}", _engineerA, null))
            .Content.ReadAsStringAsync(Ct);
        own.Should().NotContainEquivalentOf("backDated");

        foreach (Guid reviewer in new[] { _supervisor, _owner })
        {
            JsonElement past = await ReviewAsync(project, Yesterday, reviewer, _engineerA);
            past.GetProperty("backDated").GetBoolean().Should().BeTrue();
            past.GetProperty("quantities")[0].GetProperty("backDated").GetBoolean().Should().BeTrue();
            past.GetProperty("dayLabour")[0].GetProperty("backDated").GetBoolean().Should().BeTrue("KAFF-615 AC-8: a back-dated crew day carries the flag");

            JsonElement today = await ReviewAsync(project, "2026-09-25", reviewer, _engineerA);
            today.GetProperty("backDated").GetBoolean().Should().BeFalse();
            today.GetProperty("quantities")[0].GetProperty("backDated").GetBoolean().Should().BeFalse();
        }
    }

    [Fact]
    public async Task AC_614_3_the_supervising_engineer_sees_the_flag_on_his_own_log_a_junior_does_not()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        await SavedQuantityAsync(project, line, 5m, Dated(2), _supervisor);

        JsonElement own = await MineAs(project, "2026-09-23", _supervisor);

        own.GetProperty("backDated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task AC_614_3_the_review_read_is_refused_to_a_junior_engineer_an_unassigned_one_and_finance()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        foreach (Guid refused in new[] { _engineerA, _engineerUnassigned, _finance })
        {
            (await SendAsync(HttpMethod.Get, $"{Logs(project)}?date={Yesterday}", refused, null))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    private async Task<JsonElement> MineAs(Guid project, string date, Guid actor) =>
        await ReadAsync(await SendAsync(HttpMethod.Get, $"{Logs(project)}/mine?date={date}", actor, null));

    // ---- KAFF-614 AC-5, AC-6: a correction ----------------------------------------------------------------------

    [Fact]
    public async Task AC_614_5_the_engineer_who_entered_a_quantity_today_corrects_it_and_the_boq_moves_by_the_difference()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        Guid saved = await SavedQuantityAsync(project, line, 40m, At(9, 0));

        HttpResponseMessage down = await CorrectQty(project, saved, 30m, _engineerA);

        down.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ExecutedAsync(line)).Should().Be(30m);

        (await CorrectQty(project, saved, 45m, _engineerA)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await ExecutedAsync(line)).Should().Be(45m);
    }

    [Fact]
    public async Task AC_614_5_anyone_else_is_refused_a_correction_to_todays_quantity()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        Guid saved = await SavedQuantityAsync(project, line, 40m, At(9, 0));

        // Another junior, the supervising engineer (not the author), the unassigned engineer, the owner and finance.
        foreach (Guid refused in new[] { _engineerB, _supervisor, _engineerUnassigned, _owner, _finance })
        {
            HttpResponseMessage response = await CorrectQty(project, saved, 10m, refused);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, refused.ToString());
        }

        (await ExecutedAsync(line)).Should().Be(40m);
        (await RowsAsync("executed_quantity_entries", project)).Should().Be(1);
    }

    [Fact]
    public async Task AC_614_5_an_earlier_open_day_is_corrected_by_the_supervising_engineer_and_not_by_the_author()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        Guid saved = await SavedQuantityAsync(project, line, 40m, Dated(1));

        (await CorrectQty(project, saved, 10m, _engineerA)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CorrectQty(project, saved, 10m, _engineerB)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ExecutedAsync(line)).Should().Be(40m);

        (await CorrectQty(project, saved, 25m, _supervisor)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await ExecutedAsync(line)).Should().Be(25m);
    }

    [Fact]
    public async Task AC_614_5_a_day_that_has_left_the_open_days_can_no_longer_be_corrected()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        Guid saved = await SavedQuantityAsync(project, line, 40m, Dated(3));
        _clock.Advance(TimeSpan.FromDays(1));

        HttpResponseMessage response = await CorrectQty(project, saved, 25m, _supervisor);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.daily_log.correction_window_closed");
        (await ExecutedAsync(line)).Should().Be(40m);
    }

    [Fact]
    public async Task AC_614_6_a_correction_is_a_new_signed_delta_naming_the_entry_and_nothing_is_updated()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        Guid saved = await SavedQuantityAsync(project, line, 40m, At(9, 0));
        Guid correction = Guid.NewGuid();

        (await CorrectQty(project, saved, 30m, _engineerA, correction)).StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext reader = _database.CreateBareContext();
        List<ExecutedQuantityEntry> rows = await reader.ExecutedQuantityEntries.AsNoTracking()
            .Where(entry => entry.ProjectId == project).OrderBy(entry => entry.CreatedAt).ToListAsync(Ct);
        rows.Should().HaveCount(2);
        ExecutedQuantityEntry original = rows.Single(entry => entry.Id == saved);
        ExecutedQuantityEntry delta = rows.Single(entry => entry.Id == correction);
        original.Quantity.Should().Be(40m, "the saved entry is never updated");
        original.CorrectsEntryId.Should().BeNull();
        delta.Quantity.Should().Be(-10m);
        delta.CorrectsEntryId.Should().Be(saved);
        delta.CorrectedByUserId.Should().Be(_engineerA);

        JsonElement quantities = (await MineAsync(project)).GetProperty("quantities");
        quantities.GetArrayLength().Should().Be(1, "the log reads one corrected figure per saved quantity");
        quantities[0].GetProperty("entryId").GetGuid().Should().Be(saved);
        Num(quantities[0].GetProperty("quantity")).Should().Be(30m);
        quantities[0].GetProperty("corrected").GetBoolean().Should().BeTrue();
        quantities[0].GetProperty("correctedByUserId").GetGuid().Should().Be(_engineerA);
        quantities[0].GetProperty("correctedByName").GetString().Should().Be("dl-eng-a");
        quantities[0].GetProperty("correctedAt").GetDateTimeOffset().Should().BeCloseTo(_clock.GetUtcNow(), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AC_614_6_a_retried_correction_is_not_applied_twice_and_a_no_op_or_bad_figure_is_refused()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        Guid saved = await SavedQuantityAsync(project, line, 40m, At(9, 0));
        Guid correction = Guid.NewGuid();

        (await CorrectQty(project, saved, 30m, _engineerA, correction)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await CorrectQty(project, saved, 30m, _engineerA, correction)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ExecutedAsync(line)).Should().Be(30m);

        HttpResponseMessage same = await CorrectQty(project, saved, 30m, _engineerA);
        same.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(same)).Should().Be("errors.daily_log.nothing_to_correct");
        (await CorrectQty(project, saved, 0m, _engineerA)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CorrectQty(project, saved, -5m, _engineerA)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A correction cannot itself be corrected, and an unknown entry is not found.
        (await CorrectQty(project, correction, 20m, _engineerA)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CorrectQty(project, Guid.NewGuid(), 20m, _engineerA)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ExecutedAsync(line)).Should().Be(30m);
    }

    [Fact]
    public async Task AC_614_6_an_entry_of_another_project_cannot_be_reached_through_this_one()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        (Guid other, _) = await NewActiveProjectAsync();
        Guid saved = await SavedQuantityAsync(project, line, 40m, At(9, 0));

        (await CorrectQty(other, saved, 10m, _engineerA)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ExecutedAsync(line)).Should().Be(40m);
    }

    // ---- KAFF-614 AC-7, AC-8: notes and the stoppage reason -----------------------------------------------------

    [Fact]
    public async Task AC_614_8_a_stoppage_reason_is_free_text_on_a_running_project_and_does_not_change_its_status()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        (await Field(project, "StoppageReason", "Rain all day, the slab pour was cancelled", At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await MineAsync(project)).GetProperty("stoppageReasons")[0].GetProperty("text").GetString().Should().Contain("Rain");
        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Projects.AsNoTracking().SingleAsync(candidate => candidate.Id == project, Ct)).Status
            .Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public async Task AC_614_8_a_second_reason_the_same_day_is_kept_beside_the_first_and_a_past_open_day_takes_one()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        (await Field(project, "StoppageReason", "No cement delivery", At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Field(project, "StoppageReason", "Then rain", At(13, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, $"{Logs(project)}/fields", _engineerA,
            new { entryId = Guid.NewGuid(), field = "StoppageReason", text = "Power cut yesterday", occurredAt = Dated(1) }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await MineAsync(project)).GetProperty("stoppageReasons").EnumerateArray()
            .Select(reason => reason.GetProperty("text").GetString()).Should().Equal("No cement delivery", "Then rain");
        (await MineAsync(project, Yesterday)).GetProperty("stoppageReasons").GetArrayLength().Should().Be(1);
    }

    // ---- KAFF-614 AC-9: a stopped project ------------------------------------------------------------------------

    [Fact]
    public async Task AC_614_9_a_stopped_project_still_records_quantities_materials_and_photos()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        await SetStatusAsync(project, ProjectStatus.Stopped);

        (await Quantity(project, line, 5m, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, $"{Logs(project)}/materials", _engineerA,
            new { entryId = Guid.NewGuid(), materialType = "Cement", quantity = 3m, unit = "bag", isStockpile = true, occurredAt = At(9, 0) }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, $"{Logs(project)}/photos", _engineerA,
            new { entryId = Guid.NewGuid(), fileReference = await UploadedAsync(project), occurredAt = At(9, 0) }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await ExecutedAsync(line)).Should().Be(5m);
    }

    // ---- KAFF-614 AC-10: weather is gone --------------------------------------------------------------------------

    [Fact]
    public async Task AC_614_10_the_weather_kind_is_refused_and_a_stored_weather_row_stays_and_does_not_break_the_read()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid stored = Guid.NewGuid();

        HttpResponseMessage refused = await SendAsync(HttpMethod.Post, $"{Logs(project)}/fields", _engineerA,
            new { entryId = Guid.NewGuid(), field = "Weather", text = "sunny", occurredAt = At(9, 0) });
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(refused)).Should().Be("errors.daily_log.weather_retired");

        // A row written before the ruling: a note turned into weather underneath, as old data is.
        (await SendAsync(HttpMethod.Post, $"{Logs(project)}/fields", _engineerA,
            new { entryId = stored, field = "Note", text = "sunny and hot", occurredAt = At(9, 0) })).StatusCode.Should().Be(HttpStatusCode.Created);
        await using (KaffDbContext writer = _database.CreateBareAdminContext())
        {
            await writer.Database.ExecuteSqlAsync($"UPDATE field_entries SET field = 'Weather' WHERE id = {stored}", Ct);
        }

        HttpResponseMessage read = await SendAsync(HttpMethod.Get, $"{Logs(project)}/mine?date=2026-09-25", _engineerA, null);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        string json = await read.Content.ReadAsStringAsync(Ct);
        json.Should().NotContainEquivalentOf("weather").And.NotContain("sunny and hot");
        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.FieldEntries.AsNoTracking().CountAsync(entry => entry.Id == stored, Ct)).Should().Be(1, "stored rows are not deleted");
    }

    // ---- S-6: the unit of a material is a stable key, not a translated label --------------------------------------

    [Fact]
    public async Task Finding_S6_a_material_unit_is_stored_and_read_as_its_key()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        (await SendAsync(HttpMethod.Post, $"{Logs(project)}/materials", _engineerA,
            new { entryId = Guid.NewGuid(), materialType = "Cement", quantity = 3m, unit = "m3", isStockpile = false, occurredAt = At(9, 0) }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await MineAsync(project)).GetProperty("materials")[0].GetProperty("unit").GetString().Should().Be("m3");
    }

    [Theory]
    [InlineData("كيس")]
    [InlineData("Bag")]
    [InlineData("kg")]
    public async Task Finding_S6_a_material_unit_that_is_not_a_key_of_the_list_is_refused(string unit)
    {
        (Guid project, _) = await NewActiveProjectAsync();

        HttpResponseMessage refused = await SendAsync(HttpMethod.Post, $"{Logs(project)}/materials", _engineerA,
            new { entryId = Guid.NewGuid(), materialType = "Cement", quantity = 3m, unit, isStockpile = false, occurredAt = At(9, 0) });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(refused)).Should().Be("errors.daily_log.unit_not_allowed");
    }

    [Fact]
    public async Task Finding_S6_a_stored_unit_that_is_a_label_is_read_back_as_it_was_stored()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid stored = Guid.NewGuid();
        (await SendAsync(HttpMethod.Post, $"{Logs(project)}/materials", _engineerA,
            new { entryId = stored, materialType = "Cement", quantity = 3m, unit = "bag", isStockpile = false, occurredAt = At(9, 0) }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // A row written before the key: the translated label, as the old screen sent it.
        await using (KaffDbContext writer = _database.CreateBareAdminContext())
        {
            await writer.Database.ExecuteSqlAsync($"UPDATE material_entries SET unit = 'كيس' WHERE id = {stored}", Ct);
        }

        (await MineAsync(project)).GetProperty("materials")[0].GetProperty("unit").GetString().Should().Be("كيس");
    }

    // ---- S-6: a quantity line names its باب -----------------------------------------------------------------------

    [Fact]
    public async Task Finding_S6_a_quantity_line_in_the_log_read_carries_its_bab_name()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        (await Quantity(project, line, 4m, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await MineAsync(project)).GetProperty("quantities")[0].GetProperty("babName").GetString().Should().Be("أعمال خرسانة");
    }

    // ---- KAFF-614 AC-13: audit ------------------------------------------------------------------------------------

    [Fact]
    public async Task AC_614_13_a_correction_writes_an_audit_record_for_the_entry_and_for_the_boq_line()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        Guid saved = await SavedQuantityAsync(project, line, 40m, At(9, 0));
        Guid correction = Guid.NewGuid();

        (await CorrectQty(project, saved, 30m, _engineerA, correction)).StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext reader = _database.CreateBareContext();
        AuditRecord entry = await reader.AuditRecords.SingleAsync(
            r => r.EntityType == nameof(ExecutedQuantityEntry) && r.EntityId == correction && r.Action == AuditAction.Created, Ct);
        entry.ActorUserId.Should().Be(_engineerA);

        (await reader.AuditRecords.AnyAsync(
            r => r.EntityId == line && r.CorrelationId == entry.CorrelationId && r.Action == AuditAction.Modified, Ct))
            .Should().BeTrue("the line moved in the same save, and that is audited too");
    }

    [Fact]
    public async Task AC_614_13_a_stoppage_reason_and_a_second_note_are_audited()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid reason = Guid.NewGuid();

        (await SendAsync(HttpMethod.Post, $"{Logs(project)}/fields", _engineerA,
            new { entryId = reason, field = "StoppageReason", text = "Rain", occurredAt = At(9, 0) })).StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.AuditRecords.CountAsync(
            r => r.EntityType == nameof(FieldEntry) && r.EntityId == reason && r.Action == AuditAction.Created, Ct)).Should().Be(1);
    }

    // ---- KAFF-615 AC-1, AC-2: crew hours -------------------------------------------------------------------------

    [Theory]
    [InlineData("16.5")]
    [InlineData("17")]
    [InlineData("24")]
    [InlineData("7.25")]
    [InlineData("0")]
    [InlineData("-8")]
    public async Task AC_615_1_hours_above_16_or_off_the_half_hour_are_refused_and_nothing_is_saved(string hours)
    {
        (Guid project, _) = await NewActiveProjectAsync();

        HttpResponseMessage response = await Crew(project, decimal.Parse(hours, System.Globalization.CultureInfo.InvariantCulture), At(9, 0), _engineerA);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.daily_log.hours_out_of_range");
        (await RowsAsync("day_labour_entries", project)).Should().Be(0);
    }

    [Theory]
    [InlineData("0.5")]
    [InlineData("8")]
    [InlineData("15.5")]
    [InlineData("16")]
    public async Task AC_615_1_hours_within_the_rule_are_saved(string hours)
    {
        (Guid project, _) = await NewActiveProjectAsync();

        (await Crew(project, decimal.Parse(hours, System.Globalization.CultureInfo.InvariantCulture), At(9, 0), _engineerA))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AC_615_1_one_man_is_capped_at_16_hours_for_the_day_across_entries_and_a_refused_value_saves_nothing()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        (await Crew(project, 10m, At(9, 0), _engineerA)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Crew(project, 6.5m, At(11, 0), _engineerA)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RowsAsync("day_labour_entries", project)).Should().Be(1);
        (await Crew(project, 6m, At(11, 0), _engineerA)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AC_615_2_crew_hours_are_entered_for_today_or_up_to_three_days_back_under_the_check_in_rule()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        (await Crew(project, 8m, Dated(1), _engineerA)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Crew(project, 8m, Dated(3), _engineerA)).StatusCode.Should().Be(HttpStatusCode.Created);
        HttpResponseMessage tooOld = await Crew(project, 8m, Dated(4), _engineerA);
        tooOld.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(tooOld)).Should().Be("errors.daily_log.entry_outside_window");

        (await NoCheckIn("day-labour", project, Guid.Empty, _engineerB, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await NoCheckIn("day-labour", project, Guid.Empty, _engineerB, Dated(1))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---- KAFF-615 AC-3, AC-4, AC-5, AC-11: who edits which day ----------------------------------------------------

    [Fact]
    public async Task AC_615_3_the_crew_of_today_is_edited_by_the_engineer_who_entered_it_and_not_by_the_supervising_engineer()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid saved = await SavedCrewAsync(project, 8m, At(9, 0), _engineerA);

        foreach (Guid refused in new[] { _supervisor, _engineerB, _engineerUnassigned, _finance })
        {
            (await CorrectCrew(project, saved, 6m, refused)).StatusCode.Should().Be(HttpStatusCode.Forbidden, refused.ToString());
        }

        (await CorrectCrew(project, saved, 6m, _engineerA)).StatusCode.Should().Be(HttpStatusCode.Created);
        Num((await MineAsync(project)).GetProperty("dayLabour")[0].GetProperty("hours")).Should().Be(6m);
    }

    [Fact]
    public async Task AC_615_4_an_earlier_open_day_is_edited_by_the_supervising_engineer_and_is_read_only_for_the_author()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid saved = await SavedCrewAsync(project, 8m, Dated(2), _engineerA);

        (await CorrectCrew(project, saved, 6m, _engineerA)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CorrectCrew(project, saved, 6m, _engineerB)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CorrectCrew(project, saved, 6m, _supervisor)).StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement line = (await ReviewAsync(project, "2026-09-23", _supervisor, _engineerA)).GetProperty("dayLabour")[0];
        Num(line.GetProperty("hours")).Should().Be(6m);
        line.GetProperty("correctedByName").GetString().Should().Be("dl-sup");
    }

    [Fact]
    public async Task AC_615_5_the_owner_edits_the_crew_of_any_open_day_and_of_no_other()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid today = await SavedCrewAsync(project, 8m, At(9, 0), _engineerA);
        Guid back = await SavedCrewAsync(project, 8m, Dated(3), _engineerA, null);

        (await CorrectCrew(project, today, 7m, _owner)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await CorrectCrew(project, back, 7m, _owner)).StatusCode.Should().Be(HttpStatusCode.Created);

        _clock.Advance(TimeSpan.FromDays(1));
        HttpResponseMessage closed = await CorrectCrew(project, back, 6m, _owner);
        closed.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(closed)).Should().Be("errors.daily_log.correction_window_closed");
    }

    [Fact]
    public async Task AC_615_11_a_crew_edit_is_a_new_signed_delta_never_an_update_and_it_is_audited()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid saved = await SavedCrewAsync(project, 8m, At(9, 0), _engineerA);
        Guid correction = Guid.NewGuid();

        (await CorrectCrew(project, saved, 5.5m, _engineerA, correction)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await CorrectCrew(project, saved, 5.5m, _engineerA, correction)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        List<DayLabourEntry> rows = await reader.DayLabourEntries.AsNoTracking().Where(entry => entry.ProjectId == project).ToListAsync(Ct);
        rows.Should().HaveCount(2);
        rows.Single(entry => entry.Id == saved).Hours.Should().Be(8m);
        DayLabourEntry delta = rows.Single(entry => entry.Id == correction);
        delta.Hours.Should().Be(-2.5m);
        delta.CorrectsEntryId.Should().Be(saved);
        delta.CorrectedByUserId.Should().Be(_engineerA);

        (await reader.AuditRecords.CountAsync(
            r => r.EntityType == nameof(DayLabourEntry) && r.EntityId == correction && r.Action == AuditAction.Created && r.ActorUserId == _engineerA, Ct))
            .Should().Be(1);
    }

    [Fact]
    public async Task AC_615_11_a_crew_edit_obeys_the_hours_rule_and_saves_nothing_when_refused()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid saved = await SavedCrewAsync(project, 8m, At(9, 0), _engineerA);

        foreach (decimal bad in new[] { 16.5m, 24m, 7.25m, 0m })
        {
            HttpResponseMessage response = await CorrectCrew(project, saved, bad, _engineerA);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, bad.ToString());
            (await MessageKeyAsync(response)).Should().Be("errors.daily_log.hours_out_of_range");
        }

        (await CorrectCrew(project, saved, 8m, _engineerA)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RowsAsync("day_labour_entries", project)).Should().Be(1);
    }

    [Fact]
    public async Task AC_615_a_crew_edit_of_a_stopped_or_terminated_project_follows_the_log_state_ruling()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid saved = await SavedCrewAsync(project, 8m, At(9, 0), _engineerA);
        await SetStatusAsync(project, ProjectStatus.Terminated);

        HttpResponseMessage response = await CorrectCrew(project, saved, 6m, _engineerA);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.daily_log.project_state_closed_to_logs");
    }

    // ---- permissions on the new routes: one refusal per role that must be refused -----------------------------------

    [Fact]
    public async Task The_new_routes_refuse_every_caller_the_catalogue_does_not_grant()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        Guid quantity = await SavedQuantityAsync(project, line, 5m, At(9, 0));
        Guid crew = await SavedCrewAsync(project, 8m, At(9, 0), _engineerA);

        // Quantity correction: owner and finance hold no DailyLogWrite, and an unassigned engineer has no assignment.
        foreach (Guid refused in new[] { _owner, _finance, _engineerUnassigned })
        {
            (await CorrectQty(project, quantity, 3m, refused)).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"quantity {refused}");
        }

        // Crew correction: finance, and the unassigned engineer.
        foreach (Guid refused in new[] { _finance, _engineerUnassigned })
        {
            (await CorrectCrew(project, crew, 3m, refused)).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"crew {refused}");
        }

        // The review read: junior, unassigned, finance.
        foreach (Guid refused in new[] { _engineerA, _engineerUnassigned, _finance })
        {
            (await SendAsync(HttpMethod.Get, $"{Logs(project)}?date=2026-09-25", refused, null)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden, $"review {refused}");
        }

        (await ExecutedAsync(line)).Should().Be(5m);
        (await RowsAsync("day_labour_entries", project)).Should().Be(1);
    }
}
