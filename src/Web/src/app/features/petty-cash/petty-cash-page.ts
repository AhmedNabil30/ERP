import { ChangeDetectionStrategy, Component, TemplateRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';

import { toProblem } from '../../core/api/problem-details';
import { PettyCashApi, PettyCashRequest, PettyCashStatus } from '../../core/petty-cash/petty-cash.api';
import { ProjectDetail, ProjectsApi } from '../../core/projects/projects.api';
import { KaffBanner } from '../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffFigure } from '../../shared/kaff-figure/kaff-figure';
import { KaffPageHead } from '../../shared/kaff-page-head/kaff-page-head';
import { KaffState } from '../../shared/kaff-state/kaff-state';
import { KaffStatusPill, PillTone } from '../../shared/kaff-status-pill/kaff-status-pill';
import { KaffIconName } from '../../shared/kaff-icon/kaff-icon';
import { ChainPage } from '../extracts/chain-page';
import { shortDay } from '../extracts/extract-display';

const PILL: Readonly<Record<PettyCashStatus, { tone: PillTone; icon: KaffIconName; key: string }>> = {
  Draft: { tone: 'none', icon: 'file', key: 'petty_cash.status.Draft' },
  Submitted: { tone: 'run', icon: 'clock', key: 'petty_cash.status.Submitted' },
  Returned: { tone: 'stuck', icon: 'redo', key: 'petty_cash.status.Returned' },
};

/**
 * عهدة requests of one project (`site-money.html` S1 and S2). The supervisor (`DraftSubmit`) reads the drafts that
 * wait for him and submits one or returns it with a reason; the junior reads only his own, with where each stands,
 * and has no act. Only the open item carries its actions. The server decides who reads what and who may act.
 */
@Component({
  selector: 'kaff-petty-cash-page',
  imports: [KaffBanner, KaffButton, KaffFigure, KaffPageHead, KaffState, KaffStatusPill],
  templateUrl: './petty-cash-page.html',
  styleUrl: './petty-cash-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PettyCashPage extends ChainPage {
  private readonly api = inject(PettyCashApi);
  private readonly projectsApi = inject(ProjectsApi);

  protected readonly pill = PILL;
  protected readonly shortDay = shortDay;

  /** Bound from `:projectId` by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();

  protected readonly rows = signal<readonly PettyCashRequest[]>([]);
  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  protected readonly openId = signal<string | null>(null);
  protected readonly returning = signal(false);
  protected readonly reason = signal('');
  protected readonly reasonInvalid = signal(false);

  /** The supervisor reads the drafts that wait for him; everyone else reads his own. Convenience: the server decides. */
  protected readonly supervising = computed(() => {
    const entry = this.auth.current()?.projects.find((row) => row.projectId === this.projectId());
    return entry?.permissions.includes('DraftSubmit') ?? false;
  });

  /** Oldest first for the one who submits (the first waits longest); newest first as the server sends it for the drafter. */
  protected readonly shown = computed(() =>
    this.supervising() ? [...this.rows()].sort((a, b) => a.createdAt.localeCompare(b.createdAt)) : this.rows(),
  );

  /** The open item: the one tapped, else the first. */
  protected readonly current = computed(() => {
    const id = this.openId();
    const list = this.shown();
    return list.find((row) => row.id === id)?.id ?? list[0]?.id ?? null;
  });

  protected override readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');
  /** The one primary sits in the shell's slot: submit the open draft, or confirm its return. */
  protected override readonly hasPrimary = computed(() => this.supervising() && this.current() !== null && !this.loading() && this.failure() === null);

  constructor() {
    super();
    // A required input is set after construction: read it in an effect, reload untracked (NG0950).
    effect(() => {
      this.projectId();
      untracked(() => void this.reload());
    });
  }

  protected async reload(): Promise<void> {
    this.loading.set(this.rows().length === 0);
    this.failure.set(null);
    try {
      const [rows, project] = await Promise.all([
        this.api.list(this.projectId(), this.supervising() ? 'Draft' : undefined),
        this.projectsApi.get(this.projectId()).catch(() => null),
      ]);
      this.rows.set(rows);
      this.project.set(project);
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected open(id: string): void {
    this.openId.set(id);
    this.returning.set(false);
    this.actionError.set(null);
  }

  protected startReturn(): void {
    this.reason.set('');
    this.reasonInvalid.set(false);
    this.actionError.set(null);
    this.returning.set(true);
  }

  protected cancelReturn(): void {
    this.returning.set(false);
    this.actionError.set(null);
  }

  protected onReason(event: Event): void {
    this.reason.set((event.target as HTMLTextAreaElement).value);
    this.reasonInvalid.set(false);
  }

  protected async submitCurrent(): Promise<void> {
    const id = this.current();
    if (id !== null && (await this.act(() => this.api.submit(this.projectId(), id)))) {
      await this.afterAct();
    }
  }

  protected async confirmReturn(): Promise<void> {
    const id = this.current();
    const reason = this.reason().trim();
    if (id === null) {
      return;
    }
    if (reason === '') {
      this.reasonInvalid.set(true);
      return;
    }
    if (await this.act(() => this.api.returnDraft(this.projectId(), id, reason))) {
      await this.afterAct();
    }
  }

  private async afterAct(): Promise<void> {
    this.returning.set(false);
    this.openId.set(null);
    await this.reload();
  }
}
