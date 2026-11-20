import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/**
 * KAFF-936 AC-936-I. A screen declares its primary action to the shell (`HeaderActionsService`); it
 * does not draw a `kaff-button variant="primary"` of its own. A template that still does must be on
 * this list. The list may only shrink: a template that no longer draws one fails until it is removed.
 * Declarations live inside an `<ng-template>` and are not counted. A button inside an overlay part (`kaff-detail-panel`,
 * `kaff-confirm-dialog`: a side panel, a bottom sheet, a dialog) is that overlay's own action, not the screen's primary, and is
 * not counted either. The exclusion is by structure (the element a button sits in), never by file name.
 */
const NOT_YET_MIGRATED = [
  'audit/audit-trail-page.html',
  'auth/change-password/change-password-page.html',
  'babs/bab-move-dialog/bab-move-dialog.html',
  'babs/bab-tree/bab-tree-page.html',
  'boqs/boq-page.html',
  'catalogue/catalogue-import/catalogue-import-page.html',
  'catalogue/catalogue-list/catalogue-list-page.html',
  'clients/client-form/client-form-page.html',
  'closure/closure-page.html',
  'departments/department-settings/department-settings-page.html',
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

/** Overlay parts: a button inside one is the overlay's own action. Matched by element, never by file name. */
const OVERLAY = /<(kaff-detail-panel|kaff-confirm-dialog)\b[\s\S]*?<\/\1>/g;

/** An overlay whose open binding is a constant true (`[open]="true"` or a bare `open`) is page body, not an overlay. */
const ALWAYS_OPEN = /\s(?:\[open\]="true"|open)(?=[\s>/])/;

/** True when the markup draws a primary of its own: outside every ng-template and every overlay part. */
function drawsOwnPrimaryIn(source: string): boolean {
  const body = source.replace(/<ng-template[\s\S]*?<\/ng-template>/g, '').replace(OVERLAY, (overlay) => (ALWAYS_OPEN.test(overlay.slice(0, overlay.indexOf('>') + 1)) ? overlay : ''));
  return /<kaff-button[^>]*variant="primary"/.test(body);
}

function drawsOwnPrimary(file: string): boolean {
  return drawsOwnPrimaryIn(readFileSync(file, 'utf8'));
}

describe('drawsOwnPrimaryIn: what counts as a screen primary', () => {
  const primary = '<kaff-button variant="primary" testId="x">go</kaff-button>';

  it('counts a primary drawn in the page body', () => {
    expect(drawsOwnPrimaryIn('<section>' + primary + '</section>')).toBe(true);
  });

  it('does not count a primary inside an ng-template, a detail panel or a confirm dialog', () => {
    expect(drawsOwnPrimaryIn('<ng-template #a>' + primary + '</ng-template>')).toBe(false);
    expect(drawsOwnPrimaryIn('<kaff-detail-panel [open]="o"><div panelFooter>' + primary + '</div></kaff-detail-panel>')).toBe(false);
    expect(drawsOwnPrimaryIn('<kaff-confirm-dialog>' + primary + '</kaff-confirm-dialog>')).toBe(false);
  });

  it('counts the primary of an overlay bound always open: it is page body', () => {
    expect(drawsOwnPrimaryIn('<kaff-detail-panel [open]="true"><div panelFooter>' + primary + '</div></kaff-detail-panel>')).toBe(true);
    expect(drawsOwnPrimaryIn('<kaff-detail-panel open titleKey="t">' + primary + '</kaff-detail-panel>')).toBe(true);
    expect(drawsOwnPrimaryIn('<kaff-confirm-dialog [open]="true">' + primary + '</kaff-confirm-dialog>')).toBe(true);
    expect(drawsOwnPrimaryIn('<kaff-detail-panel [open]="opened()">' + primary + '</kaff-detail-panel>')).toBe(false);
  });

  it('still counts a body primary that sits beside an overlay', () => {
    expect(drawsOwnPrimaryIn('<kaff-detail-panel>' + primary + '</kaff-detail-panel>' + primary)).toBe(true);
  });
});

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
