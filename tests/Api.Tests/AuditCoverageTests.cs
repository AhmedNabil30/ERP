using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Domain.MasterData;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Kaff.Api.Tests;

/// <summary>
/// KAFF-118 — the claim that every state change in slice 1 is audited, made checkable in one place.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the story exists, in its own words:</b> <i>"the mechanism was inoperative for the whole of
/// slice 0 and nobody noticed."</i> decisions.md D-041 — <c>KaffJson.Build()</c> threw on first use,
/// so every state change would have failed rather than written a bad record, and <i>"the build was
/// clean, dotnet format was clean, and 51 tests passed against a component that could not execute
/// once."</i> This file is the guard on that class of silence.
/// </para>
/// <para>
/// <b>Most of KAFF-118's criteria were already discharged by the feature suites, and duplicating them
/// here would be worse than leaving them there</b> — two assertions of one rule drift, and the copy
/// is the one nobody updates. What this file adds is the part no feature suite can hold: the
/// structural claim that covers entities nobody has written yet, and the two negative criteria that
/// belong to no single feature. Where each criterion actually lives:
/// </para>
/// <list type="table">
/// <item><term>AC-118-A</term><description>the identity and assignment acts —
/// <c>CreateUserTests</c>, <c>MoveUserDepartmentTests</c>, <c>DeactivateUserTests</c>,
/// <c>ReactivateUserTests</c>, <c>AssignUserToProjectTests</c>,
/// <c>RevokeProjectAssignmentTests</c>, each asserting the record its own act leaves</description></item>
/// <item><term>AC-118-B</term><description>the client acts — <c>CreateClientTests</c>,
/// <c>EditClientTests</c>, <c>ArchiveClientTests</c>. <b>Executable since 2026-09-04</b>: this
/// criterion travelled with KAFF-119 when that story was deferred out of sprint 1, and 119, 121 and
/// 123 have all landed</description></item>
/// <item><term>AC-118-C</term><description><c>DeactivateUserTests</c> — four records, one
/// <c>CorrelationId</c></description></item>
/// <item><term>AC-118-D</term><description><c>ChangeUserRoleTests</c> — the same, for a role
/// change</description></item>
/// <item><term>AC-118-E</term><description><c>AuditMechanismTests</c> —
/// <c>An_event_and_an_entity_change_saved_together_share_one_correlation_id</c></description></item>
/// <item><term>AC-118-F</term><description><c>ChangePasswordTests</c> asserts the visible
/// <c>[redacted]</c> placeholder for <c>SetOwnPassword</c>, <c>DeactivateUserTests</c> for the
/// stamp</description></item>
/// <item><term>AC-118-G</term><description><c>DeactivateUserTests</c> — the reason stored
/// verbatim</description></item>
/// <item><term>AC-118-H</term><description><b>here</b> — no feature suite owns "a read writes
/// nothing", because it is a property of every read</description></item>
/// <item><term>AC-118-I</term><description><b>here</b>, and for the same reason</description></item>
/// <item><term>AC-118-J</term><description><c>ReactivateUserTests</c> —
/// <c>Twelve_audit_records_written_before_leaving_still_name_the_reactivated_user</c></description></item>
/// </list>
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class AuditCoverageTests : IAsyncLifetime
{
    private readonly PostgresDatabase _database;
    private KaffApiFactory _factory = null!;
    private HttpClient _client = null!;

    private Guid _owner;
    private Guid _marketing;
    private Guid _finance;

    public AuditCoverageTests(PostgresDatabase database) => _database = database;

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

    // ---- rule 2 · one mechanism, and it covers what has not been written yet -------------------

    /// <summary>
    /// Every entity in the model is audited, or is a named exemption with a reason.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the only assertion in the suite that covers slice 3.</b> The interceptor is
    /// opt-<i>out</i>: it audits every tracked entity unless the entity implements
    /// <see cref="IAuditExempt"/>
    /// [Verified: 2026-09-05 @ <c>src/Infrastructure/Persistence/Interceptors/AuditSaveChangesInterceptor.cs</c>
    /// -&gt; <c>WriteAuditRecords</c>], so a <c>Posting</c> added in slice 3 is audited from its first
    /// commit without anybody remembering to ask. What that design cannot defend against is somebody
    /// adding the interface — one word on a class declaration, in a diff about something else, and an
    /// entity leaves the trail with nothing to show for it.
    /// </para>
    /// <para>
    /// <b>A whitelist, not a search for suspect entities</b> (D-106). The exempt set is enumerated
    /// exactly: any new member fails this, whatever it is called and whatever slice it arrives in,
    /// and adding it to the list below is then a deliberate edit somebody has to justify — which is
    /// the same shape <c>EndpointPermissionCoverageTests</c>'s allow-list has, for the same reason.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Every_entity_is_audited_unless_it_is_a_named_exemption()
    {
        await using KaffDbContext context = _database.CreateBareContext();

        List<Type> exempt = [.. context.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(clrType => clrType.IsAssignableTo(typeof(IAuditExempt)))
            .Distinct()];

        exempt.Select(type => type.Name).Should().BeEquivalentTo(
            [nameof(AuditRecord)],
            "the audit trail cannot audit itself — a record of a record recurses, and the table is "
            + "append-only by database trigger rather than by the interceptor anyway (D-033). "
            + "EVERY OTHER member of this list is an entity that changes without leaving a trace, "
            + "which is the defect KAFF-118 exists to make impossible to introduce quietly");

        // And the model is not empty — an assertion over nothing passes, which is exactly the shape
        // of check D-041 was written about.
        context.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(clrType => clrType.IsAssignableTo(typeof(Entity)))
            .Should().HaveCountGreaterThan(
                5,
                "if the model stops being enumerable this test passes by describing nothing");
    }

    // ---- AC-118-H · a read writes nothing -----------------------------------------------------

    [Fact]
    public async Task Ten_reads_write_no_audit_record()
    {
        // Rule 6, and it is a property of every read rather than of any one screen. The criterion was
        // restated by SM-10 to name the reads the sprint actually had; the client list it could not
        // name then exists now (KAFF-124), so it is named here.
        long before = await CountAsync();

        for (int i = 0; i < 10; i++)
        {
            (await GetAsync("/api/auth/me", _marketing, Role.MarketingSales, Department.Marketing))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            (await GetAsync("/api/clients?status=all", _marketing, Role.MarketingSales, Department.Marketing))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await CountAsync()).Should().Be(
            before,
            "twenty reads wrote a record between them — reads write nothing, and a trail that grows "
            + "when nothing changed is a trail nobody can read a change out of");

        // The positive control, and it is not decoration. "The count did not change" is satisfied by
        // a counter that cannot change — a broken query, a table read from the wrong schema, an
        // interceptor that stopped running — and every one of those makes this test pass louder
        // rather than fail. One real write has to move it, in the same method, or the assertion above
        // is describing nothing. This is D-041's lesson stated as a test: the danger is not a check
        // that fails, it is a check that cannot.
        (await PostClientAsync(_marketing, Role.MarketingSales, Department.Marketing, "شركة الفجر"))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await CountAsync()).Should().BeGreaterThan(before, "one real write moves the counter");
    }

    // ---- AC-118-I · a refused write writes nothing ---------------------------------------------

    /// <summary>
    /// A write refused by a domain rule, and one refused by the permission gate, both leave the table
    /// exactly as they found it.
    /// </summary>
    /// <remarks>
    /// <b>Two refusals, because they are refused in two different places.</b> The first never reaches
    /// <c>SaveChangesAsync</c> — <c>Client.Create</c> returns a failed <c>Result</c> and the handler
    /// returns a Problem. The second never reaches the handler at all. A half-record for either would
    /// say a change happened that did not, and the trail is what slice 3's money reconciles against.
    /// </remarks>
    [Fact]
    public async Task A_refused_write_writes_no_audit_record()
    {
        long before = await CountAsync();

        // Refused by the domain: Client.Create will not take a blank name.
        HttpResponseMessage refusedByRule = await PostClientAsync(
            _marketing,
            Role.MarketingSales,
            Department.Marketing,
            name: "   ");

        refusedByRule.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await CountAsync()).Should().Be(before, "a refused creation is not a creation");

        // Refused by the gate: Finance holds no ClientManage.
        HttpResponseMessage refusedByGate = await PostClientAsync(
            _finance,
            Role.Finance,
            Department.Finance,
            name: "شركة لا يجوز لها أن توجد");

        refusedByGate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await CountAsync()).Should().Be(
            before,
            "a refusal by the gate writes nothing either — and if a security event is ever added for "
            + "one, it is an Occurred event with no entity, not a Created record for a client that "
            + "does not exist");

        // The same positive control, for the same reason: an unchanged count proves nothing unless
        // the accepted version of the very same request changes it.
        (await PostClientAsync(_marketing, Role.MarketingSales, Department.Marketing, "شركة الفجر"))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await CountAsync()).Should().BeGreaterThan(
            before,
            "the request differed from the two above only in who sent it and what it named — if this "
            + "does not move the counter, neither did anything else this test measured");
    }

    // ---- helpers -------------------------------------------------------------------------------

    private async Task<long> CountAsync()
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        return await reader.AuditRecords.LongCountAsync(Ct);
    }

    private async Task<HttpResponseMessage> GetAsync(
        string route,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        await StampAsync(request, actorId, actorRole, actorDepartment);

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> PostClientAsync(
        Guid actorId,
        Role actorRole,
        Department? actorDepartment,
        string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/clients")
        {
            Content = JsonContent.Create(new
            {
                name,
                phone = UniqueNames.Phone().Entered,
                kind = nameof(ClientKind.Corporate),
                acknowledgedDuplicatePhone = false,
            }),
        };

        await StampAsync(request, actorId, actorRole, actorDepartment);

        return await _client.SendAsync(request, Ct);
    }

    private async Task StampAsync(
        HttpRequestMessage request,
        Guid actorId,
        Role actorRole,
        Department? actorDepartment)
    {
        request.Headers.Add(TestAuthHandler.UserIdHeader, actorId.ToString());
        request.Headers.Add(TestAuthHandler.RoleHeader, actorRole.ToString());
        request.Headers.Add(TestAuthHandler.SecurityStampHeader, await CurrentStampAsync(actorId));

        if (actorDepartment is not null)
        {
            request.Headers.Add(TestAuthHandler.DepartmentHeader, actorDepartment.Value.ToString());
        }
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

        User owner = MakeUser("aud-owner", Role.Owner);
        User marketing = MakeUser("aud-marketing", Role.MarketingSales, Department.Marketing);
        User finance = MakeUser("aud-finance", Role.Finance, Department.Finance);

        context.Users.AddRange(owner, marketing, finance);

        await context.SaveChangesAsync(Ct);

        _owner = owner.Id;
        _marketing = marketing.Id;
        _finance = finance.Id;
    }

    private static User MakeUser(string userName, Role role, Department? department = null)
        => User.Create(
            UniqueNames.Code(userName), userName, UniqueNames.Phone(), role, Now, department, null, null).Value;

    private static DateTimeOffset Now => new(2026, 9, 5, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
