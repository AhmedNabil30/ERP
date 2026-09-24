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
/// KAFF-400 — <c>GET /api/projects</c>. AC-400-A, B.
/// </summary>
/// <remarks>
/// <para>
/// <b>HR is refused here, and the story's own AC-400-B text is wrong about it.</b> The brief asks for
/// "HR gets a list with no financial field present in the payload at all". <c>Permission.ProjectRead</c>
/// deliberately excludes HR (D-044 ruling 2, pinned by
/// <c>CatalogueCompletenessTests.Hr_cannot_reach_a_project_through_ProjectRead</c>), and HR's own
/// project list already exists at <c>GET /api/auth/me</c>'s <c>TeamProjectEntry</c> (KAFF-105b, D-103)
/// — a separate type built specifically so a financial field could never reach HR through a route that
/// forgot to redact one. Building this endpoint to admit HR would either duplicate that mechanism or
/// widen <c>Permission.ProjectRead</c>/<c>ProjectList</c> to a role every other financial permission in
/// the catalogue names by exclusion. <see cref="Hr_is_refused_and_must_use_its_own_project_dashboard"/>
/// pins the refusal instead; see the story's build notes for the question this raises for Karim.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ListProjectsTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _projectB;

    private Guid _owner;
    private Guid _hr;
    private Guid _assignedEngineer;
    private Guid _unassignedEngineer;
    private Guid _portalClient;

    public ListProjectsTests(PostgresDatabase database) => _database = database;

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

    [Fact]
    public async Task The_owner_sees_every_project_with_no_assignment_row()
    {
        HttpResponseMessage response = await GetAsync(_owner);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        body.RootElement.GetProperty("projects").GetArrayLength().Should().Be(
            body.RootElement.GetProperty("total").GetInt32(), "the story's own rule — a carried total");
    }

    [Fact]
    public async Task An_assigned_engineer_sees_only_the_project_it_is_assigned_to()
    {
        HttpResponseMessage response = await GetAsync(_assignedEngineer);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        List<Guid> ids = [.. body.RootElement.GetProperty("projects")
            .EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

        ids.Should().BeEquivalentTo([_projectA]);
    }

    [Fact]
    public async Task An_unassigned_engineer_sees_an_empty_list_not_a_refusal()
    {
        // ProjectList is CompanyWide — the role may call the endpoint. spec.md §9's assignment half
        // is enforced by the handler's own filter, not the gate, so this is 200 with zero rows.
        HttpResponseMessage response = await GetAsync(_unassignedEngineer);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("total").GetInt32().Should().Be(0);
    }

    /// <summary>See this class's own remarks — AC-400-B's HR expectation collides with D-044 ruling 2.</summary>
    [Fact]
    public async Task Hr_is_refused_and_must_use_its_own_project_dashboard()
    {
        HttpResponseMessage response = await GetAsync(_hr);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageKeyAsync(response)).Should().Be("errors.auth.forbidden");
    }

    [Fact]
    public async Task A_portal_client_is_refused()
    {
        (await GetAsync(_portalClient)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private async Task<HttpResponseMessage> GetAsync(Guid actorId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/projects", UriKind.Relative));

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
            UniqueNames.Code("LSP-C"), "عميل القائمة", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("LSP-PA"), "مشروع أ", client.Id, ContractType.LumpSum, Now).Value;
        Project projectB = Project.Create(
            UniqueNames.Code("LSP-PB"), "مشروع ب", client.Id, ContractType.LumpSum, Now).Value;

        User owner = MakeUser("lsp-owner", Role.Owner);
        User hr = MakeUser("lsp-hr", Role.Hr, WellKnownDepartments.HrId);
        User assignedEngineer = MakeUser(
            "lsp-assigned", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User unassignedEngineer = MakeUser(
            "lsp-unassigned", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User portal = MakeUser("lsp-portal", Role.Client, clientId: client.Id);

        context.Clients.Add(client);
        context.Projects.AddRange(projectA, projectB);
        context.Users.AddRange(owner, hr, assignedEngineer, unassignedEngineer, portal);

        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.Add(
            ProjectAssignment.Create(projectA.Id, assignedEngineer, AssignmentLevel.Junior, owner.Id, Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _projectB = projectB.Id;
        _owner = owner.Id;
        _hr = hr.Id;
        _assignedEngineer = assignedEngineer.Id;
        _unassignedEngineer = unassignedEngineer.Id;
        _portalClient = portal.Id;
    }

    private static User MakeUser(
        string userName,
        Role role,
        Guid? department = null,
        OperationsSubDepartment? subDepartment = null,
        Guid? clientId = null)
        => User.Create(
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, subDepartment, clientId).Value;

    private static DateTimeOffset Now => new(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
