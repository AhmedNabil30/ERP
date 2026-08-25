using System.Net;
using Kaff.Domain.Auditing;
using Kaff.Domain.Authorization;
using Kaff.Domain.Common;
using Kaff.Domain.Identity;

namespace Kaff.Infrastructure.Auditing;

/// <summary>Scoped per request. Holds the reason and the correlation id for the audit interceptor.</summary>
public sealed class AuditContext : IAuditContext
{
    public Guid CorrelationId { get; private set; } = Guid.CreateVersion7();

    public string? Reason { get; private set; }

    public string? RequestPath { get; private set; }

    public IPAddress? IpAddress { get; private set; }

    public ProjectAccessPath? GrantPath { get; private set; }

    public AuditActor? VerifiedActor { get; private set; }

    private readonly List<AuditEvent> _events = [];

    public IReadOnlyList<AuditEvent> Events => _events;

    public AuditActor? Actor { get; private set; }

    public void SetReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Reason = reason.Trim();
    }

    public void GrantedThrough(ProjectAccessPath path) => GrantPath = path;

    public void ActorVerifiedAs(AuditActor actor) => VerifiedActor = FullyNamed(actor, "verified");

    public void Record<TSubject>(AuditEventKind kind, Guid? subjectId)
        where TSubject : Entity
    {
        // Guid.Empty is a handler that forgot the id, not a deliberate absence — that is still
        // refused. An explicit null is legal: decisions.md D-063 §3. This guard predates the
        // nullable subject and must survive it, so the table can go on distinguishing "deliberately
        // subjectless" from "somebody's bug".
        if (subjectId == Guid.Empty)
        {
            throw new ArgumentException("An audited event must name its subject.", nameof(subjectId));
        }

        _events.Add(new AuditEvent(kind, typeof(TSubject).Name, subjectId));
    }

    public void AttributeTo(AuditActor actor) => Actor = FullyNamed(actor, "declared");

    /// <summary>
    /// Both actor channels take an actor that is named completely — id, name and role.
    /// </summary>
    /// <remarks>
    /// The half-named actor is the case the database refuses through
    /// <c>ck_audit_records_actor_is_named_completely</c>, and refusing it here as well means the
    /// caller gets an argument error at the point of the mistake rather than a constraint violation
    /// at the point of the save. <b>The constraint is the authority, not this method</b>, and not
    /// only for the usual reason: this guard sits on the two channels that <i>declare</i> an actor,
    /// and <c>AuditSaveChangesInterceptor.ResolveActor</c> constructs one directly when no gate ran
    /// — a path that never reaches here. See decisions.md D-075.
    /// </remarks>
    private static AuditActor FullyNamed(AuditActor actor, string kind)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (actor.UserId is not { } userId || userId == Guid.Empty)
        {
            throw new ArgumentException($"A {kind} actor must name a user.", nameof(actor));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(actor.DisplayName);

        if (actor.Role is null)
        {
            throw new ArgumentException($"A {kind} actor must carry a role.", nameof(actor));
        }

        return actor;
    }

    public void Clear()
    {
        Reason = null;
        Actor = null;
        _events.Clear();
    }

    /// <summary>
    /// Adopts the ambient request identifiers. Called once per request by the Api so that every audit
    /// record written while handling it shares a correlation id and carries the path and the
    /// connection address.
    /// </summary>
    public void BindToRequest(Guid correlationId, string? requestPath, IPAddress? ipAddress)
    {
        CorrelationId = correlationId;
        RequestPath = requestPath;
        IpAddress = ipAddress;
    }
}

/// <summary>
/// The actor for work that happens outside a request — migrations, seeding, scheduled jobs.
/// </summary>
/// <remarks>
/// Registered with <c>TryAddScoped</c>, so the Api's HTTP-backed implementation wins whenever one is
/// registered first. It exists so that a change made outside a request still records who made it,
/// rather than writing a null actor and leaving the trail with a hole in it.
/// </remarks>
public sealed class SystemCurrentUser : ICurrentUser
{
    public const string SystemDisplayName = "system";

    public bool IsAuthenticated => false;

    public Guid? UserId => null;

    public string DisplayName => SystemDisplayName;

    public Role? Role => null;

    public Department? Department => null;

    public OperationsSubDepartment? OperationsSubDepartment => null;

    public Guid? ClientId => null;

    /// <summary>Background work carries no token, so there is no stamp to compare.</summary>
    public string? SecurityStamp => null;
}
