import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';

import {
  CheckInLine,
  DailyLogApi,
  DayLabourLine,
  EngineerLog,
  MaterialLine,
  PhotoLine,
  QuantityLine,
  TextLine,
} from '../../core/daily-log/daily-log.api';
import { I18nService } from '../../core/i18n/i18n.service';
import { ProjectDetail } from '../../core/projects/projects.api';
import { KaffAttendanceStrip } from '../../shared/kaff-attendance-strip/kaff-attendance-strip';
import { KaffBanner } from '../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffDaySwitch } from '../../shared/kaff-day-switch/kaff-day-switch';
import { DayValue, dayDate, dayWord, localDate } from '../../shared/kaff-day-switch/site-days';
import { KaffLinkRow } from '../../shared/kaff-link-row/kaff-link-row';
import { KaffLogEntry } from '../../shared/kaff-log-entry/kaff-log-entry';
import { KaffLogGroup } from '../../shared/kaff-log-group/kaff-log-group';
import { KaffProjectFrame } from '../../shared/kaff-project-frame/kaff-project-frame';
import { KaffState } from '../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../shared/kaff-status-pill/kaff-status-pill';

/** One engineer's block, ready to draw. */
interface Block {
  readonly id: string;
  readonly name: string;
  readonly checkIn: CheckInLine | null;
  readonly checkOut: CheckInLine | null;
  readonly backDated: boolean;
  readonly stoppages: readonly TextLine[];
  readonly quantities: readonly QuantityLine[];
  readonly materials: readonly MaterialLine[];
  readonly notes: readonly TextLine[];
  readonly photos: readonly PhotoLine[];
  readonly crew: readonly DayLabourLine[];
  /** He logged nothing: one dim line stands in for the groups. */
  readonly empty: boolean;
}

/**
 * The owner's daily log (D-225 item 1, D-226), built from `.design/v2/team-a/daily-log-review.html`: every engineer's
 * log of one day, read only. One block per engineer (name, attendance, entries by kind), a link row to the crew screen
 * where the owner may edit, and the day switch with the «يوم آخر» control for any past day.
 *
 * **Read only.** No entry form, no kind row, no primary action: nothing is put in the header action slot.
 * **The crew total is the listed crew**: distinct men and summed hours from the crew lines in the blocks below, so the
 * link row and the blocks cannot disagree. **No check-out reads «لم يُسجَّل الانصراف»**: the screen never claims the
 * engineer is still on site. **The server decides**: the distance, the corrected figure and the back-dated flag are drawn
 * only where it sent them. Photos are fetched as blobs through `HttpClient` and revoked on destroy.
 */
@Component({
  selector: 'kaff-daily-log-review-page',
  imports: [
    KaffAttendanceStrip,
    KaffBanner,
    KaffButton,
    KaffDaySwitch,
    KaffLinkRow,
    KaffLogEntry,
    KaffLogGroup,
    KaffProjectFrame,
    KaffState,
    KaffStatusPill,
  ],
  templateUrl: './daily-log-review-page.html',
  styleUrl: './daily-log-review-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DailyLogReviewPage {
  /** Bound from the route: `/projects/:projectId/daily-log`. */
  readonly projectId = input.required<string>();

  private readonly api = inject(DailyLogApi);
  protected readonly i18n = inject(I18nService);

  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly stopped = computed(() => this.project()?.status === 'Stopped');

  protected readonly today = signal(new Date());
  protected readonly dayValue = signal<DayValue>('0');
  protected readonly otherDate = signal<string | null>(null);
  protected readonly maxDate = computed(() => localDate(this.today()));

  /** The day asked of the server, `yyyy-MM-dd`: the date control's day, else the chip's. */
  protected readonly date = computed(() => this.otherDate() ?? localDate(dayDate(this.today(), Number(this.dayValue()))));
  protected readonly isToday = computed(() => this.date() === this.maxDate());

  protected readonly logs = signal<readonly EngineerLog[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  /** Object URLs of the thumbnails, by entry. */
  protected readonly thumbs = signal<Readonly<Record<string, string>>>({});
  private readonly requested = new Set<string>();
  private loadToken = 0;

  protected readonly blocks = computed<readonly Block[]>(() =>
    this.logs().map(({ engineerId, engineerName, log }) => {
      const stoppages = log.stoppageReasons;
      const quantities = log.quantities;
      const materials = log.materials;
      const notes = log.notes;
      const photos = log.photos;
      const crew = log.dayLabour;
      return {
        id: engineerId,
        name: engineerName,
        checkIn: log.checkIns.find((line) => line.kind === 'CheckIn') ?? null,
        checkOut: log.checkIns.filter((line) => line.kind === 'CheckOut').at(-1) ?? null,
        backDated: log.backDated === true,
        stoppages,
        quantities,
        materials,
        notes,
        photos,
        crew,
        empty: stoppages.length + quantities.length + materials.length + notes.length + photos.length + crew.length === 0,
      };
    }),
  );

  /** Men counted once each across the listed crew lines; hours added. The row and the blocks cannot disagree. */
  protected readonly crewSummary = computed(() => {
    const lines = this.blocks().flatMap((block) => block.crew);
    if (lines.length === 0) {
      return this.i18n.t('daily_log.crew.none');
    }
    return this.i18n.t('daily_log.crew.summary', {
      count: new Set(lines.map((line) => line.employeeId)).size,
      hours: this.i18n.formatNumber(lines.reduce((sum, line) => sum + line.hours, 0)),
    });
  });

  protected readonly emptyLine = computed(() => {
    const other = this.otherDate();
    const word = this.isToday()
      ? dayWord(this.i18n, this.today(), 0)
      : other !== null
        ? this.i18n.formatDate(new Date(`${other}T12:00:00`), { weekday: 'long' })
        : dayWord(this.i18n, this.today(), Number(this.dayValue()));
    return this.i18n.t(this.isToday() ? 'daily_log.empty' : 'daily_log.review.empty_past', { day: word });
  });

  constructor() {
    // Deferred to an `effect`: `projectId` is a required input and is not readable in the constructor.
    effect(() => {
      const id = this.projectId();
      const date = this.date();
      untracked(() => void this.load(id, date));
    });

    effect(() => {
      const photos = this.blocks().flatMap((block) => block.photos);
      untracked(() => photos.forEach((line) => void this.loadThumb(line.entryId, line.url)));
    });

    inject(DestroyRef).onDestroy(() => Object.values(this.thumbs()).forEach((url) => URL.revokeObjectURL(url)));
  }

  protected onLoaded(project: ProjectDetail): void {
    this.project.set(project);
  }

  protected reload(): void {
    void this.load(this.projectId(), this.date());
  }

  protected time(line: CheckInLine | null): string {
    return line === null ? '' : this.i18n.formatDate(new Date(line.deviceTime), { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });
  }

  protected number(value: number): string {
    return this.i18n.formatNumber(value);
  }

  private async load(projectId: string, date: string): Promise<void> {
    const token = ++this.loadToken;
    this.loading.set(true);
    this.failed.set(false);
    try {
      const logs = await this.api.listDayLogs(projectId, date);
      if (token === this.loadToken) {
        this.logs.set(logs);
      }
    } catch {
      if (token === this.loadToken) {
        this.logs.set([]);
        this.failed.set(true);
      }
    } finally {
      if (token === this.loadToken) {
        this.loading.set(false);
      }
    }
  }

  private async loadThumb(entryId: string, url: string | null): Promise<void> {
    if (url === null || this.requested.has(entryId)) {
      return;
    }
    this.requested.add(entryId);
    try {
      const objectUrl = URL.createObjectURL(await this.api.fetchPhoto(url));
      this.thumbs.update((all) => ({ ...all, [entryId]: objectUrl }));
    } catch {
      this.requested.delete(entryId);
    }
  }
}
