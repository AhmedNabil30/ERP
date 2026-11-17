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
  untracked,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import {
  BabOption,
  EmployeeKind,
  EmployeeListFilter,
  EmployeeSummary,
  EmployeesApi,
} from '../../../core/employees/employees.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffInlineConfirm } from '../../../shared/kaff-inline-confirm/kaff-inline-confirm';
import { KaffRowMenu, RowMenuItem } from '../../../shared/kaff-row-menu/kaff-row-menu';
import { KaffSearchField } from '../../../shared/kaff-search-field/kaff-search-field';
import { KaffSegmentedFilter, SegmentedFilterOption } from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffSelect, SelectOption } from '../../../shared/kaff-select/kaff-select';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';
import { EmployeePanel, EmployeePanelTarget } from '../employee-panel/employee-panel';

/** The three chips `AC-207-F` draws, matching the catalogue's and the أبواب tree's own. */
const FILTERS: readonly EmployeeListFilter[] = ['active', 'archived', 'all'];

const PHONE_QUERY = '(max-width: 820px)';

/** A row, or the one green line a just-restored person leaves behind under the «مؤرشف» filter. */
type Entry =
  | { readonly type: 'row'; readonly row: EmployeeSummary }
  | { readonly type: 'restored'; readonly id: string; readonly name: string };

const digits = (text: string): string => text.replace(/\D/g, '');

/**
 * The two people lists, one component: «الموظفون» (`/employees`, salaried) and «عمالة اليومية» (`/workers`,
 * day labour). D-231, D-232, `people.html`. The route's `kind` datum picks the population; it is sent to the
 * API as `?kind=` so a list only ever holds its own people, and it is what the panel registers them as.
 * Nobody picks a kind on a form.
 *
 * Search by name or phone is client-side, over the rows the status filter already loaded. The register and
 * edit form is `kaff-employee-panel`, a side panel over this list (a bottom sheet on a phone).
 *
 * Archive is recoverable: the row's menu and the panel both open the inline confirmation. «إلغاء الأرشفة»
 * restores in place; under «مؤرشف» the restored person stays once as a green line, and the count and the
 * rows agree because the line is not a row.
 */
@Component({
  selector: 'kaff-employee-list-page',
  imports: [
    KaffBadge,
    KaffBanner,
    KaffButton,
    KaffIcon,
    KaffInlineConfirm,
    KaffRowMenu,
    KaffSearchField,
    KaffSegmentedFilter,
    KaffSelect,
    KaffState,
    KaffTable,
    KaffTableRow,
    EmployeePanel,
  ],
  templateUrl: './employee-list-page.html',
  styleUrl: './employee-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeeListPage {
  private readonly api = inject(EmployeesApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly headerActions = inject(HeaderActionsService);

  protected readonly i18n = inject(I18nService);

  /** From the route's `data`, bound by `withComponentInputBinding`. */
  readonly kind = input<EmployeeKind>('Salaried');
  /** Bound by name from `?status=`. The filter lives in the URL so a bookmark restores it. */
  readonly status = input<EmployeeListFilter>('active');

  protected readonly statusFilter = computed(() => this.status() ?? 'active');
  protected readonly isWorkers = computed(() => this.kind() === 'DayLabour');
  protected readonly segmentedOptions: readonly SegmentedFilterOption<EmployeeListFilter>[] = FILTERS.map(
    (filter) => ({ value: filter, labelKey: this.filterKey(filter) }),
  );

  protected readonly rows = signal<readonly EmployeeSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly query = signal('');
  protected readonly babFilter = signal('');
  protected readonly allBabs = signal<readonly BabOption[]>([]);

  protected readonly isPhone = signal(typeof matchMedia === 'function' && matchMedia(PHONE_QUERY).matches);

  /** Only one confirmation is open at a time (README "Row actions"). */
  protected readonly confirmingArchiveId = signal<string | null>(null);
  protected readonly archivingId = signal<string | null>(null);
  protected readonly rowFailure = signal<{ readonly id: string; readonly key: string } | null>(null);
  protected readonly restored = signal<{ readonly id: string; readonly name: string; readonly index: number } | null>(
    null,
  );
  protected readonly justSavedId = signal<string | null>(null);

  protected readonly panelTarget = signal<EmployeePanelTarget | null>(null);

  /** Grid tracks: identity (the one flexing track, pinned) first, the phone and the row menu last. */
  protected readonly columns = computed(() =>
    this.isWorkers()
      ? 'minmax(12rem, 1.4fr) minmax(0, 1fr) minmax(0, 1fr) 8rem 7rem 9rem 2.75rem'
      : 'minmax(12rem, 1.4fr) minmax(0, 1fr) minmax(0, 1fr) 7.5rem 9rem 2.75rem',
  );
  protected readonly minInline = computed(() => (this.isWorkers() ? 980 : 800));

  protected readonly headerColumns = computed<readonly TableColumnDef[]>(() =>
    this.isWorkers()
      ? [
          { labelKey: 'hr.employee.field.full_name', pinned: true },
          { labelKey: 'hr.employee.field.bab' },
          { labelKey: 'hr.employee.field.specialty' },
          { labelKey: 'hr.worker.pool.frequency', align: 'end' },
          { labelKey: 'hr.worker.pool.rating', align: 'end' },
          { labelKey: 'hr.employee.field.phone' },
          {},
        ]
      : [
          { labelKey: 'hr.employee.field.full_name', pinned: true },
          { labelKey: 'hr.employee.field.job_title' },
          { labelKey: 'hr.employee.field.department' },
          { labelKey: 'hr.employee.field.hired_on' },
          { labelKey: 'hr.employee.field.phone' },
          {},
        ],
  );

  protected readonly babOptions = computed<readonly SelectOption[]>(() => [
    { value: '', label: this.i18n.t('people.filter.all_babs') },
    ...this.allBabs()
      .filter((bab) => bab.isActive)
      .map((bab) => ({ value: bab.id, label: this.babLabel(bab) })),
  ]);

  /** The rows the search and the باب select leave. */
  protected readonly shown = computed(() => {
    const text = this.query().trim().toLowerCase();
    const number = digits(this.query());
    const bab = this.babFilter();
    return this.rows().filter((row) => {
      if (bab !== '' && row.babId !== bab) {
        return false;
      }
      return (
        text === '' ||
        row.fullName.toLowerCase().includes(text) ||
        (number !== '' && digits(row.phone).includes(number))
      );
    });
  });

  /** What the list draws: the shown rows, with a just-restored person's one green line in his place. */
  protected readonly entries = computed<readonly Entry[]>(() => {
    const done = this.restored();
    const rows = this.shown();
    const list: Entry[] = rows.map((row) =>
      done !== null && row.id === done.id
        ? { type: 'restored', id: done.id, name: done.name }
        : { type: 'row', row },
    );
    if (done !== null && !list.some((entry) => entry.type === 'restored')) {
      list.splice(Math.min(done.index, list.length), 0, { type: 'restored', id: done.id, name: done.name });
    }
    return list;
  });

  protected readonly filtering = computed(() => this.query().trim() !== '' || this.babFilter() !== '');
  /** Nothing registered in this population at all: the page shows its one first-run state, no tools. */
  protected readonly firstRun = computed(
    () =>
      !this.loading() &&
      this.failure() === null &&
      this.rows().length === 0 &&
      this.statusFilter() !== 'archived' &&
      !this.filtering() &&
      this.restored() === null,
  );

  protected readonly countText = computed(() => {
    const count = this.shown().filter((row) => row.id !== this.restored()?.id).length;
    const text = this.query().trim();
    const filter = this.statusFilter();
    const scope = this.i18n.t(
      filter === 'archived'
        ? 'people.scope.archived'
        : filter === 'all'
          ? 'people.scope.all'
          : this.isWorkers()
            ? 'people.scope.workers'
            : 'people.scope.staff',
    );
    if (text !== '') {
      return this.i18n.t('people.count.query', { count, query: text, scope });
    }
    if (filter === 'all') {
      return this.i18n.t('people.count.all', { count });
    }
    const population = this.isWorkers() ? 'workers' : 'staff';
    const form = count >= 3 && count <= 10 ? 'few' : count === 2 ? 'two' : 'one';
    const noun = this.i18n.t(`people.noun.${population}.${form}`);
    const many = count !== 1 && (count === 2 || (count >= 3 && count <= 10));
    const adj = this.i18n.t(
      `people.adj.${filter === 'archived' ? 'archived' : 'active'}_${many ? 'many' : 'one'}`,
    );
    return count === 1
      ? this.i18n.t('people.count.one', { noun, adj })
      : this.i18n.t('people.count.many', { count, noun, adj });
  });

  private readonly createAction = viewChild<TemplateRef<unknown>>('createAction');

  constructor() {
    if (typeof matchMedia === 'function') {
      const media = matchMedia(PHONE_QUERY);
      const onChange = (event: MediaQueryListEvent): void => this.isPhone.set(event.matches);
      media.addEventListener('change', onChange);
      this.destroyRef.onDestroy(() => media.removeEventListener('change', onChange));
    }

    effect(() => {
      void this.statusFilter();
      const kind = this.kind();
      untracked(() => {
        this.restored.set(null);
        this.confirmingArchiveId.set(null);
        this.justSavedId.set(null);
        this.panelTarget.set(null);
        this.babFilter.set('');
        void this.reload(kind);
      });
    });

    effect(() => {
      if (this.kind() === 'DayLabour') {
        untracked(() => void this.loadBabs());
      }
    });

    effect(() => this.headerActions.set(this.createAction() ?? null, 'primary'));
    this.destroyRef.onDestroy(() => this.headerActions.clear());
  }

  /** Literal keys, not `'hr.employee.filter.' + filter`: the catalogue test scans for the literal string. */
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

  protected onClearFilters(): void {
    this.query.set('');
    this.babFilter.set('');
  }

  protected trackEntry(_index: number, entry: Entry): string {
    return entry.type === 'row' ? entry.row.id : `restored-${entry.id}`;
  }

  protected babLabel(bab: { readonly nameAr: string; readonly nameEn: string }): string {
    return this.i18n.locale() === 'en' ? bab.nameEn : bab.nameAr;
  }

  protected rowBab(row: EmployeeSummary): string {
    return (this.i18n.locale() === 'en' ? row.babNameEn : row.babNameAr) ?? '';
  }

  /** `2022-03-12` to `12/03/2022`, Western digits like every other figure. */
  protected hired(row: EmployeeSummary): string {
    const parts = row.hiredOn?.split('-');
    return parts?.length === 3 ? `${parts[2]}/${parts[1]}/${parts[0]}` : '';
  }

  protected ratingText(row: EmployeeSummary): string {
    return row.averageRating === null ? '' : `${Number(row.averageRating).toFixed(1)}/5`;
  }

  protected timesText(row: EmployeeSummary): string {
    const times = row.timesEngaged ?? 0;
    return times === 0
      ? this.i18n.t('hr.worker.pool.never_engaged')
      : times === 1
        ? this.i18n.t('people.worker.engaged_once')
        : this.i18n.t('people.worker.engaged_times', { count: times });
  }

  protected archiveQuestion(row: EmployeeSummary): string {
    return this.i18n.t('people.archive.question', { name: row.fullName });
  }

  protected restoredText(name: string): string {
    return this.i18n.t('people.restored', { name });
  }

  protected rowMenuItems(row: EmployeeSummary): readonly RowMenuItem[] {
    return [
      { labelKey: 'action.edit', action: 'edit', icon: 'pen' },
      row.isActive
        ? { labelKey: 'people.menu.archive', action: 'archive', icon: 'arch', separatorBefore: true }
        : { labelKey: 'people.menu.unarchive', action: 'unarchive', icon: 'unarch', separatorBefore: true },
    ];
  }

  protected async onRowMenuSelected(row: EmployeeSummary, action: string): Promise<void> {
    switch (action) {
      case 'edit':
        await this.onOpen(row);
        break;
      case 'archive':
        this.onStartArchive(row.id);
        break;
      case 'unarchive':
        await this.onUnarchive(row);
        break;
    }
  }

  protected onCreate(): void {
    this.clearRowState();
    this.panelTarget.set({ mode: 'create' });
  }

  /** Opens the panel on the person's full file: the list row has no national id. */
  protected async onOpen(row: EmployeeSummary): Promise<void> {
    this.clearRowState();
    try {
      this.panelTarget.set({ mode: 'edit', file: await this.api.get(row.id), summary: row });
    } catch (error) {
      this.rowFailure.set({ id: row.id, key: toProblem(error).messageKey });
    }
  }

  protected onPanelClosed(): void {
    this.panelTarget.set(null);
  }

  protected async onPanelSaved(file: { readonly id: string }): Promise<void> {
    this.panelTarget.set(null);
    this.justSavedId.set(file.id);
    await this.reload(this.kind());
  }

  protected onPanelArchive(): void {
    const target = this.panelTarget();
    this.panelTarget.set(null);
    if (target?.mode === 'edit') {
      this.onStartArchive(target.file.id);
    }
  }

  protected async onPanelUnarchive(): Promise<void> {
    const target = this.panelTarget();
    if (target?.mode === 'edit') {
      this.panelTarget.set(null);
      await this.onUnarchive(target.summary);
    }
  }

  protected onStartArchive(id: string): void {
    this.clearRowState();
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
      await this.reload(this.kind());
    } catch (error) {
      // Surfaced, not swallowed: including the "already archived" race.
      this.rowFailure.set({ id, key: toProblem(error).messageKey });
      this.confirmingArchiveId.set(null);
    } finally {
      this.archivingId.set(null);
    }
  }

  /** Restores in place. Refusals (`not_archived`, a salaried phone now taken) stay on the row. */
  protected async onUnarchive(row: EmployeeSummary): Promise<void> {
    this.clearRowState();
    const index = this.rows().findIndex((r) => r.id === row.id);
    try {
      await this.api.unarchive(row.id);
      this.restored.set({ id: row.id, name: row.fullName, index: Math.max(index, 0) });
      await this.reload(this.kind());
    } catch (error) {
      this.rowFailure.set({ id: row.id, key: toProblem(error).messageKey });
    }
  }

  protected async onRetry(): Promise<void> {
    await this.reload(this.kind());
  }

  private clearRowState(): void {
    this.rowFailure.set(null);
    this.confirmingArchiveId.set(null);
    this.restored.set(null);
    this.justSavedId.set(null);
  }

  private async reload(kind: EmployeeKind): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      const rows = await this.api.list(this.statusFilter(), kind);
      // A late answer for the list the user has already left must not overwrite the one now shown.
      if (kind === this.kind()) {
        this.rows.set(rows);
      }
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.rows.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadBabs(): Promise<void> {
    try {
      this.allBabs.set(await this.api.listBabOptions());
    } catch {
      // The panel's select and the filter stay empty; the list itself still works.
      this.allBabs.set([]);
    }
  }
}
