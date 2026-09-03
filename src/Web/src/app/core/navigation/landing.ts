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
  | { readonly kind: 'profile' }
  | { readonly kind: 'hr-projects' }
  | { readonly kind: 'pending'; readonly titleKey: string }
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
      return { kind: 'pending', titleKey: 'landing.pending.owner.title' };
    case 'MarketingSales':
      return { kind: 'pending', titleKey: 'landing.pending.marketing_sales.title' };
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
    case 'hr-projects':
      return 'nav.hr_projects';
    case 'profile':
      return 'nav.my_profile';
    case 'pending':
      return 'nav.home';
    case 'forbidden':
      return null;
  }
}
