using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// <c>GET /api/employees/{employeeId}</c> — closes the silent data loss Frontend found (<c>31b049f</c>):
/// <c>GET /api/employees</c> (<c>EmployeeSummary</c>) does not return <c>NationalId</c>, <c>JobTitle</c>
/// or <c>HiredOn</c>, no by-id read existed, and <c>EditEmployee.Handler</c> applies its whole body on
/// every <c>PUT</c> — so an edit screen could not load the three staff fields, and any edit wiped them.
/// This is the <c>GetClient</c> precedent applied to employees.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class GetEmployeeTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _hr;
    private Guid _finance;
    private Guid _marketing;
    private Guid _siteEngineer;
    private Guid _technicalOffice;
    private Guid _headOfDesign;
    private Guid _portalClient;
    private Guid _portalClientCompany;

    public GetEmployeeTests(PostgresDatabase database) => _database = database;

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

    // ---- the defect: a full-body PUT after a GET must not wipe the staff fields --------------------

    [Fact]
    public async Task A_put_that_only_changes_the_name_after_loading_the_get_leaves_the_staff_fields_unchanged()
    {
        (Guid id, string phone) = await CreateEmployeeWithStaffDetailsAsync(
            nationalId: "29001011234567", department: "Finance", jobTitle: "مهندس موقع",
            hiredOn: new DateOnly(2024, 3, 1));

        HttpResponseMessage getResponse = await GetAsync(id, _hr, Role.Hr, Department.Hr);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument loaded = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(Ct));

        loaded.RootElement.GetProperty("nationalId").GetString().Should().Be("29001011234567");
        loaded.RootElement.GetProperty("department").GetString().Should().Be("Finance");
        loaded.RootElement.GetProperty("jobTitle").GetString().Should().Be("مهندس موقع");
        loaded.RootElement.GetProperty("hiredOn").GetString().Should().Be("2024-03-01");

        // The edit form round-trips the loaded body, changing only the name.
        HttpResponseMessage putResponse = await SendAsync(
            HttpMethod.Put, $"/api/employees/{id}", _hr, Role.Hr, Department.Hr,
            new
            {
                fullName = "New Name",
                phone,
                kind = nameof(EmployeeKind.Salaried),
                babId = (Guid?)null,
                specialty = (string?)null,
                nationalId = "29001011234567",
                department = "Finance",
                jobTitle = "مهندس موقع",
                hiredOn = "2024-03-01",
            });

        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        Employee stored = await reader.Employees.SingleAsync(candidate => candidate.Id == id, Ct);

        stored.FullName.Should().Be("New Name");
        stored.NationalId.Should().Be(
            "29001011234567", "the PUT round-tripped what the GET loaded — the staff fields must survive");
        stored.Department.Should().Be("Finance");
        stored.JobTitle.Should().Be("مهندس موقع");
        stored.HiredOn.Should().Be(new DateOnly(2024, 3, 1));
    }

    // ---- permission refusals, including an executed Role.Client call --------------------------------

    [Fact]
    public async Task Only_hr_and_the_owner_may_read_an_employee_by_id()
    {
        (Guid id, _) = await CreateEmployeeWithStaffDetailsAsync(null, null, null, null);

        foreach ((Guid actorId, Role role, Department? department, Guid? clientId) in RefusedActors())
        {
            HttpResponseMessage refused = await GetAsync(id, actorId, role, department, clientId);

            refused.StatusCode.Should().Be(
                HttpStatusCode.Forbidden, $"{role} does not hold EmployeeManage");
        }

        (await GetAsync(id, _owner, Role.Owner, null)).StatusCode.Should().Be(
            HttpStatusCode.OK, "the Owner holds every company-wide row — decisions.md D-129 §1");
    }

    private IEnumerable<(Guid ActorId, Role Role, Department? Department, Guid? ClientId)> RefusedActors()
    {
        yield return (_finance, Role.Finance, Department.Finance, null);
        yield return (_marketing, Role.MarketingSales, Department.Marketing, null);
        yield return (_siteEngineer, Role.SiteEngineer, Department.Operations, null);
        yield return (_technicalOffice, Role.TechnicalOffice, Department.Operations, null);
        yield return (_headOfDesign, Role.HeadOfDesign, Department.Operations, null);
        yield return (_portalClient, Role.Client, null, _portalClientCompany);
    }

    // ---- unknown id -----------------------------------------------------------------------------

    [Fact]
    public async Task An_unknown_id_says_so_in_a_translatable_way()
    {
        HttpResponseMessage response = await GetAsync(Guid.NewGuid(), _hr, Role.Hr, Department.Hr);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        problem.RootElement.GetProperty("messageKey").GetString().Should().Be("errors.master.employee_not_found");
    }

    // ---- it is a read: zero audit rows ------------------------------------------------------------

    [Fact]
    public async Task The_read_writes_no_audit_record()
    {
        (Guid id, _) = await CreateEmployeeWithStaffDetailsAsync(null, null, null, null);

        await using KaffDbContext before = _database.CreateBareContext();
        long countBefore = await before.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct);

        (await GetAsync(id, _hr, Role.Hr, Department.Hr)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext after = _database.CreateBareContext();
        long countAfter = await after.AuditRecords.LongCountAsync(candidate => candidate.EntityId == id, Ct);

        countAfter.Should().Be(countBefore, "a read writes no audit record");
        countAfter.Should().Be(1, "exactly the one Created record from registration — the read added none");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<(Guid Id, string Phone)> CreateEmployeeWithStaffDetailsAsync(
        string? nationalId, string? department, string? jobTitle, DateOnly? hiredOn)
    {
        string phone = UniqueNames.Phone().ToString();

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/employees", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                fullName = "Original Name",
                phone,
                kind = nameof(EmployeeKind.Salaried),
                babId = (Guid?)null,
                specialty = (string?)null,
                nationalId,
                department,
                jobTitle,
                hiredOn,
            }),
        };

        await StampAsync(request, _hr, Role.Hr, Department.Hr, null);

        HttpResponseMessage response = await _client.SendAsync(request, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return (body.RootElement.GetProperty("id").GetGuid(), phone);
    }

    private async Task<HttpResponseMessage> GetAsync(
        Guid employeeId, Guid actorId, Role actorRole, Department? actorDepartment, Guid? actorClientId = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"/api/employees/{employeeId}", UriKind.Relative));

        await StampAsync(request, actorId, actorRole, actorDepartment, actorClientId);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string route, Guid actorId, Role actorRole, Department? actorDepartment, object body)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };

        await StampAsync(request, actorId, actorRole, actorDepartment, null);

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

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client company = Client.Create(
            UniqueNames.Code("GE-C1"), "عميل بوابة الموظفين", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        User owner = MakeUser("ge-owner", Role.Owner);
        User hr = MakeUser("ge-hr", Role.Hr, Department.Hr);
        User finance = MakeUser("ge-finance", Role.Finance, Department.Finance);
        User marketing = MakeUser("ge-marketing", Role.MarketingSales, Department.Marketing);
        User siteEngineer = MakeUser(
            "ge-siteeng", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User technicalOffice = MakeUser(
            "ge-techoffice", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User headOfDesign = MakeUser(
            "ge-headdesign", Role.HeadOfDesign, Department.Operations, OperationsSubDepartment.Technical);
        User portal = MakeUser("ge-portal", Role.Client, clientId: company.Id);

        context.Clients.Add(company);
        context.Users.AddRange(
            owner, hr, finance, marketing, siteEngineer, technicalOffice, headOfDesign, portal);

        await context.SaveChangesAsync(Ct);

        _portalClientCompany = company.Id;
        _owner = owner.Id;
        _hr = hr.Id;
        _finance = finance.Id;
        _marketing = marketing.Id;
        _siteEngineer = siteEngineer.Id;
        _technicalOffice = technicalOffice.Id;
        _headOfDesign = headOfDesign.Id;
        _portalClient = portal.Id;
    }

    private static User MakeUser(
        string userName,
        Role role,
        Department? department = null,
        OperationsSubDepartment? subDepartment = null,
        Guid? clientId = null)
        => User.Create(
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment,
            clientId).Value;

    private static DateTimeOffset Now => new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
