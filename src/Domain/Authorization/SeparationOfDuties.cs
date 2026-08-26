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
    /// The one refusal the staff sign-in door gives for every reason it refuses, bar one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Five cases, one answer</b> — decisions.md D-065 (cases 1, 2, 4, 5) and D-072 §1 (case 3):
    /// a wrong password, a username that does not exist, a <see cref="Kaff.Domain.Identity.Role.Client"/>
    /// credential at the staff origin, a <see cref="Kaff.Domain.Identity.Role.Subcontractor"/>, and a
    /// locked account given the wrong password. Nabil: <i>"Never tell an attacker the account does not
    /// exist"</i>, and on the subcontractor <i>"the door must treat a subcontractor exactly the same
    /// way it treats a non-existent user."</i>
    /// </para>
    /// <para>
    /// <b>401 rather than 403, and that is not a drafting choice.</b> A 403 means "your credential
    /// was valid and you may not come in", which fires only on a real credential and is the single
    /// most informative answer an anonymous door can give (decisions.md D-063 §1). This is the same
    /// reason <see cref="RoleCannotLogIn"/> — an <see cref="ErrorType.Forbidden"/> — is unreachable
    /// from that door. It is not dead: <c>PermissionEvaluator</c> still returns it on an
    /// already-authenticated request, and nobody deletes it on the strength of D-065.
    /// </para>
    /// </remarks>
    public static readonly Error InvalidCredentials =
        Error.Unauthenticated("auth.invalid_credentials", "errors.auth.invalid_credentials");

    /// <summary>
    /// The account is inside a lockout window, and the caller proved they hold its password.
    /// </summary>
    /// <remarks>
    /// spec.md §9 amendment (D-049 ruling 3): five consecutive failures lock the account for fifteen
    /// minutes. <b>This is the only sign-in answer that is not
    /// <see cref="InvalidCredentials"/>, and it is reachable on exactly one path</b> — decisions.md
    /// D-072 §1: <i>"423 Locked only if the provided password is correct. If the password is wrong,
    /// it must return the generic 401."</i> It leaks nothing, because only somebody who already
    /// holds the credential can ever see it, which is exactly the legitimate user the UX argument is
    /// about.
    /// </remarks>
    public static readonly Error AccountLocked =
        Error.Locked("auth.account_locked", "errors.auth.account_locked");

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

    /// <summary>
    /// KAFF-103 AC-103-D. The change-password endpoint's own refusal — never produced by the gate.
    /// </summary>
    /// <remarks>
    /// Validation rather than Forbidden: the caller is already authenticated, so this states that one
    /// field of the request was wrong, the same shape as <see cref="PasswordTooShort"/>, not a claim
    /// about who they are.
    /// </remarks>
    public static readonly Error CurrentPasswordIncorrect =
        Error.Validation("auth.current_password_incorrect", "errors.auth.current_password_incorrect");

    /// <summary>
    /// KAFF-103 AC-103-B, decisions.md D-049 ruling 4. The one refusal <see cref="PermissionEvaluator"/>
    /// gives every permission-gated request while <c>User.MustChangePassword</c> is true.
    /// </summary>
    /// <remarks>
    /// Forbidden rather than the blanket key D-071/D-080 give every other gate refusal: the shell needs
    /// a distinct signal to route to the change-password screen, and unlike the axis a role × assignment
    /// refusal would disclose, "you must change your password" tells an attacker nothing they could not
    /// already infer from having the credential at all. See <c>PermissionAuthorizationHandler</c>.
    /// </remarks>
    public static readonly Error PasswordChangeRequired =
        Error.Forbidden("auth.password_change_required", "errors.auth.password_change_required");

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
