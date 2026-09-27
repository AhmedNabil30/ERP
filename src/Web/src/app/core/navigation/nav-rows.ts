import { Session } from '../auth/auth.service';

/** The sidebar's two mockup sections (`Main.dc.html` 20-91) — KAFF-922. */
export type NavGroup = 'core' | 'admin';

/** One sidebar row: the company-wide permission that gates it, where it points, and its i18n key. */
export interface NavRow {
  readonly permission: string;
  readonly path: string;
  readonly labelKey: string;
  /** Which mockup section this row renders under — KAFF-922. Grouping in the template reads this
   *  field; it never hardcodes which row belongs where. */
  readonly group: NavGroup;
  /** Icon identifier, not inline SVG — `app.html` maps this to the `<path>` markup KAFF-922 copied
   *  from `Main.dc.html`, so the icon art lives in exactly one place (the template). */
  readonly icon: string;
}

/**
 * Every sidebar row, keyed by the same **company-wide** permission `landing.ts`'s `RULED_LANDINGS`
 * reads off `Session.permissions` — never `switch (role)`, never a second hand-typed permission
 * catalogue (KAFF-215 rule 3). `app.html` iterates this list; it names no path of its own (rule 4).
 *
 * **Array order is the row order, and it is the whole of rule 6 ("stable, and does not read
 * object/Set/Map iteration order").** {@link navRowsFor} filters this fixed array — it never iterates
 * `session.permissions` to build the list — so the same session produces the same order every time,
 * independent of how the server happened to order the permissions it returned.
 *
 * **Day labour is still deliberately absent, and now it is reachable anyway.**
 * `/projects/:projectId/day-labour` needs a `:projectId` a company-wide row cannot supply, so it gets
 * no row here — `nav-rows.spec.ts`'s completeness check enforces that by construction: it derives
 * "guarded top-level route" from `app.routes.ts` as *fixed-path, permission-guarded*, which a route
 * carrying `:projectId` never is. What KAFF-215's open Question 1 actually asked — how a project-scoped
 * screen is ever reached — is answered by the `ProjectList` row below rather than by a row of its own:
 * the project list carries the `:projectId` into the URL. Before that row existed, the whole day-labour
 * feature shipped green and was reachable only by typing a GUID no screen rendered (Nabil, 2026-09-26).
 */
const NAV_ROWS: readonly NavRow[] = [
  // First row, because it is the only one that leads to project work; every row under it is company-wide
  // master data. `Permission.ProjectList` is CompanyWide and granted to Owner, Finance, TechnicalOffice,
  // MarketingSales and SiteEngineer (`PermissionCatalogue.cs:542-544`); `ListProjects.Handler` still
  // filters the rows returned to what the caller is assigned to, so this row exposes no project.
  { permission: 'ProjectList', path: '/projects', labelKey: 'nav.projects', group: 'core', icon: 'projects' },
  { permission: 'CatalogueManage', path: '/catalogue', labelKey: 'nav.catalogue', group: 'core', icon: 'catalogue' },
  { permission: 'BabManage', path: '/babs', labelKey: 'nav.babs', group: 'core', icon: 'babs' },
  { permission: 'EmployeeManage', path: '/employees', labelKey: 'nav.employees', group: 'core', icon: 'employees' },
  { permission: 'SubcontractorManage', path: '/subcontractors', labelKey: 'nav.subcontractors', group: 'core', icon: 'subcontractors' },
  { permission: 'SupplierManage', path: '/suppliers', labelKey: 'nav.suppliers', group: 'core', icon: 'suppliers' },
  { permission: 'ClientManage', path: '/clients', labelKey: 'nav.clients', group: 'core', icon: 'clients' },
  { permission: 'UserManage', path: '/users', labelKey: 'nav.users', group: 'admin', icon: 'users' },
  { permission: 'DepartmentManage', path: '/settings/departments', labelKey: 'nav.departments', group: 'admin', icon: 'departments' },
  { permission: 'AuditRead', path: '/audit', labelKey: 'nav.audit', group: 'admin', icon: 'audit' },
];

/**
 * The sidebar's row list for a session. AC-215-C: a permission absent from `session.permissions`
 * produces no row at all — not a hidden one, none in the DOM, since the template only ever iterates
 * what this returns.
 */
export function navRowsFor(session: Session): readonly NavRow[] {
  return NAV_ROWS.filter((row) => session.permissions.includes(row.permission));
}

/** One rendered sidebar section — KAFF-922. */
export interface NavRowGroup {
  readonly key: NavGroup;
  readonly labelKey: string;
  readonly rows: readonly NavRow[];
}

/** The mockup's two sections, in mockup order, before any session has been applied to them. */
const NAV_GROUP_ORDER: readonly { key: NavGroup; labelKey: string }[] = [
  { key: 'core', labelKey: 'nav.group.core' },
  { key: 'admin', labelKey: 'nav.group.admin' },
];

/**
 * `navRowsFor`'s rows, bucketed by `row.group` into the mockup's two sections. **AC-922-B**: a group
 * with no row in it is dropped here, before the template ever sees it — grouping reads `row.group` on
 * the row model, it never hardcodes which row belongs to which heading.
 */
export function navGroupsFor(session: Session): readonly NavRowGroup[] {
  const rows = navRowsFor(session);
  return NAV_GROUP_ORDER.map((group) => ({
    ...group,
    rows: rows.filter((row) => row.group === group.key),
  })).filter((group) => group.rows.length > 0);
}
