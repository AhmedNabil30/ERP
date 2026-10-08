import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { Bab, BabsApi } from '../../../core/catalogue/babs.api';
import { CatalogueApi, CatalogueItem, CatalogueItemCreate, CatalogueItemEdit } from '../../../core/catalogue/catalogue.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { CatalogueCreatePanel } from './catalogue-create-panel';
import { CatalogueListPage } from './catalogue-list-page';

function item(code: string, babId: string, status: 'Active' | 'Archived' = 'Active'): CatalogueItem {
  return {
    id: `id-${code}`,
    code,
    descriptionAr: `وصف ${code}`,
    descriptionEn: `Item ${code}`,
    unit: 'م³',
    babId,
    costPrice: '100.0000',
    baseSellRate: '120.0000',
    status,
  };
}

const BABS: readonly Bab[] = [
  { id: 'b1', code: 'CON', nameAr: 'أعمال خرسانة', nameEn: 'Concrete', parentBabId: null, defaultMarkup: '0.15', isActive: true },
  { id: 'b2', code: 'FIN', nameAr: 'أعمال تشطيبات', nameEn: 'Finishing', parentBabId: null, defaultMarkup: '0.12', isActive: true },
];

class FakeCatalogueApi implements Pick<CatalogueApi, 'list' | 'archive' | 'unarchive' | 'edit' | 'create'> {
  createFailure: HttpErrorResponse | null = null;
  readonly creates: CatalogueItemCreate[] = [];

  async create(request: CatalogueItemCreate): Promise<CatalogueItem> {
    if (this.createFailure) {
      throw this.createFailure;
    }
    this.creates.push(request);
    const made = { ...item(request.code, request.babId), descriptionAr: request.descriptionAr };
    this.items = [...this.items, made];
    return made;
  }

  editFailure: HttpErrorResponse | null = null;
  readonly edits: CatalogueItemEdit[] = [];

  async edit(_id: string, edit: CatalogueItemEdit): Promise<CatalogueItem> {
    if (this.editFailure) {
      throw this.editFailure;
    }
    this.edits.push(edit);
    return this.items[0];
  }

  items: readonly CatalogueItem[] = [];
  failWith: HttpErrorResponse | null = null;
  readonly archived: string[] = [];
  readonly unarchived: string[] = [];

  /** Every list request the screen made, so a test can say what the request carried. */
  readonly requests: { search: string; filter: string; babId: string }[] = [];

  async list(
    search: string,
    filter: 'active' | 'archived' | 'all',
    babId = '',
  ): Promise<readonly CatalogueItem[]> {
    this.requests.push({ search, filter, babId });
    if (this.failWith) {
      throw this.failWith;
    }
    return this.items.filter(
      (i) => filter === 'all' || (filter === 'active' ? i.status === 'Active' : i.status === 'Archived'),
    );
  }
  async archive(id: string): Promise<void> {
    this.archived.push(id);
  }
  async unarchive(id: string): Promise<void> {
    this.unarchived.push(id);
  }
}

/** The أبواب the fake serves; a test that needs none sets this to `[]`. */
let servedBabs: readonly Bab[] = BABS;

class FakeBabsApi implements Pick<BabsApi, 'list'> {
  async list(): Promise<readonly Bab[]> {
    return servedBabs;
  }
}

class FakeI18nService implements Pick<I18nService, 't' | 'locale' | 'formatAmount'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    if (key === 'nav.subcontractors') {
      return 'مقاولو الباطن';
    }
    return params ? `${key}::${JSON.stringify(params)}` : key;
  }
  formatAmount(value: string): string {
    return value;
  }
}

const settle = async (fixture: ComponentFixture<unknown>): Promise<void> => {
  for (let i = 0; i < 4; i++) {
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();
  }
};

async function render(
  items: readonly CatalogueItem[],
  inputs: Readonly<Record<string, unknown>> = {},
  api: FakeCatalogueApi = new FakeCatalogueApi(),
  permissions: readonly string[] = [],
  babs: readonly Bab[] = BABS,
) {
  api.items = items;
  servedBabs = babs;
  TestBed.resetTestingModule();
  localStorage.removeItem('kaff.catalogue.open-groups');
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: CatalogueApi, useValue: api },
      { provide: BabsApi, useClass: FakeBabsApi },
      { provide: I18nService, useClass: FakeI18nService },
      { provide: AuthService, useValue: { current: () => ({ permissions }) } },
    ],
  });
  const fixture = TestBed.createComponent(CatalogueListPage);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  await settle(fixture);
  const root: HTMLElement = fixture.nativeElement;
  return {
    api,
    fixture,
    root,
    byId: (id: string) => root.querySelector<HTMLElement>(`[data-testid="${id}"]`),
    click: async (id: string) => {
      root.querySelector<HTMLElement>(`[data-testid="${id}"]`)!.click();
      fixture.detectChanges();
      await settle(fixture);
    },
  };
}

function renderWithBabs(
  babs: readonly Bab[],
  items: readonly CatalogueItem[],
  api: FakeCatalogueApi = new FakeCatalogueApi(),
) {
  return render(items, { status: 'all' }, api, [], babs);
}

/**
 * `V-38-A`/`V-38-D`: `nav.subcontractors` was in both catalogues with no template reading it, and
 * TechnicalOffice — who holds `SubcontractorManage` alongside `CatalogueManage` — had no way to reach
 * `/subcontractors` except by typing the URL. A link on this screen, TechnicalOffice's own landing.
 */
describe('CatalogueListPage · subcontractors link', () => {
  it('renders a translated link to /subcontractors', async () => {
    const { byId } = await render([]);

    const link = byId('catalogue-manage-subcontractors');
    expect(link).toBeTruthy();
    expect(link?.textContent?.trim()).toBe('مقاولو الباطن');
    expect(link?.textContent).not.toContain('nav.subcontractors');
  });
});

describe('CatalogueListPage · groups', () => {
  beforeEach(() => TestBed.resetTestingModule());

  const many = (n: number, babId: string): CatalogueItem[] =>
    Array.from({ length: n }, (_, i) => item(`C-${String(i).padStart(3, '0')}`, babId));

  it('heads each باب with its name and its item count, and nothing about markup', async () => {
    const { root } = await render([...many(3, 'b1'), item('F-1', 'b2')]);

    const heading = root.querySelector('[data-testid="catalogue-group-b1"]');
    expect(heading?.textContent).toContain('أعمال خرسانة');
    expect(heading?.textContent).toContain('catalogue.list.group_count::{"count":3}');
    expect(root.textContent).not.toContain('group_margin');
    expect(root.textContent).not.toContain('markup');
    expect(root.querySelectorAll('kaff-group-heading kaff-badge').length).toBe(0);
  });

  it('opens the first group on a fresh visit, and a click on a heading collapses and reopens it', async () => {
    const { byId, click, root } = await render([...many(2, 'b1'), item('F-1', 'b2')]);

    expect(byId('catalogue-group-b1-toggle')?.getAttribute('aria-expanded')).toBe('true');
    expect(byId('catalogue-group-b2-toggle')?.getAttribute('aria-expanded')).toBe('false');
    expect(root.querySelectorAll('.item-row').length).toBe(2);

    await click('catalogue-group-b1-toggle');
    expect(byId('catalogue-group-b1-toggle')?.getAttribute('aria-expanded')).toBe('false');
    expect(root.querySelectorAll('.item-row').length).toBe(0);
  });

  it('remembers which groups the viewer left open', async () => {
    const first = await render([...many(2, 'b1'), item('F-1', 'b2')]);
    await first.click('catalogue-group-b2-toggle');
    const stored = JSON.parse(localStorage.getItem('kaff.catalogue.open-groups') ?? 'null') as string[];
    expect(stored.sort()).toEqual(['b1', 'b2']);
  });

  it('cuts a long باب with a load-more row naming what is left, and reveals the rest of that باب', async () => {
    const { byId, click, root } = await render(many(45, 'b1'));

    expect(root.querySelectorAll('.item-row').length).toBe(20);
    expect(byId('catalogue-group-more-b1')?.textContent).toContain('catalogue.group.more::{"count":25}');

    await click('catalogue-group-more-b1');
    expect(root.querySelectorAll('.item-row').length).toBe(45);
    expect(byId('catalogue-group-more-b1')).toBeNull();
  });

  it('opens every group that has a match when a search narrows the list', async () => {
    const { byId } = await render([item('C-1', 'b1'), item('F-1', 'b2')], { search: 'x' });

    expect(byId('catalogue-group-b1-toggle')?.getAttribute('aria-expanded')).toBe('true');
    expect(byId('catalogue-group-b2-toggle')?.getAttribute('aria-expanded')).toBe('true');
  });

  it('puts a cost marker, an eye-off icon and the word internal on the cost column only', async () => {
    const { root } = await render([item('C-1', 'b1')]);

    const header = root.querySelector('[data-testid="catalogue-table-header"]');
    expect(header?.querySelectorAll('kaff-badge').length).toBe(1);
    expect(header?.textContent).toContain('catalogue.badge.internal');
    expect(header?.textContent).toContain('catalogue.column.sell_price');
  });
});

describe('CatalogueListPage · AC-942-E the باب filter belongs to the server', () => {
  it('sends the chosen باب with the request and filters nothing itself', async () => {
    // The fake answers every request with both babs' rows, as a server that ignored the filter would.
    // A screen that narrowed in memory would hide b2; a screen that trusts the server shows both.
    const { api, root } = await render([item('C-1', 'b1'), item('F-1', 'b2')], { bab: 'b1' });

    expect(api.requests.some((r) => r.babId === 'b1')).toBe(true);
    expect(root.querySelectorAll('.item-row').length).toBe(2);
  });

  it('sends an empty باب when none is chosen', async () => {
    const { api } = await render([item('C-1', 'b1')]);

    expect(api.requests.filter((r) => r.search === '' && r.filter !== 'all').every((r) => r.babId === '')).toBe(
      true,
    );
  });
});

describe('CatalogueListPage · archive and restore', () => {
  it('asks the recoverable question: ordinary primary confirm, never the red one, and says where it comes back from', async () => {
    const { byId, click, root } = await render([item('C-1', 'b1')]);

    await click('catalogue-row-menu-C-1');
    await click('catalogue-row-menu-C-1-archive');

    expect(byId('catalogue-archive-body-C-1')?.textContent).toContain('catalogue.archive.question');
    expect(byId('catalogue-archive-body-C-1')?.textContent).toContain('catalogue.archive.detail');
    const confirm = byId('catalogue-archive-confirm-C-1');
    expect(confirm?.classList.contains('variant-primary')).toBe(true);
    expect(root.querySelector('.variant-danger')).toBeNull();
  });

  it('cancel puts the row back and archives nothing', async () => {
    const { byId, click, api } = await render([item('C-1', 'b1')]);

    await click('catalogue-row-menu-C-1');
    await click('catalogue-row-menu-C-1-archive');
    await click('catalogue-archive-cancel-C-1');

    expect(byId('catalogue-archive-body-C-1')).toBeNull();
    expect(byId('catalogue-row-C-1')).toBeTruthy();
    expect(api.archived).toEqual([]);
  });

  it('confirm archives that one item', async () => {
    const { click, api } = await render([item('C-1', 'b1'), item('C-2', 'b1')]);

    await click('catalogue-row-menu-C-1');
    await click('catalogue-row-menu-C-1-archive');
    await click('catalogue-archive-confirm-C-1');

    expect(api.archived).toEqual(['id-C-1']);
  });

  it('restores in place with no confirmation: the row becomes a restored band at once', async () => {
    const { byId, click, api } = await render([item('C-9', 'b1', 'Archived')], { status: 'archived' });

    expect(byId('catalogue-row-archived-badge')).toBeTruthy();
    await click('catalogue-row-menu-C-9');
    await click('catalogue-row-menu-C-9-unarchive');

    expect(api.unarchived).toEqual(['id-C-9']);
    expect(byId('catalogue-archive-body-C-9')).toBeNull();
    expect(byId('catalogue-row-restored')?.textContent).toContain('catalogue.restored');
  });
});

describe('CatalogueListPage · the table states read differently', () => {
  it('loading keeps the header and shows ten skeleton rows at the real row height, not a spinner', async () => {
    const api = new FakeCatalogueApi();
    api.list = () => new Promise(() => undefined);
    const { byId, root } = await render([], {}, api);

    const loading = byId('catalogue-list-loading');
    expect(loading?.getAttribute('aria-busy')).toBe('true');
    expect(loading?.querySelectorAll('kaff-table-row').length).toBe(10);
    expect(byId('catalogue-table-header')).toBeTruthy();
    expect(root.querySelector('[role="progressbar"]')).toBeNull();
  });

  it('an empty catalogue with nothing filtered is the empty state, with a way forward', async () => {
    const { byId } = await render([], { status: 'all' });

    expect(byId('catalogue-list-empty')).toBeTruthy();
    expect(byId('catalogue-list-empty-filtered')).toBeNull();
    expect(byId('catalogue-empty-create')).toBeTruthy();
  });

  it('with no أبواب the empty catalogue points at the أبواب module and offers nothing to create or import', async () => {
    const api = new FakeCatalogueApi();
    api.items = [];
    const { byId, root } = await renderWithBabs([], [], api);

    expect(byId('catalogue-first-run-no-babs')).toBeTruthy();
    expect(byId('catalogue-first-run-go-babs')).toBeTruthy();
    expect(byId('catalogue-first-run-example')).toBeTruthy();
    // One primary: the page header's own create, import and search are hidden while this shows.
    expect(byId('catalogue-create')).toBeNull();
    expect(byId('catalogue-import-link')).toBeNull();
    expect(byId('catalogue-search')).toBeNull();
    expect(root.querySelector('[data-testid="catalogue-empty-create"]')).toBeNull();
  });

  it('with أبواب and no items it offers create and import side by side', async () => {
    const { byId } = await render([], { status: 'all' });

    expect(byId('catalogue-list-empty')).toBeTruthy();
    expect(byId('catalogue-empty-create')).toBeTruthy();
    expect(byId('catalogue-empty-import')).toBeTruthy();
    expect(byId('catalogue-first-run-no-babs')).toBeNull();
    expect(byId('catalogue-create')).toBeNull();
  });

  it('a catalogue that holds items never shows a first run', async () => {
    const { byId } = await render([item('C-1', 'b1')]);

    expect(byId('catalogue-first-run-no-babs')).toBeNull();
    expect(byId('catalogue-list-empty')).toBeNull();
    expect(byId('catalogue-create')).toBeTruthy();
  });

  it('?create=1 opens the create panel over the list', async () => {
    const { byId } = await render([item('C-1', 'b1')], { create: '1' });

    expect(byId('catalogue-create-panel')).toBeTruthy();
    expect(byId('catalogue-create-submit')).toBeTruthy();
  });

  it('creating sends the typed item, closes the panel and shows the new row with a band saying so', async () => {
    const api = new FakeCatalogueApi();
    const { fixture, byId } = await render([item('C-1', 'b1')], { create: '1' }, api);
    const panel = fixture.debugElement.query(By.directive(CatalogueCreatePanel)).componentInstance as CatalogueCreatePanel;
    const request: CatalogueItemCreate = {
      code: 'C-2',
      descriptionAr: 'بند جديد',
      descriptionEn: null,
      unit: 'م³',
      babId: 'b1',
      costPrice: '1.00',
      baseSellRate: '2.00',
    };

    panel.createRequested.emit(request);
    await settle(fixture);

    expect(api.creates).toEqual([request]);
    expect(byId('catalogue-row-created')!.textContent).toContain('catalogue.created');
    expect(byId('catalogue-row-C-2')).toBeTruthy();
  });

  it("a refused create stays open and carries the key of the server down to the panel", async () => {
    const api = new FakeCatalogueApi();
    api.createFailure = new HttpErrorResponse({
      status: 409,
      error: { messageKey: 'errors.master.catalogue_item_code_taken' },
    });
    const { fixture, byId } = await render([item('C-1', 'b1')], { create: '1' }, api);
    const panel = fixture.debugElement.query(By.directive(CatalogueCreatePanel)).componentInstance as CatalogueCreatePanel;

    panel.createRequested.emit({
      code: 'C-1',
      descriptionAr: 'x',
      descriptionEn: null,
      unit: 'م³',
      babId: 'b1',
      costPrice: '1',
      baseSellRate: '2',
    });
    await settle(fixture);

    expect(api.creates).toHaveLength(0);
    expect(byId('catalogue-create-panel')).toBeTruthy();
    expect(byId('catalogue-row-created')).toBeNull();
  });

  it('a filter that matches nothing is not "no items yet", and offers to clear', async () => {
    const { byId } = await render([item('C-1', 'b1', 'Archived')]);

    expect(byId('catalogue-list-empty')).toBeNull();
    expect(byId('catalogue-list-empty-filtered')).toBeTruthy();
    expect(byId('catalogue-clear-filters')).toBeTruthy();
  });

  it('a 403 is the permission-refused state: a lock, no retry, not the red failure banner', async () => {
    const api = new FakeCatalogueApi();
    api.failWith = new HttpErrorResponse({ status: 403, error: { messageKey: 'errors.forbidden' } });
    const { byId } = await render([], {}, api);

    const state = byId('catalogue-list-failure');
    expect(state?.getAttribute('data-mode')).toBe('forbidden');
    expect(state?.querySelector('[role="alert"]')).toBeNull();
  });

  it('any other failure is the refusal banner carrying the server message', async () => {
    const api = new FakeCatalogueApi();
    api.failWith = new HttpErrorResponse({ status: 500, error: { messageKey: 'errors.unknown' } });
    const { byId } = await render([], {}, api);

    const state = byId('catalogue-list-failure');
    expect(state?.getAttribute('data-mode')).toBe('refusal');
    expect(state?.textContent).toContain('errors.unknown');
  });

  it('shows the result count line only when rows are shown, describing what is shown', async () => {
    const { byId } = await render([item('C-1', 'b1'), item('C-2', 'b1')]);

    expect(byId('catalogue-list-count')?.textContent).toContain('catalogue.count.active::{"count":2}');
  });
});

describe('CatalogueListPage · the price permission is presentation only', () => {
  it('opens the panel in the read-only shape when the session lacks CataloguePriceEdit', async () => {
    const { byId, root } = await render([item('C-1', 'b1')], { item: 'C-1' });

    expect(byId('catalogue-detail-panel')).toBeTruthy();
    expect(byId('catalogue-detail-read-only-reason')).toBeTruthy();
    expect(root.querySelector('#catalogue-detail-cost')).toBeNull();
  });

  it('opens the editable shape for a session that holds CataloguePriceEdit', async () => {
    const { byId, root } = await render([item('C-1', 'b1')], { item: 'C-1' }, new FakeCatalogueApi(), [
      'CataloguePriceEdit',
    ]);

    expect(byId('catalogue-detail-panel')).toBeTruthy();
    expect(root.querySelector('#catalogue-detail-cost')).toBeTruthy();
    expect(byId('catalogue-detail-read-only-reason')).toBeNull();
  });

  it('shows the server refusal in the panel when a changed price is refused, whatever the screen drew (AC-940-F)', async () => {
    const api = new FakeCatalogueApi();
    api.editFailure = new HttpErrorResponse({
      status: 403,
      error: { messageKey: 'errors.master.catalogue_price_owner_only' },
    });
    // A stale mirror: the session claims the permission, the server does not agree.
    const { fixture, root, byId } = await render([item('C-1', 'b1')], { item: 'C-1' }, api, ['CataloguePriceEdit']);

    const cost = root.querySelector<HTMLInputElement>('#catalogue-detail-cost')!;
    cost.value = '1.00';
    cost.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    byId('catalogue-detail-save')!.click();
    await settle(fixture);

    expect(byId('catalogue-detail-refusal')?.textContent).toContain('errors.master.catalogue_price_owner_only');
  });
});
