using Kaff.Domain.Common;

namespace Kaff.Domain.Authorization;

/// <summary>Errors raised by the authorization mechanism.</summary>
public static class AuthorizationErrors
{
    public static readonly Error NotAuthenticated =
        Error.Unauthenticated("auth.not_authenticated", "errors.auth.not_authenticated");

    public static readonly Error Forbidden =
        Error.Forbidden("auth.forbidden", "errors.auth.forbidden");

    public static readonly Error NotAssignedToProject =
        Error.Forbidden("auth.not_assigned_to_project", "errors.auth.not_assigned_to_project");

    public static readonly Error AssignmentLevelTooLow =
        Error.Forbidden("auth.assignment_level_too_low", "errors.auth.assignment_level_too_low");

    public static readonly Error ProjectNotSpecified =
        Error.Forbidden("auth.project_not_specified", "errors.auth.project_not_specified");

    public static readonly Error RoleCannotLogIn =
        Error.Forbidden("auth.role_cannot_log_in", "errors.auth.role_cannot_log_in");

    /// <summary>
    /// spec.md §9 amendment, decisions.md D-049 ruling 3 — "at least 8 characters, no forced
    /// complexity". The length is <see cref="Kaff.Domain.Identity.User.MinimumPasswordLength"/>.
    /// </summary>
    /// <remarks>
    /// Keyed under <c>errors.auth.*</c> rather than <c>errors.identity.*</c> because that is the key
    /// the screen names [Verified: 2026-08-23 @ <c>ux/slice-1-flows.md</c> -> S-007's error table],
    /// and a credential rule is the door's business. 🟡 decisions.md D-065 drew the namespace line
    /// around <i>sign-in</i> refusals and does not settle where a password <i>policy</i> refusal
    /// belongs; raised rather than decided.
    /// </remarks>
    public static readonly Error PasswordTooShort =
        Error.Validation("auth.password_too_short", "errors.auth.password_too_short");

    /// <summary>CLAUDE.md and spec.md §9: "Nobody creates and approves the same movement."</summary>
    public static readonly Error SameActorCreatedAndApproved =
        Error.Forbidden("auth.same_actor_created_and_approved", "errors.auth.same_actor_created_and_approved");
}

/// <summary>
/// The one rule that role and assignment cannot express: nobody approves their own movement.
/// </summary>
/// <remarks>
/// <para>
/// spec.md §9 and CLAUDE.md: "Nobody creates and approves the same movement. If your handler lets the
/// same user do both, it's wrong."
/// </para>
/// <para>
/// This is per-instance, not per-role: it compares the actor on the record with the actor now, so it
/// cannot live in the permission catalogue. Every approval handler calls it. A single shared function
/// rather than a repeated <c>if</c> so the rule reads the same everywhere and can be found by
/// searching for one name.
/// </para>
/// </remarks>
public static class SeparationOfDuties
{
    /// <summary>Refuses the approval when the approver is the person who created the movement.</summary>
    public static Result EnsureDifferentActor(Guid createdByUserId, Guid approvingUserId)
        => createdByUserId == approvingUserId
            ? Result.Failure(AuthorizationErrors.SameActorCreatedAndApproved)
            : Result.Success();

    /// <summary>
    /// Refuses when the approver appears anywhere earlier in the chain. spec.md §7 runs an extract
    /// through four gates; the same person must not hold two of them on the same document.
    /// </summary>
    public static Result EnsureDifferentActor(IEnumerable<Guid> previousActorUserIds, Guid approvingUserId)
    {
        ArgumentNullException.ThrowIfNull(previousActorUserIds);

        foreach (Guid actor in previousActorUserIds)
        {
            if (actor == approvingUserId)
            {
                return Result.Failure(AuthorizationErrors.SameActorCreatedAndApproved);
            }
        }

        return Result.Success();
    }
}
