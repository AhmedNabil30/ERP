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

    public ProjectAccessPath? GrantPath { get; private set; }

    private readonly List<AuditEvent> _events = [];

    public IReadOnlyList<AuditEvent> Events => _events;

    public AuditActor? Actor { get; private set; }

    public void SetReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Reason = reason.Trim();
    }

    public void GrantedThrough(ProjectAccessPath path) => GrantPath = path;

    public void Record<TSubject>(AuditEventKind kind, Guid subjectId)
        where TSubject : Entity
    {
        if (subjectId == Guid.Empty)
        {
            throw new ArgumentException("An audited event must name its subject.", nameof(subjectId));
        }

        _events.Add(new AuditEvent(kind, typeof(TSubject).Name, subjectId));
    }

    public void AttributeTo(AuditActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (actor.UserId is not { } userId || userId == Guid.Empty)
        {
            throw new ArgumentException("A declared actor must name a user.", nameof(actor));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(actor.DisplayName);

        if (actor.Role is null)
        {
            throw new ArgumentException("A declared actor must carry a role.", nameof(actor));
        }

        Actor = actor;
    }

    public void Clear()
    {
        Reason = null;
        Actor = null;
        _events.Clear();
    }

    /// <summary>
    /// Adopts the ambient request identifiers. Called once per request by the Api so that every audit
    /// record written while handling it shares a correlation id and carries the path.
    /// </summary>
    public void BindToRequest(Guid correlationId, string? requestPath)
    {
        CorrelationId = correlationId;
        RequestPath = requestPath;
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
