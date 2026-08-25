namespace Kaff.Api.Features.Users.ReactivateUser;

/// <summary>
/// What the Owner sends to bring a leaver back. The user is named by the route, not by the body.
/// </summary>
/// <param name="TemporaryPassword">
/// The credential the Owner issues, which the user MUST replace on first sign-in (KAFF-112 rule 4,
/// D-049 ruling 4) — the same shape <c>CreateUser</c> uses for the identical reason.
/// <b>Optional</b>, mirroring <c>CreateUser.Request.TemporaryPassword</c>: KAFF-106 rule 10 treats an
/// account with no password as a legitimate state, and nothing here forces the Owner to issue one in
/// the same request as the reactivation itself. The old credential is cleared either way
/// (<c>User.ClearPassword</c>, rule 3) — this field only decides whether a new one replaces it.
/// When present it is at least <c>User.MinimumPasswordLength</c> characters; there is no complexity
/// rule (D-049 ruling 3).
/// </param>
/// <param name="Reason">
/// Recorded verbatim on every audit record the act writes, when supplied — the same optional shape
/// as <c>DeactivateUser.Request.Reason</c> (Q35, as KAFF-110).
/// </param>
public sealed record Request(string? TemporaryPassword, string? Reason);
