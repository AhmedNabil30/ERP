using Kaff.Domain.Common;

namespace Kaff.Domain.Identity;

/// <summary>Error catalogue for identity and assignment. Codes are stable; messages are i18n keys.</summary>
public static class IdentityErrors
{
    public static readonly Error UserNameRequired =
        Error.Validation("identity.username_required", "errors.identity.username_required");

    public static readonly Error FullNameRequired =
        Error.Validation("identity.full_name_required", "errors.identity.full_name_required");

    public static readonly Error PasswordHashRequired =
        Error.Validation("identity.password_hash_required", "errors.identity.password_hash_required");

    public static readonly Error OperationsRequiresSubDepartment =
        Error.Validation("identity.operations_requires_sub_department", "errors.identity.operations_requires_sub_department");

    public static readonly Error SubDepartmentOnlyForOperations =
        Error.Validation("identity.sub_department_only_for_operations", "errors.identity.sub_department_only_for_operations");

    public static readonly Error ClientUserRequiresClient =
        Error.Validation("identity.client_user_requires_client", "errors.identity.client_user_requires_client");

    public static readonly Error NonClientUserCannotCarryClient =
        Error.Validation("identity.non_client_user_cannot_carry_client", "errors.identity.non_client_user_cannot_carry_client");

    /// <summary>spec.md §12 — a portal client and a subcontractor are not staff and hold no department.</summary>
    public static readonly Error ExternalRoleCannotHoldDepartment =
        Error.Validation("identity.external_role_cannot_hold_department", "errors.identity.external_role_cannot_hold_department");

    /// <summary>
    /// Karim, 2026-08-20 — HR is strictly administrative with zero financial visibility. An HR user
    /// in any other department would inherit that department's grants. See decisions.md D-044.
    /// </summary>
    public static readonly Error HrRoleRequiresHrDepartment =
        Error.Validation("identity.hr_role_requires_hr_department", "errors.identity.hr_role_requires_hr_department");

    /// <summary>
    /// KAFF-106 rule 11 and <c>AC-106-G</c> — usernames are unique, case-insensitively.
    /// </summary>
    /// <remarks>
    /// <c>User.Create</c> lower-cases and trims the name before storing it, so the comparison is an
    /// ordinal one against the stored form and <c>NABIL</c> collides with <c>nabil</c>. The rule
    /// itself is sourced to the slice-0 index and to nothing Karim said — it is built under the
    /// readiness waiver of decisions.md D-062 §1, and <b>Q51 stays open</b>.
    /// </remarks>
    public static readonly Error UserNameTaken =
        Error.Conflict("identity.username_taken", "errors.identity.username_taken");

    /// <summary>
    /// The route names a user that does not exist.
    /// </summary>
    /// <remarks>
    /// Added by KAFF-108, which is the first endpoint to address an existing user by id, and put here
    /// rather than in the slice because every other user-scoped act — the role change (KAFF-109),
    /// deactivation (KAFF-111), reactivation (KAFF-112), the reset link (KAFF-103) — answers the same
    /// question and must answer it the same way. <b>KAFF-108 does not name this refusal</b>: an
    /// endpoint taking an id in its route has to say something when the id names nobody, and a bare
    /// 404 with no <c>messageKey</c> would be the one refusal in the system the client cannot
    /// translate. Raised in decisions.md D-067 rather than treated as settled.
    /// </remarks>
    public static readonly Error UserNotFound =
        Error.NotFound("identity.user_not_found", "errors.identity.user_not_found");

    public static readonly Error SubcontractorCannotLogIn =
        Error.Conflict("identity.subcontractor_cannot_log_in", "errors.identity.subcontractor_cannot_log_in");

    public static readonly Error UserAlreadyActive =
        Error.Conflict("identity.user_already_active", "errors.identity.user_already_active");

    public static readonly Error UserAlreadyInactive =
        Error.Conflict("identity.user_already_inactive", "errors.identity.user_already_inactive");

    public static readonly Error AssignmentAlreadyRevoked =
        Error.Conflict("identity.assignment_already_revoked", "errors.identity.assignment_already_revoked");

    public static readonly Error AssignmentLevelNotApplicable =
        Error.Validation("identity.assignment_level_not_applicable", "errors.identity.assignment_level_not_applicable");

    public static readonly Error ClientIsNotAssignable =
        Error.Validation("identity.client_is_not_assignable", "errors.identity.client_is_not_assignable");
}
