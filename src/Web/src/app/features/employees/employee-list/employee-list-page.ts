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
import { EmployeeListFilter, EmployeeSummary, EmployeesApi } from '../../../core/employees/employees.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffRowMenu, RowMenuItem } from '../../../shared/kaff-row-menu/kaff-row-menu';
import {
  KaffSegmentedFilter,
  SegmentedFilterOption,
} from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

/** The three chips `AC-207-F` draws, matching `CatalogueListPage`'s and `BabTreePage`'s own. */
const FILTERS: readonly EmployeeListFilter[] = ['active', 'archived', 'all'];

/**
 * `S-023` · the employee register — `KAFF-207` (list), `KAFF-208` (the two populations shown together,
 * distinguished by a `Kind` badge rather than two lists).
 *
 * **One list, both populations.** `ListEmployees` (`GET /api/employees`) takes an optional `kind`
 * filter, but `S-023`'s own row in `ux/screen-inventory.md` is *"every costed person"* — the worker
 * pool's richer view (average day rate, frequency, rating) is `S-025`/`KAFF-210`, unbuilt. This screen
 * does not add a kind filter chip nobody asked for.
 *
 * **Filter lives in the URL**, not a signal alone — `apple-erp-design` §7.6, the fix `F-127-1` names for
 * `client-list-page.ts`'s own miss: a bookmarked `/employees?status=archived` must restore that filter.
 *
 * **KAFF-931**: rebuilt on the KAFF-930 interaction kit. The row's `edit` chip and filled-red `archive`
 * button collapse into one `kaff-row-menu`; danger is text, never a filled button. The kit computes a
 * menu item's own test id as `{testId}-{action}` (not a free-standing id a caller can choose), so the
 * two triggers could not keep `employee-edit-{code}`/`employee-archive-{code}` verbatim — renamed to
 * `employee-row-menu-{code}-edit`/`-archive`, the same bab-tree/catalogue-list precedent, after grepping
 * `tests\`/`qa\` for the old ids and finding no reference. The confirm/cancel/body/refusal ids below the
 * row are untouched. Loading/empty(-filtered)/refusal render through `kaff-state`.
 */
@Component({
  selector: 'kaff-employee-list-page',
  imports: [
    RouterLink,
    KaffBadge,
    KaffButton,
    KaffRowMenu,
    KaffSegmentedFilter,
    KaffState,
    KaffTable,
    KaffTableRow,
  ],
  templateUrl: './employee-list-page.html',
  styleUrl: './employee-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeeListPage {
  private readonly api = inject(EmployeesApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly i18n = inject(I18nService);
  protected readonly filters = FILTERS;

  /** Grid columns for `kaff-table`: code · name · phone · kind · specialty · archived badge.
   *  `KAFF-928`: fixed sizes plus one `minmax(_, 1fr)` for name, the one column whose content length
   *  varies. Code and phone are fixed-width figures, kind is a short badge word, specialty already
   *  truncates with ellipsis so a fixed width suits it, and the trailing column holds only the
   *  archived badge. */
  protected readonly rowColumns = '8rem minmax(10rem, 1fr) 9rem 7rem 9rem 7rem';

  /** `KAFF-925`: same column order the rows use. No "margin" column — this list has none. */
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'hr.employee.field.code' },
    { labelKey: 'hr.employee.field.full_name' },
    { labelKey: 'hr.employee.field.phone' },
    { labelKey: 'hr.employee.field.kind' },
    { labelKey: 'hr.employee.field.specialty' },
    {},
  ];

  protected readonly segmentedOptions: readonly SegmentedFilterOption<EmployeeListFilter>[] =
    FILTERS.map((filter) => ({ value: filter, labelKey: this.filterKey(filter) }));

  /** Bound by name from `?status=` — `withComponentInputBinding` in `app.config.ts`. */
  readonly status = input<EmployeeListFilter>('active');
  protected readonly statusFilter = computed(() => this.status() ?? 'active');

  protected readonly employees = signal<readonly EmployeeSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  private readonly archivingId = signal<string | null>(null);
  protected readonly confirmingArchiveId = signal<string | null>(null);
  protected readonly rowFailure = signal<{ readonly id: string; readonly key: string } | null>(null);

  /**
   * KAFF-929 finding 4: `ListEmployees` carries no separate total — the count of the current filter's
   * view, read from the response already held in {@link employees} rather than a call added for it.
   */
  protected readonly countText = computed(() =>
    this.i18n.t('hr.employee.list_count', { count: this.employees().length }),
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
    await this.router.navigate(['/employees/new']);
  }

  /** Literal keys, not `'hr.employee.filter.' + filter` — `TranslationCatalogueTests` scans for the
   *  literal string a template writes, and a concatenated one is invisible to it. */
  protected filterKey(filter: EmployeeListFilter): string {
    switch (filter) {
      case 'active':
        return 'hr.employee.filter.active';
      case 'archived':
        return 'hr.employee.filter.archived';
      case 'all':
        return 'hr.employee.filter.all';
    }
  }

  protected async onFilter(filter: EmployeeListFilter): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { status: filter },
      queryParamsHandling: 'merge',
    });
  }

  /** `enum.EmployeeKind.Salaried` / `.DayLabour` — already in both catalogues, never a synonym. */
  protected kindKey(employee: EmployeeSummary): string {
    return `enum.EmployeeKind.${employee.kind}`;
  }

  protected trackRow(_index: number, employee: EmployeeSummary): string {
    return employee.id;
  }

  protected isArchiving(id: string): boolean {
    return this.archivingId() === id;
  }

  protected rowMenuItems(employee: EmployeeSummary): readonly RowMenuItem[] {
    const items: RowMenuItem[] = [{ labelKey: 'action.edit', action: 'edit' }];
    if (employee.isActive) {
      items.push({ labelKey: 'hr.employee.archive_action', action: 'archive', tone: 'danger' });
    }
    return items;
  }

  protected async onRowMenuSelected(employee: EmployeeSummary, action: string): Promise<void> {
    switch (action) {
      case 'edit':
        await this.router.navigate(['/employees', employee.id], { state: { employee } });
        break;
      case 'archive':
        this.onStartArchive(employee.id);
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
      // AC-207-F: surfaced, not swallowed — including the "already archived" race.
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
      this.employees.set(await this.api.list(this.statusFilter()));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.employees.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
