namespace Kaff.Domain.Auditing;

/// <summary>
/// Marks a property whose value must never appear in an audit snapshot.
/// </summary>
/// <remarks>
/// The audit trail is read by people investigating money. It must record that a credential changed
/// without recording the credential. The interceptor writes a fixed placeholder for these properties
/// and still lists them in <c>ChangedProperties</c>, so the change is visible but the secret is not.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class AuditRedactedAttribute : Attribute
{
    /// <summary>The value written in place of the real one.</summary>
    public const string Placeholder = "[redacted]";
}
