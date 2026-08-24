namespace Kaff.Domain.Authorization;

/// <summary>
/// Reads the caller's <b>current</b> authorization facts from the database.
/// </summary>
/// <remarks>
/// <para>
/// The access token proves <i>who</i> is calling. It does not prove what they are, and it must never
/// be asked to: a token issued this morning describes this morning. Role, department,
/// sub-department, client scope and — most importantly — whether the account is still active all
/// come from here, on every authorized request.
/// </para>
/// <para>
/// <b>Why this exists as its own seam.</b> Until 2026-08-20 these facts were built from token claims
/// and revalidated only inside <see cref="IProjectAccessPolicy"/> — which the handler calls only when
/// the request names a project. Every <see cref="PermissionScope.CompanyWide"/> permission therefore
/// skipped revalidation entirely, so a deactivated user kept <c>UserManage</c>,
/// <c>TreasuryPostCompany</c> and the rest until their token expired. Two tests appeared to cover
/// revocation and both used project-scoped routes. See decisions.md D-048.
/// </para>
/// <para>
/// Returning <c>null</c> means "this caller has no authority" — deleted, deactivated, or never
/// existed. The handler treats all three identically and says nothing about which, because
/// distinguishing them for an unauthenticated caller is an account-enumeration oracle.
/// </para>
/// </remarks>
public interface IPermissionSubjectReader
{
    /// <summary>
    /// The live subject for <paramref name="userId"/>, or <c>null</c> if the account cannot act.
    /// </summary>
    /// <param name="securityStamp">
    /// The stamp the caller's token was issued against. **Required.** A null or non-matching value
    /// is refused, which is what makes the global sign-out of decisions.md D-051 (N5) real: rotating
    /// <c>User.SecurityStamp</c> — on a password change, a reset or a deactivation — invalidates every
    /// token for that user at once.
    ///
    /// There is deliberately no "skip the check when the claim is absent" path. D-051 names that
    /// trap by name: a revocation check with a bypass is worse than an absent one, because it reads
    /// as protection while granting none. A token minted without the claim is simply not a token
    /// this system issued.
    /// </param>
    Task<PermissionSubject?> ReadAsync(Guid userId, string? securityStamp, CancellationToken cancellationToken);
}
