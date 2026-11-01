import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { ExtractSummary, ExtractsApi } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { STATUS_KEYS } from '../extract-detail/extract-detail-page';

/** The extracts of one project, oldest first. The server decides who may read the list. */
@Component({
  selector: 'kaff-extract-list-page',
  imports: [RouterLink, KaffBadge, KaffButton, KaffIcon, KaffState],
  templateUrl: './extract-list-page.html',
  styleUrl: './extract-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtractListPage {
  private readonly api = inject(ExtractsApi);

  protected readonly i18n = inject(I18nService);
  protected readonly statusKeys = STATUS_KEYS;

  /** Bound from `:projectId` by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();

  protected readonly rows = signal<readonly ExtractSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  constructor() {
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      this.rows.set(await this.api.list(this.projectId()));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected day(value: string): string {
    const [year = 0, month = 1, day = 1] = value.split('-').map(Number);
    return this.i18n.formatDate(new Date(year, month - 1, day), { dateStyle: 'medium' });
  }
}
