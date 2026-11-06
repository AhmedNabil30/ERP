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
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';
import { Boq, BoqsApi } from '../../../core/boqs/boqs.api';
import { ExtractSummary, ExtractsApi, WaitingOnItem } from '../../../core/extracts/extracts.api';
import { assignmentLevelKey, contractTypeKey, projectWorkspaceStatusKey, roleKey } from '../../../core/i18n/enum-keys';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { ProjectDetail, ProjectProgress, ProjectsApi, TeamMember } from '../../../core/projects/projects.api';
import { closureCapsOf } from '../../closure/closure-display';
import { STATUS_PILL } from '../project-status-pill';
import { fractionToPercent } from '../../../core/catalogue/percent-wire';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffFact, KaffFactList } from '../../../shared/kaff-fact/kaff-fact';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffGroupSection } from '../../../shared/kaff-group-section/kaff-group-section';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffPartLink } from '../../../shared/kaff-part-link/kaff-part-link';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import {
  Capabilities,
  GROUP_ICON,
  GROUP_INTENT_KEY,
  GROUP_TITLE_KEY,
  GroupId,
  PendingExtract,
  primaryAction,
  visibleGroups,
} from './workspace-declaration';
import { ProgressView, WaitingView, progressViewOf, waitingViewOf } from './workspace-reading';

/** One group's read. Each group is its own read, so one failing never blanks the page. */
type Read<T> =
  | { readonly state: 'loading' }
  | { readonly state: 'ok'; readonly data: T }
  /** The server answered 404: the project has no such record yet (a BOQ before one is drafted). */
  | { readonly state: 'none' }
  | { readonly state: 'failed'; readonly key: string };

const LOADING = { state: 'loading' } as const;

/** The progress read: a Design project answers 409, which is "cannot be measured yet", not a failure and not 0%. */
type ProgressRead = Read<ProjectProgress> | { readonly state: 'unavailable' };

/**
 * The project workspace: the hub a user lands on after opening a project from the list
 * (`.design/v2/team-a/project.html`). Five groups (understand, quantify, execute, pay, organise), what
 * each holds declared per contract type in `workspace-declaration.ts`; this page is one component for all
 * three types. It reaches the existing screens (BOQ, daily log, extracts, proof, day labour) from the
 * group each belongs to.
 *
 * **Every read is separate.** The header reads `GET /api/projects/:id`; the BOQ and the extract list are
 * their own reads, so a refusal or failure in one group is a banner in that group and the rest stays.
 * **The header ships the state alone**: no health tag (متعثرة, تم تأجيلها have no data, D-044 §7).
 * **Money crosses as strings (D-135)** and is shown through `kaff-figure`; a null contract value is its
 * own variant, never 0.00. **Convenience, not the control**: the site engineer sees no money and no pay
 * group, and the server decides every read regardless.
 */
@Component({
  selector: 'kaff-project-workspace-page',
  imports: [
    RouterLink,
    KaffBadge,
    KaffBanner,
    KaffButton,
    KaffFact,
    KaffFactList,
    KaffFigure,
    KaffGroupSection,
    KaffIcon,
    KaffPartLink,
    KaffState,
    KaffStatusPill,
  ],
  templateUrl: './project-workspace-page.html',
  styleUrl: './project-workspace-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectWorkspacePage {
  private readonly projectsApi = inject(ProjectsApi);
  private readonly boqsApi = inject(BoqsApi);
  private readonly extractsApi = inject(ExtractsApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly headerActions = inject(HeaderActionsService);

  protected readonly i18n = inject(I18nService);
  protected readonly contractTypeKey = contractTypeKey;
  protected readonly projectWorkspaceStatusKey = projectWorkspaceStatusKey;
  protected readonly groupIcon = GROUP_ICON;
  protected readonly groupTitleKey = GROUP_TITLE_KEY;
  protected readonly pill = STATUS_PILL;

  /** Bound from `:projectId` by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();

  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly boq = signal<Read<Boq>>(LOADING);
  protected readonly extracts = signal<Read<readonly ExtractSummary[]>>(LOADING);
  protected readonly progress = signal<ProgressRead>(LOADING);
  protected readonly team = signal<Read<readonly TeamMember[]>>(LOADING);
  /** What the open extracts wait for. Null: nothing open, not a Lump Sum, or the read failed (the server still decides). */
  private readonly waitingItems = signal<readonly WaitingOnItem[] | null>(null);

  private readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');

  /** What this session may open on this project. Convenience; the server decides each read. */
  protected readonly caps = computed<Capabilities>(() => {
    const session = this.auth.current();
    const entry = session?.projects.find((row) => row.projectId === this.projectId());
    const has = (permission: string): boolean => entry?.permissions.includes(permission) ?? false;
    return {
      seesMoney: session !== null && session.role !== 'SiteEngineer',
      canReadProject: has('ProjectRead'),
      canReadBoq: has('ProjectManage'),
      canWriteDailyLog: has('DailyLogWrite'),
      canManageDayLabour: has('DayLabourSiteManage'),
      canReadProof: session?.permissions.includes('TreasuryReadCompany') ?? false,
      closure: closureCapsOf(entry?.permissions),
    };
  });

  /** How the progress read is drawn, for this viewer. */
  protected readonly progressView = computed<ProgressView>(() => {
    const read = this.progress();
    switch (read.state) {
      case 'loading':
        return { kind: 'loading' };
      case 'failed':
        return { kind: 'failed', key: read.key };
      case 'unavailable':
      case 'none':
        return { kind: 'unavailable' };
      case 'ok':
        return this.withoutMoneyForThoseWhoSeeNone(progressViewOf(read.data, this.i18n.locale()));
    }
  });

  /**
   * The groups this viewer sees. A progress part with nothing to say is dropped, and with it a group that
   * would be left printing a question over an empty body. "Cannot be measured" is only worth a group of
   * its own when the viewer also sees the contract facts beside it.
   */
  protected readonly groups = computed(() => {
    const project = this.project();
    if (project === null) {
      return [];
    }
    const view = this.progressView().kind;
    const noProgress = view === 'none' || (view === 'unavailable' && !this.caps().seesMoney);
    return visibleGroups(project.contractType, this.caps())
      .map((group) => ({ ...group, parts: group.parts.filter((part) => part !== 'progress' || !noProgress) }))
      .filter((group) => group.parts.length > 0);
  });

  /** The Babs with work in progress, for the site question. Empty when there is no bab list. */
  protected readonly activeBabs = computed(() => {
    const view = this.progressView();
    return view.kind === 'bar' ? view.babs.filter((bab) => bab.isActive) : [];
  });

  protected readonly activeBabNames = computed(() => this.list(this.activeBabs().map((bab) => bab.name), 'conjunction'));

  protected readonly hasBabs = computed(() => {
    const view = this.progressView();
    return view.kind === 'bar' && view.babs.length > 0;
  });

  /** The newest open extract and who it waits for, from this viewer's side. */
  protected readonly waiting = computed<WaitingView | null>(() => {
    const items = this.waitingItems();
    return items === null ? null : waitingViewOf(items, this.auth.current()?.userId ?? null);
  });

  /** The holder roles as one phrase: "Technical Office or Owner". */
  protected readonly waitingWho = computed(() => {
    const w = this.waiting();
    if (w === null) {
      return '';
    }
    const labels = w.roles.map((r) => this.i18n.t(r.supervisor ? 'projects.workspace.waiting.supervisor' : roleKey(r.role)));
    return this.list(labels, 'disjunction');
  });

  protected readonly holderNames = computed(() => this.list(this.waiting()?.holderNames ?? [], 'conjunction'));
  protected readonly barredNames = computed(() => this.list(this.waiting()?.barredNames ?? [], 'conjunction'));

  protected readonly primary = computed(() => {
    const project = this.project();
    return project === null
      ? null
      : primaryAction(project.contractType, project.status, this.caps(), project.id, this.pending());
  });

  /** The server named this viewer in `holders` of a step that is taken on the extract screen. */
  private readonly pending = computed<PendingExtract | null>(() => {
    const w = this.waiting();
    return w === null ? null : { id: w.extractId, number: w.number, hasAction: w.viewerIsHolder && w.hasScreen };
  });

  /** Separation of duties, in the extract screen's own words, when it is the reason this viewer is not offered the step. */
  protected readonly barredKey = computed(() => (this.waiting()?.viewerIsBarred ? 'extract.no_action.same_person' : null));

  /** The newest extract by number, for the pay group's one-line summary. */
  protected readonly latestExtract = computed(() => {
    const read = this.extracts();
    if (read.state !== 'ok' || read.data.length === 0) {
      return null;
    }
    return read.data.reduce((a, b) => (b.number > a.number ? b : a));
  });

  constructor() {
    // A required input is set after construction: read it in an effect, reload untracked (see BoqPage).
    effect(() => {
      this.projectId();
      untracked(() => void this.reload());
    });
    effect(() => this.headerActions.set(this.primary() !== null ? (this.primaryTemplate() ?? null) : null));
    inject(DestroyRef).onDestroy(() => this.headerActions.set(null));
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    this.boq.set(LOADING);
    this.extracts.set(LOADING);
    this.progress.set(LOADING);
    this.team.set(LOADING);
    this.waitingItems.set(null);
    try {
      this.project.set(await this.projectsApi.get(this.projectId()));
    } catch (error) {
      this.project.set(null);
      this.failure.set(toProblem(error).messageKey);
      return;
    } finally {
      this.loading.set(false);
    }
    // Not awaited: the groups load beside each other and beside the page.
    const loaded = this.project();
    const parts = loaded === null ? [] : visibleGroups(loaded.contractType, this.caps()).flatMap((group) => group.parts);
    void this.loadProgress();
    void this.loadTeam();
    if (loaded?.contractType === 'LumpSum') {
      void this.loadWaiting();
    }
    if (parts.includes('boq')) {
      void this.loadBoq();
    }
    if (parts.includes('extracts')) {
      void this.loadExtracts();
    }
  }

  protected async loadBoq(): Promise<void> {
    this.boq.set(LOADING);
    try {
      this.boq.set({ state: 'ok', data: await this.boqsApi.get(this.projectId()) });
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.boq.set({ state: 'none' });
        return;
      }
      this.boq.set({ state: 'failed', key: toProblem(error).messageKey });
    }
  }

  protected async loadExtracts(): Promise<void> {
    this.extracts.set(LOADING);
    try {
      const list = await this.extractsApi.list(this.projectId());
      this.extracts.set({ state: 'ok', data: list });
    } catch (error) {
      this.extracts.set({ state: 'failed', key: toProblem(error).messageKey });
    }
  }

  protected async loadProgress(): Promise<void> {
    this.progress.set(LOADING);
    try {
      this.progress.set({ state: 'ok', data: await this.projectsApi.progress(this.projectId()) });
    } catch (error) {
      // 409 billing.calculator_not_implemented: nothing stores stage completion yet. Honest, not 0%.
      if (error instanceof HttpErrorResponse && error.status === 409) {
        this.progress.set({ state: 'unavailable' });
        return;
      }
      this.progress.set({ state: 'failed', key: toProblem(error).messageKey });
    }
  }

  protected async loadTeam(): Promise<void> {
    this.team.set(LOADING);
    try {
      this.team.set({ state: 'ok', data: await this.projectsApi.team(this.projectId()) });
    } catch (error) {
      this.team.set({ state: 'failed', key: toProblem(error).messageKey });
    }
  }

  /** A failed read leaves the strip and the slot absent; the server still decides every action. */
  private async loadWaiting(): Promise<void> {
    try {
      this.waitingItems.set(await this.extractsApi.waitingOn(this.projectId()));
    } catch {
      this.waitingItems.set(null);
    }
  }

  /** Convenience over the server's own redaction: a viewer who sees no money is shown no money, even from a mistaken payload. */
  private withoutMoneyForThoseWhoSeeNone(view: ProgressView): ProgressView {
    if (this.caps().seesMoney) {
      return view;
    }
    if (view.kind === 'money') {
      return { kind: 'none' };
    }
    return view.kind === 'bar' ? { ...view, executedValue: null, contractValue: null } : view;
  }

  private list(items: readonly string[], type: 'conjunction' | 'disjunction'): string {
    return new Intl.ListFormat(this.i18n.locale(), { style: 'long', type }).format(items);
  }

  protected jumpTo(event: Event, id: GroupId): void {
    event.preventDefault();
    document.getElementById(`g-${id}`)?.scrollIntoView({ block: 'start' });
  }

  protected intentKey(id: GroupId): string {
    const project = this.project();
    // Without the contract facts the question "what is this contract" has no answer for this viewer.
    if (id === 'understand' && !this.caps().seesMoney) {
      return 'projects.workspace.vocab.intent.understand_progress';
    }
    return GROUP_INTENT_KEY[project?.contractType ?? 'LumpSum'][id];
  }

  /** `"0.15"` (wire fraction) to a percent text; string shift, never `Number()` (D-135). */
  protected rate(fraction: string): string {
    return `${this.i18n.formatNumber(fractionToPercent(fraction))}%`;
  }

  protected readonly roleLabelKey = roleKey;
  protected readonly levelLabelKey = assignmentLevelKey;

  protected day(value: string): string {
    const [year = 0, month = 1, day = 1] = value.split('-').map(Number);
    return this.i18n.formatDate(new Date(year, month - 1, day), { dateStyle: 'medium' });
  }

  protected async onPrimary(): Promise<void> {
    const action = this.primary();
    if (action !== null) {
      await this.router.navigate([...action.link]);
    }
  }
}
