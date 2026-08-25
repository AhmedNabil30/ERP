using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Users.ChangeUserRole;

/// <summary>
/// What changed, and — the reason this act returns a body at all — every project it took the user
/// off (KAFF-109 rule 6). Whoever re-assigns them afterwards reads this to know what to re-assign.
/// </summary>
/// <param name="UserId">The user whose role changed.</param>
/// <param name="Role">The role now in effect. Unchanged from before the request when the change was a no-op (<c>AC-109-H</c>).</param>
/// <param name="RevokedProjectIds">
/// Every project the user came off, in one act (<c>AC-109-A</c>, <c>AC-109-B</c>, <c>AC-109-C</c>).
/// Empty when the request named the role already held (rule 8) — nothing was revoked because nothing
/// changed.
/// </param>
public sealed record Response(Guid UserId, Role Role, IReadOnlyList<Guid> RevokedProjectIds);
