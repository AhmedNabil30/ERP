import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
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
import { I18nService } from '../../../core/i18n/i18n.service';

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
 */
@Component({
  selector: 'kaff-catalogue-list-page',
  imports: [FormField, RouterLink],
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

  protected readonly searchTerm = computed(() => this.search() ?? '');
  protected readonly statusFilter = computed(() => this.status() ?? 'active');

  private readonly searchModel = signal<{ query: string }>({ query: '' });
  protected readonly searchForm = form(this.searchModel);

  protected readonly items = signal<readonly CatalogueItem[]>([]);
  protected readonly babs = signal<readonly Bab[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  private readonly archivingId = signal<string | null>(null);
  protected readonly confirmingArchiveId = signal<string | null>(null);
  protected readonly rowFailure = signal<{ readonly id: string; readonly key: string } | null>(null);

  protected readonly groups = computed<readonly CatalogueGroup[]>(() =>
    groupByBab(this.items(), this.babs()),
  );

  /**
   * True when the current view is narrower than "everything" — a search term, or a filter other than
   * the default `active`. Drives which of the two empty states renders (`AC-203-D`): "nothing matches"
   * is a different fact from "the catalogue is empty", and conflating them is how an operator gets told
   * there is nothing to find when they mistyped a code.
   */
  protected readonly isFiltered = computed(
    () => this.searchTerm().trim().length > 0 || this.statusFilter() !== 'active',
  );

  constructor() {
    void this.loadBabs();

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
