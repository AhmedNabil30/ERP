using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Users.MoveUserDepartment;

/// <summary>
/// Where the user is being moved to. The user is named by the route, not by the body.
/// </summary>
/// <param name="Department">
/// The destination. Null moves the user out of every department, which is legal for the roles that
/// hold none — and is refused for <c>Role.Hr</c>, whose guard is "must be <c>Department.Hr</c>", not
/// "must not be another one" [Verified: 2026-08-23 @ <c>User.cs</c> -&gt; <c>ValidateDepartment</c>].
/// </param>
/// <param name="OperationsSubDepartment">
/// Required when, and only when, <paramref name="Department"/> is Operations (<c>AC-108-C</c>).
/// Sent as the member name, not a number.
/// </param>
public sealed record Request(
    Department? Department,
    OperationsSubDepartment? OperationsSubDepartment);
