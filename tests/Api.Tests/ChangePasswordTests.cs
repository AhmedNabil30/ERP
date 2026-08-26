using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-103 — <c>POST /api/auth/change-password</c>. Runs against the shipped JWT bearer scheme
/// (<c>KaffApiFactory(useRealAuthentication: true)</c>), the same choice <c>SignInTests</c> makes and
/// for the same reason: the cookie this endpoint re-mints, and the claims it reads to re-check the
/// caller, are invisible to a harness that replaces authentication with request headers.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ChangePasswordTests : IAsyncLifetime
{
    private const string TemporaryPassword = "owner-set-1";
    private const string CookieName = "__Host-kaff-auth";

    private readonly PostgresDatabase _database;

    private TestClock _clock = null!;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private string _forcedName = null!;
    private string _ordinaryName = null!;
    private string _inactiveName = null!;

    private Guid _forced;
    private Guid _ordinary;
    private Guid _inactive;

    public ChangePasswordTests(PostgresDatabase database) => _database = database;

    public async ValueTask InitializeAsync()
    {
        await SeedAsync();

        _clock = new TestClock();
        _factory = new KaffApiFactory(_database.ConnectionString, useRealAuthentication: true, clock: _clock);
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ---- AC-103-A · the change ends the forced state and the caller stays signed in -------------

    /// <summary>
    /// AC-103-A. Changing the temporary password frees the session, and an audit record of
    /// <see cref="AuditAction.Modified"/> names the holder as their own actor.
    /// </summary>
    [Fact]
    public async Task A_new_user_changes_the_temporary_password_and_is_then_free()
    {
        string signedIn = Cookie(await SignIn(_forcedName, TemporaryPassword));

        HttpResponseMessage response = await ChangePassword(signedIn, TemporaryPassword, "chosen-password");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        string freshCookie = Cookie(response);
        (await GetWithCookie("/probe/company", freshCookie)).StatusCode.Should().Be(
            HttpStatusCode.OK,
            "AC-103-A — the caller can use the rest of the system without signing in again");

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.Set<AuditRecord>()
            .Where(candidate => candidate.EntityType == nameof(User)
                                 && candidate.EntityId == _forced
                                 && candidate.Action == AuditAction.Modified)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        record.ActorUserId.Should().Be(_forced, "the person who changed it is the actor");
        record.ChangedProperties.Should().Contain("MustChangePassword");
    }

    // ---- AC-103-B · until then, nothing else is reachable except this endpoint -------------------

    /// <summary>
    /// AC-103-B. While <c>MustChangePassword</c> is still true, a permission-gated route is refused
    /// with <c>errors.auth.password_change_required</c> — and the change-password endpoint itself is
    /// not in that set, because it carries no permission requirement for the gate to apply to.
    /// </summary>
    [Fact]
    public async Task Until_the_password_is_changed_every_other_endpoint_refuses_it_and_this_one_does_not()
    {
        string cookie = Cookie(await SignIn(_forcedName, TemporaryPassword));

        HttpResponseMessage gated = await GetWithCookie("/probe/company", cookie);

        gated.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await gated.Content.ReadAsStringAsync(Ct)).Should().Contain("errors.auth.password_change_required");

        HttpResponseMessage change = await ChangePassword(cookie, TemporaryPassword, "chosen-password");

        change.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            "the change-password endpoint carries no RequirePermission, so PasswordChangeRequired "
            + "never applies to it");
    }

    // ---- AC-103-C · the temporary password stops working the moment it is replaced ---------------

    /// <summary>
    /// AC-103-C. After the change, the temporary password is refused exactly as any other wrong
    /// password is — the same generic key, indistinguishable from a stranger's guess.
    /// </summary>
    [Fact]
    public async Task The_temporary_password_stops_working_once_it_is_replaced()
    {
        string cookie = Cookie(await SignIn(_forcedName, TemporaryPassword));
        (await ChangePassword(cookie, TemporaryPassword, "chosen-password")).StatusCode.Should().Be(
            HttpStatusCode.NoContent);

        HttpResponseMessage retry = await SignIn(_forcedName, TemporaryPassword);

        retry.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await retry.Content.ReadAsStringAsync(Ct)).Should().Contain("errors.auth.invalid_credentials");
    }

    // ---- AC-103-D · the current password is required ----------------------------------------------

    /// <summary>
    /// AC-103-D. A wrong current password is refused, and the stored hash is unchanged — proved by
    /// the old password still working afterwards.
    /// </summary>
    [Fact]
    public async Task The_current_password_must_be_correct()
    {
        string cookie = Cookie(await SignIn(_ordinaryName, TemporaryPassword));

        HttpResponseMessage response = await ChangePassword(cookie, "not-the-real-one", "chosen-password");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain(
            "errors.auth.current_password_incorrect");

        (await SignIn(_ordinaryName, TemporaryPassword)).StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            "the refused change must not have touched the stored hash");
    }

    // ---- AC-103-E · eight characters, and nothing more, is the whole rule -------------------------

    /// <summary>AC-103-E. Eight lower-case letters are accepted; seven is refused.</summary>
    [Fact]
    public async Task Eight_characters_are_enough_and_seven_is_refused()
    {
        string cookie = Cookie(await SignIn(_ordinaryName, TemporaryPassword));

        HttpResponseMessage tooShort = await ChangePassword(cookie, TemporaryPassword, "abcdefg");

        tooShort.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooShort.Content.ReadAsStringAsync(Ct)).Should().Contain("errors.auth.password_too_short");

        HttpResponseMessage justRight = await ChangePassword(cookie, TemporaryPassword, "abcdefgh");

        justRight.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---- AC-103-F · the change ends every other session --------------------------------------------

    /// <summary>AC-103-F. A second device's session dies the moment the password changes on the first.</summary>
    [Fact]
    public async Task The_change_ends_every_other_session()
    {
        string deviceA = Cookie(await SignIn(_ordinaryName, TemporaryPassword));
        string deviceB = Cookie(await SignIn(_ordinaryName, TemporaryPassword));

        (await GetWithCookie("/probe/company", deviceB)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage changed = await ChangePassword(deviceA, TemporaryPassword, "a-brand-new-one");
        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetWithCookie("/probe/company", deviceB)).StatusCode.Should().NotBe(
            HttpStatusCode.OK,
            "SetOwnPassword rotates the stamp, which ends every other session at once (D-051 N5)");

        (await GetWithCookie("/probe/company", Cookie(changed))).StatusCode.Should().Be(
            HttpStatusCode.OK,
            "the device that made the change gets a fresh cookie in the same response");
    }

    // ---- AC-103-G · the creator never learns the chosen password -----------------------------------

    /// <summary>
    /// AC-103-G. Neither the response, the audit record's before/after JSON, nor the stored hash
    /// carries the plaintext — the redacted placeholder stands in for both credential fields.
    /// </summary>
    [Fact]
    public async Task No_field_anywhere_carries_the_chosen_password_or_its_hash()
    {
        const string Chosen = "nobody-else-should-ever-see-this";

        string cookie = Cookie(await SignIn(_ordinaryName, TemporaryPassword));
        HttpResponseMessage response = await ChangePassword(cookie, TemporaryPassword, Chosen);

        (await response.Content.ReadAsStringAsync(Ct)).Should().BeEmpty("a 204 carries no body to leak into");

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.Set<AuditRecord>()
            .Where(candidate => candidate.EntityType == nameof(User)
                                 && candidate.EntityId == _ordinary
                                 && candidate.Action == AuditAction.Modified)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        string payload = (record.BeforeJson ?? string.Empty) + (record.AfterJson ?? string.Empty);

        payload.Should().NotContain(Chosen);
        payload.Should().NotContain("pbkdf2", "PasswordHash is [AuditRedacted]");
        payload.Should().Contain("[redacted]", "the redaction is visible, not merely absent (AC-118-F)");
    }

    // ---- The endpoint re-checks the caller itself — no permission gate does it here ---------------

    /// <summary>
    /// No permission gate runs on this self-only endpoint, so the handler re-applies the same
    /// freshness D-048 requires everywhere else: a deactivated account cannot use it either.
    /// </summary>
    [Fact]
    public async Task A_deactivated_account_cannot_change_its_own_password()
    {
        string cookie = Cookie(await SignIn(_inactiveName, TemporaryPassword));

        await using (KaffDbContext context = _database.CreateContext())
        {
            User user = await context.Users.SingleAsync(candidate => candidate.Id == _inactive, Ct);
            user.Deactivate(Now).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(Ct);
        }

        HttpResponseMessage response = await ChangePassword(cookie, TemporaryPassword, "chosen-password");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Helpers -------------------------------------------------------------------------------

    private Task<HttpResponseMessage> SignIn(string userName, string password) =>
        _client.PostAsJsonAsync("/api/auth/sign-in", new { userName, password }, Ct);

    private async Task<HttpResponseMessage> ChangePassword(string cookie, string currentPassword, string newPassword)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new { currentPassword, newPassword }),
        };
        request.Headers.Add("Cookie", cookie);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> GetWithCookie(string path, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);

        return await _client.SendAsync(request, Ct);
    }

    /// <summary>
    /// The <c>name=value</c> pair, ready to go back as a <c>Cookie</c> header.
    /// </summary>
    /// <remarks>
    /// This endpoint is authenticated, not anonymous, so <c>SlidingSessionMiddleware</c> renews the
    /// session with the request's own (pre-change) stamp <b>before</b> the handler runs, and the
    /// handler mints a second cookie with the new stamp afterwards — two <c>Set-Cookie</c> headers for
    /// one name, exactly the documented shape <c>SlidingSessionMiddleware</c>'s own remarks describe
    /// for the sign-in case. The last one is what a real browser ends up storing.
    /// </remarks>
    private static string Cookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Last().Split(';')[0];

    private async Task SeedAsync()
    {
        await using KaffDbContext context = _database.CreateContext();

        User forced = MakeUser("chg-forced", Role.MarketingSales, Department.Marketing);
        forced.SetTemporaryPassword(PasswordHasher.Hash(TemporaryPassword)).IsSuccess.Should().BeTrue();

        User ordinary = MakeUser("chg-ordinary", Role.MarketingSales, Department.Marketing);
        ordinary.SetOwnPassword(PasswordHasher.Hash(TemporaryPassword)).IsSuccess.Should().BeTrue();

        User inactive = MakeUser("chg-inactive", Role.MarketingSales, Department.Marketing);
        inactive.SetOwnPassword(PasswordHasher.Hash(TemporaryPassword)).IsSuccess.Should().BeTrue();

        context.Users.AddRange(forced, ordinary, inactive);
        await context.SaveChangesAsync(Ct);

        _forced = forced.Id;
        _ordinary = ordinary.Id;
        _inactive = inactive.Id;

        _forcedName = forced.UserName;
        _ordinaryName = ordinary.UserName;
        _inactiveName = inactive.UserName;
    }

    private static User MakeUser(string userName, Role role, Department? department = null)
        => User.Create(
            UniqueNames.Code(userName),
            userName,
            UniqueNames.Phone(),
            role,
            Now,
            department).Value;

    private static DateTimeOffset Now => new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
