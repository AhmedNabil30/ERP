using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Tests.Features.DailyLogs;

/// <summary>
/// KAFF-600, the daily log core (spec.md section 8). One test group per acceptance criterion A to I; the API
/// half of J (a money-free read model) is asserted here too. K is a frontend criterion and is not built.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class DailyLogTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _owner;
    private Guid _finance;
    private Guid _engineerA;
    private Guid _engineerB;
    private Guid _engineerUnassigned;
    private Guid _itemId;
    private Guid _workerId;

    public DailyLogTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await SeedAsync();
        _factory = new KaffApiFactory(_database.ConnectionString);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- AC-600-A -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC_600_A_opening_the_log_twice_for_one_day_reaches_the_same_log()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        HttpResponseMessage first = await SendAsync(HttpMethod.Post, Logs(project), _engineerA, new { occurredAt = At(9, 0) });
        HttpResponseMessage second = await SendAsync(HttpMethod.Post, Logs(project), _engineerA, new { occurredAt = At(17, 30) });

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await IdOfAsync(first)).Should().Be(await IdOfAsync(second));
        (await CountAsync("daily_logs", project)).Should().Be(1);
    }

    // ---- AC-600-B -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC_600_B_a_site_engineer_not_assigned_to_the_project_is_refused_on_every_log_endpoint()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        foreach ((HttpMethod method, string route, object? body) in AllCalls(project, line))
        {
            (await SendAsync(method, route, _engineerUnassigned, body)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden, $"{method} {route}");
        }

        (await CountAsync("daily_logs", project)).Should().Be(0);
    }

    [Fact]
    public async Task AC_600_B_roles_other_than_site_engineer_are_refused_on_every_log_endpoint()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        foreach (Guid refused in new[] { _owner, _finance })
        {
            foreach ((HttpMethod method, string route, object? body) in AllCalls(project, line))
            {
                (await SendAsync(method, route, refused, body)).StatusCode
                    .Should().Be(HttpStatusCode.Forbidden, $"{method} {route}");
            }
        }
    }

    // ---- AC-600-C -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC_600_C_two_entries_on_one_line_add_and_neither_replaces_the_other()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        (await Quantity(project, line, 40m, "2026-09-25T09:00:00+00:00")).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Quantity(project, line, 25m, "2026-09-26T15:00:00+00:00")).StatusCode.Should().Be(HttpStatusCode.Created);

        (await ExecutedAsync(line)).Should().Be(65m);
    }

    [Fact]
    public async Task AC_600_C_a_second_entry_by_the_same_engineer_on_the_same_day_and_line_also_adds()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        (await Quantity(project, line, 40m, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Quantity(project, line, 25m, At(15, 0))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await ExecutedAsync(line)).Should().Be(65m, "RulingSet.DailyLogQuantityEntry: a quantity is a delta, so it adds");
    }

    [Fact]
    public async Task AC_600_C_a_replayed_entry_does_not_count_twice()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        Guid entryId = Guid.NewGuid();

        (await Quantity(project, line, 40m, At(9, 0), entryId)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Quantity(project, line, 40m, At(9, 0), entryId)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ExecutedAsync(line)).Should().Be(40m, "a sync retry is the same entry, not more work");
    }

    [Fact]
    public async Task AC_600_C_concurrent_entries_on_one_line_all_land()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(index =>
                Quantity(project, line, 1m, At(9, index), actor: index % 2 == 0 ? _engineerA : _engineerB)));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Created);
        (await ExecutedAsync(line)).Should().Be(8m, "an additive total must not lose a concurrent update");
    }

    [Fact]
    public async Task AC_600_C_a_zero_or_negative_quantity_is_refused()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        (await Quantity(project, line, 0m, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Quantity(project, line, -5m, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ExecutedAsync(line)).Should().Be(0m);
    }

    // ---- AC-600-D -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC_600_D_two_engineers_on_the_same_day_sum_in_separate_logs()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        (await Quantity(project, line, 10m, At(9, 0), actor: _engineerA)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Quantity(project, line, 7m, At(9, 5), actor: _engineerB)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await ExecutedAsync(line)).Should().Be(17m);
        (await CountAsync("daily_logs", project)).Should().Be(2, "one log per engineer per project per day");
    }

    // ---- AC-600-E -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC_600_E_the_later_timestamp_wins_for_a_single_valued_field()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        (await Field(project, "Weather", "sunny", At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Field(project, "Weather", "rain", At(11, 0))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await MineAsync(project)).GetProperty("weather").GetString().Should().Be("rain");
    }

    [Fact]
    public async Task AC_600_E_arrival_order_does_not_decide_the_winner_the_entry_time_does()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        (await Field(project, "Note", "later note", At(14, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Field(project, "Note", "earlier note, synced second", At(8, 0))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await MineAsync(project)).GetProperty("note").GetString().Should().Be("later note");
    }

    // ---- AC-600-F -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC_600_F_the_executed_quantity_moves_on_a_frozen_line_and_every_other_column_stays_refused()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        (await Quantity(project, line, 12.5m, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await ExecutedAsync(line)).Should().Be(12.5m);

        await using KaffDbContext writer = _database.CreateBareContext();
        Exception? thrown = null;

        try
        {
            await writer.Database.ExecuteSqlAsync($"UPDATE boq_lines SET description_ar = 'changed' WHERE id = {line}", Ct);
        }
        catch (Exception exception)
        {
            thrown = exception;
        }

        thrown.Should().NotBeNull();
        FindPostgres(thrown!).MessageText.Should().Contain("KAFF_BOQ_FROZEN");
    }

    [Fact]
    public async Task AC_600_F_a_quantity_against_a_line_of_another_project_is_refused()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        (_, Guid foreignLine) = await NewActiveProjectAsync();

        HttpResponseMessage response = await Quantity(project, foreignLine, 5m, At(9, 0));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ExecutedAsync(foreignLine)).Should().Be(0m);
    }

    [Fact]
    public async Task AC_600_F_a_quantity_against_an_unfrozen_boq_is_refused()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync(sign: false);

        HttpResponseMessage response = await Quantity(project, line, 5m, At(9, 0));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.daily_log.boq_not_frozen");
        (await ExecutedAsync(line)).Should().Be(0m);
    }

    // ---- AC-600-G -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC_600_G_a_stopped_project_accepts_an_entry_naming_the_stoppage_and_its_reason()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        await SetStatusAsync(project, ProjectStatus.Stopped);

        HttpResponseMessage response = await Field(project, "StoppageReason", "Client has not released the site", At(9, 0));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await MineAsync(project)).GetProperty("stoppageReason").GetString().Should().Contain("released the site");
    }

    [Fact]
    public async Task AC_600_G_a_terminated_project_takes_no_log()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        await SetStatusAsync(project, ProjectStatus.Terminated);

        HttpResponseMessage response = await Field(project, "Note", "x", At(9, 0));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.daily_log.project_state_closed_to_logs");
    }

    // ---- AC-600-H -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC_600_H_an_engineers_expense_is_stored_as_a_draft_and_writes_no_posting()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        long postingsBefore = await TotalPostingsAsync();

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, $"{Logs(project)}/expense-drafts", _engineerA,
            new { entryId = Guid.NewGuid(), description = "Fuel for the mixer", amount = "350.0000", occurredAt = At(10, 0) });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await CountAsync("site_expense_drafts", project)).Should().Be(1);
        (await TotalPostingsAsync()).Should().Be(postingsBefore, "the engineer never posts money");
    }

    [Fact]
    public async Task AC_600_J_an_expense_draft_read_back_by_the_engineer_carries_no_amount()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        (await SendAsync(
            HttpMethod.Post, $"{Logs(project)}/expense-drafts", _engineerA,
            new { entryId = Guid.NewGuid(), description = "Fuel for the mixer", amount = "350.0000", occurredAt = At(10, 0) }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement drafts = (await MineAsync(project)).GetProperty("expenseDrafts");

        drafts.GetArrayLength().Should().Be(1);
        drafts[0].EnumerateObject().Select(member => member.Name)
            .Should().BeEquivalentTo(["entryId", "description", "occurredAt"]);
        drafts.GetRawText().Should().NotContain("350");
    }

    [Fact]
    public async Task AC_600_H_a_zero_expense_is_refused()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, $"{Logs(project)}/expense-drafts", _engineerA,
            new { entryId = Guid.NewGuid(), description = "x", amount = "0", occurredAt = At(10, 0) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CountAsync("site_expense_drafts", project)).Should().Be(0);
    }

    // ---- AC-600-I -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task AC_600_I_an_entry_made_at_2350_belongs_to_the_day_it_occurred_even_when_it_syncs_after_midnight()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();

        // 23:50 local (+02:00) on the 25th. The API clock is the real now, which is never that day: the
        // entry arrived "later", and still belongs to the 25th.
        HttpResponseMessage response = await Quantity(project, line, 9m, "2026-09-25T23:50:00+02:00");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await MineAsync(project, "2026-09-25")).GetProperty("quantities").GetArrayLength().Should().Be(1);

        HttpResponseMessage nextDay = await SendAsync(HttpMethod.Get, $"{Logs(project)}/mine?date=2026-09-26", _engineerA, null);
        nextDay.StatusCode.Should().Be(HttpStatusCode.NotFound, "the entry did not occur on the 26th");

        await using KaffDbContext reader = _database.CreateBareContext();
        string logDate = await reader.Database
            .SqlQuery<string>($"SELECT log_date::text AS \"Value\" FROM daily_logs WHERE project_id = {project}")
            .SingleAsync(Ct);
        logDate.Should().Be("2026-09-25");
    }

    [Fact]
    public async Task AC_600_I_every_entry_keeps_its_own_occurred_at_apart_from_the_sync_time()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        (await Quantity(project, line, 3m, "2026-09-25T08:15:00+02:00")).StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext reader = _database.CreateBareContext();
        DateTime occurred = await reader.Database
            .SqlQuery<DateTime>($"SELECT occurred_at AT TIME ZONE 'UTC' AS \"Value\" FROM executed_quantity_entries WHERE project_id = {project}")
            .SingleAsync(Ct);
        DateTime created = await reader.Database
            .SqlQuery<DateTime>($"SELECT created_at AT TIME ZONE 'UTC' AS \"Value\" FROM executed_quantity_entries WHERE project_id = {project}")
            .SingleAsync(Ct);

        occurred.Should().Be(new DateTime(2026, 9, 25, 6, 15, 0));
        created.Should().BeAfter(occurred, "CreatedAt is the sync time and OccurredAt is the entry's own");
    }

    [Fact]
    public async Task AC_600_I_every_field_entity_has_an_occurred_at_column_apart_from_created_at()
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        foreach (string table in new[]
        {
            "executed_quantity_entries", "day_labour_entries", "material_entries", "photo_entries",
            "field_entries", "site_expense_drafts",
        })
        {
            int columns = await reader.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM information_schema.columns WHERE table_name = {table} AND column_name IN ('occurred_at', 'created_at')")
                .SingleAsync(Ct);
            columns.Should().Be(2, table);
        }
    }

    // ---- AC-600-J (API half) ------------------------------------------------------------------------------------

    [Fact]
    public async Task AC_600_J_the_engineers_read_model_carries_no_money_by_construction()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        (await Quantity(project, line, 4m, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, $"{Logs(project)}/day-labour", _engineerA,
            new { entryId = Guid.NewGuid(), employeeId = _workerId, hours = 8m, occurredAt = At(9, 0) })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, $"{Logs(project)}/materials", _engineerA,
            new { entryId = Guid.NewGuid(), materialType = "Cement", quantity = 30m, unit = "bag", isStockpile = true, occurredAt = At(9, 0) })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, $"{Logs(project)}/photos", _engineerA,
            new { entryId = Guid.NewGuid(), fileReference = "photos/a.jpg", caption = "slab", occurredAt = At(9, 0) })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, $"{Logs(project)}/expense-drafts", _engineerA,
            new { entryId = Guid.NewGuid(), description = "Fuel", amount = "350.0000", occurredAt = At(9, 0) })).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage read = await SendAsync(HttpMethod.Get, $"{Logs(project)}/mine?date=2026-09-25", _engineerA, null);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        string json = await read.Content.ReadAsStringAsync(Ct);

        string[] names = [.. Regex.Matches(json, "\"([A-Za-z]+)\"\\s*:").Select(match => match.Groups[1].Value)];
        names.Should().NotBeEmpty();
        names.Where(name => Regex.IsMatch(name, "rate|amount|cost|price|balance|money|total|value", RegexOptions.IgnoreCase))
            .Should().BeEmpty("the engineer's screen shows quantities, hours, counts, photos and materials by type");
        json.Should().NotContain("350.0000", "the drafted amount does not come back to the engineer's screen");
    }

    [Fact]
    public async Task AC_600_J_the_engineers_read_is_refused_to_an_unassigned_engineer()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        (await SendAsync(HttpMethod.Get, $"{Logs(project)}/mine?date=2026-09-25", _engineerUnassigned, null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- KAFF-613: check-in and check-out with location ---------------------------------------------------------------

    [Fact]
    public async Task AC_613_C_a_located_check_in_stores_the_location_the_device_time_and_the_server_time_apart()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid entryId = Guid.NewGuid();
        DateTimeOffset before = DateTimeOffset.UtcNow;

        HttpResponseMessage response = await CheckIn(project, "CheckIn", 30.0444, 31.2357, At(8, 0), 12.5, entryId);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement line = (await MineAsync(project)).GetProperty("checkIns").EnumerateArray().Single();
        line.GetProperty("entryId").GetGuid().Should().Be(entryId);
        line.GetProperty("kind").GetString().Should().Be("CheckIn");
        line.GetProperty("latitude").GetDouble().Should().Be(30.0444);
        line.GetProperty("longitude").GetDouble().Should().Be(31.2357);
        line.GetProperty("accuracyMetres").GetDouble().Should().Be(12.5);
        line.GetProperty("flagReason").ValueKind.Should().Be(JsonValueKind.Null);
        line.GetProperty("deviceTime").GetDateTimeOffset().Should().Be(DateTimeOffset.Parse(At(8, 0), System.Globalization.CultureInfo.InvariantCulture));
        line.GetProperty("serverTime").GetDateTimeOffset().Should().BeOnOrAfter(before, "the server stamps its own clock, not the device's");
        line.GetProperty("serverTime").GetDateTimeOffset().Should().NotBe(line.GetProperty("deviceTime").GetDateTimeOffset());
    }

    [Fact]
    public async Task AC_613_D_a_check_in_without_location_is_recorded_and_flagged_never_refused()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        (await CheckIn(project, "CheckIn", null, null, At(8, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        // Latitude without longitude, and coordinates off the planet, are no location either.
        (await CheckIn(project, "CheckOut", 30.0, null, At(15, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await CheckIn(project, "CheckOut", 123.0, 31.0, At(16, 0))).StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement[] lines = (await MineAsync(project)).GetProperty("checkIns").EnumerateArray().ToArray();
        lines.Should().HaveCount(3);
        lines.Should().OnlyContain(line => line.GetProperty("flagReason").GetString() == "LocationUnavailable");
        lines.Should().OnlyContain(line => line.GetProperty("latitude").ValueKind == JsonValueKind.Null);
        (await LoadProjectAsync(project)).SiteReference.Should().BeNull("an unlocated check-in sets no reference point");
    }

    [Fact]
    public async Task AC_613_E_the_first_located_check_in_sets_the_reference_point_and_a_far_one_is_flagged_not_refused()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        (await CheckIn(project, "CheckIn", null, null, At(7, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await CheckIn(project, "CheckIn", 30.0444, 31.2357, At(8, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        // About 200 m north: inside the 500 m threshold.
        (await CheckIn(project, "CheckIn", 30.0462, 31.2357, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        // About 11 km north: far.
        (await CheckIn(project, "CheckIn", 30.1444, 31.2357, At(10, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        // A check-out is measured from the same point.
        (await CheckIn(project, "CheckOut", 30.1444, 31.2357, At(17, 0))).StatusCode.Should().Be(HttpStatusCode.Created);

        (double Latitude, double Longitude)? reference = (await LoadProjectAsync(project)).SiteReference;
        reference.Should().Be((30.0444, 31.2357), "the first located check-in sets it and a later one never moves it");

        string?[] flags = (await MineAsync(project)).GetProperty("checkIns").EnumerateArray()
            .OrderBy(line => line.GetProperty("deviceTime").GetDateTimeOffset())
            .Select(line => line.GetProperty("flagReason").GetString())
            .ToArray();
        flags.Should().Equal("LocationUnavailable", null, null, "FarFromReference", "FarFromReference");
    }

    [Fact]
    public async Task AC_613_H_an_engineer_not_assigned_to_the_project_cannot_check_in()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        HttpResponseMessage response = await SendAsync(
            HttpMethod.Post, $"{Logs(project)}/check-ins", _engineerUnassigned,
            new { entryId = Guid.NewGuid(), kind = "CheckIn", latitude = 30.0, longitude = 31.0, occurredAt = At(8, 0) });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await LoadProjectAsync(project)).SiteReference.Should().BeNull();
        (await CountAsync("field_entries", project)).Should().Be(0);
    }

    [Fact]
    public async Task AC_613_H_finance_cannot_check_in()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        (await SendAsync(
            HttpMethod.Post, $"{Logs(project)}/check-ins", _finance,
            new { entryId = Guid.NewGuid(), kind = "CheckIn", latitude = 30.0, longitude = 31.0, occurredAt = At(8, 0) }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AC_613_the_kind_must_be_check_in_or_check_out_and_a_retried_entry_id_is_not_counted_twice()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid entryId = Guid.NewGuid();

        (await CheckIn(project, "Weather", 30.0, 31.0, At(8, 0))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CheckIn(project, "CheckIn", 30.0, 31.0, At(8, 0), null, entryId)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await CheckIn(project, "CheckIn", 30.0, 31.0, At(8, 0), null, entryId)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await CountAsync("field_entries", project)).Should().Be(1);
    }

    [Fact]
    public async Task AC_613_J_recording_a_check_in_writes_an_audit_record_and_audits_the_reference_point()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid entryId = Guid.NewGuid();

        (await CheckIn(project, "CheckIn", 30.0444, 31.2357, At(8, 0), null, entryId)).StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext reader = _database.CreateBareContext();
        AuditRecord entry = await reader.AuditRecords.SingleAsync(
            r => r.EntityType == nameof(FieldEntry) && r.EntityId == entryId && r.Action == AuditAction.Created, Ct);
        entry.ActorUserId.Should().Be(_engineerA);

        AuditRecord projectChange = await reader.AuditRecords.SingleAsync(
            r => r.EntityType == nameof(Project) && r.EntityId == project && r.CorrelationId == entry.CorrelationId, Ct);
        projectChange.ChangedProperties.Should().Contain(["SiteLatitude", "SiteLongitude"]);
    }

    [Fact]
    public async Task The_older_fields_route_still_takes_a_check_in_and_flags_it_for_having_no_location()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        (await Field(project, "CheckIn", null, At(8, 0))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await MineAsync(project)).GetProperty("checkIns").EnumerateArray().Single()
            .GetProperty("flagReason").GetString().Should().Be("LocationUnavailable");
    }

    // ---- helpers ------------------------------------------------------------------------------------------------

    private Task<HttpResponseMessage> CheckIn(
        Guid project, string kind, double? latitude, double? longitude, string occurredAt, double? accuracy = null, Guid? entryId = null) =>
        SendAsync(
            HttpMethod.Post, $"{Logs(project)}/check-ins", _engineerA,
            new { entryId = entryId ?? Guid.NewGuid(), kind, latitude, longitude, accuracyMetres = accuracy, occurredAt });

    private async Task<Project> LoadProjectAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Projects.SingleAsync(candidate => candidate.Id == id, Ct);
    }

    private static string Logs(Guid project) => $"/api/projects/{project}/daily-logs";

    private static string At(int hour, int minute) => $"2026-09-25T{hour:00}:{minute:00}:00+00:00";

    private IEnumerable<(HttpMethod, string, object?)> AllCalls(Guid project, Guid line)
    {
        string occurred = At(9, 0);
        yield return (HttpMethod.Post, Logs(project), new { occurredAt = occurred });
        yield return (HttpMethod.Post, $"{Logs(project)}/quantities", new { entryId = Guid.NewGuid(), boqLineId = line, quantity = 1m, occurredAt = occurred });
        yield return (HttpMethod.Post, $"{Logs(project)}/day-labour", new { entryId = Guid.NewGuid(), employeeId = _workerId, hours = 8m, occurredAt = occurred });
        yield return (HttpMethod.Post, $"{Logs(project)}/materials", new { entryId = Guid.NewGuid(), materialType = "Cement", quantity = 1m, unit = "bag", isStockpile = false, occurredAt = occurred });
        yield return (HttpMethod.Post, $"{Logs(project)}/photos", new { entryId = Guid.NewGuid(), fileReference = "p.jpg", occurredAt = occurred });
        yield return (HttpMethod.Post, $"{Logs(project)}/check-ins", new { entryId = Guid.NewGuid(), kind = "CheckIn", latitude = 30.0, longitude = 31.0, occurredAt = occurred });
        yield return (HttpMethod.Post, $"{Logs(project)}/fields", new { entryId = Guid.NewGuid(), field = "Note", text = "x", occurredAt = occurred });
        yield return (HttpMethod.Post, $"{Logs(project)}/expense-drafts", new { entryId = Guid.NewGuid(), description = "x", amount = "1", occurredAt = occurred });
        yield return (HttpMethod.Get, $"{Logs(project)}/mine?date=2026-09-25", null);
    }

    private Task<HttpResponseMessage> Quantity(
        Guid project, Guid line, decimal quantity, string occurredAt, Guid? entryId = null, Guid? actor = null) =>
        SendAsync(
            HttpMethod.Post, $"{Logs(project)}/quantities", actor ?? _engineerA,
            new { entryId = entryId ?? Guid.NewGuid(), boqLineId = line, quantity, occurredAt });

    private Task<HttpResponseMessage> Field(Guid project, string field, string? text, string occurredAt) =>
        SendAsync(
            HttpMethod.Post, $"{Logs(project)}/fields", _engineerA,
            new { entryId = Guid.NewGuid(), field, text, occurredAt });

    private async Task<JsonElement> MineAsync(Guid project, string date = "2026-09-25")
    {
        HttpResponseMessage read = await SendAsync(HttpMethod.Get, $"{Logs(project)}/mine?date={date}", _engineerA, null);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument document = JsonDocument.Parse(await read.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private static async Task<Guid> IdOfAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<decimal> ExecutedAsync(Guid line)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Database
            .SqlQuery<decimal>($"SELECT quantity_executed AS \"Value\" FROM boq_lines WHERE id = {line}")
            .SingleAsync(Ct);
    }

    private async Task<int> CountAsync(string table, Guid project)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
#pragma warning disable EF1002 // The table name is a literal from this file, never user input.
        return await reader.Database
            .SqlQueryRaw<int>($"SELECT count(*)::int AS \"Value\" FROM {table} WHERE project_id = '{project}'")
            .SingleAsync(Ct);
#pragma warning restore EF1002
    }

    private async Task<long> TotalPostingsAsync()
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Database.SqlQuery<long>($"SELECT count(*) AS \"Value\" FROM postings").SingleAsync(Ct);
    }

    private async Task SetStatusAsync(Guid project, ProjectStatus status)
    {
        await using KaffDbContext context = _database.CreateContext();
        Project loaded = await context.Projects.SingleAsync(candidate => candidate.Id == project, Ct);
        loaded.TransitionTo(status, new DateOnly(2026, 9, 25), "test").IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(Ct);
    }

    private async Task<(Guid Project, Guid Line)> NewActiveProjectAsync(bool sign = true)
    {
        Guid project;

        await using (KaffDbContext context = _database.CreateContext())
        {
            Project created = Project.Create(UniqueNames.Code("DL-P"), "مشروع اليومية", _clientId, ContractType.LumpSum, Now).Value;
            created.SetLumpSumTerms(
                Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
                Percentage.FromPercent(75m), delayPenaltyEnabled: false).IsSuccess.Should().BeTrue();
            context.Projects.Add(created);
            await context.SaveChangesAsync(Ct);

            foreach (Guid engineer in new[] { _engineerA, _engineerB })
            {
                User user = await context.Users.SingleAsync(candidate => candidate.Id == engineer, Ct);
                context.ProjectAssignments.Add(
                    ProjectAssignment.Create(created.Id, user, AssignmentLevel.Junior, _owner, Now).Value);
            }

            await context.SaveChangesAsync(Ct);
            project = created.Id;
        }

        HttpResponseMessage added = await SendAsync(
            HttpMethod.Post, $"/api/projects/{project}/boq/lines", _owner,
            new { catalogueItemId = _itemId, quantityEstimated = 100m, quantitySurveyed = 100m });
        added.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await added.Content.ReadAsStringAsync(Ct));
        Guid line = body.RootElement.GetProperty("id").GetGuid();

        if (sign)
        {
            (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/sign", _owner, new { signedOn = "2026-09-24" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await using KaffDbContext activate = _database.CreateContext();
        Project loaded = await activate.Projects.SingleAsync(candidate => candidate.Id == project, Ct);
        loaded.TransitionTo(ProjectStatus.Active, new DateOnly(2026, 9, 24)).IsSuccess.Should().BeTrue();
        await activate.SaveChangesAsync(Ct);

        return (project, line);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Guid actorId, object? body)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        await using KaffDbContext reader = _database.CreateBareContext();
        var found = await reader.Users
            .Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role })
            .FirstAsync(Ct);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, found.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, found.Role.ToString());

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;
    }

    private static PostgresException FindPostgres(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        throw new Xunit.Sdk.XunitException($"No PostgresException in the chain: {exception}");
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("DL-C"), "عميل اليومية", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        User owner = MakeUser("dl-owner", Role.Owner);
        User finance = MakeUser("dl-finance", Role.Finance, WellKnownDepartments.FinanceId);
        User engineerA = MakeEngineer("dl-eng-a");
        User engineerB = MakeEngineer("dl-eng-b");
        User engineerUnassigned = MakeEngineer("dl-eng-un");

        Bab bab = Bab.Create(UniqueNames.Code("DLB"), "أعمال خرسانة", "Concrete", Percentage.FromPercent(15m)).Value;
        CatalogueItem item = CatalogueItem.Create(
            UniqueNames.Code("DL-CON"), "خرسانة مسلحة", "م٣", bab.Id, new Money(1_000m), new Money(1_500m), "Reinforced concrete").Value;
        Employee worker = Employee.Create(
            UniqueNames.Code("DL-W"), "عامل يومية", UniqueNames.Phone(), EmployeeKind.DayLabour, Now, bab.Id).Value;

        context.Clients.Add(client);
        context.Users.AddRange(owner, finance, engineerA, engineerB, engineerUnassigned);
        context.Babs.Add(bab);
        context.CatalogueItems.Add(item);
        context.Employees.Add(worker);
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _owner = owner.Id;
        _finance = finance.Id;
        _engineerA = engineerA.Id;
        _engineerB = engineerB.Id;
        _engineerUnassigned = engineerUnassigned.Id;
        _itemId = item.Id;
        _workerId = worker.Id;
    }

    private static User MakeEngineer(string name) =>
        MakeUser(name, Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);

    private static User MakeUser(
        string userName, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
