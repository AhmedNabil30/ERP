using Kaff.Domain.Identity;

namespace Kaff.Api.Features.Users.ListUsers;

/// <summary>
/// One row of the Owner's user administration list — S-006, and the record S-008 edits.
/// </summary>
/// <remarks>
/// <para>
/// <b>This member set is pinned by a whitelist test, not by a blocklist</b>
/// (decisions.md D-106, D-114 §1). The two members that must never appear here are
/// <c>PasswordHash</c> and <c>SecurityStamp</c>: this is the only payload in the system shaped
/// directly from <see cref="User"/>, so the narrowing is the whole guarantee. There is no money on a
/// user record and there must never be — pay, if HR ever carries it, is spec.md §9's
/// "no visibility into pay if it is ever added".
/// </para>
/// <para>
/// <b><see cref="ActiveProjectNames"/> is here so a confirmation can state its consequence before the
/// act, not report it after.</b> A role change and a deactivation each revoke every active assignment
/// (D-051 Q27, D-049 ruling 5), and <c>ux/slice-1-flows.md</c> S-008 is explicit that
/// <i>"the count and the names come from the server"</i> — a client that counted an assignment list
/// of its own would be a second implementation of the revocation rule, disagreeing with the handler
/// the day either changes. Revoked rows are excluded, so the number is what would be revoked now.
/// </para>
/// </remarks>
public sealed record UserSummary(
    Guid Id,
    string UserName,
    string FullName,
    string Phone,
    Role Role,
    Department? Department,
    OperationsSubDepartment? OperationsSubDepartment,
    bool IsActive,
    IReadOnlyList<string> ActiveProjectNames);

public sealed record Response(IReadOnlyList<UserSummary> Users);
