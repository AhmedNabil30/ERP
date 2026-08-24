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
