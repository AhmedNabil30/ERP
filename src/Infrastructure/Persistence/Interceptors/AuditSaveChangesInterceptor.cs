using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kaff.Domain.Auditing;
using Kaff.Domain.Common;
using Kaff.Domain.Common.Serialization;
using Kaff.Domain.Identity;
using Kaff.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kaff.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Writes an audit record for every state change, in the same transaction as the change.
/// </summary>
/// <remarks>
/// <para>
/// CLAUDE.md: "Every state change writes an audit record: who, when, what changed (before and
/// after), and where the flow requires it, why. This is one mechanism in Domain/, not per-feature
/// code." This interceptor is that one mechanism. No handler writes an audit record itself, and none
/// should — a per-feature call is one a future session forgets.
/// </para>
/// <para>
/// <b>Opt-out, not opt-in.</b> Every entity is audited unless it implements <see cref="IAuditExempt"/>.
/// A new entity added in a later slice is therefore audited from its first commit without anyone
/// remembering to arrange it.
/// </para>
/// <para>
/// <b>Same transaction.</b> Records are added to the same <c>DbContext</c> before it builds its
/// commands, so the change and its evidence commit or roll back together. That works because entity
/// identifiers are UUID v7 generated in the domain — nothing has to wait for a database-generated key.
/// </para>
/// </remarks>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser _currentUser;
    private readonly IAuditContext _auditContext;
    private readonly TimeProvider _timeProvider;

    public AuditSaveChangesInterceptor(ICurrentUser currentUser, IAuditContext auditContext, TimeProvider timeProvider)
    {
        _currentUser = currentUser;
        _auditContext = auditContext;
        _timeProvider = timeProvider;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        WriteAuditRecords(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        WriteAuditRecords(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private void WriteAuditRecords(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();

        DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
        AuditActor actor = ResolveActor();
        List<AuditRecord> records = [];

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditExempt)
            {
                continue;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            AuditRecord? record = Describe(entry, actor, occurredAt);
            if (record is not null)
            {
                records.Add(record);
            }
        }

        // The second source. An event that changes no entity has no change-tracker entry to be found
        // by the loop above, so the handler declares it and the same mechanism writes it — one
        // writer, one table, one transaction, one correlation id. See decisions.md D-061.
        foreach (AuditEvent occurrence in _auditContext.Events)
        {
            records.Add(AuditRecord.ForEvent(
                occurredAt,
                occurrence,
                actor,
                _auditContext.Reason,
                _auditContext.CorrelationId,
                _auditContext.RequestPath));
        }

        if (records.Count == 0)
        {
            return;
        }

        context.Set<AuditRecord>().AddRange(records);

        // The reason, the events and the declared actor describe the change now being saved, not
        // whatever the request does next.
        _auditContext.Clear();
    }

    /// <summary>
    /// Who the records name.
    /// </summary>
    /// <remarks>
    /// Normally the caller. A request that carries no identity at all may declare its actor —
    /// bootstrap creates the Owner that is itself the actor, on an anonymous endpoint (KAFF-100). A
    /// request that <i>does</i> carry an identity may not, because that is impersonation written into
    /// a table nobody can correct afterwards.
    /// </remarks>
    private AuditActor ResolveActor()
    {
        if (_auditContext.Actor is not { } declared)
        {
            return new AuditActor(_currentUser.UserId, _currentUser.DisplayName, _currentUser.Role);
        }

        if (_currentUser.UserId is not null)
        {
            throw new InvalidOperationException(
                "An authenticated request may not attribute its audit records to another actor.");
        }

        return declared;
    }

    private AuditRecord? Describe(EntityEntry entry, AuditActor actor, DateTimeOffset occurredAt)
    {
        AuditAction action = entry.State switch
        {
            EntityState.Added => AuditAction.Created,
            EntityState.Modified => AuditAction.Modified,
            EntityState.Deleted => AuditAction.Deleted,
            _ => AuditAction.Modified,
        };

        List<string> changedProperties = [];
        var before = new JsonObject();
        var after = new JsonObject();

        foreach (PropertyEntry property in entry.Properties)
        {
            string name = property.Metadata.Name;
            bool redacted = IsRedacted(entry.Entity.GetType(), name);

            switch (action)
            {
                case AuditAction.Created:
                    after[name] = ToNode(property.CurrentValue, redacted);
                    break;

                case AuditAction.Deleted:
                    before[name] = ToNode(property.OriginalValue, redacted);
                    break;

                case AuditAction.Modified:
                    if (!property.IsModified || Equals(property.OriginalValue, property.CurrentValue))
                    {
                        continue;
                    }

                    changedProperties.Add(name);
                    before[name] = ToNode(property.OriginalValue, redacted);
                    after[name] = ToNode(property.CurrentValue, redacted);
                    break;

                default:
                    break;
            }
        }

        // A "modification" that changed nothing is not a state change and should leave no record.
        if (action == AuditAction.Modified && changedProperties.Count == 0)
        {
            return null;
        }

        Guid? projectId = ExtractProjectId(entry);

        return AuditRecord.For(
            occurredAt,
            action,
            entry.Entity.GetType().Name,
            ExtractId(entry),
            actor,
            action == AuditAction.Created ? null : before.ToJsonString(KaffJson.Options),
            action == AuditAction.Deleted ? null : after.ToJsonString(KaffJson.Options),
            changedProperties,
            _auditContext.Reason,
            _auditContext.CorrelationId,
            projectId,

            // Taken from the access policy, never re-derived here (KAFF-116 rule 6), and paired with
            // the project rather than with the request: one request may save a project-scoped change
            // and a company-level one, and only the first was reached by any grant path.
            projectId is null ? null : _auditContext.GrantPath,
            _auditContext.RequestPath);
    }

    private static JsonNode? ToNode(object? value, bool redacted)
    {
        if (redacted)
        {
            return JsonValue.Create(AuditRedactedAttribute.Placeholder);
        }

        return value is null ? null : JsonSerializer.SerializeToNode(value, value.GetType(), KaffJson.Options);
    }

    private static bool IsRedacted(Type entityType, string propertyName)
        => entityType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?.GetCustomAttribute<AuditRedactedAttribute>() is not null;

    private static Guid ExtractId(EntityEntry entry)
        => entry.Entity is Entity domainEntity ? domainEntity.Id : Guid.Empty;

    /// <summary>
    /// Tags the record with a project where one applies, so the trail can be filtered per project.
    /// The Project entity tags itself; everything else is asked for a <c>ProjectId</c>.
    /// </summary>
    private static Guid? ExtractProjectId(EntityEntry entry)
    {
        if (entry.Entity is Project project)
        {
            return project.Id;
        }

        PropertyEntry? projectProperty = entry.Properties
            .FirstOrDefault(property => string.Equals(property.Metadata.Name, "ProjectId", StringComparison.Ordinal));

        return projectProperty?.CurrentValue as Guid?;
    }
}
