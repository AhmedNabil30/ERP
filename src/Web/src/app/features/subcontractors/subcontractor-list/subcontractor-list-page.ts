import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { BabsApi, Bab } from '../../../core/catalogue/babs.api';
import { toProblem } from '../../../core/api/problem-details';
import { I18nService } from '../../../core/i18n/i18n.service';
import { fractionToPercent } from '../../../core/catalogue/percent-wire';
import {
  SubcontractorListFilter,
  SubcontractorSummary,
  SubcontractorsApi,
} from '../../../core/subcontractors/subcontractors.api';

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
 */
@Component({
  selector: 'kaff-subcontractor-list-page',
  imports: [RouterLink],
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

  /** Bound by name from `?status=` — `withComponentInputBinding` in `app.config.ts`. */
  readonly status = input<SubcontractorListFilter>('active');
  protected readonly statusFilter = computed(() => this.status() ?? 'active');

  protected readonly subcontractors = signal<readonly SubcontractorSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  private readonly babs = signal<readonly Bab[]>([]);

  private readonly archivingId = signal<string | null>(null);
  protected readonly confirmingArchiveId = signal<string | null>(null);
  protected readonly rowFailure = signal<{ readonly id: string; readonly key: string } | null>(null);

  constructor() {
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

  /** `0.05` -> `"5"` — a percent an operator reads, never the bare fraction (AC-211-C). */
  protected retentionLabel(subcontractor: SubcontractorSummary): string {
    return fractionToPercent(String(subcontractor.retentionRate));
  }

  protected trackRow(_index: number, subcontractor: SubcontractorSummary): string {
    return subcontractor.id;
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
      this.subcontractors.set(await this.api.list(this.statusFilter()));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.subcontractors.set([]);
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
