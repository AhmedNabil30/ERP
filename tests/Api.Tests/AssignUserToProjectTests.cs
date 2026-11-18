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
/// KAFF-113 — <c>POST /api/projects/{projectId}/assignments</c>, the second half of "permission =
/// role × assignment".
/// </summary>
/// <remarks>
/// <para>
/// Every test here goes through the HTTP endpoint. The seniority and external-role rules are already
/// pinned in the domain; what cannot be seen from there is the level above — that the handler routes
/// through <c>ProjectAssignment.Create</c> and returns its refusal, and that HR's global reach and a
/// project-scoped permission coexist on one route.
/// </para>
/// <para>
/// spec.md §9: "A user MUST be assigned to a project to open it or act on it. Role alone is
/// insufficient. Enforcement is server-side; hiding UI elements is presentation, not security."
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class AssignUserToProjectTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _projectB;

    private Guid _owner;
    private Guid _hr;
    private Guid _finance;
    private Guid _technicalOffice;
    private Guid _siteEngineer;
    private Guid _supervisorOnProjectA;
    private Guid _portalClient;
    private Guid _subcontractor;
    private Guid _leaver;

    public AssignUserToProjectTests(PostgresDatabase database) => _database = database;

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

    // ---- D-233 item 3 · only the Owner staffs a project ----------------------------------------

    /// <summary>
    /// HR held <c>ProjectAssignmentManage</c> with global reach from slice 1 (AC-113-A / AC-113-B). D-233 item 3
    /// removed the grant: HR cannot open a project, so it never reached the team tab. The refusal is a 403 and
    /// no row is written.
    /// </summary>
    [Fact]
    public async Task Hr_cannot_staff_a_project_and_the_refusal_leaves_no_row()
    {
        // D-233 item 3: only the Owner adds and removes people on a project. HR lost the grant.
        HttpResponseMessage refused = await AssignAsync(_hr, _projectA, _technicalOffice, AssignmentLevel.Standard);

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageKeyAsync(refused)).Should().Be("errors.auth.forbidden");
        (await ActiveAssignmentCountAsync(_technicalOffice)).Should().Be(0, "a refused attempt puts nobody on a project");

        HttpResponseMessage read = await SendAsync(
            new Uri($"/probe/projects/{_projectA}", UriKind.Relative), _hr);

        read.StatusCode.Should().Be(HttpStatusCode.Forbidden, "HR is absent from ProjectRead, D-044 ruling 2");
    }

    /// <summary>AC-113-A's other half: the Owner reaches every project without a row either (rule 3).</summary>
    [Fact]
    public async Task The_owner_staffs_a_project_without_an_assignment_row_of_their_own()
    {
        (await ActiveAssignmentCountAsync(_owner)).Should().Be(0);

        (await AssignAsync(_owner, _projectB, _finance, AssignmentLevel.Standard))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ---- AC-113-C · reach stops at a project that does not exist -------------------------------

    /// <summary>
    /// AC-113-C. The permission stays <c>ProjectScoped</c>, so the route must still name a project
    /// that exists — and the refusal is a 403, not a 500 and not a foreign-key violation.
    /// </summary>
    /// <remarks>
    /// This is the assertion that fails if somebody "fixes" HR's reach by widening the catalogue row
    /// to <c>CompanyWide</c>. The global-reach branch is itself bounded by the project existing
    /// [Verified: 2026-08-24 @ <c>ProjectAccessPolicy.cs</c> -&gt; <c>GlobalReachAsync</c>]; a
    /// company-wide row would never consult it.
    /// </remarks>
    [Fact]
    public async Task The_owners_reach_stops_at_a_project_that_does_not_exist()
    {
        HttpResponseMessage response = await AssignAsync(
            _owner, Guid.CreateVersion7(), _technicalOffice, AssignmentLevel.Standard);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ((int)response.StatusCode).Should().BeLessThan(500, "the refusal is not a 500");
        (await MessageKeyAsync(response)).Should().Be("errors.auth.forbidden");
    }

    // ---- AC-113-D · the same engineer, two seniorities ------------------------------------------

    /// <summary>
    /// AC-113-D. Seniority is a property of the assignment, not of the person (D-044 ruling 5).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The second half is asserted against <c>PermissionEvaluator</c> directly, which is what the
    /// criterion now says and what actually decides. The endpoint-level version of it belongs to
    /// KAFF-105b's per-project permission list, which is deferred out of this sprint.
    /// </para>
    /// <para>
    /// The access each call is given is the one the <b>database</b> holds for that project, read
    /// through the shipped policy rather than constructed in the test — otherwise the assertion is
    /// about a <c>ProjectAccess</c> the test wrote itself and the two rows never enter into it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task The_same_engineer_is_supervisor_on_one_project_and_junior_on_another()
    {
        (await AssignAsync(_owner, _projectA, _siteEngineer, AssignmentLevel.Supervisor))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await AssignAsync(_owner, _projectB, _siteEngineer, AssignmentLevel.Junior))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await LevelOnAsync(_projectA, _siteEngineer)).Should().Be(AssignmentLevel.Supervisor);
        (await LevelOnAsync(_projectB, _siteEngineer)).Should().Be(AssignmentLevel.Junior);

        var subject = new PermissionSubject(
            _siteEngineer,
            Role.SiteEngineer,
            WellKnownDepartments.OperationsId,
            OperationsSubDepartment.Technical,
            null,
            "site-engineer");

        PermissionEvaluator
            .Evaluate(subject, Permission.DraftSubmit, _projectA, await AccessAsync(subject, _projectA))
            .Should().Be(PermissionDecision.Granted, "the supervisor submits what juniors draft");

        PermissionEvaluator
            .Evaluate(subject, Permission.DraftSubmit, _projectB, await AccessAsync(subject, _projectB))
            .Should().Be(
                PermissionDecision.AssignmentLevelTooLow,
                "the same person, the same permission, the other project's row");
    }

    // ---- AC-113-E · seniority is refused where spec.md §9 does not put it -----------------------

    /// <summary>
    /// AC-113-E, both halves. A level other than <c>Standard</c> is legal only for a site engineer,
    /// and <c>Standard</c> is legal only for everybody else.
    /// </summary>
    /// <remarks>
    /// <b>The second half is the one a handler quietly breaks.</b> Coercing a Finance user's
    /// <c>Supervisor</c> to <c>Standard</c> on the way past compiles clean, keeps every Domain test
    /// green, and creates the row — decisions.md D-066 §2 recorded exactly that mutation on the
    /// create-user path. The row is therefore asserted absent as well as the status asserted 400.
    /// </remarks>
    [Fact]
    public async Task A_seniority_is_refused_for_every_role_but_the_site_engineer()
    {
        foreach ((Guid target, AssignmentLevel level, string because) in new (Guid, AssignmentLevel, string)[]
        {
            (_finance, AssignmentLevel.Supervisor, "spec.md §9 attaches Junior/Supervisor to the site engineer alone"),
            (_finance, AssignmentLevel.Junior, "and to no other role, in either direction"),
            (_siteEngineer, AssignmentLevel.Standard, "a site engineer is one or the other, never neither"),
        })
        {
            HttpResponseMessage response = await AssignAsync(_owner, _projectA, target, level);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, because);

            (await MessageKeyAsync(response)).Should()
                .Be("errors.identity.assignment_level_not_applicable");

            (await ActiveAssignmentCountAsync(target)).Should().Be(
                0, "no refused attempt may create a row with a corrected level");
        }
    }

    // ---- AC-113-F · clients and subcontractors are not assignable -------------------------------

    [Fact]
    public async Task Clients_and_subcontractors_are_not_assignable()
    {
        foreach (Guid target in new[] { _portalClient, _subcontractor })
        {
            HttpResponseMessage response = await AssignAsync(_owner, _projectA, target, AssignmentLevel.Standard);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await MessageKeyAsync(response)).Should().Be("errors.identity.client_is_not_assignable");
            (await ActiveAssignmentCountAsync(target)).Should().Be(0);
        }
    }

    [Fact]
    public async Task An_hr_user_is_not_assignable_because_an_assignment_gives_hr_nothing()
    {
        HttpResponseMessage response = await AssignAsync(_owner, _projectA, _hr, AssignmentLevel.Standard);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.hr_is_not_assignable");
        (await ActiveAssignmentCountAsync(_hr)).Should().Be(0);
    }

    // ---- AC-113-G · nobody else can staff a project ---------------------------------------------

    /// <summary>
    /// AC-113-G. Being on the project is not permission to staff it.
    /// </summary>
    /// <remarks>
    /// The Supervisor site engineer in this list is assigned to <c>_projectA</c> and holds the
    /// highest seniority the model has, so their refusal is about the role half of "role ×
    /// assignment" and could not be mistaken for a missing row.
    /// </remarks>
    [Fact]
    public async Task Nobody_but_the_owner_can_staff_a_project()
    {
        foreach (Guid caller in new[] { _hr, _finance, _technicalOffice, _supervisorOnProjectA })
        {
            HttpResponseMessage response = await AssignAsync(caller, _projectA, _siteEngineer, AssignmentLevel.Junior);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await MessageKeyAsync(response)).Should().Be(
                "errors.auth.forbidden", "the refusal must be renderable in Arabic");
        }

        (await ActiveAssignmentCountAsync(_siteEngineer)).Should().Be(
            0, "no refused attempt put anybody on a project");
    }

    // ---- AC-113-H · an inactive user is not assignable ------------------------------------------

    [Fact]
    public async Task A_deactivated_user_is_not_assignable()
    {
        HttpResponseMessage response = await AssignAsync(_owner, _projectA, _leaver, AssignmentLevel.Standard);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.user_is_inactive");

        (await ActiveAssignmentCountAsync(_leaver)).Should().Be(0, "an assignment does not resurrect one");

        await using KaffDbContext reader = _database.CreateBareContext();
        User leaver = await reader.Users.SingleAsync(user => user.Id == _leaver, Ct);
        leaver.IsActive.Should().BeFalse();
    }

    // ---- AC-113-I · no duplicate active assignment ----------------------------------------------

    [Fact]
    public async Task A_second_active_assignment_is_refused_and_re_assignment_after_revocation_is_not()
    {
        (await AssignAsync(_owner, _projectA, _technicalOffice, AssignmentLevel.Standard))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage again = await AssignAsync(_owner, _projectA, _technicalOffice, AssignmentLevel.Standard);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(again)).Should().Be("errors.identity.user_already_assigned_to_project");
        (await ActiveAssignmentCountAsync(_technicalOffice)).Should().Be(1);

        await RevokeAsync(_projectA, _technicalOffice);

        (await AssignAsync(_owner, _projectA, _technicalOffice, AssignmentLevel.Standard))
            .StatusCode.Should().Be(
                HttpStatusCode.Created,
                "the unique index filters on revoked_at IS NULL, so a revoked row is not in the way");

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.ProjectAssignments
            .CountAsync(assignment => assignment.UserId == _technicalOffice
                                      && assignment.ProjectId == _projectA, Ct))
            .Should().Be(2, "the revoked row stays on file — it is the historical team");
    }

    /// <summary>A route naming a user that does not exist is a 404 the client can translate.</summary>
    [Fact]
    public async Task Assigning_a_user_who_does_not_exist_is_refused()
    {
        HttpResponseMessage response = await AssignAsync(
            _owner, _projectA, Guid.CreateVersion7(), AssignmentLevel.Standard);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.user_not_found");
    }

    // ---- the audit half --------------------------------------------------------------------------

    /// <summary>
    /// The story's audit bullet: <c>Created</c> on <c>ProjectAssignment</c> with <c>ProjectId</c> set,
    /// so the trail filters per project — plus KAFF-116's <c>GrantPath</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No handler writes this. The assignment is an entity change, so
    /// <c>AuditSaveChangesInterceptor</c> writes it in the same transaction — decisions.md D-031 and
    /// KAFF-118 rule 2 forbid the hand-written alternative.
    /// </para>
    /// <para>
    /// <b><c>HrGlobal</c> rather than <c>Assignment</c> is the whole of KAFF-116 on this endpoint.</b>
    /// HR holds no row on this project, so a trail that derived the path from the assignment table
    /// would find nothing to point at and record either null or a fabricated <c>Assignment</c>
    /// (D-070 §2). The Owner's record on the same act says <c>OwnerGlobal</c>, and the two are
    /// separate rulings that must stay distinguishable (D-010 versus D-044 ruling 3).
    /// </para>
    /// </remarks>
    [Fact]
    public async Task The_assignment_leaves_an_audit_record_naming_the_project_and_how_the_owner_reached_it()
    {
        HttpResponseMessage ownerAssigned = await AssignAsync(
            _owner, _projectB, _technicalOffice, AssignmentLevel.Standard);

        ownerAssigned.StatusCode.Should().Be(HttpStatusCode.Created);

        Guid ownerRow = await CreatedIdAsync(ownerAssigned);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord byOwner = await reader.AuditRecords.SingleAsync(
            record => record.EntityType == nameof(ProjectAssignment)
                      && record.Action == AuditAction.Created
                      && record.EntityId == ownerRow,
            Ct);

        byOwner.ProjectId.Should().Be(_projectB, "the trail filters per project");
        byOwner.ActorUserId.Should().Be(_owner);
        byOwner.ActorRole.Should().Be(Role.Owner);
        byOwner.GrantPath.Should().Be(ProjectAccessPath.OwnerGlobal);

        using JsonDocument after = JsonDocument.Parse(byOwner.AfterJson!);

        after.RootElement.GetProperty(nameof(ProjectAssignment.UserId)).GetGuid()
            .Should().Be(_technicalOffice);

        after.RootElement.GetProperty(nameof(ProjectAssignment.AssignedByUserId)).GetGuid()
            .Should().Be(_owner, "KAFF-113 rule 10 — the row records who assigned");

        after.RootElement.GetProperty(nameof(ProjectAssignment.Level)).GetString()
            .Should().Be(nameof(AssignmentLevel.Standard), "enums travel as member names");
    }

    // ---- helpers ---------------------------------------------------------------------------------

    /// <summary>
    /// Issues the assignment as <paramref name="actorId"/>.
    /// </summary>
    /// <remarks>
    /// The role, department and stamp are read from the database for the actor, and the level is
    /// written as a string rather than as an enum value — a test serialising with the same converter
    /// the server deserialises with would pass on a numeric wire form too.
    /// </remarks>
    private async Task<HttpResponseMessage> AssignAsync(
        Guid actorId,
        Guid projectId,
        Guid targetUserId,
        AssignmentLevel level)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"/api/projects/{projectId}/assignments", UriKind.Relative))
        {
            Content = JsonContent.Create(new { userId = targetUserId, level = level.ToString() }),
        };

        (string Stamp, Role? Role) session = await SessionAsync(actorId);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, session.Stamp);

        // The role claim, because a real token carries one and the audit record's ActorRole is read
        // from it rather than from the database [Verified: 2026-08-24 @ HttpContextCurrentUser.cs ->
        // Role]. The gate does not consult it — see SendAsync, which omits it on purpose.
        if (session.Role is not null)
        {
            request.Headers.Add(TestAuthHandler.RoleHeader, session.Role.Value.ToString());
        }

        return await _client.SendAsync(request, Ct);
    }

    /// <summary>
    /// Issues a GET as <paramref name="actorId"/> carrying nothing but the id and the stamp.
    /// </summary>
    /// <remarks>
    /// The role and department headers are deliberately omitted here: the gate reads all of them from
    /// the database (D-048), so a test that supplied them would pass on a token-driven gate too.
    /// </remarks>
    private async Task<HttpResponseMessage> SendAsync(Uri route, Guid actorId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        (string stamp, Role? _) = await SessionAsync(actorId);

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, stamp);

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

    /// <summary>The identifier of the row a 201 reports, read off the response the endpoint returned.</summary>
    private static async Task<Guid> CreatedIdAsync(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return body.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key)
            ? key.GetString()
            : null;
    }

    private async Task<int> ActiveAssignmentCountAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.ProjectAssignments
            .CountAsync(assignment => assignment.UserId == userId && assignment.RevokedAt == null, Ct);
    }

    private async Task<AssignmentLevel> LevelOnAsync(Guid projectId, Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.ProjectAssignments
            .Where(assignment => assignment.ProjectId == projectId
                                 && assignment.UserId == userId
                                 && assignment.RevokedAt == null)
            .Select(assignment => assignment.Level)
            .SingleAsync(Ct);
    }

    /// <summary>The shipped access policy's answer, against the rows the endpoint actually wrote.</summary>
    private async Task<ProjectAccess> AccessAsync(PermissionSubject subject, Guid projectId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await new Kaff.Infrastructure.Authorization.ProjectAccessPolicy(reader)
            .EvaluateAsync(subject, projectId, Ct);
    }

    /// <summary>
    /// Revokes directly against the database rather than through the endpoint, so this setup
    /// step does not depend on KAFF-114's HTTP layer. KAFF-114 shipped (commit 33010e2, D-078);
    /// its own endpoint is exercised by RevokeProjectAssignmentTests.
    /// </summary>
    private async Task RevokeAsync(Guid projectId, Guid userId)
    {
        await using KaffDbContext context = _database.CreateContext();

        ProjectAssignment assignment = await context.ProjectAssignments
            .SingleAsync(candidate => candidate.ProjectId == projectId
                                      && candidate.UserId == userId
                                      && candidate.RevokedAt == null,
                Ct);

        assignment.Revoke(_owner, Now).IsSuccess.Should().BeTrue();

        await context.SaveChangesAsync(Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("ASG-C1"), "عميل الإسناد", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("ASG-PA"), "مشروع أ", client.Id, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;

        Project projectB = Project.Create(
            UniqueNames.Code("ASG-PB"), "مشروع ب", client.Id, ContractType.LumpSum, DateTimeOffset.UtcNow).Value;

        User owner = MakeUser("asg-owner", Role.Owner);
        User hr = MakeUser("asg-hr", Role.Hr, WellKnownDepartments.HrId);
        User finance = MakeUser("asg-finance", Role.Finance, WellKnownDepartments.FinanceId);
        User technicalOffice = MakeUser(
            "asg-tech", Role.TechnicalOffice, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User siteEngineer = MakeUser(
            "asg-engineer", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User supervisor = MakeUser(
            "asg-supervisor", Role.SiteEngineer, WellKnownDepartments.OperationsId, OperationsSubDepartment.Technical);
        User portal = MakeUser("asg-portal", Role.Client, clientId: client.Id);
        User subcontractor = MakeUser("asg-sub", Role.Subcontractor);
        User leaver = MakeUser("asg-leaver", Role.Finance, WellKnownDepartments.FinanceId);

        leaver.Deactivate(Now).IsSuccess.Should().BeTrue();

        context.Clients.Add(client);
        context.Projects.AddRange(projectA, projectB);
        context.Users.AddRange(
            owner, hr, finance, technicalOffice, siteEngineer, supervisor, portal, subcontractor, leaver);

        await context.SaveChangesAsync(Ct);

        // The only seeded assignment. AC-113-G needs a caller who IS on the project and at the
        // highest seniority, so their 403 is unmistakably about the role half; every other actor here
        // must hold no row, which several tests assert rather than assume.
        context.ProjectAssignments.Add(
            ProjectAssignment.Create(projectA.Id, supervisor, AssignmentLevel.Supervisor, owner.Id, Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _projectB = projectB.Id;
        _owner = owner.Id;
        _hr = hr.Id;
        _finance = finance.Id;
        _technicalOffice = technicalOffice.Id;
        _siteEngineer = siteEngineer.Id;
        _supervisorOnProjectA = supervisor.Id;
        _portalClient = portal.Id;
        _subcontractor = subcontractor.Id;
        _leaver = leaver.Id;
    }

    private static User MakeUser(
        string userName,
        Role role,
        Guid? department = null,
        OperationsSubDepartment? subDepartment = null,
        Guid? clientId = null)
        => User.Create(
            UniqueNames.Code(userName),
            userName,
            UniqueNames.Phone(),
            role,
            Now,
            department,
            subDepartment,
            clientId).Value;

    private static DateTimeOffset Now => new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
