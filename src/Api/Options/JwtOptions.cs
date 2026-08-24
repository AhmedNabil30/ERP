using System.ComponentModel.DataAnnotations;

namespace Kaff.Api.Options;

/// <summary>
/// Access-token validation settings.
/// </summary>
/// <remarks>
/// Slice 0 configures validation; slice 1 issues the tokens. The settings are validated at start-up
/// so a deployment with a missing or placeholder signing key fails immediately rather than accepting
/// unverifiable tokens.
/// </remarks>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Minimum key length for HMAC-SHA256, in bytes.</summary>
    public const int MinimumSigningKeyLength = 32;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required]
    [MinLength(MinimumSigningKeyLength)]
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Clock skew allowed when validating expiry, in seconds.</summary>
    [Range(0, 300)]
    public int ClockSkewSeconds { get; init; } = 30;

    /// <summary>
    /// Name of the <c>HttpOnly</c> cookie carrying the access token.
    /// </summary>
    /// <remarks>
    /// Nabil and the Architect, 2026-08-21 (decisions.md D-050): the token is carried in an
    /// <c>HttpOnly; Secure; SameSite=Strict</c> cookie, and <c>localStorage</c> / <c>sessionStorage</c>
    /// are prohibited for it. The <c>__Host-</c> prefix is not decoration — a browser only accepts a
    /// cookie with that prefix when it is <c>Secure</c>, path <c>/</c> and carries no <c>Domain</c>,
    /// which means a subdomain cannot set it. That closes cookie fixation from a neighbouring host,
    /// and it makes the prefix itself a constraint rather than a naming convention.
    /// </remarks>
    public string CookieName { get; init; } = "__Host-kaff-auth";

    /// <summary>
    /// Sliding inactivity window, in minutes. Karim, 2026-08-21: "Sessions auto-expire after 30
    /// minutes of inactivity." Issuing and sliding the token is slice 1's; this is where the number
    /// lives so it is configurable per environment rather than typed into a handler.
    /// </summary>
    [Range(1, 1440)]
    public int InactivityMinutes { get; init; } = 30;
}
