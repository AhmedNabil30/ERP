import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  TemplateRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';

import { toProblem } from '../../../core/api/problem-details';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import {
  OPPORTUNITY_STAGES,
  OpportunitiesApi,
  OpportunityAct,
  OpportunitySummary,
} from '../../../core/sales/opportunities.api';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffChoiceGroup } from '../../../shared/kaff-choice-group/kaff-choice-group';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { agoKey, daysQuiet, needsFollowUp, nextStage, STATUS_PILL, stageKey, statusKey } from '../../../shared/kaff-opportunity-row/opportunity-display';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStepper } from '../../../shared/kaff-stepper/kaff-stepper';

/** The close-as-lost reasons (R3-Q10 draws a list plus an optional note); the server stores free text. */
const LOST_REASONS = ['price', 'competitor', 'client_delay', 'other'] as const;

/**
 * R3-10 · S3, the opportunity page: where it stands, its one next move, and the quiet acts. Next stage stops at
 * Negotiation (D-268): a contract exists only through conversion to a project, which is not built (R3-11).
 * The visit fee, quotation, expenses and activity rows of the mock wait on R3-11 and have no server yet.
 */
@Component({
  selector: 'kaff-opportunity-page',
  imports: [
    KaffBanner,
    KaffButton,
    KaffChoiceGroup,
    KaffDetailPanel,
    KaffField,
    KaffIcon,
    KaffPageHead,
    KaffState,
    KaffStatusPill,
    KaffStepper,
  ],
  templateUrl: './opportunity-page.html',
  styleUrl: './opportunity-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OpportunityPage {
  private readonly api = inject(OpportunitiesApi);
  private readonly headerActions = inject(HeaderActionsService);
  protected readonly i18n = inject(I18nService);

  /** Bound from the route by `withComponentInputBinding`. */
  readonly opportunityId = input.required<string>();

  protected readonly opportunity = signal<OpportunitySummary | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly refusal = signal<string | null>(null);

  protected readonly closing = signal(false);
  protected readonly lostReason = signal('');
  protected readonly lostNote = signal('');
  protected readonly lostError = signal<string | null>(null);

  protected readonly steps = computed(() => OPPORTUNITY_STAGES.map((s) => this.i18n.t(stageKey(s))));
  protected readonly current = computed(() => {
    const o = this.opportunity();
    return o === null ? 0 : OPPORTUNITY_STAGES.indexOf(o.stage);
  });
  protected readonly pill = computed(() => STATUS_PILL[this.opportunity()?.status ?? 'Active']);
  protected readonly statusLabel = computed(() => this.i18n.t(statusKey(this.opportunity()?.status ?? 'Active')));
  protected readonly ago = computed(() => {
    const o = this.opportunity();
    if (o === null) return '';
    const { key, params } = agoKey(daysQuiet(o.lastActivityAt, new Date()));
    return this.i18n.t('sales.last_activity', { when: this.i18n.t(key, params) });
  });
  protected readonly followUp = computed(() => {
    const o = this.opportunity();
    return o !== null && needsFollowUp(o.inactivity);
  });
  protected readonly live = computed(() => ['Active', 'Stalled'].includes(this.opportunity()?.status ?? ''));
  protected readonly onHold = computed(() => this.opportunity()?.status === 'OnHold');
  protected readonly lost = computed(() => this.opportunity()?.status === 'ClosedLost');
  protected readonly next = computed(() => {
    const o = this.opportunity();
    return o !== null && this.live() ? nextStage(o.stage) : null;
  });
  protected readonly nextLabel = computed(() => {
    const n = this.next();
    return n === null ? '' : this.i18n.t('sales.action.advance', { stage: this.i18n.t(stageKey(n)) });
  });
  protected readonly canClose = computed(() => this.live() || this.onHold());
  protected readonly reasons = computed(() =>
    LOST_REASONS.map((r) => ({ value: r, labelKey: `sales.lost.${r}` })),
  );

  private readonly primaryAction = viewChild<TemplateRef<unknown>>('primaryAction');

  constructor() {
    effect(() => {
      const id = this.opportunityId();
      untracked(() => void this.load(id));
    });
    effect(() => this.headerActions.set(this.primaryAction() ?? null, 'primary'));
    inject(DestroyRef).onDestroy(() => this.headerActions.clear());
  }

  protected reload(): void {
    void this.load(this.opportunityId());
  }

  protected async run(act: OpportunityAct, reason: string | null = null): Promise<boolean> {
    const o = this.opportunity();
    if (o === null || this.busy()) return false;
    this.busy.set(true);
    this.refusal.set(null);
    try {
      await this.api.act(o.id, act, reason);
      this.busy.set(false);
      await this.load(o.id, false);
      return true;
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
      return false;
    } finally {
      this.busy.set(false);
    }
  }

  protected advance(): Promise<boolean> {
    return this.run('advance');
  }

  protected openClose(): void {
    this.lostReason.set('');
    this.lostNote.set('');
    this.lostError.set(null);
    this.closing.set(true);
  }

  protected async confirmClose(): Promise<void> {
    if (this.lostReason() === '') {
      this.lostError.set('sales.lost.reason_required');
      return;
    }
    const label = this.i18n.t(`sales.lost.${this.lostReason()}`);
    const note = this.lostNote().trim();
    if (await this.run('close-lost', note === '' ? label : `${label}: ${note}`)) {
      this.closing.set(false);
    }
  }

  protected onText(target: EventTarget | null, into: { set(value: string): void }): void {
    into.set((target as HTMLTextAreaElement).value);
  }

  private async load(id: string, spinner = true): Promise<void> {
    if (spinner) this.loading.set(true);
    this.failure.set(null);
    try {
      this.opportunity.set(await this.api.get(id));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }
}
