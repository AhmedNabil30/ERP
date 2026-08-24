using System.ComponentModel.DataAnnotations;

namespace Kaff.Api.Options;

/// <summary>
/// Sign-in lockout settings. spec.md §9 amendment (Karim, 2026-08-21, decisions.md D-049 ruling 3):
/// "an account locks for 15 minutes after 5 consecutive failed attempts."
/// </summary>
/// <remarks>
/// <para>
/// The numbers are a business ruling, so they live here rather than inside <c>User</c>, following
/// <see cref="JwtOptions.InactivityMinutes"/> — the other operational number Karim set in the same
/// ruling. <c>User.RecordFailedSignIn</c> takes both as arguments; the sign-in handler (slice 1) is
/// what reads them from here.
/// </para>
/// <para>
/// 🟡 The lockout is per account, which is what the amendment says and all it says. Whether it
/// should key on account-and-address is an open question in decisions.md D-049, and nothing here
/// anticipates it.
/// </para>
/// </remarks>
public sealed class LockoutOptions
{
    public const string SectionName = "Lockout";

    /// <summary>Consecutive failures that trigger a lock.</summary>
    [Range(1, 20)]
    public int MaxFailedAttempts { get; init; } = 5;

    /// <summary>How long the account stays locked, in minutes.</summary>
    [Range(1, 1440)]
    public int LockoutMinutes { get; init; } = 15;

    public TimeSpan LockoutDuration => TimeSpan.FromMinutes(LockoutMinutes);
}
