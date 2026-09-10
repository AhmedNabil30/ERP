import { describe, expect, it } from 'vitest';

import { Role, Session } from '../auth/auth.service';
import { landingFor, navLabelKeyFor, navPathFor } from './landing';

/**
 * KAFF-125 **rule 6**, made falsifiable — `V-35-G`.
 *
 * > *"Role-based routing sends each signed-in role to its ruled landing. **Built from the permission
 * > set returned by `/api/auth/me`, never from `switch (role)`** — department and per-project
 * > seniority are independent axes a role switch cannot see."*
 *
 * ⛔ **Rule 6 has no acceptance criterion.** QA found rules 6 and 9 uncovered and the criterion is the
 * BA's to write; this file is the assertion, not the criterion, and it does not certify the story.
 *
 * **Why every test below carries a permission set that disagrees with its role.** The consequence of
 * the prohibition is unobservable in slice 1: no role currently holds a company-wide permission its
 * role does not imply, so a `switch (role)` and a permission lookup produce **the same nine landings**
 * and the same nine nav items. A test that compared those nine would pass against the switch this file
 * was written to kill, and would stop anybody looking — the exact shape `V-33-C` and D-116 §3 record.
 * So the sessions here are ones the switch and the permission set answer **differently**, and each is
 * a payload the server can really send:
 *
 *   * a set that is empty although the role implies grants — what `GET /api/auth/me` returns for
 *     **any** caller holding a temporary password, because `PermissionEvaluator.Evaluate` refuses
 *     every permission with `PasswordChangeRequired` and `CompanyWidePermissionsHeld` runs it per row;
 *   * a set richer than the role implies — the department axis rule 6 names, e.g. the
 *     `Department.Operations` + `Administrative` grants that carry no role at all, and any future
 *     catalogue row that grants a company-wide permission by department.
 *
 * `landingFor` is the function whose change lapsed KAFF-125 (`V-34-H`). Nothing here re-verifies it.
 */
describe('the landing and the nav item are built from the permission set, not from the role', () => {
  function session(role: Role, permissions: readonly string[]): Session {
    return {
      userId: '11111111-1111-1111-1111-111111111111',
      displayName: 'اختبار',
      role,
      department: null,
      operationsSubDepartment: null,
      mustChangePassword: false,
      permissions,
      projects: [],
      teamProjects: [],
    };
  }

  /**
   * **The mutation this file exists to catch.** Restore `switch (role)` in `landingFor` — or read
   * `session.role` in place of `session.permissions` — and this goes red: a role switch has no case
   * that sends `Finance` to the user list, because in slice 1 no Finance user holds `UserManage`.
   *
   * The set is what the server would return the day a company-wide grant reaches Finance through the
   * department axis. Nothing about the role changed; only the evaluated set did, and rule 6 says the
   * landing must move with it.
   */
  it('a landing follows the permission, even when the role does not imply it', () => {
    const financeHoldingUserManage = session('Finance', ['UserManage']);

    expect(landingFor(financeHoldingUserManage)).toEqual({ kind: 'users' });
    expect(navPathFor(financeHoldingUserManage)).toBe('/users');
    expect(navLabelKeyFor(financeHoldingUserManage)).toBe('nav.users');
  });

  /** The same assertion on the other ruled destination, so one hard-coded row cannot satisfy both. */
  it('the client list follows ClientManage, not MarketingSales', () => {
    const engineerHoldingClientManage = session('SiteEngineer', ['ClientManage']);

    expect(landingFor(engineerHoldingClientManage)).toEqual({ kind: 'clients' });
    expect(navPathFor(engineerHoldingClientManage)).toBe('/clients');
    expect(navLabelKeyFor(engineerHoldingClientManage)).toBe('nav.clients');
  });

  /**
   * The third ruled destination, added for KAFF-202/203/206. `ux/navigation.md` -> `Landing summary`:
   * *"TechnicalOffice | S-005 My profile | S-017 Catalogue"* — slice 2 is what turns "eventual" into
   * "now", so this is the assertion that the catalogue's arrival actually moved the landing rather than
   * only adding an unreachable case to the switch.
   */
  it('the catalogue list follows CatalogueManage, not the TechnicalOffice role', () => {
    const financeHoldingCatalogueManage = session('Finance', ['CatalogueManage']);

    expect(landingFor(financeHoldingCatalogueManage)).toEqual({ kind: 'catalogue' });
    expect(navPathFor(financeHoldingCatalogueManage)).toBe('/catalogue');
    expect(navLabelKeyFor(financeHoldingCatalogueManage)).toBe('nav.catalogue');
  });

  /**
   * The converse, and the half a role switch fails hardest: the role is the Owner and the answer is
   * **not** the user list, because this caller holds nothing. A switch cannot see the difference.
   *
   * Not a hypothetical payload — `mustChangePassword: true` produces exactly this set for an Owner,
   * and offering a destination the API will refuse is what rule 6's "the server decides" forbids the
   * shell from doing. `AC-125-D` routes such a caller to the change-password screen long before this
   * function is consulted, so no live landing changes; the assertion is about the mechanism.
   */
  it('a role whose permission set is empty does not reach a ruled landing', () => {
    const ownerWithNothing = session('Owner', []);

    expect(landingFor(ownerWithNothing)).toEqual({ kind: 'profile' });
    expect(navPathFor(ownerWithNothing)).toBe('/');
    expect(navLabelKeyFor(ownerWithNothing)).toBe('nav.my_profile');
  });

  /**
   * `ux/navigation.md` -> `Landing summary` lands the Owner on S-006 and MarketingSales on S-011, and
   * the Owner holds both grants. The table's row order is what reproduces that without naming a role;
   * this pins the precedence so a later edit cannot reorder it silently and land the Owner on
   * `/clients`.
   */
  it('holding both grants lands on the user list, which is the summary the Owner is ruled to', () => {
    expect(landingFor(session('Owner', ['ClientManage', 'UserManage']))).toEqual({ kind: 'users' });
  });

  /**
   * ⛔ **The gap `V-35-G` leaves open, asserted as a gap rather than papered over.**
   *
   * HR's ruled landing is S-009a, and `ux/navigation.md` -> "How HR reaches a project at all" rules
   * its permission as "a **new narrow permission** … the guard reads whatever `GET /api/auth/me`
   * returns". That permission is `Permission.ProjectTeamRead`, and the endpoint returns it to nobody:
   * it is `PermissionScope.ProjectScoped`, so `CompanyWidePermissionsHeld` excludes it from
   * `Session.permissions`, and HR's own projects arrive as `TeamProjectEntry`, which carries no
   * `permissions` field at all.
   *
   * **So this asserts that HR's exact company-wide set decides nothing** — a caller holding it who is
   * not HR lands on the profile. If somebody later maps `EmployeeManage` or `UserRead` to the HR
   * project list to make `landingFor` look fully derived, this goes red, which is the point: a
   * permission-shaped stand-in for a permission the payload does not carry reads as compliance and
   * is not. The gap closes when `TeamProjectEntry` carries the caller's project-scoped permissions
   * (Backend) or the ruling is amended (UX/BA) — neither is this file's to do.
   */
  it("HR's landing is the one the permission set cannot decide", () => {
    const hrCompanyWideSet = ['EmployeeManage', 'UserRead'];

    expect(landingFor(session('Finance', hrCompanyWideSet))).toEqual({ kind: 'profile' });
    expect(landingFor(session('Hr', hrCompanyWideSet))).toEqual({ kind: 'hr-projects' });
  });

  /**
   * The staff-session bar, which is not a landing: `Role.Client` is refused before a staff session can
   * exist and `Role.Subcontractor` cannot log in at all (spec.md §9). Both hold company-wide nothing,
   * so the permission set cannot distinguish them from a Site Engineer — this is the server's
   * `StaffSessionRules.MayHoldStaffSession` mirrored, and `null` is what stops `App.showStaffNav`
   * mounting chrome.
   */
  it('a role that may hold no staff session mounts no chrome, whatever it holds', () => {
    for (const role of ['Client', 'Subcontractor'] as const) {
      expect(landingFor(session(role, ['UserManage', 'ClientManage']))).toEqual({ kind: 'forbidden' });
      expect(navLabelKeyFor(session(role, []))).toBeNull();
    }
  });

  /**
   * The outcome cover kept alongside the mechanism cover, so the repair cannot have moved a real
   * landing. Each set is the caller's **real** company-wide set narrowed to the rows this function
   * reads — spreading a fuller set would let a fixture agree with the code by accident (D-046).
   * `ux/navigation.md` -> `Landing summary` is the column on the right.
   */
  it('the nine roles still land where the Landing summary rules them', () => {
    const ruled: readonly (readonly [Role, readonly string[], string])[] = [
      ['Owner', ['UserManage', 'ClientManage', 'CatalogueManage'], 'users'],
      ['MarketingSales', ['ClientManage'], 'clients'],
      ['Hr', ['EmployeeManage', 'UserRead'], 'hr-projects'],
      ['Finance', [], 'profile'],
      // KAFF-202/203/206: slice 2 turns TechnicalOffice's "eventual" landing into its real one —
      // `ux/navigation.md` -> `Landing summary` names S-017 Catalogue, not My profile, from here on.
      ['TechnicalOffice', ['CatalogueManage'], 'catalogue'],
      ['SiteEngineer', [], 'profile'],
      ['HeadOfDesign', [], 'profile'],
      ['Client', [], 'forbidden'],
      ['Subcontractor', [], 'forbidden'],
    ];

    expect(ruled.map(([role, permissions]) => landingFor(session(role, permissions)).kind)).toEqual(
      ruled.map(([, , kind]) => kind),
    );
  });
});
