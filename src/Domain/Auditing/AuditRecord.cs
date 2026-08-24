using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;

namespace Kaff.Domain.Auditing;

/// <summary>What happened to the entity.</summary>
public enum AuditAction
{
    Created = 1,
    Modified = 2,

    /// <summary>
    /// Recorded for entities that permit deletion. Postings and audit records do not — the database
    /// refuses the operation before this value can ever be written for them.
    /// </summary>
    Deleted = 3,

    /// <summary>
    /// Something happened that no entity change describes — a sign-out, a clean sign-in.
    /// <see cref="AuditRecord.EventType"/> names it and is non-null exactly for this action, which
    /// the database enforces. See decisions.md D-061.
    /// </summary>
    Occurred = 4,
}

/// <summary>
/// One immutable record of one state change.
/// </summary>
/// <remarks>
/// <para>
/// CLAUDE.md: "Every state change writes an audit record: who, when, what changed (before and
/// after), and where the flow requires it, why. This is one mechanism in Domain/, not per-feature
/// code." The mechanism is the EF <c>SaveChanges</c> interceptor in Infrastructure; this is the
/// record it writes. No feature code constructs one directly.
/// </para>
/// <para>
/// The table is append-only and enforced as such by a database trigger, for the same reason
/// postings are: evidence that can be edited is not evidence.
/// </para>
/// </remarks>
public sealed class AuditRecord : Entity, IAuditExempt
{
    private AuditRecord()
    {
    }

    private AuditRecord(
        Guid id,
        DateTimeOffset occurredAt,
        AuditAction action,
        string entityType,
        Guid entityId,
        AuditEventKind? eventType,
        Guid? actorUserId,
        string actorDisplayName,
        Role? actorRole,
        string? beforeJson,
        string? afterJson,
        IReadOnlyList<string> changedProperties,
        string? reason,
        Guid correlationId,
        Guid? projectId,
        ProjectAccessPath? grantPath,
        string? requestPath)
        : base(id)
    {
        OccurredAt = occurredAt;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        EventType = eventType;
        ActorUserId = actorUserId;
        ActorDisplayName = actorDisplayName;
        ActorRole = actorRole;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        ChangedProperties = changedProperties;
        Reason = reason;
        CorrelationId = correlationId;
        ProjectId = projectId;
        GrantPath = grantPath;
        RequestPath = requestPath;
    }

    public DateTimeOffset OccurredAt { get; private set; }

    public AuditAction Action { get; private set; }

    /// <summary>CLR short name of the entity, e.g. <c>Posting</c>.</summary>
    public string EntityType { get; private set; } = null!;

    public Guid EntityId { get; private set; }

    /// <summary>
    /// What occurred, or null when this record describes an entity change instead.
    /// </summary>
    /// <remarks>
    /// Stored as text, like every other enum here: the table is append-only and will be read long
    /// after today's code is gone, and a row that means "2" is only readable with that code in hand.
    /// </remarks>
    public AuditEventKind? EventType { get; private set; }

    /// <summary>Null only for changes made outside a request — migrations, seeding, scheduled work.</summary>
    public Guid? ActorUserId { get; private set; }

    public string ActorDisplayName { get; private set; } = null!;

    public Role? ActorRole { get; private set; }

    /// <summary>State before the change, as JSON. Null on creation.</summary>
    public string? BeforeJson { get; private set; }

    /// <summary>State after the change, as JSON. Null on deletion.</summary>
    public string? AfterJson { get; private set; }

    /// <summary>Names of the properties that actually changed. Empty on creation and deletion.</summary>
    public IReadOnlyList<string> ChangedProperties { get; private set; } = [];

    /// <summary>
    /// Why. spec.md §7 requires a stored reason on every rejection; CLAUDE.md requires one
    /// "where the flow requires it". Supplied through <see cref="IAuditContext"/>.
    /// </summary>
    public string? Reason { get; private set; }

    /// <summary>Groups every record written by the same request, so one action reads as one story.</summary>
    public Guid CorrelationId { get; private set; }

    /// <summary>Set when the changed entity belongs to a project. Lets the trail be filtered per project.</summary>
    public Guid? ProjectId { get; private set; }

    /// <summary>
    /// By what authority the actor reached <see cref="ProjectId"/> — assignment, the Owner's global
    /// reach, HR's, or the portal client's own project.
    /// </summary>
    /// <remarks>
    /// <para>
    /// KAFF-116. The Owner is the actor whose authority leaves no row anywhere: global reach means
    /// there is no <c>ProjectAssignment</c> to point at, so without this the trail that watches him
    /// cannot tell "Owner, globally" from "assigned on 3 June". Three of the four paths are invisible
    /// otherwise.
    /// </para>
    /// <para>
    /// <b>Taken from the access policy that admitted the request, never re-derived.</b> A second
    /// derivation is a second source of truth and would disagree eventually — the policy already
    /// returns it on <see cref="ProjectAccess.Path"/>, and it arrives here through
    /// <see cref="IAuditContext.GrantPath"/>.
    /// </para>
    /// <para>
    /// <b>Null exactly when there is no project</b> — creating a user or a client is company-wide, no
    /// access policy ran, and there is no path to name. Not <see cref="ProjectAccessPath.OwnerGlobal"/>
    /// by default. The database says so rather than this comment:
    /// <c>ck_audit_records_grant_path</c> refuses a path without a project, and refuses
    /// <see cref="ProjectAccessPath.None"/> outright — that value is a refusal, and a refusal writes no
    /// record.
    /// </para>
    /// <para>
    /// The column lands before there is anything to read it, for the reason the whole table exists:
    /// records are append-only by trigger, so a column added later cannot be backfilled and every row
    /// written before it would be permanently silent.
    /// </para>
    /// </remarks>
    public ProjectAccessPath? GrantPath { get; private set; }

    public string? RequestPath { get; private set; }

    public static AuditRecord For(
        DateTimeOffset occurredAt,
        AuditAction action,
        string entityType,
        Guid entityId,
        AuditActor actor,
        string? beforeJson,
        string? afterJson,
        IReadOnlyList<string> changedProperties,
        string? reason,
        Guid correlationId,
        Guid? projectId,
        ProjectAccessPath? grantPath,
        string? requestPath)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);

        return new AuditRecord(
            NewId(),
            occurredAt,
            action,
            entityType,
            entityId,
            eventType: null,
            actor.UserId,
            actor.DisplayName,
            actor.Role,
            beforeJson,
            afterJson,
            changedProperties,
            reason,
            correlationId,
            projectId,
            grantPath,
            requestPath);
    }

    /// <summary>
    /// The record for something that happened without changing an entity.
    /// </summary>
    /// <remarks>
    /// Same table, same append-only trigger, same correlation id as everything else the save writes.
    /// <c>BeforeJson</c> and <c>AfterJson</c> are null because there is no state to snapshot — the
    /// event is the whole fact, and the table's check constraint accepts that shape only when
    /// <c>EventType</c> is present. See decisions.md D-061.
    /// <para>
    /// No event this system records happens on a project — signing in and out are company-wide — so
    /// <see cref="ProjectId"/> and <see cref="GrantPath"/> are both null, and stay paired. The day an
    /// event does name a project it needs both, together.
    /// </para>
    /// </remarks>
    public static AuditRecord ForEvent(
        DateTimeOffset occurredAt,
        AuditEvent occurrence,
        AuditActor actor,
        string? reason,
        Guid correlationId,
        string? requestPath)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        ArgumentNullException.ThrowIfNull(actor);

        return new AuditRecord(
            NewId(),
            occurredAt,
            AuditAction.Occurred,
            occurrence.SubjectType,
            occurrence.SubjectId,
            occurrence.Kind,
            actor.UserId,
            actor.DisplayName,
            actor.Role,
            beforeJson: null,
            afterJson: null,
            changedProperties: [],
            reason,
            correlationId,
            projectId: null,
            grantPath: null,
            requestPath);
    }
}
