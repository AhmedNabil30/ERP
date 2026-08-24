using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-106 — <c>POST /api/users</c>, the Owner creates a user with a role and a department.
/// </summary>
/// <remarks>
/// <para>
/// Every test here goes through the HTTP endpoint, never through <c>User.Create</c> directly. That
/// is the point of the file: the domain guards are already pinned in <c>Domain.Tests</c>, and the
/// level they can be bypassed at is a handler that never calls <c>Create</c> — see
/// <c>AC-106-K</c>'s own wording, and <see cref="An_hr_user_cannot_be_created_outside_hr_at_the_endpoint"/>.
/// </para>
/// <para>
/// spec.md §9: "Enforcement is server-side; hiding UI elements is presentation, not security."
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class CreateUserTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _finance;
    private Guid _technicalOffice;
    private Guid _siteEngineer;
    private Guid _marketing;
    private Guid _hr;
    private Guid _portalClient;
    private Guid _clientId;

    public CreateUserTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-106-A · the Owner creates a Finance user -----------------------------------------

    [Fact]
    public async Task The_owner_creates_a_finance_user()
    {
        string userName = UniqueNames.Code("ac106a");

        HttpResponseMessage response = await CreateAsync(
            _owner, Role.Owner, null, Body(userName, "Finance", department: "Finance"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        User created = await ReadUserAsync(userName);

        created.Role.Should().Be(Role.Finance);
        created.Department.Should().Be(Department.Finance);
        created.IsActive.Should().BeTrue("User.Create sets IsActive true");
    }

    /// <summary>
    /// D-049 ruling 4 — the Owner sets a temporary password and the user MUST replace it.
    /// </summary>
    /// <remarks>
    /// <c>SetTemporaryPassword</c> and <c>SetOwnPassword</c> differ in exactly one flag, and calling
    /// the wrong one leaves an Owner-chosen credential that never has to be replaced: two people know
    /// the password that acts as that account, permanently, and the trail cannot tell them apart.
    /// Nothing but this flag distinguishes the two calls, which is why it is asserted rather than
    /// assumed.
    /// </remarks>
    [Fact]
    public async Task The_password_the_owner_sets_is_temporary_and_is_not_stored_as_typed()
    {
        string userName = UniqueNames.Code("ac106a-pw");
        const string Password = "temporary-one";

        (await CreateAsync(_owner, Role.Owner, null, Body(userName, "Finance", department: "Finance", password: Password)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        User created = await ReadUserAsync(userName);

        created.MustChangePassword.Should().BeTrue(
            "D-049 ruling 4 — a credential somebody else chose must be replaced on first sign-in");

        created.PasswordHash.Should().NotBeNull();
        created.PasswordHash.Should().NotContain(Password, "the plaintext must never reach the column");
    }

    /// <summary>AC-106-A's audit half, and TC-1-049 — "who gave this person the treasury".</summary>
    [Fact]
    public async Task The_creation_leaves_an_audit_record_naming_the_owner_the_role_and_the_department()
    {
        string userName = UniqueNames.Code("ac106a-audit");

        (await CreateAsync(
                _owner, Role.Owner, null,
                Body(userName, "SiteEngineer", department: "Operations", subDepartment: "Technical", password: "temporary-one")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        User created = await ReadUserAsync(userName);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords.SingleAsync(
            candidate => candidate.EntityId == created.Id && candidate.Action == AuditAction.Created,
            Ct);

        record.EntityType.Should().Be(nameof(User));
        record.ActorUserId.Should().Be(_owner);
        record.ActorRole.Should().Be(Role.Owner);
        record.BeforeJson.Should().BeNull("nothing existed before a creation");

        record.GrantPath.Should().BeNull(
            "UserManage is company-wide: no project, no access policy, no path to name");

        record.AfterJson.Should().NotBeNull();

        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);

        // The snapshot's keys are the EF property names verbatim — the interceptor builds a
        // JsonObject keyed on property.Metadata.Name, and a naming policy does not touch those.
        after.RootElement.GetProperty(nameof(User.Role)).GetString().Should().Be(nameof(Role.SiteEngineer));
        after.RootElement.GetProperty(nameof(User.Department)).GetString().Should().Be(nameof(Department.Operations));

        after.RootElement.GetProperty(nameof(User.PasswordHash)).GetString()
            .Should().Be(AuditRedactedAttribute.Placeholder, "a credential must never enter the trail");

        after.RootElement.GetProperty(nameof(User.SecurityStamp)).GetString()
            .Should().Be(AuditRedactedAttribute.Placeholder);
    }

    // ---- AC-106-B and AC-106-C · nobody else, whatever their role ----------------------------

    /// <summary>
    /// AC-106-B and AC-106-C. Six roles, one refusal each, hit at the endpoint.
    /// </summary>
    /// <remarks>
    /// HR is in the list twice over: it holds <c>ProjectAssignmentManage</c> and is the role most
    /// likely to be handed this by mistake. Whoever can set a user's department can grant
    /// project-assignment power, which is what makes this the most privileged operation in the
    /// system (slice-1 kickoff §2.1, D-044 ruling 1).
    /// </remarks>
    [Fact]
    public async Task Nobody_but_the_owner_can_create_a_user()
    {
        (Guid Actor, Role Role, Department? Department, OperationsSubDepartment? Sub, Guid? Client)[] callers =
        [
            (_finance, Role.Finance, Department.Finance, null, null),
            (_technicalOffice, Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical, null),
            (_siteEngineer, Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical, null),
            (_marketing, Role.MarketingSales, Department.Marketing, null, null),
            (_hr, Role.Hr, Department.Hr, null, null),
            (_portalClient, Role.Client, null, null, _clientId),
        ];

        foreach ((Guid actor, Role role, Department? department, OperationsSubDepartment? sub, Guid? client) in callers)
        {
            string userName = UniqueNames.Code("ac106b");

            HttpResponseMessage response = await CreateAsync(
                actor, role, department, Body(userName, "Finance", department: "Finance"), sub, client);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{role} may not mint logins");

            // AC-106-B is two halves and the status is only the first. A 403 with no key leaves the
            // Arabic UI nothing to render, which is why asserting the status alone reported this
            // criterion as met while it was not (verification-2026-08-23.md, V-A).
            (await MessageKeyAsync(response)).Should().Be(
                "errors.auth.forbidden", $"{role}'s refusal must be renderable");

            (await UserExistsAsync(userName)).Should().BeFalse($"{role}'s attempt created nothing");
        }
    }

    // ---- AC-106-K · an HR user cannot be created outside the HR department --------------------

    /// <summary>
    /// AC-106-K. The four refusals, at the endpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The domain guard exists and is pinned in <c>Domain.Tests</c>
    /// [Verified: 2026-08-23 @ <c>User.cs</c> -> <c>ValidateDepartment</c>;
    /// @ <c>CatalogueCompletenessTests.cs</c> -> <c>An_hr_user_cannot_be_placed_in_another_department</c>].
    /// <b>This test is not a second copy of it.</b> What it holds is the level above: that the
    /// handler routes through <c>Create</c> and returns its refusal, rather than correcting the
    /// department on the way past — which is what a helpful handler does, compiles cleanly, keeps the
    /// domain test green, and creates the account anyway.
    /// </para>
    /// <para>
    /// The mechanism is D-035's, from the other direction: a grant naming a department and no role is
    /// satisfied by any role carrying that department, so an HR user parked in Operations /
    /// Administrative inherits whatever that department holds. Karim, 2026-08-20 — HR is strictly
    /// administrative with zero financial visibility (D-044 ruling 2).
    /// </para>
    /// </remarks>
    [Fact]
    public async Task An_hr_user_cannot_be_created_outside_hr_at_the_endpoint()
    {
        (string? Department, string? Sub)[] wrongPlacements =
        [
            ("Finance", null),
            ("Marketing", null),
            ("Operations", "Administrative"),
            (null, null),
        ];

        foreach ((string? department, string? sub) in wrongPlacements)
        {
            string userName = UniqueNames.Code("ac106k");

            HttpResponseMessage response = await CreateAsync(
                _owner, Role.Owner, null, Body(userName, "Hr", department: department, subDepartment: sub));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            (await MessageKeyAsync(response)).Should().Be(
                "errors.identity.hr_role_requires_hr_department",
                $"an HR user in {department ?? "no department"} inherits that department's grants");

            (await UserExistsAsync(userName)).Should().BeFalse("and no user is created");
        }
    }

    /// <summary>
    /// AC-106-K's second half. The constraint must not be "HR may hold no department", which would
    /// make HR uncreatable — TC-1-060.
    /// </summary>
    [Fact]
    public async Task An_hr_user_in_the_hr_department_is_created_normally()
    {
        string userName = UniqueNames.Code("ac106k-ok");

        (await CreateAsync(_owner, Role.Owner, null, Body(userName, "Hr", department: "Hr")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await ReadUserAsync(userName)).Department.Should().Be(Department.Hr);
    }

    // ---- AC-106-D, E, F · the department, external-role and client rules at the endpoint ------

    [Fact]
    public async Task An_operations_user_must_carry_a_sub_department()
    {
        string userName = UniqueNames.Code("ac106d");

        HttpResponseMessage response = await CreateAsync(
            _owner, Role.Owner, null, Body(userName, "SiteEngineer", department: "Operations"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.operations_requires_sub_department");
        (await UserExistsAsync(userName)).Should().BeFalse();
    }

    /// <summary>TC-1-055 — spec.md §9, "only Operations subdivides", from the other side.</summary>
    [Fact]
    public async Task Only_operations_users_may_carry_a_sub_department()
    {
        string userName = UniqueNames.Code("ac106d2");

        HttpResponseMessage response = await CreateAsync(
            _owner, Role.Owner, null,
            Body(userName, "MarketingSales", department: "Marketing", subDepartment: "Administrative"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.sub_department_only_for_operations");
    }

    /// <summary>
    /// AC-106-E and TC-1-057. Both external roles, because a check covering <c>Role.Client</c> alone
    /// leaves the same hole open under the other one (D-035).
    /// </summary>
    [Fact]
    public async Task An_external_role_cannot_be_given_a_department()
    {
        foreach (string role in new[] { "Client", "Subcontractor" })
        {
            string userName = UniqueNames.Code("ac106e");

            HttpResponseMessage response = await CreateAsync(
                _owner, Role.Owner, null,
                Body(userName, role, department: "Hr", clientId: role == "Client" ? _clientId : null));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await MessageKeyAsync(response)).Should().Be("errors.identity.external_role_cannot_hold_department");
            (await UserExistsAsync(userName)).Should().BeFalse("and the user is not created");
        }
    }

    [Fact]
    public async Task A_client_user_names_a_client_and_nobody_else_does()
    {
        string portal = UniqueNames.Code("ac106f-portal");

        HttpResponseMessage withoutClient = await CreateAsync(
            _owner, Role.Owner, null, Body(portal, "Client"));

        withoutClient.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(withoutClient)).Should().Be("errors.identity.client_user_requires_client");

        string staff = UniqueNames.Code("ac106f-staff");

        HttpResponseMessage staffWithClient = await CreateAsync(
            _owner, Role.Owner, null, Body(staff, "Finance", department: "Finance", clientId: _clientId));

        staffWithClient.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(staffWithClient)).Should().Be("errors.identity.non_client_user_cannot_carry_client");
    }

    // ---- AC-106-G · usernames do not collide --------------------------------------------------

    /// <summary>
    /// AC-106-G. 🟡 Built under the readiness waiver of D-062 §1; <b>Q51 is open</b> — the
    /// case-insensitive rule is sourced to the slice-0 index and to nothing Karim said.
    /// </summary>
    [Fact]
    public async Task A_username_cannot_be_taken_twice_in_a_different_case()
    {
        string userName = UniqueNames.Code("nabil");

        (await CreateAsync(_owner, Role.Owner, null, Body(userName, "Finance", department: "Finance")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        Guid firstId = (await ReadUserAsync(userName)).Id;

#pragma warning disable CA1308 // Lower-casing is the stored form: User.Create lower-cases, and the
        // uniqueness rule compares against that. ToUpperInvariant would compare the wrong string.
        HttpResponseMessage collision = await CreateAsync(
            _owner, Role.Owner, null,
            Body(userName.ToUpperInvariant(), "Finance", department: "Finance", fullName: "Someone Else"));
#pragma warning restore CA1308

        collision.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(collision)).Should().Be("errors.identity.username_taken");

        User untouched = await ReadUserAsync(userName);
        untouched.Id.Should().Be(firstId, "the existing user is untouched");
        untouched.FullName.Should().NotBe("Someone Else");
    }

    // ---- AC-106-I · eight characters is enough ------------------------------------------------

    [Fact]
    public async Task Eight_lower_case_characters_are_accepted_as_a_temporary_password()
    {
        // D-049 ruling 3: "at least 8 characters, no forced complexity." No digit, no symbol, no case
        // mix — a rule nobody gave must not be enforced by an implementation that assumed one.
        string userName = UniqueNames.Code("ac106i");

        (await CreateAsync(
                _owner, Role.Owner, null,
                Body(userName, "Finance", department: "Finance", password: "abcdefgh")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await ReadUserAsync(userName)).MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task Seven_characters_are_refused()
    {
        string userName = UniqueNames.Code("ac106i-short");

        HttpResponseMessage response = await CreateAsync(
            _owner, Role.Owner, null,
            Body(userName, "Finance", department: "Finance", password: "abcdefg"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.auth.password_too_short");
        (await UserExistsAsync(userName)).Should().BeFalse();
    }

    // ---- helpers ------------------------------------------------------------------------------

    /// <summary>
    /// The request body, with enums as their member names — which is what the wire form is.
    /// </summary>
    /// <remarks>
    /// Written as strings rather than as <see cref="Role"/> values on purpose: a test that
    /// serialises the enum with the same converter the server deserialises it with would pass on a
    /// numeric wire form too, and the UI keys these values as <c>enum.Role.&lt;Member&gt;</c>.
    /// </remarks>
    private static object Body(
        string userName,
        string role,
        string? department = null,
        string? subDepartment = null,
        Guid? clientId = null,
        string? password = null,
        string fullName = "مستخدم الاختبار") => new
        {
            fullName,
            userName,
            phone = UniqueNames.Phone().Entered,
            email = (string?)null,
            role,
            department,
            operationsSubDepartment = subDepartment,
            clientId,
            temporaryPassword = password,
        };

    private async Task<HttpResponseMessage> CreateAsync(
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        object body,
        OperationsSubDepartment? actorSubDepartment = null,
        Guid? actorClientId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/users", UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };

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

    /// <summary>The <c>messageKey</c> extension the API puts on every refusal, so the client can translate it.</summary>
    private static async Task<string?> MessageKeyAsync(HttpResponseMessage response)
    {
        using JsonDocument problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        return problem.RootElement.TryGetProperty("messageKey", out JsonElement key)
            ? key.GetString()
            : null;
    }

    private async Task<User> ReadUserAsync(string userName)
    {
        string stored = AsStored(userName);

        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users.SingleAsync(user => user.UserName == stored, Ct);
    }

    private async Task<bool> UserExistsAsync(string userName)
    {
        string stored = AsStored(userName);

        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users.AnyAsync(user => user.UserName == stored, Ct);
    }

    /// <summary>
    /// The form <c>User.Create</c> stores — trimmed and lower-cased.
    /// </summary>
    /// <remarks>
    /// Lower, not upper. The column holds the lower-cased text, so an <c>OrdinalIgnoreCase</c>
    /// comparison would not translate to SQL and <c>ToUpperInvariant</c> would compare against a
    /// string that is never stored. CA1308 warns about the opposite situation — normalising for
    /// security decisions — and this is a lookup against a known-lower column.
    /// </remarks>
#pragma warning disable CA1308
    private static string AsStored(string userName) => userName.Trim().ToLowerInvariant();
#pragma warning restore CA1308

    private async Task<string> CurrentStampAsync(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        string? stamp = await reader.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .FirstOrDefaultAsync(Ct);

        return stamp ?? "no-such-user";
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("USR-C1"), "عميل إنشاء المستخدمين", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        User owner = MakeUser("usr-owner", Role.Owner);
        User finance = MakeUser("usr-finance", Role.Finance, Department.Finance);
        User technicalOffice = MakeUser("usr-tech", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        User siteEngineer = MakeUser("usr-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User marketing = MakeUser("usr-marketing", Role.MarketingSales, Department.Marketing);
        User hr = MakeUser("usr-hr", Role.Hr, Department.Hr);
        User portal = MakeUser("usr-portal", Role.Client, clientId: client.Id);

        context.Clients.Add(client);
        context.Users.AddRange(owner, finance, technicalOffice, siteEngineer, marketing, hr, portal);

        await context.SaveChangesAsync(Ct);

        _clientId = client.Id;
        _owner = owner.Id;
        _finance = finance.Id;
        _technicalOffice = technicalOffice.Id;
        _siteEngineer = siteEngineer.Id;
        _marketing = marketing.Id;
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
