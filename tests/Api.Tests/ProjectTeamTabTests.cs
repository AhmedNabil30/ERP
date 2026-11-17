using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// D-227 / D-228 item 1 / D-229: the backend of the project team tab. Only the Owner (D-233 item 3: HR lost the
/// <c>ProjectAssignmentManage</c> grant) adds and removes people; Finance cannot be removed; any number of supervising
/// engineers; the tab reads current members with how each came to be on the project; a candidates read feeds the add sheet.
/// </summary>
/// <remarks>
/// The project is created through the real <c>POST /api/projects</c> as the Technical Office, so the Finance rows and the
/// creator row are the ones the create handler writes, not rows the test builds to match its own assertions.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ProjectTeamTabTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _project;
    private Guid _owner;
    private Guid _hr;
    private Guid _finance;
    private Guid _technicalOffice;
    private Guid _engineerOne;
    private Guid _engineerTwo;
    private Guid _engineerThree;
    private Guid _headOfDesign;
    private Guid _portalClient;
    private Guid _leaver;

    public ProjectTeamTabTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(UniqueNames.Code("TT-C"), "عميل الفريق", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;
        User owner = MakeUser("tt-owner", Role.Owner);
        User hr = MakeUser("tt-hr", Role.Hr, WellKnownDepartments.HrId);
        User finance = MakeUser("tt-finance", Role.Finance, WellKnownDepartments.FinanceId);
        User technicalOffice = MakeUser("tt-tech", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User one = MakeUser("tt-eng1", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User two = MakeUser("tt-eng2", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User three = MakeUser("tt-eng3", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User design = MakeUser("tt-design", Role.HeadOfDesign);
        User portal = MakeUser("tt-portal", Role.Client, clientId: client.Id);
        User leaver = MakeUser("tt-leaver", Role.Finance, WellKnownDepartments.FinanceId);
        leaver.Deactivate(Now).IsSuccess.Should().BeTrue();

        context.Clients.Add(client);
        context.Users.AddRange(owner, hr, finance, technicalOffice, one, two, three, design, portal, leaver);
        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _hr = hr.Id;
        _finance = finance.Id;
        _technicalOffice = technicalOffice.Id;
        _engineerOne = one.Id;
        _engineerTwo = two.Id;
        _engineerThree = three.Id;
        _headOfDesign = design.Id;
        _portalClient = portal.Id;
        _leaver = leaver.Id;

        _factory = new KaffApiFactory(_database.ConnectionString);
        _client = _factory.CreateClient();

        HttpResponseMessage created = await SendAsync(
            HttpMethod.Post, "/api/projects", _technicalOffice,
            new { name = UniqueNames.Code("مشروع الفريق"), clientId = client.Id, contractType = ContractType.LumpSum.ToString() });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        _project = (await ReadAsync(created)).GetProperty("id").GetGuid();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- D-228 item 1: Finance cannot be removed; the creator can; a removed person comes back -------------------

    [Fact]
    public async Task The_owner_cannot_remove_finance_and_the_row_stays_active()
    {
        Guid row = await ActiveRowAsync(_finance);

        HttpResponseMessage response = await SendAsync(HttpMethod.Post, RevokeRoute(row), _owner);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.finance_cannot_be_removed");
        (await ActiveRowAsync(_finance)).Should().Be(row, "the refusal changed nothing");
    }

    [Fact]
    public async Task The_owner_removes_the_creator_and_adds_him_back_with_the_old_row_kept_on_file()
    {
        Guid row = await ActiveRowAsync(_technicalOffice);

        (await SendAsync(HttpMethod.Post, RevokeRoute(row), _owner)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Post, AssignRoute, _owner, new { userId = _technicalOffice, level = "Standard" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext reader = _database.CreateBareContext();
        List<ProjectAssignment> rows = await reader.ProjectAssignments
            .Where(a => a.ProjectId == _project && a.UserId == _technicalOffice).ToListAsync(Ct);
        rows.Should().HaveCount(2);
        rows.Count(a => a.RevokedAt == null).Should().Be(1);
        rows.Single(a => a.Id == row).RevokedByUserId.Should().Be(_owner, "the removed row keeps who removed it");
    }

    // ---- D-228 item 1: no cap on supervising engineers -----------------------------------------------------------

    [Fact]
    public async Task A_project_takes_two_supervising_engineers()
    {
        (await SendAsync(HttpMethod.Post, AssignRoute, _owner, new { userId = _engineerOne, level = "Supervisor" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, AssignRoute, _owner, new { userId = _engineerTwo, level = "Supervisor" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext reader = _database.CreateBareContext();
        (await reader.ProjectAssignments.CountAsync(
            a => a.ProjectId == _project && a.Level == AssignmentLevel.Supervisor && a.RevokedAt == null, Ct)).Should().Be(2);
    }

    // ---- the team read --------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_team_read_says_how_each_member_came_and_what_the_caller_may_do()
    {
        (await SendAsync(HttpMethod.Post, AssignRoute, _owner, new { userId = _engineerOne, level = "Supervisor" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement body = await ReadAsync(await SendAsync(HttpMethod.Get, TeamRoute, _owner));

        body.GetProperty("canManage").GetBoolean().Should().BeTrue();
        JsonElement[] members = [.. body.GetProperty("members").EnumerateArray()];

        JsonElement finance = members.Single(m => m.GetProperty("userId").GetGuid() == _finance);
        finance.GetProperty("origin").GetString().Should().Be("Automatic");
        finance.GetProperty("canRemove").GetBoolean().Should().BeFalse("Finance cannot be removed");
        finance.GetProperty("assignmentId").GetGuid().Should().Be(await ActiveRowAsync(_finance));

        JsonElement creator = members.Single(m => m.GetProperty("userId").GetGuid() == _technicalOffice);
        creator.GetProperty("origin").GetString().Should().Be("Creator");
        creator.GetProperty("canRemove").GetBoolean().Should().BeTrue("the Owner may remove the creator");

        JsonElement added = members.Single(m => m.GetProperty("userId").GetGuid() == _engineerOne);
        added.GetProperty("origin").GetString().Should().Be("Added");
        added.GetProperty("level").GetString().Should().Be("Supervisor");
        added.GetProperty("role").GetString().Should().Be("SiteEngineer");
        added.GetProperty("fullName").GetString().Should().Be("tt-eng1");
        added.GetProperty("canRemove").GetBoolean().Should().BeTrue();

        members.Should().NotContain(m => m.GetProperty("userId").GetGuid() == _owner, "the Owner has global reach and no row");
        added.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["assignmentId", "userId", "fullName", "role", "level", "origin", "canRemove", "assignedAt"],
            "no phone, code or money travels with a member");
    }

    [Fact]
    public async Task A_removed_member_leaves_the_read_and_a_re_added_one_reads_as_added()
    {
        (await SendAsync(HttpMethod.Post, RevokeRoute(await ActiveRowAsync(_technicalOffice)), _owner))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Post, AssignRoute, _owner, new { userId = _technicalOffice, level = "Standard" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        JsonElement body = await ReadAsync(await SendAsync(HttpMethod.Get, TeamRoute, _owner));
        JsonElement[] again = [.. body.GetProperty("members").EnumerateArray().Where(m => m.GetProperty("userId").GetGuid() == _technicalOffice)];

        again.Should().ContainSingle("the tab lists current members only");
        again[0].GetProperty("origin").GetString().Should().Be("Added", "the Owner added him back; he is no longer the creator's row");
    }

    [Fact]
    public async Task A_member_who_cannot_manage_reads_the_team_with_nothing_removable()
    {
        (await SendAsync(HttpMethod.Post, AssignRoute, _owner, new { userId = _engineerOne, level = "Junior" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        foreach (Guid caller in new[] { _technicalOffice, _finance, _engineerOne })
        {
            HttpResponseMessage response = await SendAsync(HttpMethod.Get, TeamRoute, caller);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            JsonElement body = await ReadAsync(response);
            body.GetProperty("canManage").GetBoolean().Should().BeFalse();
            body.GetProperty("members").EnumerateArray()
                .Should().OnlyContain(m => !m.GetProperty("canRemove").GetBoolean());
        }
    }

    [Fact]
    public async Task A_correct_role_with_no_assignment_is_refused_the_team_read()
    {
        (await SendAsync(HttpMethod.Get, TeamRoute, _engineerThree)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- the candidates read --------------------------------------------------------------------------------------

    [Fact]
    public async Task Candidates_are_active_assignable_users_not_yet_on_the_project_with_a_level_flag_for_engineers()
    {
        JsonElement[] candidates = [.. (await ReadAsync(await SendAsync(HttpMethod.Get, CandidatesRoute, _owner))).EnumerateArray()];
        Guid[] ids = [.. candidates.Select(c => c.GetProperty("userId").GetGuid())];

        ids.Should().Contain([_engineerOne, _engineerTwo, _engineerThree, _headOfDesign, _hr]);
        ids.Should().NotContain(_owner, "the Owner is never a member");
        ids.Should().NotContain(_portalClient, "a portal client is not assignable");
        ids.Should().NotContain(_leaver, "an inactive user is not assignable");
        ids.Should().NotContain([_finance, _technicalOffice], "already on the project");

        JsonElement engineer = candidates.Single(c => c.GetProperty("userId").GetGuid() == _engineerOne);
        engineer.GetProperty("levelRequired").GetBoolean().Should().BeTrue();
        engineer.GetProperty("role").GetString().Should().Be("SiteEngineer");
        engineer.GetProperty("fullName").GetString().Should().Be("tt-eng1");
        candidates.Single(c => c.GetProperty("userId").GetGuid() == _headOfDesign)
            .GetProperty("levelRequired").GetBoolean().Should().BeFalse();
        engineer.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            ["userId", "fullName", "role", "levelRequired"], "no phone or code");
    }

    [Fact]
    public async Task Someone_assigned_leaves_the_candidates_and_someone_removed_returns()
    {
        (await SendAsync(HttpMethod.Post, AssignRoute, _owner, new { userId = _engineerOne, level = "Junior" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await CandidateIdsAsync()).Should().NotContain(_engineerOne);

        (await SendAsync(HttpMethod.Post, RevokeRoute(await ActiveRowAsync(_engineerOne)), _owner))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CandidateIdsAsync()).Should().Contain(_engineerOne);
    }

    // ---- permissions, endpoints hit directly -----------------------------------------------------------------------

    [Fact]
    public async Task Nobody_without_the_assignment_permission_reaches_assign_revoke_or_candidates()
    {
        (await SendAsync(HttpMethod.Post, AssignRoute, _owner, new { userId = _engineerOne, level = "Supervisor" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await SendAsync(HttpMethod.Post, AssignRoute, _owner, new { userId = _engineerTwo, level = "Junior" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        Guid removable = await ActiveRowAsync(_engineerTwo);

        foreach (Guid caller in new[] { _hr, _technicalOffice, _finance, _engineerOne, _engineerTwo, _engineerThree })
        {
            (await SendAsync(HttpMethod.Get, CandidatesRoute, caller)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Post, AssignRoute, caller, new { userId = _engineerThree, level = "Junior" }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await SendAsync(HttpMethod.Post, RevokeRoute(removable), caller)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await ActiveRowAsync(_engineerTwo)).Should().Be(removable, "no refused call removed anybody");
        (await CandidateIdsAsync()).Should().Contain(_engineerThree, "no refused call added anybody");
    }

    // ---- the add refusals carry translated keys ---------------------------------------------------------------------

    [Fact]
    public async Task A_site_engineer_sent_without_a_level_is_refused_with_the_translated_key()
    {
        HttpResponseMessage response = await SendAsync(HttpMethod.Post, AssignRoute, _owner, new { userId = _engineerOne });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.assignment_level_not_applicable");
    }

    [Fact]
    public async Task Removing_somebody_audits_who_when_and_the_before_and_after()
    {
        Guid row = await ActiveRowAsync(_technicalOffice);
        (await SendAsync(HttpMethod.Post, RevokeRoute(row), _owner)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();
        AuditRecord record = await reader.AuditRecords.SingleAsync(
            r => r.EntityType == nameof(ProjectAssignment) && r.EntityId == row && r.Action == AuditAction.Modified, Ct);

        record.ActorUserId.Should().Be(_owner);
        record.ProjectId.Should().Be(_project);
        record.ChangedProperties.Should().Contain(["RevokedAt", "RevokedByUserId"]);
        record.BeforeJson.Should().NotContain(_owner.ToString(), "before the removal nobody had removed it");
        record.AfterJson.Should().Contain(_owner.ToString());
    }

    // ---- helpers ----------------------------------------------------------------------------------------------------

    private string AssignRoute => $"/api/projects/{_project}/assignments";

    private string TeamRoute => $"/api/projects/{_project}/team";

    private string CandidatesRoute => $"/api/projects/{_project}/assignments/candidates";

    private string RevokeRoute(Guid assignmentId) => $"/api/projects/{_project}/assignments/{assignmentId}/revoke";

    private async Task<Guid> ActiveRowAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();
        return await reader.ProjectAssignments
            .Where(a => a.ProjectId == _project && a.UserId == userId && a.RevokedAt == null)
            .Select(a => a.Id).SingleAsync(Ct);
    }

    private async Task<Guid[]> CandidateIdsAsync()
    {
        JsonElement list = await ReadAsync(await SendAsync(HttpMethod.Get, CandidatesRoute, _owner));
        return [.. list.EnumerateArray().Select(c => c.GetProperty("userId").GetGuid())];
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
        => (await ReadAsync(response)).TryGetProperty("messageKey", out JsonElement key) ? key.GetString() : null;

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, Guid actorId, object? body = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        await using KaffDbContext reader = _database.CreateBareContext();
        var found = await reader.Users.Where(user => user.Id == actorId)
            .Select(user => new { user.SecurityStamp, user.Role }).FirstAsync(Ct);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, found.SecurityStamp);
        request.Headers.Add(TestAuthHandler.RoleHeader, found.Role.ToString());

        return await _client.SendAsync(request, Ct);
    }

    private static User MakeUser(
        string userName,
        Role role,
        Guid? department = null,
        OperationsSubDepartment? subDepartment = null,
        Guid? clientId = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment, clientId).Value;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
