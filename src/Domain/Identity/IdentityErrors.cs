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
    /// KAFF-100 <c>AC-100-G</c> — <c>admin</c>, <c>root</c> and <c>kaff</c> are refused on the setup
    /// screen, so the first account names a person rather than a shared login (rule 3).
    /// </summary>
    /// <remarks>
    /// ⚠️ **UNCITED — waived, Q45** (decisions.md D-062 §1, the KAFF-100 readiness waiver). Rule 3
    /// argues the account must name a person, which is a different claim from a list of forbidden
    /// words; the list itself cites nothing Karim said. If Karim answers Q45 "no", this error stops
    /// being reachable and is not deleted — the same disposition D-080 gives an unreachable
    /// <c>AuthorizationErrors</c> member.
    /// </remarks>
    public static readonly Error UserNameReserved =
        Error.Validation("identity.username_reserved", "errors.identity.username_reserved");

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

    /// <summary>
    /// KAFF-114 — the route names an assignment that does not exist on that project.
    /// </summary>
    /// <remarks>
    /// Same shape as <see cref="UserNotFound"/> (KAFF-108): an endpoint addressing a row by id in the
    /// route has to say something translatable when the id names nobody, rather than a bare 404 with
    /// no <c>messageKey</c>. Not sourced to an acceptance criterion — KAFF-114's criteria assume the
    /// row exists — so this is REST plumbing for a route parameter, not a business rule. Matched on
    /// both <c>Id</c> and <c>ProjectId</c>: an assignment id that exists but belongs to a different
    /// project is "not found on this project" rather than leaked as found elsewhere.
    /// <c>TranslationCatalogueTests</c> requires every <c>*Errors</c> key to carry a real translation
    /// in both locale catalogues, so its two lines were added there too — the only touch this session
    /// made under <c>src/Web/</c>, and it is the error-catalogue contract, not a screen. See
    /// decisions.md D-078.
    /// </remarks>
    public static readonly Error ProjectAssignmentNotFound =
        Error.NotFound(
            "identity.project_assignment_not_found",
            "errors.identity.project_assignment_not_found");

    public static readonly Error AssignmentLevelNotApplicable =
        Error.Validation("identity.assignment_level_not_applicable", "errors.identity.assignment_level_not_applicable");

    public static readonly Error ClientIsNotAssignable =
        Error.Validation("identity.client_is_not_assignable", "errors.identity.client_is_not_assignable");

    /// <summary>
    /// KAFF-113 rule 8 — a deactivated account is not assignable, and an assignment does not
    /// resurrect one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Refused in the handler rather than in <c>ProjectAssignment.Create</c>, which is handed a
    /// <c>User</c> and could see <c>IsActive</c>. Deliberately not moved there: the assignment row is
    /// not what makes a leaver safe — the subject read is, and it filters on <c>user.IsActive</c>
    /// before any role is considered [Verified: 2026-08-24 @ <c>PermissionSubjectReader.cs</c> ->
    /// <c>ReadAsync</c>]. What this refusal prevents is a row that says something untrue about who is
    /// on a team, which is a statement about the request rather than about the entity.
    /// </para>
    /// <para>
    /// Distinct from <see cref="UserAlreadyInactive"/>, which is deactivation refusing a second
    /// deactivation. The two read alike and mean different things: that one is about the act, this
    /// one is about the target.
    /// </para>
    /// </remarks>
    public static readonly Error UserIsInactive =
        Error.Conflict("identity.user_is_inactive", "errors.identity.user_is_inactive");

    /// <summary>
    /// KAFF-113 rule 9 — one active assignment per user per project.
    /// </summary>
    /// <remarks>
    /// The rule is held by <c>ux_project_assignments_active</c>, whose filter is
    /// <c>revoked_at IS NULL</c> [Verified: 2026-08-24 @ <c>IdentityConfigurations.cs</c> ->
    /// <c>ProjectAssignmentConfiguration</c>], so re-assignment after a revocation is legal by
    /// construction rather than by a second rule. The handler's pre-check is the friendly path; the
    /// index is the enforcement, and the loser of a race gets this same refusal rather than a 500.
    /// <b>Sourced to the slice-0 index and to nothing Karim said</b>, which is the shape of Q51 —
    /// see <see cref="UserNameTaken"/>.
    /// </remarks>
    public static readonly Error UserAlreadyAssignedToProject =
        Error.Conflict(
            "identity.user_already_assigned_to_project",
            "errors.identity.user_already_assigned_to_project");
}
