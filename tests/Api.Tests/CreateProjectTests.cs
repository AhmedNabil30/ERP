using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Domain.Treasury;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-400 — <c>POST /api/projects</c>. AC-400-A, C, D, E, F.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CreateProjectTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _finance;
    private Guid _marketing;
    private Guid _hr;
    private Guid _siteEngineer;

    public CreateProjectTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-400-A / the gate ----------------------------------------------------------------------

    [Fact]
    public async Task Owner_and_technical_office_may_open_a_project()
    {
        (await CreateAsync(_owner, "مشروع ١")).StatusCode.Should().Be(HttpStatusCode.Created);
        (await CreateAsync(_technicalOffice, "مشروع ٢")).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Marketing_finance_hr_and_site_engineers_are_refused()
    {
        foreach (Guid caller in new[] { _marketing, _finance, _hr, _siteEngineer })
        {
            HttpResponseMessage response = await CreateAsync(caller, "مشروع مرفوض");

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await MessageKeyAsync(response)).Should().Be("errors.auth.forbidden");
        }
    }

    // ---- AC-400-D · holdPercentage defaults to 20 --------------------------------------------------

    [Fact]
    public async Task HoldPercentage_defaults_to_20_when_omitted()
    {
        HttpResponseMessage response = await CreateAsync(_owner, "مشروع افتراضي", holdPercentage: null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("holdRate").GetString().Should().Be(
            "20.0000", "spec.md §5.1/§15: hold defaults to 20%, never the 5% subcontractor retention");

        Guid id = body.RootElement.GetProperty("id").GetGuid();

        await using KaffDbContext reader = _database.CreateBareContext();
        Project project = await reader.Projects.SingleAsync(p => p.Id == id, Ct);

        project.HoldRate!.Value.Percent.Should().Be(20m);
    }

    // ---- AC-400-E · money crosses the wire as a JSON string ------------------------------------------

    [Fact]
    public async Task Money_and_rate_fields_serialise_as_json_strings()
    {
        HttpResponseMessage response = await CreateAsync(_owner, "مشروع نقدي", contractValue: 1_000_000m);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        string raw = await response.Content.ReadAsStringAsync(Ct);
        using JsonDocument body = JsonDocument.Parse(raw);

        body.RootElement.GetProperty("contractValue").ValueKind.Should().Be(
            JsonValueKind.String, "decisions.md D-135 — a JSON number becomes a double and loses decimal(18,4)");
        body.RootElement.GetProperty("contractValue").GetString().Should().Be("1000000.0000");

        body.RootElement.GetProperty("holdRate").ValueKind.Should().Be(JsonValueKind.String);
        body.RootElement.GetProperty("advanceRate").ValueKind.Should().Be(JsonValueKind.String);
    }

    // ---- AC-400-C · creating a project opens its account set ----------------------------------------

    [Fact]
    public async Task Creating_a_lump_sum_project_opens_its_five_account_set()
    {
        HttpResponseMessage response = await CreateAsync(_owner, "مشروع بالجملة");
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        Guid id = await CreatedIdAsync(response);

        await using KaffDbContext reader = _database.CreateBareContext();

        List<AccountType> types = await reader.Accounts
            .Where(account => account.ProjectId == id)
            .Select(account => account.Type)
            .ToListAsync(Ct);

        types.Should().BeEquivalentTo(
            [
                AccountType.ClientAdvance, AccountType.Hold, AccountType.FirmAdvance,
                AccountType.MaterialAdvance, AccountType.ClientReceivable,
            ],
            "KAFF-302's factory opens the full Lump Sum set in the same transaction as the project");
    }

    // ---- AC-400-F · an audit record is written on create ---------------------------------------------

    [Fact]
    public async Task Creating_a_project_leaves_a_created_audit_record()
    {
        HttpResponseMessage response = await CreateAsync(_owner, "مشروع مدقق");
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        Guid id = await CreatedIdAsync(response);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords.SingleAsync(
            r => r.EntityType == nameof(Project) && r.EntityId == id && r.Action == AuditAction.Created, Ct);

        record.ActorUserId.Should().Be(_owner);
        record.ActorRole.Should().Be(Role.Owner);
    }

    // ---- AC-400-H reference · no new migration -------------------------------------------------------

    [Fact]
    public async Task Project_codes_are_generated_and_unique_with_no_sequence()
    {
        // No project_code_seq exists (this story's own finding — see build notes). GenerateCode
        // draws from Guid.NewGuid rather than a sequence, so uniqueness is asserted rather than
        // assumed: a collision would 500 as a unique-index violation instead of silently sharing a code.
        Guid first = await CreatedIdAsync(await CreateAsync(_owner, "مشروع أ"));
        Guid second = await CreatedIdAsync(await CreateAsync(_owner, "مشروع ب"));

        await using KaffDbContext reader = _database.CreateBareContext();

        string codeA = await reader.Projects.Where(p => p.Id == first).Select(p => p.Code).SingleAsync(Ct);
        string codeB = await reader.Projects.Where(p => p.Id == second).Select(p => p.Code).SingleAsync(Ct);

        codeA.Should().NotBe(codeB);
        codeA.Should().StartWith("PRJ-");
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private async Task<HttpResponseMessage> CreateAsync(
        Guid actorId,
        string name,
        decimal? contractValue = 1_000_000m,
        decimal? holdPercentage = 20m)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/projects", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                name,
                clientId = _clientId,
                contractType = nameof(ContractType.LumpSum),
                contractValue = contractValue?.ToString("0.0000"),
                holdPercentage = holdPercentage?.ToString("0.0000"),
                clientAdvance = "25.0000",
                status = (string?)null,
            }),
        };

        (string stamp, Role? role) = await SessionAsync(actorId);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, stamp);

        if (role is not null)
        {
            request.Headers.Add(TestAuthHandler.RoleHeader, role.Value.ToString());
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<(string Stamp, Role? Role)> SessionAsync(Guid actorId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        var found = await reader.Users
            .Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role })
            .FirstOrDefaultAsync(Ct);

        return found is null ? ("no-such-user", null) : (found.SecurityStamp, found.Role);
    }

    private static async Task<Guid> CreatedIdAsync(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key)
            ? key.GetString()
            : null;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("CRP-C"), "عميل الإنشاء", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        User owner = MakeUser("crp-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "crp-tech", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User finance = MakeUser("crp-finance", Role.Finance, WellKnownDepartments.FinanceId);
        User marketing = MakeUser("crp-marketing", Role.MarketingSales);
        User hr = MakeUser("crp-hr", Role.Hr, WellKnownDepartments.HrId);
        User siteEngineer = MakeUser(
            "crp-engineer", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);

        context.Clients.Add(client);
        context.Users.AddRange(owner, technicalOffice, finance, marketing, hr, siteEngineer);

        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _finance = finance.Id;
        _marketing = marketing.Id;
        _hr = hr.Id;
        _siteEngineer = siteEngineer.Id;
    }

    private static User MakeUser(
        string userName, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
