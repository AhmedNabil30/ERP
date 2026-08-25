using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-114 — <c>POST /api/projects/{projectId}/assignments/{assignmentId}/revoke</c>, the close half
/// of "permission = role × assignment".
/// </summary>
/// <remarks>
/// <para>
/// spec.md §9: "A user MUST be assigned to a project to open it or act on it." This suite is the
/// other direction — the row that grants that must be closeable, and closing it must survive on file
/// rather than disappear, so the trail can still answer who could act on a given day.
/// </para>
/// <para>
/// Two rules here are built under the readiness waiver of decisions.md D-062 §1 and neither question
/// is answered by this suite: <b>Q49</b> (may the last engineer be revoked off a project) and
/// <b>Q51</b> (revoking an already-revoked assignment is refused). Rule 7 (Q49) is exercised
/// nowhere in this file on purpose — asserting "revoking the last engineer succeeds" would not be
/// wrong, but a minimum-team-size rule is not in spec.md and this suite must not read one into
/// existence by testing around one.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class RevokeProjectAssignmentTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;

    private Guid _owner;
    private Guid _hr;
    private Guid _finance;
    private Guid _technicalOffice;
    private Guid _siteEngineer;
    private Guid _siteEngineerAssignmentId;
    private Guid _supervisorOnProjectA;

    public RevokeProjectAssignmentTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-114-A · access ends on the next request -----------------------------------------------

    /// <summary>
    /// AC-114-A. The engineer's token never changes — only the row backing it does — which is the
    /// whole point of decisions.md D-053/D-048: enforcement is a per-request database read, not
    /// anything baked into the token.
    /// </summary>
    /// <remarks>
    /// <b>The messageKey asserted here is <c>errors.auth.forbidden</c>, not the story's
    /// <c>errors.auth.not_assigned_to_project</c> — verified against the shipped pipeline, not
    /// assumed from the story text (SM-31).</b> <c>Program.cs</c>'s <c>CustomizeProblemDetails</c>
    /// stamps every 403 with the single blanket <c>AuthorizationErrors.Forbidden</c>
    /// [Verified: 2026-08-25 @ <c>Program.cs</c> -&gt; <c>AddProblemDetails</c>], because
    /// <c>PermissionAuthorizationHandler</c> only declines to <c>Succeed</c> — it never reaches a
    /// handler that could return a more specific <c>Problem</c>
    /// [Verified: 2026-08-25 @ <c>PermissionAuthorizationHandler.cs</c> -&gt;
    /// <c>HandleRequirementAsync</c>]. <c>SeparationOfDuties.NotAssignedToProject</c> exists as an
    /// <c>Error</c> and both catalogues already translate it, but nothing in <c>src/Api</c>
    /// references it — confirmed by a solution-wide search finding zero call sites. Every existing
    /// 403 assertion in this suite and in <c>AssignUserToProjectTests</c> already expects
    /// <c>errors.auth.forbidden</c> for the same reason. Rewiring the gate to distinguish refusal
    /// reasons is a change to the one place every protected route shares, not a KAFF-114 change —
    /// flagged for the Architect rather than built quietly here. See decisions.md D-078.
    /// </remarks>
    [Fact]
    public async Task Access_ends_on_the_next_request_after_revocation()
    {
        HttpResponseMessage beforeRevoke = await WriteAsAsync(_siteEngineer, _projectA);

        beforeRevoke.StatusCode.Should().Be(
            HttpStatusCode.OK, "the engineer is assigned to project A and may act on it");

        HttpResponseMessage revoked = await RevokeAsync(_hr, _projectA, _siteEngineerAssignmentId);

        revoked.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage afterRevoke = await WriteAsAsync(_siteEngineer, _projectA);

        afterRevoke.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the same token, read against the database on the very next request, no longer carries an "
            + "active assignment");
        (await MessageKeyAsync(afterRevoke)).Should().Be(
            "errors.auth.forbidden",
            "the shipped gate has one blanket 403 key today — see this test's remarks");
    }

    // ---- AC-114-B · the row survives ----------------------------------------------------------------

    [Fact]
    public async Task The_revoked_row_stays_on_file_with_every_column_populated()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;

        (await RevokeAsync(_hr, _projectA, _siteEngineerAssignmentId))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();

        ProjectAssignment row = await reader.ProjectAssignments
            .SingleAsync(assignment => assignment.Id == _siteEngineerAssignmentId, Ct);

        row.AssignedAt.Should().NotBe(default, "the original grant is untouched by the revocation");
        row.AssignedByUserId.Should().Be(_owner, "who put them on the project in the first place");
        row.RevokedAt.Should().NotBeNull().And.BeOnOrAfter(before);
        row.RevokedByUserId.Should().Be(_hr, "who closed the row, not who held it");
        row.IsActive.Should().BeFalse("computed from RevokedAt, not a second stored flag");
    }

    // ---- AC-114-C · re-assignment is legal -----------------------------------------------------------

    [Fact]
    public async Task Re_assignment_after_revocation_creates_a_new_row_and_leaves_the_old_one_untouched()
    {
        (await RevokeAsync(_hr, _projectA, _siteEngineerAssignmentId))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext beforeReader = _database.CreateBareContext();
        ProjectAssignment revokedRow = await beforeReader.ProjectAssignments
            .SingleAsync(assignment => assignment.Id == _siteEngineerAssignmentId, Ct);
        DateTimeOffset revokedAtBeforeReassignment = revokedRow.RevokedAt!.Value;

        HttpResponseMessage reassigned = await AssignAsync(
            _hr, _projectA, _siteEngineer, AssignmentLevel.Junior);

        reassigned.StatusCode.Should().Be(HttpStatusCode.Created);

        await using KaffDbContext afterReader = _database.CreateBareContext();

        List<ProjectAssignment> rows = await afterReader.ProjectAssignments
            .Where(assignment => assignment.UserId == _siteEngineer && assignment.ProjectId == _projectA)
            .ToListAsync(Ct);

        rows.Should().HaveCount(2, "the revoked row stays, and re-assignment is a new row");

        ProjectAssignment stillRevoked = rows.Single(row => row.Id == _siteEngineerAssignmentId);
        stillRevoked.RevokedAt.Should().Be(
            revokedAtBeforeReassignment, "re-assigning the person does not touch their old row");

        ProjectAssignment fresh = rows.Single(row => row.Id != _siteEngineerAssignmentId);
        fresh.IsActive.Should().BeTrue();
    }

    // ---- AC-114-D · twice is refused -------------------------------------------------------------------

    [Fact]
    public async Task Revoking_an_already_revoked_assignment_is_refused()
    {
        (await RevokeAsync(_hr, _projectA, _siteEngineerAssignmentId))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage second = await RevokeAsync(_hr, _projectA, _siteEngineerAssignmentId);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(second)).Should().Be("errors.identity.assignment_already_revoked");

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.ProjectAssignments
                .CountAsync(assignment => assignment.Id == _siteEngineerAssignmentId, Ct))
            .Should().Be(1, "the refused second call created no second row and deleted nothing");
    }

    // ---- AC-114-E · nobody else can --------------------------------------------------------------------

    /// <summary>
    /// AC-114-E. Written and run red before <c>Endpoint.cs</c> carried
    /// <c>RequirePermission(Permission.ProjectAssignmentManage, ...)</c>, the same discipline
    /// decisions.md D-067 exists to have applied everywhere: a permission requirement described in
    /// prose and absent from the <c>Map</c> chain is a privilege-escalation primitive, not a missing
    /// check, and only a test that can go red catches it.
    /// </summary>
    /// <remarks>
    /// The third caller is a Supervisor site engineer genuinely assigned to project A, at the highest
    /// seniority the model has — so a 403 here is unmistakably the role half of "role × assignment"
    /// refusing them, and could not be misread as a missing row (AssignUserToProjectTests'
    /// <c>AC-113-G</c> uses the identical shape).
    /// </remarks>
    [Fact]
    public async Task Nobody_but_the_owner_and_hr_can_revoke_an_assignment()
    {
        foreach (Guid caller in new[] { _finance, _technicalOffice, _supervisorOnProjectA })
        {
            HttpResponseMessage response = await RevokeAsync(caller, _projectA, _siteEngineerAssignmentId);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await MessageKeyAsync(response)).Should().Be(
                "errors.auth.forbidden", "the refusal must be renderable in Arabic");
        }

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.ProjectAssignments
                .SingleAsync(assignment => assignment.Id == _siteEngineerAssignmentId, Ct))
            .IsActive.Should().BeTrue("no refused attempt closed the row");
    }

    // ---- AC-114-F · revocation is not deletion -----------------------------------------------------
    //
    // Established in EndpointPermissionCoverageTests.cs ->
    // No_endpoint_deletes_a_project_assignment: "no such endpoint exists" is a claim about every
    // route the host maps, which is exactly what that file already enumerates for the permission
    // check (D-067/D-068). A test in this file could only prove that one URL this file made up 404s
    // — true regardless of whether a DELETE route exists somewhere else — which is not the criterion
    // and would be the "cannot fail" shape agents.md §3c warns against.

    // ---- a route naming nobody --------------------------------------------------------------------

    /// <summary>
    /// Not an acceptance criterion — this is REST plumbing this session added
    /// (<see cref="Kaff.Domain.Identity.IdentityErrors.ProjectAssignmentNotFound"/>), and CLAUDE.md
    /// asks for tests against behaviour, not against the code just written. Kept minimal: it exists so
    /// the path does not silently become a 500 rather than to certify a rule spec.md never stated.
    /// </summary>
    [Fact]
    public async Task Revoking_an_assignment_id_that_does_not_exist_on_the_project_is_a_translatable_404()
    {
        HttpResponseMessage response = await RevokeAsync(_hr, _projectA, Guid.CreateVersion7());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.project_assignment_not_found");
    }

    // ---- the audit half ------------------------------------------------------------------------------

    /// <summary>
    /// The story's audit bullet: <c>Modified</c> on <c>ProjectAssignment</c>, <c>ProjectId</c> set,
    /// <c>ChangedProperties</c> naming <c>RevokedAt</c> and <c>RevokedByUserId</c>, actor Owner or HR.
    /// No handler writes this — the change tracker sees the entity change and
    /// <c>AuditSaveChangesInterceptor</c> writes it in the same transaction, exactly as
    /// <c>AssignUserToProjectTests</c>' equivalent test asserts for the <c>Created</c> half.
    /// </summary>
    [Fact]
    public async Task The_revocation_leaves_a_modified_audit_record_naming_what_changed()
    {
        (await RevokeAsync(_hr, _projectA, _siteEngineerAssignmentId))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(candidate => candidate.EntityType == nameof(ProjectAssignment)
                                && candidate.EntityId == _siteEngineerAssignmentId
                                && candidate.Action == AuditAction.Modified)
            .SingleAsync(Ct);

        record.ProjectId.Should().Be(_projectA, "the trail filters per project");
        record.ActorUserId.Should().Be(_hr);
        record.ActorRole.Should().Be(Role.Hr);
        record.ChangedProperties.Should().BeEquivalentTo(
            [nameof(ProjectAssignment.RevokedAt), nameof(ProjectAssignment.RevokedByUserId)]);

        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        after.RootElement.GetProperty(nameof(ProjectAssignment.RevokedByUserId)).GetGuid()
            .Should().Be(_hr);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private async Task<HttpResponseMessage> RevokeAsync(Guid actorId, Guid projectId, Guid assignmentId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"/api/projects/{projectId}/assignments/{assignmentId}/revoke", UriKind.Relative));

        (string stamp, Role? role) = await SessionAsync(actorId);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, stamp);

        if (role is not null)
        {
            request.Headers.Add(TestAuthHandler.RoleHeader, role.Value.ToString());
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> AssignAsync(
        Guid actorId, Guid projectId, Guid targetUserId, AssignmentLevel level)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"/api/projects/{projectId}/assignments", UriKind.Relative))
        {
            Content = JsonContent.Create(new { userId = targetUserId, level = level.ToString() }),
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

    /// <summary>
    /// The "daily-log-style request" of AC-114-A — a real write, reached through
    /// <c>Permission.ProjectRead</c> project-scoped, the same probe route KAFF-116's tests use.
    /// </summary>
    private async Task<HttpResponseMessage> WriteAsAsync(Guid actorId, Guid projectId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"/probe/projects/{projectId}/write", UriKind.Relative));

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
            UniqueNames.Code("RVK-C1"), "عميل الإلغاء", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("RVK-PA"), "مشروع الإلغاء", client.Id, ContractType.LumpSum, Now).Value;

        User owner = MakeUser("rvk-owner", Role.Owner);
        User hr = MakeUser("rvk-hr", Role.Hr, Department.Hr);
        User finance = MakeUser("rvk-finance", Role.Finance, Department.Finance);
        User technicalOffice = MakeUser(
            "rvk-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User siteEngineer = MakeUser(
            "rvk-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User supervisor = MakeUser(
            "rvk-supervisor", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);

        context.Clients.Add(client);
        context.Projects.Add(projectA);
        context.Users.AddRange(owner, hr, finance, technicalOffice, siteEngineer, supervisor);

        await context.SaveChangesAsync(Ct);

        ProjectAssignment engineerAssignment = ProjectAssignment
            .Create(projectA.Id, siteEngineer, AssignmentLevel.Junior, owner.Id, Now).Value;

        ProjectAssignment supervisorAssignment = ProjectAssignment
            .Create(projectA.Id, supervisor, AssignmentLevel.Supervisor, owner.Id, Now).Value;

        context.ProjectAssignments.AddRange(engineerAssignment, supervisorAssignment);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _owner = owner.Id;
        _hr = hr.Id;
        _finance = finance.Id;
        _technicalOffice = technicalOffice.Id;
        _siteEngineer = siteEngineer.Id;
        _siteEngineerAssignmentId = engineerAssignment.Id;
        _supervisorOnProjectA = supervisor.Id;
    }

    private static User MakeUser(
        string userName,
        Role role,
        Department? department = null,
        OperationsSubDepartment? subDepartment = null)
        => User.Create(
            UniqueNames.Code(userName),
            userName,
            UniqueNames.Phone(),
            role,
            Now,
            department,
            subDepartment,
            null).Value;

    private static DateTimeOffset Now => new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
