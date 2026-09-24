using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Authorization;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-400 — <c>GET /api/projects/{projectId}</c>. AC-400-A, B.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class GetProjectTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectId;
    private Guid _owner;
    private Guid _assignedEngineer;
    private Guid _unassignedEngineer;
    private Guid _hr;

    public GetProjectTests(PostgresDatabase database) => _database = database;

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

    /// <summary>
    /// AC-400-B, stated verbatim in the story: "a Site Engineer with no assignment gets a refusal on
    /// GET /api/projects/{id}".
    /// </summary>
    [Fact]
    public async Task A_site_engineer_with_no_assignment_is_refused()
    {
        HttpResponseMessage response = await GetAsync(_unassignedEngineer);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageKeyAsync(response)).Should().Be("errors.auth.forbidden");
    }

    [Fact]
    public async Task An_assigned_engineer_reads_the_project()
    {
        HttpResponseMessage response = await GetAsync(_assignedEngineer);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_owner_reads_any_project_without_an_assignment_row()
    {
        (await GetAsync(_owner)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// HR holds no <c>Permission.ProjectRead</c> grant (D-044 ruling 2) — refused here, not redacted.
    /// Its own view is <c>GET /api/auth/me</c>'s <c>TeamProjectEntry</c> (KAFF-105b, D-103).
    /// </summary>
    [Fact]
    public async Task Hr_is_refused_even_though_it_has_global_reach()
    {
        HttpResponseMessage response = await GetAsync(_hr);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "HR's global reach admits it to the project, but ProjectRead is not one of HR's grants");
    }

    [Fact]
    public async Task The_header_carries_name_client_type_value_status_and_team_size()
    {
        HttpResponseMessage response = await GetAsync(_owner);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("name").GetString().Should().Be("مشروع القراءة");
        body.RootElement.GetProperty("clientName").GetString().Should().NotBeNullOrEmpty();
        body.RootElement.GetProperty("contractType").GetString().Should().Be(nameof(ContractType.LumpSum));
        body.RootElement.GetProperty("status").GetString().Should().Be(nameof(ProjectStatus.Setup));
        body.RootElement.GetProperty("teamSize").GetInt32().Should().Be(1, "one active assignment was seeded");
        body.RootElement.GetProperty("contractValue").ValueKind.Should().Be(JsonValueKind.String, "D-135");
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private async Task<HttpResponseMessage> GetAsync(Guid actorId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri($"/api/projects/{_projectId}", UriKind.Relative));

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
            UniqueNames.Code("GTP-C"), "عميل القراءة", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project project = Project.Create(
            UniqueNames.Code("GTP-P"), "مشروع القراءة", client.Id, ContractType.LumpSum, Now).Value;

        User owner = MakeUser("gtp-owner", Role.Owner);
        User assignedEngineer = MakeUser(
            "gtp-assigned", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User unassignedEngineer = MakeUser(
            "gtp-unassigned", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User hr = MakeUser("gtp-hr", Role.Hr, WellKnownDepartments.HrId);

        context.Clients.Add(client);
        context.Projects.Add(project);
        context.Users.AddRange(owner, assignedEngineer, unassignedEngineer, hr);

        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.Add(
            ProjectAssignment.Create(project.Id, assignedEngineer, AssignmentLevel.Junior, owner.Id, Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectId = project.Id;
        _owner = owner.Id;
        _assignedEngineer = assignedEngineer.Id;
        _unassignedEngineer = unassignedEngineer.Id;
        _hr = hr.Id;
    }

    private static User MakeUser(
        string userName, Role role, Guid? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
