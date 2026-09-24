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
import { ActivatedRoute, Router } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { Bab, BabListFilter, BabsApi } from '../../../core/catalogue/babs.api';
import { BabTreeRow, flattenBabTree } from '../../../core/catalogue/bab-tree';
import { fractionToPercent, percentToFraction } from '../../../core/catalogue/percent-wire';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
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

/** The three chips `KAFF-213` rule 9 draws, in the order it draws them — matches `CatalogueListPage`. */
const FILTERS: readonly BabListFilter[] = ['active', 'archived', 'all'];

/**
 * `S-021` · the باب tree — `KAFF-204` (create/edit link out, markup), `KAFF-205` (re-parent),
 * `KAFF-213` (archive). One flat list from `GET /api/babs`, nested client-side by `flattenBabTree`
 * (`KAFF-204` rule 6: "roughly 40 trades" is small enough that no server-side tree endpoint exists,
 * or is needed).
 *
 * **KAFF-931**: rebuilt on the KAFF-930 interaction kit. Every per-row button band is gone — one
 * `kaff-row-menu` per row (تعديل · نقل إلى أب آخر · أرشفة الباب, the last `tone="danger"` text, never
 * a filled red button). It is now an actual tree: a parent with children gets a disclosure control
 * (`aria-expanded`), and which parents are collapsed lives in `?collapsed=` — a comma list of باب
 * ids, so a collapsed branch survives a refresh and can be linked. The default markup % edits in
 * place with `kaff-inline-edit`, writing back through `BabsApi.edit` (name fields carried through
 * unchanged — `BabEdit` has no partial-update shape). Loading, empty and refusal render through
 * `kaff-state`.
 *
 * **Re-parent and archive both stay row-initiated**, the same shape `CatalogueListPage` uses for
 * archive/un-archive — the row menu opens the same inline picker / confirmation that lived under the
 * old button band; there is no separate confirmation screen for either, and no drag-to-reparent
 * (`KAFF-931` explicitly rules it out — a "move to…" picker is fast enough for ~40 trades).
 */
@Component({
  selector: 'kaff-bab-tree-page',
  imports: [
    KaffBadge,
    KaffButton,
    KaffInlineEdit,
    KaffRowMenu,
    KaffSegmentedFilter,
    KaffState,
    KaffTable,
    KaffTableRow,
  ],
  templateUrl: './bab-tree-page.html',
  styleUrl: './bab-tree-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BabTreePage {
  private readonly api = inject(BabsApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly i18n = inject(I18nService);
  protected readonly filters = FILTERS;

  /** Grid columns for `kaff-table`: code · name · markup · archived badge · menu. `KAFF-928`: fixed
   *  sizes plus one `minmax(_, 1fr)` for name, the one column whose content length varies. One extra
   *  fixed track for the row menu trigger, replacing the old per-row button band. */
  protected readonly rowColumns = '8rem minmax(10rem, 1fr) 7rem 7rem 3rem';

  /** `KAFF-925`: same column order the rows use. */
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'bab.field.code' },
    { labelKey: 'bab.column.name' },
    { labelKey: 'bab.field.default_markup', align: 'end' },
    {},
    {},
  ];

  protected readonly segmentedOptions: readonly SegmentedFilterOption<BabListFilter>[] = FILTERS.map(
    (filter) => ({ value: filter, labelKey: 'bab.filter.' + filter }),
  );

  /** Bound from `?status=` by `withComponentInputBinding` — read through `statusFilter()`, never
   *  directly: the router sets this to `undefined`, not the `input()` default, when the query param
   *  is simply absent from the URL (`catalogue-list-page.ts`'s own finding, `KAFF-930` build notes). */
  readonly status = input<BabListFilter>('active');
  protected readonly statusFilter = computed(() => this.status() ?? 'active');

  /** Bound from `?collapsed=` — a comma-separated list of collapsed باب ids. Absent/empty means
   *  every branch is expanded, which is today's only behaviour (a flat list, nothing collapsible). */
  readonly collapsed = input<string>('');
  protected readonly collapsedIds = computed(
    () => new Set((this.collapsed() ?? '').split(',').filter((id) => id.length > 0)),
  );

  protected readonly babs = signal<readonly Bab[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  protected readonly allRows = computed(() => flattenBabTree(this.babs()));

  /** Only the ids that have at least one child — the ones that get a disclosure control at all. */
  private readonly parentIds = computed(() => {
    const ids = new Set<string>();
    for (const bab of this.babs()) {
      if (bab.parentBabId) {
        ids.add(bab.parentBabId);
      }
    }
    return ids;
  });

  /** Single pass over the pre-order tree: a row inside a collapsed ancestor's subtree is skipped,
   *  including nested collapses — `hideDepth` tracks the depth of the branch currently hidden, and
   *  resets the moment a row at or above that depth is reached. */
  protected readonly rows = computed<readonly BabTreeRow[]>(() => {
    const collapsedIds = this.collapsedIds();
    const result: BabTreeRow[] = [];
    let hideDepth: number | null = null;

    for (const row of this.allRows()) {
      if (hideDepth !== null && row.depth > hideDepth) {
        continue;
      }
      hideDepth = null;
      result.push(row);
      if (collapsedIds.has(row.bab.id)) {
        hideDepth = row.depth;
      }
    }

    return result;
  });

  private readonly movingId = signal<string | null>(null);
  protected readonly confirmingMoveId = signal<string | null>(null);
  protected readonly moveTargetId = signal<string>('');
  protected readonly moveFailure = signal<{ readonly id: string; readonly key: string } | null>(null);

  private readonly archivingId = signal<string | null>(null);
  protected readonly confirmingArchiveId = signal<string | null>(null);
  protected readonly archiveFailure = signal<{
    readonly id: string;
    readonly key: string;
    readonly params: Readonly<Record<string, string | number>>;
  } | null>(null);

  private readonly savingMarkupId = signal<string | null>(null);
  protected readonly markupFailure = signal<{ readonly id: string; readonly key: string } | null>(null);

  /**
   * KAFF-929 finding 4: `ListBabs` carries no separate total — the count of the current filter's
   * view, read from the response already held in {@link babs} rather than a call added for it.
   */
  protected readonly countText = computed(() =>
    this.i18n.t('bab.tree.count', { count: this.babs().length }),
  );

  /** KAFF-929 finding 2, matching KAFF-926's `UserListPage`. */
  private readonly createAction = viewChild<TemplateRef<unknown>>('createAction');
  private readonly headerActions = inject(HeaderActionsService);

  constructor() {
    effect(() => {
      void this.statusFilter();
      void this.reload();
    });

    effect(() => this.headerActions.set(this.createAction() ?? null));
    inject(DestroyRef).onDestroy(() => this.headerActions.set(null));
  }

  protected async onCreate(): Promise<void> {
    await this.router.navigate(['/babs/new']);
  }

  protected async onFilter(filter: BabListFilter): Promise<void> {
    await this.navigate({ status: filter });
  }

  /** Arabic name in Arabic, English name in English — never both, never the raw field. */
  protected babLabel(bab: Bab): string {
    return this.i18n.locale() === 'en' ? bab.nameEn : bab.nameAr;
  }

  protected markupPercent(bab: Bab): string {
    return this.i18n.formatNumber(fractionToPercent(bab.defaultMarkup), { maximumFractionDigits: 4 });
  }

  /** Every other باب — a node cannot move under itself; the server refuses any deeper cycle (`AC-205-C`). */
  protected candidateParents(bab: Bab): readonly Bab[] {
    return this.babs().filter((candidate) => candidate.id !== bab.id);
  }

  protected trackRow(_index: number, row: BabTreeRow): string {
    return row.bab.id;
  }

  protected hasChildren(id: string): boolean {
    return this.parentIds().has(id);
  }

  protected isCollapsed(id: string): boolean {
    return this.collapsedIds().has(id);
  }

  protected async onToggleCollapse(id: string): Promise<void> {
    const next = new Set(this.collapsedIds());
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }
    await this.navigate({ collapsed: next.size > 0 ? [...next].join(',') : null });
  }

  protected rowMenuItems(bab: Bab): readonly RowMenuItem[] {
    const items: RowMenuItem[] = [
      { labelKey: 'action.edit', action: 'edit' },
      { labelKey: 'bab.move.title', action: 'move' },
    ];
    if (bab.isActive) {
      items.push({ labelKey: 'bab.tree.archive_action', action: 'archive', tone: 'danger' });
    }
    return items;
  }

  protected async onRowMenuSelected(bab: Bab, action: string): Promise<void> {
    switch (action) {
      case 'edit':
        await this.router.navigate(['/babs', bab.id]);
        break;
      case 'move':
        this.onStartMove(bab);
        break;
      case 'archive':
        this.onStartArchive(bab.id);
        break;
    }
  }

  protected isMoving(id: string): boolean {
    return this.movingId() === id;
  }

  protected isArchiving(id: string): boolean {
    return this.archivingId() === id;
  }

  protected onStartMove(bab: Bab): void {
    this.moveFailure.set(null);
    this.moveTargetId.set(bab.parentBabId ?? '');
    this.confirmingMoveId.set(bab.id);
  }

  protected onCancelMove(): void {
    this.confirmingMoveId.set(null);
  }

  protected onMoveTargetChange(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLSelectElement) {
      this.moveTargetId.set(target.value);
    }
  }

  protected async onConfirmMove(id: string): Promise<void> {
    this.moveFailure.set(null);
    this.movingId.set(id);

    try {
      const target = this.moveTargetId();
      await this.api.move(id, target.length > 0 ? target : null);
      this.confirmingMoveId.set(null);
      await this.reload();
    } catch (error) {
      // AC-205-C: surfaced, not swallowed — including the cycle refusal at any depth.
      this.moveFailure.set({ id, key: toProblem(error).messageKey });
    } finally {
      this.movingId.set(null);
    }
  }

  protected onStartArchive(id: string): void {
    this.archiveFailure.set(null);
    this.confirmingArchiveId.set(id);
  }

  protected onCancelArchive(): void {
    this.confirmingArchiveId.set(null);
  }

  protected async onConfirmArchive(id: string): Promise<void> {
    this.archiveFailure.set(null);
    this.archivingId.set(id);

    try {
      await this.api.archive(id);
      this.confirmingArchiveId.set(null);
      await this.reload();
    } catch (error) {
      // AC-213-B: the refusal names the count of active items still filed under the باب — carried in
      // the ProblemDetails `count` extension, resolved through the `{count}` placeholder both
      // catalogues already carry for `errors.master.bab_has_active_items`.
      const problem = toProblem(error);
      const count = problem.extensions['count'];

      this.archiveFailure.set({
        id,
        key: problem.messageKey,
        params: typeof count === 'number' ? { count } : {},
      });
    } finally {
      this.archivingId.set(null);
    }
  }

  /** `kaff-inline-edit`'s caller-driven rollback contract (`KAFF-930` build notes): the component
   *  shows the typed value optimistically; a rejected commit — including a value that does not even
   *  parse as a percent — reverts it and shows this. `BabEdit` has no partial-update shape, so the
   *  name fields go back over the wire unchanged. */
  protected async onMarkupCommitted(bab: Bab, percent: string): Promise<void> {
    this.markupFailure.set(null);

    let fraction: string;
    try {
      fraction = percentToFraction(percent);
    } catch {
      this.markupFailure.set({ id: bab.id, key: 'bab.field.markup_format_invalid' });
      return;
    }

    this.savingMarkupId.set(bab.id);
    try {
      await this.api.edit(bab.id, { nameAr: bab.nameAr, nameEn: bab.nameEn, defaultMarkup: fraction });
      await this.reload();
    } catch (error) {
      this.markupFailure.set({ id: bab.id, key: toProblem(error).messageKey });
    } finally {
      this.savingMarkupId.set(null);
    }
  }

  protected markupRefusalKey(id: string): string | null {
    const failed = this.markupFailure();
    return failed && failed.id === id ? failed.key : null;
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
      this.babs.set(await this.api.list(this.statusFilter()));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.babs.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
