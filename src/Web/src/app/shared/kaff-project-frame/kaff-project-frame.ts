import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterRenderEffect,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../core/api/problem-details';
import { AuthService } from '../../core/auth/auth.service';
import { contractTypeKey, projectWorkspaceStatusKey } from '../../core/i18n/enum-keys';
import { I18nService } from '../../core/i18n/i18n.service';
import { ProjectDetail, ProjectStage, ProjectTabs, ProjectsApi } from '../../core/projects/projects.api';
import { STATUS_PILL, shownStatus } from '../../features/projects/project-status-pill';
import { KaffBadge } from '../kaff-badge/kaff-badge';
import { KaffIcon } from '../kaff-icon/kaff-icon';
import { KaffStatusPill } from '../kaff-status-pill/kaff-status-pill';

export type FrameTab = keyof ProjectTabs;

const TAB_ORDER: readonly FrameTab[] = ['overview', 'boq', 'dailyLog', 'extracts', 'team'];
const STAGES: readonly ProjectStage[] = ['Boq', 'Execution', 'Handover'];
const STAGE_KEY: Readonly<Record<ProjectStage, string>> = {
  Boq: 'projects.frame.stage.boq',
  Execution: 'projects.frame.stage.execution',
  Handover: 'projects.frame.stage.handover',
};

/**
 * Tabs that belong to a stage not reached or to another contract type (README "The project frame": a tab of
 * a stage not reached is absent). Facts of the project, not of the viewer: the viewer's side is `tabs`.
 */
function notReached(project: ProjectDetail): readonly FrameTab[] {
  if (!project.isSigned || project.isArchived) {
    return ['dailyLog', 'extracts'];
  }
  switch (project.contractType) {
    case 'CostPlus':
      return ['boq', 'extracts'];
    case 'Design':
      return ['boq', 'dailyLog', 'extracts'];
    default:
      return [];
  }
}

/**
 * The project frame (`.design/v2/team-a/README.md` "The project frame"): back link, title and status pill,
 * client and contract type, stage line, and the tab bar. One tag above the content of every project screen:
 * `<kaff-project-frame [projectId]="id" current="boq">...the screen...</kaff-project-frame>`.
 *
 * It reads `GET /api/projects/:id` itself and hands the project to the screen through `loaded`, so the
 * screen does not read it twice. **The server decides the tabs** (D-218 item 2): a tab whose flag is
 * false is absent, never greyed. A failed or refused read draws only the back link and a neutral title,
 * and the screen shows its own refusal in the content. The primary action is never drawn here; the shell
 * places it.
 */
@Component({
  selector: 'kaff-project-frame',
  imports: [RouterLink, KaffBadge, KaffIcon, KaffStatusPill],
  templateUrl: './kaff-project-frame.html',
  styleUrl: './kaff-project-frame.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffProjectFrame {
  private readonly api = inject(ProjectsApi);
  private readonly auth = inject(AuthService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly i18n = inject(I18nService);

  readonly projectId = input.required<string>();
  readonly current = input.required<FrameTab>();

  /** The project, once read. */
  readonly loaded = output<ProjectDetail>();
  /** The read failed or was refused: the problem's message key. */
  readonly failed = output<string>();

  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly state = signal<'loading' | 'ok' | 'failed'>('loading');

  protected readonly pill = STATUS_PILL;
  protected readonly shown = shownStatus;
  protected readonly contractTypeKey = contractTypeKey;
  protected readonly statusKey = projectWorkspaceStatusKey;
  protected readonly stages = STAGES.map((id) => ({ id, key: STAGE_KEY[id] }));

  /** Tabs the server allows, in the frame's order. */
  protected readonly tabs = computed(() => {
    const project = this.project();
    if (project === null) {
      return [];
    }
    const hidden = notReached(project);
    return TAB_ORDER.filter((tab) => project.tabs[tab] && !hidden.includes(tab));
  });

  /** Index of the current stage; -1 on an archived project (nothing is current). */
  protected readonly stageIndex = computed(() => {
    const project = this.project();
    return project === null || project.isArchived ? -1 : STAGES.indexOf(project.stage);
  });

  /** The meta line is for roles that see money or the BOQ; the junior site engineer gets none. */
  protected readonly showMeta = computed(() => {
    const allowed = this.project()?.tabs;
    return allowed !== undefined && (allowed.extracts || allowed.boq);
  });

  /** Convenience: the client screen needs `ClientManage`; a link the route would turn away is not drawn. */
  protected readonly clientLinked = computed(() => this.auth.current()?.permissions.includes('ClientManage') ?? false);

  constructor() {
    effect(() => {
      this.projectId();
      untracked(() => void this.reload());
    });
    // On a phone the bar scrolls inside itself: keep the current tab in view (the first tab is already).
    afterRenderEffect(() => {
      this.tabs();
      if (this.current() !== 'overview') {
        this.host.nativeElement
          .querySelector('.tab[aria-current="page"]')
          ?.scrollIntoView?.({ inline: 'center', block: 'nearest' });
      }
    });
  }

  /** Reads the project again (after a lifecycle act, or a retry). The frame keeps its shape meanwhile. */
  async reload(): Promise<void> {
    this.state.set('loading');
    try {
      const project = await this.api.get(this.projectId());
      this.project.set(project);
      this.state.set('ok');
      this.loaded.emit(project);
    } catch (error) {
      this.project.set(null);
      this.state.set('failed');
      this.failed.emit(toProblem(error).messageKey);
    }
  }

  /** Where a tab goes. The team tab is the overview route with `?tab=team`. */
  protected link(tab: FrameTab): readonly string[] {
    const id = this.projectId();
    switch (tab) {
      case 'boq':
        return ['/projects', id, 'boq'];
      case 'dailyLog':
        return ['/projects', id, 'daily-log'];
      case 'extracts':
        return ['/projects', id, 'extracts'];
      default:
        return ['/projects', id];
    }
  }

  protected query(tab: FrameTab): Record<string, string> | null {
    return tab === 'team' ? { tab: 'team' } : null;
  }
}
