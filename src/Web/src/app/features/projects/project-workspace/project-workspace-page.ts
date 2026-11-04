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
import { Extract, ExtractSummary, ExtractsApi } from '../../../core/extracts/extracts.api';
import { contractTypeKey, projectWorkspaceStatusKey } from '../../../core/i18n/enum-keys';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { ProjectDetail, ProjectStatus, ProjectsApi } from '../../../core/projects/projects.api';
import { fractionToPercent } from '../../../core/catalogue/percent-wire';
import { extractNoActionKey } from '../../extracts/extract-display';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffFact, KaffFactList } from '../../../shared/kaff-fact/kaff-fact';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffGroupSection } from '../../../shared/kaff-group-section/kaff-group-section';
import { KaffIcon, KaffIconName } from '../../../shared/kaff-icon/kaff-icon';
import { KaffPartLink } from '../../../shared/kaff-part-link/kaff-part-link';
import { KaffProvenance } from '../../../shared/kaff-provenance/kaff-provenance';
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

/** One group's read. Each group is its own read, so one failing never blanks the page. */
type Read<T> =
  | { readonly state: 'loading' }
  | { readonly state: 'ok'; readonly data: T }
  /** The server answered 404: the project has no such record yet (a BOQ before one is drafted). */
  | { readonly state: 'none' }
  | { readonly state: 'failed'; readonly key: string };

const LOADING = { state: 'loading' } as const;

type PillTone = 'new' | 'overwrite' | 'error' | 'locked' | 'same';

/** The state pill is word, shape and colour (never colour alone); this is shape and colour. */
const STATUS_PILL: Readonly<Record<ProjectStatus, { tone: PillTone; icon: KaffIconName }>> = {
  Setup: { tone: 'same', icon: 'file' },
  Active: { tone: 'new', icon: 'check' },
  HandoverPending: { tone: 'overwrite', icon: 'info' },
  Handover: { tone: 'overwrite', icon: 'info' },
  UnderWarranty: { tone: 'overwrite', icon: 'info' },
  Closed: { tone: 'same', icon: 'lock' },
  Stopped: { tone: 'locked', icon: 'pause' },
  Terminated: { tone: 'error', icon: 'x' },
};

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
    KaffProvenance,
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
  /** The newest extract still in the chain, read in full for its server-computed `actions`. Null: none, or the read failed. */
  protected readonly pendingExtract = signal<Extract | null>(null);

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
    };
  });

  protected readonly groups = computed(() => {
    const project = this.project();
    return project === null ? [] : visibleGroups(project.contractType, this.caps());
  });

  protected readonly primary = computed(() => {
    const project = this.project();
    return project === null
      ? null
      : primaryAction(project.contractType, project.status, this.caps(), project.id, this.pending());
  });

  private readonly pending = computed<PendingExtract | null>(() => {
    const extract = this.pendingExtract();
    if (extract === null) {
      return null;
    }
    const a = extract.actions;
    // reviseEvidence is a correction, not the next step, so it does not take the slot.
    const hasAction =
      a.submitQcReport ||
      a.approveTechnicalOffice ||
      a.rejectAtTechnicalOffice ||
      a.applyDeductions ||
      a.rejectAtAccounts ||
      a.approveAsOwner ||
      a.rejectAtOwner ||
      a.issue;
    return { id: extract.id, number: extract.number, hasAction };
  });

  /** Why the pending extract offers this viewer nothing (the extract screen's own wording), else null. */
  protected readonly pendingNoActionKey = computed(() => {
    const extract = this.pendingExtract();
    return extract !== null && !this.pending()?.hasAction ? extractNoActionKey(extract) : null;
  });

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
    this.pendingExtract.set(null);
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
    const parts = this.groups().flatMap((group) => group.parts);
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
      await this.loadPending(list);
    } catch (error) {
      this.extracts.set({ state: 'failed', key: toProblem(error).messageKey });
    }
  }

  /** The newest extract not yet Issued or Collected. A failed read leaves the slot absent; the server still decides. */
  private async loadPending(list: readonly ExtractSummary[]): Promise<void> {
    const open = list.filter((row) => row.status !== 'Issued' && row.status !== 'Collected');
    const newest = open.reduce<ExtractSummary | null>((a, b) => (a === null || b.number > a.number ? b : a), null);
    if (newest === null) {
      return;
    }
    try {
      this.pendingExtract.set(await this.extractsApi.get(this.projectId(), newest.id));
    } catch {
      this.pendingExtract.set(null);
    }
  }

  protected jumpTo(event: Event, id: GroupId): void {
    event.preventDefault();
    document.getElementById(`g-${id}`)?.scrollIntoView({ block: 'start' });
  }

  protected intentKey(id: GroupId): string {
    const project = this.project();
    return GROUP_INTENT_KEY[project?.contractType ?? 'LumpSum'][id];
  }

  /** `"0.15"` (wire fraction) to a percent text; string shift, never `Number()` (D-135). */
  protected rate(fraction: string): string {
    return `${this.i18n.formatNumber(fractionToPercent(fraction))}%`;
  }

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
