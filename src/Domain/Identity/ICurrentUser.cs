namespace Kaff.Domain.Identity;

/// <summary>
/// The actor behind the current unit of work.
/// </summary>
/// <remarks>
/// Consumed by the audit interceptor and by the permission handler. Implemented in the Api against
/// the HTTP principal, and in Infrastructure by a system actor for background and migration work,
/// so that a change made outside a request still records who — or what — made it.
/// </remarks>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string DisplayName { get; }

    Role? Role { get; }

    Department? Department { get; }

    OperationsSubDepartment? OperationsSubDepartment { get; }

    /// <summary>Set for portal users. Scopes visibility to one client (spec.md §12).</summary>
    Guid? ClientId { get; }

    /// <summary>
    /// The security stamp the caller's token was issued against, or null outside a request.
    /// </summary>
    /// <remarks>
    /// The global sign-out mechanism. Karim, 2026-08-21: "a password change or account deactivation
    /// must kill all active tokens". The Architect (N5, decisions.md D-051) chose stamp rotation over
    /// a session table, accepting that revocation is all-or-nothing rather than per-device.
    ///
    /// <see cref="IPermissionSubjectReader"/> compares this against the stored stamp on every
    /// authorized request and refuses a mismatch, so rotating it invalidates every token in
    /// existence for that user at once. See decisions.md D-053.
    /// </remarks>
    string? SecurityStamp { get; }
}

/// <summary>
/// Claim type names issued in the access token. Kept as constants so the token issuer (slice 1)
/// and the authorization handler cannot drift apart.
/// </summary>
public static class KaffClaimTypes
{
    public const string UserId = "kaff:uid";
    public const string DisplayName = "kaff:name";
    public const string Role = "kaff:role";
    public const string Department = "kaff:dept";
    public const string OperationsSubDepartment = "kaff:opsub";
    public const string ClientId = "kaff:client";
    public const string SecurityStamp = "kaff:stamp";
}
