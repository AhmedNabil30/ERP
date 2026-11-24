namespace Kaff.Api.Features.Portal.ChangePassword;

/// <summary>The current password and the one that replaces it. Never stored in clear.</summary>
public sealed record Request(string? CurrentPassword, string? NewPassword);
