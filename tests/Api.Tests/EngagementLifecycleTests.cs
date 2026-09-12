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

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-210 — engagement open, close and rating under
/// <c>/api/projects/{projectId}/day-labour/engagements</c>. decisions.md D-139 §3, D-140, D-148.
/// </summary>
/// <remarks>
/// TC-2-116 … TC-2-126 (qa/slice-2/test-cases.md), plus D-140's SM-30 test 14
/// (<see cref="An_engagement_cannot_be_closed_through_another_projects_route"/>).
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class EngagementLifecycleTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _projectB;
    private Guid _worker;
    private Guid _salariedWorker;

    private Guid _assignedToA;
    private Guid _assignedToB;
    private Guid _hr;
    private Guid _finance;

    public EngagementLifecycleTests(PostgresDatabase database) => _database = database;

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

    // ---- TC-2-116 / AC-210-A · an engagement is recorded against a worker --------------------

    [Fact]
    public async Task An_assigned_site_engineer_opens_an_engagement()
    {
        HttpResponseMessage response = await OpenAsync(_projectA, _assignedToA, _worker);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Guid id = body.RootElement.GetProperty("id").GetGuid();

        body.RootElement.GetProperty("workerId").GetGuid().Should().Be(_worker);
        body.RootElement.GetProperty("projectId").GetGuid().Should().Be(_projectA);

        await using KaffDbContext reader = _database.CreateBareContext();
        Engagement stored = await reader.Engagements.SingleAsync(e => e.Id == id, Ct);

        stored.WorkerId.Should().Be(_worker);
        stored.ProjectId.Should().Be(_projectA);
        stored.IsOpen.Should().BeTrue();
    }

    // ---- rule 9 / Q76 · no money member on the request or the response -----------------------

    [Fact]
    public void No_money_member_appears_on_the_open_request_or_response()
    {
        string[] forbidden = ["rate", "wage", "salary", "money", "amount", "dayrate"];

        foreach (Type type in new[]
        {
            typeof(Api.Features.DayLabour.OpenEngagement.Request),
            typeof(Api.Features.DayLabour.OpenEngagement.Response),
        })
        {
            foreach (System.Reflection.PropertyInfo property in type.GetProperties())
            {
                string lowered = property.Name.ToLowerInvariant();
                forbidden.Should().NotContain(term => lowered.Contains(term, StringComparison.Ordinal),
                    $"{type.Name}.{property.Name} must not be money-typed — Q76, decisions.md D-140");
            }
        }
    }

    // ---- an unknown or salaried worker id is refused ------------------------------------------

    [Fact]
    public async Task Opening_an_engagement_for_an_unknown_worker_is_refused()
    {
        HttpResponseMessage response = await OpenAsync(_projectA, _assignedToA, Guid.CreateVersion7());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Opening_an_engagement_for_a_salaried_record_is_refused()
    {
        HttpResponseMessage response = await OpenAsync(_projectA, _assignedToA, _salariedWorker);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the salaried register is isolated from this pool — decisions.md D-139 §2");
    }

    // ---- TC-2-123 / AC-210-H · a role without the permission is refused -----------------------

    [Fact]
    public async Task A_role_without_DayLabourSiteManage_cannot_open_close_or_rate()
    {
        Guid engagementId = await OpenEngagementDirectAsync(_projectA, _worker);

        foreach ((Guid actor, Role role) in new[]
        {
            (_finance, Role.Finance),
            (_hr, Role.Hr),
        })
        {
            (await OpenAsync(_projectA, actor, _worker, role))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, "{0} holds no DayLabourSiteManage grant", role);

            (await CloseAsync(_projectA, engagementId, actor, role))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);

            (await RateAsync(_projectA, engagementId, 4, actor, role))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task An_unassigned_site_engineer_cannot_open_an_engagement()
    {
        // _assignedToB is a real Site Engineer, just not assigned to project A.
        (await OpenAsync(_projectA, _assignedToB, _worker))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- TC-2-126 / AC-210-K, D-140 SM-30 test 14 — the load-bearing case ---------------------

    [Fact]
    public async Task An_engagement_cannot_be_closed_through_another_projects_route()
    {
        Guid engagementId = await OpenEngagementDirectAsync(_projectA, _worker);

        // The engineer assigned to A closes A's own engagement through A's route — succeeds.
        HttpResponseMessage ownRoute = await CloseAsync(_projectA, engagementId, _assignedToA);
        ownRoute.StatusCode.Should().Be(HttpStatusCode.OK);

        Guid secondEngagementId = await OpenEngagementDirectAsync(_projectA, _worker);

        // The engineer assigned to B pairs B's route with A's engagement id — refused, even though
        // he genuinely holds DayLabourSiteManage on B.
        HttpResponseMessage crossRoute = await CloseAsync(_projectB, secondEngagementId, _assignedToB);
        crossRoute.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using KaffDbContext reader = _database.CreateBareContext();
        Engagement stillOpen = await reader.Engagements.SingleAsync(e => e.Id == secondEngagementId, Ct);
        stillOpen.IsOpen.Should().BeTrue("the mismatched close must not have taken effect");
    }

    // ---- TC-2-124 / AC-210-I · the engagement and the rating are audited ----------------------

    [Fact]
    public async Task Opening_closing_and_rating_are_each_audited_with_the_project()
    {
        HttpResponseMessage opened = await OpenAsync(_projectA, _assignedToA, _worker);
        opened.StatusCode.Should().Be(HttpStatusCode.Created);

        using JsonDocument openedBody = JsonDocument.Parse(await opened.Content.ReadAsStringAsync(Ct));
        Guid engagementId = openedBody.RootElement.GetProperty("id").GetGuid();

        (await RateAsync(_projectA, engagementId, 4, _assignedToA)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await CloseAsync(_projectA, engagementId, _assignedToA)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord created = await reader.AuditRecords.SingleAsync(
            r => r.EntityId == engagementId && r.Action == AuditAction.Created, Ct);
        created.ActorUserId.Should().Be(_assignedToA);
        created.ProjectId.Should().Be(_projectA);
        created.GrantPath.Should().Be(ProjectAccessPath.Assignment);

        List<AuditRecord> modifications = await reader.AuditRecords
            .Where(r => r.EntityId == engagementId && r.Action == AuditAction.Modified)
            .ToListAsync(Ct);

        // One for the rating, one for the close.
        modifications.Should().HaveCount(2);
        modifications.Should().OnlyContain(r => r.ProjectId == _projectA && r.GrantPath == ProjectAccessPath.Assignment);
    }

    // ---- AC-210-F · a rating is out of 5, refused outside 1-5 ---------------------------------

    [Fact]
    public async Task A_rating_outside_1_to_5_is_refused()
    {
        Guid engagementId = await OpenEngagementDirectAsync(_projectA, _worker);

        HttpResponseMessage response = await RateAsync(_projectA, engagementId, 6, _assignedToA);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.master.engagement_rating_out_of_range");
    }

    [Fact]
    public async Task A_rating_of_4_is_accepted_and_stored()
    {
        Guid engagementId = await OpenEngagementDirectAsync(_projectA, _worker);

        HttpResponseMessage response = await RateAsync(_projectA, engagementId, 4, _assignedToA);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using KaffDbContext reader = _database.CreateBareContext();
        Engagement stored = await reader.Engagements.SingleAsync(e => e.Id == engagementId, Ct);
        stored.Rating.Should().Be(4);
    }

    // ---- helpers -------------------------------------------------------------------------------

    private Task<HttpResponseMessage> OpenAsync(Guid projectId, Guid actorId, Guid workerId, Role actorRole = Role.SiteEngineer)
        => SendAsync(
            HttpMethod.Post,
            $"/api/projects/{projectId}/day-labour/engagements",
            actorId,
            actorRole,
            new { workerId });

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

    /// <summary>Opens an engagement directly against the database, bypassing HTTP, for tests whose
    /// subject is the close/rate route rather than the open route.</summary>
    /// <remarks>
    /// Defaults <paramref name="openedByUserId"/> to <c>_assignedToA</c> — decisions.md D-152 §4,
    /// D-153 §1 point 7: <c>RateEngagement</c> now refuses anyone but the opener, so a test that rates
    /// through this helper needs the same actor recorded as the opener, the way the HTTP-opened path
    /// always would.
    /// </remarks>
    private async Task<Guid> OpenEngagementDirectAsync(Guid projectId, Guid workerId, Guid? openedByUserId = null)
    {
        await using KaffDbContext context = _database.CreateContext();

        Engagement engagement = Engagement.Open(
            workerId, projectId, Today, Now, openedByUserId: openedByUserId ?? _assignedToA).Value;
        context.Engagements.Add(engagement);
        await context.SaveChangesAsync(Ct);

        return engagement.Id;
    }

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

        Department? department = actorRole switch
        {
            Role.Hr => Department.Hr,
            Role.Finance => Department.Finance,
            Role.SiteEngineer => Department.Operations,
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

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("EN-C1"), "عميل التعاقد", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("EN-PA"), "مشروع أ", client.Id, ContractType.LumpSum, Now).Value;

        Project projectB = Project.Create(
            UniqueNames.Code("EN-PB"), "مشروع ب", client.Id, ContractType.LumpSum, Now).Value;

        Bab bab = Bab.Create(UniqueNames.Code("EN-BAB"), "باب", "Bab", Percentage.FromPercent(15m)).Value;

        Employee worker = Employee.Create(
            UniqueNames.Code("EN-W"), "عامل", UniqueNames.Phone(), EmployeeKind.DayLabour, Now, bab.Id).Value;

        Employee salariedWorker = Employee.Create(
            UniqueNames.Code("EN-S"), "موظف", UniqueNames.Phone(), EmployeeKind.Salaried, Now).Value;

        User owner = MakeUser("en-owner", Role.Owner);
        User assignedToA = MakeUser("en-engineer-a", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User assignedToB = MakeUser("en-engineer-b", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User hr = MakeUser("en-hr", Role.Hr, Department.Hr);
        User finance = MakeUser("en-finance", Role.Finance, Department.Finance);

        context.Clients.Add(client);
        context.Projects.AddRange(projectA, projectB);
        context.Babs.Add(bab);
        context.Employees.AddRange(worker, salariedWorker);
        context.Users.AddRange(owner, assignedToA, assignedToB, hr, finance);

        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.Add(
            ProjectAssignment.Create(projectA.Id, assignedToA, AssignmentLevel.Junior, owner.Id, Now).Value);
        context.ProjectAssignments.Add(
            ProjectAssignment.Create(projectB.Id, assignedToB, AssignmentLevel.Junior, owner.Id, Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _projectB = projectB.Id;
        _worker = worker.Id;
        _salariedWorker = salariedWorker.Id;
        _assignedToA = assignedToA.Id;
        _assignedToB = assignedToB.Id;
        _hr = hr.Id;
        _finance = finance.Id;
    }

    private static User MakeUser(
        string userName, Role role, Department? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
