using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Users.ChangeUserRole;

/// <summary>
/// The role the Owner is setting. The user is named by the route, not by the body.
/// </summary>
/// <param name="Role">
/// One of the nine (<c>enum.Role.*</c>). Sent as the member name, not a number, the same convention
/// <c>CreateUser.Request.Role</c> uses. Department, sub-department and client id are not carried here
/// — <c>User.ChangeRole</c> re-validates the account's existing values against this role rather than
/// accepting new ones, so a Marketing user staying in <c>Department.Marketing</c> and becoming
/// <c>Role.Hr</c> is refused rather than silently corrected (<c>AC-109-G</c>).
/// </param>
public sealed record Request(Role Role);
