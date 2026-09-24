import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  TemplateRef,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';

import { toProblem } from '../../../core/api/problem-details';
import {
  DepartmentListFilter,
  DepartmentSummary,
  DepartmentsApi,
} from '../../../core/departments/departments.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffRowMenu, RowMenuItem } from '../../../shared/kaff-row-menu/kaff-row-menu';
import {
  KaffSegmentedFilter,
  SegmentedFilterOption,
} from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

const FILTERS: readonly DepartmentListFilter[] = ['active', 'archived', 'all'];

interface DepartmentDraft {
  nameAr: string;
  nameEn: string;
}

const EMPTY_DRAFT: DepartmentDraft = { nameAr: '', nameEn: '' };

/**
 * The department settings screen. KAFF-321, decisions.md D-162 (`Q85`).
 *
 * **Add, edit, archive and unarchive — all from one flat list**, the same shape `CatalogueListPage`
 * uses for catalogue items, simplified because a department carries no code, no markup and no tree
 * (D-162 names only two fields: Arabic and English names). Archive is the only retirement act — no
 * delete exists (Nabil's ruling 2026-09-15, decisions.md); unarchive is its direct reverse (AC-321-F).
 *
 * **KAFF-931**: the row's `edit`/`archive`/`unarchive` buttons collapse into one `kaff-row-menu`
 * (danger is text, never a filled button). Loading/empty/refusal render through `kaff-state`. No
 * `kaff-detail-panel`: the row's own in-place two-field edit (`editingId`/`editDraft` below) already is
 * this screen's quick-edit and does not fit `kaff-inline-edit` (one scalar, not two fields at once), so
 * it is left as the row-level form it already was.
 */
@Component({
  selector: 'kaff-department-settings-page',
  imports: [
    KaffBadge,
    KaffButton,
    KaffField,
    KaffRowMenu,
    KaffSegmentedFilter,
    KaffState,
    KaffTable,
    KaffTableRow,
  ],
  templateUrl: './department-settings-page.html',
  styleUrl: './department-settings-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DepartmentSettingsPage {
  private readonly api = inject(DepartmentsApi);

  protected readonly i18n = inject(I18nService);
  protected readonly filters = FILTERS;

  /** `KAFF-928`: the trailing `auto` track was the one flagged in this story — fixed at `18rem`,
   *  wide enough for the archive-confirm/cancel pair plus the inactive badge (the archive-confirm flow
   *  stays inline; `kaff-row-menu` is narrower, `KAFF-931`), the widest combination this column holds. */
  protected readonly rowColumns = 'minmax(10rem, 1fr) minmax(10rem, 1fr) 18rem';

  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'department.column.name_ar' },
    { labelKey: 'department.column.name_en' },
    {},
  ];

  protected readonly segmentedOptions: readonly SegmentedFilterOption<DepartmentListFilter>[] =
    FILTERS.map((filter) => ({ value: filter, labelKey: 'department.filter.' + filter }));

  protected readonly statusFilter = signal<DepartmentListFilter>('active');
  protected readonly departments = signal<readonly DepartmentSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  protected readonly creating = signal(false);
  protected readonly createDraft = signal<DepartmentDraft>(EMPTY_DRAFT);
  protected readonly createFailure = signal<string | null>(null);
  protected readonly savingCreate = signal(false);

  protected readonly editingId = signal<string | null>(null);
  protected readonly editDraft = signal<DepartmentDraft>(EMPTY_DRAFT);
  protected readonly editFailure = signal<string | null>(null);
  protected readonly savingEdit = signal(false);

  protected readonly confirmingArchiveId = signal<string | null>(null);
  protected readonly archiveFailure = signal<{ readonly id: string; readonly key: string } | null>(
    null,
  );
  protected readonly archivingId = signal<string | null>(null);

  /**
   * KAFF-929 finding 4: `ListDepartments` carries no separate total — the count of the current
   * filter's view, read from the response already held in {@link departments} rather than a call
   * added for it.
   */
  protected readonly countText = computed(() =>
    this.i18n.t('department.settings.count', { count: this.departments().length }),
  );

  /** KAFF-929 finding 2, matching KAFF-926's `UserListPage`. The create action was already a filled
   *  `kaff-button`, just placed at the bottom of the page — this moves it into the top-bar slot,
   *  `onStartCreate` unchanged. */
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

  protected onFilter(filter: DepartmentListFilter): void {
    this.statusFilter.set(filter);
  }

  protected departmentLabel(department: DepartmentSummary): string {
    return this.i18n.locale() === 'en' ? department.nameEn : department.nameAr;
  }

  protected trackRow(_index: number, department: DepartmentSummary): string {
    return department.id;
  }

  // ---- create ---------------------------------------------------------------------------------

  protected onStartCreate(): void {
    this.createFailure.set(null);
    this.createDraft.set(EMPTY_DRAFT);
    this.creating.set(true);
  }

  protected onCancelCreate(): void {
    this.creating.set(false);
  }

  protected onCreateNameArInput(event: Event): void {
    const value = readInput(event);
    this.createDraft.update((current) => ({ ...current, nameAr: value }));
  }

  protected onCreateNameEnInput(event: Event): void {
    const value = readInput(event);
    this.createDraft.update((current) => ({ ...current, nameEn: value }));
  }

  protected async onConfirmCreate(): Promise<void> {
    this.createFailure.set(null);
    this.savingCreate.set(true);

    try {
      const draft = this.createDraft();
      await this.api.create({ nameAr: draft.nameAr.trim(), nameEn: draft.nameEn.trim() });
      this.creating.set(false);
      await this.reload();
    } catch (error) {
      this.createFailure.set(toProblem(error).messageKey);
    } finally {
      this.savingCreate.set(false);
    }
  }

  // ---- edit -----------------------------------------------------------------------------------

  protected onStartEdit(department: DepartmentSummary): void {
    this.editFailure.set(null);
    this.editDraft.set({ nameAr: department.nameAr, nameEn: department.nameEn });
    this.editingId.set(department.id);
  }

  protected onCancelEdit(): void {
    this.editingId.set(null);
  }

  protected onEditNameArInput(event: Event): void {
    const value = readInput(event);
    this.editDraft.update((current) => ({ ...current, nameAr: value }));
  }

  protected onEditNameEnInput(event: Event): void {
    const value = readInput(event);
    this.editDraft.update((current) => ({ ...current, nameEn: value }));
  }

  protected async onConfirmEdit(id: string): Promise<void> {
    this.editFailure.set(null);
    this.savingEdit.set(true);

    try {
      const draft = this.editDraft();
      await this.api.edit(id, { nameAr: draft.nameAr.trim(), nameEn: draft.nameEn.trim() });
      this.editingId.set(null);
      await this.reload();
    } catch (error) {
      this.editFailure.set(toProblem(error).messageKey);
    } finally {
      this.savingEdit.set(false);
    }
  }

  // ---- archive ----------------------------------------------------------------------------------

  protected isArchiving(id: string): boolean {
    return this.archivingId() === id;
  }

  protected rowMenuItems(department: DepartmentSummary): readonly RowMenuItem[] {
    const items: RowMenuItem[] = [{ labelKey: 'action.edit', action: 'edit' }];
    if (department.isActive) {
      items.push({ labelKey: 'department.settings.archive_action', action: 'archive', tone: 'danger' });
    } else {
      items.push({
        labelKey: 'department.settings.unarchive_action',
        action: 'unarchive',
        disabled: this.isArchiving(department.id),
      });
    }
    return items;
  }

  protected onRowMenuSelected(department: DepartmentSummary, action: string): void {
    switch (action) {
      case 'edit':
        this.onStartEdit(department);
        break;
      case 'archive':
        this.onStartArchive(department.id);
        break;
      case 'unarchive':
        void this.onUnarchive(department.id);
        break;
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
      this.archiveFailure.set({ id, key: toProblem(error).messageKey });
    } finally {
      this.archivingId.set(null);
    }
  }

  // ---- unarchive ----------------------------------------------------------------------------------

  protected async onUnarchive(id: string): Promise<void> {
    this.archiveFailure.set(null);
    this.archivingId.set(id);

    try {
      await this.api.unarchive(id);
      await this.reload();
    } catch (error) {
      this.archiveFailure.set({ id, key: toProblem(error).messageKey });
    } finally {
      this.archivingId.set(null);
    }
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);

    try {
      this.departments.set(await this.api.list(this.statusFilter()));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.departments.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}

/**
 * The typed value of an `<input>`, read from the event rather than through `$any` in the template —
 * `ux/rtl-and-i18n.md` §6 hard rule 4, Nabil 2026-08-28.
 */
function readInput(event: Event): string {
  const target = event.target;
  return target instanceof HTMLInputElement ? target.value : '';
}
