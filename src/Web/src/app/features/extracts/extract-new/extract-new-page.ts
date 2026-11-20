import { ChangeDetectionStrategy, Component, TemplateRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { ExtractStatus, ExtractSummary, ExtractsApi } from '../../../core/extracts/extracts.api';
import { ProjectDetail, ProjectsApi } from '../../../core/projects/projects.api';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { ChainPage } from '../chain-page';
import { EXTRACT_PILL } from '../extract-display';

const SETTLED: readonly ExtractStatus[] = ['Issued', 'Collected', 'Disputed', 'Void'];

/** The day after a `yyyy-MM-dd` date, as `yyyy-MM-dd` (a calendar step, not money). */
export function nextDay(value: string): string {
  const [y = 0, m = 1, d = 1] = value.split('-').map(Number);
  const next = new Date(Date.UTC(y, m - 1, d + 1));
  return next.toISOString().slice(0, 10);
}

/**
 * E2, «مستخلص جديد» (`extracts.html#e2`): the junior site engineer starts the extract with its period only
 * (D-238 item 4, D-241 item 1). No money on this screen. The quantities preview and the change orders the extract
 * will bill are not sent by the API yet, so the screen shows the last extract and the period.
 */
@Component({
  selector: 'kaff-extract-new-page',
  imports: [RouterLink, KaffBanner, KaffButton, KaffField, KaffPageHead, KaffState, KaffStatusPill],
  templateUrl: './extract-new-page.html',
  styleUrl: '../chain-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtractNewPage extends ChainPage {
  private readonly api = inject(ExtractsApi);
  private readonly projects = inject(ProjectsApi);
  private readonly router = inject(Router);

  protected readonly pill = EXTRACT_PILL;

  readonly projectId = input.required<string>();

  protected readonly rows = signal<readonly ExtractSummary[]>([]);
  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly errors = signal<{ from: string | null; to: string | null }>({ from: null, to: null });

  protected readonly last = computed(() => [...this.rows()].sort((a, b) => b.number - a.number)[0] ?? null);
  /** An extract still in the chain: one at a time (spec §7). */
  protected readonly inFlight = computed(() => this.rows().find((r) => !SETTLED.includes(r.status)) ?? null);
  protected readonly unsigned = computed(() => this.project()?.isSigned === false);

  protected override readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');
  protected override readonly hasPrimary = computed(
    () => !this.loading() && this.failure() === null && this.inFlight() === null && !this.unsigned(),
  );

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
      const [rows, project] = await Promise.all([this.api.list(this.projectId()), this.projects.get(this.projectId()).catch(() => null)]);
      this.rows.set(rows);
      this.project.set(project);
      const last = this.last();
      if (this.from() === '' && last !== null) {
        this.from.set(nextDay(last.periodEnd));
      }
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected async submit(): Promise<void> {
    const from = this.from();
    const to = this.to();
    this.errors.set({
      from: from === '' ? 'extract.new.from_required' : null,
      to: to === '' ? 'extract.new.to_required' : to < from ? 'extract.new.to_before_from' : null,
    });
    if (from === '' || to === '' || to < from) {
      return;
    }
    let created: string | null = null;
    if (await this.act(async () => (created = (await this.api.prepare(this.projectId(), from, to)).id))) {
      void this.router.navigate(['/projects', this.projectId(), 'extracts', created]);
    } else if (this.actionError() === 'errors.extract.another_extract_open') {
      await this.reload();
    }
  }
}
