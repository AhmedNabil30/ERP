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
import { FormField, form } from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import {
  ClientListFilter,
  ClientSummary,
  ClientsApi,
} from '../../../core/clients/clients.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { clientKindKey } from '../../../core/i18n/enum-keys';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffSegmentedFilter, SegmentedFilterOption } from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

/** The three chips `S-011` draws, in the order it draws them. */
const FILTERS: readonly ClientListFilter[] = ['all', 'active', 'archived'];

/**
 * S-011 · the client list. Marketing's home.
 *
 * **The filtering is the server's, all three states of it.** `?status=all|active|archived` — the
 * chips send the filter and render what comes back. Filtering a list client-side that the server
 * already filtered is a list that lies the moment there is more than one page of clients, and the
 * boolean this endpoint originally shipped could not express "archived alone" at all (decisions.md
 * D-111 §3, which is the change that made this screen buildable as drawn).
 *
 * **The search box sends what was typed.** `S-011` in as many words: *"Send the raw query and let the
 * server normalise; do not normalise in the client and create a second implementation of
 * `PhoneNumber.Normalise`."* A name, a code and a phone in any of three formats all go the same way.
 *
 * **Two empty states, not one.** `clients.empty.*` when there are no clients at all and
 * `clients.empty.filtered.*` when a search matched none — spec.md §4.5's principle applied
 * consistently: an explicit empty state, never a blank area and never a phantom row. They are
 * genuinely different facts and telling an operator "no clients" when they mistyped a phone number is
 * how a duplicate gets created.
 */
@Component({
  selector: 'kaff-client-list-page',
  imports: [
    FormField,
    RouterLink,
    KaffBadge,
    KaffButton,
    KaffSegmentedFilter,
    KaffState,
    KaffTable,
    KaffTableRow,
  ],
  templateUrl: './client-list-page.html',
  styleUrl: './client-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClientListPage {
  private readonly api = inject(ClientsApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly i18n = inject(I18nService);
  protected readonly clientKindKey = clientKindKey;
  protected readonly filters = FILTERS;

  /** Grid columns for `kaff-table`: code · name · kind · phone · archived badge. `KAFF-928`: fixed
   *  sizes plus one `minmax(_, 1fr)` for name, the one column whose content length varies. Code and
   *  phone are fixed-width figures, kind is a short badge word, and the trailing column holds only
   *  the archived badge. */
  protected readonly rowColumns = '8rem minmax(10rem, 1fr) 7rem 9rem 7rem';

  /** `KAFF-925`: same column order the rows use. */
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'clients.field.code' },
    { labelKey: 'clients.field.name' },
    { labelKey: 'clients.field.kind' },
    { labelKey: 'clients.field.phone' },
    {},
  ];

  protected readonly segmentedOptions: readonly SegmentedFilterOption<ClientListFilter>[] = FILTERS.map(
    (filter) => ({ value: filter, labelKey: this.filterKey(filter) }),
  );

  private readonly searchModel = signal<{ query: string }>({ query: '' });

  protected readonly searchForm = form(this.searchModel);

  /**
   * KAFF-926 finding 7: bound by name from `?status=` — `withComponentInputBinding` (`app.config.ts`),
   * the same mechanism `catalogue-list-page.ts`'s `status` input uses. This screen was found to hold
   * the filter in a plain signal with no URL sync at all, unlike `catalogue-list-page.ts`; a reload or
   * a shared link lost the filter. `onFilter` below writes the choice to the URL instead of to a
   * signal directly, and `filter` reads it back.
   */
  readonly status = input<ClientListFilter>('active');
  protected readonly filter = computed(() => this.status() ?? 'active');

  /** KAFF-931: `?search=`, merged with `?status=` — the URL-held pattern `catalogue-list-page.ts`
   *  already uses for both of its own filters. Read through `searchTerm()`, never directly: an absent
   *  query param binds `undefined`, not the `input()` default (the same `catalogue-list-page.ts`
   *  finding). */
  readonly search = input<string>('');
  protected readonly searchTerm = computed(() => this.search() ?? '');

  protected readonly clients = signal<readonly ClientSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  /**
   * True only when the list is empty *and* nothing was asked for.
   *
   * Held as its own signal rather than derived from `search()` at render time, because `search()`
   * changes as the operator types and the empty state must describe the request that produced the
   * rows on screen — not the one being typed over it.
   */
  protected readonly searchWasApplied = signal(false);

  /**
   * KAFF-929 finding 4: `ListClients` carries no separate total — the count of the current filter's
   * view, read from the response already held in {@link clients} rather than a call added for it.
   */
  protected readonly countText = computed(() =>
    this.i18n.t('clients.list.count', { count: this.clients().length }),
  );

  /** KAFF-929 finding 2, matching KAFF-926's `UserListPage`. */
  private readonly createAction = viewChild<TemplateRef<unknown>>('createAction');
  private readonly headerActions = inject(HeaderActionsService);

  constructor() {
    // The draft box starts from whatever the URL already carries — read once, here, rather than kept
    // in sync by a reactive `effect()`. `catalogue-list-page.ts`'s constructor states the exact reason
    // an `effect()` may not call `searchModel.set(...)`: it reassigns the signal `form()` wraps from
    // outside the form system and corrupts `FormField`'s internal state mid-render.
    this.searchModel.set({ query: this.searchTerm() });

    // Reacts to `filter` and `searchTerm` (both URL-driven, so this also fires on back/forward and on
    // a shared link) and runs once immediately — the same shape `catalogue-list-page.ts`'s constructor
    // effect uses for its own URL-bound filter and search.
    effect(() => {
      void this.filter();
      void this.searchTerm();
      void this.reload();
    });

    effect(() => this.headerActions.set(this.createAction() ?? null));
    inject(DestroyRef).onDestroy(() => this.headerActions.set(null));
  }

  protected async onSubmitSearch(): Promise<void> {
    await this.navigate({ search: this.searchModel().query.trim() || null });
  }

  protected async onCreate(): Promise<void> {
    await this.router.navigate(['/clients/new']);
  }

  protected async onFilter(filter: ClientListFilter): Promise<void> {
    await this.navigate({ status: filter });
  }

  private async navigate(patch: Readonly<Record<string, string | null>>): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: patch,
      queryParamsHandling: 'merge',
    });
  }

  protected filterKey(filter: ClientListFilter): string {
    switch (filter) {
      case 'all':
        return 'clients.filter.all';
      case 'active':
        return 'clients.filter.active';
      case 'archived':
        return 'clients.filter.archived';
    }
  }

  protected trackClient(_index: number, client: ClientSummary): string {
    return client.id;
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);

    const applied = this.searchTerm().trim();

    try {
      this.clients.set(await this.api.list(applied, this.filter()));
      this.searchWasApplied.set(applied.length > 0);
    } catch (error) {
      // A 403 here is the mechanism working, not a bug — spec.md §9 makes the server the decider,
      // and a role without ClientManage reaching this route by URL is exactly what it decides.
      // The key is the server's; this file does not choose it.
      this.failure.set(toProblem(error).messageKey);
      this.clients.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
