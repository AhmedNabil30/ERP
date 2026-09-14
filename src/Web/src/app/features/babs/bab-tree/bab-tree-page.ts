import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { Bab, BabListFilter, BabsApi } from '../../../core/catalogue/babs.api';
import { flattenBabTree } from '../../../core/catalogue/bab-tree';
import { fractionToPercent } from '../../../core/catalogue/percent-wire';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import {
  KaffSegmentedFilter,
  SegmentedFilterOption,
} from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffTableHeader, TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

/** The three chips `KAFF-213` rule 9 draws, in the order it draws them — matches `CatalogueListPage`. */
const FILTERS: readonly BabListFilter[] = ['active', 'archived', 'all'];

/**
 * `S-021` · the باب tree — `KAFF-204` (create/edit link out, markup), `KAFF-205` (re-parent),
 * `KAFF-213` (archive). One flat list from `GET /api/babs`, nested client-side by `flattenBabTree`
 * (`KAFF-204` rule 6: "roughly 40 trades" is small enough that no server-side tree endpoint exists,
 * or is needed).
 *
 * **Re-parent and archive both happen from the row**, the same shape `CatalogueListPage` uses for
 * archive/un-archive — there is no separate confirmation screen for either.
 */
@Component({
  selector: 'kaff-bab-tree-page',
  imports: [RouterLink, KaffBadge, KaffButton, KaffSegmentedFilter, KaffTableHeader, KaffTableRow],
  templateUrl: './bab-tree-page.html',
  styleUrl: './bab-tree-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BabTreePage {
  private readonly api = inject(BabsApi);

  protected readonly i18n = inject(I18nService);
  protected readonly filters = FILTERS;

  /** Grid columns for `kaff-table-row`/`kaff-table-header`: code · name · markup · archived badge. */
  protected readonly rowColumns = 'auto minmax(10rem, 1fr) auto auto';

  /** `KAFF-925`: same column order the rows use. */
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'bab.field.code' },
    { labelKey: 'bab.column.name' },
    { labelKey: 'bab.field.default_markup', align: 'end' },
    {},
  ];

  protected readonly segmentedOptions: readonly SegmentedFilterOption<BabListFilter>[] = FILTERS.map(
    (filter) => ({ value: filter, labelKey: 'bab.filter.' + filter }),
  );

  protected readonly statusFilter = signal<BabListFilter>('active');
  protected readonly babs = signal<readonly Bab[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  protected readonly rows = computed(() => flattenBabTree(this.babs()));

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

  constructor() {
    effect(() => {
      void this.statusFilter();
      void this.reload();
    });
  }

  protected async onFilter(filter: BabListFilter): Promise<void> {
    this.statusFilter.set(filter);
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

  protected trackRow(_index: number, row: { readonly bab: Bab }): string {
    return row.bab.id;
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
