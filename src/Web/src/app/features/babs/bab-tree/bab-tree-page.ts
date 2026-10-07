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
import { Bab, BabCreate, BabEdit, BabListFilter, BabsApi } from '../../../core/catalogue/babs.api';
import { flattenBabTree } from '../../../core/catalogue/bab-tree';
import { CatalogueApi } from '../../../core/catalogue/catalogue.api';
import {
  fractionToPercent,
  PERCENT_INPUT_PATTERN,
  percentToFraction,
} from '../../../core/catalogue/percent-wire';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffInlineConfirm } from '../../../shared/kaff-inline-confirm/kaff-inline-confirm';
import { KaffRowMenu, RowMenuItem } from '../../../shared/kaff-row-menu/kaff-row-menu';
import { KaffSearchField } from '../../../shared/kaff-search-field/kaff-search-field';
import {
  KaffSegmentedFilter,
  SegmentedFilterOption,
} from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';
import { KaffValueEditor } from '../../../shared/kaff-value-editor/kaff-value-editor';
import { BabMoveDialog } from '../bab-move-dialog/bab-move-dialog';
import { BabPanel } from '../bab-panel/bab-panel';

/** The three chips `KAFF-213` rule 9 draws, in the order it draws them — matches `CatalogueListPage`. */
const FILTERS: readonly BabListFilter[] = ['active', 'archived', 'all'];

/** How long the "restored" and "saved" bands stay before the row returns to its normal shape. */
const RESTORED_BAND_MS = 2500;
const SAVED_BANNER_MS = 3000;

const PHONE_QUERY = '(max-width: 820px)';

/** One drawn row of the tree (or of a flattened result list). */
interface RowView {
  readonly bab: Bab;
  readonly depth: number;
  /** A search or the archived view flattens the tree: no indent, no disclosure, a parent path instead. */
  readonly flat: boolean;
  readonly path: string | null;
  readonly kidCount: number;
  readonly open: boolean;
  /** 1-based position among its siblings, for the treegrid's `aria-posinset`. */
  readonly pos: number;
  readonly setSize: number;
}

/** The archive of one باب, in whichever of its two shapes: a refusal or the quiet confirmation. */
interface ArchiveFlow {
  readonly id: string;
  readonly kind: 'confirm' | 'refusal';
  /** The باب's OWN active items — never its subtree's (`D-130` §5, `KAFF-213`). */
  readonly count: number | null;
}

/**
 * S-021 · the أبواب tree — `KAFF-204` (create, edit, markup), `KAFF-205` (move), `KAFF-213` (archive),
 * `KAFF-943` (restore). One flat list from `GET /api/babs` (always `status=All`, filtered here: the move
 * and parent pickers need every باب, and "roughly 40 trades" is small), nested client-side by
 * `flattenBabTree`.
 *
 * **A real `treegrid`.** The table is `role="treegrid"`; every row carries `aria-level`, `aria-posinset`,
 * `aria-setsize` and, for a parent, `aria-expanded`, so indentation is never the only carrier of the
 * hierarchy for assistive technology. The kit's table and row parts supply the grid and the cells; the
 * roles go on the elements this page owns.
 *
 * **Archive is two different answers.** The server refuses to archive a باب that holds its OWN active
 * items, naming the count (`D-130` §5). When the count is already known here the answer is shown at once
 * as a refusal, with a way forward and no confirmation step; when the باب holds none, the quiet
 * recoverable confirmation says so and that its child أبواب are untouched. A server refusal on a stale
 * count switches the confirmation to the refusal. Active child أبواب never block archiving, and nothing
 * cascades in either direction, archiving or restoring.
 *
 * **Open and unruled, and not decided here:** what an ACTIVE child under an ARCHIVED parent means (the
 * active view promotes it to the top level, as it always has; the full view nests it under its dimmed
 * parent; "add a child" is not offered on an archived row); whether the markup is Owner-only (every
 * `BabManage` holder gets the editor); whether names must be unique (no rule, no error); and whether the
 * sort order is per parent or global (it is typed on create and not shown).
 */
@Component({
  selector: 'kaff-bab-tree-page',
  imports: [
    RouterLink,
    KaffBadge,
    KaffBanner,
    KaffButton,
    KaffIcon,
    KaffInlineConfirm,
    KaffRowMenu,
    KaffSearchField,
    KaffSegmentedFilter,
    KaffState,
    KaffTable,
    KaffTableRow,
    KaffValueEditor,
    BabMoveDialog,
    BabPanel,
  ],
  templateUrl: './bab-tree-page.html',
  styleUrl: './bab-tree-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BabTreePage {
  private readonly api = inject(BabsApi);
  private readonly catalogueApi = inject(CatalogueApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly i18n = inject(I18nService);

  /** Tracks run in `rowColumns` order (the stylesheet); the English name drops out below 1060px. */
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'bab.column.name', pinned: true },
    { labelKey: 'bab.column.name_en', cssClass: 'cell-en' },
    { labelKey: 'bab.column.items', align: 'end' },
    { labelKey: 'bab.column.default_markup', align: 'end' },
    {},
  ];

  protected readonly skeletonRows = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9] as const;
  protected readonly skeletonIndent = [0, 0, 1, 1, 1, 0, 0, 0, 1, 1] as const;

  protected readonly segmentedOptions: readonly SegmentedFilterOption<BabListFilter>[] = FILTERS.map(
    (filter) => ({ value: filter, labelKey: `bab.filter.${filter}` }),
  );

  /** Bound from `?status=`, `?search=`, `?collapsed=` and `?bab=` by `withComponentInputBinding`. The router
   *  sets an absent param to `undefined`, not the declared default, so each is read through a `computed`. */
  readonly status = input<BabListFilter>('active');
  readonly search = input<string>('');
  /** A comma list of collapsed باب ids; absent means every branch is open. */
  readonly collapsed = input<string>('');
  /** The code of the باب whose details panel is open. */
  readonly bab = input<string>('');

  protected readonly statusFilter = computed(() => this.status() ?? 'active');
  protected readonly searchTerm = computed(() => (this.search() ?? '').trim());
  protected readonly collapsedIds = computed(
    () => new Set((this.collapsed() ?? '').split(',').filter((id) => id.length > 0)),
  );
  private readonly openCode = computed(() => (this.bab() ?? '').toUpperCase());

  /** Every باب, archived or not. */
  protected readonly babs = signal<readonly Bab[]>([]);
  /** Each باب's own ACTIVE items, or `null` when the catalogue could not be read. */
  protected readonly ownItems = signal<ReadonlyMap<string, number> | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<{ readonly key: string; readonly status: number } | null>(null);

  protected readonly isPhone = signal(typeof matchMedia === 'function' && matchMedia(PHONE_QUERY).matches);

  // ---- row-level state; only one editor, confirmation or refusal is open at a time ----------------
  protected readonly editingMarkupId = signal<string | null>(null);
  protected readonly markupDraft = signal('');
  protected readonly markupBusy = signal(false);
  protected readonly markupRefusal = signal<string | null>(null);
  protected readonly savedMarkup = signal<{
    readonly id: string;
    readonly name: string;
    readonly from: string;
    readonly to: string;
  } | null>(null);

  protected readonly archiveFlow = signal<ArchiveFlow | null>(null);
  protected readonly archivingId = signal<string | null>(null);
  protected readonly restored = signal<{ readonly id: string; readonly name: string } | null>(null);
  protected readonly rowFailure = signal<{ readonly id: string; readonly key: string } | null>(null);

  protected readonly movingBab = signal<Bab | null>(null);
  protected readonly moveBusy = signal(false);
  protected readonly moveRefusal = signal<string | null>(null);

  // ---- panel state ---------------------------------------------------------------------------------
  /** `null` when closed; `parentId` is the preset parent of a "new child". */
  protected readonly creating = signal<{ readonly parentId: string | null } | null>(null);
  protected readonly panelBusy = signal(false);
  protected readonly panelSaved = signal(false);
  protected readonly panelRefusal = signal<string | null>(null);
  protected readonly created = signal<{ readonly id: string; readonly name: string; readonly code: string } | null>(null);

  private readonly byId = computed(() => new Map(this.babs().map((b) => [b.id, b])));

  private readonly names = computed(() => {
    const english = this.i18n.locale() === 'en';
    return new Map(this.babs().map((b) => [b.id, english ? b.nameEn : b.nameAr]));
  });

  /** The باب's own active items plus every descendant's: shown beside the own count, for information only. */
  private readonly subtreeItems = computed(() => {
    const own = this.ownItems();
    const result = new Map<string, number>();
    if (own === null) {
      return result;
    }
    const childrenOf = new Map<string, Bab[]>();
    for (const b of this.babs()) {
      if (b.parentBabId !== null) {
        const siblings = childrenOf.get(b.parentBabId) ?? [];
        siblings.push(b);
        childrenOf.set(b.parentBabId, siblings);
      }
    }
    const total = (id: string, seen: Set<string>): number => {
      if (seen.has(id)) {
        return 0;
      }
      seen.add(id);
      return (own.get(id) ?? 0) + (childrenOf.get(id) ?? []).reduce((sum, c) => sum + total(c.id, seen), 0);
    };
    for (const b of this.babs()) {
      result.set(b.id, total(b.id, new Set()));
    }
    return result;
  });

  /** The أبواب the current status filter keeps, before search. */
  private readonly viewBabs = computed(() => {
    const filter = this.statusFilter();
    return filter === 'all' ? this.babs() : this.babs().filter((b) => b.isActive === (filter === 'active'));
  });

  /** Search and the archived view flatten the tree into a result list (README "The tree"). */
  protected readonly isFlat = computed(() => this.searchTerm().length > 0 || this.statusFilter() === 'archived');

  private readonly childIdsInView = computed(() => {
    const ids = new Set<string>();
    for (const b of this.viewBabs()) {
      if (b.parentBabId !== null) {
        ids.add(b.parentBabId);
      }
    }
    return ids;
  });

  protected readonly rows = computed<readonly RowView[]>(() => {
    const view = this.viewBabs();
    const kidCounts = new Map<string, number>();
    for (const b of view) {
      if (b.parentBabId !== null) {
        kidCounts.set(b.parentBabId, (kidCounts.get(b.parentBabId) ?? 0) + 1);
      }
    }

    if (this.isFlat()) {
      const term = this.searchTerm().toLowerCase();
      const matches = view.filter(
        (b) =>
          term.length === 0 ||
          `${b.code} ${b.nameAr} ${b.nameEn}`.toLowerCase().includes(term),
      );
      return matches.map((b, index) => ({
        bab: b,
        depth: 0,
        flat: true,
        path: this.pathOf(b),
        kidCount: 0,
        open: false,
        pos: index + 1,
        setSize: matches.length,
      }));
    }

    const collapsed = this.collapsedIds();
    const tree = flattenBabTree(view);
    const siblingCounts = new Map<string | null, number>();
    for (const { bab } of tree) {
      siblingCounts.set(bab.parentBabId, (siblingCounts.get(bab.parentBabId) ?? 0) + 1);
    }
    const seen = new Map<string | null, number>();
    const result: RowView[] = [];
    let hideBelow: number | null = null;

    for (const { bab, depth } of tree) {
      const parentKey = depth === 0 && !view.some((b) => b.id === bab.parentBabId) ? null : bab.parentBabId;
      const pos = (seen.get(parentKey) ?? 0) + 1;
      seen.set(parentKey, pos);
      if (hideBelow !== null && depth > hideBelow) {
        continue;
      }
      hideBelow = null;
      result.push({
        bab,
        depth,
        flat: false,
        path: null,
        kidCount: kidCounts.get(bab.id) ?? 0,
        open: !collapsed.has(bab.id),
        pos,
        setSize: siblingCounts.get(parentKey) ?? 1,
      });
      if (collapsed.has(bab.id)) {
        hideBelow = depth;
      }
    }
    return result;
  });

  protected readonly hasAnyBab = computed(() => this.babs().length > 0);
  /** Nothing exists at all: the first run, which teaches (README "First run"). */
  protected readonly isFirstRun = computed(() => !this.loading() && this.failure() === null && !this.hasAnyBab());
  protected readonly hasParents = computed(() => this.childIdsInView().size > 0);

  private readonly parentIdsInView = computed(() => [...this.childIdsInView()]);

  protected readonly metaText = computed(() => {
    if (this.loading() && !this.hasAnyBab()) {
      return null;
    }
    const count = this.babs().length;
    return count === 0 ? this.i18n.t('bab.tree.count_none') : this.i18n.t('bab.tree.count', { count });
  });

  private readonly scopeText = computed(() => this.i18n.t(`bab.scope.${this.statusFilter()}`));

  /** What is shown, not what exists (README "Result count line"). */
  protected readonly countText = computed(() => {
    const count = this.rows().length;
    const query = this.searchTerm();
    if (query.length > 0) {
      return this.i18n.t('bab.count.search', { count, query });
    }
    return this.i18n.t(`bab.count.${this.statusFilter()}`, { count });
  });

  protected readonly emptyFilteredBody = computed(() =>
    this.i18n.t('bab.empty.no_match_body', { what: this.searchTerm(), scope: this.scopeText() }),
  );

  protected readonly archivedEmpty = computed(
    () => this.statusFilter() === 'archived' && this.searchTerm().length === 0,
  );

  protected readonly panelBab = computed<Bab | null>(() => {
    const code = this.openCode();
    return code.length === 0 ? null : (this.babs().find((b) => b.code.toUpperCase() === code) ?? null);
  });

  protected readonly panelOwnItems = computed(() => {
    const bab = this.panelBab();
    return bab === null ? null : (this.ownItems()?.get(bab.id) ?? (this.ownItems() === null ? null : 0));
  });

  protected readonly panelChildCount = computed(() => {
    const bab = this.panelBab();
    return bab === null ? 0 : this.babs().filter((b) => b.parentBabId === bab.id).length;
  });

  constructor() {
    void this.reload();

    if (typeof matchMedia === 'function') {
      const query = matchMedia(PHONE_QUERY);
      const onChange = (event: MediaQueryListEvent): void => this.isPhone.set(event.matches);
      query.addEventListener('change', onChange);
      this.destroyRef.onDestroy(() => query.removeEventListener('change', onChange));
    }

    // A deep link or a refresh onto `?bab=` whose باب does not exist closes the panel rather than showing nothing.
    effect(() => {
      if (!this.loading() && this.openCode().length > 0 && this.panelBab() === null) {
        void this.navigate({ bab: null });
      }
    });

    this.destroyRef.onDestroy(() => this.clearTimers());
  }

  // ---- toolbar ------------------------------------------------------------------------------------

  protected onSearch(term: string): Promise<void> {
    this.dismissBands();
    return this.navigate({ search: term.length > 0 ? term : null });
  }

  protected onFilter(filter: BabListFilter): Promise<void> {
    this.dismissBands();
    return this.navigate({ status: filter === 'active' ? null : filter });
  }

  protected onClearSearch(): Promise<void> {
    return this.navigate({ search: null });
  }

  protected onExpandAll(): Promise<void> {
    return this.navigate({ collapsed: null });
  }

  protected onCollapseAll(): Promise<void> {
    return this.navigate({ collapsed: this.parentIdsInView().join(',') || null });
  }

  protected onCreate(parentId: string | null = null): void {
    this.dismissBands();
    this.panelRefusal.set(null);
    this.panelSaved.set(false);
    this.creating.set({ parentId });
    void this.navigate({ bab: null });
  }

  // ---- rows ---------------------------------------------------------------------------------------

  protected babLabel(bab: Bab): string {
    return this.names().get(bab.id) ?? bab.nameAr;
  }

  protected markupPercent(bab: Bab): string {
    return `${this.i18n.formatNumber(fractionToPercent(bab.defaultMarkup), { maximumFractionDigits: 4 })}%`;
  }

  protected itemCountOf(bab: Bab): number | null {
    return this.ownItems()?.get(bab.id) ?? (this.ownItems() === null ? null : 0);
  }

  protected subtreeCountOf(bab: Bab): number | null {
    return this.ownItems() === null ? null : (this.subtreeItems().get(bab.id) ?? 0);
  }

  protected figure(count: number): string {
    return this.i18n.formatNumber(count);
  }

  protected guides(depth: number): readonly number[] {
    return Array.from({ length: depth }, (_, index) => index);
  }

  protected phoneSub(row: RowView): string {
    const parts: string[] = [];
    const items = this.itemCountOf(row.bab);
    if (items !== null) {
      parts.push(this.i18n.t('bab.count.items', { count: items }));
    }
    if (row.kidCount > 0 && !row.flat) {
      parts.push(this.childrenText(row.kidCount));
    }
    return parts.join(' · ');
  }

  /** Arabic counts one and two as words ("فرع واحد", "فرعان"), and three and up as a number. */
  protected childrenText(count: number): string {
    return count === 1
      ? this.i18n.t('bab.count.child_one')
      : count === 2
        ? this.i18n.t('bab.count.child_two')
        : this.i18n.t('bab.count.children', { count });
  }

  protected toggleLabel(row: RowView): string {
    return this.i18n.t(row.open ? 'bab.tree.collapse_label' : 'bab.tree.expand_label', {
      name: this.babLabel(row.bab),
    });
  }

  protected pathText(row: RowView): string | null {
    return row.path === null ? null : this.i18n.t('bab.row.within', { path: row.path });
  }

  protected markupEditLabel(bab: Bab): string {
    return this.i18n.t('bab.row.edit_markup', { name: this.babLabel(bab) });
  }

  protected isSavedBand(bab: Bab): boolean {
    return this.savedMarkup()?.id === bab.id;
  }

  protected isCreatedBand(bab: Bab): boolean {
    return this.created()?.id === bab.id;
  }

  protected isRestored(bab: Bab): boolean {
    return this.restored()?.id === bab.id;
  }

  protected rowFailureKey(bab: Bab): string | null {
    const failed = this.rowFailure();
    return failed?.id === bab.id ? failed.key : null;
  }

  protected rowMenuItems(bab: Bab): readonly RowMenuItem[] {
    const items: RowMenuItem[] = [
      { labelKey: 'bab.menu.details', action: 'details', icon: 'eye' },
      { labelKey: 'bab.menu.edit_markup', action: 'markup', icon: 'pen' },
    ];
    if (bab.isActive) {
      // A child under an ARCHIVED parent is the open question; it is not offered from an archived row.
      items.push({ labelKey: 'bab.menu.add_child', action: 'child', icon: 'child' });
    }
    items.push({ labelKey: 'bab.menu.move', action: 'move', icon: 'move' });
    items.push(
      bab.isActive
        ? { labelKey: 'bab.menu.archive', action: 'archive', icon: 'arch', separatorBefore: true }
        : {
            labelKey: 'bab.menu.unarchive',
            action: 'unarchive',
            icon: 'unarch',
            separatorBefore: true,
            disabled: this.archivingId() === bab.id,
          },
    );
    return items;
  }

  protected onRowMenuSelected(bab: Bab, action: string): void {
    switch (action) {
      case 'details':
        void this.onOpenPanel(bab);
        break;
      case 'markup':
        this.onStartMarkup(bab);
        break;
      case 'child':
        this.onCreate(bab.id);
        break;
      case 'move':
        this.onStartMove(bab);
        break;
      case 'archive':
        this.onStartArchive(bab);
        break;
      case 'unarchive':
        void this.onUnarchive(bab);
        break;
    }
  }

  /** A click anywhere on a row opens the panel, except on the row's own controls. */
  protected onRowClick(bab: Bab, event: Event): void {
    const target = event.target as HTMLElement | null;
    if (target?.closest('button, a, kaff-row-menu')) {
      return;
    }
    void this.onOpenPanel(bab);
  }

  protected onOpenPanel(bab: Bab): Promise<void> {
    this.creating.set(null);
    this.panelRefusal.set(null);
    this.panelSaved.set(false);
    return this.navigate({ bab: bab.code });
  }

  protected async onToggle(row: RowView): Promise<void> {
    const next = new Set(this.collapsedIds());
    if (row.open) {
      next.add(row.bab.id);
    } else {
      next.delete(row.bab.id);
    }
    await this.navigate({ collapsed: next.size > 0 ? [...next].join(',') : null });
  }

  // ---- the markup, a deliberate edit --------------------------------------------------------------

  protected readonly editingBab = computed(() => {
    const id = this.editingMarkupId();
    return id === null ? null : (this.byId().get(id) ?? null);
  });

  private canonical(percent: string): string | null {
    const text = percent.trim();
    return PERCENT_INPUT_PATTERN.test(text) ? fractionToPercent(percentToFraction(text)) : null;
  }

  protected readonly markupDirty = computed(() => {
    const bab = this.editingBab();
    const typed = this.canonical(this.markupDraft());
    return bab !== null && typed !== null && typed !== fractionToPercent(bab.defaultMarkup);
  });

  protected readonly markupError = computed<string | null>(() => {
    const text = this.markupDraft().trim();
    if (text.length === 0) {
      return this.i18n.t('bab.error.markup_required');
    }
    return PERCENT_INPUT_PATTERN.test(text) ? null : this.i18n.t('bab.error.markup_number');
  });

  protected readonly markupAfter = computed(() => {
    const typed = this.canonical(this.markupDraft());
    return typed === null ? '' : `${this.i18n.formatNumber(typed, { maximumFractionDigits: 4 })}%`;
  });

  protected onStartMarkup(bab: Bab): void {
    this.dismissBands();
    this.archiveFlow.set(null);
    this.creating.set(null);
    this.markupRefusal.set(null);
    this.markupDraft.set(fractionToPercent(bab.defaultMarkup));
    this.editingMarkupId.set(bab.id);
    if (this.openCode().length > 0) {
      void this.navigate({ bab: null });
    }
  }

  protected onCancelMarkup(): void {
    this.editingMarkupId.set(null);
    this.markupRefusal.set(null);
  }

  protected async onSaveMarkup(): Promise<void> {
    const bab = this.editingBab();
    if (bab === null || !this.markupDirty() || this.markupError() !== null) {
      return;
    }
    const from = this.markupPercent(bab);
    this.markupBusy.set(true);
    this.markupRefusal.set(null);
    try {
      // `EditBab` has no partial-update shape, so the names go back over the wire unchanged.
      const saved = await this.api.edit(bab.id, {
        nameAr: bab.nameAr,
        nameEn: bab.nameEn,
        defaultMarkup: percentToFraction(this.markupDraft()),
      });
      this.editingMarkupId.set(null);
      this.savedMarkup.set({
        id: bab.id,
        name: this.babLabel(bab),
        from,
        to: this.markupPercent(saved),
      });
      await this.reload(false);
    } catch (error) {
      this.markupRefusal.set(this.i18n.t(toProblem(error).messageKey));
    } finally {
      this.markupBusy.set(false);
    }
  }

  protected savedMarkupText(): string {
    const saved = this.savedMarkup();
    return saved === null
      ? ''
      : this.i18n.t('bab.markup.saved', { name: saved.name, from: saved.from, to: saved.to });
  }

  protected markupTitle(bab: Bab): string {
    return this.i18n.t('bab.markup.title', { name: this.babLabel(bab) });
  }

  protected markupCurrent(bab: Bab): string {
    return this.i18n.t('bab.markup.current', { value: this.markupPercent(bab) });
  }

  // ---- archive and restore ------------------------------------------------------------------------

  protected onStartArchive(bab: Bab): void {
    this.dismissBands();
    this.editingMarkupId.set(null);
    this.rowFailure.set(null);
    this.creating.set(null);
    if (this.openCode().length > 0) {
      void this.navigate({ bab: null });
    }
    const own = this.itemCountOf(bab);
    // A باب that holds its own active items is refused at once: the answer is already known, so no
    // confirmation is asked for. An unknown count falls through to the confirmation and the server decides.
    this.archiveFlow.set(
      own !== null && own > 0
        ? { id: bab.id, kind: 'refusal', count: own }
        : { id: bab.id, kind: 'confirm', count: own },
    );
  }

  protected onCloseArchive(): void {
    this.archiveFlow.set(null);
  }

  protected async onConfirmArchive(bab: Bab): Promise<void> {
    this.rowFailure.set(null);
    this.archivingId.set(bab.id);
    try {
      await this.api.archive(bab.id);
      this.archiveFlow.set(null);
      await this.reload(false);
    } catch (error) {
      const problem = toProblem(error);
      const count = problem.extensions['count'];
      if (problem.messageKey === 'errors.master.bab_has_active_items' && typeof count === 'number') {
        // The count on screen was stale or unknown: the server's answer is the refusal.
        this.archiveFlow.set({ id: bab.id, kind: 'refusal', count });
        await this.loadOwnItems();
      } else {
        this.archiveFlow.set(null);
        this.rowFailure.set({ id: bab.id, key: problem.messageKey });
      }
    } finally {
      this.archivingId.set(null);
    }
  }

  protected archiveFlowFor(bab: Bab): ArchiveFlow | null {
    const flow = this.archiveFlow();
    return flow?.id === bab.id ? flow : null;
  }

  protected archiveQuestion(bab: Bab): string {
    return this.i18n.t('bab.archive.question', { name: this.babLabel(bab) });
  }

  protected archiveDetail(row: RowView): string {
    const kids = this.babs().filter((b) => b.parentBabId === row.bab.id && b.isActive).length;
    return this.i18n.t(kids > 0 ? 'bab.archive.detail_children' : 'bab.archive.detail', { children: kids });
  }

  protected refusalTitle(bab: Bab): string {
    return this.i18n.t('bab.archive.refused_title', { name: this.babLabel(bab) });
  }

  protected refusalBody(count: number): string {
    return this.i18n.t('bab.archive.refused_body', { count });
  }

  protected catalogueQuery(bab: Bab): Record<string, string> {
    return { bab: bab.id };
  }

  /** The refusal's way forward: the catalogue, filtered to this باب's items. */
  protected onViewItems(bab: Bab): Promise<boolean> {
    return this.router.navigate(['/catalogue'], { queryParams: this.catalogueQuery(bab) });
  }

  /** Restores in place: no confirmation, no toast. The row says so for a moment, then returns. */
  protected async onUnarchive(bab: Bab): Promise<void> {
    this.dismissBands();
    this.rowFailure.set(null);
    this.archivingId.set(bab.id);
    try {
      await this.api.unarchive(bab.id);
      this.restored.set({ id: bab.id, name: this.babLabel(bab) });
      this.restoredTimer = setTimeout(() => {
        this.restored.set(null);
        void this.reload(false);
      }, RESTORED_BAND_MS);
    } catch (error) {
      const problem = toProblem(error);
      this.rowFailure.set({ id: bab.id, key: problem.messageKey });
      if (problem.messageKey === 'errors.master.not_archived') {
        await this.reload(false);
      }
    } finally {
      this.archivingId.set(null);
    }
  }

  protected restoredText(): string {
    return this.i18n.t('bab.restored', { name: this.restored()?.name ?? '' });
  }

  // ---- move ---------------------------------------------------------------------------------------

  protected onStartMove(bab: Bab): void {
    this.dismissBands();
    this.moveRefusal.set(null);
    this.movingBab.set(bab);
  }

  protected onCancelMove(): void {
    this.movingBab.set(null);
  }

  protected async onConfirmMove(parentId: string | null): Promise<void> {
    const bab = this.movingBab();
    if (bab === null) {
      return;
    }
    this.moveRefusal.set(null);
    this.moveBusy.set(true);
    try {
      await this.api.move(bab.id, parentId);
      this.movingBab.set(null);
      // The new parent opens to show the result.
      const next = new Set(this.collapsedIds());
      const wasCollapsed = parentId !== null && next.delete(parentId);
      if (wasCollapsed) {
        await this.navigate({ collapsed: next.size > 0 ? [...next].join(',') : null });
      }
      await this.reload(false);
    } catch (error) {
      // AC-205-C: surfaced, not swallowed — including the cycle refusal at any depth.
      this.moveRefusal.set(toProblem(error).messageKey);
    } finally {
      this.moveBusy.set(false);
    }
  }

  // ---- the panel: create and edit -----------------------------------------------------------------

  protected onClosePanel(): void {
    this.panelRefusal.set(null);
    this.panelSaved.set(false);
    if (this.creating() !== null) {
      this.creating.set(null);
    } else {
      void this.navigate({ bab: null });
    }
  }

  protected async onPanelCreate(request: BabCreate): Promise<void> {
    this.panelRefusal.set(null);
    this.panelBusy.set(true);
    try {
      const created = await this.api.create(request);
      this.creating.set(null);
      // The new row appears in place, highlighted, with its parent opened and scrolled into view.
      const next = new Set(this.collapsedIds());
      const wasCollapsed = created.parentBabId !== null && next.delete(created.parentBabId);
      const patch: Record<string, string | null> = { bab: null };
      if (this.statusFilter() === 'archived' || this.searchTerm().length > 0) {
        patch['status'] = null;
        patch['search'] = null;
      }
      if (wasCollapsed) {
        patch['collapsed'] = next.size > 0 ? [...next].join(',') : null;
      }
      await this.navigate(patch);
      this.created.set({ id: created.id, name: this.babLabel(created), code: created.code });
      await this.reload(false);
      this.scrollTimer = setTimeout(() => {
        document.querySelector(`[data-testid="bab-row-${created.code}"]`)?.scrollIntoView({ block: 'center' });
      }, 50);
    } catch (error) {
      this.panelRefusal.set(toProblem(error).messageKey);
    } finally {
      this.panelBusy.set(false);
    }
  }

  protected async onPanelSave(edit: BabEdit): Promise<void> {
    const bab = this.panelBab();
    if (bab === null) {
      return;
    }
    this.panelRefusal.set(null);
    this.panelSaved.set(false);
    this.panelBusy.set(true);
    try {
      await this.api.edit(bab.id, edit);
      await this.reload(false);
      this.panelSaved.set(true);
      this.savedTimer = setTimeout(() => this.panelSaved.set(false), SAVED_BANNER_MS);
    } catch (error) {
      this.panelRefusal.set(toProblem(error).messageKey);
    } finally {
      this.panelBusy.set(false);
    }
  }

  protected onPanelMove(): void {
    const bab = this.panelBab();
    if (bab !== null) {
      this.onStartMove(bab);
    }
  }

  protected onPanelMarkup(): void {
    const bab = this.panelBab();
    if (bab !== null) {
      this.onStartMarkup(bab);
    }
  }

  protected onPanelArchive(): void {
    const bab = this.panelBab();
    if (bab !== null) {
      this.onStartArchive(bab);
    }
  }

  protected onPanelUnarchive(): void {
    const bab = this.panelBab();
    if (bab !== null) {
      void this.onUnarchive(bab);
      void this.navigate({ bab: null });
    }
  }

  // ---- data ---------------------------------------------------------------------------------------

  protected trackRow(_index: number, row: RowView): string {
    return row.bab.id;
  }

  private restoredTimer: ReturnType<typeof setTimeout> | null = null;
  private savedTimer: ReturnType<typeof setTimeout> | null = null;
  private scrollTimer: ReturnType<typeof setTimeout> | null = null;

  private clearTimers(): void {
    for (const timer of [this.restoredTimer, this.savedTimer, this.scrollTimer]) {
      if (timer !== null) {
        clearTimeout(timer);
      }
    }
  }

  /** The saved, created and restored bands last until the next thing the person does. */
  private dismissBands(): void {
    this.savedMarkup.set(null);
    this.created.set(null);
  }

  private pathOf(bab: Bab): string | null {
    if (bab.parentBabId === null) {
      return null;
    }
    const path: string[] = [];
    let cursor: Bab | undefined = this.byId().get(bab.parentBabId);
    for (let guard = 0; cursor !== undefined && guard < 32; guard += 1) {
      path.unshift(this.names().get(cursor.id) ?? cursor.nameAr);
      cursor = cursor.parentBabId === null ? undefined : this.byId().get(cursor.parentBabId);
    }
    return path.length > 0 ? path.join(' / ') : null;
  }

  private async navigate(patch: Readonly<Record<string, string | null>>): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: patch,
      queryParamsHandling: 'merge',
    });
  }

  private async reload(showLoading = true): Promise<void> {
    if (showLoading) {
      this.loading.set(true);
    }
    this.failure.set(null);
    try {
      this.babs.set(await this.api.list('all'));
    } catch (error) {
      const problem = toProblem(error);
      this.failure.set({ key: problem.messageKey, status: problem.status });
      this.babs.set([]);
    } finally {
      this.loading.set(false);
    }
    await this.loadOwnItems();
  }

  /**
   * Each باب's own active items, from one catalogue read. A reader without `CatalogueManage` gets `null`
   * and the count column shows a dash; archiving then goes to the confirmation and the server decides.
   */
  private async loadOwnItems(): Promise<void> {
    try {
      const items = await this.catalogueApi.list('', 'active');
      const counts = new Map<string, number>();
      for (const item of items) {
        counts.set(item.babId, (counts.get(item.babId) ?? 0) + 1);
      }
      this.ownItems.set(counts);
    } catch {
      this.ownItems.set(null);
    }
  }
}
