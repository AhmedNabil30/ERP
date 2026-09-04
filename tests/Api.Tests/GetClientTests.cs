using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// <c>GET /api/clients/{clientId}</c> — the read S-014 loads its form from. KAFF-126, decisions.md D-113.
/// </summary>
/// <remarks>
/// Built because writing the screen found that nothing could load the record `PUT /api/clients/{id}`
/// edits: KAFF-124 shipped a list whose rows carry six fields, KAFF-121 shipped an edit taking nine,
/// and S-014 is reachable by URL so router state cannot stand in.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class GetClientTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _marketing;
    private Guid _finance;
    private Guid _portalClient;
    private Guid _portalClientCompany;

    public GetClientTests(PostgresDatabase database) => _database = database;

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

    [Fact]
    public async Task The_whole_editable_file_comes_back_including_the_fields_the_list_row_omits()
    {
        Guid id = await RegisterAsync("شركة النيل للتطوير");

        await SetDetailsAsync(id, notes: "تأخر في السداد مرتين", address: "التجمع الخامس");

        HttpResponseMessage response = await GetAsync(id, _marketing, Role.MarketingSales, Department.Marketing);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("name").GetString().Should().Be("شركة النيل للتطوير");
        body.RootElement.GetProperty("address").GetString().Should().Be("التجمع الخامس");
        body.RootElement.GetProperty("notes").GetString().Should().Be(
            "تأخر في السداد مرتين",
            "the edit form has to load the note it is going to save back — this is the one payload in "
            + "the slice that carries them, and it is gated ClientManage");
        body.RootElement.GetProperty("isActive").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("code").GetString().Should().MatchRegex(@"^C-\d{5,}$");
    }

    /// <summary>An archived client is still readable by id — S-014 was asked for that client.</summary>
    [Fact]
    public async Task An_archived_client_is_still_readable_by_id()
    {
        Guid id = await RegisterAsync("عميل مؤرشف");

        await ArchiveAsync(id);

        HttpResponseMessage response = await GetAsync(id, _marketing, Role.MarketingSales, Department.Marketing);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("isActive").GetBoolean().Should().BeFalse(
            "the list hides archived clients by default; a screen reached by id asked for this one, "
            + "and spec.md §3 attaches a reopened opportunity to the original");
    }

    /// <summary>
    /// The payload is pinned, because it is the widest client payload in the slice.
    /// </summary>
    /// <remarks>
    /// A whitelist, not a search for suspect words (decisions.md D-106). No balance, no credit limit,
    /// no withholding category — and the fields that are here are here because S-014 edits them.
    /// </remarks>
    [Fact]
    public void The_payload_carries_the_editable_file_and_no_money()
    {
        typeof(Kaff.Api.Features.Clients.GetClient.Response)
            .GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                [
                    "Id", "Code", "Name", "Phone", "Kind", "AlternatePhone", "Email", "Address",
                    "TaxRegistrationNumber", "Notes", "IsActive",
                ],
                "a money column or a withholding category added here fails, whatever it is named");
    }

    [Fact]
    public async Task Nobody_outside_marketing_and_the_owner_may_read_a_client_file()
    {
        Guid id = await RegisterAsync("عميل محمي");

        await SetDetailsAsync(id, notes: "ملاحظة داخلية", address: null);

        HttpResponseMessage finance = await GetAsync(id, _finance, Role.Finance, Department.Finance);

        finance.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        HttpResponseMessage portal = await GetAsync(
            id, _portalClient, Role.Client, null, actorClientId: _portalClientCompany);

        portal.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "this is the one payload carrying internal notes, and spec.md §12 forbids a client ever "
            + "seeing them — on a read there is no audit constraint to fail behind the gate");

        (await portal.Content.ReadAsStringAsync(Ct)).Should().NotContain("ملاحظة داخلية");
    }

    [Fact]
    public async Task An_unknown_id_says_so_in_a_translatable_way()
    {
        HttpResponseMessage response = await GetAsync(
            Guid.NewGuid(), _marketing, Role.MarketingSales, Department.Marketing);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString().Should().Be("errors.master.client_not_found");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<Guid> RegisterAsync(string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/clients", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                name,
                phone = UniqueNames.Phone().Entered,
                kind = nameof(ClientKind.Corporate),
                acknowledgedDuplicatePhone = false,
            }),
        };

        await StampAsync(request, _marketing, Role.MarketingSales, Department.Marketing, null);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> GetAsync(
        Guid clientId,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        Guid? actorClientId = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"/api/clients/{clientId}", UriKind.Relative));

        await StampAsync(request, actorId, actorRole, actorDepartment, actorClientId);

        return await _client.SendAsync(request, Ct);
    }

    private async Task StampAsync(
        HttpRequestMessage request,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        Guid? actorClientId)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        if (actorDepartment is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, actorDepartment.Value.ToString());
        }

        if (actorClientId is not null)
        {
            request.Headers.Add(TestAuthHandler.ClientIdHeader, actorClientId.Value.ToString());
        }
    }

    private async Task<string> CurrentStampAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .SingleAsync(Ct);
    }

    private async Task SetDetailsAsync(Guid id, string? notes, string? address)
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = await context.Clients.SingleAsync(candidate => candidate.Id == id, Ct);

        client.SetContactDetails(null, null, address, notes);

        await context.SaveChangesAsync(Ct);
    }

    private async Task ArchiveAsync(Guid id)
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = await context.Clients.SingleAsync(candidate => candidate.Id == id, Ct);

        client.Archive().IsSuccess.Should().BeTrue();

        await context.SaveChangesAsync(Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client company = Client.Create(
            UniqueNames.Code("GET-C1"),
            "عميل بوابة العملاء",
            UniqueNames.Phone(),
            ClientKind.Corporate,
            Now).Value;

        User marketing = MakeUser("get-marketing", Role.MarketingSales, Department.Marketing);
        User finance = MakeUser("get-finance", Role.Finance, Department.Finance);
        User portal = MakeUser("get-portal", Role.Client, clientId: company.Id);

        context.Clients.Add(company);
        context.Users.AddRange(marketing, finance, portal);

        await context.SaveChangesAsync(Ct);

        _portalClientCompany = company.Id;
        _marketing = marketing.Id;
        _finance = finance.Id;
        _portalClient = portal.Id;
    }

    private static User MakeUser(string userName, Role role, Department? department = null, Guid? clientId = null)
        => User.Create(
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, null, clientId).Value;

    private static DateTimeOffset Now => new(2026, 9, 4, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
