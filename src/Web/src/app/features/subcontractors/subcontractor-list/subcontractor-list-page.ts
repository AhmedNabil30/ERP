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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { BabsApi, Bab } from '../../../core/catalogue/babs.api';
import { toProblem } from '../../../core/api/problem-details';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { fractionToPercent } from '../../../core/catalogue/percent-wire';
import {
  SubcontractorListFilter,
  SubcontractorSummary,
  SubcontractorsApi,
} from '../../../core/subcontractors/subcontractors.api';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffSearchField } from '../../../shared/kaff-search-field/kaff-search-field';
import { KaffSegmentedFilter, SegmentedFilterOption } from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffRowMenu, RowMenuItem } from '../../../shared/kaff-row-menu/kaff-row-menu';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

/** `S-028`'s three server states, matching `EmployeeListPage`'s own shape. */
const FILTERS: readonly SubcontractorListFilter[] = ['active', 'archived', 'all'];

/**
 * `S-028` · the subcontractor list. KAFF-211.
 *
 * **Filter lives in the URL**, not a signal alone — apple-erp-design §7.6: a bookmarked
 * `/subcontractors?status=archived` must restore that filter, mirroring `EmployeeListPage`.
 *
 * **No rate, no price, no bid of any kind on this row** — AC-211-H, AC-211-L: only the retention
 * percentage, which is a different figure with a different source (§5.1) than the rate card §1 and
 * D-139 §4 keep off this record entirely.
 *
 * **KAFF-931**: the row's edit chip and filled-red archive button collapse into one `kaff-row-menu`
 * (danger is text, never a filled button). Loading/empty(-filtered)/refusal render through `kaff-state`.
 *
 * **KAFF-932**: moved onto the `kaff-table` / `kaff-table-header` / `kaff-table-row` shell, same shape
 * as `EmployeeListPage` — column geometry lives in the shell now, not in this stylesheet.
 */
@Component({
  selector: 'kaff-subcontractor-list-page',
  imports: [RouterLink, KaffButton, KaffSearchField, KaffSegmentedFilter, KaffRowMenu, KaffState, KaffTable, KaffTableRow],
  templateUrl: './subcontractor-list-page.html',
  styleUrl: './subcontractor-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubcontractorListPage {
  private readonly api = inject(SubcontractorsApi);
  private readonly babsApi = inject(BabsApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly i18n = inject(I18nService);
  protected readonly filters = FILTERS;

  /** Grid columns for `kaff-table`: identity (name, with the code beneath it) · phone · trade ·
   *  retention % · trailing. Same track widths as `EmployeeListPage.rowColumns` — AC-932-B: column
   *  edges align across list screens because the widths, not just the shape, match. */
  protected readonly rowColumns = 'minmax(0, 1fr) 9rem 7rem 9rem 7rem';

  /** `KAFF-925`: same column order the rows use. No label on the trailing actions column. */
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'subcontractor.field.name' },
    { labelKey: 'subcontractor.field.phone' },
    { labelKey: 'subcontractor.field.trade' },
    { labelKey: 'subcontractor.field.retention_rate', align: 'end' },
    {},
  ];

  /** Bound by name from `?status=` — `withComponentInputBinding` in `app.config.ts`. */
  readonly status = input<SubcontractorListFilter>('active');
  protected readonly statusFilter = computed(() => this.status() ?? 'active');

  protected readonly segmentedOptions: readonly SegmentedFilterOption<SubcontractorListFilter>[] = FILTERS.map(
    (filter) => ({ value: filter, labelKey: this.filterKey(filter) }),
  );

  /** The search is local: the register is a few dozen rows, so name, code and phone are matched here. */
  protected readonly searchTerm = signal('');

  protected readonly all = signal<readonly SubcontractorSummary[]>([]);
  protected readonly subcontractors = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    if (term === '') return this.all();
    return this.all().filter((row) =>
      [row.name, row.code, row.phone].some((value) => (value ?? '').toLowerCase().includes(term)),
    );
  });
  protected readonly countText = computed(() =>
    this.i18n.t('subcontractor.list.count', { count: this.subcontractors().length }),
  );

  private readonly createAction = viewChild<TemplateRef<unknown>>('createAction');
  private readonly headerActions = inject(HeaderActionsService);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  private readonly babs = signal<readonly Bab[]>([]);

  private readonly archivingId = signal<string | null>(null);
  protected readonly confirmingArchiveId = signal<string | null>(null);
  protected readonly rowFailure = signal<{ readonly id: string; readonly key: string } | null>(null);

  constructor() {
    effect(() => this.headerActions.set(this.createAction() ?? null));
    inject(DestroyRef).onDestroy(() => this.headerActions.set(null));

    void this.loadBabs();

    effect(() => {
      void this.statusFilter();
      void this.reload();
    });
  }

  /** Literal keys, never a concatenation — `TranslationCatalogueTests` scans for the literal string. */
  protected filterKey(filter: SubcontractorListFilter): string {
    switch (filter) {
      case 'active':
        return 'subcontractor.filter.active';
      case 'archived':
        return 'subcontractor.filter.archived';
      case 'all':
        return 'subcontractor.filter.all';
    }
  }

  protected async onCreate(): Promise<void> {
    await this.router.navigate(['/subcontractors/new']);
  }

  protected async onFilter(filter: SubcontractorListFilter): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { status: filter },
      queryParamsHandling: 'merge',
    });
  }

  /** The trade باب's name, or the shared "unknown" fallback — the catalogue list's own key, unowned by any feature. */
  protected tradeLabel(subcontractor: SubcontractorSummary): string {
    if (subcontractor.tradeBabId === null) {
      return this.i18n.t('subcontractor.field.no_trade');
    }

    const bab = this.babs().find((candidate) => candidate.id === subcontractor.tradeBabId);
    if (bab === undefined) {
      return this.i18n.t('catalogue.list.bab_unknown');
    }

    return this.i18n.locale() === 'en' ? bab.nameEn : bab.nameAr;
  }

  /** `"0.050000"` -> `"5"` — a percent an operator reads, never the bare fraction (D-151). */
  protected retentionLabel(subcontractor: SubcontractorSummary): string {
    return fractionToPercent(subcontractor.retentionRate);
  }

  protected trackRow(_index: number, subcontractor: SubcontractorSummary): string {
    return subcontractor.id;
  }

  protected isArchiving(id: string): boolean {
    return this.archivingId() === id;
  }

  protected rowMenuItems(subcontractor: SubcontractorSummary): readonly RowMenuItem[] {
    const items: RowMenuItem[] = [{ labelKey: 'action.edit', action: 'edit' }];
    if (subcontractor.isActive) {
      items.push({ labelKey: 'subcontractor.archive_action', action: 'archive', tone: 'danger' });
    }
    return items;
  }

  protected async onRowMenuSelected(subcontractor: SubcontractorSummary, action: string): Promise<void> {
    switch (action) {
      case 'edit':
        await this.router.navigate(['/subcontractors', subcontractor.id]);
        break;
      case 'archive':
        this.onStartArchive(subcontractor.id);
        break;
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
      this.rowFailure.set({ id, key: toProblem(error).messageKey });
      this.confirmingArchiveId.set(null);
    } finally {
      this.archivingId.set(null);
    }
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);

    try {
      this.all.set(await this.api.list(this.statusFilter()));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.all.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadBabs(): Promise<void> {
    try {
      this.babs.set(await this.babsApi.list('all'));
    } catch {
      this.babs.set([]);
    }
  }
}
