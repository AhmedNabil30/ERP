using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.DailyLogs;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Projects;

/// <summary>
/// KAFF-420, server side of AC-3 to AC-5: the project frame sends each role its tabs, and the routes behind a tab it is
/// not given are refused. D-217 item 37, D-218 items 3, 5, 6, 7.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProjectFrameTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectId;
    private Guid _unsignedProjectId;
    private Guid _owner;
    private Guid _supervisor;
    private Guid _junior;
    private Guid _finance;
    private Guid _technicalOffice;
    private Guid _marketing;
    private Guid _stranger;

    public ProjectFrameTests(PostgresDatabase database) => _database = database;

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

    // ---- tabs per role ------------------------------------------------------------------------------

    [Theory]
    [InlineData("owner", true, true, true, true, true)]
    [InlineData("supervisor", true, true, true, true, true)]
    [InlineData("junior", true, false, true, false, true)]
    [InlineData("technicalOffice", true, true, false, true, true)]
    [InlineData("finance", true, false, false, true, true)]
    [InlineData("marketing", true, false, false, true, true)]
    public async Task Each_role_is_sent_exactly_the_tabs_it_may_open(
        string who, bool overview, bool boq, bool dailyLog, bool extracts, bool team)
    {
        using JsonDocument body = await ReadAsync(Who(who), $"/api/projects/{_projectId}");
        JsonElement tabs = body.RootElement.GetProperty("tabs");

        tabs.GetProperty("overview").GetBoolean().Should().Be(overview);
        tabs.GetProperty("boq").GetBoolean().Should().Be(boq);
        tabs.GetProperty("dailyLog").GetBoolean().Should().Be(dailyLog);
        tabs.GetProperty("extracts").GetBoolean().Should().Be(extracts);
        tabs.GetProperty("team").GetBoolean().Should().Be(team);
    }

    [Fact]
    public async Task The_frame_carries_name_client_status_contract_type_signed_stage_and_archived()
    {
        using JsonDocument signed = await ReadAsync(_owner, $"/api/projects/{_projectId}");
        JsonElement root = signed.RootElement;

        root.GetProperty("name").GetString().Should().Be("مشروع الإطار");
        root.GetProperty("clientName").GetString().Should().Be("عميل الإطار");
        root.GetProperty("status").GetString().Should().Be(nameof(ProjectStatus.Setup));
        root.GetProperty("contractType").GetString().Should().Be(nameof(ContractType.LumpSum));
        root.GetProperty("isSigned").GetBoolean().Should().BeTrue();
        root.GetProperty("isArchived").GetBoolean().Should().BeFalse();
        root.GetProperty("stage").GetString().Should().Be("Boq", "Setup is the BOQ stage");

        using JsonDocument unsigned = await ReadAsync(_owner, $"/api/projects/{_unsignedProjectId}");
        unsigned.RootElement.GetProperty("isSigned").GetBoolean().Should().BeFalse();
    }

    // ---- money ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("owner")]
    [InlineData("supervisor")]
    [InlineData("finance")]
    [InlineData("technicalOffice")]
    public async Task Owner_supervisor_finance_and_technical_office_receive_the_three_money_figures(string who)
    {
        using JsonDocument body = await ReadAsync(Who(who), $"/api/projects/{_projectId}");
        JsonElement money = body.RootElement.GetProperty("money");

        money.TryGetProperty("billedToDate", out _).Should().BeTrue();
        money.TryGetProperty("collectedToDate", out _).Should().BeTrue();
        money.TryGetProperty("outstanding", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("junior")]
    [InlineData("marketing")]
    public async Task The_junior_and_marketing_never_receive_the_money_block(string who)
    {
        string raw = await RawAsync(Who(who), $"/api/projects/{_projectId}");
        using JsonDocument body = JsonDocument.Parse(raw);

        body.RootElement.TryGetProperty("money", out _).Should().BeFalse("the key is absent, not null");
        raw.Should().NotContain("billedToDate").And.NotContain("collectedToDate").And.NotContain("outstanding");
    }


    [Fact]
    public async Task Every_overview_role_sees_the_last_daily_log_and_whether_the_site_logged_today()
    {
        foreach (string who in new[] { "owner", "supervisor", "junior" })
        {
            using JsonDocument body = await ReadAsync(Who(who), $"/api/projects/{_projectId}");

            body.RootElement.GetProperty("lastDailyLogOn").GetString().Should().Be(
                new Kaff.Domain.Rulings.RulingSet().DailyLogEntry.SiteDate(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd"));
            body.RootElement.GetProperty("loggedToday").GetBoolean().Should().BeTrue();
        }

        using JsonDocument none = await ReadAsync(_owner, $"/api/projects/{_unsignedProjectId}");
        none.RootElement.GetProperty("lastDailyLogOn").ValueKind.Should().Be(JsonValueKind.Null);
        none.RootElement.GetProperty("loggedToday").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Logged_today_follows_the_site_day_not_the_utc_day()
    {
        // 22:30 UTC on 10 June is 01:30 on 11 June in Cairo (UTC+3 in summer): the two dates differ.
        var clock = new TestClock();
        clock.Advance(new DateTimeOffset(2026, 6, 10, 22, 30, 0, TimeSpan.Zero) - DateTimeOffset.UtcNow);
        await using KaffDbContext context = _database.CreateContext();
        context.DailyLogs.Add(DailyLog.Open(
            _unsignedProjectId, _junior, new DateTimeOffset(2026, 6, 11, 1, 30, 0, TimeSpan.FromHours(3)), clock.GetUtcNow()));
        await context.SaveChangesAsync(Ct);

        await using var factory = new KaffApiFactory(_database.ConnectionString, clock: clock);
        using HttpClient http = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/api/projects/{_unsignedProjectId}", UriKind.Relative));
        request.Headers.Add(TestAuthHandler.UserIdHeader, _owner.ToString());
        request.Headers.Add(
            TestAuthHandler.SecurityStampHeader,
            await context.Users.Where(user => user.Id == _owner).Select(user => user.SecurityStamp).FirstAsync(Ct));
        request.Headers.Add(TestAuthHandler.RoleHeader, nameof(Role.Owner));
        using HttpResponseMessage response = await http.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("loggedToday").GetBoolean().Should().BeTrue("the log is dated 11 June, the Cairo day");
    }

    // ---- routes behind a tab the role does not have ----------------------------------------------------

    [Fact]
    public async Task The_junior_is_refused_the_boq_and_the_extract_routes()
    {
        foreach (string path in new[]
        {
            $"/api/projects/{_projectId}/boq",
            $"/api/projects/{_projectId}/extracts",
            $"/api/projects/{_projectId}/waiting-on",
        })
        {
            (await StatusAsync(_junior, path)).Should().Be(HttpStatusCode.Forbidden, path);
        }
    }

    [Fact]
    public async Task The_supervisor_and_the_owner_may_open_the_boq_and_the_extract_routes()
    {
        foreach (Guid caller in new[] { _supervisor, _owner })
        {
            (await StatusAsync(caller, $"/api/projects/{_projectId}/boq")).Should().NotBe(HttpStatusCode.Forbidden);
            (await StatusAsync(caller, $"/api/projects/{_projectId}/extracts")).Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task The_junior_keeps_the_daily_log_its_boq_line_list_and_the_team()
    {
        (await StatusAsync(_junior, $"/api/projects/{_projectId}/daily-logs/boq-lines")).Should().NotBe(HttpStatusCode.Forbidden);
        (await StatusAsync(_junior, $"/api/projects/{_projectId}/team")).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(_junior, $"/api/projects/{_projectId}/daily-logs/mine?date={DateTime.UtcNow:yyyy-MM-dd}"))
            .Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_unassigned_engineer_is_refused_the_frame_and_the_boq()
    {
        (await StatusAsync(_stranger, $"/api/projects/{_projectId}")).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(_stranger, $"/api/projects/{_projectId}/boq")).Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- helpers ---------------------------------------------------------------------------------------

    private Guid Who(string who) => who switch
    {
        "owner" => _owner,
        "supervisor" => _supervisor,
        "junior" => _junior,
        "finance" => _finance,
        "technicalOffice" => _technicalOffice,
        "marketing" => _marketing,
        _ => throw new ArgumentOutOfRangeException(nameof(who)),
    };

    private async Task<HttpStatusCode> StatusAsync(Guid actorId, string path)
    {
        using HttpResponseMessage response = await SendAsync(actorId, path);
        return response.StatusCode;
    }

    private async Task<string> RawAsync(Guid actorId, string path)
    {
        using HttpResponseMessage response = await SendAsync(actorId, path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        return await response.Content.ReadAsStringAsync(Ct);
    }

    private async Task<JsonDocument> ReadAsync(Guid actorId, string path) => JsonDocument.Parse(await RawAsync(actorId, path));

    private async Task<HttpResponseMessage> SendAsync(Guid actorId, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));

        await using KaffDbContext reader = _database.CreateBareContext();
        var found = await reader.Users.Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role }).FirstAsync(Ct);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, found.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, found.Role.ToString());

        return await _client.SendAsync(request, Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("FRM-C"), "عميل الإطار", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project project = Project.Create(
            UniqueNames.Code("FRM-P"), "مشروع الإطار", client.Id, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;
        project.SetLumpSumTerms(
            Percentage.FromPercent(25m), Percentage.FromPercent(20m), Percentage.FromPercent(25m),
            Percentage.FromPercent(75m), delayPenaltyEnabled: false);
        project.Sign(new DateOnly(2026, 9, 25), new Money(1_000_000m)).IsSuccess.Should().BeTrue();

        Project unsigned = Project.Create(
            UniqueNames.Code("FRM-U"), "مشروع غير موقع", client.Id, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;

        User owner = MakeUser("frm-owner", Role.Owner);
        User supervisor = MakeEngineer("frm-sup");
        User junior = MakeEngineer("frm-jun");
        User stranger = MakeEngineer("frm-str");
        User finance = MakeUser("frm-fin", Role.Finance, WellKnownDepartments.FinanceId);
        User technicalOffice = MakeUser(
            "frm-tech", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User marketing = MakeUser("frm-mkt", Role.MarketingSales);

        context.Clients.Add(client);
        context.Projects.AddRange(project, unsigned);
        context.Users.AddRange(owner, supervisor, junior, stranger, finance, technicalOffice, marketing);
        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.AddRange(
            ProjectAssignment.Create(project.Id, supervisor, AssignmentLevel.Supervisor, owner.Id, Now).Value,
            ProjectAssignment.Create(project.Id, junior, AssignmentLevel.Junior, owner.Id, Now).Value,
            ProjectAssignment.Create(project.Id, finance, AssignmentLevel.Standard, owner.Id, Now).Value,
            ProjectAssignment.Create(project.Id, technicalOffice, AssignmentLevel.Standard, owner.Id, Now).Value,
            ProjectAssignment.Create(project.Id, marketing, AssignmentLevel.Standard, owner.Id, Now).Value);
        context.DailyLogs.Add(DailyLog.Open(project.Id, junior.Id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(Ct);

        _projectId = project.Id;
        _unsignedProjectId = unsigned.Id;
        _owner = owner.Id;
        _supervisor = supervisor.Id;
        _junior = junior.Id;
        _stranger = stranger.Id;
        _finance = finance.Id;
        _technicalOffice = technicalOffice.Id;
        _marketing = marketing.Id;
    }

    private static User MakeEngineer(string userName)
        => MakeUser(userName, Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);

    private static User MakeUser(
        string userName, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
