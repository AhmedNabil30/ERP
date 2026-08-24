using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Users.CreateUser;

/// <summary>
/// What the Owner sends to create a user. The field set is S-007's
/// [Verified: 2026-08-23 @ <c>ux/slice-1-flows.md</c> -> S-007].
/// </summary>
/// <param name="FullName">Arabic, normally. Required.</param>
/// <param name="UserName">Login identifier. Lower-cased and trimmed by <c>User.Create</c>.</param>
/// <param name="Phone">Entered form; <c>PhoneNumber</c> normalises it.</param>
/// <param name="Email">Optional.</param>
/// <param name="Role">One of the nine. Sent as the member name, not a number.</param>
/// <param name="Department">
/// Absent for <c>Role.Client</c> and <c>Role.Subcontractor</c>, and forced to
/// <c>Department.Hr</c> for <c>Role.Hr</c> — the endpoint refuses anything else rather than
/// correcting it (<c>AC-106-K</c>).
/// </param>
/// <param name="OperationsSubDepartment">Required when, and only when, the department is Operations.</param>
/// <param name="ClientId">Required for <c>Role.Client</c>, forbidden for everyone else.</param>
/// <param name="TemporaryPassword">
/// The credential the Owner issues and the user MUST replace on first sign-in (D-049 ruling 4).
/// <b>Optional</b>, and that is not a convenience: S-007 does not render the field for
/// <c>Role.Subcontractor</c>, which can hold no credential at all, and KAFF-106 rule 10 describes an
/// account with no password as a legitimate state — "cannot sign in" is the absence of a credential.
/// When present it is at least <c>User.MinimumPasswordLength</c> characters and nothing more; there
/// is no complexity rule (D-049 ruling 3).
/// </param>
public sealed record Request(
    string? FullName,
    string? UserName,
    string? Phone,
    string? Email,
    Role Role,
    Department? Department,
    OperationsSubDepartment? OperationsSubDepartment,
    Guid? ClientId,
    string? TemporaryPassword);
