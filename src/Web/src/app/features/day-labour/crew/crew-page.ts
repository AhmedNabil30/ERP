import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  TemplateRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';
import { DailyLogApi } from '../../../core/daily-log/daily-log.api';
import { CrewMember, DayLabourApi, PoolWorker } from '../../../core/day-labour/day-labour.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffDaySwitch } from '../../../shared/kaff-day-switch/kaff-day-switch';
import { DayValue, dayDate, localDate, localIso } from '../../../shared/kaff-day-switch/site-days';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffFigurePair } from '../../../shared/kaff-figure-pair/kaff-figure-pair';
import { KaffHoursStepper } from '../../../shared/kaff-hours-stepper/kaff-hours-stepper';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffProjectFrame } from '../../../shared/kaff-project-frame/kaff-project-frame';
import { KaffSearchField } from '../../../shared/kaff-search-field/kaff-search-field';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';

/** D-217 item 1: at most 16 hours per man per day. The server enforces it again (`DailyLogEntryRule`). */
export const MAX_HOURS = 16;

/** D-217 item 2: hours move in half-hour steps. */
const STEP = 0.5;

/** One saved crew entry of one day, with the engineer whose log holds it. */
interface CrewLine {
  readonly entryId: string;
  readonly employeeId: string;
  readonly workerName: string;
  readonly hours: number;
  /** The server's verdict for this caller, per line (`canCorrect`). Absent (an older server) counts as no. */
  readonly canCorrect: boolean;
  readonly backDated: boolean;
}

/** One row of the crew list, as drawn. */
interface Row {
  readonly id: string;
  readonly name: string;
  readonly trade: string | null;
  /** What the day holds for him now: the sum of his saved entries, corrections included. */
  readonly saved: number;
  /** The row takes a stepper. A saved row the viewer may not edit is drawn as a tick and hours. */
  readonly editable: boolean;
  readonly text: string;
  readonly value: number;
  /** A typed value above the cap: kept on screen, refused in place, not counted. */
  readonly bad: boolean;
  readonly changed: boolean;
  readonly failKey: string | null;
  /** «حفظ» was pressed with no hours anywhere: this row, the first, is the gap. */
  readonly required: boolean;
}

/** The hours a text field says, cleaned the way the mock cleans it. `bad` is above the cap. */
export function parseHours(text: string): { value: number; bad: boolean } {
  const parsed = parseFloat(text.replace(/[^\d.]/g, ''));
  if (Number.isNaN(parsed)) {
    return { value: 0, bad: false };
  }
  const value = Math.round(parsed / STEP) * STEP;
  return { value, bad: value > MAX_HOURS };
}

/**
 * The crew screen under D-217 and D-218 (KAFF-615), built from `.design/v2/team-a/day-labour.html`: how many hours
 * each man worked on the chosen day, in one save. Today and three days back (the day switch is the daily log's).
 *
 * **The server decides who edits which day.** Each saved line carries `canCorrect`, the verdict the correction
 * endpoint would give; the screen draws the edit action from it and nothing else. An edit never updates a saved
 * entry: it sends a correction naming the entry (KAFF-615 AC-11).
 *
 * **Who is on the list.** The crew read (`listCrew`: the men engaged on the project, with trade) merged with the men
 * who have hours on a day this screen has read. A role that may not read the crew (403) gets no roster, only the
 * day's saved lines.
 */
@Component({
  selector: 'kaff-crew-page',
  imports: [
    RouterLink,
    KaffBanner,
    KaffButton,
    KaffDaySwitch,
    KaffDetailPanel,
    KaffFigurePair,
    KaffHoursStepper,
    KaffIcon,
    KaffProjectFrame,
    KaffSearchField,
    KaffState,
    KaffStatusPill,
  ],
  templateUrl: './crew-page.html',
  styleUrl: './crew-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CrewPage {
  /** Bound from the route: `/projects/:projectId/day-labour`. */
  readonly projectId = input.required<string>();
  /**
   * `?date=yyyy-MM-dd`: the day the viewer was looking at on the daily log. A day inside the four-day window opens on
   * its chip. An older day opens only for a reviewer (the owner, the supervising engineer), through the other-day
   * control. For anyone else, and for a malformed or future date, the screen opens on today.
   */
  readonly date = input<string | undefined>(undefined);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  private readonly logs = inject(DailyLogApi);
  private readonly labour = inject(DayLabourApi);
  private readonly auth = inject(AuthService);
  private readonly headerActions = inject(HeaderActionsService);

  protected readonly i18n = inject(I18nService);
  protected readonly maxHours = MAX_HOURS;

  private readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');

  protected readonly today = signal(new Date());
  protected readonly dayValue = signal<DayValue>('0');
  /** A reviewer's «يوم آخر»: any earlier day, `yyyy-MM-dd`. `null` while one of the four chips is chosen. */
  protected readonly otherDate = signal<string | null>(null);
  private readonly dayIndex = computed(() => Number(this.dayValue()));
  /** The day shown, `yyyy-MM-dd`. */
  private readonly shownDate = computed(() => this.otherDate() ?? this.dateOf(this.dayIndex()));
  protected readonly maxDate = computed(() => this.dateOf(0));
  protected readonly isToday = computed(() => this.shownDate() === this.dateOf(0));

  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly errorKey = signal<string | null>(null);

  /** Every day read so far, by `yyyy-MM-dd`. */
  private readonly days = signal<Readonly<Record<string, readonly CrewLine[]>>>({});
  /** The men this screen knows, by id: from the days read and from the men engaged here. */
  private readonly known = signal<ReadonlyMap<string, string>>(new Map());
  private readonly trades = signal<ReadonlyMap<string, string | null>>(new Map());
  /** The company pool, read only when the add-a-man sheet opens. */
  private readonly pool = signal<readonly PoolWorker[]>([]);

  /** What the user typed, by man. A man not in here shows what is saved. */
  private readonly draft = signal<Readonly<Record<string, string>>>({});
  private readonly failed = signal<Readonly<Record<string, string>>>({});
  /** Ids sent for a row, kept until it saves, so a retried press is recognised by the server and counted once. */
  private readonly sentIds = new Map<string, string>();
  protected readonly editing = signal(false);
  protected readonly busy = signal(false);
  /** True only after a save made in this visit: arriving on a saved day is a state, not an event. */
  private readonly savedNow = signal(false);
  /** «حفظ» was pressed with nothing to save: the gap is marked until the user types or the day changes. */
  private readonly attempted = signal(false);

  protected readonly sheetOpen = signal(false);
  protected readonly sheetSearch = signal('');
  protected readonly sheetBusy = signal<string | null>(null);
  protected readonly sheetError = signal<string | null>(null);

  // --- who the viewer is: presentation only, the server decides --------------------------------------------------
  private readonly session = computed(() => this.auth.current());
  private readonly permissions = computed(
    () => this.session()?.projects.find((entry) => entry.projectId === this.projectId())?.permissions ?? [],
  );
  /** `DailyLogReview`: the owner and the supervising site engineer. It is also how the screen reads other engineers' logs. */
  protected readonly canReview = computed(() => this.permissions().includes('DailyLogReview'));
  protected readonly canRecord = computed(() => this.permissions().includes('DailyLogWrite'));

  private dateOf(index: number): string {
    return localDate(dayDate(this.today(), index));
  }

  private readonly shownLines = computed<readonly CrewLine[]>(() => this.days()[this.shownDate()] ?? []);
  private readonly yesterdayLines = computed<readonly CrewLine[]>(() => this.days()[this.dateOf(1)] ?? []);

  /** The server's verdict for the line (D-217 item 3, D-218 item 16 are decided there). */
  private mayEdit(line: CrewLine): boolean {
    return line.canCorrect;
  }

  private savedOf(id: string, lines: readonly CrewLine[]): { total: number; editable: boolean } {
    const mine = lines.filter((line) => line.employeeId === id);
    return {
      total: mine.reduce((sum, line) => sum + line.hours, 0),
      editable: mine.length > 0 && mine.every((line) => this.mayEdit(line)),
    };
  }

  protected readonly hasSaved = computed(() => this.shownLines().length > 0);
  private readonly anyEditable = computed(() => this.shownLines().some((line) => this.mayEdit(line)));
  /** An earlier day opens for editing straight away for the supervising engineer (mock `?view=flag`). */
  private readonly autoEdit = computed(() => !this.isToday() && this.anyEditable());
  /** The stepper state: nothing saved yet, a press of the edit action, or the supervising engineer on an earlier day. */
  protected readonly editMode = computed(() => (!this.hasSaved() ? this.canRecord() : this.editing() || this.autoEdit()));

  private readonly baseRows = computed<readonly Row[]>(() => {
    const lines = this.shownLines();
    const draft = this.draft();
    const failed = this.failed();
    const editMode = this.editMode();
    const trades = this.trades();
    return [...this.known()]
      .sort(([, a], [, b]) => a.localeCompare(b, 'ar'))
      .map(([id, name]) => {
        const { total, editable: mayEditSaved } = this.savedOf(id, lines);
        const editable = editMode && (total > 0 ? mayEditSaved : this.canRecord());
        const touched = draft[id];
        const text = touched ?? (total > 0 ? String(total) : '');
        const { value, bad } = touched === undefined ? { value: total, bad: false } : parseHours(touched);
        return {
          id,
          name,
          trade: trades.get(id) ?? null,
          saved: total,
          editable,
          text,
          value,
          bad,
          changed: touched !== undefined && !bad && value > 0 && value !== total,
          failKey: failed[id] ?? null,
          required: false,
        };
      });
  });

  /** After a press with no hours anywhere, the first row that takes hours is marked (D-229 item 3). */
  protected readonly rows = computed<readonly Row[]>(() => {
    const base = this.baseRows();
    const anyHours = base.some((row) => !(row.editable && row.bad) && (row.editable ? row.value : row.saved) > 0);
    if (!this.attempted() || anyHours) {
      return base;
    }
    const first = base.findIndex((row) => row.editable);
    return base.map((row, index) => (index === first ? { ...row, required: true } : row));
  });

  /**
   * What the list shows. The editing state lists the whole crew, because each man takes a stepper. A read state lists
   * only the men with hours on the day, so the rows and the summary above them are the same men (consultant review 7).
   */
  protected readonly visibleRows = computed(() => (this.editMode() ? this.rows() : this.rows().filter((row) => row.saved > 0)));

  private readonly anyBad = computed(() => this.rows().some((row) => row.editable && row.bad));
  /** AC-9: a refused value is not counted, in the men or in the hours. */
  private readonly counted = computed(() =>
    this.rows().filter((row) => !(row.editable && row.bad) && (row.editable ? row.value : row.saved) > 0),
  );
  protected readonly menCount = computed(() => this.counted().length);
  protected readonly hoursTotal = computed(() => this.counted().reduce((sum, row) => sum + (row.editable ? row.value : row.saved), 0));
  private readonly changes = computed(() => this.rows().filter((row) => row.editable && row.changed));
  protected readonly canSave = computed(() => this.editMode() && this.changes().length > 0 && !this.anyBad() && !this.busy());

  /** The edit action: a saved day the viewer may edit and is not editing yet. */
  protected readonly canStartEdit = computed(() => this.hasSaved() && !this.editMode() && this.anyEditable());
  protected readonly justSaved = computed(() => this.savedNow() && this.hasSaved() && !this.editMode());
  protected readonly showSame = computed(
    () => this.isToday() && this.editMode() && this.canRecord() && this.menCount() === 0 && this.yesterdayLines().length > 0,
  );
  /** The back-dated tag: drawn only where the server sent the flag, which it sends to the supervising engineer and the owner. */
  protected readonly backDated = computed(() => this.shownLines().some((line) => line.backDated));
  protected readonly empty = computed(() => this.known().size === 0);

  protected readonly primary = computed<{ labelKey: string; act: 'save' | 'edit' | 'add' } | null>(() => {
    if (this.loading() || this.failure() !== null) {
      return null;
    }
    if (this.empty()) {
      return this.canRecord() ? { labelKey: 'crew.add', act: 'add' } : null;
    }
    if (this.editMode()) {
      return { labelKey: 'action.save', act: 'save' };
    }
    return this.canStartEdit() ? { labelKey: 'action.edit', act: 'edit' } : null;
  });

  protected readonly sheetResults = computed(() => {
    const needle = this.sheetSearch().trim().toLowerCase();
    const known = this.known();
    return this.pool().filter(
      (worker) =>
        worker.isActive &&
        !known.has(worker.id) &&
        (needle === '' || worker.fullName.toLowerCase().includes(needle) || (worker.specialty ?? '').toLowerCase().includes(needle)),
    );
  });

  constructor() {
    // Deferred to an `effect`: `projectId` is a required input and is not readable in the constructor (NG0950).
    effect(() => {
      const id = this.projectId();
      untracked(() => void this.loadAll(id));
    });

    effect(() => {
      const date = this.shownDate();
      const id = this.projectId();
      untracked(() => {
        this.resetDraft();
        this.savedNow.set(false);
        if (!this.loading()) {
          void this.loadDay(id, date);
        }
      });
    });

    // The day carried in the URL. Read when the input or the viewer's rights change, never from the constructor.
    effect(() => {
      const wanted = this.date();
      const reviewer = this.canReview();
      untracked(() => this.applyDate(wanted, reviewer));
    });

    effect(() => this.headerActions.set(this.primary() !== null ? (this.primaryTemplate() ?? null) : null, 'primary'));
    inject(DestroyRef).onDestroy(() => this.headerActions.clear());
  }

  protected onPrimary(): void {
    switch (this.primary()?.act) {
      case 'save':
        void this.onSave();
        break;
      case 'edit':
        this.savedNow.set(false);
        this.editing.set(true);
        break;
      case 'add':
        this.openSheet();
        break;
    }
  }

  protected reload(): void {
    void this.loadAll(this.projectId());
  }

  // --- the rows ---------------------------------------------------------------------------------------------------

  protected onStep(row: Row, direction: -1 | 1): void {
    const current = row.bad ? parseHours(row.text).value : row.value;
    const floor = row.saved > 0 ? STEP : 0;
    const next = Math.min(MAX_HOURS, Math.max(floor, current + direction * STEP));
    this.setText(row.id, next === 0 ? '' : String(next));
  }

  /** Opens on the day the URL names, as far as the viewer may see it (see `date`). */
  private applyDate(wanted: string | undefined, reviewer: boolean): void {
    if (wanted === undefined || !/^\d{4}-\d{2}-\d{2}$/.test(wanted)) {
      return;
    }
    const index = [0, 1, 2, 3].find((i) => this.dateOf(i) === wanted);
    if (index !== undefined) {
      this.otherDate.set(null);
      this.dayValue.set(String(index) as DayValue);
    } else if (reviewer && wanted < this.dateOf(0)) {
      this.otherDate.set(wanted);
    }
  }

  protected onTyped(row: Row, text: string): void {
    this.setText(row.id, text);
  }

  /** On leaving the field the text shows the half-hour value that will be saved, as long as it is allowed. */
  protected onCommitted(row: Row): void {
    const { value, bad } = parseHours(this.draft()[row.id] ?? row.text);
    if (!bad) {
      this.setText(row.id, value === 0 ? '' : String(value));
    }
  }

  protected minusDisabled(row: Row): boolean {
    return row.value <= (row.saved > 0 ? STEP : 0) && !row.bad;
  }

  protected plusDisabled(row: Row): boolean {
    return row.value >= MAX_HOURS && !row.bad;
  }

  /** The copy action: the men of yesterday and their hours go into the rows, to be adjusted and saved (D-217 item 4). */
  protected copyYesterday(): void {
    const totals = new Map<string, number>();
    for (const line of this.yesterdayLines()) {
      totals.set(line.employeeId, (totals.get(line.employeeId) ?? 0) + line.hours);
    }
    this.draft.set(Object.fromEntries([...totals].map(([id, hours]) => [id, String(Math.min(hours, MAX_HOURS))])));
  }

  private setText(id: string, text: string): void {
    this.attempted.set(false);
    this.draft.update((current) => ({ ...current, [id]: text }));
    this.failed.update(({ [id]: _dropped, ...rest }) => rest);
  }

  private resetDraft(): void {
    this.draft.set({});
    this.failed.set({});
    this.attempted.set(false);
    this.errorKey.set(null);
    this.editing.set(false);
  }

  // --- save -------------------------------------------------------------------------------------------------------

  protected async onSave(): Promise<void> {
    if (this.busy()) {
      return;
    }
    if (!this.canSave()) {
      // D-229 item 3: nothing to save is a gap the user can fix here. The button is not greyed: this marks the row
      // that needs hours (the first bad one, else the first one) and focuses it.
      this.attempted.set(true);
      const target = this.visibleRows().findIndex((row) => row.editable && row.bad);
      const index = target >= 0 ? target : this.visibleRows().findIndex((row) => row.editable);
      afterNextRender(
        () => this.host.nativeElement.querySelector<HTMLElement>(`[data-testid="crew-hours-${index}-input"]`)?.focus(),
        { injector: this.injector },
      );
      return;
    }
    this.busy.set(true);
    this.errorKey.set(null);
    this.savedNow.set(false);
    const projectId = this.projectId();
    const date = this.shownDate();
    const occurredAt = localIso(new Date(), this.isToday() ? null : date);
    const lines = this.shownLines();
    let refused = 0;

    for (const row of this.changes()) {
      const sent = this.sentIds.get(row.id) ?? crypto.randomUUID();
      this.sentIds.set(row.id, sent);
      try {
        if (row.saved > 0) {
          // The first saved entry carries the difference, so the man's day reads what the screen shows.
          const first = lines.find((line) => line.employeeId === row.id)!;
          await this.logs.correctDayLabour(projectId, first.entryId, {
            correctionId: sent,
            hours: first.hours + (row.value - row.saved),
          });
        } else {
          await this.logs.recordDayLabour(projectId, { entryId: sent, occurredAt, employeeId: row.id, hours: row.value });
        }
        this.sentIds.delete(row.id);
      } catch (error) {
        refused++;
        this.failed.update((current) => ({ ...current, [row.id]: toProblem(error).messageKey }));
      }
    }

    try {
      await this.loadDay(projectId, date);
      if (refused === 0) {
        this.draft.set({});
        this.editing.set(false);
        this.savedNow.set(true);
      }
    } finally {
      this.busy.set(false);
    }
  }

  // --- the add-a-man sheet ---------------------------------------------------------------------------------------

  protected openSheet(): void {
    this.sheetSearch.set('');
    this.sheetError.set(null);
    this.sheetOpen.set(true);
    if (this.pool().length === 0) {
      void this.loadPool();
    }
  }

  private async loadPool(): Promise<void> {
    try {
      this.pool.set(await this.labour.listPool(this.projectId()));
    } catch (error) {
      this.sheetError.set(toProblem(error).messageKey);
    }
  }

  /** The engage action: engages the man on this project and puts him on the list (D-217 item 6: his phone is never shown). */
  protected async engage(worker: PoolWorker): Promise<void> {
    this.sheetBusy.set(worker.id);
    this.sheetError.set(null);
    try {
      await this.labour.openEngagement(this.projectId(), worker.id);
      this.known.update((current) => new Map(current).set(worker.id, worker.fullName));
      this.trades.update((current) => new Map(current).set(worker.id, worker.specialty));
      this.sheetOpen.set(false);
    } catch (error) {
      this.sheetError.set(toProblem(error).messageKey);
    } finally {
      this.sheetBusy.set(null);
    }
  }

  protected engagedLabel(worker: PoolWorker): string {
    if (worker.frequency === 0) {
      return this.i18n.t('hr.worker.pool.never_engaged');
    }
    return worker.frequency === 1 ? this.i18n.t('crew.sheet.once') : this.i18n.t('crew.sheet.times', { count: worker.frequency });
  }

  protected rating(worker: PoolWorker): string | null {
    return worker.frequency > 0 && worker.averageRating !== null
      ? this.i18n.formatNumber(worker.averageRating, { maximumFractionDigits: 1 })
      : null;
  }

  protected number(value: number): string {
    return this.i18n.formatNumber(value);
  }

  // --- loading ----------------------------------------------------------------------------------------------------

  private async loadAll(projectId: string): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    this.today.set(new Date());
    this.known.set(new Map());
    this.days.set({});
    this.trades.set(new Map());
    this.pool.set([]);
    try {
      const [crew] = await Promise.all([
        this.readCrew(projectId),
        this.loadDay(projectId, this.dateOf(0), true),
        this.loadDay(projectId, this.dateOf(1), true),
      ]);
      this.trades.set(new Map(crew.map((man) => [man.workerId, man.trade])));
      this.known.update((current) => {
        const next = new Map(current);
        crew.forEach((man) => next.set(man.workerId, man.name));
        return next;
      });
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
    if (this.failure() === null && !(this.shownDate() in this.days())) {
      await this.loadDay(projectId, this.shownDate());
    }
  }

  /** A role the server refuses the crew read (403) has no roster: not a failure. */
  private async readCrew(projectId: string): Promise<readonly CrewMember[]> {
    try {
      return await this.labour.listCrew(projectId);
    } catch (error) {
      if (toProblem(error).status === 403) {
        return [];
      }
      throw error;
    }
  }

  /** Reads one day. A reviewer reads every engineer's log; the others read their own. */
  private async loadDay(projectId: string, date: string, throwOnFailure = false): Promise<void> {
    try {
      let lines: CrewLine[];
      if (this.canReview()) {
        const all = await this.logs.listDayLogs(projectId, date);
        lines = all.flatMap((entry) =>
          entry.log.dayLabour.map((line) => toLine(line, entry.log.backDated === true)),
        );
      } else {
        const log = await this.logs.getMine(projectId, date);
        lines = (log?.dayLabour ?? []).map((line) => toLine(line, false));
      }
      this.days.update((current) => ({ ...current, [date]: lines }));
      this.known.update((current) => {
        const next = new Map(current);
        lines.forEach((line) => next.set(line.employeeId, line.workerName));
        return next;
      });
    } catch (error) {
      if (throwOnFailure) {
        throw error;
      }
      this.errorKey.set(toProblem(error).messageKey);
    }
  }
}

function toLine(
  line: { entryId: string; employeeId: string; workerName: string; hours: number; canCorrect?: boolean; backDated?: boolean },
  logBackDated: boolean,
): CrewLine {
  return {
    entryId: line.entryId,
    employeeId: line.employeeId,
    workerName: line.workerName,
    hours: line.hours,
    canCorrect: line.canCorrect === true,
    backDated: line.backDated ?? logBackDated,
  };
}
