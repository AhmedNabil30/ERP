import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';
import { ExtractSummary, ExtractsApi } from '../../../core/extracts/extracts.api';
import { roleKey } from '../../../core/i18n/enum-keys';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffProjectFrame } from '../../../shared/kaff-project-frame/kaff-project-frame';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';
import { EXTRACT_STATUS_PILL, WAITING_STEP_FALLBACK_KEY, WAITING_STEP_KEY } from '../extract-display';
import { STATUS_KEYS } from '../extract-detail/extract-detail-page';

/** The extracts of one project, oldest first. The server decides who may read the list. */
@Component({
  selector: 'kaff-extract-list-page',
  imports: [RouterLink, KaffButton, KaffFigure, KaffProjectFrame, KaffState, KaffStatusPill, KaffTable, KaffTableRow],
  templateUrl: './extract-list-page.html',
  styleUrl: './extract-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtractListPage {
  private readonly api = inject(ExtractsApi);

  protected readonly i18n = inject(I18nService);
  protected readonly statusKeys = STATUS_KEYS;
  protected readonly pill = EXTRACT_STATUS_PILL;

  private readonly auth = inject(AuthService);

  /** Mirrors the server's `ExtractViewer.MaySeeMoney`. Convenience only: the server withholds `money`. */
  private static readonly MONEY_PERMISSIONS = ['ExtractDocumentAssemble', 'ExtractDeductionsApply', 'FinancialMovementApprove'];

  /** Bound from `:projectId` by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();

  protected readonly rows = signal<readonly ExtractSummary[]>([]);

  /**
   * The money columns exist only for a session that holds an extract-money permission on this project AND
   * only if the server sent money. A site engineer's list has neither: the columns are absent, not blank,
   * even if a payload carried figures it should not have.
   */
  protected readonly showMoney = computed(() => {
    const entry = this.auth.current()?.projects.find((p) => p.projectId === this.projectId());
    const mayRead = entry?.permissions.some((p) => ExtractListPage.MONEY_PERMISSIONS.includes(p)) ?? false;
    return mayRead && this.rows().some((row) => row.money !== null);
  });

  protected readonly columns = computed(() =>
    this.showMoney()
      ? 'minmax(5.5rem, 0.7fr) minmax(9rem, 1.2fr) 8rem minmax(10rem, 1.6fr) repeat(3, minmax(7rem, 1fr))'
      : 'minmax(10rem, 1fr) minmax(12rem, 2fr) 11rem minmax(14rem, 2fr)',
  );
  /** Below this frame width the table scrolls inside itself rather than squeezing seven columns. */
  protected readonly minInline = computed(() => (this.showMoney() ? 1000 : null));

  protected readonly headerColumns = computed<readonly TableColumnDef[]>(() => [
    { labelKey: 'extract.list.column.number', pinned: true },
    { labelKey: 'extract.list.column.period' },
    { labelKey: 'extract.list.column.status' },
    { labelKey: 'extract.list.column.waiting' },
    ...(this.showMoney()
      ? [
          { labelKey: 'extract.net_payable', align: 'end' as const },
          { labelKey: 'projects.workspace.pay.collected', align: 'end' as const },
          { labelKey: 'projects.workspace.pay.outstanding', align: 'end' as const },
        ]
      : []),
  ]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  constructor() {
    // `projectId` is a required input, set AFTER construction: reading it here throws NG0950, which the
    // catch in reload() turned into the unexpected-error state on first load. An effect reads it once it
    // exists and reloads if the route moves to another project. reload() runs untracked so its own signal
    // writes do not re-trigger the effect.
    effect(() => {
      this.projectId();
      untracked(() => void this.reload());
    });
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

  /** "Waiting for <step> from <roles>", the workspace strip's own words. Null: nothing waits on anyone. */
  protected waitingText(row: ExtractSummary): string | null {
    const w = row.waitingOn;
    if (w.isTerminal) {
      return null;
    }
    const who = w.roles.map((r) =>
      this.i18n.t(r.role === 'SiteEngineer' && r.minimumLevel === 'Supervisor' ? 'projects.workspace.waiting.supervisor' : roleKey(r.role)),
    );
    return this.i18n.t('extract.list.waiting', {
      step: this.i18n.t(WAITING_STEP_KEY[w.status] ?? WAITING_STEP_FALLBACK_KEY),
      who: new Intl.ListFormat(this.i18n.locale(), { style: 'long', type: 'disjunction' }).format(who),
    });
  }

  protected day(value: string): string {
    const [year = 0, month = 1, day = 1] = value.split('-').map(Number);
    return this.i18n.formatDate(new Date(year, month - 1, day), { dateStyle: 'medium' });
  }
}
