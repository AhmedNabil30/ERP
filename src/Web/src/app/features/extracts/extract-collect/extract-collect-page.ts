import { ChangeDetectionStrategy, Component, TemplateRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { Adjustment, ChainApi } from '../../../core/extracts/chain.api';
import { Extract, ExtractsApi } from '../../../core/extracts/extracts.api';
import { ProjectDetail, ProjectsApi } from '../../../core/projects/projects.api';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffCollectionPosition } from '../../../shared/kaff-collection-position/kaff-collection-position';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { ChainPage } from '../chain-page';
import { CollectionForm } from '../collection-form/collection-form';
import { EXTRACT_PILL, isPositive } from '../extract-display';

/** Refusals the form shows under a field; any other is a banner. */
const FIELD_REFUSALS = new Set(['errors.collection.exceeds_expected', 'errors.treasury.posting_date_out_of_range']);

/**
 * E5, «تسجيل دفعة» (`extracts.html#e5`): Finance records what arrived on an issued extract. Shown only when the
 * read's `recordCollection` flag allows; otherwise the screen says why in one line and links where to go.
 */
@Component({
  selector: 'kaff-extract-collect-page',
  imports: [RouterLink, KaffBanner, KaffButton, KaffCollectionPosition, KaffPageHead, KaffState, CollectionForm],
  templateUrl: './extract-collect-page.html',
  styleUrl: '../chain-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtractCollectPage extends ChainPage {
  private readonly api = inject(ExtractsApi);
  private readonly chain = inject(ChainApi);
  private readonly projects = inject(ProjectsApi);
  private readonly router = inject(Router);

  readonly projectId = input.required<string>();
  readonly extractId = input.required<string>();

  protected readonly data = signal<Extract | null>(null);
  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly creditNote = signal<Adjustment | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  private readonly form = viewChild(CollectionForm);
  protected override readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');
  protected override readonly hasPrimary = computed(() => this.data()?.actions.recordCollection === true);

  protected readonly name = computed(() => {
    const x = this.data();
    return x === null ? this.i18n.t('extract.list.title') : this.i18n.t('extract.number', { number: x.number });
  });

  /** The contract withholds: the server expects a withheld amount on this extract. */
  protected readonly withholding = computed(() => isPositive(this.data()?.collection?.expectedWithheld));

  protected readonly fieldRefusal = computed(() => {
    const key = this.actionError();
    return key !== null && FIELD_REFUSALS.has(key) ? key : null;
  });

  protected readonly bannerRefusal = computed(() => {
    const key = this.actionError();
    return key !== null && !FIELD_REFUSALS.has(key) ? key : null;
  });

  /** Why nothing may be recorded: not issued yet, a credit note waits on the Owner, or nothing remains. */
  protected readonly refusal = computed(() => {
    const x = this.data();
    if (x === null || x.actions.recordCollection) {
      return null;
    }
    if (x.status !== 'Issued') {
      return this.i18n.t('collection.refused.status', { number: x.number, status: this.i18n.t(EXTRACT_PILL[x.status].key) });
    }
    if (this.creditNote()?.status === 'Raised') {
      return this.i18n.t('collection.refused.credit_note');
    }
    return this.i18n.t('errors.collection.nothing_remaining');
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
      const [extract, project] = await Promise.all([
        this.api.get(this.projectId(), this.extractId()),
        this.projects.get(this.projectId()).catch(() => null),
      ]);
      this.data.set(extract);
      this.project.set(project);
      if (!extract.actions.recordCollection && extract.status === 'Issued' && this.held(this.projectId()).has('TreasuryReadProject')) {
        const notes = await this.chain.adjustments(this.projectId()).catch(() => [] as readonly Adjustment[]);
        this.creditNote.set(notes.find((n) => n.sourceDocumentId === extract.id && n.status === 'Raised') ?? null);
      }
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected async submit(): Promise<void> {
    const request = this.form()?.request();
    if (!request) {
      return;
    }
    if (await this.act(() => this.api.collect(this.projectId(), this.extractId(), request))) {
      void this.router.navigate(['/projects', this.projectId(), 'extracts', this.extractId()]);
    }
  }
}
