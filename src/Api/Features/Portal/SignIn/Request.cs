namespace Kaff.Api.Features.Portal.SignIn;

/// <summary>What the portal sign-in screen sends. Never stored. No validator, for the timing reason in the staff door's Request.</summary>
public sealed record Request(string? UserName, string? Password);
