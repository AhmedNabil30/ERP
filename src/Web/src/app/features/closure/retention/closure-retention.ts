import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';

import { toProblem } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';
import { ClosureApi, SubcontractorRetention } from '../../../core/closure/closure.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffInlineConfirm } from '../../../shared/kaff-inline-confirm/kaff-inline-confirm';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { shortDay } from '../../extracts/extract-display';

/** One release posting (or one waiting release request) as a row, with where its reversal stands. */
export interface RetentionRow {
  readonly key: string;
  readonly subcontractorId: string;
  readonly name: string;
  readonly amount: string | null;
  readonly date: string | null;
  readonly postingId: string | null;
  readonly state: 'reversed' | 'release-waiting' | 'reversal-waiting' | 'open';
  readonly asker: string | null;
  readonly reason: string | null;
  /** The reversal request on this very row was made by the viewer: the server refuses a self decision. */
  readonly mine: boolean;
}

type Sheet = { kind: 'ask' | 'return'; row: RetentionRow } | null;

/** Roles that read subcontractor retention (D-273 answer 4). The server decides; this keeps the panel off other screens. */
const READERS: readonly string[] = ['Finance', 'Owner', 'TechnicalOffice'];

/**
 * Subcontractor retention on the closure screen (`closure-retention-reversal.html`, D-271): each posted release by
 * name, amount and date. Finance asks to reverse one with a reason; the Owner approves or returns it with a reason;
 * the one who asked never decides. A release request that waits is shown as status only: no release act is drawn.
 */
@Component({
  selector: 'kaff-closure-retention',
  imports: [KaffBanner, KaffButton, KaffDetailPanel, KaffFigure, KaffInlineConfirm, KaffState, KaffStatusPill],
  templateUrl: './closure-retention.html',
  styleUrl: './closure-retention.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClosureRetention {
  private readonly api = inject(ClosureApi);
  private readonly auth = inject(AuthService);
  protected readonly i18n = inject(I18nService);

  readonly projectId = input.required<string>();

  protected readonly data = signal<readonly SubcontractorRetention[] | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly done = signal(false);

  protected readonly sheet = signal<Sheet>(null);
  protected readonly reason = signal('');
  protected readonly reasonInvalid = signal(false);
  protected readonly confirming = signal<string | null>(null);

  private readonly held = computed(() => {
    const entry = this.auth.current()?.projects.find((row) => row.projectId === this.projectId());
    return new Set(entry?.permissions ?? []);
  });
  /** Finance asks. Convenience only: the server decides. */
  protected readonly mayAsk = computed(() => this.held().has('SubcontractorRetentionRelease'));
  /** The Owner decides. */
  protected readonly mayDecide = computed(() => this.held().has('FinancialMovementApprove'));
  protected readonly mayRead = computed(() => READERS.includes(this.auth.current()?.role ?? ''));
  /** Hidden outright when the server refuses the read: not an error the user can act on. */
  protected readonly visible = computed(() => this.mayRead() && this.failure() !== 'errors.auth.forbidden');

  protected readonly rows = computed<readonly RetentionRow[]>(() => {
    const me = this.auth.current()?.userId;
    const out: RetentionRow[] = [];
    for (const sub of this.data() ?? []) {
      const rev = sub.reversalRequest;
      for (const release of sub.releases) {
        const waiting = rev !== null && rev.postingId === release.postingId;
        out.push({
          key: release.postingId,
          subcontractorId: sub.subcontractorId,
          name: sub.name,
          amount: release.amount,
          date: shortDay(release.postingDate),
          postingId: release.postingId,
          state: release.reversed ? 'reversed' : waiting ? 'reversal-waiting' : 'open',
          asker: waiting ? rev.askedByName : null,
          reason: waiting ? rev.reason : null,
          mine: waiting && rev.askedByUserId === me,
        });
      }
      if (sub.releaseRequest !== null) {
        out.push({
          key: `request-${sub.subcontractorId}`,
          subcontractorId: sub.subcontractorId,
          name: sub.name,
          amount: null,
          date: shortDay(sub.releaseRequest.createdAt),
          postingId: null,
          state: 'release-waiting',
          asker: sub.releaseRequest.askedByName,
          reason: null,
          mine: false,
        });
      }
    }
    return out;
  });

  /** A release is reversed once; while a request waits on this subcontractor, no second one is offered. */
  protected canAsk(row: RetentionRow): boolean {
    if (!this.mayAsk() || row.state !== 'open') {
      return false;
    }
    return !this.data()?.find((sub) => sub.subcontractorId === row.subcontractorId)?.reversalRequest;
  }

  protected canDecide(row: RetentionRow): boolean {
    return this.mayDecide() && row.state === 'reversal-waiting' && !row.mine;
  }

  /** Who asked is named to the one who decides; everyone else reads that it waits for the Owner. */
  protected namesAsker(row: RetentionRow): boolean {
    return this.mayDecide() && row.asker !== null;
  }

  constructor() {
    // A required input is set after construction: read it in an effect, reload untracked (NG0950).
    effect(() => {
      this.projectId();
      untracked(() => void this.reload());
    });
  }

  protected async reload(): Promise<void> {
    if (!this.mayRead()) {
      this.loading.set(false);
      return;
    }
    this.loading.set(this.data() === null);
    this.failure.set(null);
    try {
      this.data.set(await this.api.getRetention(this.projectId()));
    } catch (error) {
      this.data.set(null);
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected openSheet(kind: 'ask' | 'return', row: RetentionRow): void {
    this.reason.set('');
    this.reasonInvalid.set(false);
    this.actionError.set(null);
    this.confirming.set(null);
    this.sheet.set({ kind, row });
  }

  protected closeSheet(): void {
    this.sheet.set(null);
    this.actionError.set(null);
  }

  protected onReason(event: Event): void {
    this.reason.set((event.target as HTMLTextAreaElement).value);
    this.reasonInvalid.set(false);
  }

  protected askConfirm(row: RetentionRow): void {
    this.actionError.set(null);
    this.confirming.set(row.key);
  }

  protected async sendReason(): Promise<void> {
    const open = this.sheet();
    const reason = this.reason().trim();
    if (open === null) {
      return;
    }
    if (reason === '') {
      this.reasonInvalid.set(true);
      return;
    }
    const { row, kind } = open;
    const ok = await this.run(() =>
      kind === 'ask'
        ? this.api.requestRetentionReversal(this.projectId(), row.subcontractorId, row.postingId ?? '', reason)
        : this.api.rejectRetentionReversal(this.projectId(), row.subcontractorId, reason),
    );
    if (ok) {
      this.sheet.set(null);
    }
  }

  protected async approve(row: RetentionRow): Promise<void> {
    if (await this.run(() => this.api.approveRetentionReversal(this.projectId(), row.subcontractorId))) {
      this.confirming.set(null);
      this.done.set(true);
    }
  }

  private async run(call: () => Promise<unknown>): Promise<boolean> {
    this.busy.set(true);
    this.actionError.set(null);
    this.done.set(false);
    try {
      await call();
    } catch (error) {
      this.actionError.set(toProblem(error).messageKey);
      return false;
    } finally {
      this.busy.set(false);
    }
    await this.reload();
    return true;
  }
}
