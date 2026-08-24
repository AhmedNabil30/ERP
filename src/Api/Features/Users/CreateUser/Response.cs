using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Users.CreateUser;

/// <summary>
/// The created account, as the Owner's screen needs to see it.
/// </summary>
/// <remarks>
/// <b>No credential appears here, ever.</b> Not the hash, not the temporary password, not the
/// security stamp. S-007: "the password never appears again anywhere — not in the success message,
/// not on S-008, not in the audit record, not in a toast."
/// </remarks>
/// <param name="Id">The new user's identifier.</param>
/// <param name="UserName">As stored — lower-cased and trimmed, which is what the uniqueness rule compares.</param>
/// <param name="FullName">As stored.</param>
/// <param name="Role">The role granted.</param>
/// <param name="Department">Null for a role that holds none.</param>
/// <param name="OperationsSubDepartment">Null unless the department is Operations.</param>
/// <param name="ClientId">Set only for a portal user.</param>
/// <param name="IsActive">Always true on creation — <c>User.Create</c> says so.</param>
/// <param name="MustChangePassword">
/// True when a temporary password was issued. The whole point of D-049 ruling 4, so the screen can
/// say it out loud.
/// </param>
public sealed record Response(
    Guid Id,
    string UserName,
    string FullName,
    Role Role,
    Department? Department,
    OperationsSubDepartment? OperationsSubDepartment,
    Guid? ClientId,
    bool IsActive,
    bool MustChangePassword);
