namespace Kaff.Api.Features.Auth.ChangePassword;

/// <summary>
/// What the signed-in caller sends to replace their own password. KAFF-103.
/// </summary>
/// <param name="CurrentPassword">
/// Required (KAFF-103 rule 5, built under the readiness waiver — Q48 stays open for Karim). Checked
/// against the caller's own stored hash before anything changes.
/// </param>
/// <param name="NewPassword">
/// At least <c>User.MinimumPasswordLength</c> characters and nothing more — no complexity rule
/// (D-049 ruling 3).
/// </param>
public sealed record Request(string? CurrentPassword, string? NewPassword);
