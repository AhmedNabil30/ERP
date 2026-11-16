import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  TemplateRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';

import { AssignmentLevel } from '../../../core/auth/auth.service';
import { toProblem } from '../../../core/api/problem-details';
import { assignmentLevelKey, roleKey } from '../../../core/i18n/enum-keys';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectsApi, TeamCandidate, TeamMember, TeamRead } from '../../../core/projects/projects.api';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffChoiceGroup } from '../../../shared/kaff-choice-group/kaff-choice-group';
import { KaffConfirmDialog } from '../../../shared/kaff-confirm-dialog/kaff-confirm-dialog';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffSelect, SelectOption } from '../../../shared/kaff-select/kaff-select';
import { KaffState } from '../../../shared/kaff-state/kaff-state';

type TeamState =
  | { readonly state: 'loading' }
  | { readonly state: 'ok'; readonly read: TeamRead }
  | { readonly state: 'failed'; readonly key: string };

const LEVEL_CHOICES = [
  { value: 'Junior', labelKey: assignmentLevelKey('Junior') },
  { value: 'Supervisor', labelKey: assignmentLevelKey('Supervisor') },
];

/**
 * The team tab's pane (`.design/v2/team-a/project-team.html`, D-227 item 2, D-228 item 1, D-229): the
 * current members, an add sheet and a remove confirmation. **What the viewer may do comes from the
 * server's `canManage` and each member's `canRemove`, never from a role name**; the server decides
 * every call again. The primary «إضافة شخص» is declared through the shell slot by the workspace page,
 * which reads {@link declared}.
 */
@Component({
  selector: 'kaff-project-team-tab',
  imports: [
    KaffBadge,
    KaffBanner,
    KaffButton,
    KaffChoiceGroup,
    KaffConfirmDialog,
    KaffDetailPanel,
    KaffField,
    KaffIcon,
    KaffSelect,
    KaffState,
  ],
  templateUrl: './project-team-tab.html',
  styleUrl: './project-team-tab.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectTeamTab {
  private readonly api = inject(ProjectsApi);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly i18n = inject(I18nService);
  protected readonly roleLabelKey = roleKey;
  protected readonly levelLabelKey = assignmentLevelKey;
  protected readonly levelChoices = LEVEL_CHOICES;

  readonly projectId = input.required<string>();
  /** `?add=1`: open the add sheet once the team is read and the caller may add. */
  readonly openAdd = input(false);

  /** `?add=1` opens the sheet once; a reload of the list (canManage flickers) must not reopen it. */
  private addOpened = false;
  /** The sheet's own confirm is not the screen's primary (that is the shell slot's); it takes the variant by binding. */
  protected readonly sheetConfirm = 'primary' as const;
  private readonly addAction = viewChild<TemplateRef<unknown>>('addAction');

  protected readonly team = signal<TeamState>({ state: 'loading' });
  protected readonly members = computed(() => {
    const t = this.team();
    return t.state === 'ok' ? t.read.members : [];
  });
  protected readonly canManage = computed(() => {
    const t = this.team();
    return t.state === 'ok' && t.read.canManage;
  });
  /** The primary the workspace places in the shell slot: only for a caller the server lets add. */
  readonly declared = computed<TemplateRef<unknown> | null>(() => (this.canManage() ? (this.addAction() ?? null) : null));
  protected readonly noEngineer = computed(
    () => this.team().state === 'ok' && !this.members().some((m) => m.role === 'SiteEngineer'),
  );

  // ---- add ----------------------------------------------------------------------------------------
  protected readonly sheetOpen = signal(false);
  protected readonly candidates = signal<readonly TeamCandidate[] | null>(null);
  protected readonly pick = signal('');
  protected readonly level = signal('');
  protected readonly attempted = signal(false);
  protected readonly busy = signal(false);
  protected readonly refusal = signal<string | null>(null);

  protected readonly chosen = computed(() => this.candidates()?.find((c) => c.userId === this.pick()) ?? null);
  protected readonly personOptions = computed<readonly SelectOption[]>(() => [
    { value: '', label: '' },
    ...(this.candidates() ?? []).map((c) => ({
      value: c.userId,
      label: `${c.fullName} — ${this.i18n.t(roleKey(c.role))}`,
    })),
  ]);
  protected readonly personError = computed(() => (this.attempted() && this.chosen() === null ? 'projects.team.error.person' : null));
  protected readonly levelError = computed(() =>
    this.attempted() && this.chosen()?.levelRequired === true && this.level() === '' ? 'projects.team.error.level' : null,
  );

  // ---- remove ------------------------------------------------------------------------------------
  protected readonly removing = signal<TeamMember | null>(null);
  protected readonly removeBusy = signal(false);
  protected readonly removeParams = computed(() => ({ name: this.removing()?.fullName ?? '' }));

  // ---- result ------------------------------------------------------------------------------------
  protected readonly done = signal<{ readonly key: string; readonly name: string } | null>(null);
  protected readonly justAdded = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);

  constructor() {
    effect(() => {
      this.projectId();
      untracked(() => void this.load());
    });
    effect(() => {
      if (this.openAdd() && this.canManage() && !this.addOpened) {
        this.addOpened = true;
        untracked(() => void this.openSheet());
      }
    });
  }

  protected async load(): Promise<void> {
    this.team.set({ state: 'loading' });
    try {
      this.team.set({ state: 'ok', read: await this.api.teamRead(this.projectId()) });
    } catch (error) {
      this.team.set({ state: 'failed', key: toProblem(error).messageKey });
    }
  }

  protected async openSheet(): Promise<void> {
    this.pick.set('');
    this.level.set('');
    this.attempted.set(false);
    this.refusal.set(null);
    this.done.set(null);
    this.candidates.set(null);
    this.sheetOpen.set(true);
    try {
      this.candidates.set(await this.api.teamCandidates(this.projectId()));
    } catch (error) {
      this.candidates.set([]);
      this.refusal.set(toProblem(error).messageKey);
    }
  }

  protected onPick(value: string): void {
    this.pick.set(value);
    this.level.set('');
    this.attempted.set(false);
    this.refusal.set(null);
  }

  /** Never disabled for a gap the user can fix (D-229 item 3): pressing with a gap marks the field and sends nothing. */
  protected async confirmAdd(): Promise<void> {
    this.attempted.set(true);
    const person = this.chosen();
    if (person === null || (person.levelRequired && this.level() === '')) {
      return;
    }
    this.busy.set(true);
    this.refusal.set(null);
    try {
      await this.api.assign(this.projectId(), {
        userId: person.userId,
        level: person.levelRequired ? (this.level() as AssignmentLevel) : 'Standard',
      });
      this.sheetOpen.set(false);
      this.actionError.set(null);
      this.done.set({ key: 'projects.team.added', name: person.fullName });
      this.justAdded.set(person.userId);
      await this.load();
      this.scrollTo(`[data-user="${person.userId}"]`);
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
    } finally {
      this.busy.set(false);
    }
  }

  protected async confirmRemove(): Promise<void> {
    const member = this.removing();
    if (member === null) {
      return;
    }
    this.removeBusy.set(true);
    try {
      await this.api.removeMember(this.projectId(), member.assignmentId);
      this.removing.set(null);
      this.justAdded.set(null);
      this.actionError.set(null);
      this.done.set({ key: 'projects.team.removed', name: member.fullName });
      await this.load();
    } catch (error) {
      this.removing.set(null);
      this.actionError.set(toProblem(error).messageKey);
    } finally {
      this.removeBusy.set(false);
    }
  }

  protected initial(name: string): string {
    return [...name][0] ?? '';
  }

  private scrollTo(selector: string): void {
    setTimeout(() => this.host.nativeElement.querySelector(selector)?.scrollIntoView?.({ block: 'center' }), 0);
  }
}
