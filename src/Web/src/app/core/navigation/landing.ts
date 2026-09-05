import { Role } from '../auth/auth.service';

/**
 * S-004's role dispatch, and the side nav's one entry — one source, so the shell's nav item always
 * points at what the landing actually renders. `ux/navigation.md` -> `Landing summary`.
 *
 * **Branches on role, not a `switch (role)` menu built for permission convenience.** Rule 6 forbids
 * building *navigation* from `switch (role)` because department and per-project seniority are axes a
 * role switch cannot see — that rule is about which nav items a role sees as it grows in later slices,
 * not about which of two server-projected shapes (`Projects` vs `TeamProjects`) this response carries.
 * The server itself decided that by role, not by catalogue grant (decisions.md D-103:
 * "`Handler.HandleAsync` branches on `user.Role == Role.Hr` directly, not on which catalogue grant
 * matches"), for the same reason it must not be re-derived from a grant here: a future
 * `ProjectTeamRead` grant added to another role must not silently repaint that role's landing.
 *
 * **`Owner` and `MarketingSales` render an honest "not built yet," not an invented dashboard.** Their
 * ruled landings are S-006 (no `GET` route exists at all) and S-011 (KAFF-119…124, deferred) — see the
 * story's own table. `agents.md`: a criterion that cannot pass is as bad as one that cannot fail, and
 * inventing content for either is exactly the "plausible fill" the story calls this project's most
 * expensive failure mode.
 */
export type Landing =
  | { readonly kind: 'clients' }
  | { readonly kind: 'users' }
  | { readonly kind: 'profile' }
  | { readonly kind: 'hr-projects' }
  | { readonly kind: 'forbidden' };

export function landingFor(role: Role): Landing {
  switch (role) {
    case 'Hr':
      return { kind: 'hr-projects' };
    case 'Finance':
    case 'TechnicalOffice':
    case 'SiteEngineer':
    case 'HeadOfDesign':
      return { kind: 'profile' };
    case 'Owner':
      // ⚠️ Was `pending` until 2026-09-05, and the paragraph above still explains why: S-006 was
      // "no `GET` route exists at all", so an honest "not built yet" was the only truthful thing to
      // render. **That route exists now and so does S-006** (KAFF-127), so the honest surface is the
      // real one. `ux/screen-inventory.md` -> S-006: "Every user, their role, department, active
      // state. Owner only."
      //
      // **`pending` is gone with it.** MarketingSales left it on 2026-09-04 and the Owner was the
      // last role in it, so the branch, its three catalogue keys and the landing page's `@case` are
      // deleted rather than kept warm for a role that might want one later. `agents.md`: a criterion
      // that cannot pass is as bad as one that cannot fail, and a surface no role reaches is neither.
      return { kind: 'users' };
    case 'MarketingSales':
      // âš ï¸ Was `pending` until 2026-09-04, and the paragraph above still explains why: S-011 was
      // "KAFF-119â€¦124, deferred", so an honest "not built yet" was the only truthful thing to render.
      // **All five of those stories are now built and S-011 exists** (KAFF-126), so the honest
      // surface is the real one. `ux/navigation.md` -> `Landing summary`: "MarketingSales | S-011
      // Client list".
      return { kind: 'clients' };
    case 'Client':
    case 'Subcontractor':
      // Defensive only: `Role.Client` is refused before a staff session can exist at all
      // (`StaffSessionRules.MayHoldStaffSession`) and `Role.Subcontractor` cannot log in (spec.md §9).
      // Neither reaches this line in production. `ux/navigation.md`: "the shell renders S-016
      // forbidden and mounts no staff chrome — not one frame, not empty" is the rule this honours if
      // one somehow did.
      return { kind: 'forbidden' };
  }
}

/**
 * The side nav's single slice-1 entry, labelled per role. `null` for a role that must mount no chrome
 * at all (the same defensive `forbidden` case {@link landingFor} returns).
 *
 * **Exactly one item, because exactly one destination exists.** Every other item any role's table in
 * `ux/navigation.md` names points at a screen slice 1 has not built — CLAUDE.md and that file both
 * forbid navigation "on the assumption it will need it." `nav.hr_projects` is the literal key that
 * file gives HR's item; the others follow its `nav.*` convention rather than inventing a `shell.nav.*`
 * one, since a destination that already renders real content deserves a real label and one that does
 * not (`Owner`, `MarketingSales`) gets the same honest `nav.home` any of them would.
 */
export function navLabelKeyFor(role: Role): string | null {
  switch (landingFor(role).kind) {
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
 * **Still one item per role**, because one destination per role is still all that is ruled — and the
 * Owner reaches the client list too (`ClientManage` is granted to `Role.Owner` and
 * `Role.MarketingSales` alike) without that being their *landing*. `ux/navigation.md` and CLAUDE.md
 * both forbid navigation built "on the assumption it will need it"; a second Owner item belongs to
 * whatever story rules the Owner's menu, not to this function.
 */
export function navPathFor(role: Role): string {
  switch (landingFor(role).kind) {
    case 'clients':
      return '/clients';
    case 'users':
      return '/users';
    default:
      return '/';
  }
}
