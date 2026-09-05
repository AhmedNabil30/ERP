using System.Net;
using System.Net.Http;
using System.Text.Json;
using Kaff.Api.Features.Users.ListUsers;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-127 — <c>GET /api/users</c>, the read the five identity endpoints shipped without.
/// </summary>
/// <remarks>
/// <para>
/// <b>The census is complete on purpose.</b> <c>V-33-A</c> found <c>Role.HeadOfDesign</c> asserted
/// against no endpoint anywhere in the repository, and <c>V-33-B</c> found the per-endpoint coverage
/// uneven. Every role that can hold a staff session is refused here by name, and the portal
/// <c>Role.Client</c> with it — <c>Role.Subcontractor</c> is the one omission, and it is spec.md §9's:
/// "record only, no login."
/// </para>
/// <para>
/// This endpoint returns <b>every account in Kaff</b> with its username, department and active state.
/// It is the widest identity payload in the system, so the refusals matter more here than the success
/// case does.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class ListUsersTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _finance;
    private Guid _technicalOffice;
    private Guid _siteEngineer;
    private Guid _headOfDesign;
    private Guid _marketing;
    private Guid _hr;
    private Guid _portalClient;
    private Guid _portalClientCompany;

    private Guid _staffedEngineer;
    private string _staffedEngineerName = null!;
    private string _projectAName = null!;
    private string _projectBName = null!;
    private string _revokedProjectName = null!;

    public ListUsersTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-127-A's contract half · the Owner can read the list -------------------------------

    /// <summary>
    /// <c>AC-127-A</c> is a rendering criterion and is discharged by <c>tests/E2E.Tests</c>. What this
    /// pins is the contract that screen renders from — a <c>200</c> carrying an array, never a null
    /// the component has to guard, and every account in it whatever its state.
    /// </summary>
    [Fact]
    public async Task The_owner_reads_every_account_active_and_inactive_alike()
    {
        IReadOnlyList<JsonElement> users = await ListAsync(_owner, Role.Owner);

        users.Select(user => user.GetProperty("id").GetGuid()).Should().Contain(
            [_owner, _finance, _hr, _portalClient],
            "D-049 ruling 5 — leavers are deactivated and never deleted, so a list that filtered by "
            + "state would hide the only accounts ReactivateUser can act on");
    }

    // ---- AC-127-D and AC-127-E · the consequence is stated before the act ----------------------

    /// <summary>
    /// The count and the names a deactivation or a role change would revoke, from the server.
    /// </summary>
    /// <remarks>
    /// <c>ux/slice-1-flows.md</c> S-008: <i>"The count and the names come from the server … Do not
    /// compute them in the client from an assignment list and do not guess the number."</i> A revoked
    /// row must not appear, because the confirmation says what <b>would</b> be revoked now, not what
    /// once was.
    /// </remarks>
    [Fact]
    public async Task The_row_names_the_active_assignments_a_role_change_or_deactivation_would_revoke()
    {
        JsonElement engineer = await FindAsync(_owner, Role.Owner, _staffedEngineer);

        IReadOnlyList<string> names =
        [
            .. engineer.GetProperty("activeProjectNames").EnumerateArray()
                .Select(element => element.GetString()!),
        ];

        names.Should().BeEquivalentTo(
            [_projectAName, _projectBName],
            "AC-127-D asks for the number and AC-127-E for the assignments, both BEFORE the act — a "
            + "screen cannot state a consequence the payload does not carry");

        names.Should().NotContain(
            _revokedProjectName,
            "an already-revoked row would inflate the count the Owner is asked to accept");
    }

    [Fact]
    public async Task A_user_holding_no_assignment_carries_an_empty_list_and_not_a_null()
    {
        JsonElement financeUser = await FindAsync(_owner, Role.Owner, _finance);

        JsonElement names = financeUser.GetProperty("activeProjectNames");

        names.ValueKind.Should().Be(
            JsonValueKind.Array,
            "S-008 omits the revocation line rather than rendering \"0 projects\", and a component "
            + "cannot branch on a field that is sometimes absent");

        names.GetArrayLength().Should().Be(0);
    }

    // ---- AC-127-G's server half · one test per role, asserting what it cannot reach -------------

    /// <summary>
    /// <c>UserManage</c> is <c>CompanyWide</c> and granted to <c>Role.Owner</c> alone (D-044 ruling 1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>Role.Hr</c> is refused, and that is spec.md's ruling rather than an oversight.</b> HR
    /// holds <c>Permission.UserRead</c> — but the 2026-08-22 amendment (D-055 §3) is explicit that the
    /// grant is <i>"names and roles only"</i> and <i>"does not hand HR the Owner's user administration
    /// surface — usernames, departments and active state for every account"</i>. This payload is that
    /// surface. HR's narrow list is a screen nobody has cut, and <c>UserRead</c> still has no endpoint.
    /// </para>
    /// <para>
    /// <c>Role.HeadOfDesign</c> is here because of <c>V-33-A</c>: it was asserted against no endpoint
    /// anywhere in the repository, so a catalogue edit granting it anything went unnoticed by a green
    /// suite.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Every_role_but_the_owner_is_refused_and_no_username_reaches_the_body()
    {
        (Guid Actor, Role Role, Department? Department, OperationsSubDepartment? Sub, Guid? Client)[] refused =
        [
            (_finance, Role.Finance, Department.Finance, null, null),
            (_technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical, null),
            (_siteEngineer, Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical, null),
            (_headOfDesign, Role.HeadOfDesign, null, null, null),
            (_marketing, Role.MarketingSales, Department.Marketing, null, null),
            (_hr, Role.Hr, Department.Hr, null, null),
            (_portalClient, Role.Client, null, null, _portalClientCompany),
        ];

        foreach ((Guid actor, Role role, Department? department, OperationsSubDepartment? sub, Guid? client) in refused)
        {
            HttpResponseMessage response = await SendAsync(actor, role, department, sub, client);

            response.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                "{0} does not hold UserManage — spec.md §9, only the Owner creates and administers users",
                role);

            (await response.Content.ReadAsStringAsync(Ct)).Should().NotContain(
                _staffedEngineerName,
                "a refusal that still names an account has refused nothing that matters");
        }
    }

    // ---- no credential, and no money, ever ----------------------------------------------------

    /// <summary>
    /// The row's member set, pinned by a whitelist rather than a search for suspect words.
    /// </summary>
    /// <remarks>
    /// <b>D-106's lesson, and D-114 §1's.</b> A blocklist answers about the words on it and says
    /// nothing about the field nobody predicted — that is how a <c>decimal RetainedAmount</c> reached
    /// the wire past a green suite. This is the only payload in the system projected directly from
    /// <see cref="User"/>, which carries <c>PasswordHash</c> and <c>SecurityStamp</c>, so the
    /// narrowing is the whole guarantee. Any added member fails this, whatever it is called.
    /// </remarks>
    [Fact]
    public void The_user_row_carries_exactly_these_members_and_no_credential()
    {
        typeof(UserSummary).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(
                [
                    "Id",
                    "UserName",
                    "FullName",
                    "Phone",
                    "Role",
                    "Department",
                    "OperationsSubDepartment",
                    "IsActive",
                    "ActiveProjectNames",
                ]);

        typeof(Response).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["Users"], "the wrapper carries the list and nothing else");
    }

    /// <summary>
    /// A credential must not reach the wire even under a member name that does not look like one.
    /// </summary>
    /// <remarks>
    /// The whitelist above is the structural guarantee; this drives the real serialiser against a real
    /// account holding a real hash, because a member set is a claim about the type and this is a claim
    /// about the bytes. <c>D-050</c>: the token never reaches JavaScript, and neither does the hash
    /// that mints it.
    /// </remarks>
    [Fact]
    public async Task No_password_hash_or_security_stamp_reaches_the_wire()
    {
        HttpResponseMessage response = await SendAsync(_owner, Role.Owner, null, null, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync(Ct);

        await using KaffDbContext reader = _database.CreateBareContext();

        var secrets = await reader.Users
            .Where(user => user.Id == _owner)
            .Select(user => new { user.PasswordHash, user.SecurityStamp })
            .SingleAsync(Ct);

        secrets.SecurityStamp.Should().NotBeNullOrWhiteSpace("the fixture must actually hold one");

        body.Should().NotContain(secrets.SecurityStamp);

        // The middles, so the assertion holds whichever casing the serialiser is configured for.
        body.Should().NotContain("asswordHash");
        body.Should().NotContain("ecurityStamp");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private async Task<JsonElement> FindAsync(Guid actorId, Role actorRole, Guid userId)
    {
        IReadOnlyList<JsonElement> users = await ListAsync(actorId, actorRole);

        return users.Single(user => user.GetProperty("id").GetGuid() == userId);
    }

    private async Task<IReadOnlyList<JsonElement>> ListAsync(Guid actorId, Role actorRole)
    {
        HttpResponseMessage response = await SendAsync(actorId, actorRole, null, null, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Cloned: the JsonDocument is disposed before the caller reads the elements.
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return [.. body.RootElement.GetProperty("users").EnumerateArray().Select(user => user.Clone())];
    }

    private async Task<HttpResponseMessage> SendAsync(
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        OperationsSubDepartment? actorSubDepartment,
        Guid? actorClientId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/users", UriKind.Relative));

        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        if (actorDepartment is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, actorDepartment.Value.ToString());
        }

        if (actorSubDepartment is not null)
        {
            request.Headers.Add(TestAuthHandler.SubDepartmentHeader, actorSubDepartment.Value.ToString());
        }

        if (actorClientId is not null)
        {
            request.Headers.Add(TestAuthHandler.ClientIdHeader, actorClientId.Value.ToString());
        }

        return await _client.SendAsync(request, Ct);
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

        Client company = Client.Create(
            UniqueNames.Code("LSU-C1"),
            "عميل بوابة قائمة المستخدمين",
            UniqueNames.Phone(),
            ClientKind.Corporate,
            Now).Value;

        _projectAName = "مشروع " + UniqueNames.Code("LSU-PA");
        _projectBName = "مشروع " + UniqueNames.Code("LSU-PB");
        _revokedProjectName = "مشروع " + UniqueNames.Code("LSU-PR");

        Project projectA = Project.Create(
            UniqueNames.Code("LSU-PA"), _projectAName, company.Id, ContractType.LumpSum, Now).Value;
        Project projectB = Project.Create(
            UniqueNames.Code("LSU-PB"), _projectBName, company.Id, ContractType.LumpSum, Now).Value;
        Project revoked = Project.Create(
            UniqueNames.Code("LSU-PR"), _revokedProjectName, company.Id, ContractType.LumpSum, Now).Value;

        User owner = MakeUser("lsu-owner", Role.Owner);
        User finance = MakeUser("lsu-finance", Role.Finance, Department.Finance);
        User technicalOffice = MakeUser(
            "lsu-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User siteEngineer = MakeUser(
            "lsu-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User headOfDesign = MakeUser("lsu-design", Role.HeadOfDesign);
        User marketing = MakeUser("lsu-marketing", Role.MarketingSales, Department.Marketing);
        User hr = MakeUser("lsu-hr", Role.Hr, Department.Hr);
        User portal = MakeUser("lsu-portal", Role.Client, clientId: company.Id);

        User staffed = MakeUser(
            "lsu-staffed", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);

        // Every account the refusal test stamps needs a security stamp the handler will accept, and
        // the hash is what makes No_password_hash_or_security_stamp_reaches_the_wire non-vacuous.
        owner.SetOwnPassword("not-a-real-hash-lsu").IsSuccess.Should().BeTrue();

        context.Clients.Add(company);
        context.Projects.AddRange(projectA, projectB, revoked);
        context.Users.AddRange(
            owner, finance, technicalOffice, siteEngineer, headOfDesign, marketing, hr, portal, staffed);

        ProjectAssignment revokedRow =
            ProjectAssignment.Create(revoked.Id, staffed, AssignmentLevel.Junior, owner.Id, Now).Value;

        revokedRow.Revoke(owner.Id, Now).IsSuccess.Should().BeTrue();

        context.ProjectAssignments.AddRange(
            ProjectAssignment.Create(projectA.Id, staffed, AssignmentLevel.Junior, owner.Id, Now).Value,
            ProjectAssignment.Create(projectB.Id, staffed, AssignmentLevel.Supervisor, owner.Id, Now).Value,
            revokedRow);

        await context.SaveChangesAsync(Ct);

        _portalClientCompany = company.Id;
        _owner = owner.Id;
        _finance = finance.Id;
        _technicalOffice = technicalOffice.Id;
        _siteEngineer = siteEngineer.Id;
        _headOfDesign = headOfDesign.Id;
        _marketing = marketing.Id;
        _hr = hr.Id;
        _portalClient = portal.Id;
        _staffedEngineer = staffed.Id;
        _staffedEngineerName = staffed.UserName;
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

    private static DateTimeOffset Now => new(2026, 9, 5, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
