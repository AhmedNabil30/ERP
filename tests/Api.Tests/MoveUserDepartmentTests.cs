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
/// KAFF-108 — <c>PUT /api/users/{userId}/department</c>, the Owner moves someone between departments.
/// </summary>
/// <remarks>
/// <para>
/// Every test here goes through the HTTP endpoint. The department rules themselves are already pinned
/// in <c>Domain.Tests</c>; what cannot be seen from there is the level above — that the handler routes
/// through <c>User.MoveToDepartment</c> and returns its refusal, and that the gate re-reads the moved
/// user's authority on their next request rather than trusting the token they already hold.
/// </para>
/// <para>
/// spec.md §9: "Enforcement is server-side; hiding UI elements is presentation, not security."
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class MoveUserDepartmentTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _projectId;
    private Guid _otherProjectId;
    private Guid _owner;
    private Guid _technicalOffice;
    private Guid _technicalOfficeAdmin;
    private Guid _siteEngineer;
    private Guid _finance;
    private Guid _hr;
    private Guid _portalClient;

    public MoveUserDepartmentTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-108-A · a move takes effect on the next request -----------------------------------

    /// <summary>
    /// AC-108-A. A <c>Role.TechnicalOffice</c> user moved into Operations / Administrative reaches
    /// <c>SiteExpenseConfirm</c> on their next request, holding the token they already had.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The stamp is captured before the move and replayed after it.</b> That is what makes "the
    /// same token" real rather than assumed: <c>SendAsync</c> would otherwise read the current stamp,
    /// and the test would pass even if the move had rotated it and forced a re-authentication. This is
    /// D-048 — the token supplies the user id and the database supplies the authority
    /// [Verified: 2026-08-23 @ <c>PermissionAuthorizationHandler.cs</c> -&gt; <c>BuildSubjectAsync</c>].
    /// </para>
    /// <para>
    /// The role is load-bearing and the criterion says so: the grant names <c>Role.TechnicalOffice</c>
    /// <b>and</b> the sub-department, and a criterion written without the role would assert the F-04
    /// leak as correct behaviour (D-052 §1, D-053 §2). <see cref="A_site_engineer_gains_nothing_from_the_same_move"/>
    /// is the other side of it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_move_changes_what_the_next_request_reaches_without_a_new_token()
    {
        string tokenIssuedBeforeTheMove = await CurrentStampAsync(_technicalOffice);

        (await SendAsync(SiteExpense(_projectId), _technicalOffice, tokenIssuedBeforeTheMove))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "Operations / Technical is not the sub-department SiteExpenseConfirm names");

        (await MoveAsync(_owner, _technicalOffice, "Operations", "Administrative"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SendAsync(SiteExpense(_projectId), _technicalOffice, tokenIssuedBeforeTheMove))
            .StatusCode.Should().Be(
                HttpStatusCode.OK,
                "permissions come from the database on every request, not from the token");
    }

    /// <summary>AC-108-B — and the reverse takes effect just as fast, on the same token.</summary>
    [Fact]
    public async Task The_reverse_move_takes_effect_on_the_next_request_too()
    {
        string tokenIssuedBeforeTheMove = await CurrentStampAsync(_technicalOfficeAdmin);

        (await SendAsync(SiteExpense(_projectId), _technicalOfficeAdmin, tokenIssuedBeforeTheMove))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await MoveAsync(_owner, _technicalOfficeAdmin, "Marketing", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SendAsync(SiteExpense(_projectId), _technicalOfficeAdmin, tokenIssuedBeforeTheMove))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "authority removed by a move is gone on the next request, not at token expiry");
    }

    // ---- AC-108-G · the department alone is never enough on money -----------------------------

    /// <summary>
    /// AC-108-G. The same move that grants <c>SiteExpenseConfirm</c> to a Technical Office user
    /// grants a Site Engineer nothing.
    /// </summary>
    /// <remarks>
    /// spec.md §8: site expenses are entered "by Finance or Admin, <b>not the engineer</b>". The
    /// domain half is pinned by <c>A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense</c>
    /// [Verified: 2026-08-23 @ <c>PermissionEvaluatorTests.cs</c> -&gt;
    /// <c>A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense</c>];
    /// what this adds is the same assertion at the endpoint, after a real move.
    /// </remarks>
    [Fact]
    public async Task A_site_engineer_gains_nothing_from_the_same_move()
    {
        (await MoveAsync(_owner, _siteEngineer, "Operations", "Administrative"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ReadUserAsync(_siteEngineer)).OperationsSubDepartment
            .Should().Be(OperationsSubDepartment.Administrative, "the move itself is legal");

        (await SendAsync(SiteExpense(_projectId), _siteEngineer))
            .StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "the grant names Role.TechnicalOffice beside the sub-department — finding F-04");
    }

    // ---- AC-108-C · the department rules are re-applied on a move -----------------------------

    [Fact]
    public async Task The_department_rules_are_re_applied_on_a_move()
    {
        HttpResponseMessage withoutSub = await MoveAsync(_owner, _finance, "Operations", null);

        withoutSub.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(withoutSub)).Should().Be("errors.identity.operations_requires_sub_department");

        HttpResponseMessage withSub = await MoveAsync(_owner, _finance, "Marketing", "Administrative");

        withSub.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(withSub)).Should().Be("errors.identity.sub_department_only_for_operations");

        (await ReadUserAsync(_finance)).Department.Should().Be(
            Department.Finance, "neither refusal moved anybody");
    }

    // ---- AC-108-D · HR stays in HR ------------------------------------------------------------

    /// <summary>
    /// AC-108-D. The move-path half of <c>AC-106-K</c>, refused at the endpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The domain guard exists and is pinned in <c>Domain.Tests</c>
    /// [Verified: 2026-08-23 @ <c>User.cs</c> -&gt; <c>ValidateDepartment</c>;
    /// @ <c>CatalogueCompletenessTests.cs</c> -&gt; <c>An_hr_user_cannot_be_placed_in_another_department</c>].
    /// <b>This test is not a second copy of it.</b> What it holds is the level above: that the handler
    /// routes through <c>MoveToDepartment</c> and returns its refusal, rather than correcting the
    /// department on the way past — which is what a helpful handler does, compiles cleanly, keeps the
    /// domain test green, and moves nobody while reporting success. D-066 §2 recorded exactly that
    /// mutation on the create path; SM-21 made the KAFF-107 fold conditional on both halves existing.
    /// </para>
    /// <para>
    /// All four destinations, because the guard is "must be <c>Department.Hr</c>" rather than "must
    /// not be Finance" — including <c>null</c>, which is the reading a narrower guard would let
    /// through.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task An_hr_user_cannot_be_moved_out_of_hr_at_the_endpoint()
    {
        (string? Department, string? Sub)[] wrongDestinations =
        [
            ("Finance", null),
            ("Marketing", null),
            ("Operations", "Administrative"),
            (null, null),
        ];

        foreach ((string? department, string? sub) in wrongDestinations)
        {
            HttpResponseMessage response = await MoveAsync(_owner, _hr, department, sub);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            (await MessageKeyAsync(response)).Should().Be(
                "errors.identity.hr_role_requires_hr_department",
                $"an HR user in {department ?? "no department"} inherits that department's grants");

            User unmoved = await ReadUserAsync(_hr);

            unmoved.Department.Should().Be(Department.Hr, "and the department is unchanged");
            unmoved.OperationsSubDepartment.Should().BeNull();
        }
    }

    /// <summary>
    /// AC-108-D's second half. The constraint must not be "HR may hold no department", which would
    /// make HR unmovable even to where it already is.
    /// </summary>
    [Fact]
    public async Task An_hr_user_may_be_moved_within_hr()
    {
        (await MoveAsync(_owner, _hr, "Hr", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ReadUserAsync(_hr)).Department.Should().Be(Department.Hr);
    }

    /// <summary>
    /// KAFF-108 rule 4 — an external role cannot be given a department by a move any more than by
    /// creation (spec.md §12, decisions.md D-035).
    /// </summary>
    /// <remarks>
    /// A department-only grant matches any role carrying that department, so a portal client parked
    /// in a department would inherit company-wide permissions that skip both the project check and
    /// the client check.
    /// </remarks>
    [Fact]
    public async Task An_external_role_cannot_be_moved_into_a_department()
    {
        HttpResponseMessage response = await MoveAsync(_owner, _portalClient, "Marketing", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.external_role_cannot_hold_department");
        (await ReadUserAsync(_portalClient)).Department.Should().BeNull();
    }

    // ---- AC-108-E · nobody but the Owner can move anyone --------------------------------------

    /// <summary>
    /// AC-108-E. HR is in this list for the reason KAFF-108 rule 7 gives: whoever sets a department
    /// can grant capability without touching a role, which is why the act is <c>UserManage</c> and
    /// not <c>ProjectAssignmentManage</c> (D-044 ruling 1).
    /// </summary>
    [Fact]
    public async Task Nobody_but_the_owner_can_move_a_user_between_departments()
    {
        Guid[] callers = [_hr, _finance, _technicalOffice];

        foreach (Guid caller in callers)
        {
            HttpResponseMessage response = await MoveAsync(caller, _siteEngineer, "Marketing", null);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        User untouched = await ReadUserAsync(_siteEngineer);

        untouched.Department.Should().Be(Department.Operations, "no refused attempt moved anybody");
        untouched.OperationsSubDepartment.Should().Be(OperationsSubDepartment.Technical);
    }

    // ---- AC-108-F · assignments survive the move ----------------------------------------------

    [Fact]
    public async Task Assignments_survive_the_move()
    {
        (await MoveAsync(_owner, _technicalOffice, "Marketing", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();

        List<ProjectAssignment> assignments = await reader.ProjectAssignments
            .Where(assignment => assignment.UserId == _technicalOffice)
            .ToListAsync(Ct);

        assignments.Should().HaveCount(2);
        assignments.Should().AllSatisfy(assignment => assignment.IsActive.Should().BeTrue());
        assignments.Select(assignment => assignment.ProjectId)
            .Should().BeEquivalentTo(new[] { _projectId, _otherProjectId });
    }

    // ---- the audit half -----------------------------------------------------------------------

    /// <summary>
    /// The story's audit bullet: <c>Modified</c> on <c>User</c>, actor the Owner, before and after
    /// both carrying department and sub-department, <c>ChangedProperties</c> naming them.
    /// </summary>
    /// <remarks>
    /// No handler writes this. The move is an entity change, so
    /// <c>AuditSaveChangesInterceptor</c> writes it in the same transaction — decisions.md D-031 and
    /// KAFF-118 rule 2 forbid the hand-written alternative.
    /// </remarks>
    [Fact]
    public async Task The_move_leaves_an_audit_record_naming_the_owner_and_both_departments()
    {
        // Finance → Operations / Financial, because it changes BOTH columns. A move within
        // Operations changes one, and the interceptor correctly names only that one — which would
        // make this test assert less than the story's audit bullet describes.
        (await MoveAsync(_owner, _finance, "Operations", "Financial"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords.SingleAsync(
            candidate => candidate.EntityId == _finance && candidate.Action == AuditAction.Modified,
            Ct);

        record.EntityType.Should().Be(nameof(User));
        record.ActorUserId.Should().Be(_owner);
        record.ActorRole.Should().Be(Role.Owner);

        record.GrantPath.Should().BeNull(
            "UserManage is company-wide: no project, no access policy, no path to name");

        record.ChangedProperties.Should().BeEquivalentTo(
            new[] { nameof(User.Department), nameof(User.OperationsSubDepartment) });

        using JsonDocument before = JsonDocument.Parse(record.BeforeJson!);
        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        before.RootElement.GetProperty(nameof(User.Department)).GetString()
            .Should().Be(nameof(Department.Finance));

        before.RootElement.GetProperty(nameof(User.OperationsSubDepartment)).ValueKind
            .Should().Be(JsonValueKind.Null);

        after.RootElement.GetProperty(nameof(User.Department)).GetString()
            .Should().Be(nameof(Department.Operations));

        after.RootElement.GetProperty(nameof(User.OperationsSubDepartment)).GetString()
            .Should().Be(nameof(OperationsSubDepartment.Financial));
    }

    /// <summary>A route naming a user that does not exist is a 404 the client can translate.</summary>
    /// <remarks>
    /// KAFF-108 names no such refusal; an endpoint addressing a user by id has to answer something.
    /// See decisions.md D-067.
    /// </remarks>
    [Fact]
    public async Task Moving_a_user_who_does_not_exist_is_refused()
    {
        HttpResponseMessage response = await MoveAsync(_owner, Guid.CreateVersion7(), "Marketing", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.user_not_found");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static Uri SiteExpense(Guid projectId) =>
        new($"/probe/projects/{projectId}/site-expense-confirm", UriKind.Relative);

    /// <summary>
    /// Issues the move as <paramref name="actorId"/>.
    /// </summary>
    /// <remarks>
    /// The department and sub-department are written as strings rather than as enum values, because a
    /// test that serialises with the same converter the server deserialises with would pass on a
    /// numeric wire form too.
    /// </remarks>
    private async Task<HttpResponseMessage> MoveAsync(
        Guid actorId,
        Guid targetUserId,
        string? department,
        string? subDepartment)
    {
        User? actor = await FindUserAsync(actorId);

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            new Uri($"/api/users/{targetUserId}/department", UriKind.Relative))
        {
            Content = JsonContent.Create(new { department, operationsSubDepartment = subDepartment }),
        };

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, (actor?.Role ?? Role.Owner).ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, actor?.SecurityStamp ?? "no-such-user");

        if (actor?.Department is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, actor.Department.Value.ToString());
        }

        if (actor?.OperationsSubDepartment is not null)
        {
            request.Headers.Add(
                TestAuthHandler.SubDepartmentHeader, actor.OperationsSubDepartment.Value.ToString());
        }

        return await _client.SendAsync(request, Ct);
    }

    /// <summary>
    /// Issues a GET as <paramref name="userId"/>, optionally with a stamp captured earlier.
    /// </summary>
    /// <remarks>
    /// The role, department and sub-department headers are deliberately <b>not</b> sent. The gate
    /// reads all three from the database (D-048), so omitting them proves the answer came from there
    /// — a test that supplied fresh headers after a move would pass on a token-driven gate too.
    /// </remarks>
    private async Task<HttpResponseMessage> SendAsync(Uri route, Guid userId, string? securityStamp = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);

        request.Headers.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        request.Headers.Add(
            TestAuthHandler.SecurityStampHeader,
            securityStamp ?? await CurrentStampAsync(userId));

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key)
            ? key.GetString()
            : null;
    }

    private async Task<User> ReadUserAsync(Guid userId) =>
        await FindUserAsync(userId) ?? throw new InvalidOperationException($"No user {userId}.");

    private async Task<User?> FindUserAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users.FirstOrDefaultAsync(user => user.Id == userId, Ct);
    }

    private async Task<string> CurrentStampAsync(Guid userId) =>
        (await FindUserAsync(userId))?.SecurityStamp ?? "no-such-user";

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("MOV-C1"), "عميل نقل الأقسام", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project project = Project.Create(
            UniqueNames.Code("MOV-P1"), "مشروع", client.Id, ContractType.LumpSum, Now).Value;

        Project otherProject = Project.Create(
            UniqueNames.Code("MOV-P2"), "مشروع آخر", client.Id, ContractType.LumpSum, Now).Value;

        User owner = MakeUser("mov-owner", Role.Owner);
        User technicalOffice = MakeUser(
            "mov-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User technicalOfficeAdmin = MakeUser(
            "mov-tech-admin", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Administrative);
        User siteEngineer = MakeUser(
            "mov-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User finance = MakeUser("mov-finance", Role.Finance, Department.Finance);
        User hr = MakeUser("mov-hr", Role.Hr, Department.Hr);
        User portal = MakeUser("mov-portal", Role.Client, clientId: client.Id);

        context.Clients.Add(client);
        context.Projects.AddRange(project, otherProject);
        context.Users.AddRange(owner, technicalOffice, technicalOfficeAdmin, siteEngineer, finance, hr, portal);

        await context.SaveChangesAsync(Ct);

        // Two projects for the Technical Office user, which is what AC-108-F counts; the other three
        // are assigned to the first project so their refusals are about the permission and not about
        // a missing assignment.
        context.ProjectAssignments.AddRange(
            ProjectAssignment.Create(project.Id, technicalOffice, AssignmentLevel.Standard, owner.Id, Now).Value,
            ProjectAssignment.Create(otherProject.Id, technicalOffice, AssignmentLevel.Standard, owner.Id, Now).Value,
            ProjectAssignment.Create(project.Id, technicalOfficeAdmin, AssignmentLevel.Standard, owner.Id, Now).Value,
            ProjectAssignment.Create(project.Id, siteEngineer, AssignmentLevel.Supervisor, owner.Id, Now).Value);

        await context.SaveChangesAsync(Ct);

        _projectId = project.Id;
        _otherProjectId = otherProject.Id;
        _owner = owner.Id;
        _technicalOffice = technicalOffice.Id;
        _technicalOfficeAdmin = technicalOfficeAdmin.Id;
        _siteEngineer = siteEngineer.Id;
        _finance = finance.Id;
        _hr = hr.Id;
        _portalClient = portal.Id;
    }

    private static User MakeUser(
        string userName,
        Role role,
        Department? department = null,
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
