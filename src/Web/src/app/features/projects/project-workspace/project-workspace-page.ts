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
import { AuthApi } from '../../../core/auth/auth.api';
import { AuthService } from '../../../core/auth/auth.service';
import { BoqsApi } from '../../../core/boqs/boqs.api';
import { fractionToPercent } from '../../../core/catalogue/percent-wire';
import { ExtractsApi, WaitingOnItem } from '../../../core/extracts/extracts.api';
import { assignmentLevelKey, contractTypeKey, roleKey } from '../../../core/i18n/enum-keys';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { ProjectDetail, ProjectProgress, ProjectsApi, TeamMember } from '../../../core/projects/projects.api';
import { closureCapsOf } from '../../closure/closure-display';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffFact, KaffFactList } from '../../../shared/kaff-fact/kaff-fact';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffInlineConfirm } from '../../../shared/kaff-inline-confirm/kaff-inline-confirm';
import { KaffProjectFrame } from '../../../shared/kaff-project-frame/kaff-project-frame';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { Capabilities, PendingExtract, primaryAction } from './workspace-declaration';
import { BabRow, ProgressView, WaitingView, progressViewOf, waitingViewOf } from './workspace-reading';

/** One read. Each is its own, so one failing never blanks the page. */
type Read<T> =
  | { readonly state: 'loading' }
  | { readonly state: 'ok'; readonly data: T }
  | { readonly state: 'failed'; readonly key: string };

const LOADING = { state: 'loading' } as const;

/** The default design fee per square metre (AC-417-K); the server computes the stored value, this only previews it. */
const DESIGN_RATE_PER_SQUARE_METRE = 450n;
const AREA_PATTERN = /^(\d+)(?:\.(\d{1,4}))?$/;

/** Area x rate in exact integer arithmetic (D-135: no floats near money). Null when the area is not a positive decimal. */
export function designFeeOf(area: string): string | null {
  const match = AREA_PATTERN.exec(area.trim());
  if (match === null) {
    return null;
  }
  const scaled = BigInt(match[1] + (match[2] ?? '').padEnd(4, '0'));
  if (scaled === 0n) {
    return null;
  }
  const fee = (scaled * DESIGN_RATE_PER_SQUARE_METRE).toString().padStart(5, '0');
  return `${fee.slice(0, -4)}.${fee.slice(-4)}`;
}

/** The progress read: a Design project answers 409, which is "cannot be measured yet", not a failure and not 0%. */
type ProgressRead = Read<ProjectProgress> | { readonly state: 'unavailable' };

/**
 * The project workspace's overview and team tab, inside the project frame
 * (`.design/v2/team-a/project.html`, D-218). The frame draws the title, stage line and tabs from the
 * server's answer; this page draws the pane under it, by contract type and by what the server sent:
 * the hero (percent done, and the three contract figures when the caller may read money), the babs,
 * the money figures, the contract terms, the last daily log. A figure the server did not send is not drawn.
 *
 * **Every read is separate**: the project (the frame), the progress, the extract that waits, the team.
 * **Money crosses as strings (D-135)** and is shown through `kaff-figure`. **Convenience, not the
 * control**: the server decides every read and every tab regardless.
 */
@Component({
  selector: 'kaff-project-workspace-page',
  imports: [
    RouterLink,
    KaffBanner,
    KaffButton,
    KaffFact,
    KaffFactList,
    KaffFigure,
    KaffIcon,
    KaffInlineConfirm,
    KaffProjectFrame,
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
  private readonly authApi = inject(AuthApi);
  private readonly router = inject(Router);
  private readonly headerActions = inject(HeaderActionsService);

  protected readonly i18n = inject(I18nService);
  protected readonly contractTypeKey = contractTypeKey;
  protected readonly roleLabelKey = roleKey;
  protected readonly levelLabelKey = assignmentLevelKey;

  /** Bound from `:projectId` by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();
  /** Bound from `?tab=`: `team` opens the team tab, anything else the overview. */
  readonly tab = input<string | undefined>(undefined);

  private readonly frame = viewChild(KaffProjectFrame);
  private readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');

  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly failure = signal<string | null>(null);
  protected readonly progress = signal<ProgressRead>(LOADING);
  protected readonly team = signal<Read<readonly TeamMember[]>>(LOADING);
  /** What the open extracts wait for. Null: nothing open, not a Lump Sum, or the read failed (the server still decides). */
  private readonly waitingItems = signal<readonly WaitingOnItem[] | null>(null);
  protected readonly termsOpen = signal(false);

  /** The team tab, when the server lets this viewer open it. */
  protected readonly isTeam = computed(() => this.tab() === 'team' && this.project()?.tabs.team === true);
  protected readonly current = computed(() => (this.isTeam() ? 'team' : 'overview'));

  /** What this session may use on this project. Convenience; the server decides each read. */
  protected readonly caps = computed<Capabilities & { readonly canManage: boolean }>(() => {
    const project = this.project();
    const entry = this.auth.current()?.projects.find((row) => row.projectId === this.projectId());
    return {
      seesMoney: project?.tabs.extracts ?? false,
      canReadBoq: project?.tabs.boq ?? false,
      canWriteDailyLog: project?.tabs.dailyLog ?? false,
      canManage: entry?.permissions.includes('ProjectManage') ?? false,
      closure: closureCapsOf(entry?.permissions),
    };
  });

  /** The figures the server sent for money. Absent for a caller without `ProjectMoneyRead`. */
  protected readonly teamRows = computed(() => {
    const read = this.team();
    return read.state === 'ok' ? read.data : [];
  });

  protected readonly money = computed(() => this.project()?.money ?? null);

  protected readonly progressView = computed<ProgressView>(() => {
    const read = this.progress();
    switch (read.state) {
      case 'loading':
        return { kind: 'loading' };
      case 'failed':
        return { kind: 'failed', key: read.key };
      case 'unavailable':
        return { kind: 'unavailable' };
      case 'ok':
        return progressViewOf(read.data, this.i18n.locale());
    }
  });

  /** The babs with their status word, as the mock draws them. */
  protected readonly babs = computed(() => {
    const view = this.progressView();
    return view.kind === 'bar' ? view.babs.map((bab) => ({ ...bab, status: babStatus(bab) })) : [];
  });

  /** Executed on site: the progress read's value, which the server withholds from a caller without money. */
  protected readonly executedValue = computed(() => {
    const view = this.progressView();
    return view.kind === 'bar' ? view.executedValue : null;
  });

  /** The newest open extract and who it waits for, from this viewer's side. Only for a caller who reads extract money. */
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
    return new Intl.ListFormat(this.i18n.locale(), { style: 'long', type: 'disjunction' }).format(labels);
  });

  protected readonly primary = computed(() => {
    const project = this.project();
    if (project === null) {
      return null;
    }
    if (project.isArchived) {
      return this.caps().canManage ? { labelKey: 'projects.frame.action.reopen', act: 'reopen' as const } : null;
    }
    return primaryAction(project.contractType, project.status, this.caps(), project.id, this.pending(), project.isSigned);
  });

  /** The server named this viewer in `holders` of a step that is taken on the extract screen. */
  private readonly pending = computed<PendingExtract | null>(() => {
    const w = this.waiting();
    return w === null ? null : { id: w.extractId, number: w.number, hasAction: w.viewerIsHolder && w.hasScreen };
  });

  constructor() {
    effect(() => {
      this.projectId();
      untracked(() => {
        this.project.set(null);
        this.failure.set(null);
      });
    });
    // The team is read only when its tab is open, and again when the project is read again.
    effect(() => {
      if (this.isTeam()) {
        untracked(() => void this.loadTeam());
      }
    });
    effect(() => this.headerActions.set(this.primary() !== null ? (this.primaryTemplate() ?? null) : null, 'primary'));
    inject(DestroyRef).onDestroy(() => this.headerActions.clear());
  }

  /** The frame read the project: start the reads this overview needs. Not awaited, so they load beside each other. */
  protected onLoaded(project: ProjectDetail): void {
    this.failure.set(null);
    this.project.set(project);
    this.progress.set(LOADING);
    this.waitingItems.set(null);
    if (project.isSigned && !project.isArchived) {
      void this.loadProgress();
      if (project.contractType === 'LumpSum' && project.tabs.extracts) {
        void this.loadWaiting();
      }
    }
  }

  protected onFailed(key: string): void {
    this.project.set(null);
    this.failure.set(key);
  }

  protected async retry(): Promise<void> {
    await this.frame()?.reload();
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

  /** A failed read leaves the line and the slot absent; the server still decides every action. */
  private async loadWaiting(): Promise<void> {
    try {
      this.waitingItems.set(await this.extractsApi.waitingOn(this.projectId()));
    } catch {
      this.waitingItems.set(null);
    }
  }

  /** `"0.15"` (wire fraction) to a percent text; string shift, never `Number()` (D-135). */
  protected rate(fraction: string): string {
    return `${this.i18n.formatNumber(fractionToPercent(fraction))}%`;
  }

  /** The one percent format on this screen: at most two decimals, the hero and the babs alike. */
  protected pct(text: string): string {
    return `${this.i18n.formatNumber(text, { maximumFractionDigits: 2 })}%`;
  }

  protected day(value: string): string {
    const [year = 0, month = 1, day = 1] = value.split('-').map(Number);
    return this.i18n.formatDate(new Date(year, month - 1, day), { dateStyle: 'medium' });
  }

  protected initial(name: string): string {
    return [...name][0] ?? '';
  }

  // ---- archive and design-to-execution (KAFF-417, KAFF-415) ---------------------------------------
  /** Convenience; the server checks `ProjectManage` (and `ProjectCreate` for converting) on every call. */
  protected readonly canManage = computed(() => this.caps().canManage);
  protected readonly canConvert = computed(() => {
    const project = this.project();
    return (
      project !== null &&
      project.contractType === 'Design' &&
      project.conversionOffered === true &&
      !project.executionProjectId &&
      this.canManage() &&
      (this.auth.current()?.permissions.includes('ProjectCreate') ?? false)
    );
  });
  // ---- sign a Design project (KAFF-417 AC-417-K): no BOQ, so the area is entered here ---------------
  protected readonly canSignDesign = computed(() => {
    const project = this.project();
    return (
      project !== null &&
      project.contractType === 'Design' &&
      !project.signedOn &&
      !project.isArchived &&
      this.canManage()
    );
  });
  protected readonly area = signal('');
  protected readonly areaAttempted = signal(false);
  protected readonly confirmingSign = signal(false);
  protected readonly designFee = computed(() => designFeeOf(this.area()));
  protected readonly areaError = computed(() =>
    this.areaAttempted() && this.designFee() === null ? 'projects.sign_design.error.area' : null,
  );
  protected readonly signDesignDetail = computed(() => {
    const fee = this.designFee();
    return fee === null ? null : this.i18n.t('projects.sign_design.confirm_detail', { fee: this.i18n.formatAmount(fee) });
  });

  protected onAreaInput(event: Event): void {
    this.area.set((event.target as HTMLInputElement).value);
    this.confirmingSign.set(false);
  }

  protected askSignDesign(): void {
    this.areaAttempted.set(true);
    this.lifecycleError.set(null);
    this.confirmingSign.set(this.designFee() !== null);
  }

  protected async confirmSignDesign(): Promise<void> {
    if (this.designFee() === null) {
      return;
    }
    this.lifecycleBusy.set(true);
    this.lifecycleError.set(null);
    try {
      await this.boqsApi.sign(this.projectId(), { areaSquareMetres: this.area().trim() });
      await this.frame()?.reload();
    } catch (error) {
      this.lifecycleError.set(toProblem(error).messageKey);
    } finally {
      this.confirmingSign.set(false);
      this.lifecycleBusy.set(false);
    }
  }

  protected readonly lifecycleError = signal<string | null>(null);
  protected readonly lifecycleBusy = signal(false);
  protected readonly converting = signal(false);
  protected readonly convertName = signal('');
  protected readonly convertType = signal<'LumpSum' | 'CostPlus'>('LumpSum');
  protected readonly convertTypes = [
    { value: 'LumpSum' as const, labelKey: contractTypeKey('LumpSum') },
    { value: 'CostPlus' as const, labelKey: contractTypeKey('CostPlus') },
  ];

  protected async reopen(): Promise<void> {
    this.lifecycleBusy.set(true);
    this.lifecycleError.set(null);
    try {
      await this.projectsApi.reopen(this.projectId());
      await this.frame()?.reload();
    } catch (error) {
      this.lifecycleError.set(toProblem(error).messageKey);
    } finally {
      this.lifecycleBusy.set(false);
    }
  }

  protected openConvert(): void {
    this.convertName.set(this.i18n.t('projects.convert.default_name', { name: this.project()?.name ?? '' }));
    this.lifecycleError.set(null);
    this.converting.set(true);
  }

  protected async confirmConvert(): Promise<void> {
    const name = this.convertName().trim();
    if (name.length === 0) {
      this.lifecycleError.set('errors.project.name_required');
      return;
    }
    this.lifecycleBusy.set(true);
    this.lifecycleError.set(null);
    try {
      const created = await this.projectsApi.convertToExecution(this.projectId(), name, this.convertType());
      try {
        // The session lists the projects a caller may open and predates this one.
        this.auth.set(await this.authApi.me());
      } catch {
        // the navigation below still runs; a guard may send the user to the list
      }
      await this.router.navigate(['/projects', created.id]);
    } catch (error) {
      this.lifecycleError.set(toProblem(error).messageKey);
    } finally {
      this.lifecycleBusy.set(false);
    }
  }

  protected onNameInput(event: Event): void {
    this.convertName.set((event.target as HTMLInputElement).value);
  }

  protected async onPrimary(): Promise<void> {
    const action = this.primary();
    if (action?.act === 'reopen') {
      await this.reopen();
    } else if (action?.link) {
      await this.router.navigate([...action.link]);
    }
  }
}

/** The status word of one bab: finished at 100%, running when active or started, otherwise not started. */
function babStatus(bab: BabRow): 'finished' | 'running' | 'idle' {
  if ((bab.percent ?? 0) >= 100) {
    return 'finished';
  }
  return bab.isActive || (bab.percent ?? 0) > 0 ? 'running' : 'idle';
}
