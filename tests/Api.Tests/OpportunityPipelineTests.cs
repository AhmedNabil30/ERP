using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Sales;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// R3-01 to R3-04 — open, list, move, hold, flag, close lost and reopen opportunities. The gate is
/// <c>Permission.OpportunityManage</c> (Owner and Marketing, spec.md §2).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OpportunityPipelineTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _marketing;
    private Guid _finance;
    private Guid _technical;
    private Guid _hr;
    private Guid _portal;
    private Guid _clientId;

    public OpportunityPipelineTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await SeedAsync();
        _factory = new KaffApiFactory(_database);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- R3-01 ---------------------------------------------------------------------------------

    [Fact]
    public async Task Marketing_opens_an_opportunity_as_a_Lead_with_a_generated_code()
    {
        HttpResponseMessage response = await CreateAsync(_marketing, Role.MarketingSales, _clientId, "فيلا التجمع");

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("code").GetString().Should().StartWith("O-");
        body.RootElement.GetProperty("stage").GetString().Should().Be("Lead");
        body.RootElement.GetProperty("status").GetString().Should().Be("Active");

        HttpResponseMessage second = await CreateAsync(_owner, Role.Owner, _clientId, "فيلا ثانية");
        second.StatusCode.Should().Be(HttpStatusCode.Created, "the Owner holds OpportunityManage too");

        using JsonDocument other = JsonDocument.Parse(await second.Content.ReadAsStringAsync(Ct));
        other.RootElement.GetProperty("code").GetString().Should().NotBe(body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task One_opportunity_reads_in_the_list_row_shape_for_Marketing_and_the_Owner_and_an_unknown_id_is_not_found()
    {
        Guid id = await SeedOpportunityAsync("قراءة", Start(0));
        JsonElement listed = await ReadRowAsync(id);

        foreach ((Guid user, Role role) in new[] { (_marketing, Role.MarketingSales), (_owner, Role.Owner) })
        {
            HttpResponseMessage response = await GetAsync($"/api/opportunities/{id}", user, role);
            response.StatusCode.Should().Be(HttpStatusCode.OK, role.ToString());
            using JsonDocument one = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            one.RootElement.GetRawText().Should().Be(listed.GetRawText());
            one.RootElement.GetProperty("title").GetString().Should().Be("قراءة");
        }

        (await GetAsync($"/api/opportunities/{Guid.NewGuid()}", _marketing, Role.MarketingSales)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_typed_code_in_the_request_is_ignored()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/opportunities", UriKind.Relative))
        {
            Content = JsonContent.Create(new { clientId = _clientId, title = "عمارة", code = "HACK-1" }),
        };
        await StampAsync(request, _marketing, Role.MarketingSales);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("code").GetString().Should().StartWith("O-");
    }

    [Fact]
    public async Task Create_is_refused_for_an_unknown_client_and_a_blank_title()
    {
        (await CreateAsync(_marketing, Role.MarketingSales, Guid.NewGuid(), "x"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await CreateAsync(_marketing, Role.MarketingSales, _clientId, "   "))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Every_other_role_is_refused_on_every_opportunity_endpoint()
    {
        Guid id = await SeedOpportunityAsync("منع", Start(0));

        (Guid User, Role Role)[] refused =
        [
            (_finance, Role.Finance), (_technical, Role.TechnicalOffice), (_hr, Role.Hr), (_portal, Role.Client),
        ];

        foreach ((Guid user, Role role) in refused)
        {
            (await CreateAsync(user, role, _clientId, "x")).StatusCode.Should().Be(HttpStatusCode.Forbidden, role.ToString());
            (await GetAsync("/api/opportunities", user, role)).StatusCode.Should().Be(HttpStatusCode.Forbidden, role.ToString());
            (await GetAsync($"/api/opportunities/{id}", user, role)).StatusCode.Should().Be(HttpStatusCode.Forbidden, role.ToString());

            foreach (string action in new[] { "advance", "hold", "resume", "close-lost", "reopen" })
            {
                (await PostAsync($"/api/opportunities/{id}/{action}", user, role, new { reason = "x" }))
                    .StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{role} on {action}");
            }
        }

        (await ReadAsync(id)).Stage.Should().Be(OpportunityStage.Lead);
    }

    [Fact]
    public async Task The_list_filters_by_stage_and_status_and_searches_client_name_and_phone()
    {
        string nonce = UniqueNames.Code("LST");
        (Guid clientId, string name, string phone) = await SeedClientAsync($"شركة {nonce}");
        Guid lead = await SeedOpportunityAsync($"{nonce} lead", Start(0), clientId);
        Guid meeting = await SeedOpportunityAsync($"{nonce} meeting", Start(0), clientId);
        Guid held = await SeedOpportunityAsync($"{nonce} held", Start(0), clientId);

        (await PostAsync($"/api/opportunities/{meeting}/advance", _marketing, Role.MarketingSales)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostAsync($"/api/opportunities/{held}/hold", _marketing, Role.MarketingSales)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ListIdsAsync($"search={Uri.EscapeDataString(name)}")).Should().BeEquivalentTo([lead, meeting, held]);
        (await ListIdsAsync($"search={Uri.EscapeDataString(phone)}")).Should().BeEquivalentTo([lead, meeting, held], "the phone finds the opportunities of that client");
        (await ListIdsAsync($"search={Uri.EscapeDataString(name)}&stage=Meeting")).Should().BeEquivalentTo([meeting]);
        (await ListIdsAsync($"search={Uri.EscapeDataString(name)}&status=OnHold")).Should().BeEquivalentTo([held]);
        (await ListIdsAsync($"search={Uri.EscapeDataString(name)}&status=Active&stage=Lead")).Should().BeEquivalentTo([lead]);

        (await GetAsync("/api/opportunities?stage=Nope", _marketing, Role.MarketingSales))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "an unknown filter is refused, not read as all");
        (await GetAsync("/api/opportunities?status=Nope", _marketing, Role.MarketingSales))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_and_stage_change_write_audit_records_with_before_and_after()
    {
        HttpResponseMessage created = await CreateAsync(_marketing, Role.MarketingSales, _clientId, "تدقيق");
        using JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Ct));
        Guid id = body.RootElement.GetProperty("id").GetGuid();

        (await PostAsync($"/api/opportunities/{id}/advance", _marketing, Role.MarketingSales)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();
        List<AuditRecord> records = await reader.AuditRecords
            .Where(record => record.EntityId == id)
            .OrderBy(record => record.OccurredAt)
            .ToListAsync(Ct);

        records.Select(record => record.Action).Should().Equal(AuditAction.Created, AuditAction.Modified);
        records[1].BeforeJson.Should().Contain("Lead");
        records[1].AfterJson.Should().Contain("Meeting");
        records[1].ActorUserId.Should().Be(_marketing);
    }

    // ---- R3-02 ---------------------------------------------------------------------------------

    [Fact]
    public async Task Advance_moves_one_stage_and_stops_at_Negotiation()
    {
        Guid id = await SeedOpportunityAsync("مراحل", Start(0));

        for (int i = 0; i < 4; i++)
        {
            (await PostAsync($"/api/opportunities/{id}/advance", _marketing, Role.MarketingSales)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await ReadAsync(id)).Stage.Should().Be(OpportunityStage.Negotiation);
        (await PostAsync($"/api/opportunities/{id}/advance", _marketing, Role.MarketingSales)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PostAsync($"/api/opportunities/{Guid.NewGuid()}/advance", _marketing, Role.MarketingSales)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Hold_then_resume_returns_to_the_same_stage_and_a_held_opportunity_cannot_move()
    {
        Guid id = await SeedOpportunityAsync("تعليق", Start(0));
        await PostAsync($"/api/opportunities/{id}/advance", _marketing, Role.MarketingSales);

        (await PostAsync($"/api/opportunities/{id}/hold", _marketing, Role.MarketingSales)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostAsync($"/api/opportunities/{id}/advance", _marketing, Role.MarketingSales)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PostAsync($"/api/opportunities/{id}/resume", _marketing, Role.MarketingSales)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostAsync($"/api/opportunities/{id}/resume", _marketing, Role.MarketingSales)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        Opportunity stored = await ReadAsync(id);
        stored.Stage.Should().Be(OpportunityStage.Meeting);
        stored.Status.Should().Be(OpportunityStatus.Active);
    }

    [Fact]
    public async Task A_stage_move_revives_a_stalled_opportunity()
    {
        Guid id = await SeedOpportunityAsync("متوقف", Start(8));

        (await ReadRowAsync(id)).GetProperty("status").GetString().Should().Be("Stalled");

        (await PostAsync($"/api/opportunities/{id}/advance", _marketing, Role.MarketingSales)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        JsonElement row = await ReadRowAsync(id);
        row.GetProperty("status").GetString().Should().Be("Active");
        row.GetProperty("inactivity").GetString().Should().Be("None");
    }

    // ---- R3-03 ---------------------------------------------------------------------------------

    [Fact]
    public async Task Quiet_days_flag_on_the_list_and_the_follow_up_filter_lists_only_flagged_live_rows()
    {
        string nonce = UniqueNames.Code("QUI");
        (Guid clientId, string name, _) = await SeedClientAsync($"شركة {nonce}");
        Guid fresh = await SeedOpportunityAsync($"{nonce} fresh", Start(1), clientId);
        Guid day2 = await SeedOpportunityAsync($"{nonce} d2", Start(2), clientId);
        Guid day4 = await SeedOpportunityAsync($"{nonce} d4", Start(4), clientId);
        Guid day7 = await SeedOpportunityAsync($"{nonce} d7", Start(7), clientId);
        Guid held = await SeedOpportunityAsync($"{nonce} held", Start(9), clientId);
        Guid lost = await SeedOpportunityAsync($"{nonce} lost", Start(9), clientId);

        await PostAsync($"/api/opportunities/{held}/hold", _marketing, Role.MarketingSales);
        await PostAsync($"/api/opportunities/{lost}/close-lost", _marketing, Role.MarketingSales, new { reason = "سعر" });

        string search = $"search={Uri.EscapeDataString(name)}";

        (await ReadRowAsync(fresh)).GetProperty("inactivity").GetString().Should().Be("None");
        (await ReadRowAsync(day2)).GetProperty("inactivity").GetString().Should().Be("FirstAlert");
        (await ReadRowAsync(day4)).GetProperty("inactivity").GetString().Should().Be("SecondAlert");
        JsonElement stalled = await ReadRowAsync(day7);
        stalled.GetProperty("inactivity").GetString().Should().Be("Stalled");
        stalled.GetProperty("status").GetString().Should().Be("Stalled");
        (await ReadAsync(day7)).Status.Should().Be(OpportunityStatus.Active, "Stalled is computed on read, not stored");

        (await ListIdsAsync($"{search}&followUp=true")).Should().BeEquivalentTo([day2, day4, day7]);
        (await ListIdsAsync($"{search}&status=Stalled")).Should().BeEquivalentTo([day7]);
        (await ListIdsAsync($"{search}&status=Active")).Should().BeEquivalentTo([fresh, day2, day4]);
    }

    // ---- R3-04 ---------------------------------------------------------------------------------

    [Fact]
    public async Task Close_lost_is_refused_without_a_reason_and_stores_it_with_the_audit_reason()
    {
        Guid id = await SeedOpportunityAsync("خسارة", Start(0));

        (await PostAsync($"/api/opportunities/{id}/close-lost", _marketing, Role.MarketingSales))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "no body, no reason");
        (await PostAsync($"/api/opportunities/{id}/close-lost", _marketing, Role.MarketingSales, new { reason = "  " }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadAsync(id)).Status.Should().Be(OpportunityStatus.Active);

        (await PostAsync($"/api/opportunities/{id}/close-lost", _marketing, Role.MarketingSales, new { reason = "العميل اختار منافس" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        Opportunity stored = await ReadAsync(id);
        stored.Status.Should().Be(OpportunityStatus.ClosedLost);
        stored.ClosedLostReason.Should().Be("العميل اختار منافس");

        AuditRecord record = await LastModifiedAsync(id);
        record.Reason.Should().Be("العميل اختار منافس");
        record.AfterJson.Should().Contain("ClosedLost");

        (await PostAsync($"/api/opportunities/{id}/close-lost", _marketing, Role.MarketingSales, new { reason = "مرة تانية" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reopen_returns_the_opportunity_to_a_live_stage_on_the_same_client_without_a_new_client()
    {
        Guid id = await SeedOpportunityAsync("إعادة", Start(0));
        await PostAsync($"/api/opportunities/{id}/advance", _marketing, Role.MarketingSales);
        await PostAsync($"/api/opportunities/{id}/close-lost", _marketing, Role.MarketingSales, new { reason = "سعر" });

        int clientsBefore = await CountClientsAsync();

        (await PostAsync($"/api/opportunities/{id}/reopen", _marketing, Role.MarketingSales, new { reason = "العميل رجع" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        Opportunity stored = await ReadAsync(id);
        stored.Status.Should().Be(OpportunityStatus.Active);
        stored.Stage.Should().Be(OpportunityStage.Meeting);
        stored.ClientId.Should().Be(_clientId);
        (await CountClientsAsync()).Should().Be(clientsBefore);

        (await LastModifiedAsync(id)).Reason.Should().Be("العميل رجع");

        (await PostAsync($"/api/opportunities/{id}/reopen", _marketing, Role.MarketingSales))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "only a lost opportunity reopens");
    }

    // ---- helpers -------------------------------------------------------------------------------

    private static DateTimeOffset Start(int quietDays) => DateTimeOffset.UtcNow.AddDays(-quietDays).AddMinutes(-5);

    private Task<HttpResponseMessage> CreateAsync(Guid actor, Role role, Guid clientId, string title) =>
        PostAsync("/api/opportunities", actor, role, new { clientId, title });

    private async Task<HttpResponseMessage> PostAsync(string path, Guid actor, Role role, object? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        await StampAsync(request, actor, role);
        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, Guid actor, Role role)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        await StampAsync(request, actor, role);
        return await _client.SendAsync(request, Ct);
    }

    private async Task<List<Guid>> ListIdsAsync(string query)
    {
        HttpResponseMessage response = await GetAsync($"/api/opportunities?{query}", _marketing, Role.MarketingSales);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return [.. body.RootElement.GetProperty("opportunities").EnumerateArray().Select(row => row.GetProperty("id").GetGuid())];
    }

    private async Task<JsonElement> ReadRowAsync(Guid id)
    {
        Opportunity stored = await ReadAsync(id);
        HttpResponseMessage response = await GetAsync(
            $"/api/opportunities?search={Uri.EscapeDataString(stored.Code)}", _marketing, Role.MarketingSales);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("opportunities").EnumerateArray().Single().Clone();
    }

    private async Task StampAsync(HttpRequestMessage request, Guid actorId, Role role)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, role.ToString());

        await using KaffDbContext reader = _database.CreateBareContext();
        string stamp = await reader.Users.Where(user => user.Id == actorId).Select(user => user.SecurityStamp).SingleAsync(Ct);
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, stamp);

        Guid? department = role switch
        {
            Role.Finance => WellKnownDepartments.FinanceId,
            Role.Hr => WellKnownDepartments.HrId,
            _ => null,
        };

        if (department is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, department.Value.ToString());
        }

        if (role == Role.Client)
        {
            request.Headers.Add(TestAuthHandler.ClientIdHeader, _clientId.ToString());
        }
    }

    private async Task<AuditRecord> LastModifiedAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.AuditRecords
            .Where(candidate => candidate.EntityId == id && candidate.Action == AuditAction.Modified)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);
    }

    private async Task<Opportunity> ReadAsync(Guid id)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Opportunities.SingleAsync(opportunity => opportunity.Id == id, Ct);
    }

    private async Task<int> CountClientsAsync()
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.Clients.CountAsync(Ct);
    }

    private async Task<Guid> SeedOpportunityAsync(string title, DateTimeOffset lastActivity, Guid? clientId = null)
    {
        await using KaffDbContext context = _database.CreateContext();
        Opportunity opportunity = Opportunity.Create(
            UniqueNames.Code("O"), clientId ?? _clientId, title, lastActivity).Value;
        context.Opportunities.Add(opportunity);
        await context.SaveChangesAsync(Ct);
        return opportunity.Id;
    }

    private async Task<(Guid Id, string Name, string Phone)> SeedClientAsync(string name)
    {
        await using KaffDbContext context = _database.CreateContext();
        PhoneNumber phone = UniqueNames.Phone();
        Client client = Client.Create(UniqueNames.Code("OC"), name, phone, ClientKind.Corporate, DateTimeOffset.UtcNow).Value;
        context.Clients.Add(client);
        await context.SaveChangesAsync(Ct);
        return (client.Id, name, phone.Entered);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client company = Client.Create(
            UniqueNames.Code("OPP-C"),
            $"شركة {UniqueNames.Code("OPPN")}",
            UniqueNames.Phone(),
            ClientKind.Corporate,
            DateTimeOffset.UtcNow).Value;

        User owner = MakeUser("opp-owner", Role.Owner);
        User marketing = MakeUser("opp-marketing", Role.MarketingSales);
        User finance = MakeUser("opp-finance", Role.Finance, WellKnownDepartments.FinanceId);
        User technical = MakeUser("opp-tech", Role.TechnicalOffice);
        User hr = MakeUser("opp-hr", Role.Hr, WellKnownDepartments.HrId);
        User portal = MakeUser("opp-portal", Role.Client, clientId: company.Id);

        context.Clients.Add(company);
        context.Users.AddRange(owner, marketing, finance, technical, hr, portal);
        await context.SaveChangesAsync(Ct);

        _clientId = company.Id;
        _owner = owner.Id;
        _marketing = marketing.Id;
        _finance = finance.Id;
        _technical = technical.Id;
        _hr = hr.Id;
        _portal = portal.Id;
    }

    private static User MakeUser(string userName, Role role, Guid? department = null, Guid? clientId = null)
        => User.Create(
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, DateTimeOffset.UtcNow, department, null, clientId).Value;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
