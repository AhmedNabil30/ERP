import { ChangeDetectionStrategy, Component, TemplateRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';

import { toProblem } from '../../../core/api/problem-details';
import { Adjustment, ChainApi } from '../../../core/extracts/chain.api';
import { Extract, ExtractsApi } from '../../../core/extracts/extracts.api';
import { ProjectDetail, ProjectsApi } from '../../../core/projects/projects.api';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { FigureRow, KaffFigureList } from '../../../shared/kaff-figure-list/kaff-figure-list';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffWaitingLine } from '../../../shared/kaff-waiting-line/kaff-waiting-line';
import { ChainPage } from '../chain-page';
import { CREDIT_NOTE_PILL, lineText, shortDay } from '../extract-display';

/**
 * E10, «إشعار دائن» (`extracts.html#e10`): the Owner decides a credit note Finance raised; Finance reads it. Each
 * decision hangs off the read's `actions` flag (D-247 item 3), which already excludes the raiser (separation of duties)
 * and anyone without the Owner's permission. The server checks again on approval and rejection.
 */
@Component({
  selector: 'kaff-credit-note-page',
  imports: [KaffBanner, KaffButton, KaffDetailPanel, KaffField, KaffFigure, KaffFigureList, KaffPageHead, KaffState, KaffStatusPill, KaffWaitingLine],
  templateUrl: './credit-note-page.html',
  styleUrl: '../chain-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreditNotePage extends ChainPage {
  private readonly api = inject(ExtractsApi);
  private readonly chain = inject(ChainApi);
  private readonly projects = inject(ProjectsApi);

  protected readonly pill = CREDIT_NOTE_PILL;
  protected readonly shortDay = shortDay;

  readonly projectId = input.required<string>();
  readonly extractId = input.required<string>();
  readonly noteId = input.required<string>();

  protected readonly note = signal<Adjustment | null>(null);
  protected readonly extract = signal<Extract | null>(null);
  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly rejecting = signal(false);
  protected readonly reason = signal('');
  protected readonly reasonError = signal<string | null>(null);

  /** The server's answer: the caller may approve now. */
  protected readonly mayApprove = computed(() => this.note()?.actions?.approve === true);
  protected readonly mayReject = computed(() => this.note()?.actions?.reject === true);

  /** The caller raised it: the decision is someone else's (the separation-of-duties line). */
  protected readonly raisedByMe = computed(() => this.note()?.status === 'Raised' && this.note()?.raisedByUserId === this.auth.current()?.userId);

  protected override readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');
  protected override readonly hasPrimary = computed(() => this.mayApprove() && !this.rejecting());

  protected readonly name = computed(() => {
    const x = this.extract();
    return x === null ? this.i18n.t('extract.list.title') : this.i18n.t('extract.number', { number: x.number });
  });

  protected readonly waiting = computed(() => (this.note()?.status === 'Raised' ? lineText(this.i18n, this.note()?.waitingOn ?? null) : null));

  protected readonly facts = computed<FigureRow[]>(() => {
    const n = this.note();
    const x = this.extract();
    if (n === null) {
      return [];
    }
    const rows: FigureRow[] = [];
    if (n.disputeReason) rows.push({ label: this.i18n.t('dispute.client_claim'), text: n.disputeReason });
    rows.push({ label: this.i18n.t('credit_note.reason'), text: n.reason });
    if (x?.document) rows.push({ label: this.i18n.t('credit_note.net_unit'), amount: x.document.printedTotal });
    // The due figure after the note is the server's, so it is shown once the note is approved and has lowered it.
    if (x?.collection && n.status === 'Approved') rows.push({ label: this.i18n.t('credit_note.due_after_unit'), amount: x.collection.expected });
    rows.push({ label: this.i18n.t('credit_note.raised_on'), text: shortDay(n.raisedAt) });
    return rows;
  });

  constructor() {
    super();
    effect(() => {
      this.projectId();
      this.extractId();
      this.noteId();
      untracked(() => void this.reload());
    });
  }

  protected async reload(): Promise<void> {
    this.loading.set(this.note() === null);
    this.failure.set(null);
    try {
      const [note, extract, project] = await Promise.all([
        this.chain.adjustment(this.projectId(), this.noteId()),
        this.api.get(this.projectId(), this.extractId()).catch(() => null),
        this.project() === null ? this.projects.get(this.projectId()).catch(() => null) : Promise.resolve(this.project()),
      ]);
      this.note.set(note);
      this.extract.set(extract);
      this.project.set(project);
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected async approve(): Promise<void> {
    await this.act(() => this.chain.approveAdjustment(this.projectId(), this.noteId()));
    await this.reload();
  }

  protected openReject(): void {
    this.reason.set('');
    this.reasonError.set(null);
    this.actionError.set(null);
    this.rejecting.set(true);
  }

  protected async reject(): Promise<void> {
    const reason = this.reason().trim();
    if (reason === '') {
      this.reasonError.set('credit_note.reject_reason_required');
      return;
    }
    if (await this.act(() => this.chain.rejectAdjustment(this.projectId(), this.noteId(), reason))) {
      this.rejecting.set(false);
    }
    await this.reload();
  }
}
