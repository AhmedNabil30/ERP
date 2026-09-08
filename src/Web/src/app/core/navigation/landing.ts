import { Role, Session } from '../auth/auth.service';

/**
 * S-004's landing dispatch, and the side nav's one entry — one source, so the shell's nav item always
 * points at what the landing actually renders. `ux/navigation.md` -> `Landing summary`.
 *
 * **Built from the permission set `GET /api/auth/me` returns, not from `switch (role)`** — KAFF-125
 * rule 6, and `ux/navigation.md` -> `Navigation is built from the permission set, not from
 * switch (role)`. This file was a nine-case `switch (role)` until 2026-09-08; `V-35-G` found it in
 * breach of the rule on its face, and this is the repair.
 *
 * **Why the rule exists, in this file's own terms.** Department is a second independent axis —
 * `SiteExpenseConfirm` and `PhotoPublish` are granted to `Department.Operations` +
 * `OperationsSubDepartment.Administrative` **with no role named**, so one Site Engineer holds them and
 * another does not. Seniority is a third, and it is per project. A role switch sees none of that; the
 * evaluated set the server sends already has all three folded in. The consequence is unobservable in
 * slice 1 — no role today holds a company-wide permission its role does not imply — which is exactly
 * why the assertion in `landing.spec.ts` is written against the *mechanism* and not against the nine
 * outputs, which agree under both designs.
 *
 * ⛔ **This is presentation, and it decides nothing.** CLAUDE.md: *"Never enforce permissions in the
 * frontend alone. UI hiding is convenience; the server decides."* Every route linked from here is
 * authorised again server-side against role × assignment, re-read from the database on each request
 * (D-048). A caller who reaches `/users` by typing it is refused by the API, not by this function.
 *
 * **`Owner` and `MarketingSales` render real surfaces, not invented dashboards.** Their ruled landings
 * S-006 and S-011 exist now (KAFF-127, KAFF-126); they were an honest "not built yet" until they did.
 */
export type Landing =
  | { readonly kind: 'clients' }
  | { readonly kind: 'users' }
  | { readonly kind: 'profile' }
  | { readonly kind: 'hr-projects' }
  | { readonly kind: 'forbidden' };

/**
 * Each ruled landing, and the **company-wide** permission on `Session.permissions` that entitles a
 * caller to it. `ux/navigation.md` -> `Landing summary` is the source of both rows.
 *
 * **The order is the rule, and it names no role.** The Owner holds `UserManage` *and* `ClientManage`;
 * the summary lands the Owner on S-006 and MarketingSales on S-011. Putting the narrower grant first
 * reproduces that table from the set alone. A role that later gains `ClientManage` without
 * `UserManage` lands on the client list with no change here — which is the whole point of rule 6.
 *
 * Both are `PermissionScope.CompanyWide` in `PermissionCatalogue`, which is what puts them on this
 * payload at all: `PermissionEvaluator.CompanyWidePermissionsHeld` excludes project-scoped rows by
 * construction (D-035). See {@link landingFor} for the one landing that costs us.
 */
const RULED_LANDINGS: readonly (readonly [permission: string, landing: Landing])[] = [
  // S-006 the user list — `Permission.UserManage`, CompanyWide, `Role.Owner` alone today (D-044).
  ['UserManage', { kind: 'users' }],
  // S-011 the client list — `Permission.ClientManage`, CompanyWide, Owner and Marketing (spec.md §2).
  ['ClientManage', { kind: 'clients' }],
];

/**
 * The roles that may hold a staff session at all — the mirror of the server's
 * `StaffSessionRules.MayHoldStaffSession`, and **not** a landing decision.
 *
 * An allow-list rather than `role !== 'Client' && role !== 'Subcontractor'`, for `V-27-C`'s reason on
 * the server side of the same bar: a deny-list answers "permitted" for every value outside the nine,
 * so a role added later is admitted by silence.
 */
const STAFF_SHELL_ROLES: ReadonlySet<Role> = new Set<Role>([
  'Owner',
  'Finance',
  'TechnicalOffice',
  'SiteEngineer',
  'HeadOfDesign',
  'MarketingSales',
  'Hr',
]);

export function landingFor(session: Session): Landing {
  // Not a landing: whether any staff chrome mounts. `Role.Client` is refused before a staff session
  // can exist (`StaffSessionRules.MayHoldStaffSession`) and `Role.Subcontractor` cannot log in at all
  // (spec.md §9), so neither reaches this line in production. `ux/navigation.md`: "the shell renders
  // S-016 forbidden and mounts no staff chrome — not one frame, not empty."
  if (!STAFF_SHELL_ROLES.has(session.role)) {
    return { kind: 'forbidden' };
  }

  const ruled = RULED_LANDINGS.find(([permission]) => session.permissions.includes(permission));

  if (ruled) {
    return ruled[1];
  }

  // ⛔ **The one landing the permission set cannot decide, and it is a hole in the payload rather
  // than a licence to switch on the role.** `ux/navigation.md` -> "How HR reaches a project at all"
  // rules HR's landing S-009a and rules its permission as "a **new narrow permission**, not
  // `ProjectRead` … the guard reads whatever `GET /api/auth/me` returns". That permission exists —
  // `Permission.ProjectTeamRead`, granted to Owner and HR (D-051 Q32, D-100 Q43) — and
  // **`GET /api/auth/me` does not return it, to anybody**:
  //
  //   * it is `PermissionScope.ProjectScoped`, so `CompanyWidePermissionsHeld` excludes it from
  //     `Session.permissions` by construction (D-035);
  //   * and HR's projects arrive as `TeamProjectEntry`, which — unlike `ProjectEntry` — carries no
  //     `permissions` field at all (D-103).
  //
  // So there is no set to read, and `EmployeeManage` / `UserRead` (HR's only company-wide grants) are
  // the employee register and a name list, not S-009a. Deriving the HR project list from either would
  // be a permission-shaped invention — which reads as compliance and is not. It is named here instead
  // and routed: `V-35-G`'s residue. **What closes it is one of two changes, and neither is this
  // file's**: `TeamProjectEntry` gains the caller's project-scoped permissions (Backend), or
  // `ux/navigation.md`'s ruling is amended (UX/BA). Asserted as a gap, not as behaviour, by
  // `landing.spec.ts` -> "HR's landing is the one the permission set cannot decide".
  if (session.role === 'Hr') {
    return { kind: 'hr-projects' };
  }

  // S-005 My profile. `ux/navigation.md`: "an honest statement that the role has nothing to do yet in
  // this slice" — and the correct answer for a caller whose set is empty because the server emptied
  // it, which `MustChangePassword` does (see `PermissionEvaluator.CompanyWidePermissionsHeld`).
  return { kind: 'profile' };
}

/**
 * The side nav's single slice-1 entry, labelled per the landing it points at. `null` for a session
 * that must mount no chrome at all (the same defensive `forbidden` case {@link landingFor} returns).
 *
 * **Exactly one item, because exactly one destination exists.** Every other item any role's table in
 * `ux/navigation.md` names points at a screen slice 1 has not built — CLAUDE.md and that file both
 * forbid navigation "on the assumption it will need it." `nav.hr_projects` is the literal key that
 * file gives HR's item; the others follow its `nav.*` convention.
 */
export function navLabelKeyFor(session: Session): string | null {
  switch (landingFor(session).kind) {
    case 'clients':
      return 'nav.clients';
    case 'users':
      return 'nav.users';
    case 'hr-projects':
      return 'nav.hr_projects';
    case 'profile':
      return 'nav.my_profile';
    case 'forbidden':
      return null;
  }
}

/**
 * Where the side nav's one entry points.
 *
 * It used to be `/` for every role, because `/` was the only route a staff user could reach. It is
 * not any more: KAFF-126 added `/clients` and KAFF-127 added `/users`, and a nav item labelled
 * "Clients" or "Users" that navigates to the landing page is a label that lies.
 *
 * **Still one item per session**, because one destination per landing is still all that is ruled —
 * and the Owner reaches the client list too (`ClientManage` is granted to `Role.Owner` and
 * `Role.MarketingSales` alike) without that being their *landing*. `ux/navigation.md` and CLAUDE.md
 * both forbid navigation built "on the assumption it will need it"; a second Owner item belongs to
 * whatever story rules the Owner's menu, not to this function.
 */
export function navPathFor(session: Session): string {
  switch (landingFor(session).kind) {
    case 'clients':
      return '/clients';
    case 'users':
      return '/users';
    default:
      return '/';
  }
}
