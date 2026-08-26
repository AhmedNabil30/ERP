using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Identity;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Contracts;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Domain.Projects;
using Kaff.Infrastructure.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-101a — <c>POST /api/auth/sign-in</c>, the staff door.
/// </summary>
/// <remarks>
/// <para>
/// <b>This suite runs against the shipped JWT bearer scheme, not <see cref="TestAuthHandler"/></b>
/// (<c>KaffApiFactory(useRealAuthentication: true)</c>). Everything this story is about — the cookie,
/// its five attributes, the token inside it, the expiry, the security stamp claim — is invisible to a
/// harness that replaces authentication with request headers.
/// </para>
/// <para>
/// <b>Cookies are carried by hand rather than by a <c>CookieContainer</c>.</b> The assertions are
/// about the <c>Set-Cookie</c> header itself, and a container would parse it away — and would refuse
/// the <c>Secure</c> cookie over the test host's <c>http://</c> anyway.
/// </para>
/// <para>
/// ⚠️ <b>The criterion this file exists for is <c>AC-101a-P</c>.</b> A locked account given the wrong
/// password answers <c>401</c>, not <c>423</c> — which is what an implementation consulting
/// <c>User.LockedOutUntil</c> before verifying the password cannot do. The other half of rule 14a,
/// the even time envelope, is measured in <see cref="PasswordHasherTests"/> against the pure function
/// and once more here through the pipeline.
/// </para>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class SignInTests : IAsyncLifetime
{
    private const string Password = "site-engineer-1";
    private const string CookieName = "__Host-kaff-auth";
    private const string InvalidCredentials = "errors.auth.invalid_credentials";

    private readonly PostgresDatabase _database;

    private TestClock _clock = null!;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private string _engineerName = null!;
    private string _ownerName = null!;
    private string _clientName = null!;
    private string _subcontractorName = null!;
    private string _lockedName = null!;
    private string _inactiveName = null!;
    private string _noCredentialName = null!;

    private Guid _engineer;
    private Guid _owner;
    private Guid _project;

    public SignInTests(PostgresDatabase database) => _database = database;

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

    // ---- AC-101a-A · a valid credential opens a session and hands JavaScript nothing -----------

    /// <summary>
    /// AC-101a-A and TC-1-220: the body carries no token, the cookie carries all five attributes,
    /// and the cookie authenticates the next request.
    /// </summary>
    [Fact]
    public async Task A_valid_credential_sets_the_session_cookie_and_puts_no_token_in_the_body()
    {
        HttpResponseMessage response = await SignIn(_ownerName, Password);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        string body = await response.Content.ReadAsStringAsync(Ct);
        body.Should().BeEmpty("D-050 — the response body carries no token in any field under any name");

        string setCookie = response.Headers.GetValues("Set-Cookie").Single();

        setCookie.Should().StartWith(CookieName + "=", "the __Host- prefix is case-sensitive");

        // Attribute names are case-insensitive on the wire, so the five below are matched against a
        // lower-cased copy rather than against whatever casing the framework emits today.
        setCookie = setCookie.ToLowerInvariant();

        setCookie.Should().Contain("httponly", "an injected script must not be able to read it");
        setCookie.Should().Contain("secure", "the __Host- prefix is invalid without it");
        setCookie.Should().Contain("samesite=strict", "this is the whole of the CSRF control (rule 3)");
        setCookie.Should().Contain("path=/", "the __Host- prefix requires it");
        setCookie.Should().NotContain("domain=", "a Domain attribute makes the __Host- prefix invalid");

        // The other half of the criterion: the cookie is a session, not a decoration.
        HttpResponseMessage next = await GetWithCookie("/probe/company", Cookie(response));
        next.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Rule 12: identity, and no permission or assignment list.</summary>
    [Fact]
    public async Task The_token_carries_the_user_the_name_the_role_and_the_stamp_and_nothing_else()
    {
        HttpResponseMessage response = await SignIn(_engineerName, Password);

        Dictionary<string, JsonElement> claims = TokenClaims(Cookie(response));

        claims.Should().ContainKey("kaff:uid");
        claims.Should().ContainKey("kaff:name");
        claims.Should().ContainKey("kaff:role");
        claims.Should().ContainKey("kaff:stamp");

        claims.Keys.Where(key => key.StartsWith("kaff:", StringComparison.Ordinal)).Should().HaveCount(
            4,
            "rule 12 — the token carries no permission list and no assignment list; both are "
            + "re-evaluated server-side per request");

        string serialised = JsonSerializer.Serialize(claims);
        serialised.Should().NotContain(Password);
        serialised.Should().NotContain("pbkdf2", "AC-101a-L — the hash never leaves the database");
    }

    // ---- AC-101a-B · five refusals nobody can tell apart ---------------------------------------

    /// <summary>
    /// AC-101a-B. The wrong password, an unknown username, a <see cref="Role.Client"/>, a
    /// <see cref="Role.Subcontractor"/> and a locked account given the wrong password — five
    /// responses, byte-for-byte identical.
    /// </summary>
    /// <remarks>
    /// The client and the subcontractor are posted with the <b>correct</b> password, which is what
    /// makes them the interesting cases: a 403 or a distinct key would fire only on a real
    /// credential, and that is the single most informative answer an anonymous door can give
    /// (decisions.md D-063 §1, D-065 cases 4 and 5).
    /// </remarks>
    [Fact]
    public async Task Five_different_refusals_are_one_answer()
    {
        await Lock(_lockedName);

        (string Name, string Submitted)[] cases =
        [
            (_engineerName, "wrong-password"),
            ("no-such-person-at-kaff", Password),
            (_clientName, Password),
            (_subcontractorName, Password),
            (_lockedName, "wrong-password"),
        ];

        List<string> bodies = [];

        foreach ((string name, string submitted) in cases)
        {
            HttpResponseMessage response = await SignIn(name, submitted);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "'{0}' must answer 401", name);
            response.Headers.Should().NotContain(
                header => header.Key == "Set-Cookie",
                "no refusal mints a session");

            bodies.Add(WithoutTraceId(await response.Content.ReadAsStringAsync(Ct)));
        }

        bodies.Should().AllBe(
            bodies[0],
            "AC-101a-B — all five are identical in status, body and messageKey, and nothing anywhere "
            + "in the response distinguishes which case it was");

        bodies[0].Should().Contain(InvalidCredentials);
    }

    /// <summary>
    /// AC-101a-G. The subcontractor refusal is audited, and is the same answer as an unknown user.
    /// </summary>
    [Fact]
    public async Task A_subcontractor_is_refused_like_a_stranger_and_the_attempt_is_recorded()
    {
        HttpResponseMessage response = await SignIn(_subcontractorName, Password);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain(InvalidCredentials);
        (await response.Content.ReadAsStringAsync(Ct)).Should().NotContain("role_cannot_log_in");

        (await EventsFor(_subcontractorName)).Should().Contain(AuditEventKind.SignInFailed);
    }

    // ---- AC-101a-P · the locked account answers on the truth of the password -------------------

    /// <summary>
    /// ⚠️ <b><c>AC-101a-P</c> — the criterion that fails on the wrong ordering.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The correct password against a locked account gets <c>423</c> /
    /// <c>errors.auth.account_locked</c>; the wrong one gets the generic <c>401</c>, byte-for-byte
    /// what an unknown username gets. <b>An implementation that consults <c>User.LockedOutUntil</c>
    /// before verifying the password cannot produce both</b> — it does not yet know which it is
    /// looking at, so it answers 423 to the wrong password too and this test goes red on the second
    /// half.
    /// </para>
    /// <para>
    /// <b>Watched red</b> on 2026-08-26 by hoisting the lockout check above
    /// <c>PasswordHasher.Verify</c>: <c>Expected the enum to be HttpStatusCode.Unauthorized {value: 401},
    /// but found HttpStatusCode.Locked {value: 423}</c>, here and in
    /// <see cref="Five_different_refusals_are_one_answer"/>. Nothing else in the suite moved.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_locked_account_answers_423_to_the_right_password_and_401_to_a_wrong_one()
    {
        await Lock(_lockedName);

        HttpResponseMessage correct = await SignIn(_lockedName, Password);

        ((int)correct.StatusCode).Should().Be(423, "D-072 §1 — 423 only when the password is correct");
        (await correct.Content.ReadAsStringAsync(Ct)).Should().Contain("errors.auth.account_locked");
        correct.Headers.Should().NotContain(header => header.Key == "Set-Cookie", "a lock mints nothing");

        HttpResponseMessage wrong = await SignIn(_lockedName, "wrong-password");

        wrong.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "a wrong password against a locked account is the generic 401 and nothing else — "
            + "answering 423 here is the enumeration primitive D-072 §1 was written to seal, and it "
            + "is what an implementation that checks the lockout before the password produces");

        (await wrong.Content.ReadAsStringAsync(Ct)).Should().Contain(InvalidCredentials);
    }

    /// <summary>
    /// ⚠️ Rule 14a's second half, through the pipeline: <b>no refusal is distinguishable by how long
    /// it takes.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The status-code assertions above kill the ordering defect in its locked-account form. They
    /// cannot see it in its other form — <c>if (user is null) return Unauthorized;</c> — which
    /// returns the correct status for every case in the suite and answers an unknown username in
    /// microseconds while every real attempt pays for 600,000 PBKDF2 iterations. That is the
    /// enumeration oracle re-opened as a clock, and this is the only test in the suite that fails
    /// on it.
    /// </para>
    /// <para>
    /// <b>The statistic is the minimum of three attempts</b>, and the assertion is "at least half the
    /// baseline". The baseline is a known username with a wrong password, which cannot be answered
    /// without hashing. The margin between doing the work and skipping it is three orders of
    /// magnitude, so no scheduler noise reaches the threshold from either side; the pure-function
    /// version of this measurement, with no HTTP in the way, is
    /// <see cref="PasswordHasherTests.Verifying_against_no_stored_hash_costs_what_verifying_against_one_costs"/>.
    /// </para>
    /// <para>
    /// <b>Watched red</b> on 2026-08-26 with an early <c>if (user is null)</c> return before the
    /// hash: the unknown username answered in <b>61,475 ticks against a 4,653,877 baseline</b> —
    /// 1.3% of the work — while <b>every other test in this file stayed green</b>, 20 of 21. That
    /// is the whole reason this test exists.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task No_refusal_is_faster_than_the_hash_it_should_have_paid_for()
    {
        await Lock(_lockedName);

        // Warm the pipeline: the first request through a fresh host pays for JIT, the connection
        // pool and the first query plan, and that cost lands on whichever case runs first.
        _ = await SignIn(_engineerName, "warm-up");

        long baseline = Fastest(_engineerName, "wrong-password");

        (string Name, string Submitted, string Why)[] cases =
        [
            ("no-such-person-at-kaff", Password, "a username that matches no row"),
            (_subcontractorName, Password, "a subcontractor, whose PasswordHash is null by rule"),
            (_noCredentialName, Password, "an account whose credential was cleared"),
            (_lockedName, "wrong-password", "a locked account given the wrong password"),
        ];

        foreach ((string name, string submitted, string why) in cases)
        {
            long elapsed = Fastest(name, submitted);

            elapsed.Should().BeGreaterThan(
                baseline / 2,
                "{0} must cost what a wrong password costs. It answered in {1} ticks against a "
                + "{2}-tick baseline — a fraction of the work, which is the user-enumeration oracle "
                + "KAFF-101a rule 14a exists to close, arriving as a clock rather than as a status "
                + "code",
                why,
                elapsed,
                baseline);
        }
    }

    // ---- AC-101a-C, AC-101a-D · the lockout ----------------------------------------------------

    /// <summary>AC-101a-C. Five failures lock; the sixth attempt fails with the right password.</summary>
    [Fact]
    public async Task Five_failures_lock_the_account_for_fifteen_minutes()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            (await SignIn(_engineerName, "wrong-password")).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized);
        }

        ((int)(await SignIn(_engineerName, Password)).StatusCode).Should().Be(
            423,
            "the sixth attempt fails even with the correct password");

        _clock.Advance(TimeSpan.FromMinutes(15));

        (await SignIn(_engineerName, Password)).StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            "after fifteen minutes the correct password succeeds");
    }

    /// <summary>AC-101a-C's third clause: the lockout is on the record as its own fact.</summary>
    [Fact]
    public async Task The_lockout_writes_its_own_audit_record()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            _ = await SignIn(_engineerName, "wrong-password");
        }

        (await EventsFor(_engineerName)).Should().Contain(
            AuditEventKind.AccountLockedOut,
            "\"the account was locked at 14:02\" is the fact somebody will ask about");
    }

    /// <summary>AC-101a-D. A success resets the run, so five further failures are needed.</summary>
    [Fact]
    public async Task A_success_resets_the_counter()
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            _ = await SignIn(_engineerName, "wrong-password");
        }

        (await SignIn(_engineerName, Password)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        for (int attempt = 0; attempt < 4; attempt++)
        {
            _ = await SignIn(_engineerName, "wrong-password");
        }

        (await SignIn(_engineerName, Password)).StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            "four failures after a success is four, not nine — the run restarted");
    }

    // ---- AC-101a-E · eight characters, no complexity -------------------------------------------

    /// <summary>AC-101a-E. Eight lower-case letters, no digit, no symbol, and it signs in.</summary>
    /// <remarks>
    /// Karim's reason is itself a requirement — "so site workers don't struggle to log in" — so the
    /// failure this guards against is a complexity rule added later by somebody being helpful.
    /// </remarks>
    [Fact]
    public async Task Eight_lower_case_letters_are_enough()
    {
        const string Simple = "abcdefgh";

        Simple.Length.Should().Be(User.MinimumPasswordLength);

        await using (KaffDbContext context = _database.CreateContext())
        {
            User user = await context.Users.SingleAsync(candidate => candidate.UserName == _ownerName, Ct);
            user.SetOwnPassword(PasswordHasher.Hash(Simple)).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(Ct);
        }

        (await SignIn(_ownerName, Simple)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---- AC-101a-H, AC-101a-I, AC-101a-N · the session dies when it should ---------------------

    /// <summary>AC-101a-H. Deactivation ends the open session and refuses the next sign-in.</summary>
    [Fact]
    public async Task A_deactivated_user_loses_the_open_session_and_cannot_sign_in_again()
    {
        HttpResponseMessage signedIn = await SignIn(_ownerName, Password);
        string cookie = Cookie(signedIn);

        (await GetWithCookie("/probe/company", cookie)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using (KaffDbContext context = _database.CreateContext())
        {
            User user = await context.Users.SingleAsync(candidate => candidate.Id == _owner, Ct);
            user.Deactivate(Now).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(Ct);
        }

        (await GetWithCookie("/probe/company", cookie)).StatusCode.Should().NotBe(
            HttpStatusCode.OK,
            "the very next request is refused — D-048 re-reads the user row on every one");

        (await SignIn(_ownerName, Password)).StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "and a fresh sign-in with the correct password is refused too, with the same generic "
            + "401 every other refusal gets");
    }

    /// <summary>
    /// AC-101a-I and AC-101a-N. A password change on one device kills the session on the other, and
    /// the mechanism is the stamp rather than anything about the token's own lifetime.
    /// </summary>
    [Fact]
    public async Task A_password_change_kills_the_session_on_the_other_device()
    {
        string deviceA = Cookie(await SignIn(_ownerName, Password));
        string deviceB = Cookie(await SignIn(_ownerName, Password));

        (await GetWithCookie("/probe/company", deviceB)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using (KaffDbContext context = _database.CreateContext())
        {
            User user = await context.Users.SingleAsync(candidate => candidate.Id == _owner, Ct);
            user.SetOwnPassword(PasswordHasher.Hash("a-brand-new-one")).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(Ct);
        }

        (await GetWithCookie("/probe/company", deviceA)).StatusCode.Should().NotBe(HttpStatusCode.OK);
        (await GetWithCookie("/probe/company", deviceB)).StatusCode.Should().NotBe(
            HttpStatusCode.OK,
            "rotating User.SecurityStamp invalidates every token in existence for that user at once "
            + "— D-051 N5, and there is no session table to revoke one device from");
    }

    /// <summary>
    /// Rule 10. An account deactivated before this session existed cannot sign in, and 🟡 gets the
    /// generic 401 rather than a key of its own.
    /// </summary>
    /// <remarks>
    /// <b>This assertion is narrower than it looks, and the narrowness is deliberate.</b> No ruling
    /// covers the inactive account. The story's i18n bullet names <c>errors.auth.account_inactive</c>
    /// and no criterion reaches it; <c>AC-101a-H</c> says only "refused". The generic 401 is what
    /// D-065's own reasoning gives — a distinct answer here is reachable from the username alone and
    /// announces that the account exists, which is exactly what case 5 refused for the
    /// subcontractor. It is recorded as a question in decisions.md D-084 rather than settled by this
    /// test: <b>if Nabil rules the other way, this line is what changes.</b>
    /// </remarks>
    [Fact]
    public async Task An_inactive_account_is_refused_like_a_stranger()
    {
        HttpResponseMessage response = await SignIn(_inactiveName, Password);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain(InvalidCredentials);
        response.Headers.Should().NotContain(header => header.Key == "Set-Cookie");
    }

    // ---- AC-101a-J · thirty idle minutes -------------------------------------------------------

    /// <summary>
    /// AC-101a-J, first half. A session nobody used for thirty minutes is refused.
    /// </summary>
    /// <remarks>
    /// The session is minted by a host whose clock is thirty-one minutes behind the real one, so the
    /// token really has expired by the time it is presented and the shipped validator — which uses
    /// the framework's clock, not this suite's — really does refuse it. Nothing here shortens
    /// <c>JwtOptions.InactivityMinutes</c>: the number under test is the shipped 30.
    /// </remarks>
    [Fact]
    public async Task A_session_idle_for_thirty_minutes_is_refused()
    {
        var past = new TestClock(TimeSpan.FromMinutes(-31));
        await using var host = new KaffApiFactory(
            _database.ConnectionString, useRealAuthentication: true, clock: past);

        using HttpClient client = host.CreateClient();

        HttpResponseMessage signedIn = await SignIn(_ownerName, Password, client);
        signedIn.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetWithCookie("/probe/company", Cookie(signedIn), client)).StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "thirty minutes of inactivity ends the session (rule 5, JwtOptions.InactivityMinutes)");
    }

    /// <summary>
    /// AC-101a-J, second half. <b>Activity slides the window</b> — a session used every twenty
    /// minutes for two hours is still valid, and the expiry it carries has moved by two hours.
    /// </summary>
    /// <remarks>
    /// The assertion is on the expiry moving, not merely on the requests succeeding: with sliding
    /// removed the requests would still succeed inside the first window, and only the final expiry
    /// tells the two implementations apart. An absolute thirty minutes signs a site engineer out in
    /// the middle of a daily log, which is what this criterion exists to prevent.
    /// </remarks>
    [Fact]
    public async Task Activity_slides_the_window()
    {
        HttpResponseMessage signedIn = await SignIn(_ownerName, Password);

        string cookie = Cookie(signedIn);
        long firstExpiry = Expiry(cookie);

        for (int twentyMinutes = 0; twentyMinutes < 6; twentyMinutes++)
        {
            _clock.Advance(TimeSpan.FromMinutes(20));

            HttpResponseMessage response = await GetWithCookie("/probe/company", cookie);

            response.StatusCode.Should().Be(
                HttpStatusCode.OK,
                "the session is used inside every window and must never lapse");

            cookie = Cookie(response);
        }

        Expiry(cookie).Should().BeGreaterThan(
            firstExpiry + (int)TimeSpan.FromHours(2).TotalSeconds - 60,
            "two hours of activity moved the inactivity window two hours forward. Without the slide "
            + "the session expires thirty minutes after sign-in whatever the user is doing");
    }

    // ---- AC-101a-O · what a failed sign-in records, and what it must never record --------------

    /// <summary>
    /// ⚠️ <b><c>AC-101a-O</c></b>. A sign-in against a username that does not exist writes a record
    /// carrying the connection address, the timestamp and <b>no subject</b> — and nowhere in it, in
    /// any column or any JSON, does the string the caller typed appear.
    /// </summary>
    /// <remarks>
    /// D-062 §3, Nabil: "strictly FORBID storing the typed input. Users frequently type their
    /// password into the username/email field by mistake." The assertion searches every text column
    /// of the row for both submitted strings, because the rule is about the record rather than about
    /// one field of it — <c>audit_records</c> is append-only by trigger, so a plaintext password
    /// written into it could never be removed.
    /// </remarks>
    [Fact]
    public async Task A_failed_sign_in_against_a_stranger_records_the_address_and_not_the_typed_string()
    {
        const string Typed = "MyActualPasswordTypedInTheWrongBox";
        string stranger = UniqueNames.Code("ghost");

        (await SignIn(stranger, Typed)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.Set<AuditRecord>()
            .Where(candidate => candidate.EventType == AuditEventKind.SignInFailedUnknownUser)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        record.EntityType.Should().Be(
            nameof(User),
            "a sign-in was attempted against a User — the kind is known even though the row is not");
        record.EntityId.Should().BeNull("there is no subject; D-063 §3 made the column nullable for this");
        record.ActorUserId.Should().BeNull("nobody authenticated");
        record.IpAddress.Should().Be(
            KaffApiFactory.TestRemoteAddress,
            "D-063 §2 and D-079 — the connection's address, never a header. Failed sign-ins are the "
            + "rows this column was added for");
        record.OccurredAt.Should().NotBe(default);
        record.RequestPath.Should().Be("/api/auth/sign-in");

        string everything = string.Join(
            ' ',
            record.EntityType,
            record.ActorDisplayName,
            record.BeforeJson,
            record.AfterJson,
            record.Reason,
            record.RequestPath,
            string.Join(',', record.ChangedProperties));

        everything.Should().NotContain(Typed, "the typed input is never stored — D-062 §3");
        everything.Should().NotContain(stranger, "nor the username as typed");
    }

    /// <summary>KAFF-101a audit section / TC-1-008: a successful sign-in is on the record.</summary>
    [Fact]
    public async Task A_successful_sign_in_names_the_user_the_time_and_the_path()
    {
        (await SignIn(_engineerName, Password)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.Set<AuditRecord>()
            .Where(candidate => candidate.EventType == AuditEventKind.SignedIn
                                && candidate.EntityId == _engineer)
            .OrderByDescending(candidate => candidate.OccurredAt)
            .FirstAsync(Ct);

        record.ActorUserId.Should().Be(_engineer, "the person who signed in is the actor");
        record.ActorRole.Should().Be(Role.SiteEngineer);
        record.RequestPath.Should().Be("/api/auth/sign-in");
        record.IpAddress.Should().Be(KaffApiFactory.TestRemoteAddress);
    }

    /// <summary>
    /// AC-101a-L. Neither the password, the hash nor the stamp appears in any audit record the
    /// sign-in path writes.
    /// </summary>
    [Fact]
    public async Task No_audit_record_the_door_writes_contains_the_credential()
    {
        string stamp = await StampOf(_engineer);

        _ = await SignIn(_engineerName, Password);
        _ = await SignIn(_engineerName, "wrong-password");

        await using KaffDbContext reader = _database.CreateBareContext();

        List<AuditRecord> records = await reader.Set<AuditRecord>()
            .Where(candidate => candidate.RequestPath == "/api/auth/sign-in")
            .ToListAsync(Ct);

        records.Should().NotBeEmpty();

        foreach (AuditRecord record in records)
        {
            string payload = (record.BeforeJson ?? string.Empty) + (record.AfterJson ?? string.Empty);

            payload.Should().NotContain(Password);
            payload.Should().NotContain("pbkdf2", "PasswordHash is [AuditRedacted]");
            payload.Should().NotContain(stamp, "SecurityStamp is [AuditRedacted] too");
        }
    }

    // ---- AC-101a-K · the session grants nothing by itself --------------------------------------

    /// <summary>
    /// AC-101a-K. A real session for an engineer assigned to no project reaches no project-scoped
    /// endpoint. <b>403 and <c>errors.auth.forbidden</c></b> — decisions.md D-080, which ruled the
    /// blanket key correct and corrected this criterion (commit <c>5a2c282</c>).
    /// </summary>
    /// <remarks>
    /// The existing coverage of this criterion runs through <see cref="TestAuthHandler"/>; this is
    /// the same assertion against a session the door actually minted, which is what the criterion
    /// says ("given a valid session").
    /// </remarks>
    [Fact]
    public async Task A_real_session_reaches_no_project_it_is_not_assigned_to()
    {
        string cookie = Cookie(await SignIn(_engineerName, Password));

        HttpResponseMessage response = await GetWithCookie($"/probe/projects/{_project}", cookie);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("errors.auth.forbidden");
    }

    // ---- The door acts for nobody --------------------------------------------------------------

    /// <summary>
    /// Signing in while already holding a session replaces it, and does not attribute the record to
    /// whoever the old cookie named.
    /// </summary>
    /// <remarks>
    /// Without discarding the inbound identity this is a 500: no gate runs on an anonymous endpoint,
    /// so the audit interceptor would build an actor from the token's claims with no verified role
    /// beside it, and <c>ck_audit_records_actor_is_named_completely</c> refuses a half-named actor
    /// outright. Reachable by any user who submits the sign-in form twice.
    /// </remarks>
    [Fact]
    public async Task Signing_in_again_while_holding_a_session_replaces_it()
    {
        string first = Cookie(await SignIn(_ownerName, Password));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in")
        {
            Content = JsonContent.Create(new { userName = _ownerName, password = Password }),
        };
        request.Headers.Add("Cookie", first);

        HttpResponseMessage second = await _client.SendAsync(request, Ct);

        second.StatusCode.Should().Be(HttpStatusCode.NoContent, "and not a 500 from a half-named actor");
        (await GetWithCookie("/probe/company", Cookie(second))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- Rule 16b · the minter is the guarantee ------------------------------------------------

    /// <summary>
    /// Rule 16b / D-063 §1. The guarantee lives in the function that mints a staff session, so a
    /// future door that forgets the rule cannot mint one anyway.
    /// </summary>
    /// <remarks>
    /// It is a programmer-error guard and it throws; it is not the user-facing path, which is the
    /// generic 401 asserted in <see cref="Five_different_refusals_are_one_answer"/>. Unreachable
    /// from this handler by construction — which is the point, and is why it is exercised directly.
    /// </remarks>
    [Fact]
    public void The_staff_session_minter_refuses_a_client_and_a_subcontractor()
    {
        var minter = _factory.Services.GetRequiredService<StaffSessionMinter>();

        foreach (Role role in new[] { Role.Client, Role.Subcontractor })
        {
            User user = role == Role.Client
                ? User.Create(UniqueNames.Code("mint"), "عميل", UniqueNames.Phone(), role, Now, clientId: Guid.NewGuid()).Value
                : User.Create(UniqueNames.Code("mint"), "مقاول", UniqueNames.Phone(), role, Now).Value;

            Action mint = () => minter.Issue(new DefaultHttpContext().Response, user, Now);

            mint.Should().Throw<InvalidOperationException>(
                "no staff session may exist for {0} — decisions.md D-062 §2 and D-063 §1",
                role);
        }
    }

    // ---- Helpers -------------------------------------------------------------------------------

    private Task<HttpResponseMessage> SignIn(string userName, string password, HttpClient? client = null) =>
        (client ?? _client).PostAsJsonAsync(
            "/api/auth/sign-in",
            new { userName, password },
            Ct);

    private async Task<HttpResponseMessage> GetWithCookie(string path, string cookie, HttpClient? client = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);

        return await (client ?? _client).SendAsync(request, Ct);
    }

    /// <summary>Drives the account into a lockout through the door itself, not by writing the row.</summary>
    private async Task Lock(string userName)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            _ = await SignIn(userName, "wrong-password");
        }
    }

    /// <summary>The fastest of three attempts, in stopwatch ticks. See the timing test's remarks.</summary>
    private long Fastest(string userName, string password)
    {
        long fastest = long.MaxValue;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            long started = Stopwatch.GetTimestamp();
            _ = SignIn(userName, password).GetAwaiter().GetResult();
            fastest = Math.Min(fastest, Stopwatch.GetTimestamp() - started);
        }

        return fastest;
    }

    /// <summary>
    /// Blanks the one field that differs between two identical refusals: the per-request trace id.
    /// </summary>
    /// <remarks>
    /// <b>It is not a discriminator and removing it does not weaken the assertion.</b> ASP.NET
    /// Core's problem-details writer stamps <c>traceId</c> from the current <c>Activity</c>, so it is
    /// different on two consecutive calls with the <i>same</i> username and the same password —
    /// which is exactly why it tells an attacker nothing about which of the five cases they hit.
    /// Everything else in the body is compared byte for byte, including the <c>type</c>, the
    /// <c>title</c>, the <c>code</c> and the <c>messageKey</c>.
    /// </remarks>
    private static string WithoutTraceId(string body) =>
        System.Text.RegularExpressions.Regex.Replace(
            body,
            "\"traceId\":\"[^\"]*\"",
            "\"traceId\":\"\"");

    /// <summary>The <c>name=value</c> pair, ready to go back as a <c>Cookie</c> header.</summary>
    private static string Cookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

    private static Dictionary<string, JsonElement> TokenClaims(string cookie)
    {
        string payload = cookie[(cookie.IndexOf('=', StringComparison.Ordinal) + 1)..].Split('.')[1];

        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            Convert.FromBase64String(payload.PadRight((payload.Length + 3) / 4 * 4, '=')
                .Replace('-', '+')
                .Replace('_', '/')))!;
    }

    private static long Expiry(string cookie) => TokenClaims(cookie)["exp"].GetInt64();

    private async Task<List<AuditEventKind>> EventsFor(string userName)
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        Guid userId = await reader.Users
            .Where(user => user.UserName == userName)
            .Select(user => user.Id)
            .SingleAsync(Ct);

        return await reader.Set<AuditRecord>()
            .Where(record => record.EntityId == userId && record.EventType != null)
            .Select(record => record.EventType!.Value)
            .ToListAsync(Ct);
    }

    private async Task<string> StampOf(Guid userId)
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

        Client client = Client.Create(
            UniqueNames.Code("SGN-C1"), "عميل الدخول", UniqueNames.Phone(), ClientKind.Corporate, Now).Value;

        Project project = Project.Create(
            UniqueNames.Code("SGN-P1"), "مشروع الدخول", client.Id, ContractType.LumpSum, Now).Value;

        User engineer = MakeUser(
            "sgn-engineer", Role.SiteEngineer, Department.Operations, OperationsSubDepartment.Technical);
        User owner = MakeUser("sgn-owner", Role.Owner);
        User locked = MakeUser("sgn-locked", Role.Finance, Department.Finance);
        User inactive = MakeUser("sgn-inactive", Role.Finance, Department.Finance);
        User noCredential = MakeUser("sgn-nocred", Role.Finance, Department.Finance);
        User portal = User.Create(
            UniqueNames.Code("sgn-client"), "عميل البوابة", UniqueNames.Phone(), Role.Client, Now,
            clientId: client.Id).Value;
        User subcontractor = MakeUser("sgn-sub", Role.Subcontractor);

        foreach (User credentialled in new[] { engineer, owner, locked, inactive, portal })
        {
            credentialled.SetOwnPassword(PasswordHasher.Hash(Password)).IsSuccess.Should().BeTrue();
        }

        inactive.Deactivate(Now).IsSuccess.Should().BeTrue();

        // The subcontractor never gets one: User.StorePasswordHash refuses the role, and
        // ck_users_subcontractor_cannot_log_in refuses it again at the database. That is the whole
        // reason the door must not answer them differently — there is no password to check, so a
        // distinct refusal could only ever be produced from the username alone.
        subcontractor.SetOwnPassword(PasswordHasher.Hash(Password)).IsFailure.Should().BeTrue();

        context.Clients.Add(client);
        context.Projects.Add(project);
        context.Users.AddRange(engineer, owner, locked, inactive, noCredential, portal, subcontractor);

        await context.SaveChangesAsync(Ct);

        _project = project.Id;
        _engineer = engineer.Id;
        _owner = owner.Id;

        _engineerName = engineer.UserName;
        _ownerName = owner.UserName;
        _clientName = portal.UserName;
        _subcontractorName = subcontractor.UserName;
        _lockedName = locked.UserName;
        _inactiveName = inactive.UserName;
        _noCredentialName = noCredential.UserName;
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
            subDepartment).Value;

    private static DateTimeOffset Now => new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
