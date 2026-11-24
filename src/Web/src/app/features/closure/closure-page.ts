import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';

import { RouterLink } from '@angular/router';

import { toProblem } from '../../core/api/problem-details';
import { AuthService } from '../../core/auth/auth.service';
import {
  ClosureApi,
  Snag,
  SnagOwnerKind,
  SnagPriority,
  SnagResolutionKind,
  WarrantyCallback,
} from '../../core/closure/closure.api';
import { projectWorkspaceStatusKey } from '../../core/i18n/enum-keys';
import { I18nService } from '../../core/i18n/i18n.service';
import { dmyOf } from '../../core/format/site-day';
import { ProofApi, ProofResponse } from '../../core/proof/proof.api';
import { ProjectDetail, ProjectStatus, ProjectsApi } from '../../core/projects/projects.api';
import { SubcontractorSummary, SubcontractorsApi } from '../../core/subcontractors/subcontractors.api';
import { ExtractsApi } from '../../core/extracts/extracts.api';
import { ClosureSummary } from '../../core/closure/closure.api';
import { ChainApi, ReleasedHoldCollection } from '../../core/extracts/chain.api';
import { ClosureRetention } from './retention/closure-retention';
import { KaffBadge } from '../../shared/kaff-badge/kaff-badge';
import { KaffCollectionPosition } from '../../shared/kaff-collection-position/kaff-collection-position';
import { KaffDetailPanel } from '../../shared/kaff-detail-panel/kaff-detail-panel';
import { CollectionForm } from '../extracts/collection-form/collection-form';
import { EXTRACT_PILL, isPositive } from '../extracts/extract-display';
import { KaffBanner } from '../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffFigureList, FigureRow } from '../../shared/kaff-figure-list/kaff-figure-list';
import { KaffFigure } from '../../shared/kaff-figure/kaff-figure';
import { KaffIcon } from '../../shared/kaff-icon/kaff-icon';
import { KaffInlineConfirm } from '../../shared/kaff-inline-confirm/kaff-inline-confirm';
import { KaffPageHead } from '../../shared/kaff-page-head/kaff-page-head';
import { KaffSelect, SelectOption } from '../../shared/kaff-select/kaff-select';
import { KaffState } from '../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../shared/kaff-status-pill/kaff-status-pill';
import { shownStatus } from '../projects/project-status-pill';
import { CLOSURE_PILL, ClosureCaps, ClosureStep, STEP_LABEL_KEY, closureCapsOf, closureNextSteps } from './closure-display';

/** A project act that asks the user to confirm before it runs. */
export type ProjectAct = ClosureStep;

const ACT_KEYS: Readonly<Record<ProjectAct, { confirm: string; detail: string | null }>> = {
  'practical-completion': { confirm: 'closure.confirm.practical_completion', detail: 'closure.confirm.practical_completion_detail' },
  handover: { confirm: 'closure.confirm.handover', detail: 'closure.confirm.handover_detail' },
  close: { confirm: 'closure.confirm.close', detail: 'closure.confirm.close_detail' },
  'hold-release': { confirm: 'closure.confirm.hold_release', detail: 'closure.confirm.hold_release_detail' },
};

const UNMET_KEYS: Readonly<Record<string, string>> = {
  ExtractsIssued: 'closure.unmet.ExtractsIssued',
  AdvanceRecovered: 'closure.unmet.AdvanceRecovered',
  HoldReleased: 'closure.unmet.HoldReleased',
};

/**
 * The end of a project's life, in one screen: practical completion, the snag list, handover, the hold
 * release, the warranty and its callbacks, and closing.
 *
 * **A control the user may not use is absent, never disabled.** Every control hangs off the user's own
 * permission on THIS project (`Session.projects[].permissions`, role AND assignment already folded in by
 * the server's evaluator) and the project's state. The server decides every act again.
 *
 * **The hold only grows and releases once, in full, at handover.** The release has no amount field: the
 * figure shown is the hold ledger as the server derives it from postings (the proof read), and the one
 * button sends no body. Nothing on this screen debits the hold for a snag, a callback or an adjustment.
 *
 * **Terminated is unruled** (Q108). The control asks, and the server's refusal is rendered as what it is:
 * not decided yet.
 */
@Component({
  selector: 'kaff-closure-page',
  imports: [
    ClosureRetention,
    CollectionForm,
    KaffBadge,
    KaffCollectionPosition,
    KaffDetailPanel,
    KaffBanner,
    KaffButton,
    KaffFigure,
    KaffFigureList,
    RouterLink,
    KaffIcon,
    KaffInlineConfirm,
    KaffPageHead,
    KaffSelect,
    KaffState,
    KaffStatusPill,
  ],
  templateUrl: './closure-page.html',
  styleUrl: './closure-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClosurePage {
  private readonly api = inject(ClosureApi);
  private readonly extractsApi = inject(ExtractsApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly proofApi = inject(ProofApi);
  private readonly subcontractorsApi = inject(SubcontractorsApi);
  private readonly auth = inject(AuthService);
  private readonly chainApi = inject(ChainApi);

  protected readonly i18n = inject(I18nService);
  protected readonly statusKey = projectWorkspaceStatusKey;
  protected readonly shown = shownStatus;
  protected readonly pill = CLOSURE_PILL;
  protected readonly actKeys = ACT_KEYS;
  protected readonly stepLabel = STEP_LABEL_KEY;

  /** Bound from `:projectId` by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();

  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly snags = signal<readonly Snag[]>([]);
  protected readonly callbacks = signal<readonly WarrantyCallback[]>([]);
  protected readonly subcontractors = signal<readonly SubcontractorSummary[]>([]);
  /** The proof read, for the hold ledger only. Null: not asked (no `HoldRelease`), or the read failed. */
  protected readonly proof = signal<ProofResponse | null>(null);
  protected readonly proofFailed = signal(false);

  /** D-257 closure read: the hold the release would move (null for a viewer without money) and the open extracts. */
  protected readonly summary = signal<ClosureSummary | null>(null);
  /** Extract id by number: the closure read names open extracts by number, the route by id. Empty when the list is refused. */
  protected readonly extractIds = signal<ReadonlyMap<number, string>>(new Map());
  protected readonly extractStatusKey = (status: keyof typeof EXTRACT_PILL): string => EXTRACT_PILL[status].key;
  protected readonly holdRows = computed<readonly FigureRow[]>(() => {
    const hold = this.summary()?.hold ?? null;
    return hold === null ? [] : [{ label: this.i18n.t('closure.hold.title'), amount: hold, testId: 'closure-pending-hold' }];
  });

  /**
   * D-244 item 2: a released hold is collected like an extract, through the same collection form (E5). Read for a
   * session that records collections, once the project is handed over. Null: not asked, refused, or failed.
   */
  protected readonly releasedHold = signal<ReleasedHoldCollection | null>(null);
  protected readonly collectingHold = signal(false);
  protected readonly holdCollectError = signal<string | null>(null);
  protected readonly holdCollected = signal(false);
  private readonly holdForm = viewChild(CollectionForm);

  /** Something of the released hold is expected: the hold was released and the client owes it. */
  protected readonly showHoldCollection = computed(() => isPositive(this.releasedHold()?.expected));
  protected readonly holdMayCollect = computed(() => isPositive(this.releasedHold()?.remaining));

  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly actionDetail = signal<string | null>(null);
  protected readonly note = signal<string | null>(null);

  protected readonly confirming = signal<ProjectAct | null>(null);
  protected readonly terminating = signal(false);
  protected readonly terminationReason = signal('');
  protected readonly terminationRefused = signal<string | null>(null);

  protected readonly raisingSnag = signal(false);
  protected readonly snagDescription = signal('');
  protected readonly snagLocation = signal('');
  protected readonly snagOwner = signal<string>('Kaff');
  protected readonly snagSubcontractor = signal('');
  protected readonly snagPriority = signal<string>('Minor');
  protected readonly snagInvalid = signal(false);

  protected readonly resolvingSnag = signal<string | null>(null);
  protected readonly resolutionKind = signal<string>('Absorbed');
  protected readonly resolutionReason = signal('');
  protected readonly resolutionInvalid = signal(false);

  protected readonly raisingCallback = signal(false);
  protected readonly callbackDescription = signal('');
  protected readonly callbackInvalid = signal(false);

  /** What this user may do on THIS project. Convenience; the server decides each act. */
  protected readonly caps = computed<ClosureCaps>(() => {
    const entry = this.auth.current()?.projects.find((row) => row.projectId === this.projectId());
    return closureCapsOf(entry?.permissions);
  });

  protected readonly status = computed<ProjectStatus | null>(() => this.project()?.status ?? null);

  /** The project acts this user may take now, in order. */
  protected readonly steps = computed<readonly ClosureStep[]>(() => {
    const project = this.project();
    return project === null ? [] : closureNextSteps(project.contractType, project.status, this.caps());
  });

  protected readonly showHold = computed(() => {
    const project = this.project();
    return (
      project !== null &&
      (this.caps().holdRelease || this.caps().approve) &&
      project.contractType === 'LumpSum' &&
      (project.status === 'Handover' || project.status === 'UnderWarranty')
    );
  });

  /** The hold ledger as the server derives it, and whether a release already stands. */
  protected readonly hold = computed(() => {
    const read = this.proof();
    if (read === null) {
      return null;
    }
    const ledger = read.ledgers.find((row) => row.key === 'Hold');
    const reversed = new Set(read.postings.map((posting) => posting.reversesId).filter((id) => id !== null));
    const released = read.postings.some((posting) => posting.type === 'HoldRelease' && !reversed.has(posting.id));
    return {
      balance: ledger?.balance ?? '0',
      postingCount: ledger?.postingIds.length ?? 0,
      released,
      empty: /^-?[0.]*$/.test(ledger?.balance ?? '0'),
    };
  });

  /** Snag acts exist from practical completion until the warranty runs. */
  protected readonly snagActsOpen = computed(() => {
    const status = this.status();
    return this.caps().snag && (status === 'HandoverPending' || status === 'Handover');
  });

  protected readonly canRaiseSnag = computed(() => this.caps().snag && this.status() === 'HandoverPending');

  protected readonly showSnags = computed(() => {
    const status = this.status();
    return status === 'HandoverPending' || status === 'Handover' || this.snags().length > 0;
  });

  protected readonly showCallbacks = computed(() => {
    const status = this.status();
    return status === 'Handover' || status === 'UnderWarranty' || this.callbacks().length > 0;
  });

  protected readonly canRaiseCallback = computed(() => {
    const status = this.status();
    return this.caps().callback && (status === 'Handover' || status === 'UnderWarranty');
  });

  protected readonly canTerminate = computed(() => {
    const status = this.status();
    return (
      this.caps().manage &&
      (status === 'Active' || status === 'HandoverPending' || status === 'Handover' || status === 'UnderWarranty')
    );
  });

  /** The latest hold release request (D-262): Finance asks, the Owner approves or sends it back with a reason. */
  protected readonly request = computed(() => this.summary()?.holdReleaseRequest ?? null);
  protected readonly waiting = computed(() => this.request()?.status === 'Requested');
  protected readonly returnedReason = computed(() => {
    const request = this.request();
    return request?.status === 'Rejected' ? request.rejectionReason : null;
  });
  /** Finance may ask while none waits. The server refuses a second request and a release with nothing to release. */
  protected readonly canAsk = computed(() => this.caps().holdRelease && !this.waiting());
  /** The Owner decides a waiting request, never one the same user asked for (the server refuses that too). */
  protected readonly canDecide = computed(
    () => this.caps().approve && this.waiting() && this.request()?.requestedByUserId !== this.auth.current()?.userId,
  );

  /** The hold can be acted on now: a figure stands, no release already does, and this user may ask or decide. */
  protected readonly holdActionable = computed(() => {
    const h = this.hold();
    return this.showHold() && h !== null && !h.released && !h.empty && (this.canAsk() || this.canDecide());
  });

  protected readonly returning = signal(false);
  protected readonly returnReason = signal('');
  protected readonly returnInvalid = signal(false);

  /** True when this user has no act on the page at all, so the page says so rather than showing a blank. */
  protected readonly nothingToDo = computed(
    () =>
      this.steps().length === 0 &&
      !this.snagActsOpen() &&
      !this.canRaiseCallback() &&
      !this.canTerminate() &&
      !this.holdActionable() &&
      !this.callbacks().some((row) => !row.isResolved && this.caps().callback),
  );

  protected readonly ownerOptions = computed<readonly SelectOption[]>(() => [
    { value: 'Kaff', label: this.i18n.t('closure.snag.owner.Kaff') },
    ...(this.subcontractors().length > 0
      ? [{ value: 'Subcontractor', label: this.i18n.t('closure.snag.owner.Subcontractor') }]
      : []),
  ]);

  protected readonly priorityOptions = computed<readonly SelectOption[]>(() => [
    { value: 'Minor', label: this.i18n.t('closure.snag.priority.Minor') },
    { value: 'Major', label: this.i18n.t('closure.snag.priority.Major') },
  ]);

  protected readonly subcontractorOptions = computed<readonly SelectOption[]>(() => [
    { value: '', label: this.i18n.t('closure.snag.choose_subcontractor') },
    ...this.subcontractors().map((row) => ({ value: row.id, label: row.name })),
  ]);

  constructor() {
    // A required input is set after construction: read it in an effect, reload untracked (NG0950).
    effect(() => {
      this.projectId();
      untracked(() => void this.reload());
    });
  }

  protected async reload(): Promise<void> {
    this.loading.set(this.project() === null);
    this.failure.set(null);
    try {
      this.project.set(await this.projectsApi.get(this.projectId()));
    } catch (error) {
      this.project.set(null);
      this.failure.set(toProblem(error).messageKey);
      return;
    } finally {
      this.loading.set(false);
    }
    await Promise.all([this.loadSummary(), this.loadSnags(), this.loadCallbacks(), this.loadHold(), this.loadReleasedHold()]);
  }

  private async loadSummary(): Promise<void> {
    try {
      this.summary.set(await this.api.getClosure(this.projectId()));
    } catch {
      this.summary.set(null);
    }
    const open = this.summary()?.openExtracts ?? [];
    const list = open.length === 0 ? [] : await this.extractsApi.list(this.projectId()).catch(() => []);
    this.extractIds.set(new Map(list.map((row) => [row.number, row.id])));
  }

  private async loadSnags(): Promise<void> {
    try {
      this.snags.set(await this.api.listSnags(this.projectId()));
    } catch {
      this.snags.set([]);
    }
  }

  private async loadCallbacks(): Promise<void> {
    try {
      this.callbacks.set(await this.api.listCallbacks(this.projectId()));
    } catch {
      this.callbacks.set([]);
    }
  }

  /** The hold figure is read only for a user who may release it: the proof read is Finance's and the Owner's. */
  private async loadHold(): Promise<void> {
    this.proof.set(null);
    this.proofFailed.set(false);
    if (!this.showHold()) {
      return;
    }
    try {
      this.proof.set(await this.proofApi.get(this.projectId()));
    } catch {
      this.proofFailed.set(true);
    }
  }

  private async loadReleasedHold(): Promise<void> {
    const project = this.project();
    const held = this.auth.current()?.projects.find((row) => row.projectId === this.projectId())?.permissions ?? [];
    if (project === null || project.contractType !== 'LumpSum' || !held.includes('ExtractCollect')
      || !['Handover', 'UnderWarranty', 'Closed', 'Terminated'].includes(project.status)) {
      this.releasedHold.set(null);
      return;
    }
    this.releasedHold.set(await this.chainApi.releasedHold(this.projectId()).catch(() => null));
  }

  protected openHoldCollection(): void {
    this.holdCollectError.set(null);
    this.holdCollected.set(false);
    this.collectingHold.set(true);
  }

  protected async collectHold(): Promise<void> {
    const request = this.holdForm()?.request();
    if (!request) {
      return;
    }
    this.busy.set(true);
    this.holdCollectError.set(null);
    try {
      await this.chainApi.collectReleasedHold(this.projectId(), request);
      this.collectingHold.set(false);
      this.holdCollected.set(true);
      await this.loadReleasedHold();
    } catch (error) {
      this.holdCollectError.set(toProblem(error).messageKey);
    } finally {
      this.busy.set(false);
    }
  }

  protected snagOwnerName(snag: Snag): string {
    if (snag.ownerKind === 'Kaff') {
      return this.i18n.t('closure.snag.owner.Kaff');
    }
    return (
      this.subcontractors().find((row) => row.id === snag.subcontractorId)?.name ??
      this.i18n.t('closure.snag.owner.Subcontractor')
    );
  }

  protected readonly warrantyEnd = computed(() => {
    const project = this.project();
    return project?.status === 'UnderWarranty' && project.warrantyEndsOn ? this.day(project.warrantyEndsOn) : null;
  });

  protected day(value: string): string {
    return dmyOf(value);
  }

  // ---------- project acts ----------
  protected ask(act: ProjectAct): void {
    this.closePanels();
    this.confirming.set(act);
  }

  protected closePanels(): void {
    this.confirming.set(null);
    this.returning.set(false);
    this.terminating.set(false);
    this.terminationRefused.set(null);
    this.raisingSnag.set(false);
    this.resolvingSnag.set(null);
    this.raisingCallback.set(false);
    this.actionError.set(null);
    this.actionDetail.set(null);
    this.note.set(null);
  }

  protected confirm(act: ProjectAct): void {
    const id = this.projectId();
    const call: Record<ProjectAct, () => Promise<unknown>> = {
      'practical-completion': () => this.api.declarePracticalCompletion(id),
      handover: () => this.api.handOver(id),
      close: () => this.api.close(id),
      'hold-release': () => this.api.approveHoldRelease(id),
    };
    void this.act(call[act], DONE_KEYS[act]);
  }

  protected requestRelease(): void {
    void this.act(() => this.api.releaseHold(this.projectId()), 'closure.done.hold_requested');
  }

  protected openReturn(): void {
    this.closePanels();
    this.returnReason.set('');
    this.returnInvalid.set(false);
    this.returning.set(true);
  }

  protected onReturnReason(event: Event): void {
    this.returnReason.set((event.target as HTMLTextAreaElement).value);
    this.returnInvalid.set(false);
  }

  protected submitReturn(): void {
    const reason = this.returnReason().trim();
    if (reason === '') {
      this.returnInvalid.set(true);
      return;
    }
    void this.act(() => this.api.rejectHoldRelease(this.projectId(), reason), 'closure.done.hold_returned');
  }

  protected openTerminate(): void {
    this.closePanels();
    this.terminating.set(true);
  }

  protected async submitTermination(): Promise<void> {
    const reason = this.terminationReason().trim();
    if (reason === '') {
      this.terminationRefused.set('errors.closure.reason_required');
      return;
    }
    this.busy.set(true);
    try {
      await this.api.terminate(this.projectId(), reason);
    } catch (error) {
      // The server refuses every termination today; its own words say why (Q108).
      this.terminationRefused.set(toProblem(error).messageKey);
    } finally {
      this.busy.set(false);
    }
  }

  protected onTerminationReason(event: Event): void {
    this.terminationReason.set((event.target as HTMLTextAreaElement).value);
    this.terminationRefused.set(null);
  }

  // ---------- snags ----------
  protected async openSnagForm(): Promise<void> {
    this.closePanels();
    this.raisingSnag.set(true);
    if (this.subcontractors().length === 0) {
      try {
        this.subcontractors.set(await this.subcontractorsApi.list('active'));
      } catch {
        // A user who cannot read the subcontractor list raises Kaff-owned snags only.
        this.subcontractors.set([]);
      }
    }
  }

  protected onText(target: { set(value: string): void }, event: Event): void {
    target.set((event.target as HTMLInputElement | HTMLTextAreaElement).value);
    this.snagInvalid.set(false);
    this.resolutionInvalid.set(false);
    this.callbackInvalid.set(false);
  }

  protected submitSnag(): void {
    const description = this.snagDescription().trim();
    const location = this.snagLocation().trim();
    const owner = this.snagOwner() as SnagOwnerKind;
    const subcontractor = this.snagSubcontractor();
    if (description === '' || location === '' || (owner === 'Subcontractor' && subcontractor === '')) {
      this.snagInvalid.set(true);
      return;
    }
    void this.act(
      () =>
        this.api.raiseSnag(this.projectId(), {
          description,
          location,
          ownerKind: owner,
          subcontractorId: owner === 'Subcontractor' ? subcontractor : null,
          priority: this.snagPriority() as SnagPriority,
        }),
      'closure.done.snag_raised',
      () => {
        this.snagDescription.set('');
        this.snagLocation.set('');
        this.snagOwner.set('Kaff');
        this.snagSubcontractor.set('');
        this.snagPriority.set('Minor');
      },
    );
  }

  protected fixSnag(snag: Snag): void {
    void this.act(() => this.api.fixSnag(this.projectId(), snag.id), 'closure.done.snag_fixed');
  }

  protected verifySnag(snag: Snag): void {
    void this.act(() => this.api.verifySnag(this.projectId(), snag.id), 'closure.done.snag_verified');
  }

  protected openResolve(snag: Snag): void {
    this.closePanels();
    this.resolvingSnag.set(snag.id);
    this.resolutionKind.set('Absorbed');
    this.resolutionReason.set('');
  }

  protected resolutionOptions(snag: Snag): readonly SelectOption[] {
    return [
      ...(snag.ownerKind === 'Subcontractor'
        ? [{ value: 'DebitNote', label: this.i18n.t('closure.snag.resolution.DebitNote') }]
        : []),
      { value: 'Absorbed', label: this.i18n.t('closure.snag.resolution.Absorbed') },
    ];
  }

  protected submitResolution(snag: Snag): void {
    const reason = this.resolutionReason().trim();
    if (reason === '') {
      this.resolutionInvalid.set(true);
      return;
    }
    void this.act(
      () => this.api.resolveSnag(this.projectId(), snag.id, this.resolutionKind() as SnagResolutionKind, reason),
      'closure.done.snag_resolved',
    );
  }

  // ---------- callbacks ----------
  protected openCallbackForm(): void {
    this.closePanels();
    this.raisingCallback.set(true);
  }

  protected submitCallback(): void {
    const description = this.callbackDescription().trim();
    if (description === '') {
      this.callbackInvalid.set(true);
      return;
    }
    void this.act(
      () => this.api.raiseCallback(this.projectId(), description),
      'closure.done.callback_raised',
      () => this.callbackDescription.set(''),
    );
  }

  protected resolveCallback(callback: WarrantyCallback): void {
    void this.act(() => this.api.resolveCallback(this.projectId(), callback.id), 'closure.done.callback_resolved');
  }

  /** Runs one act, then re-reads where the project stands: a refusal can mean it moved under us. */
  private async act(call: () => Promise<unknown>, doneKey: string, afterSuccess?: () => void): Promise<void> {
    this.busy.set(true);
    try {
      await call();
      this.closePanels();
      afterSuccess?.();
      this.note.set(doneKey);
    } catch (error) {
      const problem = toProblem(error);
      this.closePanels();
      this.actionError.set(problem.messageKey);
      this.actionDetail.set(this.detailOf(problem.extensions));
    } finally {
      this.busy.set(false);
    }
    await this.reload();
  }

  /** The server names what stands in the way (the major snags, the unmet closing conditions); say them. */
  private detailOf(extensions: Readonly<Record<string, unknown>>): string | null {
    const snags = extensions['snags'];
    if (Array.isArray(snags) && snags.length > 0) {
      return this.i18n.t('closure.refusal.snags', {
        list: snags.map((n) => this.i18n.formatNumber(Number(n))).join(' - '),
      });
    }
    const unmet = extensions['unmet'];
    if (Array.isArray(unmet) && unmet.length > 0) {
      const names = unmet.map((name) => this.i18n.t(UNMET_KEYS[String(name)] ?? 'errors.unknown'));
      return this.i18n.t('closure.refusal.unmet', { list: names.join(' - ') });
    }
    return null;
  }
}

const DONE_KEYS: Readonly<Record<ProjectAct, string>> = {
  'practical-completion': 'closure.done.practical_completion',
  handover: 'closure.done.handover',
  close: 'closure.done.close',
  'hold-release': 'closure.done.hold_release',
};
