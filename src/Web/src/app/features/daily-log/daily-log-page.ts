import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormField, form } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { toProblem } from '../../core/api/problem-details';
import { DailyLog, DailyLogApi, DailyLogField } from '../../core/daily-log/daily-log.api';
import { DayLabourApi, PoolWorker } from '../../core/day-labour/day-labour.api';
import { I18nService } from '../../core/i18n/i18n.service';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffField } from '../../shared/kaff-field/kaff-field';
import { KaffState } from '../../shared/kaff-state/kaff-state';

type EntryKind = 'field' | 'labour' | 'material';

/** Check-in and check-out are timestamps in themselves; the server accepts no text for them. */
const FIELDS_WITHOUT_TEXT: ReadonlySet<DailyLogField> = new Set<DailyLogField>(['CheckIn', 'CheckOut']);

const FIELD_OPTIONS: readonly { value: DailyLogField; labelKey: string }[] = [
  { value: 'CheckIn', labelKey: 'daily_log.field.check_in' },
  { value: 'CheckOut', labelKey: 'daily_log.field.check_out' },
  { value: 'Weather', labelKey: 'daily_log.field.weather' },
  { value: 'Note', labelKey: 'daily_log.field.note' },
  { value: 'StoppageReason', labelKey: 'daily_log.field.stoppage_reason' },
];

interface EntryDraft {
  text: string;
  hours: string;
  materialType: string;
  quantity: string;
  unit: string;
  isStockpile: boolean;
}

const BLANK: EntryDraft = { text: '', hours: '', materialType: '', quantity: '', unit: '', isStockpile: false };

/**
 * The engineer's daily log: `KAFF-600`, `AC-600-J`, `AC-600-K`. Mobile-first, one hand, RTL.
 *
 * **Money-free by construction (`AC-600-J`).** It reads `GET …/daily-logs/mine`, a separate read model
 * with no rate, amount, cost or balance member, and no input here accepts one. Every write is gated
 * `DailyLogWrite` plus the route's project on the server; `dailyLogWriteGuard` is convenience.
 *
 * One primary action only (the bottom bar's "Save"), which saves whichever entry kind is selected.
 * Each entry carries a device-generated id (a retry is recognised, never double-counted) and the
 * device's own time with its offset (`OccurredAt`, rule 11), never the server clock.
 *
 * Not offered here, and why: executed-quantity entry (the engineer has no money-free read of the BOQ
 * lines to pick from; `GET …/boq` is `ProjectManage` and carries cost), photo entry (no upload
 * endpoint exists, only a file reference), expense drafts (the request carries an amount, which
 * `AC-600-J` forbids on this screen). Quantities and photos are shown read-only when present.
 */
@Component({
  selector: 'kaff-daily-log-page',
  imports: [FormField, KaffField, KaffButton, KaffState, RouterLink],
  templateUrl: './daily-log-page.html',
  styleUrl: './daily-log-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DailyLogPage {
  /** Bound from the route: `/projects/:projectId/daily-log`. */
  readonly projectId = input.required<string>();

  private readonly api = inject(DailyLogApi);
  private readonly dayLabour = inject(DayLabourApi);
  private readonly auth = inject(AuthService);

  protected readonly i18n = inject(I18nService);

  protected readonly fieldOptions = FIELD_OPTIONS;

  protected readonly projectName = computed<string | null>(
    () => this.auth.current()?.projects.find((entry) => entry.projectId === this.projectId())?.name ?? null,
  );

  protected readonly log = signal<DailyLog | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadFailure = signal<string | null>(null);
  protected readonly workers = signal<readonly PoolWorker[]>([]);

  protected readonly kind = signal<EntryKind>('field');
  protected readonly selectedField = signal<DailyLogField>('Note');
  protected readonly selectedWorker = signal('');

  private readonly model = signal<EntryDraft>({ ...BLANK });
  protected readonly entryForm = form(this.model);

  protected readonly busy = signal(false);
  protected readonly errorKey = signal<string | null>(null);

  protected readonly takesText = computed(() => !FIELDS_WITHOUT_TEXT.has(this.selectedField()));

  protected readonly isEmpty = computed(() => {
    const log = this.log();
    return (
      log === null ||
      (log.checkIn === null &&
        log.checkOut === null &&
        log.weather === null &&
        log.note === null &&
        log.stoppageReason === null &&
        log.quantities.length === 0 &&
        log.dayLabour.length === 0 &&
        log.materials.length === 0 &&
        log.photos.length === 0)
    );
  });

  constructor() {
    // Deferred to an `effect`: `projectId` is a required input and is not readable in the constructor.
    effect(() => {
      const id = this.projectId();
      void this.load(id);
      void this.loadWorkers(id);
    });
  }

  protected setKind(kind: EntryKind): void {
    this.kind.set(kind);
    this.errorKey.set(null);
  }

  protected onFieldChange(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLSelectElement) {
      this.selectedField.set(target.value as DailyLogField);
    }
  }

  protected onWorkerChange(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLSelectElement) {
      this.selectedWorker.set(target.value);
    }
  }

  protected time(value: string | null): string {
    return value === null ? '' : this.i18n.formatDate(new Date(value), { hour: '2-digit', minute: '2-digit' });
  }

  protected number(value: number): string {
    return this.i18n.formatNumber(value);
  }

  protected async onSave(): Promise<void> {
    this.errorKey.set(null);
    const entry = { entryId: crypto.randomUUID(), occurredAt: localIso(new Date()) };
    const value = this.model();
    const projectId = this.projectId();

    try {
      switch (this.kind()) {
        case 'field': {
          const text = value.text.trim();
          if (this.takesText() && text.length === 0) {
            this.errorKey.set('errors.daily_log.text_required');
            return;
          }
          this.busy.set(true);
          await this.api.recordField(projectId, {
            ...entry,
            field: this.selectedField(),
            text: this.takesText() ? text : null,
          });
          break;
        }
        case 'labour': {
          const hours = positive(value.hours);
          if (this.selectedWorker() === '') {
            this.errorKey.set('errors.daily_log.worker_not_found');
            return;
          }
          if (hours === null || hours > 24) {
            this.errorKey.set('errors.daily_log.hours_out_of_range');
            return;
          }
          this.busy.set(true);
          await this.api.recordDayLabour(projectId, { ...entry, employeeId: this.selectedWorker(), hours });
          break;
        }
        case 'material': {
          const quantity = positive(value.quantity);
          if (value.materialType.trim().length === 0 || value.unit.trim().length === 0) {
            this.errorKey.set('errors.daily_log.text_required');
            return;
          }
          if (quantity === null) {
            this.errorKey.set('errors.daily_log.quantity_must_be_positive');
            return;
          }
          this.busy.set(true);
          await this.api.recordMaterial(projectId, {
            ...entry,
            materialType: value.materialType.trim(),
            quantity,
            unit: value.unit.trim(),
            isStockpile: value.isStockpile,
          });
          break;
        }
      }

      this.model.set({ ...BLANK });
      this.selectedWorker.set('');
      await this.load(projectId);
    } catch (error) {
      this.errorKey.set(toProblem(error).messageKey);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(projectId: string): Promise<void> {
    try {
      this.log.set(await this.api.getMine(projectId, localDate(new Date())));
      this.loadFailure.set(null);
    } catch (error) {
      this.loadFailure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadWorkers(projectId: string): Promise<void> {
    try {
      this.workers.set(await this.dayLabour.listPool(projectId));
    } catch {
      // Labour entry just has nobody to pick; the rest of the log still works.
      this.workers.set([]);
    }
  }
}

/** A positive finite number from a text field, or `null`. */
function positive(text: string): number | null {
  const value = Number(text.trim());
  return Number.isFinite(value) && value > 0 ? value : null;
}

/** The device's calendar date, `yyyy-MM-dd`: the day the log belongs to (rule 11). */
function localDate(now: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** ISO-8601 in the device's own offset, not UTC, so an entry at 23:50 stays on its own day. */
function localIso(now: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  const offset = -now.getTimezoneOffset();
  const sign = offset >= 0 ? '+' : '-';
  return (
    `${localDate(now)}T${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}` +
    `${sign}${pad(Math.floor(Math.abs(offset) / 60))}:${pad(Math.abs(offset) % 60)}`
  );
}
