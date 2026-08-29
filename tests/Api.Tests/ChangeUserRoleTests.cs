using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-109 — <c>PUT /api/users/{userId}/role</c>, and the reversal decisions.md D-051 (Q27) made.
/// </summary>
/// <remarks>
/// <para>
/// <b>D-049 ruling 6 said a role change is refused while the user supervises a project. That is not
/// the rule under test here.</b> D-051 (Q27) reverses it the next day: a role change always revokes
/// every active <c>ProjectAssignment</c> the user holds — Supervisor, Junior and Standard alike — and
/// never refuses on that ground. Every test below that changes a role that carries assignments expects
/// success, not a refusal, and checks that the assignments are gone from the active set instead.
/// </para>
/// <para>
/// spec.md §9, decisions.md D-048: the token supplies identity, the database supplies authority, on
/// every request — <c>AC-109-E</c> and <c>AC-109-F</c> exercise that mechanism rather than adding one.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ChangeUserRoleTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectA;
    private Guid _projectB;
    private Guid _projectC;

    private Guid _owner;
    private Guid _hr;
    private Guid _financeProbe;
    private Guid _supervisorEngineer;
    private Guid _juniorEngineer;
    private Guid _mirrorFinance;
    private Guid _marketingUser;
    private Guid _credentialedOwner;
    private Guid _recordlessOwner;

    public ChangeUserRoleTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-109-A · a supervisor comes off site, and is not refused -----------------------------

    /// <summary>
    /// AC-109-A. The reversal itself: a role change succeeds while the user is an active Supervisor,
    /// and their assignment is revoked rather than the request being refused.
    /// </summary>
    [Fact]
    public async Task A_supervisor_comes_off_site_and_is_not_refused()
    {
        HttpResponseMessage response = await ChangeRoleAsync(_owner, _supervisorEngineer, Role.TechnicalOffice);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "D-051 (Q27) reverses D-049 ruling 6 — no refusal");

        Response body = await ReadResponseAsync(response);

        body.RevokedProjectIds.Should().BeEquivalentTo(new[] { _projectA }, "the response names project A");

        ProjectAssignment row = await AssignmentAsync(_supervisorEngineer, _projectA);

        row.IsActive.Should().BeFalse("the supervisor's link to the site is severed");
        row.RevokedByUserId.Should().Be(_owner);
    }

    // ---- AC-109-B · junior assignments go too -----------------------------------------------------

    /// <summary>AC-109-B — the half D-049 left open. Junior rows are revoked exactly like Supervisor rows.</summary>
    [Fact]
    public async Task Junior_assignments_go_too()
    {
        HttpResponseMessage response = await ChangeRoleAsync(_owner, _juniorEngineer, Role.TechnicalOffice);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Response body = await ReadResponseAsync(response);

        body.RevokedProjectIds.Should().BeEquivalentTo(
            new[] { _projectA, _projectB, _projectC }, "all three are named");

        await using KaffDbContext reader = _database.CreateBareContext();

        List<ProjectAssignment> rows = await reader.ProjectAssignments
            .Where(assignment => assignment.UserId == _juniorEngineer)
            .ToListAsync(Ct);

        rows.Should().HaveCount(3);
        rows.Should().AllSatisfy(row => row.IsActive.Should().BeFalse());
    }

    // ---- AC-109-C · the mirror case ---------------------------------------------------------------

    /// <summary>
    /// AC-109-C. An office user holding <c>Standard</c> rows becomes a Site Engineer. Both rows are
    /// revoked, and nothing is left behind that <c>ProjectAssignment.Create</c> would refuse to create
    /// — the mirror of <see cref="Junior_assignments_go_too"/>.
    /// </summary>
    [Fact]
    public async Task The_mirror_case_standard_rows_become_a_site_engineer()
    {
        HttpResponseMessage response = await ChangeRoleAsync(_owner, _mirrorFinance, Role.SiteEngineer);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Response body = await ReadResponseAsync(response);

        body.RevokedProjectIds.Should().BeEquivalentTo(new[] { _projectA, _projectB });

        await using KaffDbContext reader = _database.CreateBareContext();

        (await reader.ProjectAssignments
            .Where(assignment => assignment.UserId == _mirrorFinance && assignment.RevokedAt == null)
            .CountAsync(Ct))
            .Should().Be(0, "no Standard row is left active under a SiteEngineer");

        // The general invariant this case exists to protect: no active row anywhere pairs a level
        // with a role that ProjectAssignment.Create would refuse to create.
        var activePairs = await reader.ProjectAssignments
            .Where(assignment => assignment.RevokedAt == null)
            .Join(reader.Users, assignment => assignment.UserId, user => user.Id,
                (assignment, user) => new { assignment.Level, user.Role })
            .ToListAsync(Ct);

        activePairs.Should().AllSatisfy(pair =>
        {
            if (pair.Role == Role.SiteEngineer)
            {
                pair.Level.Should().NotBe(AssignmentLevel.Standard, "a SiteEngineer never holds Standard");
            }
            else
            {
                pair.Level.Should().Be(AssignmentLevel.Standard, "only a SiteEngineer holds Junior/Supervisor");
            }
        });
    }

    // ---- AC-109-D · history survives ---------------------------------------------------------------

    /// <summary>AC-109-D. Revocation is a soft close: the row stays, with its original authorship intact.</summary>
    [Fact]
    public async Task History_survives_the_revocation()
    {
        (await ChangeRoleAsync(_owner, _supervisorEngineer, Role.TechnicalOffice))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        ProjectAssignment row = await AssignmentAsync(_supervisorEngineer, _projectA);

        row.AssignedByUserId.Should().Be(_owner, "nothing about the original assignment is rewritten");
        row.RevokedAt.Should().NotBeNull();
        row.RevokedByUserId.Should().Be(_owner);
    }

    // ---- AC-109-E · nothing is restored -------------------------------------------------------------

    /// <summary>
    /// AC-109-E. The new role (TechnicalOffice) genuinely holds <c>ProjectRead</c>
    /// [Verified: 2026-08-25 @ <c>PermissionCatalogue.cs</c> -&gt; the <c>Permission.ProjectRead</c>
    /// row] — so a request refused here is refused on the assignment axis, not the role axis, which is
    /// the point: the revoked link is not restored by anything about the new role being compatible.
    /// </summary>
    [Fact]
    public async Task Nothing_is_restored_after_the_change()
    {
        string session = await StaleSession(_supervisorEngineer);

        (await ChangeRoleAsync(_owner, _supervisorEngineer, Role.TechnicalOffice))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        Uri projectRoute = new($"/probe/projects/{_projectA}", UriKind.Relative);

        HttpResponseMessage response = await SendAsync(projectRoute, _supervisorEngineer, session);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageKeyAsync(response)).Should().Be(
            "errors.auth.forbidden", "the blanket key — D-080 — discloses nothing about which axis gated the route");
    }

    // ---- AC-109-F · takes effect immediately, on the same session -----------------------------------

    /// <summary>
    /// AC-109-F. A live session opened before the change loses <c>TreasuryPostProject</c> on its very
    /// next request — no re-authentication, no token rotation. The security stamp is captured once,
    /// before the change, and never re-read, so this is honest about testing the database re-read
    /// (D-048) rather than a fresh token that happens to carry the new authority.
    /// </summary>
    [Fact]
    public async Task The_change_takes_effect_on_the_next_request_not_at_token_expiry()
    {
        string session = await StaleSession(_financeProbe);

        Uri treasuryPost = new($"/probe/projects/{_projectA}/treasury-post", UriKind.Relative);

        (await SendAsync(treasuryPost, _financeProbe, session))
            .StatusCode.Should().Be(HttpStatusCode.OK, "Finance holds TreasuryPostProject with a live assignment");

        (await ChangeRoleAsync(_owner, _financeProbe, Role.TechnicalOffice))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(treasuryPost, _financeProbe, session))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "authority is re-read from the database on every request — the same session, a different answer");
    }

    // ---- AC-109-G · the department rules are re-applied ----------------------------------------------

    /// <summary>
    /// AC-109-G. KAFF-109 rule 11: department compatibility is re-applied exactly as at creation. A
    /// refused change is not a change — the assignment the Marketing user still holds proves it.
    /// </summary>
    [Fact]
    public async Task The_department_rules_are_re_applied_and_a_refusal_revokes_nothing()
    {
        HttpResponseMessage response = await ChangeRoleAsync(_owner, _marketingUser, Role.Hr);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.hr_role_requires_hr_department");

        await using KaffDbContext reader = _database.CreateBareContext();

        User untouched = await reader.Users.SingleAsync(user => user.Id == _marketingUser, Ct);
        untouched.Role.Should().Be(Role.MarketingSales, "a refused change is not a change");

        ProjectAssignment row = await AssignmentAsync(_marketingUser, _projectA);
        row.IsActive.Should().BeTrue("no assignment is revoked when the change itself is refused");
    }

    // ---- AC-109-H · a change to the same role does nothing --------------------------------------------

    /// <summary>AC-109-H. Rule 8 — the request is accepted, and there is nothing to sever.</summary>
    [Fact]
    public async Task A_change_to_the_same_role_revokes_nothing()
    {
        HttpResponseMessage response = await ChangeRoleAsync(_owner, _supervisorEngineer, Role.SiteEngineer);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Response body = await ReadResponseAsync(response);
        body.RevokedProjectIds.Should().BeEmpty("the role held is the role requested — rule 8");

        (await AssignmentAsync(_supervisorEngineer, _projectA)).IsActive.Should().BeTrue();
    }

    // ---- AC-109-I · only the Owner may -----------------------------------------------------------------

    /// <summary>AC-109-I. HR staffs every project and still may not change a role — that is UserManage, not ProjectAssignmentManage.</summary>
    [Fact]
    public async Task Nobody_but_the_owner_can_change_a_role()
    {
        HttpResponseMessage response = await ChangeRoleAsync(_hr, _supervisorEngineer, Role.TechnicalOffice);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageKeyAsync(response)).Should().Be("errors.auth.forbidden");

        await using KaffDbContext reader = _database.CreateBareContext();

        User untouched = await reader.Users.SingleAsync(user => user.Id == _supervisorEngineer, Ct);
        untouched.Role.Should().Be(Role.SiteEngineer, "no refused attempt changed anybody's role");

        (await AssignmentAsync(_supervisorEngineer, _projectA)).IsActive.Should().BeTrue(
            "and no refused attempt revoked anything");
    }

    // ---- AC-109-J · the before-state and every revocation are in the trail --------------------------

    /// <summary>
    /// AC-109-J. One <c>User</c> record naming the old and new role, one <c>ProjectAssignment</c>
    /// record per revoked row naming its project, all sharing the request's correlation id — the same
    /// shape <c>AC-110-F</c> already established for KAFF-111.
    /// </summary>
    [Fact]
    public async Task The_trail_names_the_actor_both_roles_and_every_revoked_project()
    {
        HttpResponseMessage response = await ChangeRoleAsync(_owner, _juniorEngineer, Role.TechnicalOffice);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        Guid correlationId = Guid.Parse(
            response.Headers.GetValues(Kaff.Api.Common.Middleware.AuditCorrelationMiddleware.HeaderName).Single());

        await using KaffDbContext reader = _database.CreateBareContext();

        List<AuditRecord> records = await reader.AuditRecords
            .Where(record => record.CorrelationId == correlationId)
            .ToListAsync(Ct);

        records.Should().HaveCount(4, "one User record and one per revoked assignment");

        records.Should().AllSatisfy(record =>
        {
            record.Action.Should().Be(AuditAction.Modified);
            record.ActorUserId.Should().Be(_owner);
        });

        AuditRecord user = records.Single(record => record.EntityType == nameof(User));

        user.ChangedProperties.Should().Contain(nameof(User.Role));

        using JsonDocument before = JsonDocument.Parse(user.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(user.AfterJson!);

        before.RootElement.GetProperty(nameof(User.Role)).GetString().Should().Be(nameof(Role.SiteEngineer));
        after.RootElement.GetProperty(nameof(User.Role)).GetString().Should().Be(nameof(Role.TechnicalOffice));

        List<AuditRecord> assignmentRecords = records
            .Where(record => record.EntityType == nameof(ProjectAssignment))
            .ToList();

        assignmentRecords.Should().HaveCount(3);
        assignmentRecords.Select(record => record.ProjectId)
            .Should().BeEquivalentTo(new Guid?[] { _projectA, _projectB, _projectC });
    }

    // ---- V-26-A · the reachable 500 -------------------------------------------------------------

    /// <summary>
    /// <c>V-26-A</c>. Converting an account that holds a credential into a subcontractor is refused
    /// with a translatable key, not a bare <c>500</c>.
    /// </summary>
    /// <remarks>
    /// The target is a departmentless <see cref="Role.Owner"/> — the shape KAFF-100's setup screen
    /// mints, and the one that passes every check <c>User.ChangeRole</c> used to apply. The save then
    /// violated <c>ck_users_subcontractor_cannot_log_in</c> and the <c>DbUpdateException</c> reached
    /// the caller as a ProblemDetails carrying no <c>code</c> and no <c>messageKey</c>, which the
    /// Arabic shell cannot render — CLAUDE.md, "no hardcoded user-facing strings", and "domain errors
    /// are <c>Result&lt;T&gt;</c>, not exceptions".
    /// </remarks>
    [Fact]
    public async Task Converting_an_account_that_holds_a_credential_into_a_subcontractor_is_refused()
    {
        HttpResponseMessage response = await ChangeRoleAsync(_owner, _credentialedOwner, Role.Subcontractor);

        response.StatusCode.Should().Be(
            HttpStatusCode.Conflict,
            "the database's own rule, translated back into a Result — never a 500");
        (await MessageKeyAsync(response)).Should().Be("errors.identity.subcontractor_cannot_log_in");

        (await ActorRoleAsync(_credentialedOwner)).Should().Be(Role.Owner, "a refused change is not a change");

        await using KaffDbContext reader = _database.CreateBareContext();

        List<ProjectAssignment> rows = await reader.ProjectAssignments
            .Where(assignment => assignment.UserId == _credentialedOwner)
            .ToListAsync(Ct);

        rows.Should().HaveCount(2);
        rows.Should().AllSatisfy(row => row.IsActive.Should().BeTrue(
            "AC-109-G — a refused change revokes nothing. This is the request decisions.md D-082 §4 "
            + "argued could not fail mid-batch; it could, and now it is refused before the loop runs"));
    }

    /// <summary>
    /// The same conversion on an account holding no credential succeeds. The refusal above is about
    /// the credential, not about the role.
    /// </summary>
    /// <remarks>
    /// Exactly what <c>ck_users_subcontractor_cannot_log_in</c> permits. Here so nobody reads
    /// <see cref="Converting_an_account_that_holds_a_credential_into_a_subcontractor_is_refused"/> as
    /// "a user may never become a subcontractor" — a rule no source states, and the open half of
    /// decisions.md D-088's question for Nabil.
    /// </remarks>
    [Fact]
    public async Task Converting_an_account_with_no_credential_into_a_subcontractor_succeeds()
    {
        HttpResponseMessage response = await ChangeRoleAsync(_owner, _recordlessOwner, Role.Subcontractor);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ActorRoleAsync(_recordlessOwner)).Should().Be(Role.Subcontractor);
    }

    /// <summary>A route naming a user that does not exist is a 404 the client can translate.</summary>
    [Fact]
    public async Task Changing_the_role_of_a_user_who_does_not_exist_is_refused()
    {
        HttpResponseMessage response = await ChangeRoleAsync(_owner, Guid.CreateVersion7(), Role.TechnicalOffice);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.user_not_found");
    }

    /// <summary>
    /// <c>V-27-C</c>. A number that is not one of the nine roles is refused, and nothing is written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>All three of these answered <c>200</c> and persisted.</b> The Verifier's 36-input sweep read
    /// <c>role = '99'</c> back out of the users table: the enum is stored as text (D-002), so the
    /// column takes whatever the wire sent. No validator exists in this slice, no check constraint
    /// refuses it — <c>ck_users_client_scope</c> and <c>ck_users_operations_sub_department</c> are
    /// both satisfied by a value that is neither <c>'Client'</c> nor <c>'Operations'</c> — and
    /// <c>ChangeRole</c> re-applied the creation invariants without ever asking whether the role was a
    /// role (qa/slice-1/verification-2026-08-27.md §6).
    /// </para>
    /// <para>
    /// <b>Sent as a JSON number, which is how it gets in.</b> The wire convention is the member name
    /// and <c>ChangeUserRole.Request</c> documents it as such, but <c>JsonStringEnumConverter</c>
    /// accepts integers as well, and every integer is a candidate <c>Role</c> because the CLR does not
    /// range-check an enum cast.
    /// </para>
    /// <para>
    /// The status code is not asserted beyond "not success" on purpose: <c>W-5</c> is open with the
    /// Architect over whether a framework-produced <c>400</c> should carry a <c>messageKey</c>, and
    /// this test is about the value never reaching the table, not about which refusal shape wins that
    /// question.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(99)]
    public async Task A_role_outside_the_enum_is_refused_and_never_persisted(int role)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            new Uri($"/api/users/{_supervisorEngineer}/role", UriKind.Relative))
        {
            Content = JsonContent.Create(new { role }),
        };

        request.Headers.Add(TestAuthHandler.UserIdHeader, _owner.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await StaleSession(_owner));
        request.Headers.Add(TestAuthHandler.RoleHeader, nameof(Role.Owner));

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.IsSuccessStatusCode.Should().BeFalse(
            "{0} is not one of the nine roles spec.md §9 names, and an account holding it is refused "
            + "everything by PermissionEvaluator while still being admitted through every predicate "
            + "written as a deny-list",
            role);

        (await ActorRoleAsync(_supervisorEngineer)).Should().Be(
            Role.SiteEngineer,
            "a refused change is not a change — and an audit row this account later authored would "
            + "carry actor_role = '{0}' into an append-only table where it can never be corrected",
            role);
    }

    // ---- helpers -------------------------------------------------------------------------------------

    private async Task<string> StaleSession(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .SingleAsync(Ct);
    }

    private async Task<HttpResponseMessage> ChangeRoleAsync(Guid actorId, Guid targetUserId, Role role)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            new Uri($"/api/users/{targetUserId}/role", UriKind.Relative))
        {
            Content = JsonContent.Create(new { role = role.ToString() }),
        };

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await StaleSession(actorId));

        Role? actorRole = await ActorRoleAsync(actorId);

        if (actorRole is not null)
        {
            request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.Value.ToString());
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<Role?> ActorRoleAsync(Guid actorId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == actorId)
            .Select(user => (Role?)user.Role)
            .FirstOrDefaultAsync(Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(Uri route, Guid userId, string securityStamp)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        request.Headers.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, securityStamp);

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key)
            ? key.GetString()
            : null;
    }

    private static async Task<Response> ReadResponseAsync(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        JsonElement root = body.RootElement;

        return new Response(
            root.GetProperty("userId").GetGuid(),
            Enum.Parse<Role>(root.GetProperty("role").GetString()!),
            root.GetProperty("revokedProjectIds").EnumerateArray().Select(element => element.GetGuid()).ToArray());
    }

    private async Task<ProjectAssignment> AssignmentAsync(Guid userId, Guid projectId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.ProjectAssignments
            .SingleAsync(assignment => assignment.UserId == userId && assignment.ProjectId == projectId, Ct);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("CRR-C1"), "عميل تغيير الدور", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project projectA = Project.Create(
            UniqueNames.Code("CRR-PA"), "مشروع أ", client.Id, ContractType.LumpSum, Now).Value;
        Project projectB = Project.Create(
            UniqueNames.Code("CRR-PB"), "مشروع ب", client.Id, ContractType.LumpSum, Now).Value;
        Project projectC = Project.Create(
            UniqueNames.Code("CRR-PC"), "مشروع ج", client.Id, ContractType.LumpSum, Now).Value;

        User owner = MakeUser("crr-owner", Role.Owner);
        User hr = MakeUser("crr-hr", Role.Hr, Department.Hr);
        User financeProbe = MakeUser("crr-finance", Role.Finance, Department.Finance);
        User supervisorEngineer = MakeUser(
            "crr-supervisor", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User juniorEngineer = MakeUser(
            "crr-junior", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User mirrorFinance = MakeUser("crr-mirror", Role.Finance, Department.Finance);
        User marketingUser = MakeUser("crr-marketing", Role.MarketingSales, Department.Marketing);

        // V-26-A's two targets. Both are departmentless Role.Owner accounts — the shape KAFF-100's
        // setup screen mints, and the one that reaches ck_users_subcontractor_cannot_log_in — and they
        // differ in exactly one thing: whether a credential is stored.
        User credentialedOwner = MakeUser("crr-spare-owner", Role.Owner);
        User recordlessOwner = MakeUser("crr-recordless-owner", Role.Owner, holdsCredential: false);

        context.Clients.Add(client);
        context.Projects.AddRange(projectA, projectB, projectC);
        context.Users.AddRange(
            owner, hr, financeProbe, supervisorEngineer, juniorEngineer, mirrorFinance, marketingUser,
            credentialedOwner, recordlessOwner);

        await context.SaveChangesAsync(Ct);

        context.ProjectAssignments.AddRange(
            ProjectAssignment.Create(projectA.Id, financeProbe, AssignmentLevel.Standard, owner.Id, Now).Value,
            ProjectAssignment.Create(projectA.Id, supervisorEngineer, AssignmentLevel.Supervisor, owner.Id, Now).Value,
            ProjectAssignment.Create(projectA.Id, juniorEngineer, AssignmentLevel.Junior, owner.Id, Now).Value,
            ProjectAssignment.Create(projectB.Id, juniorEngineer, AssignmentLevel.Junior, owner.Id, Now).Value,
            ProjectAssignment.Create(projectC.Id, juniorEngineer, AssignmentLevel.Junior, owner.Id, Now).Value,
            ProjectAssignment.Create(projectA.Id, mirrorFinance, AssignmentLevel.Standard, owner.Id, Now).Value,
            ProjectAssignment.Create(projectB.Id, mirrorFinance, AssignmentLevel.Standard, owner.Id, Now).Value,
            ProjectAssignment.Create(projectA.Id, marketingUser, AssignmentLevel.Standard, owner.Id, Now).Value,

            // Two rows on the credentialed owner, so the refusal below is also the AC-109-K shape:
            // the request that used to fail at the database, with the role change and both revocations
            // in the change tracker together, now fails before the revocation loop starts.
            ProjectAssignment.Create(projectA.Id, credentialedOwner, AssignmentLevel.Standard, owner.Id, Now).Value,
            ProjectAssignment.Create(projectB.Id, credentialedOwner, AssignmentLevel.Standard, owner.Id, Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectA = projectA.Id;
        _projectB = projectB.Id;
        _projectC = projectC.Id;
        _owner = owner.Id;
        _hr = hr.Id;
        _financeProbe = financeProbe.Id;
        _supervisorEngineer = supervisorEngineer.Id;
        _juniorEngineer = juniorEngineer.Id;
        _mirrorFinance = mirrorFinance.Id;
        _marketingUser = marketingUser.Id;
        _credentialedOwner = credentialedOwner.Id;
        _recordlessOwner = recordlessOwner.Id;
    }

    /// <summary>
    /// A seeded user, <b>holding a credential</b> unless the caller asks for one without.
    /// </summary>
    /// <remarks>
    /// <b>The credential is the point, not decoration.</b> This helper built every user through
    /// <c>User.Create</c> alone, so no seeded row had a <c>PasswordHash</c> and
    /// <c>ck_users_subcontractor_cannot_log_in</c> — <c>role &lt;&gt; 'Subcontractor' OR password_hash
    /// IS NULL</c> — was satisfied vacuously by every case in this file at once. The suite was green
    /// and <c>PUT /api/users/{userId}/role</c> answered <c>500</c> to a request the Owner can make on
    /// his own account (qa/slice-1/verification-2026-08-26.md, <c>V-26-A</c>). A test that cannot fail
    /// is worse than no test; these rows now carry what a real staff account carries.
    /// <para>
    /// The value is a literal rather than a <c>PasswordHasher.Hash</c> call because nothing in this
    /// file verifies it — every request here authenticates through <see cref="TestAuthHandler"/> — and
    /// paying 600,000 PBKDF2 iterations per seeded user for a string nobody reads would be a cost with
    /// no assertion behind it.
    /// </para>
    /// </remarks>
    private static User MakeUser(
        string userName,
        Role role,
        Department? department = null,
        OperationsSubDepartment? subDepartment = null,
        bool holdsCredential = true)
    {
        User user = User.Create(
            UniqueNames.Code(userName),
            userName,
            UniqueNames.Phone(),
            role,
            Now,
            department,
            subDepartment).Value;

        if (holdsCredential)
        {
            user.SetOwnPassword(SeededCredential).IsSuccess.Should().BeTrue();
        }

        return user;
    }

    /// <summary>What a seeded staff account stores in <c>PasswordHash</c>. See <see cref="MakeUser"/>.</summary>
    private const string SeededCredential = "seeded-credential-not-verified-by-these-tests";

    /// <summary>The shape <c>ChangeUserRole.Response</c> carries, read back from JSON in these tests.</summary>
    private sealed record Response(Guid UserId, Role Role, IReadOnlyList<Guid> RevokedProjectIds);

    private static DateTimeOffset Now => new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
