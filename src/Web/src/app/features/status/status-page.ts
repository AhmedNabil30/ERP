import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { I18nService } from '../../core/i18n/i18n.service';
import { toProblem } from '../../core/api/problem-details';

interface HealthResponse {
  readonly status: string;
  readonly databaseReachable: boolean;
  readonly guardsInstalled: boolean;
  readonly missingGuards: readonly string[];
}

type State =
  | { readonly kind: 'loading' }
  | { readonly kind: 'ready'; readonly health: HealthResponse }
  | { readonly kind: 'error'; readonly messageKey: string };

/**
 * The one page slice 0 ships.
 *
 * It proves the whole stack is connected — Angular reaches the API, the API reaches PostgreSQL, and
 * the database guards are installed — and gives the E2E smoke test something real to assert. Feature
 * screens arrive with their slices, after the UX agent's flows are approved.
 *
 * The state is one discriminated union narrowed into computed signals, so the template stays free of
 * `$any` and keeps working under `strictTemplates`.
 */
@Component({
  selector: 'kaff-status-page',
  templateUrl: './status-page.html',
  styleUrl: './status-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatusPage {
  private readonly http = inject(HttpClient);
  private readonly state = signal<State>({ kind: 'loading' });

  protected readonly i18n = inject(I18nService);

  protected readonly isLoading = computed(() => this.state().kind === 'loading');

  protected readonly health = computed<HealthResponse | null>(() => {
    const state = this.state();
    return state.kind === 'ready' ? state.health : null;
  });

  protected readonly errorKey = computed<string | null>(() => {
    const state = this.state();
    return state.kind === 'error' ? state.messageKey : null;
  });

  constructor() {
    void this.refresh();
  }

  protected async refresh(): Promise<void> {
    this.state.set({ kind: 'loading' });

    try {
      const health = await firstValueFrom(this.http.get<HealthResponse>('/api/health'));
      this.state.set({ kind: 'ready', health });
    } catch (error: unknown) {
      this.state.set({ kind: 'error', messageKey: toProblem(error).messageKey });
    }
  }
}
