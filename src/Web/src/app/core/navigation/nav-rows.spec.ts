import { describe, expect, it } from 'vitest';

import arJson from '../../../../public/locales/ar.json';
import enJson from '../../../../public/locales/en.json';
import { Role, Session } from '../auth/auth.service';
import { routes } from '../../app.routes';
import { navRowsFor } from './nav-rows';

/**
 * KAFF-215 — the sidebar's row list, and the completeness check that is this story's whole point:
 * a routed, permission-guarded screen with no row must fail the build, not ship silently (rule 5,
 * `AC-215-B`).
 */

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
 * "Guarded top-level route" derived from `app.routes.ts` itself, not typed out by hand: a fixed path
 * (no `:param` — that is what holds day labour out, per KAFF-215's open Question 1) carrying more
 * guards than the two every signed-in route carries (`sessionGuard`, `mustChangePasswordGuard`). Every
 * route matching that shape today is one of the eight this story rows.
 *
 * **This is the load-bearing derivation.** A future route added to `app.routes.ts` with a permission
 * guard and a fixed path changes this function's output on its own; nothing here needs editing for
 * the completeness test below to see it.
 */
function guardedTopLevelRoutePaths(): readonly string[] {
  return routes
    .filter(
      (route) =>
        typeof route.path === 'string' &&
        route.path.length > 0 &&
        !route.path.includes(':') &&
        Array.isArray(route.canActivate) &&
        route.canActivate.length > 2,
    )
    .map((route) => `/${route.path}`);
}

/**
 * `AC-215-B`. Drop a row from `nav-rows.ts` (any one) and this goes red — watched red, 2026-09-13,
 * by removing the suppliers row and confirming the failure, before restoring it. That is what proves
 * the check is load-bearing rather than a assertion that happens to agree with itself.
 */
describe('AC-215-B — every guarded top-level route has a row, derived from app.routes.ts', () => {
  it('the route list and the row table name exactly the same paths', () => {
    const ownerWithEverything = session('Owner', [
      'ClientManage',
      'UserManage',
      'AuditRead',
      'CatalogueManage',
      'BabManage',
      'EmployeeManage',
      'SubcontractorManage',
      'SupplierManage',
    ]);

    const routePaths = [...guardedTopLevelRoutePaths()].sort();
    const rowPaths = navRowsFor(ownerWithEverything)
      .map((row) => row.path)
      .sort();

    expect(rowPaths).toEqual(routePaths);
  });
});

describe('AC-215-C — a permission the session lacks shows no row at all', () => {
  it('a session holding only ClientManage produces only the clients row', () => {
    const rows = navRowsFor(session('MarketingSales', ['ClientManage']));

    expect(rows.map((row) => row.path)).toEqual(['/clients']);
  });

  it('a session holding nothing produces no rows', () => {
    expect(navRowsFor(session('SiteEngineer', []))).toEqual([]);
  });
});

describe('AC-215-D — row order is stable and does not depend on Set/Map iteration order', () => {
  it('the same permissions, listed in a different order, produce the same row order', () => {
    const forward = navRowsFor(
      session('Owner', ['ClientManage', 'UserManage', 'CatalogueManage', 'SupplierManage']),
    );
    const reversed = navRowsFor(
      session('Owner', ['SupplierManage', 'CatalogueManage', 'UserManage', 'ClientManage']),
    );

    expect(reversed.map((row) => row.path)).toEqual(forward.map((row) => row.path));
  });

  it('rendered twice against the same set, the order is identical', () => {
    const permissions = ['SupplierManage', 'ClientManage', 'EmployeeManage'];

    expect(navRowsFor(session('Owner', permissions))).toEqual(
      navRowsFor(session('Owner', permissions)),
    );
  });
});

describe('AC-215-E — every row label exists in both locales', () => {
  it('every nav row key resolves to a real translation in ar.json and en.json', () => {
    const ar: Record<string, string> = arJson;
    const en: Record<string, string> = enJson;
    const everyRow = navRowsFor(
      session('Owner', [
        'ClientManage',
        'UserManage',
        'AuditRead',
        'CatalogueManage',
        'BabManage',
        'EmployeeManage',
        'SubcontractorManage',
        'SupplierManage',
      ]),
    );

    for (const row of everyRow) {
      expect(ar[row.labelKey], `ar is missing ${row.labelKey}`).toBeTruthy();
      expect(ar[row.labelKey]).not.toBe(row.labelKey);
      expect(en[row.labelKey], `en is missing ${row.labelKey}`).toBeTruthy();
      expect(en[row.labelKey]).not.toBe(row.labelKey);
    }
  });
});
