using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-105a — <c>GET /api/auth/me</c>. Runs against the shipped JWT bearer scheme
/// (<c>KaffApiFactory(useRealAuthentication: true)</c>), the same choice <c>SignInTests</c> and
/// <c>ChangePasswordTests</c> make: this endpoint's whole job is reading the cookie the real scheme
/// produces and the row it names, which <c>TestAuthHandler</c> would substitute away.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class MeTests : IAsyncLifetime
{
    private const string Password = "me-endpoint-1";
    private const string CookieName = "__Host-kaff-auth";

    // Matches the values KaffApiFactory sets as environment variables for every test host — the same
    // constants SignOutTests uses to hand-mint a Role.Client token.
    private const string SigningKey = "tests-only-signing-key-long-enough-for-hmac-sha256";
    private const string TokenIssuer = "kaff-erp-tests";

    private readonly PostgresDatabase _database;

    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private string _financeName = null!;
    private string _forcedName = null!;
    private string _technicalOfficeName = null!;
    private string _inactiveName = null!;
    private string _portalName = null!;
    private string _subcontractorName = null!;

    // The sign-in identifier (UniqueNames.Code-suffixed) and User.FullName are deliberately not the
    // same string — Create trims fullName but leaves it otherwise untouched — so displayName
    // assertions need the literal passed as fullName, not the login name above.
    private string _financeFullName = null!;
    private string _forcedFullName = null!;

    private Guid _finance;
    private Guid _forced;
    private Guid _technicalOffice;
    private Guid _inactive;
    private Guid _portalUser;
    private Guid _subcontractor;

    public MeTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await SeedAsync();

        _factory = new KaffApiFactory(_database.ConnectionString, useRealAuthentication: true);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- AC-105a-A · the caller learns who they are ------------------------------------------------

    /// <summary>
    /// AC-105a-A. Id, display name, role and department, and the flat set of CompanyWide permissions
    /// Finance holds — nothing ProjectScoped beside them (rule 4).
    /// </summary>
    [Fact]
    public async Task An_active_finance_user_learns_who_they_are_and_what_they_hold()
    {
        string cookie = Cookie(await SignIn(_financeName, Password));

        HttpResponseMessage response = await GetWithCookie(cookie);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("userId").GetGuid().Should().Be(_finance);
        body.RootElement.GetProperty("displayName").GetString().Should().Be(_financeFullName);
        body.RootElement.GetProperty("role").GetString().Should().Be(nameof(Role.Finance));
        body.RootElement.GetProperty("department").GetString().Should().Be(nameof(Department.Finance));
        body.RootElement.GetProperty("mustChangePassword").GetBoolean().Should().BeFalse();

        List<string> permissions =
        [
            .. body.RootElement.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()!),
        ];

        permissions.Should().BeEquivalentTo(
            ["SupplierManage", "TreasuryPostCompany", "AccountManage", "PeriodClose"],
            "these are Finance's only CompanyWide catalogue rows today (rule 4/5)");

        permissions.Should().NotContain(
            "ProjectFinancialsEdit",
            "it is ProjectScoped — rule 4 keeps it out of this endpoint even though Finance holds it");
    }

    // ---- AC-105a-B · no token, anywhere -------------------------------------------------------------

    /// <summary>AC-105a-B. The bearer token itself never appears in the response body.</summary>
    [Fact]
    public async Task No_field_in_the_response_carries_the_session_token()
    {
        string cookie = Cookie(await SignIn(_financeName, Password));
        string token = cookie[(cookie.IndexOf('=', StringComparison.Ordinal) + 1)..];

        string body = await (await GetWithCookie(cookie)).Content.ReadAsStringAsync(Ct);

        body.Should().NotContain(token, "D-050 — the token exists only in the HttpOnly cookie");
        body.Should().NotContain("token", "no field carries it under any name");
    }

    // ---- AC-105a-C · a forced password change is announced, as a field on a 200 --------------------

    /// <summary>
    /// AC-105a-C, decisions.md D-072 §2. The call succeeds — it is not refused in any shape — and the
    /// flag rides in the payload rather than in the status code.
    /// </summary>
    [Fact]
    public async Task A_forced_password_change_is_announced_as_a_field_on_a_200_not_a_refusal()
    {
        string cookie = Cookie(await SignIn(_forcedName, Password));

        HttpResponseMessage response = await GetWithCookie(cookie);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "AC-105a-C — not a 403, not a 401, and not an empty profile");

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("mustChangePassword").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("userId").GetGuid().Should().Be(_forced, "the profile is full, not empty");
        body.RootElement.GetProperty("displayName").GetString().Should().Be(_forcedFullName);
    }

    // ---- AC-105a-D · signed out is not "signed in as nobody" ----------------------------------------

    /// <summary>AC-105a-D. No cookie, no header: refused with 401, not an all-null profile.</summary>
    [Fact]
    public async Task With_no_session_the_call_is_refused_with_401_not_an_empty_profile()
    {
        HttpResponseMessage response = await GetWithCookie(cookie: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("errors.auth.not_authenticated");
    }

    // ---- V-26-B · the two roles that may never hold a staff session --------------------------------

    /// <summary>
    /// <c>V-26-B</c>. spec.md §9: a subcontractor is <i>"record only, no login"</i> — and this endpoint
    /// answered one with a <c>200</c> and their name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Hand-minted, and the reason is worth stating exactly, because the Verifier's report says
    /// otherwise.</b> qa/slice-1/verification-2026-08-26.md calls this half "reachable in production"
    /// through KAFF-109, on the reasoning that <c>User.ChangeRole</c> does not rotate the security
    /// stamp (D-051 Q27) so a converted account keeps its live session. Holding a live staff session
    /// means having signed in, which means holding a credential — and a credential is exactly what
    /// blocks the conversion, with a <c>500</c> before D-088 and a <c>409</c> after it. Clearing the
    /// credential first is the only way through, and <c>User.ClearPassword</c> rotates the stamp, which
    /// kills the session. **Both halves of <c>V-26-B</c> therefore need a hand-issued identity today**,
    /// exactly as the report already concedes for <see cref="Role.Client"/> in its §8. The defect it
    /// found is real; the route to it was one step longer than recorded. See decisions.md D-089.
    /// </para>
    /// <para>
    /// <b>Which is why the bar is a property of the door rather than a patch on a path.</b> The
    /// hand-minted token below is a faithful reproduction of a session <c>StaffSessionMinter</c> refuses
    /// to create and the portal door of D-051 Q33 will one day create for the sibling role. This test
    /// fails the moment the bar is removed from <c>LiveSession</c>; nothing else in the suite does.
    /// </para>
    /// <para>
    /// <b>The refusal must be the blanket one.</b> Nabil: <i>"If we return a specific
    /// <c>errors.auth.role_cannot_log_in</c>, we are explicitly telling the attacker: 'This account
    /// exists and belongs to a subcontractor.' That is a security breach."</i> The key asserted below
    /// is the same <c>errors.auth.forbidden</c> a deactivated account and a stale stamp both get.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_subcontractor_session_is_refused_not_answered_with_a_profile()
    {
        string stamp = await StampOf(_subcontractor);
        string cookie =
            $"{CookieName}={MintToken(_subcontractor, _subcontractorName, Role.Subcontractor, stamp, DateTimeOffset.UtcNow)}";

        HttpResponseMessage refused = await GetWithCookie(cookie);

        refused.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "spec.md §9 — record only, no login. The role bar the sign-in door and the permission "
            + "evaluator both apply is not optional on the routes the gate does not run on");

        string body = await refused.Content.ReadAsStringAsync(Ct);

        body.Should().Contain(
            "errors.auth.forbidden",
            "the blanket refusal D-080 requires — the same one a deactivated account gets");
        body.Should().NotContain(
            "role_cannot_log_in",
            "a specific key here tells an attacker the account exists and what it is (Nabil, D-080)");
        body.Should().NotContain(
            nameof(Role.Subcontractor),
            "and neither does the body name the role in any other field");
    }

    // ---- AC-105a-H · a portal client's company-wide set is empty -----------------------------------

    /// <summary>
    /// <c>V-26-B</c>, the sibling half: a hand-minted <see cref="Role.Client"/> staff session is
    /// refused rather than answered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This case used to assert the opposite</b> — a <c>200</c> carrying an empty permission array,
    /// on the reasoning that <c>StaffSessionMinter</c> refuses that role so no such caller could exist.
    /// The token was treated as a stand-in for an unreachable state; <c>V-26-B</c> showed it is a
    /// faithful reproduction of a state KAFF-109 <i>can</i> produce for the sibling role, and the
    /// endpoint answered it. decisions.md D-062 §2, Nabil: a <see cref="Role.Client"/> may not
    /// <i>"sign in or authenticate through the staff portal"</i>, and <c>/api/auth/*</c> is that portal
    /// (D-063 §1).
    /// </para>
    /// <para>
    /// <b><c>AC-105a-H</c>'s substance — a client holds no company-wide permission — is unaffected and
    /// is proved where it is a fact about the rule rather than about this route</b>
    /// [@ <c>tests/Domain.Tests/PermissionEvaluatorTests.cs</c> -&gt;
    /// <c>A_portal_client_holds_no_company_wide_permission</c>]. 🟡 When the portal door of D-051 Q33
    /// ships, whether it reuses this endpoint is a question for that story — it would have to widen
    /// this bar deliberately, which is the point of it being one line in Domain.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_hand_minted_portal_client_session_is_refused_by_the_staff_door()
    {
        string stamp = await StampOf(_portalUser);
        string cookie = $"{CookieName}={MintToken(_portalUser, _portalName, Role.Client, stamp, DateTimeOffset.UtcNow)}";

        HttpResponseMessage response = await GetWithCookie(cookie);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        string raw = await response.Content.ReadAsStringAsync(Ct);

        raw.Should().Contain("errors.auth.forbidden");
        raw.Should().NotContain(nameof(Role.Client), "a refusal names no role");
        raw.Should().NotContain("PortalRead");
        raw.Should().NotContain("PortalApprove");
    }

    // ---- AC-105a-G · nothing secret leaks ------------------------------------------------------------

    /// <summary>AC-105a-G. No password hash, no security stamp, anywhere in the payload.</summary>
    [Fact]
    public async Task Nothing_secret_leaks()
    {
        string cookie = Cookie(await SignIn(_financeName, Password));
        string body = await (await GetWithCookie(cookie)).Content.ReadAsStringAsync(Ct);

        body.Should().NotContain("passwordHash", "[AuditRedacted] governs the trail, not the API — rule 9");
        body.Should().NotContain("securityStamp");
        body.Should().NotContain("pbkdf2");
    }

    // ---- The trap named in the brief: the role must come from the row, not the token's claim -------

    /// <summary>
    /// KAFF-109 changes a role without rotating the security stamp (decisions.md D-051 Q27) — the old
    /// token keeps authenticating. If this handler read the role from the token's claim instead of the
    /// database row, it would report the role as it was when the token was minted. It must report the
    /// role as it is now.
    /// </summary>
    [Fact]
    public async Task A_role_changed_after_sign_in_is_reported_fresh_not_from_the_stale_token()
    {
        string cookie = Cookie(await SignIn(_technicalOfficeName, Password));

        await using (KaffDbContext context = _database.CreateContext())
        {
            User user = await context.Users.SingleAsync(candidate => candidate.Id == _technicalOffice, Ct);
            user.ChangeRole(Role.Finance).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(Ct);
        }

        HttpResponseMessage response = await GetWithCookie(cookie);
        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "ChangeRole does not rotate the stamp, so the old cookie still authenticates");

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

        body.RootElement.GetProperty("role").GetString().Should().Be(
            nameof(Role.Finance),
            "the database row now says Finance; the token's claim still says TechnicalOffice, and the "
            + "database must win");
    }

    // ---- The endpoint re-checks its own caller — no permission gate does it here --------------------

    /// <summary>
    /// No permission gate runs on this self-only endpoint, so the handler re-applies the same freshness
    /// D-048 requires everywhere else: a deactivated account's still-valid token is refused, not
    /// answered with a profile.
    /// </summary>
    [Fact]
    public async Task A_deactivated_accounts_token_is_refused_not_answered_with_a_profile()
    {
        string cookie = Cookie(await SignIn(_inactiveName, Password));

        await using (KaffDbContext context = _database.CreateContext())
        {
            User user = await context.Users.SingleAsync(candidate => candidate.Id == _inactive, Ct);
            user.Deactivate(Now).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(Ct);
        }

        (await GetWithCookie(cookie)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A password change on one device rotates the stamp (D-049 ruling 2), which ends every other
    /// session at once — including what this endpoint will answer for it. Proves the handler's own
    /// stamp re-check is not dead code: nothing upstream of a SelfOnlyEndpoints route applies it.
    /// </summary>
    [Fact]
    public async Task A_password_changed_on_another_device_ends_this_endpoints_answer_too()
    {
        string deviceA = Cookie(await SignIn(_financeName, Password));
        string deviceB = Cookie(await SignIn(_financeName, Password));

        (await GetWithCookie(deviceB)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword = Password, newPassword = "a-brand-new-one" }),
        };
        request.Headers.Add("Cookie", deviceA);
        (await _client.SendAsync(request, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetWithCookie(deviceB)).StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "SetOwnPassword rotates the stamp; this endpoint's own freshness check must see that too");
    }

    // ---- Helpers --------------------------------------------------------------------------------

    private Task<HttpResponseMessage> SignIn(string userName, string password) =>
        _client.PostAsJsonAsync("/api/auth/sign-in", new { userName, password }, Ct);

    private async Task<HttpResponseMessage> GetWithCookie(string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await _client.SendAsync(request, Ct);
    }

    private static string Cookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Last().Split(';')[0];

    private async Task<string> StampOf(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .SingleAsync(Ct);
    }

    /// <summary>
    /// Signs a token by hand with the same key, issuer and audience <see cref="KaffApiFactory"/>
    /// configures for every test host — the same technique <c>SignOutTests</c> uses, and for the same
    /// reason: <c>StaffSessionMinter</c> refuses to mint one for <see cref="Role.Client"/>.
    /// </summary>
    private static string MintToken(Guid userId, string displayName, Role role, string stamp, DateTimeOffset issuedAt)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = TokenIssuer,
            Audience = TokenIssuer,
            SigningCredentials = credentials,
            IssuedAt = issuedAt.UtcDateTime,
            Expires = issuedAt.UtcDateTime.AddMinutes(30),
            Subject = new ClaimsIdentity(
            [
                new Claim(KaffClaimTypes.UserId, userId.ToString()),
                new Claim(KaffClaimTypes.DisplayName, displayName),
                new Claim(KaffClaimTypes.Role, role.ToString()),
                new Claim(KaffClaimTypes.SecurityStamp, stamp),
            ]),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        Client client = Client.Create(
            UniqueNames.Code("ME-C1"), "عميل الملف الشخصي", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        User finance = MakeUser("me-finance", Role.Finance, Department.Finance);
        finance.SetOwnPassword(PasswordHasher.Hash(Password)).IsSuccess.Should().BeTrue();

        User forced = MakeUser("me-forced", Role.Finance, Department.Finance);
        forced.SetTemporaryPassword(PasswordHasher.Hash(Password)).IsSuccess.Should().BeTrue();

        User technicalOffice = MakeUser("me-tech-office", Role.TechnicalOffice, Department.Operations, OperationsSubDepartment.Technical);
        technicalOffice.SetOwnPassword(PasswordHasher.Hash(Password)).IsSuccess.Should().BeTrue();

        User inactive = MakeUser("me-inactive", Role.Finance, Department.Finance);
        inactive.SetOwnPassword(PasswordHasher.Hash(Password)).IsSuccess.Should().BeTrue();

        // A subcontractor record: no department (ValidateDepartment refuses an external role one),
        // no credential (StorePasswordHash refuses that role one), active. Everything a live session
        // needs except a role that may hold one.
        User subcontractor = MakeUser("me-subcontractor", Role.Subcontractor);

        User portal = User.Create(
            UniqueNames.Code("me-portal"), "عميل البوابة", UniqueNames.Phone(), Role.Client, Now,
            clientId: client.Id).Value;

        context.Clients.Add(client);
        context.Users.AddRange(finance, forced, technicalOffice, inactive, portal, subcontractor);

        await context.SaveChangesAsync(Ct);

        _finance = finance.Id;
        _forced = forced.Id;
        _technicalOffice = technicalOffice.Id;
        _inactive = inactive.Id;
        _portalUser = portal.Id;
        _subcontractor = subcontractor.Id;

        _financeName = finance.UserName;
        _forcedName = forced.UserName;
        _technicalOfficeName = technicalOffice.UserName;
        _inactiveName = inactive.UserName;
        _portalName = portal.UserName;
        _subcontractorName = subcontractor.UserName;

        _financeFullName = finance.FullName;
        _forcedFullName = forced.FullName;
    }

    private static User MakeUser(string userName, Role role, Department? department = null, OperationsSubDepartment? subDepartment = null)
        => User.Create(
            UniqueNames.Code(userName),
            userName,
            UniqueNames.Phone(),
            role,
            Now,
            department,
            subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
