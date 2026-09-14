using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-210 — the day rate's own permission row and the two routes it gates. decisions.md D-152 §2
/// (Q76), D-153 §1.
/// </summary>
/// <remarks>SM-30 tests 5-14, D-153 §1's own list.</remarks>
[Collection(DatabaseCollection.Name)]
public sealed class DayLabourRateManageTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _projectB;
    private Guid _worker;

    private Guid _opener;
    private Guid _otherEngineerOnA;
    private Guid _engineerOnB;
    private Guid _finance;
    private Guid _hr;
    private Guid _technicalOffice;
    private Guid _marketing;

    public DayLabourRateManageTests(PostgresDatabase database) => _database = database;

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

    // ---- SM-30 test 5 ---------------------------------------------------------------------------

    [Fact]
    public async Task Only_the_engineer_who_opened_an_engagement_can_record_its_day_rate()
    {
        Guid engagementId = await OpenAsAsync(_projectA, _worker, _opener);

        (await SetDayRateAsync(_projectA, engagementId, 350m, _opener, Role.SiteEngineer))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        Guid secondEngagementId = await OpenAsAsync(_projectA, _worker, _opener);

        HttpResponseMessage byOther = await SetDayRateAsync(
            _projectA, secondEngagementId, 400m, _otherEngineerOnA, Role.SiteEngineer);
        byOther.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using KaffDbContext reader = _database.CreateBareContext();
        Engagement stored = await reader.Engagements.SingleAsync(e => e.Id == secondEngagementId, Ct);
        stored.DayRate.Should().BeNull("the refused call must not have written a rate");
    }

    // ---- SM-30 test 6 ---------------------------------------------------------------------------

    [Fact]
    public async Task Finance_reads_a_day_rate_and_cannot_record_one()
    {
        Guid engagementId = await OpenAsAsync(_projectA, _worker, _opener);
        (await SetDayRateAsync(_projectA, engagementId, 300m, _opener, Role.SiteEngineer))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await SetDayRateAsync(_projectA, engagementId, 500m, _finance, Role.Finance))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "Finance was not on site to agree anything — D-153 §1 point 6");

        (await ListEngagementsAsync(_projectA, _worker, _finance, Role.Finance))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- SM-30 test 7 ---------------------------------------------------------------------------

    [Fact]
    public async Task A_site_engineer_reads_the_day_rate_only_on_engagements_he_opened()
    {
        Guid openerEngagement = await OpenAsAsync(_projectA, _worker, _opener);
        (await SetDayRateAsync(_projectA, openerEngagement, 300m, _opener, Role.SiteEngineer))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        Guid otherEngagement = await OpenAsAsync(_projectA, _worker, _otherEngineerOnA);
        (await SetDayRateAsync(_projectA, otherEngagement, 400m, _otherEngineerOnA, Role.SiteEngineer))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage openerRead = await ListEngagementsAsync(_projectA, _worker, _opener, Role.SiteEngineer);
        openerRead.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await openerRead.Content.ReadAsStringAsync(Ct));
        JsonElement items = body.RootElement.GetProperty("items");

        items.GetArrayLength().Should().Be(1, "the opener sees only their own engagement");
        items[0].GetProperty("id").GetGuid().Should().Be(openerEngagement);
    }

    // ---- SM-30 test 8 ---------------------------------------------------------------------------

    [Fact]
    public async Task Every_role_without_DayLabourRateManage_is_refused_both_rate_routes()
    {
        Guid engagementId = await OpenAsAsync(_projectA, _worker, _opener);

        foreach ((Guid actor, Role role) in new[]
        {
            (_hr, Role.Hr),
            (_technicalOffice, Role.TechnicalOffice),
            (_marketing, Role.MarketingSales),
        })
        {
            (await SetDayRateAsync(_projectA, engagementId, 300m, actor, role))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} holds no DayLabourRateManage grant", role);

            (await ListEngagementsAsync(_projectA, _worker, actor, role))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    // ---- SM-30 test 9 ---------------------------------------------------------------------------

    [Fact]
    public void The_day_labour_pool_carries_no_money_member()
    {
        string[] forbidden = ["rate", "wage", "salary", "money", "amount", "dayrate"];

        System.Reflection.PropertyInfo[] properties =
            typeof(Api.Features.DayLabour.ListPool.PoolWorker).GetProperties();

        properties.Should().NotBeEmpty("a property-less record would pass this loop having checked nothing — V-38-K");

        foreach (System.Reflection.PropertyInfo property in properties)
        {
            string lowered = property.Name.ToLowerInvariant();
            forbidden.Should().NotContain(term => lowered.Contains(term, StringComparison.Ordinal),
                $"PoolWorker.{property.Name} must not be money-typed — D-153 §1 point 3: "
                + "DayLabourSiteManage stays untouched and money-free");
        }
    }

    // ---- SM-30 test 10, rule 6a --------------------------------------------------------------

    [Fact]
    public async Task A_day_rate_cannot_be_recorded_through_another_projects_route()
    {
        Guid engagementId = await OpenAsAsync(_projectA, _worker, _opener);

        HttpResponseMessage crossRoute = await SetDayRateAsync(_projectB, engagementId, 300m, _engineerOnB, Role.SiteEngineer);
        crossRoute.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using KaffDbContext reader = _database.CreateBareContext();
        Engagement stored = await reader.Engagements.SingleAsync(e => e.Id == engagementId, Ct);
        stored.DayRate.Should().BeNull();
    }

    // ---- SM-30 test 11, AC-210-C ------------------------------------------------------------

    [Fact]
    public async Task A_day_rate_of_487_6543_is_stored_and_read_back_exactly()
    {
        Guid engagementId = await OpenAsAsync(_projectA, _worker, _opener);

        (await SetDayRateAsync(_projectA, engagementId, 487.6543m, _opener, Role.SiteEngineer))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        Engagement stored = await reader.Engagements.SingleAsync(e => e.Id == engagementId, Ct);
        stored.DayRate!.Value.Amount.Should().Be(487.6543m);
    }

    // ---- SM-30 test 12 ------------------------------------------------------------------------

    [Fact]
    public async Task Recording_a_day_rate_is_audited_with_its_project()
    {
        Guid engagementId = await OpenAsAsync(_projectA, _worker, _opener);

        (await SetDayRateAsync(_projectA, engagementId, 300m, _opener, Role.SiteEngineer))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord modified = await reader.AuditRecords.SingleAsync(
            r => r.EntityId == engagementId && r.Action == AuditAction.Modified, Ct);

        modified.ActorUserId.Should().Be(_opener);
        modified.ProjectId.Should().Be(_projectA);
        modified.ChangedProperties.Should().Contain(nameof(Engagement.DayRate));
    }

    // ---- TC-2-119 / AC-210-D · the average is recomputed on every read -----------------------

    [Fact]
    public async Task The_average_day_rate_is_recomputed_from_the_engagements_on_every_read()
    {
        Guid first = await OpenAsAsync(_projectA, _worker, _opener);
        (await SetDayRateAsync(_projectA, first, 300m, _opener, Role.SiteEngineer)).StatusCode.Should().Be(HttpStatusCode.OK);

        Guid second = await OpenAsAsync(_projectA, _worker, _opener);
        (await SetDayRateAsync(_projectA, second, 400m, _opener, Role.SiteEngineer)).StatusCode.Should().Be(HttpStatusCode.OK);

        Guid third = await OpenAsAsync(_projectA, _worker, _opener);
        (await SetDayRateAsync(_projectA, third, 500m, _opener, Role.SiteEngineer)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage firstRead = await ListEngagementsAsync(_projectA, _worker, _opener, Role.SiteEngineer);
        using JsonDocument firstBody = JsonDocument.Parse(await firstRead.Content.ReadAsStringAsync(Ct));
        DecimalText.TryParse(firstBody.RootElement.GetProperty("averageDayRate").GetString(), out decimal firstAverage)
            .Should().BeTrue();
        firstAverage.Should().Be(400m, "the average of 300, 400 and 500");

        Guid fourth = await OpenAsAsync(_projectA, _worker, _opener);
        (await SetDayRateAsync(_projectA, fourth, 900m, _opener, Role.SiteEngineer)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage secondRead = await ListEngagementsAsync(_projectA, _worker, _opener, Role.SiteEngineer);
        using JsonDocument secondBody = JsonDocument.Parse(await secondRead.Content.ReadAsStringAsync(Ct));
        DecimalText.TryParse(secondBody.RootElement.GetProperty("averageDayRate").GetString(), out decimal secondAverage)
            .Should().BeTrue();
        secondAverage.Should().Be(525m,
            "adding a fourth engagement at 900 changes the average on the next read with nothing else written");
    }

    // ---- AC-210-G · an unengaged worker's read is null, not zero -----------------------------

    [Fact]
    public async Task A_worker_with_no_engagements_on_this_project_reads_as_unengaged_not_zero()
    {
        HttpResponseMessage response = await ListEngagementsAsync(_projectA, _worker, _opener, Role.SiteEngineer);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
        body.RootElement.GetProperty("averageDayRate").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("averageRating").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("frequency").GetInt32().Should().Be(0);
    }

    // ---- SM-30 test 13, Q77 (D-152 §3) -------------------------------------------------------

    [Fact]
    public async Task Any_assigned_engineer_closes_an_engagement_another_opened()
    {
        Guid engagementId = await OpenAsAsync(_projectA, _worker, _opener);

        HttpResponseMessage closedByOther = await CloseAsync(_projectA, engagementId, _otherEngineerOnA);

        closedByOther.StatusCode.Should().Be(
            HttpStatusCode.OK, "D-152 §3: any engineer assigned to the same project may close");
    }

    // ---- SM-30 test 14, D-152 §4 --------------------------------------------------------------

    [Fact]
    public async Task Only_the_engineer_who_opened_an_engagement_can_rate_it()
    {
        Guid engagementId = await OpenAsAsync(_projectA, _worker, _opener);

        (await RateAsync(_projectA, engagementId, 4, _otherEngineerOnA))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await RateAsync(_projectA, engagementId, 4, _opener))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- helpers -------------------------------------------------------------------------------

    private Task<HttpResponseMessage> OpenAsync(Guid projectId, Guid workerId, Guid actorId, Role actorRole = Role.SiteEngineer)
        => SendAsync(
            HttpMethod.Post,
            $"/api/projects/{projectId}/day-labour/engagements",
            actorId,
            actorRole,
            new { workerId });

    private async Task<Guid> OpenAsAsync(Guid projectId, Guid workerId, Guid actorId, Role actorRole = Role.SiteEngineer)
    {
        HttpResponseMessage response = await OpenAsync(projectId, workerId, actorId, actorRole);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> SetDayRateAsync(
        Guid projectId, Guid engagementId, decimal dayRate, Guid actorId, Role actorRole)
        => SendAsync(
            HttpMethod.Put,
            $"/api/projects/{projectId}/day-labour/engagements/{engagementId}/day-rate",
            actorId,
            actorRole,
            new { dayRate });

    private Task<HttpResponseMessage> ListEngagementsAsync(Guid projectId, Guid workerId, Guid actorId, Role actorRole)
        => SendAsync(
            HttpMethod.Get,
            $"/api/projects/{projectId}/day-labour/engagements?workerId={workerId}",
            actorId,
            actorRole,
            body: null);

    private Task<HttpResponseMessage> CloseAsync(Guid projectId, Guid engagementId, Guid actorId, Role actorRole = Role.SiteEngineer)
        => SendAsync(
            HttpMethod.Post,
            $"/api/projects/{projectId}/day-labour/engagements/{engagementId}/close",
            actorId,
            actorRole,
            body: null);

    private Task<HttpResponseMessage> RateAsync(
        Guid projectId, Guid engagementId, int rating, Guid actorId, Role actorRole = Role.SiteEngineer)
        => SendAsync(
            HttpMethod.Post,
            $"/api/projects/{projectId}/day-labour/engagements/{engagementId}/rate",
            actorId,
            actorRole,
            new { rating });

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
            Role.SiteEngineer => WellKnownDepartments.OperationsId,
            Role.MarketingSales => null,
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

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("DR-C1"), "عميل الأجر", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("DR-PA"), "مشروع أ", client.Id, ContractType.LumpSum, Now).Value;

        Project projectB = Project.Create(
            UniqueNames.Code("DR-PB"), "مشروع ب", client.Id, ContractType.LumpSum, Now).Value;

        Bab bab = Bab.Create(UniqueNames.Code("DR-BAB"), "باب", "Bab", Percentage.FromPercent(15m)).Value;

        Employee worker = Employee.Create(
            UniqueNames.Code("DR-W"), "عامل", UniqueNames.Phone(), EmployeeKind.DayLabour, Now, bab.Id).Value;

        User owner = MakeUser("dr-owner", Role.Owner);
        User opener = MakeUser("dr-engineer-opener", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User otherEngineerOnA = MakeUser("dr-engineer-other", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User engineerOnB = MakeUser("dr-engineer-b", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User finance = MakeUser("dr-finance", Role.Finance, WellKnownDepartments.FinanceId);
        User hr = MakeUser("dr-hr", Role.Hr, WellKnownDepartments.HrId);
        User technicalOffice = MakeUser("dr-tech-office", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User marketing = MakeUser("dr-marketing", Role.MarketingSales, null);

        context.Clients.Add(client);
        context.Projects.AddRange(projectA, projectB);
        context.Babs.Add(bab);
        context.Employees.Add(worker);
        context.Users.AddRange(owner, opener, otherEngineerOnA, engineerOnB, finance, hr, technicalOffice, marketing);

        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.Add(
            ProjectAssignment.Create(projectA.Id, opener, AssignmentLevel.Junior, owner.Id, Now).Value);
        context.ProjectAssignments.Add(
            ProjectAssignment.Create(projectA.Id, otherEngineerOnA, AssignmentLevel.Junior, owner.Id, Now).Value);
        context.ProjectAssignments.Add(
            ProjectAssignment.Create(projectB.Id, engineerOnB, AssignmentLevel.Junior, owner.Id, Now).Value);
        context.ProjectAssignments.Add(
            ProjectAssignment.Create(projectA.Id, finance, AssignmentLevel.Standard, owner.Id, Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _projectB = projectB.Id;
        _worker = worker.Id;
        _opener = opener.Id;
        _otherEngineerOnA = otherEngineerOnA.Id;
        _engineerOnB = engineerOnB.Id;
        _finance = finance.Id;
        _hr = hr.Id;
        _technicalOffice = technicalOffice.Id;
        _marketing = marketing.Id;
    }

    private static User MakeUser(
        string userName, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static readonly DateTimeOffset Now = new(2026, 9, 12, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
