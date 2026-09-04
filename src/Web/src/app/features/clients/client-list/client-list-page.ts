import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormField, form } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import {
  ClientListFilter,
  ClientSummary,
  ClientsApi,
} from '../../../core/clients/clients.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { clientKindKey } from '../../../core/i18n/enum-keys';

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
  imports: [FormField, RouterLink],
  templateUrl: './client-list-page.html',
  styleUrl: './client-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClientListPage {
  private readonly api = inject(ClientsApi);

  protected readonly i18n = inject(I18nService);
  protected readonly clientKindKey = clientKindKey;
  protected readonly filters = FILTERS;

  private readonly searchModel = signal<{ query: string }>({ query: '' });

  protected readonly searchForm = form(this.searchModel);
  protected readonly filter = signal<ClientListFilter>('active');
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

  constructor() {
    void this.reload();
  }

  protected async onSubmitSearch(): Promise<void> {
    await this.reload();
  }

  protected async onFilter(filter: ClientListFilter): Promise<void> {
    this.filter.set(filter);
    await this.reload();
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

    const applied = this.searchModel().query.trim();

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
