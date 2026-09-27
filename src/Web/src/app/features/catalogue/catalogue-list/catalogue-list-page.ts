import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  TemplateRef,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { FormField, form } from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { Bab, BabsApi } from '../../../core/catalogue/babs.api';
import {
  CatalogueApi,
  CatalogueItem,
  CatalogueItemListFilter,
} from '../../../core/catalogue/catalogue.api';
import { CatalogueGroup, groupByBab } from '../../../core/catalogue/group-by-bab';
import { toWireDecimal } from '../../../core/catalogue/money-wire';
import { fractionToPercent } from '../../../core/catalogue/percent-wire';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffGroupHeading } from '../../../shared/kaff-group-heading/kaff-group-heading';
import { KaffInlineEdit } from '../../../shared/kaff-inline-edit/kaff-inline-edit';
import { KaffRowMenu, RowMenuItem } from '../../../shared/kaff-row-menu/kaff-row-menu';
import {
  KaffSegmentedFilter,
  SegmentedFilterOption,
} from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

/** The three chips `KAFF-206` rule 7 draws, in the order it draws them. */
const FILTERS: readonly CatalogueItemListFilter[] = ['active', 'archived', 'all'];

/**
 * S-017 · the catalogue list, grouped by باب. `KAFF-203` (search) + `KAFF-206` (archive/un-archive).
 *
 * **Filter and search live in the URL, not in a signal.** `F-127-1` is an open finding precisely
 * because the client list (`client-list-page.ts`) put them in signals only — bookmarking, a refresh
 * and a shared link all lost the filter. `search` and `status` here are `input()`s bound by
 * `withComponentInputBinding` (`app.config.ts`) straight from `?search=` and `?status=`; every filter
 * chip and the search box write to the URL and read the result back rather than holding their own
 * state, so the URL is the single source of truth this screen has.
 *
 * **Grouped by باب, then ordered by code — `AC-203-I`.** The server already returns the list in that
 * order (`ListCatalogueItems.Handler`); `groupByBab` only buckets the already-ordered rows, it does not
 * re-sort them. An item whose `babId` matches no باب still renders, in its own group with no name,
 * rather than vanishing — the brief calls this out explicitly and `group-by-bab.spec.ts` pins it.
 *
 * **Archive and un-archive happen from the row**, per the brief — unlike the client screen, there is no
 * separate form-page danger zone for this feature. `AC-206-E`: archiving an already-archived item is
 * refused, and the refusal renders next to the row that produced it rather than being swallowed.
 *
 * **KAFF-931**: rebuilt on the KAFF-930 interaction kit. The row's two `variant="secondary"`
 * archive/unarchive buttons collapse into one `kaff-row-menu` per row; the danger-toned item is text,
 * never a filled red button (the one filled `variant="danger"` button left is the existing inline
 * archive-confirmation's own confirm, kept byte-identical per the story's own instruction). The row no
 * longer navigates straight to the full edit page — it opens a `kaff-detail-panel` instead (code,
 * description, unit, cost price via `kaff-inline-edit`, and the derived-sell-price note), with the open
 * item in `?item=<code>` so it is linkable and survives a refresh. A link inside the panel still reaches
 * the full edit page (`/catalogue/:id`) for what the panel does not cover. Loading, empty(-filtered) and
 * refusal render through `kaff-state`.
 *
 * **No recent-changes list in the panel.** The brief names one; there is no endpoint this screen can call
 * for it — `AuditApi.list` is `Permission.AuditRead`, Owner-only (`audit.api.ts`), while this screen is
 * `CatalogueManage` (Technical Office and Owner), so wiring the audit call here would 403 for the exact
 * role the brief is building this panel for. Building one client-side by filtering the whole audit trail
 * by `entityId` would also be a second source of truth for a permission this screen does not hold. Left
 * out and flagged rather than faked — see this story's build notes.
 */
@Component({
  selector: 'kaff-catalogue-list-page',
  imports: [
    FormField,
    RouterLink,
    KaffBadge,
    KaffButton,
    KaffDetailPanel,
    KaffGroupHeading,
    KaffInlineEdit,
    KaffRowMenu,
    KaffSegmentedFilter,
    KaffState,
    KaffTable,
    KaffTableRow,
  ],
  templateUrl: './catalogue-list-page.html',
  styleUrl: './catalogue-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogueListPage {
  private readonly api = inject(CatalogueApi);
  private readonly babsApi = inject(BabsApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly i18n = inject(I18nService);
  protected readonly filters = FILTERS;

  /** Grid columns for `kaff-table`: identity (description, with the code beneath it) · unit · cost ·
   *  sell · trailing action/badge.
   *
   *  `ux/components.md:259` rules the name first, so the code no longer owns a track of its own — it
   *  renders as secondary text inside the identity cell. The identity column is the single flexing
   *  track, `minmax(0, 1fr)`; every other track is fixed. **No `auto` track**: `auto` sizes each row
   *  against its own content, which is the defect this shell exists to fix. Unit is one or two Arabic
   *  characters (`م٣`/`طن`), cost/sell are formatted money, and the trailing column is the row's own
   *  quiet action plus the archived badge. */
  protected readonly rowColumns = 'minmax(0, 1fr) 4rem 8rem 8rem 9rem';

  /** `KAFF-925`: same column order the rows use, the shared header wires above the first group. */
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'catalogue.column.description' },
    { labelKey: 'catalogue.column.unit' },
    { labelKey: 'catalogue.column.cost_price', align: 'end' },
    { labelKey: 'catalogue.column.base_sell_rate', align: 'end' },
    {},
  ];

  protected readonly segmentedOptions: readonly SegmentedFilterOption<CatalogueItemListFilter>[] =
    FILTERS.map((filter) => ({ value: filter, labelKey: this.filterKey(filter) }));

  /**
   * Bound by name from `?search=` / `?status=` — `withComponentInputBinding` in `app.config.ts`.
   *
   * **Read through `searchTerm()` / `statusFilter()` below, never directly.** The router sets these
   * inputs to `undefined`, not to the `input()` default, on a navigation where the query param is
   * simply absent (`/catalogue` with no `?search=` at all) — an Angular behaviour distinct from "never
   * bound". `CatalogueApi.list` then threw `Cannot read properties of undefined (reading 'trim')` on
   * the very first load, which stuck the screen on the generic failure banner for every screenshot
   * taken afterward. Found by actually rendering the screen and reading the console, not by reasoning
   * about the types — the declared `input<string>('')` looked like enough of a guarantee that it
   * wasn't.
   */
  readonly search = input<string>('');
  readonly status = input<CatalogueItemListFilter>('active');
  /** Bound from `?item=` — the open panel's item code, matching the `?collapsed=`/`?status=` pattern
   *  `bab-tree-page.ts` already uses for its own URL-held state. */
  readonly item = input<string>('');

  protected readonly searchTerm = computed(() => this.search() ?? '');
  protected readonly statusFilter = computed(() => this.status() ?? 'active');
  protected readonly openItemCode = computed(() => this.item() ?? '');

  private readonly searchModel = signal<{ query: string }>({ query: '' });
  protected readonly searchForm = form(this.searchModel);

  protected readonly items = signal<readonly CatalogueItem[]>([]);
  protected readonly babs = signal<readonly Bab[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  private readonly archivingId = signal<string | null>(null);
  protected readonly confirmingArchiveId = signal<string | null>(null);
  protected readonly rowFailure = signal<{ readonly id: string; readonly key: string } | null>(null);

  private readonly savingCostId = signal<string | null>(null);
  protected readonly costFailure = signal<{ readonly id: string; readonly key: string } | null>(null);

  protected readonly groups = computed<readonly CatalogueGroup[]>(() =>
    groupByBab(this.items(), this.babs()),
  );

  /** The panel's open item, resolved from `?item=<code>` against the loaded page — `null` when the URL
   *  names no code or the code matches nothing in the current filter's view (a stale link, an archived
   *  item under `?status=active`). */
  protected readonly openItem = computed<CatalogueItem | null>(() => {
    const code = this.openItemCode();
    if (code.length === 0) {
      return null;
    }
    return this.items().find((item) => item.code === code) ?? null;
  });

  /** The باب the open item belongs to — `null` while `babs` is still loading or the item names a باب
   *  `loadBabs` could not resolve, same "no name rather than a made-up figure" rule `babLabel` uses. */
  private readonly openItemBab = computed<Bab | null>(() => {
    const item = this.openItem();
    if (item === null) {
      return null;
    }
    return this.babs().find((bab) => bab.id === item.babId) ?? null;
  });

  /** `catalogue.detail.sell_rate_note` — the line explaining the sell price comes from the باب's
   *  markup, not a value this panel can set directly. `null` when the باب is unresolved. */
  protected readonly sellRateNote = computed<string | null>(() => {
    const bab = this.openItemBab();
    if (bab === null) {
      return null;
    }
    const percent = this.i18n.formatNumber(fractionToPercent(bab.defaultMarkup), {
      maximumFractionDigits: 4,
    });
    return this.i18n.t('catalogue.detail.sell_rate_note', { percent });
  });

  /**
   * KAFF-929 finding 4: "the count is real information an operator uses and the app currently shows
   * nowhere." `ListCatalogueItems` carries no separate total — this reads the response already held
   * in {@link items}/{@link groups} rather than adding a call for one. It is the count of the current
   * filter's view (whatever `statusFilter` selects), not a whole-catalogue total.
   */
  protected readonly countText = computed(() =>
    this.i18n.t('catalogue.list.count', {
      count: this.items().length,
      babCount: this.groups().length,
    }),
  );

  /**
   * True when the current view could be hiding rows that exist — `AC-203-D`, repaired per `V-36-A`.
   * The true "nothing exists yet" state only applies to the one query that cannot be hiding anything:
   * `status=all` with no search term. **`active`, the default, is itself a filter** — `KAFF-206` rule
   * 7 says so, and archiving every item must not make the screen claim the catalogue is empty (which
   * sent an operator to retype a code the server then refused as taken, against a row they could not
   * see). So `active` and `archived` both count as filtered here, same as a search term does.
   */
  protected readonly isFiltered = computed(
    () => this.searchTerm().trim().length > 0 || this.statusFilter() !== 'all',
  );

  /** KAFF-929 finding 2, matching KAFF-926's `UserListPage`: projects the primary action into the
   *  shell's top-bar slot instead of the bottom `.create` link the finding reopens. */
  private readonly createAction = viewChild<TemplateRef<unknown>>('createAction');
  private readonly headerActions = inject(HeaderActionsService);

  constructor() {
    void this.loadBabs();

    effect(() => this.headerActions.set(this.createAction() ?? null));
    inject(DestroyRef).onDestroy(() => this.headerActions.set(null));

    // The draft box starts from whatever the URL already carries (a shared link, a bookmark) —
    // read once, here, rather than kept in sync by a reactive `effect()`. An `effect()` that calls
    // `searchModel.set(...)` reassigns the exact signal `form(this.searchModel)` wraps from *outside*
    // the form system on every URL change, which corrupts `FormField`'s internal state mid-render
    // (`TypeError: this.field(...) is not a function`, thrown out of `CatalogueListPage_Template` and
    // silently blanking everything after the search box — found by actually rendering the screen, not
    // by reasoning about it). `onClearFilters` below writes to the same signal too, but only from a
    // discrete click handler, the same shape `onKindChange` / `onBabChange` already use safely
    // elsewhere in this codebase — the difference that matters is "reactive effect" vs "user gesture".
    this.searchModel.set({ query: this.searchTerm() });

    effect(() => {
      // Reading both is what makes this effect re-run when either changes.
      void this.searchTerm();
      void this.statusFilter();
      void this.reload();
    });
  }

  protected async onSubmitSearch(): Promise<void> {
    await this.navigate({ search: this.searchModel().query.trim() || null });
  }

  protected async onCreate(): Promise<void> {
    await this.router.navigate(['/catalogue/new']);
  }

  protected async onFilter(filter: CatalogueItemListFilter): Promise<void> {
    await this.navigate({ status: filter });
  }

  protected async onClearFilters(): Promise<void> {
    this.searchModel.set({ query: '' });
    await this.navigate({ search: null, status: null });
  }

  protected filterKey(filter: CatalogueItemListFilter): string {
    switch (filter) {
      case 'active':
        return 'catalogue.filter.active';
      case 'archived':
        return 'catalogue.filter.archived';
      case 'all':
        return 'catalogue.filter.all';
    }
  }

  /** Arabic name in Arabic, English name in English — never both, never the raw enum. */
  protected babLabel(group: CatalogueGroup): string {
    if (group.bab === null) {
      return this.i18n.t('catalogue.list.bab_unknown');
    }
    return this.i18n.locale() === 'en' ? group.bab.nameEn : group.bab.nameAr;
  }

  /** `KAFF-925`: null when the باب is unknown — no markup exists to show, so the pill is omitted
   *  rather than showing a made-up figure. */
  protected groupMarginText(group: CatalogueGroup): string | null {
    if (group.bab === null) {
      return null;
    }
    const percent = this.i18n.formatNumber(fractionToPercent(group.bab.defaultMarkup), {
      maximumFractionDigits: 4,
    });
    return this.i18n.t('catalogue.list.group_margin', { percent });
  }

  protected groupCountText(group: CatalogueGroup): string {
    return this.i18n.t('catalogue.list.group_count', { count: group.items.length });
  }

  protected isArchiving(id: string): boolean {
    return this.archivingId() === id;
  }

  /**
   * One item per row: `archive` (danger, active items) or `unarchive` (archived items) — never both.
   * `V-38`/bab-tree precedent: the kit computes a menu item's own test id as `{testId}-{action}`, not
   * as a free-standing id a caller can choose, so the trigger's `testId` is `catalogue-row-menu-{code}`
   * and the item ids follow from it (`-archive`/`-unarchive`) rather than reusing `catalogue-archive-`/
   * `catalogue-unarchive-` verbatim — see this story's build notes for why, and the grep that found no
   * test or QA reference to the old trigger ids before renaming them.
   */
  protected rowMenuItems(item: CatalogueItem): readonly RowMenuItem[] {
    return item.status === 'Active'
      ? [{ labelKey: 'catalogue.item.archive_action', action: 'archive', tone: 'danger' }]
      : [
          {
            labelKey: 'catalogue.item.unarchive_action',
            action: 'unarchive',
            disabled: this.isArchiving(item.id),
          },
        ];
  }

  protected onRowMenuSelected(item: CatalogueItem, action: string): void {
    if (action === 'archive') {
      this.onStartArchive(item.id);
    } else if (action === 'unarchive') {
      void this.onUnarchive(item.id);
    }
  }

  protected onOpenItem(item: CatalogueItem): Promise<void> {
    return this.navigate({ item: item.code });
  }

  protected onClosePanel(): Promise<void> {
    return this.navigate({ item: null });
  }

  protected isSavingCost(id: string): boolean {
    return this.savingCostId() === id;
  }

  protected costRefusalKey(id: string): string | null {
    const failed = this.costFailure();
    return failed && failed.id === id ? failed.key : null;
  }

  /** `kaff-inline-edit`'s caller-driven rollback contract (`KAFF-930` build notes), same shape
   *  `bab-tree-page.ts`'s `onMarkupCommitted` uses. `CatalogueItemEdit` has no partial-update shape, so
   *  every other field — including `baseSellRate`, which this panel only ever shows derived, never
   *  edits — travels back over the wire unchanged. */
  protected async onCostCommitted(item: CatalogueItem, raw: string): Promise<void> {
    this.costFailure.set(null);

    let costPrice: string;
    try {
      costPrice = toWireDecimal(raw);
    } catch {
      this.costFailure.set({ id: item.id, key: 'catalogue.field.price_format_invalid' });
      return;
    }

    this.savingCostId.set(item.id);
    try {
      await this.api.edit(item.id, {
        descriptionAr: item.descriptionAr,
        descriptionEn: item.descriptionEn,
        unit: item.unit,
        costPrice,
        baseSellRate: item.baseSellRate,
      });
      await this.reload();
    } catch (error) {
      this.costFailure.set({ id: item.id, key: toProblem(error).messageKey });
    } finally {
      this.savingCostId.set(null);
    }
  }

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
      await this.reload();
    } catch (error) {
      // AC-206-E: surfaced, not swallowed — including the "already archived" race.
      this.rowFailure.set({ id, key: toProblem(error).messageKey });
      this.confirmingArchiveId.set(null);
    } finally {
      this.archivingId.set(null);
    }
  }

  protected async onUnarchive(id: string): Promise<void> {
    this.rowFailure.set(null);
    this.archivingId.set(id);

    try {
      await this.api.unarchive(id);
      await this.reload();
    } catch (error) {
      this.rowFailure.set({ id, key: toProblem(error).messageKey });
    } finally {
      this.archivingId.set(null);
    }
  }

  protected trackGroup(_index: number, group: CatalogueGroup): string {
    return group.babId;
  }

  protected trackItem(_index: number, item: CatalogueItem): string {
    return item.id;
  }

  private async navigate(patch: Readonly<Record<string, string | null>>): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: patch,
      queryParamsHandling: 'merge',
    });
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);

    try {
      this.items.set(await this.api.list(this.searchTerm(), this.statusFilter()));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.items.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadBabs(): Promise<void> {
    try {
      this.babs.set(await this.babsApi.list());
    } catch {
      // `GET /api/babs` is gated `Permission.BabManage`, not `CatalogueManage` — the brief's own
      // warning that the two can diverge. A caller holding one but not the other still sees every
      // item; groups just render without a name rather than the whole screen failing.
      this.babs.set([]);
    }
  }
}
