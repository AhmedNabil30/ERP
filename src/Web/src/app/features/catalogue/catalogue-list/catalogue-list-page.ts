import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';
import { Bab, BabsApi } from '../../../core/catalogue/babs.api';
import {
  CatalogueApi,
  CatalogueItem,
  CatalogueItemCreate,
  CatalogueItemEdit,
  CatalogueItemListFilter,
} from '../../../core/catalogue/catalogue.api';
import { CatalogueGroup, groupByBab } from '../../../core/catalogue/group-by-bab';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffFirstRun } from '../../../shared/kaff-first-run/kaff-first-run';
import { KaffGroupHeading } from '../../../shared/kaff-group-heading/kaff-group-heading';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffInlineConfirm } from '../../../shared/kaff-inline-confirm/kaff-inline-confirm';
import { KaffRowMenu, RowMenuItem } from '../../../shared/kaff-row-menu/kaff-row-menu';
import { KaffSearchField } from '../../../shared/kaff-search-field/kaff-search-field';
import {
  KaffSegmentedFilter,
  SegmentedFilterOption,
} from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffSelect, SelectOption } from '../../../shared/kaff-select/kaff-select';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';
import { CatalogueCreatePanel } from './catalogue-create-panel';
import { CatalogueItemPanel } from './catalogue-item-panel';

/** The three segments `KAFF-206` rule 7 draws, in the order it draws them. */
const FILTERS: readonly CatalogueItemListFilter[] = ['active', 'archived', 'all'];

/** How many of a باب's items its group shows before the in-group "load more" row (README "Designing for 612 items"). */
const GROUP_PAGE = 20;

/** Where the open/closed state of the باب groups is remembered between visits. A per-viewer convenience only. */
const OPEN_GROUPS_KEY = 'kaff.catalogue.open-groups';

/** How long the "restored" band replaces an un-archived row before the row returns in its new place. */
const RESTORED_BAND_MS = 2500;
const SAVED_BANNER_MS = 3000;
/** How long the "created" band stays under the new row. */
const CREATED_BAND_MS = 4000;

const PHONE_QUERY = '(max-width: 820px)';

const SUPERSCRIPTS: Readonly<Record<string, string>> = { '²': '2', '³': '3' };

interface Totals {
  readonly count: number;
  readonly babCount: number;
}

/**
 * S-017 · the catalogue list (كتالوج الأسعار), grouped by باب. `KAFF-203` (search), `KAFF-206`
 * (archive and un-archive), `KAFF-940` (cost and sell rate belong to the Owner alone).
 *
 * **Two independent stored figures.** `costPrice` is internal and keeps its tint, its eye-off icon and
 * its «داخلي» badge; `baseSellRate` is a plain stored figure. Neither is calculated from the other and
 * neither explains the other (D-179). The باب's default markup is `lineMarkup` on a BOQ line, not a
 * catalogue concept, so it appears nowhere on this screen.
 *
 * **Filter and search live in the URL, not in a signal.** `search`, `status`, `bab` and `item` are
 * `input()`s bound by `withComponentInputBinding` straight from the query string; every control writes
 * to the URL and reads the result back, so a bookmark, a refresh and a shared link all restore the view.
 * The router sets an absent param to `undefined` rather than the declared default, so the inputs are
 * only ever read through the `computed`s below.
 *
 * **612 items across 8 أبواب.** The list is grouped under collapsible, sticky باب headings. A group
 * shows its first {@link GROUP_PAGE} items and ends with a "load more" row that reveals the rest of
 * that باب — never a page boundary that would cut a باب in two. Which groups are open is remembered per
 * viewer; a search, a باب choice or the archived view opens every group that has matches.
 *
 * **Archive is reversible, so its confirmation is the quiet one** and says where the item comes back
 * from. Un-archive needs no confirmation: the row becomes a one-line "restored" band for a moment, then
 * returns in its new place. There is no toast anywhere on this screen.
 *
 * **Permissions.** `canEditPrices` mirrors `Permission.CataloguePriceEdit` (Owner only) so the panel
 * draws the right shape. It is presentation: the server refuses a changed price from anyone else
 * regardless of what this screen sends, and the refusal renders in the panel (AC-940-F).
 */
@Component({
  selector: 'kaff-catalogue-list-page',
  imports: [
    RouterLink,
    KaffBadge,
    KaffBanner,
    KaffButton,
    KaffFirstRun,
    KaffGroupHeading,
    KaffIcon,
    KaffInlineConfirm,
    KaffRowMenu,
    KaffSearchField,
    KaffSegmentedFilter,
    KaffSelect,
    KaffState,
    KaffTable,
    KaffTableRow,
    CatalogueCreatePanel,
    CatalogueItemPanel,
  ],
  templateUrl: './catalogue-list-page.html',
  styleUrl: './catalogue-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogueListPage {
  private readonly api = inject(CatalogueApi);
  private readonly babsApi = inject(BabsApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly i18n = inject(I18nService);

  /** Tracks run in `rowColumns` order below; the English column drops out of the narrow variant in CSS. */
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'catalogue.column.description', pinned: true },
    { labelKey: 'catalogue.column.description_en', cssClass: 'cell-en' },
    { labelKey: 'catalogue.column.unit' },
    {
      labelKey: 'catalogue.column.cost_price',
      align: 'end',
      cssClass: 'cell-internal',
      markerIcon: 'eyeoff',
      markerKey: 'catalogue.badge.internal',
    },
    { labelKey: 'catalogue.column.sell_price', align: 'end' },
    {},
  ];

  /** Ten skeleton rows at the real row height (README "Loading"). */
  protected readonly skeletonRows = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9] as const;

  protected readonly segmentedOptions: readonly SegmentedFilterOption<CatalogueItemListFilter>[] =
    FILTERS.map((filter) => ({ value: filter, labelKey: `catalogue.filter.${filter}` }));

  /** Bound by name from `?search=`, `?status=`, `?bab=` and `?item=` — `withComponentInputBinding`. */
  readonly search = input<string>('');
  readonly status = input<CatalogueItemListFilter>('active');
  readonly bab = input<string>('');
  readonly item = input<string>('');
  /** `?create=1` opens the create panel over the list. */
  readonly create = input<string>('');

  protected readonly searchTerm = computed(() => this.search() ?? '');
  protected readonly statusFilter = computed(() => this.status() ?? 'active');
  protected readonly babFilter = computed(() => this.bab() ?? '');
  protected readonly openItemCode = computed(() => this.item() ?? '');
  protected readonly createOpen = computed(() => (this.create() ?? '') === '1');

  protected readonly items = signal<readonly CatalogueItem[]>([]);
  protected readonly babs = signal<readonly Bab[]>([]);
  protected readonly totals = signal<Totals | null>(null);
  protected readonly loading = signal(true);
  /** False until the first read of the whole catalogue has settled, so a first-run state never flashes. */
  protected readonly totalsReady = signal(false);
  /** True once the باب read succeeded: an unread باب list is not evidence that none exists. */
  protected readonly babsLoaded = signal(false);
  /** Every code in the catalogue, archived included: codes are unique across all of them. */
  protected readonly takenCodes = signal<ReadonlySet<string>>(new Set());
  protected readonly failure = signal<{ readonly key: string; readonly status: number } | null>(null);

  protected readonly isPhone = signal(typeof matchMedia === 'function' && matchMedia(PHONE_QUERY).matches);

  /** Row-level state. Only one confirmation is open at a time (README "Row actions"). */
  protected readonly confirmingArchiveId = signal<string | null>(null);
  protected readonly archivingId = signal<string | null>(null);
  protected readonly rowFailure = signal<{ readonly id: string; readonly key: string } | null>(null);
  protected readonly restored = signal<{ readonly id: string; readonly name: string } | null>(null);

  /** Create panel state. */
  protected readonly createSaving = signal(false);
  protected readonly createRefusal = signal<string | null>(null);
  protected readonly created = signal<{ readonly id: string; readonly code: string; readonly name: string } | null>(
    null,
  );

  /** Panel state. */
  protected readonly panelSaving = signal(false);
  protected readonly panelArchiving = signal(false);
  protected readonly panelSaved = signal(false);
  protected readonly panelRefusal = signal<string | null>(null);

  /** The user's own open groups, or `null` before they have ever toggled one (then the first is open). */
  private readonly openGroups = signal<ReadonlySet<string> | null>(readOpenGroups());
  private readonly expandedGroups = signal<ReadonlySet<string>>(new Set());

  /** The panel's price shape. Mirrors `Permission.CataloguePriceEdit`; the server is the control. */
  protected readonly canEditPrices = computed(
    () => this.auth.current()?.permissions.includes('CataloguePriceEdit') ?? false,
  );

  private readonly babNameById = computed(() => {
    const english = this.i18n.locale() === 'en';
    return new Map(this.babs().map((bab) => [bab.id, english ? bab.nameEn : bab.nameAr]));
  });

  protected readonly babOptions = computed<readonly SelectOption[]>(() => [
    { value: '', label: this.i18n.t('catalogue.filter.bab_all') },
    ...this.babs()
      .filter((bab) => bab.isActive)
      .map((bab) => ({ value: bab.id, label: this.babNameById().get(bab.id) ?? bab.nameAr })),
  ]);

  protected readonly groups = computed<readonly CatalogueGroup[]>(() =>
    groupByBab(this.items(), this.babs()),
  );

  protected readonly openItem = computed<CatalogueItem | null>(() => {
    const code = this.openItemCode();
    return code.length === 0 ? null : (this.items().find((item) => item.code === code) ?? null);
  });

  protected readonly openItemBabName = computed(() => {
    const item = this.openItem();
    return item === null ? null : (this.babNameById().get(item.babId) ?? null);
  });

  /** A search, a باب choice or the archived view narrows the list to what matched, so every group opens. */
  private readonly forceOpen = computed(
    () =>
      this.searchTerm().trim().length > 0 ||
      this.babFilter().length > 0 ||
      this.statusFilter() === 'archived',
  );

  /** True when the view could be hiding rows that exist: `active` is itself a filter (V-36-A). */
  protected readonly isFiltered = computed(
    () =>
      this.searchTerm().trim().length > 0 ||
      this.babFilter().length > 0 ||
      this.statusFilter() !== 'all',
  );

  protected readonly hasNarrowing = computed(
    () => this.searchTerm().trim().length > 0 || this.babFilter().length > 0,
  );

  protected readonly metaText = computed(() => {
    const totals = this.totals();
    return totals === null
      ? null
      : this.i18n.t('catalogue.list.count', { count: totals.count, babCount: totals.babCount });
  });

  private readonly scopeText = computed(() => this.i18n.t(`catalogue.scope.${this.statusFilter()}`));

  /** What is shown, not what exists (README "Result count line"). */
  protected readonly countText = computed(() => {
    const count = this.items().length;
    const query = this.searchTerm().trim();
    if (query.length > 0) {
      return this.i18n.t('catalogue.count.search', { count, query, scope: this.scopeText() });
    }
    if (this.babFilter().length > 0) {
      return this.i18n.t('catalogue.count.bab', { count, scope: this.scopeText() });
    }
    return this.i18n.t(`catalogue.count.${this.statusFilter()}`, { count });
  });

  protected readonly emptyFilteredBody = computed(() =>
    this.i18n.t('catalogue.list.empty_filtered_body', {
      what: this.searchTerm().trim() || this.i18n.t('catalogue.list.empty_filtered_current'),
      scope: this.scopeText(),
    }),
  );

  /** The archived view with nothing narrowing it has its own words (README "Empty states"). */
  private readonly activeBabCount = computed(() => this.babs().filter((bab) => bab.isActive).length);

  /**
   * The two first runs (README "The catalogue's first run"). They are about the WHOLE catalogue, not the
   * current filter: with no active باب an item has nothing to attach to, so the screen points at the أبواب
   * module; with أبواب and no items at all it offers create and import side by side.
   */
  protected readonly isFirstRunNoBabs = computed(
    () => this.isCatalogueEmpty() && this.babsLoaded() && this.activeBabCount() === 0,
  );
  protected readonly isFirstRunNoItems = computed(
    () => this.isCatalogueEmpty() && this.babsLoaded() && this.activeBabCount() > 0,
  );
  protected readonly isFirstRun = computed(() => this.isFirstRunNoBabs() || this.isFirstRunNoItems());

  private readonly isCatalogueEmpty = computed(
    () => this.totalsReady() && this.totals()?.count === 0 && this.failure() === null,
  );

  protected readonly archivedEmpty = computed(
    () => this.statusFilter() === 'archived' && !this.hasNarrowing(),
  );

  constructor() {
    void this.loadBabs();
    void this.loadTotals();

    if (typeof matchMedia === 'function') {
      const query = matchMedia(PHONE_QUERY);
      const onChange = (event: MediaQueryListEvent): void => this.isPhone.set(event.matches);
      query.addEventListener('change', onChange);
      this.destroyRef.onDestroy(() => query.removeEventListener('change', onChange));
    }

    effect(() => {
      // Search, status and باب all go to the server. Reading them re-runs this.
      void this.searchTerm();
      void this.babFilter();
      void this.statusFilter();
      this.expandedGroups.set(new Set());
      void this.reload();
    });

    this.destroyRef.onDestroy(() => this.clearTimers());
  }

  // ---- toolbar -----------------------------------------------------------------------------------

  protected onSearch(term: string): Promise<void> {
    return this.navigate({ search: term.length > 0 ? term : null });
  }

  protected onFilter(filter: CatalogueItemListFilter): Promise<void> {
    return this.navigate({ status: filter === 'active' ? null : filter });
  }

  protected onBab(babId: string): Promise<void> {
    return this.navigate({ bab: babId.length > 0 ? babId : null });
  }

  protected onClearFilters(): Promise<void> {
    return this.navigate({ search: null, status: null, bab: null });
  }

  protected onCreate(): Promise<void> {
    this.createRefusal.set(null);
    return this.navigate({ create: '1', item: null });
  }

  protected onCloseCreate(): Promise<void> {
    this.createRefusal.set(null);
    return this.navigate({ create: null });
  }

  protected onGoToBabs(): Promise<boolean> {
    return this.router.navigate(['/babs']);
  }

  protected onImport(): Promise<boolean> {
    return this.router.navigate(['/catalogue/import']);
  }

  // ---- groups ------------------------------------------------------------------------------------

  protected isOpen(group: CatalogueGroup, index: number): boolean {
    if (this.forceOpen()) {
      return true;
    }
    const stored = this.openGroups();
    return stored === null ? index === 0 : stored.has(group.babId);
  }

  protected onToggleGroup(group: CatalogueGroup, index: number): void {
    const current = new Set(
      this.openGroups() ?? this.groups().filter((g, i) => this.isOpen(g, i)).map((g) => g.babId),
    );
    if (this.isOpen(group, index) && !this.forceOpen()) {
      current.delete(group.babId);
    } else {
      current.add(group.babId);
    }
    this.openGroups.set(current);
    writeOpenGroups(current);
  }

  protected visibleRows(group: CatalogueGroup): readonly CatalogueItem[] {
    return this.expandedGroups().has(group.babId) ? group.items : group.items.slice(0, GROUP_PAGE);
  }

  protected hiddenCount(group: CatalogueGroup): number {
    return this.expandedGroups().has(group.babId) ? 0 : Math.max(0, group.items.length - GROUP_PAGE);
  }

  protected onLoadMore(group: CatalogueGroup): void {
    this.expandedGroups.update((set) => new Set(set).add(group.babId));
  }

  protected babLabel(group: CatalogueGroup): string {
    return group.bab === null
      ? this.i18n.t('catalogue.list.bab_unknown')
      : (this.babNameById().get(group.bab.id) ?? group.bab.nameAr);
  }

  protected groupCountText(group: CatalogueGroup): string {
    return this.i18n.t('catalogue.list.group_count', { count: group.items.length });
  }

  protected moreText(group: CatalogueGroup): string {
    return this.i18n.t('catalogue.group.more', { count: this.hiddenCount(group) });
  }

  // ---- rows --------------------------------------------------------------------------------------

  protected rowMenuItems(item: CatalogueItem): readonly RowMenuItem[] {
    const read: readonly RowMenuItem[] = [
      { labelKey: 'catalogue.menu.view', action: 'view', icon: 'eye' },
      { labelKey: 'catalogue.menu.edit', action: 'edit', icon: 'pen' },
    ];
    // Archive is recoverable, so it is ink-coloured with the archive icon and an ellipsis — never the
    // red danger tone, which is reserved for what nothing can bring back.
    return item.status === 'Active'
      ? [
          ...read,
          {
            labelKey: 'catalogue.menu.archive',
            action: 'archive',
            icon: 'arch',
            separatorBefore: true,
          },
        ]
      : [
          ...read,
          {
            labelKey: 'catalogue.item.unarchive_action',
            action: 'unarchive',
            icon: 'unarch',
            separatorBefore: true,
            disabled: this.archivingId() === item.id,
          },
        ];
  }

  protected onRowMenuSelected(item: CatalogueItem, action: string): void {
    if (action === 'view' || action === 'edit') {
      void this.onOpenItem(item);
    } else if (action === 'archive') {
      this.onStartArchive(item.id);
    } else if (action === 'unarchive') {
      void this.onUnarchive(item);
    }
  }

  /** A click anywhere on a row opens the panel, except on the row's own controls. */
  protected onRowClick(item: CatalogueItem, event: Event): void {
    const target = event.target as HTMLElement | null;
    if (target?.closest('button, a, kaff-row-menu')) {
      return;
    }
    void this.onOpenItem(item);
  }

  protected onOpenItem(item: CatalogueItem): Promise<void> {
    this.panelRefusal.set(null);
    this.panelSaved.set(false);
    return this.navigate({ item: item.code });
  }

  protected rowActionsLabel(item: CatalogueItem): string {
    return this.i18n.t('catalogue.row.actions', { name: item.descriptionAr });
  }

  protected isRestored(item: CatalogueItem): boolean {
    return this.restored()?.id === item.id;
  }

  protected restoredText(): string {
    return this.i18n.t('catalogue.restored', { name: this.restored()?.name ?? '' });
  }

  protected archiveQuestion(item: CatalogueItem): string {
    return this.i18n.t('catalogue.archive.question', { name: item.descriptionAr });
  }

  protected unitParts(unit: string): { readonly base: string; readonly sup: string | null } {
    const last = unit.at(-1) ?? '';
    const sup = SUPERSCRIPTS[last];
    return sup === undefined ? { base: unit, sup: null } : { base: unit.slice(0, -1), sup };
  }

  // ---- archive and restore -----------------------------------------------------------------------

  protected onStartArchive(id: string): void {
    this.rowFailure.set(null);
    this.confirmingArchiveId.set(id);
  }

  protected onCancelArchive(): void {
    this.confirmingArchiveId.set(null);
  }

  protected async onConfirmArchive(id: string): Promise<void> {
    this.rowFailure.set(null);
    this.archivingId.set(id);
    try {
      await this.api.archive(id);
      this.confirmingArchiveId.set(null);
      await this.refresh();
    } catch (error) {
      // AC-206-E: surfaced next to the row that produced it, not swallowed.
      this.rowFailure.set({ id, key: toProblem(error).messageKey });
      this.confirmingArchiveId.set(null);
    } finally {
      this.archivingId.set(null);
    }
  }

  /** Restores in place: no confirmation, no toast. The row says so for a moment, then returns. */
  protected async onUnarchive(item: CatalogueItem): Promise<void> {
    this.rowFailure.set(null);
    this.archivingId.set(item.id);
    try {
      await this.api.unarchive(item.id);
      this.restored.set({ id: item.id, name: item.descriptionAr });
      this.restoredTimer = setTimeout(() => {
        this.restored.set(null);
        void this.refresh();
      }, RESTORED_BAND_MS);
    } catch (error) {
      this.rowFailure.set({ id: item.id, key: toProblem(error).messageKey });
    } finally {
      this.archivingId.set(null);
    }
  }

  // ---- create ------------------------------------------------------------------------------------

  protected async onCreateRequested(request: CatalogueItemCreate): Promise<void> {
    this.createRefusal.set(null);
    this.createSaving.set(true);
    try {
      const made = await this.api.create(request);
      // A new item must be where the person can see it: a filter that would hide it is cleared.
      const hidden =
        this.statusFilter() === 'archived' || this.hasNarrowing();
      await this.navigate(
        hidden ? { create: null, search: null, status: null, bab: null } : { create: null },
      );
      await this.refresh();
      this.revealCreated(made);
    } catch (error) {
      // The server is the control: a taken code, a negative price, a missing باب all arrive as a key and
      // render in the panel, next to the field when there is one.
      this.createRefusal.set(toProblem(error).messageKey);
    } finally {
      this.createSaving.set(false);
    }
  }

  protected isJustCreated(item: CatalogueItem): boolean {
    return this.created()?.id === item.id;
  }

  protected createdText(): string {
    const made = this.created();
    return made === null ? '' : this.i18n.t('catalogue.created', { name: made.name, code: made.code });
  }

  /** Opens the باب the new item sits in, shows it past the 'load more' row, and says so in place. */
  private revealCreated(made: CatalogueItem): void {
    const open = new Set(
      this.openGroups() ?? this.groups().filter((g, i) => this.isOpen(g, i)).map((g) => g.babId),
    );
    open.add(made.babId);
    this.openGroups.set(open);
    this.expandedGroups.update((set) => new Set(set).add(made.babId));
    this.created.set({ id: made.id, code: made.code, name: made.descriptionAr });
    if (this.createdTimer !== null) {
      clearTimeout(this.createdTimer);
    }
    this.createdTimer = setTimeout(() => this.created.set(null), CREATED_BAND_MS);
    // After the rows have rendered, bring the new one into view.
    requestAnimationFrame(() =>
      requestAnimationFrame(() =>
        document
          .querySelector<HTMLElement>('[data-testid="catalogue-row-created"]')
          ?.scrollIntoView({ block: 'center' }),
      ),
    );
  }

  // ---- panel -------------------------------------------------------------------------------------

  protected onClosePanel(): Promise<void> {
    this.panelRefusal.set(null);
    this.panelSaved.set(false);
    return this.navigate({ item: null });
  }

  protected async onPanelSave(item: CatalogueItem, edit: CatalogueItemEdit): Promise<void> {
    this.panelRefusal.set(null);
    this.panelSaved.set(false);
    this.panelSaving.set(true);
    try {
      await this.api.edit(item.id, edit);
      await this.refresh();
      this.panelSaved.set(true);
      this.savedTimer = setTimeout(() => this.panelSaved.set(false), SAVED_BANNER_MS);
    } catch (error) {
      // The server is the control (AC-940-F): a changed price from a non-Owner is refused here with a
      // translated message, whatever this screen drew.
      this.panelRefusal.set(toProblem(error).messageKey);
    } finally {
      this.panelSaving.set(false);
    }
  }

  protected async onPanelArchive(item: CatalogueItem): Promise<void> {
    this.panelRefusal.set(null);
    this.panelArchiving.set(true);
    try {
      await this.api.archive(item.id);
      await this.navigate({ item: null });
      await this.refresh();
    } catch (error) {
      this.panelRefusal.set(toProblem(error).messageKey);
    } finally {
      this.panelArchiving.set(false);
    }
  }

  protected async onPanelUnarchive(item: CatalogueItem): Promise<void> {
    this.panelRefusal.set(null);
    this.panelArchiving.set(true);
    try {
      await this.api.unarchive(item.id);
      await this.refresh();
    } catch (error) {
      this.panelRefusal.set(toProblem(error).messageKey);
    } finally {
      this.panelArchiving.set(false);
    }
  }

  // ---- data --------------------------------------------------------------------------------------

  protected trackGroup(_index: number, group: CatalogueGroup): string {
    return group.babId;
  }

  protected trackItem(_index: number, item: CatalogueItem): string {
    return item.id;
  }

  private restoredTimer: ReturnType<typeof setTimeout> | null = null;
  private savedTimer: ReturnType<typeof setTimeout> | null = null;
  private createdTimer: ReturnType<typeof setTimeout> | null = null;

  private clearTimers(): void {
    for (const timer of [this.restoredTimer, this.savedTimer, this.createdTimer]) {
      if (timer !== null) {
        clearTimeout(timer);
      }
    }
  }

  private async navigate(patch: Readonly<Record<string, string | null>>): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: patch,
      queryParamsHandling: 'merge',
    });
  }

  /** The list and the totals together: any mutation can change both. */
  private async refresh(): Promise<void> {
    await Promise.all([this.reload(), this.loadTotals()]);
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      this.items.set(await this.api.list(this.searchTerm(), this.statusFilter(), this.babFilter()));
    } catch (error) {
      const problem = toProblem(error);
      this.failure.set({ key: problem.messageKey, status: problem.status });
      this.items.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  /** "612 بنداً في 8 أبواب" counts everything, whatever the filter is, so it is its own read. */
  private async loadTotals(): Promise<void> {
    try {
      const all = await this.api.list('', 'all');
      this.totals.set({ count: all.length, babCount: new Set(all.map((item) => item.babId)).size });
      this.takenCodes.set(new Set(all.map((item) => item.code.toUpperCase())));
    } catch {
      this.totals.set(null);
    } finally {
      this.totalsReady.set(true);
    }
  }

  private async loadBabs(): Promise<void> {
    try {
      // `GET /api/babs` is its own permission (`BabManage`); both roles that hold `CatalogueManage` hold it
      // today, but a caller with one and not the other still sees every item, with unnamed groups.
      this.babs.set(await this.babsApi.list('all'));
      this.babsLoaded.set(true);
    } catch {
      this.babs.set([]);
    }
  }
}

function readOpenGroups(): ReadonlySet<string> | null {
  try {
    const raw = localStorage.getItem(OPEN_GROUPS_KEY);
    if (raw === null) {
      return null;
    }
    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed) ? new Set(parsed.filter((id): id is string => typeof id === 'string')) : null;
  } catch {
    return null;
  }
}

function writeOpenGroups(open: ReadonlySet<string>): void {
  try {
    localStorage.setItem(OPEN_GROUPS_KEY, JSON.stringify([...open]));
  } catch {
    // Private window or blocked storage: the groups simply open fresh next visit.
  }
}
