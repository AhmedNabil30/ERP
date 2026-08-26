namespace Kaff.Api.Features.Auth.SignIn;

/// <summary>
/// What the sign-in screen sends. Two fields, and neither is ever stored anywhere.
/// </summary>
/// <remarks>
/// <b>There is no <c>Validator.cs</c> for this request, and that is deliberate.</b> A validator runs
/// before the handler and refuses on shape — an absent password, a short one — which means it answers
/// in microseconds where every real attempt pays for 600,000 PBKDF2 iterations. That is the timing
/// oracle KAFF-101a rule 14a exists to close, arriving through the one file nobody would think to
/// look in. The eight-character minimum of D-049 ruling 3 is a rule about <i>setting</i> a password
/// (<c>CreateUser.Validator</c>, <c>CreateOwner.Validator</c>); at the door, a password too short to
/// have ever been set simply does not match, which is the same answer for the same reason as every
/// other wrong password.
/// </remarks>
/// <param name="UserName">
/// Compared against the stored form, which <c>User.Create</c> trims and lower-cases. <b>Never
/// recorded</b> — decisions.md D-062 §3.
/// </param>
/// <param name="Password">The submitted plaintext. Hashed, compared, and never stored or logged.</param>
public sealed record Request(string? UserName, string? Password);
