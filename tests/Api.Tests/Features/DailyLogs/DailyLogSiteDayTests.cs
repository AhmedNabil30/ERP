using System.Net;
using System.Net.Http;

namespace Kaff.Api.Tests.Features.DailyLogs;

/// <summary>
/// The day an entry belongs to is the calendar day of its instant in the site time zone, whatever offset the device wrote
/// (D-217, D-218). The server clock is moved to 22:30 UTC on 2026-09-25, which is 01:30 on the 26th in Cairo (+03:00 in September).
/// </summary>
public sealed partial class DailyLogTests
{
    private const string SiteToday = "2026-09-26";

    private void ClockAtTwentyTwoThirtyUtc() =>
        _clock.Advance(new DateTimeOffset(2026, 9, 25, 22, 30, 0, TimeSpan.Zero) - _clock.GetUtcNow());

    [Fact]
    public async Task A_check_in_sent_in_utc_after_2200_is_accepted_as_today_because_the_site_day_has_turned()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        ClockAtTwentyTwoThirtyUtc();

        HttpResponseMessage utc = await SendAsync(
            HttpMethod.Post, $"{Logs(project)}/check-ins", _engineerA,
            new { entryId = Guid.NewGuid(), kind = "CheckIn", latitude = 30.0444, longitude = 31.2357, occurredAt = "2026-09-25T22:30:00+00:00" },
            autoCheckIn: false);

        utc.StatusCode.Should().Be(HttpStatusCode.Created, "the same instant sent as +03:00 was already accepted");
    }

    [Fact]
    public async Task The_same_instant_gives_the_same_answer_whatever_offset_the_check_in_is_written_in()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        ClockAtTwentyTwoThirtyUtc();

        foreach (string written in new[] { "2026-09-25T22:30:00+00:00", "2026-09-26T00:30:00+02:00", "2026-09-26T01:30:00+03:00", "2026-09-25T19:30:00-03:00" })
        {
            (await SendAsync(
                HttpMethod.Post, $"{Logs(project)}/check-ins", _engineerA,
                new { entryId = Guid.NewGuid(), kind = "CheckIn", latitude = 30.0444, longitude = 31.2357, occurredAt = written },
                autoCheckIn: false)).StatusCode.Should().Be(HttpStatusCode.Created, written);
        }

        (await CountAsync("daily_logs", project)).Should().Be(1, "every one of them is the 26th in Cairo");
    }

    [Fact]
    public async Task A_quantity_sent_in_utc_after_2200_lands_on_the_cairo_day()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        ClockAtTwentyTwoThirtyUtc();
        (await CheckIn(project, "CheckIn", 30.0444, 31.2357, "2026-09-25T22:30:00+00:00")).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage response = await NoCheckIn("quantity", project, line, _engineerA, "2026-09-25T22:35:00+00:00");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await MineAsync(project, SiteToday)).GetProperty("quantities").GetArrayLength().Should().Be(1);
        (await CountAsync("daily_logs", project)).Should().Be(1, "no log was opened on the 25th");
    }

    [Theory]
    [InlineData("2026-09-22T22:00:00+00:00", HttpStatusCode.Created)] // 01:00 on the 23rd in Cairo: three days back
    [InlineData("2026-09-22T20:59:00+00:00", HttpStatusCode.Conflict)] // 23:59 on the 22nd in Cairo: four days back
    [InlineData("2026-09-23T01:00:00+03:00", HttpStatusCode.Created)] // same instant as the first, written in Cairo's offset
    public async Task The_three_day_window_is_counted_in_site_days(string occurredAt, HttpStatusCode expected)
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        ClockAtTwentyTwoThirtyUtc();
        (await CheckIn(project, "CheckIn", 30.0444, 31.2357, "2026-09-25T22:30:00+00:00")).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage response = await NoCheckIn("quantity", project, line, _engineerA, occurredAt);

        response.StatusCode.Should().Be(expected);
        if (expected == HttpStatusCode.Conflict)
        {
            (await MessageKeyAsync(response)).Should().Be("errors.daily_log.entry_outside_window");
        }
    }
}
