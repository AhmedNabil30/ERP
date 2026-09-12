namespace Kaff.Domain.Common;

/// <summary>
/// Base for every persisted entity.
/// </summary>
/// <remarks>
/// Identifiers are UUID v7: time-ordered, so index locality is preserved, and generated in the
/// domain rather than by the database. Client-side generation is what lets the audit interceptor
/// write a complete record in the same transaction as the change — it never has to wait for a
/// database-generated key. See decisions.md D-004.
/// </remarks>
public abstract class Entity : IEquatable<Entity>
{
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Entity identifier must not be empty.", nameof(id));
        }

        Id = id;
    }

    /// <summary>Materialisation constructor for EF Core.</summary>
    protected Entity()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Creates a new time-ordered identifier.</summary>
    public static Guid NewId() => Guid.CreateVersion7();

    public bool Equals(Entity? other)
        => other is not null && GetType() == other.GetType() && Id != Guid.Empty && Id == other.Id;

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

/// <summary>
/// Marks an entity that the audit interceptor must not write audit records for.
/// Only the audit table itself and other append-only technical tables qualify.
/// Auditing is opt-out, not opt-in, so a new entity cannot silently escape it.
/// </summary>
public interface IAuditExempt
{
}

/// <summary>
/// Marks an entity whose own row carries no project, but which exists <b>because</b> a project-scoped
/// grant authorised the act that created it — so the audit record still falls back to
/// <see cref="Auditing.IAuditContext.GrantProjectId"/> when the entity names none of its own.
/// </summary>
/// <remarks>
/// decisions.md D-149, correcting D-148 §4: the fallback is per marked <b>type</b>, not unconditional.
/// D-148 tagged every project-less entity in the save with the grant's project, which wrongly pulled a
/// company-wide entity (e.g. <c>Client</c>) saved alongside a project-scoped one into that project's
/// trail. Only an entity that opts in this way is eligible for the fallback — implemented by
/// <see cref="Kaff.Domain.MasterData.Employee"/> and by nothing else: the row exists only because the
/// Site Engineer's project authorised its registration (D-140 point 3), so the act and the entity are
/// the same act. A company-wide entity that merely happens to be saved in the same request is not.
/// <para>
/// <b>The ceiling</b>: the marker is per type, not per act. A future endpoint that modifies an
/// existing <see cref="Kaff.Domain.MasterData.Employee"/> for an unrelated, non-project reason would
/// still take the fallback. That endpoint does not exist today; when it does, this is the marker that
/// makes the choice deliberate rather than a silent inheritance.
/// </para>
/// </remarks>
public interface IAuditScopedByGrant
{
}
