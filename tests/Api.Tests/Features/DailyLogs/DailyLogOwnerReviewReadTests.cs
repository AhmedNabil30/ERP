using System.Net;
using System.Text.Json;

namespace Kaff.Api.Tests.Features.DailyLogs;

/// <summary>D-226: the review read gives the owner's daily log screen everything the approved mock shows.</summary>
public sealed partial class DailyLogTests
{
    private async Task<JsonElement> ReviewListAsync(Guid project, string date, Guid actor) =>
        await ReadAsync(await SendAsync(HttpMethod.Get, $"{Logs(project)}?date={date}", actor, null));

    private Task<HttpResponseMessage> CheckInAs(Guid actor, Guid project, string kind, double latitude, double longitude, string occurredAt) =>
        SendAsync(
            HttpMethod.Post, $"{Logs(project)}/check-ins", actor,
            new { entryId = Guid.NewGuid(), kind, latitude, longitude, occurredAt }, autoCheckIn: false);

    [Fact]
    public async Task D226_2_each_check_in_line_carries_its_whole_metre_distance_from_the_site_reference()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        (await CheckInAs(_engineerA, project, "CheckIn", 30.0444, 31.2357, At(8, 0))).StatusCode.Should().Be(HttpStatusCode.Created);
        // 0.01 degree of latitude is about 1112 m: far.
        (await CheckInAs(_engineerB, project, "CheckIn", 30.0544, 31.2357, At(9, 0))).StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement near = (await ReviewAsync(project, "2026-09-25", _owner, _engineerA)).GetProperty("checkIns")[0];
        JsonElement far = (await ReviewAsync(project, "2026-09-25", _owner, _engineerB)).GetProperty("checkIns")[0];

        near.GetProperty("distanceFromSiteMetres").GetInt32().Should().Be(0);
        far.GetProperty("flagReason").GetString().Should().Be("FarFromReference");
        far.GetProperty("distanceFromSiteMetres").GetInt32().Should().BeInRange(1100, 1125);

        JsonElement mine = (await MineAsync(project)).GetProperty("checkIns")[0];
        mine.GetProperty("distanceFromSiteMetres").GetInt32().Should().Be(0, "the own-log read shares the shape");
    }

    [Fact]
    public async Task D226_4_the_review_read_returns_any_past_day_and_an_empty_list_for_a_future_day()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        await SavedQuantityAsync(project, line, 40m, At(9, 0));
        _clock.Advance(TimeSpan.FromDays(30));

        foreach (Guid reader in new[] { _owner, _supervisor })
        {
            JsonElement past = await ReviewListAsync(project, "2026-09-25", reader);
            past.GetArrayLength().Should().Be(1, "reading has no entry window");
            past[0].GetProperty("log").GetProperty("quantities").GetArrayLength().Should().Be(1);
        }

        (await ReviewListAsync(project, "2027-06-01", _owner)).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task D226_3_each_engineer_log_carries_its_own_crew_with_name_trade_hours_and_no_money_or_contact()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid hoursEntry = await SavedCrewAsync(project, 8m, At(9, 0), _engineerA);
        await SavedCrewAsync(project, 6m, At(9, 0), _engineerB);
        (await CorrectCrew(project, hoursEntry, 7m, _engineerA)).StatusCode.Should().Be(HttpStatusCode.Created);

        foreach (Guid reader in new[] { _owner, _supervisor })
        {
            HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"{Logs(project)}?date=2026-09-25", reader, null);
            string raw = await response.Content.ReadAsStringAsync(Ct);
            using JsonDocument body = JsonDocument.Parse(raw);
            JsonElement logA = body.RootElement.EnumerateArray().Single(item => item.GetProperty("engineerId").GetGuid() == _engineerA).GetProperty("log");
            JsonElement logB = body.RootElement.EnumerateArray().Single(item => item.GetProperty("engineerId").GetGuid() == _engineerB).GetProperty("log");

            JsonElement man = logA.GetProperty("dayLabour").EnumerateArray().Single();
            man.GetProperty("workerName").GetString().Should().Be("عامل يومية");
            man.GetProperty("trade").GetString().Should().Be("أعمال خرسانة");
            Num(man.GetProperty("hours")).Should().Be(7m);
            man.GetProperty("corrected").GetBoolean().Should().BeTrue();
            Num(logB.GetProperty("dayLabour").EnumerateArray().Single().GetProperty("hours")).Should().Be(6m);

            foreach (string forbidden in new[] { "rate", "phone", "code", "money", "amount", "cost", "balance" })
            {
                raw.Should().NotContainEquivalentOf(forbidden, $"reader {reader}");
            }
        }
    }

    [Fact]
    public async Task D226_1_a_corrected_quantity_shows_the_figure_it_replaced_and_who_corrected_it()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        Guid saved = await SavedQuantityAsync(project, line, 40m, At(9, 0));
        Guid untouched = await SavedQuantityAsync(project, line, 5m, At(10, 0));
        (await CorrectQty(project, saved, 30m, _engineerA)).StatusCode.Should().Be(HttpStatusCode.Created);
        _clock.Advance(TimeSpan.FromDays(1));
        (await CorrectQty(project, saved, 25m, _supervisor)).StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement rows = (await ReviewAsync(project, "2026-09-25", _owner, _engineerA)).GetProperty("quantities");
        JsonElement corrected = rows.EnumerateArray().Single(row => row.GetProperty("entryId").GetGuid() == saved);
        JsonElement plain = rows.EnumerateArray().Single(row => row.GetProperty("entryId").GetGuid() == untouched);

        Num(corrected.GetProperty("quantity")).Should().Be(25m);
        Num(corrected.GetProperty("previousQuantity")).Should().Be(30m, "the figure in force before the latest correction");
        corrected.GetProperty("correctedByName").GetString().Should().Be("dl-sup");
        plain.GetProperty("previousQuantity").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task D226_5_an_engineer_who_checked_in_and_logged_nothing_still_appears_with_his_name_and_no_check_out_is_null()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        (await CheckInAs(_engineerB, project, "CheckIn", 30.0444, 31.2357, At(8, 0))).StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement list = await ReviewListAsync(project, "2026-09-25", _owner);
        JsonElement entry = list.EnumerateArray().Single(item => item.GetProperty("engineerId").GetGuid() == _engineerB);
        JsonElement log = entry.GetProperty("log");

        entry.GetProperty("engineerName").GetString().Should().Be("dl-eng-b");
        log.GetProperty("checkIn").ValueKind.Should().Be(JsonValueKind.String);
        log.GetProperty("checkOut").ValueKind.Should().Be(JsonValueKind.Null);
        log.GetProperty("backDated").GetBoolean().Should().BeFalse();
        log.GetProperty("quantities").GetArrayLength().Should().Be(0);
    }
}
