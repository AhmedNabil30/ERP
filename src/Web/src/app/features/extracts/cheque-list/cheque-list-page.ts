import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { ChainApi, Cheque } from '../../../core/extracts/chain.api';
import { ProjectDetail, ProjectsApi } from '../../../core/projects/projects.api';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';
import { ChainPage } from '../chain-page';
import { CHEQUE_PILL, shortDay } from '../extract-display';

const DAY_MS = 86_400_000;

/**
 * E7, «الشيكات تحت التحصيل» (`extracts.html#e7`), a sub-section of the extracts tab (D-241 item 3): the cheques in
 * hand, oldest first. Finance's rows open the extract's payments (E6); the Owner reads the list (D-239 item 4) and
 * his rows open the extract (E3). No primary: each row is the way in.
 */
@Component({
  selector: 'kaff-cheque-list-page',
  imports: [RouterLink, KaffButton, KaffFigure, KaffPageHead, KaffState, KaffStatusPill, KaffTable, KaffTableRow],
  templateUrl: './cheque-list-page.html',
  styleUrls: ['../chain-page.css', '../extract-list/extract-list-page.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChequeListPage extends ChainPage {
  private readonly chain = inject(ChainApi);
  private readonly projects = inject(ProjectsApi);

  protected readonly pill = CHEQUE_PILL;
  protected readonly shortDay = shortDay;

  readonly projectId = input.required<string>();

  protected readonly rows = signal<readonly Cheque[]>([]);
  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  protected override readonly primaryTemplate = signal(undefined);
  protected override readonly hasPrimary = signal(false);

  /** Finance moves cheques; everyone else who reads this list reads the extract. */
  private readonly movesCheques = computed(() => this.held(this.projectId()).has('ExtractCollect'));

  protected readonly columns = 'minmax(7rem, 0.8fr) minmax(8rem, 0.8fr) minmax(8rem, 0.9fr) minmax(8rem, 0.8fr) minmax(6rem, 0.6fr) minmax(8rem, 1fr)';
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'extract.list.column.number', pinned: true },
    { labelKey: 'cheque.number' },
    { labelKey: 'extract.list.column.status' },
    { labelKey: 'cheque.received_on' },
    { labelKey: 'cheque.since' },
    { labelKey: 'cheque.amount_unit', align: 'end' },
  ];

  constructor() {
    super();
    effect(() => {
      this.projectId();
      untracked(() => void this.reload());
    });
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      const [rows, project] = await Promise.all([this.chain.cheques(this.projectId()), this.projects.get(this.projectId()).catch(() => null)]);
      this.rows.set(rows);
      this.project.set(project);
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  /** Where the row goes. A released-hold cheque has no extract and opens nothing. */
  protected link(row: Cheque): readonly string[] | null {
    const extractId = row.collection.extractId;
    if (extractId === null) {
      return null;
    }
    return this.movesCheques()
      ? ['/projects', this.projectId(), 'extracts', extractId, 'payments']
      : ['/projects', this.projectId(), 'extracts', extractId];
  }

  protected title(row: Cheque): string {
    return row.extractNumber === null ? this.i18n.t('cheque.released_hold') : this.i18n.t('extract.number', { number: row.extractNumber });
  }

  /** Days since the cheque last moved, in the language's own unit words. */
  protected age(row: Cheque, now: number = Date.now()): string {
    const days = Math.max(0, Math.floor((now - new Date(row.stateChangedAt).getTime()) / DAY_MS));
    return this.i18n.formatNumber(days, { style: 'unit', unit: 'day', unitDisplay: 'long' });
  }
}
