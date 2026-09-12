import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { I18nService } from '../../../core/i18n/i18n.service';
import { SupplierListFilter, SupplierSummary, SuppliersApi } from '../../../core/suppliers/suppliers.api';

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
 */
@Component({
  selector: 'kaff-supplier-list-page',
  imports: [RouterLink],
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

  readonly status = input<SupplierListFilter>('active');
  protected readonly statusFilter = computed(() => this.status() ?? 'active');

  protected readonly suppliers = signal<readonly SupplierSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  private readonly archivingId = signal<string | null>(null);
  protected readonly confirmingArchiveId = signal<string | null>(null);
  protected readonly rowFailure = signal<{ readonly id: string; readonly key: string } | null>(null);

  constructor() {
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
      this.suppliers.set(await this.api.list(this.statusFilter()));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.suppliers.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
