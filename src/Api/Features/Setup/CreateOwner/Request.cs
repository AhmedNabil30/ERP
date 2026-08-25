namespace Kaff.Api.Features.Setup.CreateOwner;

/// <summary>
/// What the setup screen sends. S-002's field set — no department, no client, no email: the Owner is
/// not one of §9's four departments (KAFF-100 rule 2) and has no company email on file (spec.md §9).
/// </summary>
/// <param name="FullName">Required (rule 3). Arabic, normally.</param>
/// <param name="UserName">Login identifier. Lower-cased and trimmed by <c>User.Create</c>.</param>
/// <param name="Phone">Entered form; <c>PhoneNumber</c> normalises it (rule 11).</param>
/// <param name="Password">
/// The Owner's own choice (rule 7) — at least <c>User.MinimumPasswordLength</c> characters, no forced
/// complexity (rule 9). <c>ConfirmPassword</c> is a client-side-only check (S-002); the server never
/// sees a second copy to compare.
/// </param>
public sealed record Request(
    string? FullName,
    string? UserName,
    string? Phone,
    string? Password);
