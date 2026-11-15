import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * KAFF-936 AC-936-I. A screen declares its primary action to the shell (`HeaderActionsService`); it
 * does not draw a `kaff-button variant="primary"` of its own. A template that still does must be on
 * this list. The list may only shrink: a template that no longer draws one fails until it is removed.
 * Declarations live inside an `<ng-template>` and are not counted.
 */
const NOT_YET_MIGRATED = [
  'audit/audit-trail-page.html',
  'auth/change-password/change-password-page.html',
  'babs/bab-move-dialog/bab-move-dialog.html',
  'babs/bab-panel/bab-panel.html',
  'babs/bab-tree/bab-tree-page.html',
  'boqs/boq-page.html',
  'catalogue/catalogue-import/catalogue-import-page.html',
  'catalogue/catalogue-list/catalogue-create-panel.html',
  'catalogue/catalogue-list/catalogue-item-panel.html',
  'catalogue/catalogue-list/catalogue-list-page.html',
  'clients/client-form/client-form-page.html',
  'closure/closure-page.html',
  'departments/department-settings/department-settings-page.html',
  'employees/employee-form/employee-form-page.html',
  'extracts/extract-detail/extract-detail-page.html',
  'projects/project-create/project-create-page.html',
  'projects/project-list/project-list-page.html',
  'projects/project-workspace/project-workspace-page.html',
  'subcontractors/subcontractor-form/subcontractor-form-page.html',
  'suppliers/supplier-form/supplier-form-page.html',
  'users/user-form/user-form-page.html',
];

const FEATURES = join(process.cwd(), 'src/app/features');

function templates(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((e) =>
    e.isDirectory() ? templates(join(dir, e.name)) : e.name.endsWith('.html') ? [join(dir, e.name)] : [],
  );
}

function drawsOwnPrimary(file: string): boolean {
  const withoutDeclarations = readFileSync(file, 'utf8').replace(/<ng-template[\s\S]*?<\/ng-template>/g, '');
  return /<kaff-button[^>]*variant="primary"/.test(withoutDeclarations);
}

describe('The primary-action contract (KAFF-936 AC-936-I)', () => {
  const all = templates(FEATURES).map((f) => f.slice(FEATURES.length + 1).replaceAll(String.fromCharCode(92), '/'));
  const drawing = all.filter((f) => drawsOwnPrimary(join(FEATURES, f)));

  it('no template draws its own primary unless it is on the allowlist', () => {
    expect(drawing.filter((f) => !NOT_YET_MIGRATED.includes(f))).toEqual([]);
  });

  it('every allowlisted template still draws one (the list may only shrink)', () => {
    expect(NOT_YET_MIGRATED.filter((f) => !drawing.includes(f))).toEqual([]);
  });

  it('the two screens that improvised a footer are not on the list', () => {
    expect(NOT_YET_MIGRATED).not.toContain('day-labour/worker-register/worker-register-page.html');
    expect(NOT_YET_MIGRATED).not.toContain('daily-log/daily-log-page.html');
  });
});
