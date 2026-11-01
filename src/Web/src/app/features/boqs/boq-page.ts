import { ChangeDetectionStrategy, Component, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../core/api/problem-details';
import { Boq, BoqsApi } from '../../core/boqs/boqs.api';
import { I18nService } from '../../core/i18n/i18n.service';
import { KaffBadge } from '../../shared/kaff-badge/kaff-badge';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffIcon } from '../../shared/kaff-icon/kaff-icon';
import { KaffProjectLinks } from '../../shared/kaff-project-links/kaff-project-links';
import { KaffState } from '../../shared/kaff-state/kaff-state';

/**
 * The project's BOQ, read-only. It shows cost-bearing data, so the server gates it `ProjectManage`; this
 * page never decides who may read and has no edit control. Lines are the frozen copy the contract was
 * signed on.
 */
@Component({
  selector: 'kaff-boq-page',
  imports: [RouterLink, KaffBadge, KaffButton, KaffIcon, KaffProjectLinks, KaffState],
  templateUrl: './boq-page.html',
  styleUrl: './boq-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BoqPage {
  private readonly api = inject(BoqsApi);

  protected readonly i18n = inject(I18nService);

  /** Bound from `:projectId` by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();

  protected readonly boq = signal<Boq | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  constructor() {
    // A required input is set after construction: read it in an effect, reload untracked (see ExtractListPage).
    effect(() => {
      this.projectId();
      untracked(() => void this.reload());
    });
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      this.boq.set(await this.api.get(this.projectId()));
    } catch (error) {
      this.boq.set(null);
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected day(value: string): string {
    const [year = 0, month = 1, day = 1] = value.split('-').map(Number);
    return this.i18n.formatDate(new Date(year, month - 1, day), { dateStyle: 'medium' });
  }

  protected quantity(value: string | null): string {
    return value === null ? '-' : this.i18n.formatNumber(value);
  }

  protected amount(value: string | null): string {
    return value === null ? '-' : this.i18n.formatAmount(value);
  }
}
