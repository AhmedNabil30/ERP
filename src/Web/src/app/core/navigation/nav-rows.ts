import { Session } from '../auth/auth.service';

/** One sidebar row: the company-wide permission that gates it, where it points, and its i18n key. */
export interface NavRow {
  readonly permission: string;
  readonly path: string;
  readonly labelKey: string;
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
 * **Day labour is deliberately absent.** `/projects/:projectId/day-labour` needs a `:projectId` a
 * company-wide row cannot supply; KAFF-215's open Question 1 leaves how it gets a row unanswered, and
 * this table does not guess. `nav-rows.spec.ts`'s completeness check enforces this by construction:
 * it derives "guarded top-level route" from `app.routes.ts` as *fixed-path, permission-guarded*, which
 * a route carrying `:projectId` never is.
 */
const NAV_ROWS: readonly NavRow[] = [
  { permission: 'ClientManage', path: '/clients', labelKey: 'nav.clients' },
  { permission: 'UserManage', path: '/users', labelKey: 'nav.users' },
  { permission: 'AuditRead', path: '/audit', labelKey: 'nav.audit' },
  { permission: 'CatalogueManage', path: '/catalogue', labelKey: 'nav.catalogue' },
  { permission: 'BabManage', path: '/babs', labelKey: 'nav.babs' },
  { permission: 'EmployeeManage', path: '/employees', labelKey: 'nav.employees' },
  { permission: 'SubcontractorManage', path: '/subcontractors', labelKey: 'nav.subcontractors' },
  { permission: 'SupplierManage', path: '/suppliers', labelKey: 'nav.suppliers' },
];

/**
 * The sidebar's row list for a session. AC-215-C: a permission absent from `session.permissions`
 * produces no row at all — not a hidden one, none in the DOM, since the template only ever iterates
 * what this returns.
 */
export function navRowsFor(session: Session): readonly NavRow[] {
  return NAV_ROWS.filter((row) => session.permissions.includes(row.permission));
}
