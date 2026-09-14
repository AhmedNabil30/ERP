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
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-209 — <c>/api/projects/{projectId}/day-labour</c> and its three sibling routes.
/// decisions.md D-139 §§1–2, D-140, D-141, D-146.
/// </summary>
/// <remarks>
/// Named tests are D-140's SM-30 list, tests 5–13 (test 14 belongs to KAFF-210).
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class RegisterDayLabourerFromSiteTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _bab;

    private Guid _owner;
    private Guid _assignedEngineer;
    private Guid _unassignedEngineer;
    private Guid _hr;
    private Guid _finance;
    private Guid _technicalOffice;
    private Guid _headOfDesign;
    private Guid _marketing;

    public RegisterDayLabourerFromSiteTests(PostgresDatabase database) => _database = database;

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

    // ---- D-140 SM-30 test 5 / AC-209-A ------------------------------------------------------------

    [Fact]
    public async Task An_assigned_site_engineer_registers_a_day_labourer_from_site()
    {
        HttpResponseMessage response = await RegisterAsync(
            _projectA, _assignedEngineer, Role.SiteEngineer, "Worker One", UniqueNames.Phone().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Guid id = body.RootElement.GetProperty("id").GetGuid();

        await using KaffDbContext reader = _database.CreateBareContext();
        Employee stored = await reader.Employees.SingleAsync(e => e.Id == id, Ct);

        stored.Kind.Should().Be(EmployeeKind.DayLabour);
        stored.FullName.Should().Be("Worker One");
        stored.BabId.Should().Be(_bab);

        // The pool sees him too — AC-209-A.
        HttpResponseMessage pool = await GetAsync($"/api/projects/{_projectA}/day-labour", _owner, Role.Owner);
        using JsonDocument poolBody = JsonDocument.Parse(await pool.Content.ReadAsStringAsync(Ct));

        poolBody.RootElement.GetProperty("items").EnumerateArray()
            .Should().ContainSingle(item => item.GetProperty("id").GetGuid() == id);
    }

    // ---- AC-209-A · the request type carries no Kind member ---------------------------------------

    [Fact]
    public void The_register_request_carries_no_Kind_member()
    {
        Type requestType = typeof(Api.Features.DayLabour.RegisterFromSite.Request);

        requestType.GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(
                ["FullName", "Phone", "BabId", "Specialty", "AcknowledgedDuplicatePhone"],
                "D-140's own reasoning: leaving Kind out is stronger than validating it");
    }

    // ---- D-140 SM-30 test 6 / AC-209-E ------------------------------------------------------------

    [Fact]
    public async Task An_unassigned_site_engineer_is_refused_registration()
    {
        HttpResponseMessage response = await RegisterAsync(
            _projectA, _unassignedEngineer, Role.SiteEngineer, "Should Not Exist", UniqueNames.Phone().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Employees.AnyAsync(e => e.FullName == "Should Not Exist", Ct))
            .Should().BeFalse("no worker is created by the refused call");
    }

    // ---- D-140 SM-30 test 7 ------------------------------------------------------------------------

    [Fact]
    public async Task A_site_engineer_cannot_register_a_salaried_record_by_sending_kind()
    {
        string phone = UniqueNames.Phone().ToString();

        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/projects/{_projectA}/day-labour", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                fullName = "Sneaky Kind",
                phone,
                babId = _bab,
                specialty = (string?)null,
                acknowledgedDuplicatePhone = false,
                kind = "Salaried",
            }),
        };

        await StampAsync(request, _assignedEngineer, Role.SiteEngineer);
        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext reader = _database.CreateBareContext();
        Employee stored = await reader.Employees.SingleAsync(e => e.FullName == "Sneaky Kind", Ct);
        stored.Kind.Should().Be(EmployeeKind.DayLabour, "the request type has no Kind member to bind to");
    }

    // ---- D-140 SM-30 test 8 -------------------------------------------------------------------------

    [Fact]
    public async Task A_site_engineer_is_refused_every_EmployeeManage_route()
    {
        Guid someSalariedId = await CreateSalariedAsync();

        (await GetAsync("/api/employees", _assignedEngineer, Role.SiteEngineer))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await RegisterViaEmployeeManageAsync())
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await GetAsync($"/api/employees/{someSalariedId}", _assignedEngineer, Role.SiteEngineer))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await GetAsync("/api/employees/babs", _assignedEngineer, Role.SiteEngineer))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- D-140 SM-30 test 9 -------------------------------------------------------------------------

    [Fact]
    public async Task The_site_engineer_pool_returns_no_salaried_row_and_no_staff_field()
    {
        Guid salariedId = await CreateSalariedAsync();

        HttpResponseMessage registered = await RegisterAsync(
            _projectA, _assignedEngineer, Role.SiteEngineer, "Pool Worker", UniqueNames.Phone().ToString());
        registered.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage pool = await GetAsync(
            $"/api/projects/{_projectA}/day-labour", _assignedEngineer, Role.SiteEngineer);
        pool.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await pool.Content.ReadAsStringAsync(Ct));
        JsonElement[] items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();

        items.Should().NotBeEmpty();
        items.Should().NotContain(item => item.GetProperty("id").GetGuid() == salariedId,
            "the salaried row is filtered in the EF query, not after loading");

        foreach (JsonElement item in items)
        {
            item.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
                ["id", "code", "fullName", "phone", "babId", "specialty", "isActive", "frequency", "averageRating"],
                "D-140 point 4's allow-list, plus the two non-money figures D-153 §1 point 5 adds — "
                + "frequency and average rating stay available here; the average day rate stays "
                + "gated behind DayLabourRateManage's own ListEngagements read");
        }
    }

    // ---- D-140 SM-30 test 10 ------------------------------------------------------------------------

    [Fact]
    public async Task The_site_engineer_bab_options_carry_no_markup()
    {
        HttpResponseMessage response = await GetAsync(
            $"/api/projects/{_projectA}/day-labour/babs", _assignedEngineer, Role.SiteEngineer);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        JsonElement[] items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();

        items.Should().Contain(item => item.GetProperty("id").GetGuid() == _bab);

        foreach (JsonElement item in items)
        {
            item.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
                ["id", "code", "nameAr", "nameEn", "parentBabId", "isActive"],
                "D-137's own allow-list, unchanged on the new route");
        }
    }

    // ---- D-140 SM-30 test 11 / AC-209-K --------------------------------------------------------------

    [Fact]
    public async Task A_salaried_phone_match_is_restricted_for_a_site_engineer()
    {
        string phone = UniqueNames.Phone().ToString();
        Guid salariedId = await CreateSalariedAsync(phone);

        HttpResponseMessage check = await PhoneCheckAsync(_projectA, phone, _assignedEngineer);
        check.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument checkBody = JsonDocument.Parse(await check.Content.ReadAsStringAsync(Ct));
        JsonElement[] matches = checkBody.RootElement.GetProperty("matches").EnumerateArray().ToArray();

        matches.Should().ContainSingle();
        JsonElement match = matches[0];

        match.GetProperty("restricted").GetBoolean().Should().BeTrue();
        match.TryGetProperty("id", out _).Should().BeFalse("no id, name or code — D-140 point 6");
        match.TryGetProperty("name", out _).Should().BeFalse();
        match.TryGetProperty("code", out _).Should().BeFalse();

        HttpResponseMessage registered = await RegisterAsync(
            _projectA, _assignedEngineer, Role.SiteEngineer, "Cross Match", phone, acknowledgedDuplicatePhone: true);

        registered.StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord acknowledgement = await reader.AuditRecords.SingleAsync(
            record => record.EventType == AuditEventKind.DuplicatePhoneAcknowledged
                      && record.EntityId == salariedId,
            Ct);

        acknowledgement.EntityType.Should().Be(nameof(Employee),
            "the audit row names the real salaried id, never exposed to the response");
    }

    // ---- D-140 SM-30 test 12 / AC-209-F --------------------------------------------------------------

    [Fact]
    public async Task Every_role_without_DayLabourSiteManage_is_refused()
    {
        foreach ((Guid actor, Role role) in new[]
        {
            (_finance, Role.Finance),
            (_marketing, Role.MarketingSales),
            (_technicalOffice, Role.TechnicalOffice),
            (_headOfDesign, Role.HeadOfDesign),
            (_hr, Role.Hr),
        })
        {
            (await RegisterAsync(_projectA, actor, role, $"Refused {role}", UniqueNames.Phone().ToString()))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} holds neither DayLabourSiteManage nor a way into this route", role);

            (await GetAsync($"/api/projects/{_projectA}/day-labour", actor, role))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);

            (await GetAsync($"/api/projects/{_projectA}/day-labour/babs", actor, role))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);

            (await PhoneCheckAsync(_projectA, UniqueNames.Phone().ToString(), actor, role))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.Employees.AnyAsync(e => e.FullName.StartsWith("Refused "), Ct))
            .Should().BeFalse("no worker is created by any refused call");
    }

    // ---- D-140 SM-30 test 13 / AC-209-H --------------------------------------------------------------

    [Fact]
    public async Task Registration_from_site_is_audited_with_its_project()
    {
        HttpResponseMessage response = await RegisterAsync(
            _projectA, _assignedEngineer, Role.SiteEngineer, "Audited Worker", UniqueNames.Phone().ToString());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Guid id = body.RootElement.GetProperty("id").GetGuid();

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords.SingleAsync(
            r => r.EntityId == id && r.Action == AuditAction.Created, Ct);

        record.EntityType.Should().Be(nameof(Employee));
        record.ActorUserId.Should().Be(_assignedEngineer);

        // decisions.md D-148. Employee carries no ProjectId of its own (D-140 point 3: no
        // RegisteredOnProjectId column), so AuditSaveChangesInterceptor falls back to the project the
        // gate granted this request against — IAuditContext.GrantProjectId, set by ScopedTo in the
        // granted branch of PermissionAuthorizationHandler — and pairs it with the path on identity.
        record.ProjectId.Should().Be(_projectA);
        record.GrantPath.Should().Be(ProjectAccessPath.Assignment);
    }

    // ---- AC-209-B · a worker without a باب is refused, handler and database -------------------------

    [Fact]
    public async Task A_worker_without_a_bab_is_refused_by_the_handler()
    {
        HttpResponseMessage response = await RegisterAsync(
            _projectA, _assignedEngineer, Role.SiteEngineer, "No Trade", UniqueNames.Phone().ToString(), omitBab: true);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.master.day_labour_requires_trade");
    }

    [Fact]
    public async Task A_day_labour_row_with_no_bab_is_also_refused_by_the_database_check_constraint()
    {
        await using KaffDbContext context = _database.CreateContext();

        Func<Task> insert = async () => await context.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO employees
                 (id, code, full_name, phone_entered, phone_normalised, kind, bab_id, is_active, created_at)
               VALUES
                 (gen_random_uuid(), {UniqueNames.Code("RAWSITE")}, 'Raw Site Insert', '01000000001',
                  '01000000001', 'DayLabour', NULL, TRUE, now())",
            Ct);

        (await insert.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    // ---- AC-209-D / TC-2-108 · a repeated day-labour phone warns and is acknowledged ------------------

    [Fact]
    public async Task A_repeated_day_labour_phone_warns_and_is_acknowledged_never_refused()
    {
        string phone = UniqueNames.Phone().ToString();

        HttpResponseMessage first = await RegisterAsync(
            _projectA, _assignedEngineer, Role.SiteEngineer, "First Worker", phone);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync(Ct));
        Guid firstId = firstBody.RootElement.GetProperty("id").GetGuid();

        HttpResponseMessage unacknowledged = await RegisterAsync(
            _projectA, _assignedEngineer, Role.SiteEngineer, "Second Worker", phone);

        unacknowledged.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(unacknowledged)).Should().Be("errors.master.duplicate_phone_not_acknowledged");

        HttpResponseMessage acknowledged = await RegisterAsync(
            _projectA, _assignedEngineer, Role.SiteEngineer, "Second Worker", phone, acknowledgedDuplicatePhone: true);

        acknowledged.StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext reader = _database.CreateBareContext();
        string normalised = PhoneNumber.Create(phone).Value.Normalised;

        (await reader.Employees.CountAsync(e => e.PhoneNormalised == normalised, Ct)).Should().Be(
            2, "both workers exist");

        // Filtered by the matched entity's id, not a global count — the shared test database (per
        // PostgresDatabase collection fixture) carries DuplicatePhoneAcknowledged rows from every other
        // test in the run.
        (await reader.AuditRecords.CountAsync(
            r => r.EventType == AuditEventKind.DuplicatePhoneAcknowledged && r.EntityId == firstId, Ct))
            .Should().Be(1, "one record, against the first worker who was matched");
    }

    // ---- AC-209-G · no rate captured -------------------------------------------------------------------

    [Fact]
    public void No_money_member_appears_on_the_request_or_the_response()
    {
        string[] forbidden = ["rate", "wage", "salary", "money", "amount", "day_rate", "dayrate"];

        foreach (Type type in new[]
        {
            typeof(Api.Features.DayLabour.RegisterFromSite.Request),
            typeof(Api.Features.DayLabour.RegisterFromSite.Response),
        })
        {
            foreach (System.Reflection.PropertyInfo property in type.GetProperties())
            {
                string lowered = property.Name.ToLowerInvariant();
                forbidden.Should().NotContain(term => lowered.Contains(term, StringComparison.Ordinal),
                    $"{type.Name}.{property.Name} must not be money-typed — rule 8, AC-209-G");
            }
        }
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private Task<HttpResponseMessage> RegisterAsync(
        Guid projectId, Guid actorId, Role actorRole, string fullName, string phone,
        bool omitBab = false, bool acknowledgedDuplicatePhone = false)
    {
        return SendAsync(
            HttpMethod.Post,
            $"/api/projects/{projectId}/day-labour",
            actorId,
            actorRole,
            new
            {
                fullName,
                phone,
                babId = omitBab ? (Guid?)null : _bab,
                specialty = "عام",
                acknowledgedDuplicatePhone,
            });
    }

    private Task<HttpResponseMessage> GetAsync(string route, Guid actorId, Role actorRole)
        => SendAsync(HttpMethod.Get, route, actorId, actorRole, body: null);

    private Task<HttpResponseMessage> PhoneCheckAsync(Guid projectId, string phone, Guid actorId, Role? actorRole = null)
        => SendAsync(
            HttpMethod.Post,
            $"/api/projects/{projectId}/day-labour/phone-check",
            actorId,
            actorRole ?? Role.SiteEngineer,
            new { phone });

    private Task<HttpResponseMessage> RegisterViaEmployeeManageAsync()
        => SendAsync(
            HttpMethod.Post,
            "/api/employees",
            _assignedEngineer,
            Role.SiteEngineer,
            new
            {
                fullName = "Blocked",
                phone = UniqueNames.Phone().ToString(),
                kind = nameof(EmployeeKind.DayLabour),
                babId = _bab,
            });

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string route, Guid actorId, Role actorRole, object? body)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        await StampAsync(request, actorId, actorRole);

        return await _client.SendAsync(request, Ct);
    }

    private async Task StampAsync(HttpRequestMessage request, Guid actorId, Role actorRole)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        Guid? department = actorRole switch
        {
            Role.Hr => WellKnownDepartments.HrId,
            Role.Finance => WellKnownDepartments.FinanceId,
            Role.MarketingSales => null,
            Role.TechnicalOffice or Role.SiteEngineer or Role.HeadOfDesign => WellKnownDepartments.OperationsId,
            _ => null,
        };

        if (department is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, department.Value.ToString());
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

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;
    }

    private async Task<Guid> CreateSalariedAsync(string? phone = null)
    {
        await using KaffDbContext context = _database.CreateContext();

        Employee employee = Employee.Create(
            UniqueNames.Code("SAL"),
            "Salaried Person",
            PhoneNumber.Create(phone ?? UniqueNames.Phone().ToString()).Value,
            EmployeeKind.Salaried,
            Now).Value;

        context.Employees.Add(employee);
        await context.SaveChangesAsync(Ct);

        return employee.Id;
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("DL-C1"), "عميل العمالة", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("DL-PA"), "مشروع العمالة", client.Id, ContractType.LumpSum, Now).Value;

        Bab bab = Bab.Create(UniqueNames.Code("DL-BAB"), "باب", "Bab", Percentage.FromPercent(15m)).Value;

        User owner = MakeUser("dl-owner", Role.Owner);
        User assignedEngineer = MakeUser(
            "dl-engineer-a", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User unassignedEngineer = MakeUser(
            "dl-engineer-u", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User hr = MakeUser("dl-hr", Role.Hr, WellKnownDepartments.HrId);
        User finance = MakeUser("dl-finance", Role.Finance, WellKnownDepartments.FinanceId);
        User technicalOffice = MakeUser(
            "dl-tech", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User headOfDesign = MakeUser(
            "dl-design", Role.HeadOfDesign, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User marketing = MakeUser("dl-marketing", Role.MarketingSales, null);

        context.Clients.Add(client);
        context.Projects.Add(projectA);
        context.Babs.Add(bab);
        context.Users.AddRange(
            owner, assignedEngineer, unassignedEngineer, hr, finance, technicalOffice, headOfDesign, marketing);

        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.Add(
            ProjectAssignment.Create(projectA.Id, assignedEngineer, AssignmentLevel.Junior, owner.Id, Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _bab = bab.Id;
        _owner = owner.Id;
        _assignedEngineer = assignedEngineer.Id;
        _unassignedEngineer = unassignedEngineer.Id;
        _hr = hr.Id;
        _finance = finance.Id;
        _technicalOffice = technicalOffice.Id;
        _headOfDesign = headOfDesign.Id;
        _marketing = marketing.Id;
    }

    private static User MakeUser(
        string userName, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
