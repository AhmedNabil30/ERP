using Kaff.Api.Tests.Infrastructure;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;
using Kaff.Infrastructure.Auditing;
using Kaff.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kaff.Api.Tests;

/// <summary>
/// One mechanism, invoked centrally. CLAUDE.md: "Every state change writes an audit record."
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AuditMechanismTests
{
    private readonly PostgresDatabase _database;

    public AuditMechanismTests(PostgresDatabase database) => _database = database;

    private static DateTimeOffset Now => new(2026, 4, 1, 7, 0, 0, TimeSpan.Zero);

    /// <summary>Ambient test cancellation, threaded through every database call.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// The audit context of a request that passed the gate — which is every authenticated request
    /// the application serves.
    /// </summary>
    /// <remarks>
    /// <c>PermissionAuthorizationHandler</c> hands the row it read out of the users table to
    /// <see cref="IAuditContext.ActorVerifiedAs"/> on every grant, and D-069 makes an endpoint with
    /// no gate a build failure. These tests invoke the interceptor without an HTTP request, so the
    /// gate cannot run and the actor has to be stated. Stated here rather than defaulted inside
    /// <c>PostgresDatabase</c>, because a harness that supplies a verified actor to every save could
    /// no longer exhibit a save that lacks one — which is
    /// <see cref="An_actor_is_named_completely_or_not_at_all"/>'s whole subject. See decisions.md
    /// D-075.
    /// </remarks>
    private static AuditContext Gated(StubCurrentUser actor)
    {
        var auditContext = new AuditContext();
        auditContext.ActorVerifiedAs(new AuditActor(actor.UserId, actor.DisplayName, actor.Role));
        return auditContext;
    }

    [Fact]
    public async Task Creating_an_entity_writes_an_audit_record_without_the_feature_asking()
    {
        var actor = new StubCurrentUser();
        AuditContext auditContext = Gated(actor);

        Domain.MasterData.Client client = NewClient();

        await using (KaffDbContext context = _database.CreateContext(actor, auditContext))
        {
            context.Clients.Add(client);
            await context.SaveChangesAsync(Ct);
        }

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .SingleAsync(entry => entry.EntityId == client.Id, Ct);

        record.Action.Should().Be(AuditAction.Created);
        record.EntityType.Should().Be(nameof(Domain.MasterData.Client));
        record.ActorUserId.Should().Be(actor.UserId);
        record.AfterJson.Should().NotBeNull();
        record.BeforeJson.Should().BeNull();
        record.CorrelationId.Should().Be(auditContext.CorrelationId);
    }

    [Fact]
    public async Task Modifying_an_entity_records_before_and_after_for_the_changed_properties_only()
    {
        var actor = new StubCurrentUser();
        Domain.MasterData.Client client = NewClient();

        await using (KaffDbContext context = _database.CreateContext(actor, Gated(actor)))
        {
            context.Clients.Add(client);
            await context.SaveChangesAsync(Ct);
        }

        AuditContext auditContext = Gated(actor);
        auditContext.SetReason("العميل طلب تحديث بيانات التواصل");

        await using (KaffDbContext context = _database.CreateContext(actor, auditContext))
        {
            Domain.MasterData.Client tracked = await context.Clients.SingleAsync(c => c.Id == client.Id, Ct);
            tracked.SetContactDetails(null, "karim@example.com", null, null);
            await context.SaveChangesAsync(Ct);
        }

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(entry => entry.EntityId == client.Id && entry.Action == AuditAction.Modified)
            .SingleAsync(Ct);

        record.ChangedProperties.Should().Contain(nameof(Domain.MasterData.Client.Email));
        record.BeforeJson.Should().NotBeNull();
        record.AfterJson.Should().NotBeNull();

        // spec.md §7 requires a stored reason wherever a flow demands one.
        record.Reason.Should().Be("العميل طلب تحديث بيانات التواصل");
    }

    [Fact]
    public async Task A_credential_change_is_recorded_without_recording_the_credential()
    {
        var actor = new StubCurrentUser();

        User user = User.Create(
            UniqueNames.Code("audit-user"),
            "Audit User",
            UniqueNames.Phone(),
            Role.Finance,
            Now,
            Department.Finance).Value;

        await using (KaffDbContext context = _database.CreateContext(actor, Gated(actor)))
        {
            context.Users.Add(user);
            await context.SaveChangesAsync(Ct);
        }

        await using (KaffDbContext context = _database.CreateContext(actor, Gated(actor)))
        {
            User tracked = await context.Users.SingleAsync(u => u.Id == user.Id, Ct);
            tracked.SetOwnPassword("a-hash-that-must-never-reach-the-audit-trail");
            await context.SaveChangesAsync(Ct);
        }

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords
            .Where(entry => entry.EntityId == user.Id && entry.Action == AuditAction.Modified)
            .SingleAsync(Ct);

        record.ChangedProperties.Should().Contain(nameof(User.PasswordHash));
        record.AfterJson.Should().NotContain("a-hash-that-must-never-reach-the-audit-trail");
        record.AfterJson.Should().Contain(AuditRedactedAttribute.Placeholder);
    }

    [Fact]
    public async Task An_audit_record_cannot_be_changed_afterwards()
    {
        var actor = new StubCurrentUser();
        Domain.MasterData.Client client = NewClient();

        await using (KaffDbContext context = _database.CreateContext(actor, Gated(actor)))
        {
            context.Clients.Add(client);
            await context.SaveChangesAsync(Ct);
        }

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords.SingleAsync(entry => entry.EntityId == client.Id, Ct);

        await DatabaseGuard.RefusesAsync(
            () => reader.Database.ExecuteSqlAsync(
                $"UPDATE audit_records SET reason = 'rewritten' WHERE id = {record.Id}",
                Ct),
            DatabaseGuard.AppendOnly);
    }

    /// <summary>
    /// The whole point of decisions.md D-061. Sign-out (KAFF-102) touches no row at all, so the
    /// change tracker has nothing for the interceptor to find — and this asserts both halves of the
    /// mechanism working: that EF still invokes the interceptor on an empty change tracker, and that
    /// a declared event becomes a record without a handler constructing one.
    /// </summary>
    [Fact]
    public async Task An_event_that_changes_no_entity_still_writes_a_record()
    {
        var actor = new StubCurrentUser();
        AuditContext auditContext = Gated(actor);
        Guid subjectId = Guid.CreateVersion7();

        await using (KaffDbContext context = _database.CreateContext(actor, auditContext))
        {
            auditContext.Record<User>(AuditEventKind.SignedOut, subjectId);

            // Nothing is tracked. Under the entity-state mechanism alone this saves nothing.
            await context.SaveChangesAsync(Ct);
        }

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords.SingleAsync(entry => entry.EntityId == subjectId, Ct);

        record.Action.Should().Be(AuditAction.Occurred);
        record.EventType.Should().Be(AuditEventKind.SignedOut);
        record.EntityType.Should().Be(nameof(User));
        record.ActorUserId.Should().Be(actor.UserId);
        record.CorrelationId.Should().Be(auditContext.CorrelationId);
        record.BeforeJson.Should().BeNull();
        record.AfterJson.Should().BeNull();
    }

    /// <summary>
    /// One save is one story whichever source the records came from — KAFF-118 AC-118-E, which does
    /// not stop applying because half the records describe no entity.
    /// </summary>
    [Fact]
    public async Task An_event_and_an_entity_change_saved_together_share_one_correlation_id()
    {
        var actor = new StubCurrentUser();
        AuditContext auditContext = Gated(actor);
        Domain.MasterData.Client client = NewClient();

        await using (KaffDbContext context = _database.CreateContext(actor, auditContext))
        {
            context.Clients.Add(client);
            auditContext.Record<User>(AuditEventKind.SignedIn, actor.UserId!.Value);
            await context.SaveChangesAsync(Ct);
        }

        await using KaffDbContext reader = _database.CreateBareContext();

        List<AuditRecord> records = await reader.AuditRecords
            .Where(entry => entry.CorrelationId == auditContext.CorrelationId)
            .ToListAsync(Ct);

        records.Should().HaveCount(2);
        records.Should().ContainSingle(entry => entry.EventType == AuditEventKind.SignedIn);
        records.Should().ContainSingle(entry => entry.EntityId == client.Id && entry.EventType == null);
    }

    /// <summary>
    /// Cited by the constraint's own comment in <c>AuditConfiguration</c>. A row is an entity change
    /// or an event; the two columns cannot drift apart, and the database is what says so.
    /// </summary>
    [Fact]
    public async Task Only_an_Occurred_record_carries_an_event_type()
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        await DatabaseGuard.RefusesAsync(
            () => InsertRawAuditRecordAsync(reader, action: "Modified", eventType: "SignedOut"),
            "ck_audit_records_event_shape");

        await DatabaseGuard.RefusesAsync(
            () => InsertRawAuditRecordAsync(reader, action: "Occurred", eventType: null),
            "ck_audit_records_event_shape");
    }

    /// <summary>
    /// KAFF-100. The setup endpoint is anonymous by definition and the actor is created by the very
    /// transaction being audited, so the request has no identity to read — but D-051 (Q31) rejected a
    /// seed precisely so the first row of the trail would not name nobody.
    /// </summary>
    [Fact]
    public async Task A_request_with_no_identity_may_name_the_actor_it_is_creating()
    {
        var anonymous = new SystemCurrentUser();
        var auditContext = new AuditContext();

        User owner = NewOwner();
        auditContext.AttributeTo(new AuditActor(owner.Id, owner.FullName, owner.Role));

        await using (KaffDbContext context = _database.CreateContext(anonymous, auditContext))
        {
            context.Users.Add(owner);
            await context.SaveChangesAsync(Ct);
        }

        await using KaffDbContext reader = _database.CreateBareContext();

        AuditRecord record = await reader.AuditRecords.SingleAsync(entry => entry.EntityId == owner.Id, Ct);

        record.Action.Should().Be(AuditAction.Created);
        record.ActorUserId.Should().Be(owner.Id);
        record.ActorDisplayName.Should().Be(owner.FullName);
        record.ActorRole.Should().Be(Role.Owner);
    }

    /// <summary>
    /// The other half of the same rule, and the one that matters: an actor override on a request that
    /// already has an identity is impersonation written into a table nobody can correct afterwards.
    /// </summary>
    [Fact]
    public async Task An_authenticated_request_may_not_name_a_different_actor()
    {
        var actor = new StubCurrentUser();
        var auditContext = new AuditContext();
        auditContext.AttributeTo(new AuditActor(Guid.CreateVersion7(), "somebody else", Role.Owner));

        await using KaffDbContext context = _database.CreateContext(actor, auditContext);
        context.Clients.Add(NewClient());

        Func<Task> save = () => context.SaveChangesAsync(Ct);

        await save.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// KAFF-116, and the reason the rule is a constraint rather than a line in the interceptor: the
    /// interceptor is one writer today and the table outlives it. Both halves are refused by the
    /// database — a path over no project, and the one value that means "refused".
    /// </summary>
    [Fact]
    public async Task A_grant_path_is_refused_without_a_project_and_may_never_be_None()
    {
        await using KaffDbContext reader = _database.CreateBareContext();

        // A company-wide act reached no project through any policy, so it can name no authority over
        // one. AC-116-E's "not OwnerGlobal by default", said where it cannot be argued with.
        await DatabaseGuard.RefusesAsync(
            () => InsertRawAuditRecordAsync(
                reader, action: "Modified", eventType: null, projectId: null, grantPath: "OwnerGlobal"),
            "ck_audit_records_grant_path");

        // None is what a refusal carries, and a refusal writes no record at all.
        await DatabaseGuard.RefusesAsync(
            () => InsertRawAuditRecordAsync(
                reader, action: "Modified", eventType: null, projectId: Guid.CreateVersion7(), grantPath: "None"),
            "ck_audit_records_grant_path");
    }

    /// <summary>
    /// An actor is named completely or not at all, and the database is what says so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// decisions.md D-075. <c>ActorRole</c> cannot be <c>IsRequired()</c> — the one genuinely
    /// roleless actor is work outside a request, which names no user either — so the pairing is a
    /// check constraint or it is nothing.
    /// </para>
    /// <para>
    /// <b>The first half is the reachable one, and it is reachable past the application guard.</b>
    /// <c>AuditContext.FullyNamed</c> refuses a half-named actor on both channels that
    /// <i>declare</i> one, but <c>AuditSaveChangesInterceptor.ResolveActor</c> falls back to
    /// constructing <c>(user id, display name, null role)</c> directly when no gate ran on the
    /// request — it passes through neither channel, so no application guard sees it. This save is
    /// exactly that shape, and before the constraint existed it committed silently into a table that
    /// is append-only and no-truncate by trigger.
    /// </para>
    /// <para>
    /// The second half is the mirror, written raw because nothing constructs it: a role over nobody.
    /// It is refused for the same reason the grant-path constraint refuses a path over no project —
    /// the interceptor is one writer today and the table outlives it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task An_actor_is_named_completely_or_not_at_all()
    {
        var actor = new StubCurrentUser();

        await using (KaffDbContext context = _database.CreateContext(actor, new AuditContext()))
        {
            context.Clients.Add(NewClient());

            await DatabaseGuard.RefusesAsync(
                () => context.SaveChangesAsync(Ct),
                "ck_audit_records_actor_is_named_completely");
        }

        await using KaffDbContext reader = _database.CreateBareContext();

        await DatabaseGuard.RefusesAsync(
            () => InsertRawAuditRecordAsync(
                reader, action: "Modified", eventType: null, actorRole: nameof(Role.Owner)),
            "ck_audit_records_actor_is_named_completely");
    }

    private const string EmptyJsonObject = "{}";

    private static Task<int> InsertRawAuditRecordAsync(
        KaffDbContext context,
        string action,
        string? eventType,
        Guid? projectId = null,
        string? grantPath = null,
        Guid? actorUserId = null,
        string? actorRole = null)
        => context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO audit_records
               (id, occurred_at, action, entity_type, entity_id, event_type,
                actor_display_name, changed_properties, correlation_id, after_json,
                project_id, grant_path, actor_user_id, actor_role)
             VALUES
               ({Guid.CreateVersion7()}, {Now}, {action}, 'User', {Guid.CreateVersion7()}, {eventType},
                'raw', '[]'::jsonb, {Guid.CreateVersion7()}, {EmptyJsonObject}::jsonb,
                {projectId}, {grantPath}, {actorUserId}, {actorRole})
             """,
            Ct);

    private static User NewOwner()
    {
        return User.Create(
            UniqueNames.Code("owner"),
            "نبيل",
            UniqueNames.Phone(),
            Role.Owner,
            Now).Value;
    }

    private static Domain.MasterData.Client NewClient()
    {
        return Domain.MasterData.Client.Create(
            UniqueNames.Code("AUD"),
            "عميل التدقيق",
            UniqueNames.Phone(),
            Domain.MasterData.ClientKind.Individual,
            Now).Value;
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId { get; } = Guid.CreateVersion7();

        public string DisplayName => "audit-test-actor";

        public Role? Role => Domain.Identity.Role.Finance;

        public Department? Department => Domain.Identity.Department.Finance;

        public OperationsSubDepartment? OperationsSubDepartment => null;

        public Guid? ClientId => null;

        /// <summary>
        /// Irrelevant here: the audit interceptor records who acted, and the stamp is consulted only
        /// by the authorization gate, which these tests do not go through.
        /// </summary>
        public string? SecurityStamp => null;
    }
}
