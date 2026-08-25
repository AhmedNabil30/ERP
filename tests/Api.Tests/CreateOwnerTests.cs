using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-100 — <c>POST /api/setup</c> and <c>GET /api/setup</c>, the one-time bootstrap of the first
/// Owner.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not <see cref="Kaff.Api.Tests.Infrastructure.DatabaseCollection"/>.</b> Every other
/// test class in this suite shares one database for the whole run
/// [Verified: 2026-08-26 @ <c>UniqueNames.cs</c> -> the remarks on why names must be unique
/// process-wide], which is fine for tests that never assume the <c>users</c> table is empty. This
/// story's entire premise is "given a database with no users" (AC-100-A through E), and a table dozens
/// of other test classes have already seeded cannot honestly stand in for that.
/// </para>
/// <para>
/// <b><see cref="Fixture"/> creates one dedicated, private database for this class alone</b> — once,
/// not once per test method. A first version created and dropped a whole PostgreSQL database per
/// <c>[Fact]</c>; run concurrently with the shared <c>postgres</c> collection (xUnit runs collections
/// in parallel, and a class with no <c>[Collection]</c> gets its own), that churn measurably
/// destabilised an unrelated test elsewhere in the suite — <c>AssignUserToProjectTests</c> intermittently
/// saw <c>NotAuthenticated</c> where it expected <c>404</c>, reproduced twice with this class's original
/// shape present and absent zero times in three runs without it. <c>Fixture.ResetAsync</c>'s
/// <c>TRUNCATE</c> gives every test the same empty-table guarantee for a fraction of the cost.
/// </para>
/// <para>
/// <b>AC-100-F and part of AC-100-I are out of reach from this file.</b> Signing in (KAFF-101a) and
/// <c>GET /api/auth/me</c> (KAFF-105a) do not exist yet — the same gap D-081 recorded for KAFF-112's
/// AC-112-D/F. What this file proves instead: the created row's <see cref="User.MustChangePassword"/>
/// is false, which is the fact those later endpoints will read. The Arabic/RTL screen (AC-100-I) is
/// Frontend's; no screen exists under <c>src/Web</c> yet.
/// </para>
/// </remarks>
public sealed class CreateOwnerTests : IClassFixture<CreateOwnerTests.Fixture>, IAsyncLifetime
{
    private readonly Fixture _fixture;
    private HttpClient _client = null!;

    public CreateOwnerTests(Fixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        await _fixture.ResetAsync();
        _client = _fixture.Factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>One database and one host for the whole class, created once and torn down once.</summary>
    public sealed class Fixture : IAsyncLifetime
    {
        public PostgresDatabase Database { get; } = new();

        public KaffApiFactory Factory { get; private set; } = null!;

        public async ValueTask InitializeAsync()
        {
            await Database.InitializeAsync();
            Factory = new KaffApiFactory(Database.ConnectionString);
        }

        public async ValueTask DisposeAsync()
        {
            await Factory.DisposeAsync();
            await Database.DisposeAsync();
        }

        /// <summary>
        /// Puts <c>users</c> back to "a database with no users" — every AC-100 precondition — without
        /// the cost of a fresh database per test.
        /// </summary>
        /// <remarks>
        /// <c>audit_records</c> is deliberately not touched here: it is append-only and
        /// trigger-protected (<c>trg_audit_records_no_truncate</c>), and a <c>TRUNCATE</c> against it
        /// is refused by the database — CLAUDE.md's own rule, working exactly as intended against a
        /// test that tried to take a shortcut through it. Tests that need an audit count assert the
        /// change this test made, not the table's total, for that reason.
        /// </remarks>
        public async Task ResetAsync()
        {
            await using KaffDbContext context = Database.CreateBareContext();

#pragma warning disable EF1002 // Fixed SQL, no user input.
            await context.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE users CASCADE;",
                TestContext.Current.CancellationToken);
#pragma warning restore EF1002
        }
    }

    // ---- AC-100-A · an empty system offers the screen, and one Owner comes out of it ----------

    [Fact]
    public async Task An_empty_system_mints_exactly_one_owner()
    {
        string userName = UniqueNames.Code("ac100a");

        HttpResponseMessage response = await SetupAsync(Body(userName));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        User owner = await ReadUserAsync(userName);

        owner.Role.Should().Be(Role.Owner);
        owner.Department.Should().BeNull("rule 2 — the Owner is not one of §9's four departments");
        owner.IsActive.Should().BeTrue();
        owner.IsBootstrapOwner.Should().BeTrue();

        (await UserCountAsync()).Should().Be(1, "exactly one User exists");
    }

    /// <summary>
    /// AC-100-A's audit half, and D-051 (Q31)'s entire reason for Shape B: "my name and account
    /// creation date must appear naturally in the Audit Trail from day one."
    /// </summary>
    [Fact]
    public async Task The_creation_leaves_an_audit_record_naming_the_new_owner_as_its_own_actor()
    {
        string userName = UniqueNames.Code("ac100a-audit");

        (await SetupAsync(Body(userName))).StatusCode.Should().Be(HttpStatusCode.Created);

        User owner = await ReadUserAsync(userName);

        await using KaffDbContext reader = _fixture.Database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords.SingleAsync(
            candidate => candidate.EntityId == owner.Id && candidate.Action == AuditAction.Created,
            Ct);

        record.EntityType.Should().Be(nameof(User));
        record.ActorUserId.Should().Be(owner.Id, "D-061 — the new Owner is its own actor, never null");
        record.ActorRole.Should().Be(Role.Owner);
        record.OccurredAt.Should().NotBe(default);
        record.GrantPath.Should().BeNull("UserManage is company-wide: no project, no access policy");
    }

    // ---- AC-100-F (partial) · no forced change -------------------------------------------------

    [Fact]
    public async Task The_owner_is_not_forced_to_change_the_password_he_typed()
    {
        string userName = UniqueNames.Code("ac100f");

        (await SetupAsync(Body(userName))).StatusCode.Should().Be(HttpStatusCode.Created);

        User owner = await ReadUserAsync(userName);

        owner.MustChangePassword.Should().BeFalse(
            "rule 7/8 — he typed it himself, so SetOwnPassword ran, not SetTemporaryPassword");
    }

    // ---- AC-100-B · it cannot happen twice ------------------------------------------------------

    [Fact]
    public async Task A_second_call_against_an_initialised_system_is_refused()
    {
        (await SetupAsync(Body(UniqueNames.Code("ac100b-first")))).StatusCode.Should().Be(HttpStatusCode.Created);

        // audit_records is append-only and shared across every test in this class (it cannot be
        // truncated, by design — see Fixture.ResetAsync), so the count after the winning call is the
        // baseline the refusal must not move, not an absolute total.
        int auditRecordsAfterFirstCall = await AuditRecordCountAsync();

        string secondUserName = UniqueNames.Code("ac100b-second");
        HttpResponseMessage second = await SetupAsync(Body(secondUserName));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(second)).Should().Be("errors.setup.already_completed");

        (await UserExistsAsync(secondUserName)).Should().BeFalse("no second user is created");
        (await UserCountAsync()).Should().Be(1);
        (await AuditRecordCountAsync()).Should().Be(
            auditRecordsAfterFirstCall, "no audit record is written for the refusal");
    }

    /// <summary>
    /// AC-100-B says "of any role, active or not" — a non-Owner row must refuse the screen exactly as
    /// an Owner row would. Seeded directly through <c>User.Create</c>, not through the endpoint, so
    /// this is independent of whether bootstrap itself works.
    /// </summary>
    [Fact]
    public async Task Any_existing_user_of_any_role_refuses_a_second_setup()
    {
        await using (KaffDbContext seed = _fixture.Database.CreateBareContext())
        {
            User someone = User.Create(
                UniqueNames.Code("ac100b-seed"),
                "Seeded User",
                UniqueNames.Phone(),
                Role.Finance,
                Now,
                Department.Finance).Value;

            seed.Users.Add(someone);
            await seed.SaveChangesAsync(Ct);
        }

        HttpResponseMessage response = await SetupAsync(Body(UniqueNames.Code("ac100b-attempt")));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be("errors.setup.already_completed");
        (await UserCountAsync()).Should().Be(1, "the seeded row, and nothing the refused call added");
    }

    // ---- AC-100-C · two simultaneous requests produce one Owner ---------------------------------

    /// <summary>
    /// <b>Watched to fail</b>: with <c>ux_users_bootstrap_owner_once</c> commented out of
    /// <c>IdentityConfigurations.cs</c>, this test failed with two <c>201</c>s and
    /// <c>UserCountAsync() == 2</c> — confirmed 2026-08-26, then the index was restored and the suite
    /// re-run green. The database constraint is what this test depends on, not luck: whichever of the
    /// two requests' <c>SaveChangesAsync</c> commits second gets a real Postgres unique-violation,
    /// regardless of how the two requests' courtesy <c>Users.AnyAsync()</c> reads interleaved.
    /// </summary>
    [Fact]
    public async Task Two_concurrent_requests_produce_exactly_one_owner_and_one_refusal()
    {
        // audit_records is append-only and shared across every test in this class (see
        // Fixture.ResetAsync's remarks), so "exactly one Created record" is asserted as a delta from
        // this test's own baseline, not as the table's total.
        int auditRecordsBefore = await AuditRecordCountAsync();

        using var first = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/setup", UriKind.Relative))
        {
            Content = JsonContent.Create(Body(UniqueNames.Code("ac100c-a"), fullName: "Race Contestant A")),
        };

        using var second = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/setup", UriKind.Relative))
        {
            Content = JsonContent.Create(Body(UniqueNames.Code("ac100c-b"), fullName: "Race Contestant B")),
        };

        HttpResponseMessage[] responses = await Task.WhenAll(
            _client.SendAsync(first, Ct),
            _client.SendAsync(second, Ct));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).Should().Be(
            1, "exactly one request succeeds");
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(
            1, "exactly one request is refused");

        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }

        (await UserCountAsync()).Should().Be(1, "never two Owners");
        (await AuditRecordCountAsync()).Should().Be(
            auditRecordsBefore + 1, "exactly one Created record, for the winner alone");
    }

    // ---- AC-100-E · deactivating the Owner does not re-open it -----------------------------------

    [Fact]
    public async Task A_deactivated_owner_still_refuses_a_second_setup()
    {
        string userName = UniqueNames.Code("ac100e");
        (await SetupAsync(Body(userName))).StatusCode.Should().Be(HttpStatusCode.Created);

        await using (KaffDbContext context = _fixture.Database.CreateBareContext())
        {
            User owner = await context.Users.SingleAsync(u => u.UserName == userName, Ct);
            owner.Deactivate(Now).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(Ct);
        }

        HttpResponseMessage response = await SetupAsync(Body(UniqueNames.Code("ac100e-attempt")));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await MessageKeyAsync(response)).Should().Be(
            "errors.setup.already_completed", "rule 4 counts users, not active users");
    }

    // ---- AC-100-G · no shared login survives review ---------------------------------------------

    [Theory]
    [InlineData("admin")]
    [InlineData("root")]
    [InlineData("kaff")]
    public async Task A_reserved_user_name_is_refused(string reserved)
    {
        HttpResponseMessage response = await SetupAsync(Body(reserved));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.username_reserved");
        (await UserCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task An_empty_full_name_is_refused()
    {
        HttpResponseMessage response = await SetupAsync(Body(UniqueNames.Code("ac100g"), fullName: " "));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageKeyAsync(response)).Should().Be("errors.identity.full_name_required");
        (await UserCountAsync()).Should().Be(0);
    }

    // ---- AC-100-H · the password never leaves the database ---------------------------------------

    [Fact]
    public async Task The_password_appears_in_neither_the_response_nor_the_audit_record()
    {
        const string Password = "correct-horse-battery";
        string userName = UniqueNames.Code("ac100h");

        HttpResponseMessage response = await SetupAsync(Body(userName, password: Password));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        string body = await response.Content.ReadAsStringAsync(Ct);
        body.Should().NotContain(Password);

        User owner = await ReadUserAsync(userName);
        owner.PasswordHash.Should().NotBeNull();
        owner.PasswordHash.Should().NotContain(Password, "only a hash is stored");

        await using KaffDbContext reader = _fixture.Database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords.SingleAsync(
            candidate => candidate.EntityId == owner.Id && candidate.Action == AuditAction.Created, Ct);

        record.AfterJson.Should().NotBeNull();
        record.AfterJson.Should().NotContain(Password);

        using JsonDocument after = JsonDocument.Parse(record.AfterJson!);
        after.RootElement.GetProperty(nameof(User.PasswordHash)).GetString()
            .Should().Be(AuditRedactedAttribute.Placeholder, "a credential must never enter the trail");
        after.RootElement.GetProperty(nameof(User.SecurityStamp)).GetString()
            .Should().Be(AuditRedactedAttribute.Placeholder);
    }

    // ---- GET /api/setup · S-002's availability probe ---------------------------------------------

    [Fact]
    public async Task Availability_is_true_while_the_table_is_empty_and_false_for_ever_after()
    {
        HttpResponseMessage before = await _client.GetAsync(new Uri("/api/setup", UriKind.Relative), Ct);
        before.StatusCode.Should().Be(HttpStatusCode.OK);
        (await AvailableAsync(before)).Should().BeTrue();

        (await SetupAsync(Body(UniqueNames.Code("ac-avail")))).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage after = await _client.GetAsync(new Uri("/api/setup", UriKind.Relative), Ct);
        after.StatusCode.Should().Be(HttpStatusCode.OK);
        (await AvailableAsync(after)).Should().BeFalse("rule 5 — the emptiness test, never a flag");
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static object Body(
        string userName,
        string fullName = "مستخدم الاختبار",
        string? password = "password123") => new
        {
            fullName,
            userName,
            phone = UniqueNames.Phone().Entered,
            password,
        };

    private async Task<HttpResponseMessage> SetupAsync(object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/setup", UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<bool> AvailableAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.GetProperty("available").GetBoolean();
    }

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

        await using KaffDbContext reader = _fixture.Database.CreateBareContext();

        return await reader.Users.SingleAsync(user => user.UserName == stored, Ct);
    }

    private async Task<bool> UserExistsAsync(string userName)
    {
        string stored = AsStored(userName);

        await using KaffDbContext reader = _fixture.Database.CreateBareContext();

        return await reader.Users.AnyAsync(user => user.UserName == stored, Ct);
    }

    /// <summary>The form <c>User.Create</c> stores — trimmed and lower-cased. See CreateUserTests.AsStored.</summary>
#pragma warning disable CA1308
    private static string AsStored(string userName) => userName.Trim().ToLowerInvariant();
#pragma warning restore CA1308

    private async Task<int> UserCountAsync()
    {
        await using KaffDbContext reader = _fixture.Database.CreateBareContext();
        return await reader.Users.CountAsync(Ct);
    }

    private async Task<int> AuditRecordCountAsync()
    {
        await using KaffDbContext reader = _fixture.Database.CreateBareContext();
        return await reader.AuditRecords.CountAsync(Ct);
    }

    private static DateTimeOffset Now => new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
