import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { Bab, BabCreate, BabEdit, BabsApi } from '../../../core/catalogue/babs.api';
import { CatalogueApi, CatalogueItem } from '../../../core/catalogue/catalogue.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { BabTreePage } from './bab-tree-page';

function bab(
  code: string,
  parent: string | null,
  options: { active?: boolean; markup?: string } = {},
): Bab {
  return {
    id: `id-${code}`,
    code,
    nameAr: `باب ${code}`,
    nameEn: `Bab ${code}`,
    parentBabId: parent === null ? null : `id-${parent}`,
    defaultMarkup: options.markup ?? '0.150000',
    isActive: options.active ?? true,
  };
}

function item(code: string, babCode: string): CatalogueItem {
  return {
    id: `item-${code}`,
    code,
    descriptionAr: code,
    descriptionEn: code,
    unit: 'م²',
    babId: `id-${babCode}`,
    costPrice: '100.0000',
    baseSellRate: '120.0000',
    status: 'Active',
  };
}

/** CON (5 own items, children CNR and CNP), FIN (no own items, one child TIL), MAS, and OLD archived. */
const BABS: readonly Bab[] = [
  bab('CON', null),
  bab('CNR', 'CON'),
  bab('CNP', 'CON'),
  bab('FIN', null, { markup: '0.300000' }),
  bab('TIL', 'FIN'),
  bab('MAS', null),
  bab('OLD', null, { active: false }),
];
const ITEMS: readonly CatalogueItem[] = [
  ...[1, 2, 3, 4, 5].map((n) => item(`CON-${n}`, 'CON')),
  item('CNR-1', 'CNR'),
];

class FakeBabsApi implements Pick<BabsApi, 'list' | 'create' | 'edit' | 'move' | 'archive' | 'unarchive'> {
  babs: Bab[] = [...BABS];
  readonly archived: string[] = [];
  readonly unarchived: string[] = [];
  readonly edits: { id: string; edit: BabEdit }[] = [];
  readonly created: BabCreate[] = [];
  archiveFailure: HttpErrorResponse | null = null;
  createFailure: HttpErrorResponse | null = null;
  unarchiveFailure: HttpErrorResponse | null = null;

  async list(): Promise<readonly Bab[]> {
    return this.babs;
  }
  async create(request: BabCreate): Promise<Bab> {
    if (this.createFailure) {
      throw this.createFailure;
    }
    this.created.push(request);
    const made = bab(request.code, null);
    this.babs = [...this.babs, made];
    return made;
  }
  async edit(id: string, edit: BabEdit): Promise<Bab> {
    this.edits.push({ id, edit });
    const found = this.babs.find((b) => b.id === id)!;
    const next = { ...found, ...edit };
    this.babs = this.babs.map((b) => (b.id === id ? next : b));
    return next;
  }
  async move(id: string, parentBabId: string | null): Promise<{ id: string; parentBabId: string | null }> {
    return { id, parentBabId };
  }
  async archive(id: string): Promise<void> {
    if (this.archiveFailure) {
      throw this.archiveFailure;
    }
    this.archived.push(id);
    this.babs = this.babs.map((b) => (b.id === id ? { ...b, isActive: false } : b));
  }
  async unarchive(id: string): Promise<void> {
    if (this.unarchiveFailure) {
      throw this.unarchiveFailure;
    }
    this.unarchived.push(id);
    this.babs = this.babs.map((b) => (b.id === id ? { ...b, isActive: true } : b));
  }
}

class FakeCatalogueApi implements Pick<CatalogueApi, 'list'> {
  failWith: HttpErrorResponse | null = null;
  async list(): Promise<readonly CatalogueItem[]> {
    if (this.failWith) {
      throw this.failWith;
    }
    return ITEMS;
  }
}

class FakeI18nService implements Pick<I18nService, 't' | 'locale' | 'formatNumber'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    return params ? `${key}::${JSON.stringify(params)}` : key;
  }
  formatNumber(value: number | string): string {
    return String(value);
  }
}

const settle = async (fixture: ComponentFixture<unknown>): Promise<void> => {
  for (let i = 0; i < 5; i++) {
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }
};

async function render(
  inputs: Readonly<Record<string, unknown>> = {},
  api = new FakeBabsApi(),
  catalogue = new FakeCatalogueApi(),
) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: BabsApi, useValue: api },
      { provide: CatalogueApi, useValue: catalogue },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });
  const fixture = TestBed.createComponent(BabTreePage);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  await settle(fixture);
  const root: HTMLElement = fixture.nativeElement;
  const byId = (id: string): HTMLElement | null => root.querySelector<HTMLElement>(`[data-testid="${id}"]`);
  const click = async (id: string): Promise<void> => {
    (byId(id) as HTMLElement).click();
    fixture.detectChanges();
    await settle(fixture);
  };
  const menu = async (code: string, action: string): Promise<void> => {
    await click(`bab-row-menu-${code}`);
    await click(`bab-row-menu-${code}-${action}`);
  };
  const type = async (selector: string, value: string): Promise<void> => {
    const input = root.querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await settle(fixture);
  };
  return { api, fixture, root, byId, click, menu, type };
}

const problem = (status: number, messageKey: string, extensions: Record<string, unknown> = {}) =>
  new HttpErrorResponse({ status, error: { status, messageKey, ...extensions } });

describe('BabTreePage · the tree', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('is a treegrid whose rows carry their level, position and expanded state', async () => {
    const { root } = await render();
    const grid = root.querySelector('[role="treegrid"]');
    expect(grid).toBeTruthy();

    const con = root.querySelector('[data-testid="bab-row-CON"] [role="row"]')!;
    expect(con.getAttribute('aria-level')).toBe('1');
    expect(con.getAttribute('aria-expanded')).toBe('true');
    expect(con.getAttribute('aria-setsize')).toBe('3');

    const cnr = root.querySelector('[data-testid="bab-row-CNR"] [role="row"]')!;
    expect(cnr.getAttribute('aria-level')).toBe('2');
    expect(cnr.getAttribute('aria-posinset')).toBe('1');
    // A leaf has no disclosure state to announce.
    expect(cnr.getAttribute('aria-expanded')).toBeNull();
  });

  it('shows the active tree by default and hides the archived باب', async () => {
    const { byId } = await render();
    expect(byId('bab-row-CON')).toBeTruthy();
    expect(byId('bab-row-OLD')).toBeNull();
  });

  it('collapses a branch from the URL and the disclosure toggles it', async () => {
    const { byId, root } = await render({ collapsed: 'id-CON' });
    expect(byId('bab-row-CNR')).toBeNull();
    expect(root.querySelector('[data-testid="bab-toggle-CON"]')?.getAttribute('aria-expanded')).toBe('false');
  });

  it('flattens to a result list with the parent path while searching', async () => {
    const { byId, root } = await render({ search: 'cn' });
    expect(byId('bab-row-CNR')).toBeTruthy();
    expect(byId('bab-row-CNP')).toBeTruthy();
    expect(byId('bab-row-CON')).toBeNull();
    expect(root.querySelector('[data-testid="bab-toggle-CNR"]')).toBeNull();
    expect(root.textContent).toContain('bab.row.within');
  });

  it('says so when a search matches nothing, and offers to clear it', async () => {
    const { byId } = await render({ search: 'zzz' });
    expect(byId('bab-tree-empty-filtered')).toBeTruthy();
    expect(byId('bab-clear-search')).toBeTruthy();
  });

  it('draws the first-run state when no باب exists at all, with one primary action', async () => {
    const api = new FakeBabsApi();
    api.babs = [];
    const { byId, root } = await render({}, api);
    expect(byId('bab-tree-empty')).toBeTruthy();
    expect(root.querySelectorAll('[data-testid="bab-create"]').length).toBe(1);
    expect(byId('bab-first-run-example')).toBeTruthy();
  });
});

describe('BabTreePage · archive: a refusal when it holds its own items, a quiet confirmation when it does not', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('refuses at once, naming the OWN count, with no confirmation step', async () => {
    const { byId, menu, api } = await render();
    await menu('CON', 'archive');

    const refusal = byId('bab-archive-refusal-CON');
    expect(refusal).toBeTruthy();
    // Five items in CON itself, never the sixth that lives in its child CNR.
    expect(refusal?.textContent).toContain('"count":5');
    expect(byId('bab-archive-confirm-CON')).toBeNull();
    expect(api.archived).toEqual([]);
  });

  it('confirms quietly when it holds none, and does not count its children as a blocker', async () => {
    const { byId, menu, click, api } = await render();
    await menu('FIN', 'archive');

    expect(byId('bab-archive-refusal-FIN')).toBeNull();
    expect(byId('bab-archive-body-FIN')?.textContent).toContain('bab.archive.question');

    await click('bab-archive-confirm-FIN');
    // Only the one باب is archived: no cascade to TIL.
    expect(api.archived).toEqual(['id-FIN']);
    expect(api.archived).not.toContain('id-TIL');
  });

  it('a parent with active children but no items of its own archives', async () => {
    const { menu, click, api } = await render();
    await menu('FIN', 'archive');
    await click('bab-archive-confirm-FIN');
    expect(api.archived).toHaveLength(1);
  });

  it('turns a server refusal on a stale count into the refusal, carrying the server count', async () => {
    const api = new FakeBabsApi();
    api.archiveFailure = problem(409, 'errors.master.bab_has_active_items', { count: 7 });
    const catalogue = new FakeCatalogueApi();
    catalogue.failWith = problem(403, 'errors.auth.forbidden');
    const { byId, menu, click } = await render({}, api, catalogue);

    // No count could be read, so even CON goes to the confirmation and the server decides.
    await menu('CON', 'archive');
    expect(byId('bab-archive-body-CON')).toBeTruthy();
    await click('bab-archive-confirm-CON');
    expect(byId('bab-archive-refusal-CON')?.textContent).toContain('"count":7');
  });

  it('closes the refusal without archiving anything', async () => {
    const { byId, menu, click, api } = await render();
    await menu('CON', 'archive');
    await click('bab-archive-close-CON');
    expect(byId('bab-archive-refusal-CON')).toBeNull();
    expect(api.archived).toEqual([]);
  });
});

describe('BabTreePage · restore (AC-943-F)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('restores an archived باب in place through the unarchive call and says so', async () => {
    const { byId, menu, api } = await render({ status: 'archived' });
    await menu('OLD', 'unarchive');
    expect(api.unarchived).toEqual(['id-OLD']);
    expect(byId('bab-row-restored')).toBeTruthy();
  });

  it('offers no restore on an active row, and no add-child on an archived one', async () => {
    const { root, click } = await render({ status: 'all' });
    await click('bab-row-menu-OLD');
    expect(root.querySelector('[data-testid="bab-row-menu-OLD-unarchive"]')).toBeTruthy();
    expect(root.querySelector('[data-testid="bab-row-menu-OLD-child"]')).toBeNull();
  });

  it('shows the translated refusal when the باب is already active', async () => {
    const api = new FakeBabsApi();
    api.unarchiveFailure = problem(409, 'errors.master.not_archived');
    const { byId, menu } = await render({ status: 'archived' }, api);
    await menu('OLD', 'unarchive');
    expect(byId('bab-row-refusal-OLD')?.textContent).toContain('errors.master.not_archived');
  });
});

describe('BabTreePage · the markup is a deliberate edit', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('opens an editor with Save disabled until the value differs, then shows before and after', async () => {
    const { byId, click, type, root } = await render();
    await click('bab-markup-CON');

    const save = (byId('bab-markup-editor-CON-save') as HTMLButtonElement);
    expect(save.disabled).toBe(true);

    await type('[data-testid="bab-markup-editor-CON-input"]', '18');
    expect(save.disabled).toBe(false);
    expect(byId('bab-markup-editor-CON-delta')?.textContent).toContain('15%');
    expect(byId('bab-markup-editor-CON-delta')?.textContent).toContain('18%');
    expect(root.textContent).toContain('bab.markup.explanation');
  });

  it('saves only the markup, with the names unchanged, as the wire fraction', async () => {
    const { click, type, api, byId } = await render();
    await click('bab-markup-CON');
    await type('[data-testid="bab-markup-editor-CON-input"]', '18');
    await click('bab-markup-editor-CON-save');

    expect(api.edits).toEqual([
      { id: 'id-CON', edit: { nameAr: 'باب CON', nameEn: 'Bab CON', defaultMarkup: '0.18' } },
    ]);
    expect(byId('bab-markup-saved')).toBeTruthy();
  });

  it('refuses a value that is not a number and a value that is unchanged', async () => {
    const { byId, click, type } = await render();
    await click('bab-markup-CON');
    await type('[data-testid="bab-markup-editor-CON-input"]', '1x');
    expect((byId('bab-markup-editor-CON-save') as HTMLButtonElement).disabled).toBe(true);
    await type('[data-testid="bab-markup-editor-CON-input"]', '15.0');
    expect((byId('bab-markup-editor-CON-save') as HTMLButtonElement).disabled).toBe(true);
  });

  it('cancel closes the editor and changes nothing', async () => {
    const { byId, click, api } = await render();
    await click('bab-markup-CON');
    await click('bab-markup-editor-CON-cancel');
    expect(byId('bab-markup-editor-CON')).toBeNull();
    expect(api.edits).toEqual([]);
  });
});

describe('BabTreePage · create', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('refuses the empty form with a field error each, and has no duplicate-name error', async () => {
    const { byId, click, root } = await render();
    await click('bab-create');
    await click('bab-submit');

    expect(root.textContent).toContain('bab.error.name_ar_required');
    expect(root.textContent).toContain('bab.error.code_required');
    expect(root.textContent).toContain('bab.error.markup_required');
    expect(byId('bab-panel-errors')).toBeTruthy();
    expect(root.textContent).not.toMatch(/duplicate|name_taken|name_unique/);
  });

  it('refuses a repeated code case-insensitively, before the server is asked', async () => {
    const { click, type, root, api } = await render();
    await click('bab-create');
    await type('#bab-field-name-ar', 'أعمال');
    await type('#bab-field-name-en', 'Works');
    await type('#bab-field-code', 'con');
    await type('#bab-field-default-markup', '14');
    await click('bab-submit');

    expect(root.textContent).toContain('bab.error.code_taken');
    expect(api.created).toEqual([]);
  });

  it('lets two أبواب share an Arabic name', async () => {
    const { click, type, api } = await render();
    await click('bab-create');
    await type('#bab-field-name-ar', 'باب MAS');
    await type('#bab-field-name-en', 'Works');
    await type('#bab-field-code', 'scf');
    await type('#bab-field-default-markup', '14');
    await click('bab-submit');

    expect(api.created).toHaveLength(1);
  });

  it('sends the typed code uppercased, the markup as a fraction and no inherited markup', async () => {
    const { click, type, api } = await render();
    await click('bab-create');
    await type('#bab-field-name-ar', 'أعمال سقالات');
    await type('#bab-field-name-en', 'Scaffolding');
    await type('#bab-field-code', 'scf');
    await type('#bab-field-sort-order', '90');
    await type('#bab-field-default-markup', '12.75');
    await click('bab-submit');

    expect(api.created).toEqual([
      {
        code: 'SCF',
        nameAr: 'أعمال سقالات',
        nameEn: 'Scaffolding',
        parentBabId: null,
        defaultMarkup: '0.1275',
        sortOrder: 90,
      },
    ]);
  });

  it('"add a child" presets the parent and still asks for its own markup', async () => {
    const { menu, type, click, api, root } = await render();
    await menu('CON', 'child');
    expect(root.querySelector<HTMLInputElement>('#bab-field-default-markup')?.value).toBe('');

    await type('#bab-field-name-ar', 'فرع');
    await type('#bab-field-name-en', 'Child');
    await type('#bab-field-code', 'chd');
    await type('#bab-field-default-markup', '20');
    await click('bab-submit');

    expect(api.created[0].parentBabId).toBe('id-CON');
    expect(api.created[0].defaultMarkup).toBe('0.2');
  });

  it('the parent is chosen in a second step inside the panel, then returns to the form', async () => {
    const { byId, click, root } = await render();
    await click('bab-create');
    await click('bab-field-parent');
    expect(byId('bab-pick-done')).toBeTruthy();

    await click('bab-pick-option-MAS');
    await click('bab-pick-done');
    expect(root.querySelector('#bab-field-name-ar')).toBeTruthy();
    expect(byId('bab-field-parent')?.textContent).toContain('باب MAS');
  });

  it('shows the repeated-code refusal on the code field when the server answers 409', async () => {
    const api = new FakeBabsApi();
    api.createFailure = problem(409, 'errors.master.bab_code_taken');
    const { click, type, root } = await render({}, api);
    await click('bab-create');
    await type('#bab-field-name-ar', 'أعمال');
    await type('#bab-field-name-en', 'Works');
    await type('#bab-field-code', 'zzz');
    await type('#bab-field-default-markup', '14');
    await click('bab-submit');

    expect(root.textContent).toContain('bab.error.code_taken');
  });
});

describe('BabTreePage · edit', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('edits the two names and nothing else: the code and the parent are read-only, the sort order is absent', async () => {
    const { root, byId } = await render({ bab: 'CON' });
    expect(byId('bab-code')).toBeTruthy();
    expect(root.querySelector('#bab-field-code')).toBeNull();
    expect(root.querySelector('#bab-field-sort-order')).toBeNull();
    expect(root.querySelector('#bab-field-default-markup')).toBeNull();
    expect(root.querySelector('#bab-field-name-ar')).toBeTruthy();
    expect((byId('bab-submit') as HTMLButtonElement).disabled).toBe(true);
  });

  it('saves the names with the stored markup untouched', async () => {
    const { type, click, api } = await render({ bab: 'FIN' });
    await type('#bab-field-name-ar', 'تشطيبات');
    await click('bab-submit');
    expect(api.edits).toEqual([
      { id: 'id-FIN', edit: { nameAr: 'تشطيبات', nameEn: 'Bab FIN', defaultMarkup: '0.300000' } },
    ]);
  });
});
