using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// <c>GET /api/employees</c> as the people screens read it (D-231, D-232, D-233 item 1): each population asks for its own
/// rows, a salaried row carries job title, department and hire date, a day-labour row carries the باب name and the two
/// company-wide figures, and nobody reads a rate or any money here.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ListEmployeesTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _hr;
    private Guid _owner;
    private Guid _finance;
    private Guid _siteEngineer;
    private Guid _staff;
    private Guid _ratedWorker;
    private Guid _freshWorker;

    public ListEmployeesTests(PostgresDatabase database) => _database = database;

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

    [Fact]
    public async Task A_salaried_row_carries_job_title_department_and_hire_date_and_a_worker_row_carries_none_of_them()
    {
        JsonElement items = await ListAsync(_hr, Role.Hr, "?kind=Salaried");

        JsonElement staff = items.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == _staff);
        staff.GetProperty("jobTitle").GetString().Should().Be("محاسب");
        staff.GetProperty("department").GetString().Should().Be("الحسابات");
        staff.GetProperty("hiredOn").GetString().Should().Be("2025-03-01");

        JsonElement workers = await ListAsync(_hr, Role.Hr, "?kind=DayLabour");
        JsonElement worker = workers.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == _ratedWorker);
        worker.GetProperty("jobTitle").ValueKind.Should().Be(JsonValueKind.Null);
        worker.GetProperty("department").ValueKind.Should().Be(JsonValueKind.Null);
        worker.GetProperty("hiredOn").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_kind_filter_gives_each_screen_only_its_own_population()
    {
        Guid[] salaried = IdsOf(await ListAsync(_hr, Role.Hr, "?kind=Salaried"));
        Guid[] labour = IdsOf(await ListAsync(_hr, Role.Hr, "?kind=DayLabour"));

        salaried.Should().Contain(_staff).And.NotContain([_ratedWorker, _freshWorker]);
        labour.Should().Contain([_ratedWorker, _freshWorker]).And.NotContain(_staff);
    }

    [Fact]
    public async Task A_worker_row_names_its_bab_and_carries_times_engaged_and_average_rating_company_wide()
    {
        JsonElement items = await ListAsync(_hr, Role.Hr, "?kind=DayLabour");

        JsonElement rated = items.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == _ratedWorker);
        rated.GetProperty("babNameAr").GetString().Should().Be("نجارة");
        rated.GetProperty("babNameEn").GetString().Should().Be("Carpentry");
        rated.GetProperty("timesEngaged").GetInt32().Should().Be(2, "two engagements, on two different projects");
        decimal.Parse(rated.GetProperty("averageRating").GetString()!, System.Globalization.CultureInfo.InvariantCulture).Should().Be(4.5m);

        JsonElement fresh = items.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == _freshWorker);
        fresh.GetProperty("timesEngaged").GetInt32().Should().Be(0);
        fresh.GetProperty("averageRating").ValueKind.Should().Be(JsonValueKind.Null, "an unrated man is not a zero rating");

        JsonElement staff = (await ListAsync(_hr, Role.Hr, "?kind=Salaried"))
            .EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == _staff);
        staff.GetProperty("babNameAr").ValueKind.Should().Be(JsonValueKind.Null);
        staff.GetProperty("timesEngaged").ValueKind.Should().Be(JsonValueKind.Null, "a salaried row has no engagements");
        staff.GetProperty("averageRating").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_raw_body_read_as_hr_carries_no_rate_and_no_money()
    {
        // One engagement of the rated worker has a day rate stored; it must not surface on this read (D-232 item 4).
        HttpResponseMessage response = await SendAsync(_hr, Role.Hr, string.Empty);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        foreach (JsonElement item in body.RootElement.GetProperty("items").EnumerateArray())
        {
            foreach (JsonProperty property in item.EnumerateObject())
            {
                property.Name.Should().NotContainAny(["rate", "Rate", "salary", "wage", "amount", "pay"]);

                if (property.Value.ValueKind == JsonValueKind.Number)
                {
                    property.Name.Should().BeOneOf("timesEngaged", "averageRating", "kind");
                }
            }
        }

    }

    [Fact]
    public async Task A_role_without_the_register_permission_is_refused()
    {
        foreach ((Guid caller, Role role) in new[] { (_finance, Role.Finance), (_siteEngineer, Role.SiteEngineer) })
        {
            (await SendAsync(caller, role, "?kind=DayLabour")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await SendAsync(_owner, Role.Owner, "?kind=DayLabour")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- helpers ------------------------------------------------------------------------------------

    private static Guid[] IdsOf(JsonElement items) =>
        [.. items.EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    private async Task<JsonElement> ListAsync(Guid actor, Role role, string query)
    {
        HttpResponseMessage response = await SendAsync(actor, role, query);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return body.RootElement.GetProperty("items").Clone();
    }

    private async Task<HttpResponseMessage> SendAsync(Guid actor, Role role, string query)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/api/employees{query}", UriKind.Relative));

        await using KaffDbContext reader = _database.CreateBareContext();
        string stamp = await reader.Users.Where(user => user.Id == actor).Select(user => user.SecurityStamp).SingleAsync(Ct);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actor.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, role.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, stamp);

        Guid? department = role switch
        {
            Role.Hr => WellKnownDepartments.HrId,
            Role.Finance => WellKnownDepartments.FinanceId,
            Role.SiteEngineer => WellKnownDepartments.OperationsId,
            _ => null,
        };

        if (department is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, department.Value.ToString());
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("LE-C"), "عميل", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        Project projectA = Project.Create(UniqueNames.Code("LE-PA"), "مشروع أ", client.Id, ContractType.LumpSum, Now).Value;
        Project projectB = Project.Create(UniqueNames.Code("LE-PB"), "مشروع ب", client.Id, ContractType.LumpSum, Now).Value;
        Bab bab = Bab.Create(UniqueNames.Code("LE-BAB"), "نجارة", "Carpentry", Percentage.FromPercent(15m)).Value;

        Employee staff = Employee.Create(
            UniqueNames.Code("LE-S"), "موظف", UniqueNames.Phone(), EmployeeKind.Salaried, Now).Value;
        staff.SetStaffDetails(null, "الحسابات", "محاسب", new DateOnly(2025, 3, 1));

        Employee rated = Employee.Create(
            UniqueNames.Code("LE-W1"), "عامل أول", UniqueNames.Phone(), EmployeeKind.DayLabour, Now, bab.Id).Value;
        Employee fresh = Employee.Create(
            UniqueNames.Code("LE-W2"), "عامل ثان", UniqueNames.Phone(), EmployeeKind.DayLabour, Now, bab.Id).Value;

        Engagement first = Engagement.Open(rated.Id, projectA.Id, Today, Now).Value;
        first.SetDayRate(Money.From(750m));
        first.Rate(4);
        Engagement second = Engagement.Open(rated.Id, projectB.Id, Today, Now).Value;
        second.Rate(5);

        User owner = MakeUser("le-owner", Role.Owner);
        User hr = MakeUser("le-hr", Role.Hr, WellKnownDepartments.HrId);
        User finance = MakeUser("le-finance", Role.Finance, WellKnownDepartments.FinanceId);
        User engineer = MakeUser(
            "le-engineer", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);

        context.Clients.Add(client);
        context.Projects.AddRange(projectA, projectB);
        context.Babs.Add(bab);
        context.Employees.AddRange(staff, rated, fresh);
        context.Engagements.AddRange(first, second);
        context.Users.AddRange(owner, hr, finance, engineer);
        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _hr = hr.Id;
        _finance = finance.Id;
        _siteEngineer = engineer.Id;
        _staff = staff.Id;
        _ratedWorker = rated.Id;
        _freshWorker = fresh.Id;
    }

    private static User MakeUser(
        string userName, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
