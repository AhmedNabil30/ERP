using System.Text.Json.Nodes;
using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Audit.ReadAuditTrail;

/// <summary>
/// One record of one state change, as the Owner reads it. KAFF-117.
/// </summary>
/// <remarks>
/// <para>
/// <b>The whole row, deliberately.</b> Every column of <c>AuditRecord</c> except the ones that would
/// be a second statement of something already here. The table is append-only with no backfill path,
/// so a column omitted from the only thing that reads it is a column written forever and read never —
/// <see cref="IpAddress"/> is the one to think about, and decisions.md D-079 exists precisely to make
/// that value mean something, which it can only do if somebody can see it.
/// </para>
/// <para>
/// <b><see cref="Before"/> and <see cref="After"/> are JSON, not strings holding JSON.</b> The column
/// is <c>jsonb</c>; handing the screen a quoted string to re-parse would put a second parser in the
/// system for data that is already structured.
/// </para>
/// <para>
/// <b>Redaction is not performed here and must not be.</b> <c>PasswordHash</c> and
/// <c>SecurityStamp</c> are <c>[AuditRedacted]</c>, so the placeholder is what the interceptor
/// <i>stored</i> — the secret never entered the table. A second redactor on the read path would be a
/// second source of truth and would disagree with the first eventually, and it would also quietly
/// excuse a future entity whose secret was never redacted on the way in. Story rule 7 is asserted
/// against a real password change in <c>ReadAuditTrailTests</c>.
/// </para>
/// <para>
/// <b>No money, and none can arrive by accident.</b> From slice 3 the trail carries every posting —
/// inside <see cref="Before"/> and <see cref="After"/>, which are the entity's own snapshot and not a
/// projection this type computes. There is nothing here to join a balance onto, and D-106's whitelist
/// test is what keeps it that way.
/// </para>
/// </remarks>
/// <param name="Id">The record.</param>
/// <param name="OccurredAt">When. The trail is ordered by this, newest first.</param>
/// <param name="Action">Created, Modified, Deleted — or Occurred, for something that changed no entity.</param>
/// <param name="EventType">What occurred, non-null exactly when <paramref name="Action"/> is Occurred.</param>
/// <param name="EntityType">CLR short name of the subject, e.g. <c>Client</c>.</param>
/// <param name="EntityId">The subject. Null only for an event that declares none.</param>
/// <param name="ActorUserId">Who. Null only for work outside a request — migrations, seeding.</param>
/// <param name="ActorDisplayName">Copied at the time of the change, so a later rename cannot rewrite history.</param>
/// <param name="ActorRole">The role they acted under. Null together with the user id, never alone.</param>
/// <param name="Before">State before, as stored. Null on creation.</param>
/// <param name="After">State after, as stored. Null on deletion.</param>
/// <param name="ChangedProperties">What actually moved. Empty on creation and deletion.</param>
/// <param name="Reason">Why, where the flow required one — spec.md §7, and <c>AC-117-G</c>.</param>
/// <param name="CorrelationId">Groups every record one request wrote, so one action reads as one story.</param>
/// <param name="ProjectId">Set where the entity belongs to a project. What <c>?projectId=</c> filters on.</param>
/// <param name="GrantPath">By what authority the actor reached the project — <c>AC-117-F</c>, KAFF-116.</param>
/// <param name="RequestPath">The route. Never the query string.</param>
/// <param name="IpAddress">The connection address, per decisions.md D-063 §2 and D-079.</param>
public sealed record AuditEntry(
    Guid Id,
    DateTimeOffset OccurredAt,
    AuditAction Action,
    AuditEventKind? EventType,
    string EntityType,
    Guid? EntityId,
    Guid? ActorUserId,
    string ActorDisplayName,
    Role? ActorRole,
    JsonNode? Before,
    JsonNode? After,
    IReadOnlyList<string> ChangedProperties,
    string? Reason,
    Guid CorrelationId,
    Guid? ProjectId,
    ProjectAccessPath? GrantPath,
    string? RequestPath,
    string? IpAddress);

/// <summary>
/// The records that matched. KAFF-117.
/// </summary>
/// <remarks>
/// A wrapper object rather than a bare array, following <c>ListClients</c>: a total and a page can be
/// added without breaking the shape. <b>There is no paging today and that is a ceiling, not an
/// omission</b> — see the handler.
/// </remarks>
/// <param name="Records">Newest first. Empty when nothing matched — a 200, never a 404 and never a null.</param>
public sealed record Response(IReadOnlyList<AuditEntry> Records);
