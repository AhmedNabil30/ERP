using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-102 — <c>POST /api/auth/sign-out</c>, the other half of the staff door.
/// </summary>
/// <remarks>
/// <para>
/// <b>Runs against the shipped JWT bearer scheme</b> (<c>KaffApiFactory(useRealAuthentication: true)</c>),
/// the same choice <c>SignInTests</c> makes and for the same reason: this story is entirely about the
/// cookie, and <see cref="TestAuthHandler"/> replaces the thing under test.
/// </para>
/// <para>
/// <b><c>AC-102-F</c> needs a <see cref="Role.Client"/> session, and nothing in the shipped code can
/// mint one.</b> <c>StaffSessionMinter.Issue</c> throws for <see cref="Role.Client"/> by construction
/// (decisions.md D-063 §1) — spec.md's client portal is a separate host with its own door (D-051
/// Q33), and that door does not exist yet. <see cref="MintToken"/> signs a token by hand, with the
/// same key, issuer and audience <see cref="KaffApiFactory"/> configures, so it authenticates through
/// the real JWT bearer scheme exactly as a future client-portal token would. This proves the sign-out
/// handler is role-agnostic; it does not prove a client can reach it today, because nothing issues
/// that caller a session yet. Flagged in the session report rather than silently worked around.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class SignOutTests : IAsyncLifetime
{
    private const string Password = "sign-out-owner-1";
    private const string CookieName = "__Host-kaff-auth";

    // Matches the values KaffApiFactory sets as environment variables for every test host.
    private const string SigningKey = "tests-only-signing-key-long-enough-for-hmac-sha256";
    private const string TokenIssuer = "kaff-erp-tests";

    private readonly PostgresDatabase _database;

    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private string _ownerName = null!;
    private string _portalName = null!;

    private Guid _owner;
    private Guid _portalUser;

    public SignOutTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-102-A · the browser stops being signed in ------------------------------------------

    [Fact]
    public async Task A_signed_out_browser_is_refused_by_the_next_authenticated_request()
    {
        string cookie = Cookie(await SignIn(_ownerName, Password));

        (await SignOut(cookie)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A real browser honours the Set-Cookie clear and attaches nothing on the next request.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe/company");
        HttpResponseMessage next = await _client.SendAsync(request, Ct);

        next.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await next.Content.ReadAsStringAsync(Ct)).Should().Contain("errors.auth.not_authenticated");
    }

    // ---- AC-102-B · the limit is asserted, not assumed -----------------------------------------

    /// <summary>
    /// <c>AC-102-B</c>: a tool that ignores <c>Set-Cookie</c> and replays the old value is still
    /// accepted. D-051 (N5)'s accepted trade — the day this goes red, somebody decided to add a
    /// session table rather than drifted into needing one.
    /// </summary>
    [Fact]
    public async Task A_replayed_cookie_still_works_because_nothing_is_revoked()
    {
        string cookie = Cookie(await SignIn(_ownerName, Password));

        (await SignOut(cookie)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetWithCookie("/probe/company", cookie)).StatusCode.Should().Be(
            HttpStatusCode.OK,
            "D-051 (N5) — sign-out clears the cookie in the browser; it does not revoke the token, so "
            + "a caller who kept the value can still use it until it expires");
    }

    // ---- AC-102-C · my other device is untouched -----------------------------------------------

    [Fact]
    public async Task Signing_out_on_one_device_leaves_the_other_untouched()
    {
        string deviceA = Cookie(await SignIn(_ownerName, Password));
        string deviceB = Cookie(await SignIn(_ownerName, Password));

        (await SignOut(deviceA)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetWithCookie("/probe/company", deviceB)).StatusCode.Should().Be(
            HttpStatusCode.OK,
            "rule 1 / D-049 ruling 2 — sign-out ends this device's session only");
    }

    // ---- AC-102-D · the cookie is actually cleared ---------------------------------------------

    [Fact]
    public async Task The_cookie_is_cleared_with_the_same_name_path_and_attributes_it_was_minted_with()
    {
        string cookie = Cookie(await SignIn(_ownerName, Password));

        HttpResponseMessage response = await SignOut(cookie);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        string setCookie = response.Headers.GetValues("Set-Cookie").Single();
        setCookie.Should().StartWith(CookieName + "=", "rule 3 / D-050 — same name");

        int equals = setCookie.IndexOf('=', StringComparison.Ordinal);
        int semicolon = setCookie.IndexOf(';', StringComparison.Ordinal);
        setCookie[(equals + 1)..semicolon].Should().BeEmpty("a clear carries no token value");

        string lower = setCookie.ToLowerInvariant();
        lower.Should().Contain("path=/", "the __Host- prefix requires it");
        lower.Should().Contain("secure", "the __Host- prefix requires it");
        lower.Should().Contain("samesite=strict");
        lower.Should().NotContain("domain=", "a Domain attribute makes the __Host- prefix invalid");
    }

    // ---- AC-102-E · sign-out does not disable the account --------------------------------------

    [Fact]
    public async Task Signing_out_never_deactivates_the_account_and_a_fresh_sign_in_still_works()
    {
        string cookie = Cookie(await SignIn(_ownerName, Password));
        (await SignOut(cookie)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SignIn(_ownerName, Password)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();
        bool isActive = await reader.Users
            .Where(user => user.Id == _owner)
            .Select(user => user.IsActive)
            .SingleAsync(Ct);

        isActive.Should().BeTrue("rule 5 — signing out never deactivates the account");
    }

    /// <summary>Rule 6 / rule 2a: sign-out is deliberately not one of the acts that rotate the stamp.</summary>
    [Fact]
    public async Task Sign_out_never_rotates_the_security_stamp()
    {
        string before = await StampOf(_owner);
        string cookie = Cookie(await SignIn(_ownerName, Password));

        (await SignOut(cookie)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await StampOf(_owner)).Should().Be(
            before,
            "only a password change or a deactivation rotates the stamp (rule 6, D-049 ruling 2) — "
            + "rotating here would sign the caller out on every other device too, which rule 1 forbids");
    }

    // ---- AC-102-F · a portal user can sign out -------------------------------------------------

    /// <summary>
    /// See the class remarks: the session is hand-minted because no client-portal door ships yet.
    /// </summary>
    [Fact]
    public async Task A_client_role_session_can_sign_out_too()
    {
        string stamp = await StampOf(_portalUser);

        // Real wall-clock time, deliberately not the fixed `Now` used for entity timestamps: this
        // factory runs no TestClock, so the shipped JWT bearer scheme validates Expires against
        // TimeProvider.System, and a token stamped 2026-05-01 would already read as expired.
        string cookie = $"{CookieName}={MintToken(_portalUser, _portalName, Role.Client, stamp, DateTimeOffset.UtcNow)}";

        HttpResponseMessage response = await SignOut(cookie);

        response.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            "rule 4 / spec.md §9, §12 — sign-out is available to every authenticated role");

        (await response.Content.ReadAsStringAsync(Ct)).Should().BeEmpty(
            "nothing about any client is exposed in the response");

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.Set<AuditRecord>()
            .Where(candidate => candidate.EventType == AuditEventKind.SignedOut
                                 && candidate.EntityId == _portalUser)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        record.ActorRole.Should().Be(Role.Client);
    }

    // ---- Rule 7 · already signed out is not an error -------------------------------------------

    [Fact]
    public async Task Signing_out_with_no_session_is_not_an_error()
    {
        (await SignOut(cookie: null)).StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            "rule 7 — signing out when already signed out is not an error worth a refusal");
    }

    /// <summary>
    /// 🟡 Not settled by the story — see the handler's remarks and the session report. Asserted so a
    /// later session that starts writing a null-actor row for this case makes a deliberate choice
    /// rather than an accidental one.
    /// </summary>
    [Fact]
    public async Task Signing_out_with_no_session_writes_no_audit_record()
    {
        int before = await SignedOutEventCount();

        (await SignOut(cookie: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SignedOutEventCount()).Should().Be(
            before,
            "nothing changed and there is no actor to name — writing a record here was considered "
            + "and deliberately not done; see the handler's remarks");
    }

    // ---- Audit — the trail names the actor, the time and the path ------------------------------

    [Fact]
    public async Task A_sign_out_names_the_actor_the_time_and_the_path()
    {
        string cookie = Cookie(await SignIn(_ownerName, Password));

        (await SignOut(cookie)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.Set<AuditRecord>()
            .Where(candidate => candidate.EventType == AuditEventKind.SignedOut
                                 && candidate.EntityId == _owner)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        record.ActorUserId.Should().Be(_owner);
        record.ActorRole.Should().Be(Role.Owner);
        record.RequestPath.Should().Be("/api/auth/sign-out");
        record.IpAddress.Should().Be(KaffApiFactory.TestRemoteAddress);
        record.BeforeJson.Should().BeNull("an event has no state to snapshot — decisions.md D-061");
        record.AfterJson.Should().BeNull();
    }

    // ---- Helpers --------------------------------------------------------------------------------

    private Task<HttpResponseMessage> SignIn(string userName, string password) =>
        _client.PostAsJsonAsync("/api/auth/sign-in", new { userName, password }, Ct);

    private async Task<HttpResponseMessage> SignOut(string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-out");

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> GetWithCookie(string path, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);

        return await _client.SendAsync(request, Ct);
    }

    private static string Cookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

    private async Task<string> StampOf(Guid userId)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Users
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .SingleAsync(Ct);
    }

    private async Task<int> SignedOutEventCount()
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.Set<AuditRecord>()
            .CountAsync(record => record.EventType == AuditEventKind.SignedOut, Ct);
    }

    /// <summary>
    /// Signs a token by hand with the same key, issuer and audience <see cref="KaffApiFactory"/>
    /// configures for every test host — the same shape <c>StaffSessionMinter.Mint</c> produces, built
    /// independently because that class refuses to mint one for <see cref="Role.Client"/>. See the
    /// class remarks.
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
            UniqueNames.Code("SGO-C1"), "عميل الخروج", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        User owner = User.Create(
            UniqueNames.Code("sgo-owner"), "sgo-owner", UniqueNames.Phone(), Role.Owner, Now).Value;
        owner.SetOwnPassword(PasswordHasher.Hash(Password)).IsSuccess.Should().BeTrue();

        User portal = User.Create(
            UniqueNames.Code("sgo-client"), "عميل البوابة", UniqueNames.Phone(), Role.Client, Now,
            clientId: client.Id).Value;

        context.Clients.Add(client);
        context.Users.AddRange(owner, portal);

        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _ownerName = owner.UserName;
        _portalUser = portal.Id;
        _portalName = portal.UserName;
    }

    private static DateTimeOffset Now => new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
