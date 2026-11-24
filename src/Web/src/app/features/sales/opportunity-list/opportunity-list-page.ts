import { ChangeDetectionStrategy, Component, DestroyRef, TemplateRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import {
  OPPORTUNITY_STAGES,
  OpportunitiesApi,
  OpportunityStage,
  OpportunityStatus,
  OpportunitySummary,
} from '../../../core/sales/opportunities.api';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffOpportunityRow } from '../../../shared/kaff-opportunity-row/kaff-opportunity-row';
import { stageKey } from '../../../shared/kaff-opportunity-row/opportunity-display';
import { KaffSearchField } from '../../../shared/kaff-search-field/kaff-search-field';
import { KaffSegmentedFilter, SegmentedFilterOption } from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffSelect, SelectOption } from '../../../shared/kaff-select/kaff-select';
import { KaffState } from '../../../shared/kaff-state/kaff-state';

type View = 'all' | 'followUp';

/** The statuses a person looks for: working, put off, lost. Won waits on conversion (R3-11). */
const STATUSES: readonly OpportunityStatus[] = ['Active', 'OnHold', 'ClosedLost'];

/**
 * R3-10 · S1, the pipeline list. Every filter is the server's: stage, status, search and follow-up are
 * sent as query parameters and the rows are drawn as they come back. The server derives the quiet flag.
 */
@Component({
  selector: 'kaff-opportunity-list-page',
  imports: [KaffButton, KaffOpportunityRow, KaffSearchField, KaffSegmentedFilter, KaffSelect, KaffState],
  templateUrl: './opportunity-list-page.html',
  styleUrl: './opportunity-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OpportunityListPage {
  private readonly api = inject(OpportunitiesApi);
  private readonly router = inject(Router);
  private readonly headerActions = inject(HeaderActionsService);
  protected readonly i18n = inject(I18nService);

  protected readonly searchDraft = signal('');
  protected readonly search = signal('');
  protected readonly stage = signal('');
  protected readonly status = signal('');
  protected readonly view = signal<View>('all');
  private readonly attempt = signal(0);

  protected readonly rows = signal<readonly OpportunitySummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly now = signal(new Date());

  protected readonly stageOptions = computed<readonly SelectOption[]>(() => [
    { value: '', label: this.i18n.t('sales.filter.all_stages') },
    ...OPPORTUNITY_STAGES.map((s) => ({ value: s, label: this.i18n.t(stageKey(s)) })),
  ]);
  protected readonly statusOptions = computed<readonly SelectOption[]>(() => [
    { value: '', label: this.i18n.t('sales.filter.all_statuses') },
    ...STATUSES.map((s) => ({ value: s, label: this.i18n.t(`sales.status.${s}`) })),
  ]);
  protected readonly viewOptions: readonly SegmentedFilterOption<View>[] = [
    { value: 'all', labelKey: 'sales.filter.all' },
    { value: 'followUp', labelKey: 'sales.follow_up' },
  ];

  /** True when any filter is on, so an empty answer reads as nothing matching and not as no opportunities. */
  protected readonly filtered = computed(
    () => this.search().trim() !== '' || this.stage() !== '' || this.status() !== '' || this.view() !== 'all',
  );

  private readonly createAction = viewChild<TemplateRef<unknown>>('createAction');

  constructor() {
    effect(() => {
      void this.attempt();
      void this.reload(
        this.search(),
        this.stage() as OpportunityStage | '',
        this.status() as OpportunityStatus | '',
        this.view() === 'followUp',
      );
    });
    effect(() => this.headerActions.set(this.createAction() ?? null, 'primary'));
    inject(DestroyRef).onDestroy(() => this.headerActions.clear());
  }

  protected onCreate(): void {
    void this.router.navigate(['/sales/new']);
  }

  protected retry(): void {
    this.attempt.update((n) => n + 1);
  }

  private async reload(search: string, stage: OpportunityStage | '', status: OpportunityStatus | '', followUp: boolean): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      this.rows.set(await this.api.list({ search, stage, status, followUp }));
      this.now.set(new Date());
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.rows.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
