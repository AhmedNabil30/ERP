using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.DailyLogs;

/// <summary>
/// Read-side gaps for the site screens: <c>canCorrect</c> on every quantity and crew line (the same verdict the Correct endpoints
/// give, since both come from <c>DailyLogAccess.CorrectionRightsAsync</c>), and the project crew read.
/// </summary>
public sealed partial class DailyLogTests
{
    // A different figure per reader, so a correction to the figure already in force is never mistaken for a refusal.
    private decimal FigureFor(Guid reader) => reader == _engineerA ? 6m : reader == _supervisor ? 5m : 4m;

    // ---- canCorrect: the flag equals what the endpoint then does ------------------------------------------------

    [Fact]
    public async Task canCorrect_on_a_quantity_line_equals_what_the_correct_endpoint_does_for_every_reader()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        Guid todayEntry = await SavedQuantityAsync(project, line, 40m, At(9, 0));
        Guid earlierEntry = await SavedQuantityAsync(project, line, 20m, Dated(1));

        // Junior author reads his own log; supervising engineer and owner read the review list.
        foreach ((string day, Guid entry) in new[] { ("2026-09-25", todayEntry), (Yesterday, earlierEntry) })
        {
            foreach (Guid reader in new[] { _engineerA, _supervisor, _owner })
            {
                JsonElement log = reader == _engineerA
                    ? await MineAs(project, day, _engineerA)
                    : await ReviewAsync(project, day, reader, _engineerA);
                JsonElement row = log.GetProperty("quantities").EnumerateArray().Single(item => item.GetProperty("entryId").GetGuid() == entry);

                row.GetProperty("canCorrect").GetBoolean().Should().Be(
                    (await CorrectQty(project, entry, FigureFor(reader), reader)).StatusCode == HttpStatusCode.Created,
                    $"reader {reader} day {day}");
            }
        }
    }

    [Fact]
    public async Task canCorrect_on_a_crew_line_equals_what_the_correct_endpoint_does_for_every_reader()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        Guid todayEntry = await SavedCrewAsync(project, 8m, At(9, 0), _engineerA);
        Guid earlierEntry = await SavedCrewAsync(project, 8m, Dated(1), _engineerA);

        foreach ((string day, Guid entry) in new[] { ("2026-09-25", todayEntry), (Yesterday, earlierEntry) })
        {
            foreach (Guid reader in new[] { _engineerA, _supervisor, _owner })
            {
                JsonElement log = reader == _engineerA
                    ? await MineAs(project, day, _engineerA)
                    : await ReviewAsync(project, day, reader, _engineerA);
                JsonElement row = log.GetProperty("dayLabour").EnumerateArray().Single(item => item.GetProperty("entryId").GetGuid() == entry);

                row.GetProperty("canCorrect").GetBoolean().Should().Be(
                    (await CorrectCrew(project, entry, FigureFor(reader), reader)).StatusCode == HttpStatusCode.Created,
                    $"reader {reader} day {day}");
            }
        }
    }

    [Fact]
    public async Task canCorrect_is_false_on_every_line_once_the_project_is_closed_to_logs()
    {
        (Guid project, Guid line) = await NewActiveProjectAsync();
        Guid quantity = await SavedQuantityAsync(project, line, 40m, At(9, 0));
        await SavedCrewAsync(project, 8m, At(9, 0), _engineerA);
        await SetStatusAsync(project, ProjectStatus.Terminated);

        JsonElement log = await ReviewAsync(project, "2026-09-25", _owner, _engineerA);

        log.GetProperty("quantities").EnumerateArray().Should().OnlyContain(row => !row.GetProperty("canCorrect").GetBoolean());
        log.GetProperty("dayLabour").EnumerateArray().Should().OnlyContain(row => !row.GetProperty("canCorrect").GetBoolean());
        (await CorrectQty(project, quantity, 7m, _engineerA)).StatusCode.Should().NotBe(HttpStatusCode.Created);
    }

    // ---- the project crew read ----------------------------------------------------------------------------------

    private static string CrewRoute(Guid project) => $"/api/projects/{project}/day-labour/crew";

    private async Task OpenEngagementAsync(Guid project, Guid worker, Guid actor) =>
        (await SendAsync(HttpMethod.Post, $"/api/projects/{project}/day-labour/engagements", actor, new { workerId = worker }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

    [Fact]
    public async Task The_crew_read_lists_the_open_engagements_of_the_project_with_trade_and_no_rate_phone_or_code()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        (Guid other, _) = await NewActiveProjectAsync();
        await OpenEngagementAsync(project, _workerId, _engineerA);

        HttpResponseMessage response = await SendAsync(HttpMethod.Get, CrewRoute(project), _engineerB, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string raw = await response.Content.ReadAsStringAsync(Ct);
        using JsonDocument body = JsonDocument.Parse(raw);
        JsonElement man = body.RootElement.GetProperty("items").EnumerateArray().Single();

        man.GetProperty("workerId").GetGuid().Should().Be(_workerId);
        man.GetProperty("name").GetString().Should().Be("عامل يومية");
        man.GetProperty("trade").GetString().Should().Be("أعمال خرسانة");
        foreach (string forbidden in new[] { "rate", "phone", "code", "rating", "money" })
        {
            raw.Should().NotContainEquivalentOf(forbidden);
        }

        (await SendAsync(HttpMethod.Get, CrewRoute(other), _engineerB, null)).StatusCode.Should().Be(HttpStatusCode.OK);
        JsonDocument.Parse(await (await SendAsync(HttpMethod.Get, CrewRoute(other), _engineerB, null)).Content.ReadAsStringAsync(Ct))
            .RootElement.GetProperty("items").GetArrayLength().Should().Be(0, "another project's crew is not this one's");
    }

    [Fact]
    public async Task The_crew_read_leaves_out_a_closed_engagement()
    {
        (Guid project, _) = await NewActiveProjectAsync();
        await OpenEngagementAsync(project, _workerId, _engineerA);
        HttpResponseMessage listed = await SendAsync(HttpMethod.Get, CrewRoute(project), _engineerA, null);
        listed.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (KaffDbContext context = _database.CreateContext())
        {
            Engagement engagement = await context.Engagements.SingleAsync(item => item.ProjectId == project, Ct);
            engagement.Close(new DateOnly(2026, 9, 25), new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero)).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(Ct);
        }

        JsonDocument.Parse(await (await SendAsync(HttpMethod.Get, CrewRoute(project), _engineerA, null)).Content.ReadAsStringAsync(Ct))
            .RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task The_crew_read_refuses_a_site_engineer_not_assigned_and_every_other_role()
    {
        (Guid project, _) = await NewActiveProjectAsync();

        foreach (Guid refused in new[] { _engineerUnassigned, _finance, _owner })
        {
            (await SendAsync(HttpMethod.Get, CrewRoute(project), refused, null)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden, refused.ToString());
        }

        (await SendAsync(HttpMethod.Get, CrewRoute(project), _supervisor, null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
