using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
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
    private Guid _otherTechnicalOffice;
    private Guid _itemId;

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
            "0.2", "spec.md §5.1/§15: hold defaults to 20%, never the 5% subcontractor retention — "
                + "D-151, the wire carries the fraction, not the percent figure");

        Guid id = body.RootElement.GetProperty("id").GetGuid();

        await using KaffDbContext reader = _database.CreateBareContext();
        Project project = await reader.Projects.SingleAsync(p => p.Id == id, Ct);

        project.HoldRate!.Value.Percent.Should().Be(20m);
    }

    // ---- AC-400-E · money crosses the wire as a JSON string ------------------------------------------

    [Fact]
    public async Task Money_and_rate_fields_serialise_as_json_strings()
    {
        HttpResponseMessage response = await CreateAsync(_owner, "مشروع نقدي");

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        string raw = await response.Content.ReadAsStringAsync(Ct);
        using JsonDocument body = JsonDocument.Parse(raw);

        body.RootElement.GetProperty("holdRate").ValueKind.Should().Be(
            JsonValueKind.String, "decisions.md D-135 — a JSON number becomes a double and loses precision");
        body.RootElement.GetProperty("advanceRate").ValueKind.Should().Be(JsonValueKind.String);
    }

    // ---- The contract value is derived, never typed -------------------------------------------------

    [Fact]
    public async Task A_typed_contract_value_is_ignored_and_a_new_lump_sum_project_has_none_until_it_is_signed()
    {
        // The request record has no ContractValue member any more. A client that still sends one must not
        // be able to set it: the value is the BOQ total at signature and nothing else.
        HttpResponseMessage response = await CreateAsync(_owner, "مشروع بلا قيمة", typedContractValue: "9999999.0000");

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("contractValue").ValueKind.Should().Be(JsonValueKind.Null);

        Guid id = body.RootElement.GetProperty("id").GetGuid();

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Projects.SingleAsync(p => p.Id == id, Ct)).ContractValue.Should().BeNull();
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

    // ---- D-227 item 1: the creator is assigned in the same save ------------------------------------

    [Fact]
    public async Task The_technical_office_creator_can_open_read_and_build_the_project_he_created_but_a_colleague_cannot()
    {
        HttpResponseMessage created = await CreateAsync(_technicalOffice, "مشروع المكتب الفني");
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid id = await CreatedIdAsync(created);

        (await SendAsync(HttpMethod.Get, $"/api/projects/{id}", _technicalOffice)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage list = await SendAsync(HttpMethod.Get, "/api/projects", _technicalOffice);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await list.Content.ReadAsStringAsync(Ct)).Should().Contain(id.ToString());

        (await SendAsync(
            HttpMethod.Post,
            $"/api/projects/{id}/boq/lines",
            _technicalOffice,
            new { catalogueItemId = _itemId, quantityEstimated = 5m, quantitySurveyed = 4m }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Get, $"/api/projects/{id}/boq", _technicalOffice)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(HttpMethod.Get, $"/api/projects/{id}", _otherTechnicalOffice)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, $"/api/projects/{id}/boq", _otherTechnicalOffice)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_creator_row_is_standard_audited_and_sits_beside_the_one_finance_row()
    {
        Guid id = await CreatedIdAsync(await CreateAsync(_technicalOffice, "مشروع الطاقم"));

        await using KaffDbContext reader = _database.CreateBareContext();
        List<ProjectAssignment> rows = await reader.ProjectAssignments.Where(a => a.ProjectId == id).ToListAsync(Ct);

        rows.Count(a => a.UserId == _technicalOffice).Should().Be(1);
        rows.Count(a => a.UserId == _finance).Should().Be(1);
        int financeActive = await reader.Users.CountAsync(u => u.Role == Role.Finance && u.IsActive, Ct);
        rows.Should().HaveCount(financeActive + 1, "Finance staffing is unchanged and only the creator is added");
        rows.Should().OnlyContain(a => a.Level == AssignmentLevel.Standard && a.AssignedByUserId == _technicalOffice);
        List<Guid> rowIds = rows.Select(a => a.Id).ToList();
        (await reader.AuditRecords.CountAsync(
            r => r.EntityType == nameof(ProjectAssignment) && rowIds.Contains(r.EntityId!.Value) && r.Action == AuditAction.Created, Ct))
            .Should().Be(financeActive + 1);
    }

    [Fact]
    public async Task An_owner_creator_gets_no_row_because_owner_reach_is_global()
    {
        Guid id = await CreatedIdAsync(await CreateAsync(_owner, "مشروع المالك"));

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.ProjectAssignments.AnyAsync(a => a.ProjectId == id && a.UserId == _owner, Ct)).Should().BeFalse();
        (await SendAsync(HttpMethod.Get, $"/api/projects/{id}", _owner)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Guid actorId, object? body = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        (string stamp, Role? role) = await SessionAsync(actorId);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, stamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, role!.Value.ToString());

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> CreateAsync(
        Guid actorId,
        string name,
        decimal? holdPercentage = 20m,
        string? typedContractValue = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/projects", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                name,
                clientId = _clientId,
                contractType = nameof(ContractType.LumpSum),
                contractValue = typedContractValue,
                // D-151: HoldPercentage is Percentage on the wire — the fraction, not the percent figure.
                holdPercentage = holdPercentage.HasValue
                    ? (holdPercentage.Value / 100m).ToString(CultureInfo.InvariantCulture)
                    : null,
                clientAdvance = "0.25",
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
        User otherTechnicalOffice = MakeUser(
            "crp-tech2", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        Bab bab = Bab.Create(UniqueNames.Code("CRB"), "أعمال خرسانة", "Concrete", Percentage.FromPercent(15m)).Value;
        CatalogueItem item = CatalogueItem.Create(
            UniqueNames.Code("CRI"), "خرسانة مسلحة", "م٣", bab.Id, new Money(1_000m), new Money(1_500m), "Reinforced concrete").Value;
        User finance = MakeUser("crp-finance", Role.Finance, WellKnownDepartments.FinanceId);
        User marketing = MakeUser("crp-marketing", Role.MarketingSales);
        User hr = MakeUser("crp-hr", Role.Hr, WellKnownDepartments.HrId);
        User siteEngineer = MakeUser(
            "crp-engineer", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);

        context.Clients.Add(client);
        context.Users.AddRange(owner, technicalOffice, otherTechnicalOffice, finance, marketing, hr, siteEngineer);
        context.Babs.Add(bab);
        context.CatalogueItems.Add(item);

        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _finance = finance.Id;
        _marketing = marketing.Id;
        _hr = hr.Id;
        _siteEngineer = siteEngineer.Id;
        _otherTechnicalOffice = otherTechnicalOffice.Id;
        _itemId = item.Id;
    }

    private static User MakeUser(
        string userName, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
