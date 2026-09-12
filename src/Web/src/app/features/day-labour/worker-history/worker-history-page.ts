import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';

import { AuthService } from '../../../core/auth/auth.service';
import { toProblem } from '../../../core/api/problem-details';
import {
  DayLabourApi,
  EngagementEntry,
  WorkerEngagementHistory,
} from '../../../core/day-labour/day-labour.api';
import { WIRE_DECIMAL_PATTERN, toWireDecimal } from '../../../core/catalogue/money-wire';
import { I18nService } from '../../../core/i18n/i18n.service';

/**
 * `S-027` · a worker's engagement history, with the day rate and the pool's three derived figures —
 * `KAFF-210`, `GET …/day-labour/engagements?workerId=`, gated `Permission.DayLabourRateManage`
 * (D-153 §1), separately from the pool's `DayLabourSiteManage` route.
 *
 * **The day rate is the one money member in this slice.** It shows only on this screen (never on
 * `S-025`'s pool, which carries no money member — D-153 §1 point 5), and it is edited only here.
 * `AC-210-G`'s three empty states are explicit `hr.worker.pool.never_engaged` text, never a `0` and
 * never a blank cell — a worker with no engagements has three `null`/`0` figures from the server and
 * this component renders every one of them as the same translated string.
 */
@Component({
  selector: 'kaff-worker-history-page',
  templateUrl: './worker-history-page.html',
  styleUrl: './worker-history-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkerHistoryPage {
  /** Bound from the route — `/projects/:projectId/day-labour/workers/:workerId/history`. */
  readonly projectId = input.required<string>();
  readonly workerId = input.required<string>();

  private readonly api = inject(DayLabourApi);
  private readonly auth = inject(AuthService);

  protected readonly i18n = inject(I18nService);

  protected readonly history = signal<WorkerEngagementHistory | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  private readonly busyEngagementId = signal<string | null>(null);
  private readonly rateDrafts = signal<ReadonlyMap<string, string>>(new Map());
  protected readonly rowFailure = signal<{ readonly engagementId: string; readonly key: string } | null>(
    null,
  );

  /**
   * `D-153 §1 point 6`: Finance reads a day rate and never writes one. Hiding the edit control for
   * Finance is convenience, same as every other client-side check here — the server refuses the write
   * regardless (`errors.master.engagement_not_responsible_engineer`), this only avoids offering a
   * control that would just come back refused.
   */
  protected readonly canEditRate = computed(() => this.auth.current()?.role !== 'Finance');

  protected readonly frequencyLabel = computed(() => {
    const frequency = this.history()?.frequency ?? 0;
    return frequency > 0 ? this.i18n.formatNumber(frequency) : this.i18n.t('hr.worker.pool.never_engaged');
  });

  protected readonly averageRatingLabel = computed(() => {
    const rating = this.history()?.averageRating ?? null;
    return rating === null
      ? this.i18n.t('hr.worker.pool.never_engaged')
      : this.i18n.formatNumber(rating, { maximumFractionDigits: 2 });
  });

  protected readonly averageDayRateLabel = computed(() => {
    const rate = this.history()?.averageDayRate ?? null;
    return rate === null ? this.i18n.t('hr.worker.pool.never_engaged') : this.i18n.formatMoney(rate);
  });

  constructor() {
    // Deferred to an `effect` — required inputs throw `NG0951` read straight from the constructor
    // body, the same reason `worker-pool-page.ts` defers its own load.
    effect(() => {
      const projectId = this.projectId();
      const workerId = this.workerId();
      void this.reload(projectId, workerId);
    });
  }

  protected trackEngagement(_index: number, engagement: EngagementEntry): string {
    return engagement.id;
  }

  protected projectLabel(engagement: EngagementEntry): string {
    const project = this.auth.current()?.projects.find((entry) => entry.projectId === engagement.projectId);
    return project?.name ?? engagement.projectId;
  }

  protected isBusy(engagementId: string): boolean {
    return this.busyEngagementId() === engagementId;
  }

  protected rateDraft(engagementId: string): string {
    return this.rateDrafts().get(engagementId) ?? '';
  }

  protected onRateInput(engagementId: string, event: Event): void {
    const target = event.target;
    if (target instanceof HTMLInputElement) {
      this.rateDrafts.update((current) => new Map(current).set(engagementId, target.value));
    }
  }

  protected async onSetRate(engagement: EngagementEntry): Promise<void> {
    const raw = this.rateDraft(engagement.id).trim();

    if (!WIRE_DECIMAL_PATTERN.test(raw)) {
      this.rowFailure.set({ engagementId: engagement.id, key: 'bab.field.markup_format_invalid' });
      return;
    }

    this.rowFailure.set(null);
    this.busyEngagementId.set(engagement.id);

    try {
      const result = await this.api.setEngagementDayRate(this.projectId(), engagement.id, toWireDecimal(raw));
      this.history.update((current) =>
        current === null
          ? current
          : {
              ...current,
              items: current.items.map((item) =>
                item.id === engagement.id ? { ...item, dayRate: result.dayRate } : item,
              ),
            },
      );
      this.rateDrafts.update((current) => {
        const next = new Map(current);
        next.delete(engagement.id);
        return next;
      });
    } catch (error) {
      this.rowFailure.set({ engagementId: engagement.id, key: toProblem(error).messageKey });
    } finally {
      this.busyEngagementId.set(null);
    }
  }

  private async reload(projectId: string, workerId: string): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);

    try {
      this.history.set(await this.api.listEngagements(projectId, workerId));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.history.set(null);
    } finally {
      this.loading.set(false);
    }
  }
}
