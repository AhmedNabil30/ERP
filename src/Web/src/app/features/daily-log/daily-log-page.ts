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
import { FormField, form } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../core/api/problem-details';
import { BoqLines, BoqPickLine, CheckInLine, DailyLog, DailyLogApi, QuantityLine } from '../../core/daily-log/daily-log.api';
import { I18nService } from '../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../core/layout/header-actions.service';
import { ProjectDetail } from '../../core/projects/projects.api';
import { KaffAttendanceStrip } from '../../shared/kaff-attendance-strip/kaff-attendance-strip';
import { KaffBanner } from '../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffDetailPanel } from '../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../shared/kaff-field/kaff-field';
import { KaffFigurePair } from '../../shared/kaff-figure-pair/kaff-figure-pair';
import { KaffKindRow, KindOption } from '../../shared/kaff-kind-row/kaff-kind-row';
import { KaffLinkRow } from '../../shared/kaff-link-row/kaff-link-row';
import { KaffLogEntry } from '../../shared/kaff-log-entry/kaff-log-entry';
import { KaffLogGroup } from '../../shared/kaff-log-group/kaff-log-group';
import { KaffPickerButton } from '../../shared/kaff-picker-button/kaff-picker-button';
import { KaffProjectFrame } from '../../shared/kaff-project-frame/kaff-project-frame';
import { KaffReadonlyField } from '../../shared/kaff-readonly-field/kaff-readonly-field';
import { KaffSearchField } from '../../shared/kaff-search-field/kaff-search-field';
import { KaffSegmentedFilter, SegmentedFilterOption } from '../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffSelect, SelectOption } from '../../shared/kaff-select/kaff-select';
import { KaffState } from '../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../shared/kaff-status-pill/kaff-status-pill';

type EntryKind = 'note' | 'quantity' | 'material' | 'stoppage';
type DayValue = '0' | '1' | '2' | '3';

/** The open window (D-217 item 7): today and three days back. The server judges it again. */
const DAY_VALUES: readonly DayValue[] = ['0', '1', '2', '3'];

/** How long to wait for a fix before telling the engineer location is not available (AC-613-D). */
const LOCATION_TIMEOUT_MS = 15_000;

const KIND_OPTIONS: readonly KindOption<EntryKind>[] = [
  { value: 'note', labelKey: 'daily_log.kind.note', icon: 'note' },
  { value: 'quantity', labelKey: 'daily_log.kind.quantity', icon: 'qty' },
  { value: 'material', labelKey: 'daily_log.kind.material', icon: 'mat' },
  { value: 'stoppage', labelKey: 'daily_log.kind.stoppage', icon: 'stop' },
];

/** The material units the screen offers; Karim confirms the list (D-217 item 17). */
const UNIT_KEYS: readonly string[] = [
  'daily_log.unit.bag',
  'daily_log.unit.m3',
  'daily_log.unit.m2',
  'daily_log.unit.ton',
  'daily_log.unit.metre',
  'daily_log.unit.piece',
  'daily_log.unit.litre',
];

interface EntryDraft {
  note: string;
  stoppage: string;
  quantity: string;
  materialName: string;
  materialQuantity: string;
}

const BLANK: EntryDraft = { note: '', stoppage: '', quantity: '', materialName: '', materialQuantity: '' };

/** The quantity row being corrected (D-218 item 14). */
interface EditTarget {
  readonly entryId: string;
  readonly boqLineId: string;
}

/**
 * The daily log under D-217 and D-218 (KAFF-614), built from `.design/v2/team-a/daily-log.html`.
 *
 * **Check-in first.** Until the engineer has checked in today the screen holds one state block and the
 * "الحضور" primary; no day switch, no entry control. The server refuses every entry without it (AC-1), so this is
 * presentation. After it: the day switch (today and three days back), the entry kinds, the form, and "المسجَّل".
 *
 * **Period deltas.** A quantity entered is what was done in the period and it adds (D-217 item 11). A correction
 * sends the figure the saved quantity should read; the server stores a signed delta that names the entry.
 *
 * **Money-free by construction** (`AC-600-J`): the read is `GET …/daily-logs/mine`, a read model with no money member.
 *
 * **The server decides.** The back-dated tag is drawn only where the server sent the flag. Refusals come back as
 * message keys. The one primary ("الحضور" or "حفظ") goes into the shell's primary-action slot.
 *
 * Not built: the photo kind (no upload endpoint exists, only a file reference).
 */
@Component({
  selector: 'kaff-daily-log-page',
  imports: [
    FormField,
    RouterLink,
    KaffAttendanceStrip,
    KaffBanner,
    KaffButton,
    KaffDetailPanel,
    KaffField,
    KaffFigurePair,
    KaffKindRow,
    KaffLinkRow,
    KaffLogEntry,
    KaffLogGroup,
    KaffPickerButton,
    KaffProjectFrame,
    KaffReadonlyField,
    KaffSearchField,
    KaffSegmentedFilter,
    KaffSelect,
    KaffState,
    KaffStatusPill,
  ],
  templateUrl: './daily-log-page.html',
  styleUrl: './daily-log-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DailyLogPage {
  /** Bound from the route: `/projects/:projectId/daily-log`. */
  readonly projectId = input.required<string>();

  private readonly api = inject(DailyLogApi);
  private readonly headerActions = inject(HeaderActionsService);

  protected readonly i18n = inject(I18nService);

  private readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');

  protected readonly kindOptions = KIND_OPTIONS;
  protected readonly unitOptions = computed<readonly SelectOption[]>(() =>
    UNIT_KEYS.map((key) => ({ value: this.i18n.t(key), label: this.i18n.t(key) })),
  );

  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly stopped = computed(() => this.project()?.status === 'Stopped');

  /** Today's log: it says whether the engineer has checked in today, whichever day is shown. */
  protected readonly todayLog = signal<DailyLog | null>(null);
  private readonly pastLog = signal<DailyLog | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadFailure = signal<string | null>(null);
  /** The project's state takes no log (the server said so): the screen shows only that. */
  protected readonly closed = signal(false);

  protected readonly dayValue = signal<DayValue>('0');
  private readonly dayIndex = computed(() => Number(this.dayValue()));
  protected readonly isToday = computed(() => this.dayIndex() === 0);
  private readonly today = signal(new Date());

  /** The day switch: today and up to three days back. Weekday names for the older two. */
  protected readonly dayOptions = computed<readonly SegmentedFilterOption<DayValue>[]>(() =>
    DAY_VALUES.map((value) => ({ value, labelKey: '', labelText: this.dayWord(Number(value)) })),
  );
  protected readonly dayWordNow = computed(() => this.dayWord(this.dayIndex()));
  protected readonly dateLine = computed(() =>
    this.i18n.formatDate(this.dayDate(this.dayIndex()), { weekday: 'long', day: 'numeric', month: 'long' }),
  );

  protected readonly shownLog = computed<DailyLog | null>(() => (this.isToday() ? this.todayLog() : this.pastLog()));

  protected readonly checkInLine = computed<CheckInLine | null>(
    () => this.todayLog()?.checkIns.find((entry) => entry.kind === 'CheckIn') ?? null,
  );
  protected readonly checkOutLine = computed<CheckInLine | null>(
    () => this.todayLog()?.checkIns.filter((entry) => entry.kind === 'CheckOut').at(-1) ?? null,
  );
  protected readonly checkedIn = computed(() => this.checkInLine() !== null);

  /** The server sends the back-dated flag only to the supervising engineer (and the owner): its presence says who may edit an earlier day. */
  private readonly serverSendsFlag = computed(() => this.shownLog()?.backDated !== undefined);
  protected readonly canEditShown = computed(() => this.isToday() || this.serverSendsFlag());

  /** Entries of the shown day, newest first inside each group (README "المسجَّل"). */
  protected readonly stoppages = computed(() => newestFirst(this.shownLog()?.stoppageReasons ?? []));
  protected readonly quantities = computed(() => newestFirst(this.shownLog()?.quantities ?? []));
  protected readonly materials = computed(() => newestFirst(this.shownLog()?.materials ?? []));
  protected readonly notes = computed(() => newestFirst(this.shownLog()?.notes ?? []));
  protected readonly photos = computed(() => newestFirst(this.shownLog()?.photos ?? []));
  protected readonly nothingLogged = computed(
    () =>
      this.stoppages().length + this.quantities().length + this.materials().length + this.notes().length + this.photos().length === 0,
  );

  protected readonly crewSummary = computed(() => {
    const crew = this.shownLog()?.dayLabour ?? [];
    if (crew.length === 0) {
      return this.i18n.t('daily_log.crew.none');
    }
    const hours = crew.reduce((sum, line) => sum + line.hours, 0);
    return this.i18n.t('daily_log.crew.summary', {
      count: new Set(crew.map((line) => line.employeeId)).size,
      hours: this.i18n.formatNumber(hours),
    });
  });

  /** `null` once loaded means the project has no BOQ. */
  protected readonly boq = signal<BoqLines | null>(null);
  protected readonly boqFailure = signal<string | null>(null);
  protected readonly selectedLineId = signal('');
  protected readonly selectedLine = computed<BoqPickLine | null>(
    () => this.boq()?.lines.find((line) => line.id === this.selectedLineId()) ?? null,
  );

  protected readonly pickerOpen = signal(false);
  protected readonly pickerSearch = signal('');
  protected readonly pickerResults = computed(() => {
    const needle = this.pickerSearch().trim().toLowerCase();
    const lines = this.boq()?.lines ?? [];
    return needle === '' ? lines : lines.filter((line) => this.lineName(line).toLowerCase().includes(needle));
  });

  protected readonly kind = signal<EntryKind>('note');
  protected readonly editing = signal<EditTarget | null>(null);
  private readonly model = signal<EntryDraft>({ ...BLANK });
  protected readonly entryForm = form(this.model);
  protected readonly materialUnit = signal('');

  protected readonly busy = signal(false);
  protected readonly saved = signal(false);
  protected readonly errorKey = signal<string | null>(null);

  protected readonly locating = signal<'CheckIn' | 'CheckOut' | null>(null);
  protected readonly locationRefused = signal(false);

  /** The primary is disabled until the form is complete (README "Primary حفظ"). */
  protected readonly valid = computed(() => {
    const draft = this.model();
    switch (this.kind()) {
      case 'note':
        return draft.note.trim().length > 0;
      case 'stoppage':
        return draft.stoppage.trim().length > 0;
      case 'quantity':
        return (this.editing() !== null || this.selectedLine() !== null) && positive(draft.quantity) !== null;
      case 'material':
        return draft.materialName.trim().length > 0 && positive(draft.materialQuantity) !== null && this.materialUnit() !== '';
    }
  });

  /** What the one primary says and does. `null`: no primary (loading, failed, closed). */
  protected readonly primary = computed<{ labelKey: string; act: 'check-in' | 'save' } | null>(() => {
    if (this.loading() || this.loadFailure() !== null || this.closed()) {
      return null;
    }
    if (!this.checkedIn()) {
      return { labelKey: 'daily_log.field.check_in', act: 'check-in' };
    }
    return { labelKey: this.editing() !== null ? 'daily_log.action.save_edit' : 'action.save', act: 'save' };
  });

  constructor() {
    // Deferred to an `effect`: `projectId` is a required input and is not readable in the constructor.
    effect(() => {
      const id = this.projectId();
      untracked(() => void this.loadAll(id));
    });

    // A past day is read when it is chosen; today's log is always held (it carries the check-in).
    effect(() => {
      const index = this.dayIndex();
      const id = this.projectId();
      untracked(() => {
        this.cancelEdit();
        this.saved.set(false);
        this.errorKey.set(null);
        if (index > 0) {
          void this.loadPast(id, index);
        }
      });
    });

    effect(() => this.headerActions.set(this.primary() !== null ? (this.primaryTemplate() ?? null) : null));
    inject(DestroyRef).onDestroy(() => this.headerActions.set(null));
  }

  protected onLoaded(project: ProjectDetail): void {
    if (this.project() === null && project.status === 'Stopped') {
      this.kind.set('stoppage');
    }
    this.project.set(project);
  }

  protected onPrimary(): void {
    const action = this.primary()?.act;
    if (action === 'check-in') {
      void this.press('CheckIn');
    } else if (action === 'save') {
      void this.onSave();
    }
  }

  protected reload(): void {
    void this.loadAll(this.projectId());
  }

  /**
   * Check-in and check-out (KAFF-613). One press takes the place and the device time and sends both. Without a place
   * nothing is sent and the screen says why; the server refuses it too.
   */
  protected async press(kind: 'CheckIn' | 'CheckOut'): Promise<void> {
    if (this.locating() !== null) {
      return;
    }
    this.locationRefused.set(false);
    this.errorKey.set(null);
    this.locating.set(kind);
    try {
      const occurredAt = localIso(new Date()); // device time at the press, not after the place resolves
      const place = await currentPlace();
      await this.api.recordCheckIn(this.projectId(), {
        entryId: crypto.randomUUID(),
        occurredAt,
        kind,
        latitude: place.latitude,
        longitude: place.longitude,
        accuracyMetres: place.accuracyMetres,
      });
      await this.loadToday(this.projectId());
    } catch (error) {
      if (error instanceof LocationError) {
        this.locationRefused.set(true);
      } else {
        this.refuse(toProblem(error).messageKey);
      }
    } finally {
      this.locating.set(null);
    }
  }

  protected time(value: string | null): string {
    return value === null ? '' : this.i18n.formatDate(new Date(value), { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });
  }

  protected number(value: number): string {
    return this.i18n.formatNumber(value);
  }

  protected lineName(line: BoqPickLine): string {
    return this.i18n.locale() === 'en' && line.descriptionEn ? line.descriptionEn : line.descriptionAr;
  }

  /** The line's one quantity: surveyed if there is one, else estimated (D-217 item 21). */
  protected lineTotal(line: BoqPickLine): string {
    const total = line.quantitySurveyed ?? line.quantityEstimated;
    return total === null ? '—' : this.number(total);
  }

  protected openPicker(): void {
    this.pickerSearch.set('');
    this.pickerOpen.set(true);
  }

  protected pick(line: BoqPickLine): void {
    this.selectedLineId.set(line.id);
    this.model.update((draft) => ({ ...draft, quantity: '' }));
    this.pickerOpen.set(false);
  }

  /** "تعديل": loads the saved row into the form. The kind row goes while editing. */
  protected startEdit(line: QuantityLine): void {
    this.saved.set(false);
    this.errorKey.set(null);
    this.kind.set('quantity');
    this.selectedLineId.set(line.boqLineId);
    this.editing.set({ entryId: line.entryId, boqLineId: line.boqLineId });
    this.model.update((draft) => ({ ...draft, quantity: String(line.quantity) }));
  }

  protected cancelEdit(): void {
    if (this.editing() !== null) {
      this.editing.set(null);
      this.selectedLineId.set('');
      this.model.update((draft) => ({ ...draft, quantity: '' }));
    }
  }

  protected setKind(kind: EntryKind): void {
    this.kind.set(kind);
    this.saved.set(false);
    this.errorKey.set(null);
  }

  protected async onSave(): Promise<void> {
    if (!this.valid() || this.busy()) {
      return;
    }
    this.errorKey.set(null);
    const entry = { entryId: crypto.randomUUID(), occurredAt: this.occurredAt() };
    const draft = this.model();
    const projectId = this.projectId();
    this.busy.set(true);

    try {
      switch (this.kind()) {
        case 'note':
          await this.api.recordField(projectId, { ...entry, field: 'Note', text: draft.note.trim() });
          break;
        case 'stoppage':
          await this.api.recordField(projectId, { ...entry, field: 'StoppageReason', text: draft.stoppage.trim() });
          break;
        case 'quantity': {
          const quantity = positive(draft.quantity) as number;
          const target = this.editing();
          if (target !== null) {
            await this.api.correctQuantity(projectId, target.entryId, { correctionId: crypto.randomUUID(), quantity });
          } else {
            await this.api.recordQuantity(projectId, { ...entry, boqLineId: this.selectedLineId(), quantity });
          }
          break;
        }
        case 'material':
          await this.api.recordMaterial(projectId, {
            ...entry,
            materialType: draft.materialName.trim(),
            quantity: positive(draft.materialQuantity) as number,
            unit: this.materialUnit(),
            isStockpile: false,
          });
          break;
      }

      // The picked line stays after a quantity so the next one is one field away; everything else clears.
      this.editing.set(null);
      this.model.set({ ...BLANK });
      this.materialUnit.set('');
      if (this.kind() !== 'quantity') {
        this.selectedLineId.set('');
      }
      this.saved.set(true);
      await Promise.all([this.loadToday(projectId), this.isToday() ? Promise.resolve() : this.loadPast(projectId, this.dayIndex()), this.loadBoq(projectId)]);
    } catch (error) {
      this.refuse(toProblem(error).messageKey);
    } finally {
      this.busy.set(false);
    }
  }

  private refuse(key: string): void {
    if (key === 'errors.daily_log.project_state_closed_to_logs') {
      this.closed.set(true);
    } else if (key === 'errors.daily_log.location_required') {
      this.locationRefused.set(true);
    } else {
      this.errorKey.set(key);
    }
  }

  /** The device's time on the shown day: an entry belongs to the day it names, not the day it is sent (rule 11). */
  private occurredAt(): string {
    return localIso(new Date(), this.isToday() ? null : localDate(this.dayDate(this.dayIndex())));
  }

  private dayDate(index: number): Date {
    const date = new Date(this.today());
    date.setDate(date.getDate() - index);
    return date;
  }

  private dayWord(index: number): string {
    if (index === 0) {
      return this.i18n.t('daily_log.day.today');
    }
    if (index === 1) {
      return this.i18n.t('daily_log.day.yesterday');
    }
    return this.i18n.formatDate(this.dayDate(index), { weekday: 'long' });
  }

  private async loadAll(projectId: string): Promise<void> {
    this.loading.set(true);
    this.today.set(new Date());
    await Promise.all([this.loadToday(projectId), this.loadBoq(projectId)]);
    this.loading.set(false);
  }

  private async loadToday(projectId: string): Promise<void> {
    try {
      this.todayLog.set(await this.api.getMine(projectId, localDate(new Date())));
      this.loadFailure.set(null);
    } catch (error) {
      this.loadFailure.set(toProblem(error).messageKey);
    }
  }

  private async loadPast(projectId: string, index: number): Promise<void> {
    this.pastLog.set(null);
    try {
      this.pastLog.set(await this.api.getMine(projectId, localDate(this.dayDate(index))));
    } catch (error) {
      this.errorKey.set(toProblem(error).messageKey);
    }
  }

  private async loadBoq(projectId: string): Promise<void> {
    try {
      this.boq.set(await this.api.listBoqLines(projectId));
      this.boqFailure.set(null);
    } catch (error) {
      this.boqFailure.set(toProblem(error).messageKey);
    }
  }
}

function newestFirst<T extends { readonly occurredAt: string }>(lines: readonly T[]): readonly T[] {
  return [...lines].sort((a, b) => b.occurredAt.localeCompare(a.occurredAt));
}

/** The reason a place could not be taken. */
class LocationError extends Error {
  constructor(readonly messageKey: string) {
    super(messageKey);
  }
}

interface Place {
  readonly latitude: number;
  readonly longitude: number;
  readonly accuracyMetres: number | null;
}

/** The device's place, or a `LocationError`. High accuracy: the site is a few hundred metres across. */
function currentPlace(): Promise<Place> {
  return new Promise((resolve, reject) => {
    if (!('geolocation' in navigator)) {
      reject(new LocationError('daily_log.location.refused'));
      return;
    }
    navigator.geolocation.getCurrentPosition(
      (position) =>
        resolve({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracyMetres: Number.isFinite(position.coords.accuracy) ? position.coords.accuracy : null,
        }),
      () => reject(new LocationError('daily_log.location.refused')),
      { enableHighAccuracy: true, timeout: LOCATION_TIMEOUT_MS, maximumAge: 0 },
    );
  });
}

/** A positive finite number from a text field, or `null`. */
function positive(text: string): number | null {
  const value = Number(text.trim());
  return Number.isFinite(value) && value > 0 ? value : null;
}

/** The device's calendar date, `yyyy-MM-dd`. */
function localDate(now: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** ISO-8601 in the device's own offset, not UTC. `day` puts the device's time of day on another date. */
function localIso(now: Date, day: string | null = null): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  const offset = -now.getTimezoneOffset();
  const sign = offset >= 0 ? '+' : '-';
  return (
    `${day ?? localDate(now)}T${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}` +
    `${sign}${pad(Math.floor(Math.abs(offset) / 60))}:${pad(Math.abs(offset) % 60)}`
  );
}
