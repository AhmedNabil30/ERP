using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests.Features.Projects;

/// <summary>
/// KAFF-419 backend: no contract type default, one name per client, the quick-add client, Finance staffed at creation,
/// audit. D-217 items 34 and 36, D-218 items 8 and 9.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProjectCreateRulesTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _clientId;
    private Guid _otherClientId;
    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _finance;
    private Guid _inactiveFinance;
    private Guid _marketing;
    private Guid _siteEngineer;

    public ProjectCreateRulesTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-1 ---------------------------------------------------------------------------------------

    [Fact]
    public async Task A_request_with_no_contract_type_is_refused_and_nothing_is_created()
    {
        string name = UniqueNames.Code("no-type");
        HttpResponseMessage response = await PostAsync(_owner, "/api/projects", new { name, clientId = _clientId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.project.contract_type_required");

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Projects.AnyAsync(project => project.Name == name, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task A_contract_type_that_is_not_one_of_the_three_is_refused()
    {
        HttpResponseMessage response = await PostAsync(
            _owner, "/api/projects", new { name = "x", clientId = _clientId, contractType = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.project.contract_type_required");
    }

    // ---- AC-2 ---------------------------------------------------------------------------------------

    [Fact]
    public async Task The_same_name_for_the_same_client_is_refused_trimmed_and_ignoring_case()
    {
        string name = UniqueNames.Code("Villa");
        (await CreateProjectAsync(_owner, name, _clientId)).StatusCode.Should().Be(HttpStatusCode.Created);

        foreach (string again in new[] { name, $"  {name}  ", name.ToUpperInvariant(), name.ToLowerInvariant() })
        {
            HttpResponseMessage response = await CreateProjectAsync(_technicalOffice, again, _clientId);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict, again);
            (await MessageKeyAsync(response)).Should().Be("errors.project.name_taken_for_client");
        }
    }

    [Fact]
    public async Task The_same_name_for_another_client_is_accepted()
    {
        string name = UniqueNames.Code("Tower");
        (await CreateProjectAsync(_owner, name, _clientId)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await CreateProjectAsync(_owner, name, _otherClientId)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_stopped_project_still_holds_its_name()
    {
        string name = UniqueNames.Code("Stopped");
        Guid id = await IdOfAsync(await CreateProjectAsync(_owner, name, _clientId));

        await using (KaffDbContext context = _database.CreateContext())
        {
            Project project = await context.Projects.SingleAsync(candidate => candidate.Id == id, Ct);
            project.TransitionTo(ProjectStatus.Stopped, new DateOnly(2026, 11, 1), "test").IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(Ct);
        }

        (await CreateProjectAsync(_owner, name, _clientId)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task The_database_itself_refuses_a_second_project_of_the_same_name_for_a_client()
    {
        string name = UniqueNames.Code("Raw");
        await using KaffDbContext context = _database.CreateContext();
        context.Projects.Add(Project.Create(
            UniqueNames.Code("RW1"), name, _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value);
        context.Projects.Add(Project.Create(
            UniqueNames.Code("RW2"), name.ToUpperInvariant(), _clientId, ContractType.LumpSum, DateTimeOffset.UtcNow).Value);

        Func<Task> act = () => context.SaveChangesAsync(Ct);

        (await act.Should().ThrowAsync<DbUpdateException>()).Which.InnerException!.Message
            .Should().Contain("ux_projects_client_name");
    }

    // ---- AC-3 ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Whoever_may_create_a_project_may_quick_add_a_client()
    {
        foreach (Guid caller in new[] { _owner, _technicalOffice })
        {
            HttpResponseMessage response = await PostAsync(
                caller, "/api/clients/quick", QuickBody(UniqueNames.Code("عميل سريع"), UniqueNames.Phone().Entered));

            response.StatusCode.Should().Be(HttpStatusCode.Created);

            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            body.RootElement.GetProperty("kind").GetString().Should().Be(nameof(ClientKind.Individual));
        }
    }

    [Fact]
    public async Task Quick_add_is_refused_to_everyone_who_may_not_create_a_project()
    {
        foreach (Guid caller in new[] { _marketing, _finance, _siteEngineer })
        {
            HttpResponseMessage add = await PostAsync(
                caller, "/api/clients/quick", QuickBody(UniqueNames.Code("مرفوض"), UniqueNames.Phone().Entered));
            HttpResponseMessage check = await PostAsync(
                caller, "/api/clients/quick/phone-check", new { phone = UniqueNames.Phone().Entered });

            add.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            check.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task The_full_client_screen_keeps_its_own_permission()
    {
        HttpResponseMessage technicalOffice = await PostAsync(
            _technicalOffice, "/api/clients", new { name = "x", phone = UniqueNames.Phone().Entered, kind = "Individual" });
        HttpResponseMessage marketing = await PostAsync(
            _marketing, "/api/clients", new { name = UniqueNames.Code("full"), phone = UniqueNames.Phone().Entered, kind = "Individual" });

        technicalOffice.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the Technical Office holds ProjectCreate, not ClientManage");
        marketing.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Quick_add_without_a_kind_is_refused()
    {
        HttpResponseMessage response = await PostAsync(
            _owner, "/api/clients/quick", new { name = UniqueNames.Code("بلا نوع"), phone = UniqueNames.Phone().Entered });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.master.client_kind_required");
    }

    // ---- AC-4 ---------------------------------------------------------------------------------------

    [Fact]
    public async Task A_phone_that_belongs_to_another_client_warns_and_then_allows()
    {
        string phone = UniqueNames.Phone().Entered;
        (await PostAsync(_owner, "/api/clients/quick", QuickBody(UniqueNames.Code("الأول"), phone)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage check = await PostAsync(_technicalOffice, "/api/clients/quick/phone-check", new { phone });
        check.StatusCode.Should().Be(HttpStatusCode.OK);
        using (JsonDocument matches = JsonDocument.Parse(await check.Content.ReadAsStringAsync(Ct)))
        {
            matches.RootElement.GetProperty("matches").GetArrayLength().Should().Be(1);
        }

        HttpResponseMessage asked = await PostAsync(_technicalOffice, "/api/clients/quick", QuickBody(UniqueNames.Code("الثاني"), phone));
        asked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(asked)).Should().Be("errors.master.duplicate_phone_not_acknowledged");

        HttpResponseMessage allowed = await PostAsync(
            _technicalOffice, "/api/clients/quick", QuickBody(UniqueNames.Code("الثاني"), phone, acknowledged: true));
        allowed.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ---- Client picker (D-222) ----------------------------------------------------------------------

    [Fact]
    public async Task The_technical_office_can_list_clients_for_the_picker_with_id_and_name_only()
    {
        HttpResponseMessage response = await GetAsync(_technicalOffice, "/api/clients/picker");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        JsonElement item = body.RootElement.GetProperty("clients").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == _clientId);
        item.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["id", "name"]);
        item.GetProperty("name").GetString().Should().Be("عميل القواعد");
    }

    [Fact]
    public async Task The_picker_leaves_out_an_archived_client()
    {
        await using (KaffDbContext context = _database.CreateContext())
        {
            Client archived = await context.Clients.SingleAsync(c => c.Id == _otherClientId, Ct);
            archived.Archive().IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(Ct);
        }

        HttpResponseMessage response = await GetAsync(_technicalOffice, "/api/clients/picker");

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("clients").EnumerateArray()
            .Select(c => c.GetProperty("id").GetGuid()).Should().Contain(_clientId).And.NotContain(_otherClientId);
    }

    [Fact]
    public async Task The_picker_is_refused_to_a_role_without_project_create()
    {
        (await GetAsync(_siteEngineer, "/api/clients/picker")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetAsync(_finance, "/api/clients/picker")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- AC-5 ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Every_active_finance_user_is_assigned_to_a_new_project_and_an_inactive_one_is_not()
    {
        Guid id = await IdOfAsync(await CreateProjectAsync(_owner, UniqueNames.Code("Staffed"), _clientId));

        await using KaffDbContext reader = _database.CreateBareContext();
        List<ProjectAssignment> rows = await reader.ProjectAssignments.Where(a => a.ProjectId == id).ToListAsync(Ct);

        rows.Should().Contain(a => a.UserId == _finance && a.RevokedAt == null && a.Level == AssignmentLevel.Standard);
        rows.Should().NotContain(a => a.UserId == _inactiveFinance);
        rows.Select(a => a.UserId).Should().BeEquivalentTo(
            await reader.Users.Where(u => u.Role == Role.Finance && u.IsActive).Select(u => u.Id).ToListAsync(Ct));

        using HttpResponseMessage read = await GetAsync(_finance, $"/api/projects/{id}");
        read.StatusCode.Should().Be(HttpStatusCode.OK, "Finance reaches the project through the assignment row");
    }

    [Fact]
    public void The_finance_staffing_is_registered_as_an_assumed_ruling()
    {
        Kaff.Domain.Rulings.RulingNote note = Kaff.Domain.Rulings.RulingSet.Default.FinanceStaffing.Note;

        note.Status.Should().Be(Kaff.Domain.Rulings.RulingStatus.Assumed);
        note.Question.Should().Contain("Karim to confirm, D-217 item 36");
        Kaff.Domain.Rulings.RulingSet.Default.Notes.Should().Contain(note);
    }

    // ---- AC-7 ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Creating_a_project_and_quick_adding_a_client_each_write_an_audit_record_naming_the_actor()
    {
        Guid clientId = await IdOfAsync(await PostAsync(
            _technicalOffice, "/api/clients/quick", QuickBody(UniqueNames.Code("مدقق"), UniqueNames.Phone().Entered)));
        Guid projectId = await IdOfAsync(await CreateProjectAsync(_technicalOffice, UniqueNames.Code("Audited"), clientId));

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord clientRecord = await reader.AuditRecords.SingleAsync(
            r => r.EntityType == nameof(Client) && r.EntityId == clientId && r.Action == AuditAction.Created, Ct);
        AuditRecord projectRecord = await reader.AuditRecords.SingleAsync(
            r => r.EntityType == nameof(Project) && r.EntityId == projectId && r.Action == AuditAction.Created, Ct);

        clientRecord.ActorUserId.Should().Be(_technicalOffice);
        projectRecord.ActorUserId.Should().Be(_technicalOffice);
        projectRecord.AfterJson.Should().NotBeNull();
    }

    // ---- helpers ------------------------------------------------------------------------------------

    private static object QuickBody(string name, string phone, bool acknowledged = false) => new
    {
        name,
        phone,
        kind = nameof(ClientKind.Individual),
        acknowledgedDuplicatePhone = acknowledged,
    };

    private Task<HttpResponseMessage> CreateProjectAsync(Guid actor, string name, Guid clientId) => PostAsync(
        actor,
        "/api/projects",
        new { name, clientId, contractType = nameof(ContractType.LumpSum) });

    private async Task<HttpResponseMessage> PostAsync(Guid actorId, string route, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(route, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };

        await SignAsync(request, actorId);
        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> GetAsync(Guid actorId, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));
        await SignAsync(request, actorId);
        return await _client.SendAsync(request, Ct);
    }

    private async Task SignAsync(HttpRequestMessage request, Guid actorId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        var found = await reader.Users.Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role }).FirstAsync(Ct);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, found.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, found.Role.ToString());
    }

    private static async Task<Guid> IdOfAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(UniqueNames.Code("PCR-C"), "عميل القواعد", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        Client other = Client.Create(UniqueNames.Code("PCR-D"), "عميل آخر", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        User owner = MakeUser("pcr-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "pcr-tech", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User finance = MakeUser("pcr-fin", Role.Finance, WellKnownDepartments.FinanceId);
        User inactiveFinance = MakeUser("pcr-fin-off", Role.Finance, WellKnownDepartments.FinanceId);
        User marketing = MakeUser("pcr-mkt", Role.MarketingSales);
        User engineer = MakeUser(
            "pcr-eng", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);

        context.Clients.AddRange(client, other);
        context.Users.AddRange(owner, technicalOffice, finance, inactiveFinance, marketing, engineer);
        await context.SaveChangesAsync(Ct);

        inactiveFinance.Deactivate(Now).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _otherClientId = other.Id;
        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _finance = finance.Id;
        _inactiveFinance = inactiveFinance.Id;
        _marketing = marketing.Id;
        _siteEngineer = engineer.Id;
    }

    private static User MakeUser(
        string userName, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
