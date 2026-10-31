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

import { toProblem } from '../../../core/api/problem-details';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { SupplierListFilter, SupplierSummary, SuppliersApi } from '../../../core/suppliers/suppliers.api';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffSearchField } from '../../../shared/kaff-search-field/kaff-search-field';
import { KaffSegmentedFilter, SegmentedFilterOption } from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffRowMenu, RowMenuItem } from '../../../shared/kaff-row-menu/kaff-row-menu';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

/** `S-030`'s three server states, matching `EmployeeListPage`'s own shape. */
const FILTERS: readonly SupplierListFilter[] = ['active', 'archived', 'all'];

/**
 * `S-030` · the supplier list (and, per its own row in `ux/screen-inventory.md`, create/edit share the
 * screen too — the form lives at `supplier-form-page.ts`). KAFF-212.
 *
 * **One row per supplier, never one per project** — KAFF-212 rule 2, AC-212-B: §2's whole point is one
 * account serving many projects, so this row carries no project column to invent one from.
 *
 * **Filter lives in the URL**, matching `EmployeeListPage`'s and `SubcontractorListPage`'s own shape
 * (apple-erp-design §7.6).
 *
 * **KAFF-931**: the row's edit chip and filled-red archive button collapse into one `kaff-row-menu`
 * (danger is text, never a filled button). Loading/empty(-filtered)/refusal render through `kaff-state`.
 *
 * **KAFF-932**: moved onto the `kaff-table` / `kaff-table-header` / `kaff-table-row` shell, same shape
 * as `EmployeeListPage` — column geometry lives in the shell now, not in this stylesheet.
 */
@Component({
  selector: 'kaff-supplier-list-page',
  imports: [RouterLink, KaffButton, KaffSearchField, KaffSegmentedFilter, KaffRowMenu, KaffState, KaffTable, KaffTableRow],
  templateUrl: './supplier-list-page.html',
  styleUrl: './supplier-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SupplierListPage {
  private readonly api = inject(SuppliersApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly i18n = inject(I18nService);
  protected readonly filters = FILTERS;

  /** Grid columns for `kaff-table`: identity (name, with the code beneath it) · phone · address ·
   *  trailing. */
  protected readonly rowColumns = 'minmax(0, 1fr) 9rem minmax(10rem, 16rem) 7rem';

  /** `KAFF-925`: same column order the rows use. No label on the trailing actions column. */
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'supplier.field.name' },
    { labelKey: 'supplier.field.phone' },
    { labelKey: 'supplier.field.address' },
    {},
  ];

  readonly status = input<SupplierListFilter>('active');
  protected readonly statusFilter = computed(() => this.status() ?? 'active');

  protected readonly segmentedOptions: readonly SegmentedFilterOption<SupplierListFilter>[] = FILTERS.map(
    (filter) => ({ value: filter, labelKey: this.filterKey(filter) }),
  );

  /** The search is local: the register is a few dozen rows, so name, code and phone are matched here. */
  protected readonly searchTerm = signal('');

  protected readonly all = signal<readonly SupplierSummary[]>([]);
  protected readonly suppliers = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    if (term === '') return this.all();
    return this.all().filter((row) =>
      [row.name, row.code, row.phone].some((value) => (value ?? '').toLowerCase().includes(term)),
    );
  });
  protected readonly countText = computed(() =>
    this.i18n.t('supplier.list.count', { count: this.suppliers().length }),
  );

  private readonly createAction = viewChild<TemplateRef<unknown>>('createAction');
  private readonly headerActions = inject(HeaderActionsService);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  private readonly archivingId = signal<string | null>(null);
  protected readonly confirmingArchiveId = signal<string | null>(null);
  protected readonly rowFailure = signal<{ readonly id: string; readonly key: string } | null>(null);

  constructor() {
    effect(() => this.headerActions.set(this.createAction() ?? null));
    inject(DestroyRef).onDestroy(() => this.headerActions.set(null));

    effect(() => {
      void this.statusFilter();
      void this.reload();
    });
  }

  protected filterKey(filter: SupplierListFilter): string {
    switch (filter) {
      case 'active':
        return 'supplier.filter.active';
      case 'archived':
        return 'supplier.filter.archived';
      case 'all':
        return 'supplier.filter.all';
    }
  }

  protected async onCreate(): Promise<void> {
    await this.router.navigate(['/suppliers/new']);
  }

  protected async onFilter(filter: SupplierListFilter): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { status: filter },
      queryParamsHandling: 'merge',
    });
  }

  protected trackRow(_index: number, supplier: SupplierSummary): string {
    return supplier.id;
  }

  protected isArchiving(id: string): boolean {
    return this.archivingId() === id;
  }

  protected rowMenuItems(supplier: SupplierSummary): readonly RowMenuItem[] {
    const items: RowMenuItem[] = [{ labelKey: 'action.edit', action: 'edit' }];
    if (supplier.isActive) {
      items.push({ labelKey: 'supplier.archive_action', action: 'archive', tone: 'danger' });
    }
    return items;
  }

  protected async onRowMenuSelected(supplier: SupplierSummary, action: string): Promise<void> {
    switch (action) {
      case 'edit':
        await this.router.navigate(['/suppliers', supplier.id]);
        break;
      case 'archive':
        this.onStartArchive(supplier.id);
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
}
