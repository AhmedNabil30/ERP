import { ChangeDetectionStrategy, Component, TemplateRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { DisputeResolutionKind, Extract, ExtractsApi } from '../../../core/extracts/extracts.api';
import { ProjectDetail, ProjectsApi } from '../../../core/projects/projects.api';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { Choice, KaffChoiceGroup } from '../../../shared/kaff-choice-group/kaff-choice-group';
import { KaffCollectionPosition } from '../../../shared/kaff-collection-position/kaff-collection-position';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { FigureRow, KaffFigureList } from '../../../shared/kaff-figure-list/kaff-figure-list';
import { KaffMoneyField } from '../../../shared/kaff-money-field/kaff-money-field';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { ChainPage } from '../chain-page';
import { EXTRACT_PILL, shortDay, wireAmount, lastOf } from '../extract-display';

const KINDS: readonly Choice[] = [
  { value: 'CreditNote', labelKey: 'dispute.kind.price', hintKey: 'dispute.kind.price_hint' },
  { value: 'RevisedExtract', labelKey: 'dispute.kind.quantities', hintKey: 'dispute.kind.quantities_hint' },
];

/**
 * E8 «تسجيل اعتراض» and E9 «حل الاعتراض» (`extracts.html#e8`, `#e9`), one page with two modes chosen by the route.
 * Raising needs the client's reason. Resolving asks the kind of error first: a price or amount error ends in a
 * credit note, a quantity error in a revised extract that cancels this one (D-235 item 3, D-241 item 2). Each mode
 * shows its form only when the read's flag (`raiseDispute`, `resolveDispute`) allows.
 */
@Component({
  selector: 'kaff-extract-dispute-page',
  imports: [
    RouterLink,
    KaffBanner,
    KaffButton,
    KaffChoiceGroup,
    KaffCollectionPosition,
    KaffField,
    KaffFigureList,
    KaffMoneyField,
    KaffPageHead,
    KaffState,
    KaffStatusPill,
  ],
  templateUrl: './extract-dispute-page.html',
  styleUrl: '../chain-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtractDisputePage extends ChainPage {
  private readonly api = inject(ExtractsApi);
  private readonly projects = inject(ProjectsApi);
  private readonly router = inject(Router);

  protected readonly pill = EXTRACT_PILL;

  readonly projectId = input.required<string>();
  readonly extractId = input.required<string>();
  /** From the route data: `raise` (E8) or `resolve` (E9). */
  readonly mode = input<'raise' | 'resolve'>('raise');

  protected readonly data = signal<Extract | null>(null);
  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  /** Only the resolutions the server would accept now (D-260 item 6). */
  protected readonly kinds = computed(() => KINDS.filter((k) => this.data()?.disputeResolutions?.includes(k.value as DisputeResolutionKind)));

  protected readonly kind = signal('');
  protected readonly reason = signal('');
  protected readonly amount = signal('');
  protected readonly errors = signal<{ kind: string | null; reason: string | null; amount: string | null }>({ kind: null, reason: null, amount: null });

  protected readonly allowed = computed(() => {
    const a = this.data()?.actions;
    return a !== undefined && (this.mode() === 'raise' ? a.raiseDispute : a.resolveDispute);
  });

  /** The extract is in the status this screen works on, so a refusal here is the caller's role, not the status. */
  protected readonly statusFits = computed(() => this.data()?.status === (this.mode() === 'raise' ? 'Issued' : 'Disputed'));

  protected override readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');
  protected override readonly hasPrimary = this.allowed;

  protected readonly primaryKey = computed(() =>
    this.mode() === 'raise' ? 'dispute.raise_submit' : this.kind() === 'RevisedExtract' ? 'dispute.create_revision' : 'dispute.issue_credit_note',
  );

  protected readonly name = computed(() => {
    const x = this.data();
    return x === null ? this.i18n.t('extract.list.title') : this.i18n.t('extract.number', { number: x.number });
  });

  protected readonly claim = computed(() => {
    const x = this.data();
    const raised = lastOf(x?.gateHistory, (r) => r.gate === 'Dispute');
    return x && x.status === 'Disputed' ? { date: raised ? shortDay(raised.occurredAt) : '', reason: x.disputeReason ?? raised?.reason ?? '' } : null;
  });

  /** Net payable as a fact beside the credit-note amount (D-239 item 2: no cap, no limit hint). */
  protected readonly facts = computed<FigureRow[]>(() => {
    const d = this.data()?.document;
    return d ? [{ label: this.i18n.t('extract.net_payable'), amount: d.printedTotal }] : [];
  });

  constructor() {
    super();
    effect(() => {
      this.projectId();
      this.extractId();
      untracked(() => void this.reload());
    });
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      const [extract, project] = await Promise.all([this.api.get(this.projectId(), this.extractId()), this.projects.get(this.projectId()).catch(() => null)]);
      this.data.set(extract);
      this.project.set(project);
      // One offer left: it is already the choice.
      if (this.kinds().length === 1) {
        this.kind.set(this.kinds()[0].value);
      }
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected async submit(): Promise<void> {
    const reason = this.reason().trim();
    const back = ['/projects', this.projectId(), 'extracts', this.extractId()];
    if (this.mode() === 'raise') {
      this.errors.set({ kind: null, amount: null, reason: reason === '' ? 'dispute.reason_required' : null });
      if (reason !== '' && (await this.act(() => this.api.raiseDispute(this.projectId(), this.extractId(), reason)))) {
        void this.router.navigate(back);
      }
      return;
    }
    const kind = this.kind() as DisputeResolutionKind | '';
    const amount = kind === 'CreditNote' ? wireAmount(this.amount()) : '0';
    this.errors.set({
      kind: kind === '' ? 'dispute.kind_required' : null,
      reason: reason === '' ? 'dispute.resolution_reason_required' : null,
      amount: kind === 'CreditNote' && amount === null ? 'dispute.amount_required' : null,
    });
    if (kind === '' || reason === '' || amount === null) {
      return;
    }
    if (await this.act(() => this.api.resolveDispute(this.projectId(), this.extractId(), kind, reason, amount))) {
      void this.router.navigate(back);
    }
  }
}
