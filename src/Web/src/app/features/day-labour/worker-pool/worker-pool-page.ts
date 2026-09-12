import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { toProblem } from '../../../core/api/problem-details';
import { DayLabourApi, PoolWorker } from '../../../core/day-labour/day-labour.api';
import { I18nService } from '../../../core/i18n/i18n.service';

/**
 * One worker's engagement, tracked locally from the moment this screen opens it. KAFF-210.
 *
 * **There is no read endpoint for a worker's engagement history** — `KAFF-210`'s own commit note
 * (660de2f) flags the pool's derived read (`S-025`/`S-027`) as held on Q76/Q78, so an engagement this
 * screen did not itself open is invisible to it. That is the honest shape of what is built today, not
 * a shortcut: opening one here is the only way this session learns its id, and closing or rating it
 * needs exactly that id, which the open call already returned.
 */
interface TrackedEngagement {
  readonly id: string;
  readonly openedOn: string;
  readonly closedOn: string | null;
  readonly rating: number | null;
}

/** `RateEngagement.Request`'s own range — D-139 §3. Exported so the client-side echo is unit-tested. */
export function ratingOutOfRange(rating: number): boolean {
  return !Number.isInteger(rating) || rating < 1 || rating > 5;
}

/**
 * `S-025` · the day-labour pool, and this slice's share of `S-027` · engagement open/close/rate —
 * `KAFF-209`, `KAFF-210`.
 *
 * **No day rate is shown here, and never will be — D-153 §1 point 5.** The average day rate is a
 * money figure and stays on `WorkerHistoryPage`'s rate-gated read alone. Frequency and average rating
 * are not money, so `ListPool` carries them and this screen renders them directly, both derived on
 * every read and never stored (rule 2) — adding a client-computed figure here would be the same
 * mistake with extra steps.
 */
@Component({
  selector: 'kaff-worker-pool-page',
  imports: [RouterLink],
  templateUrl: './worker-pool-page.html',
  styleUrl: './worker-pool-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkerPoolPage {
  /** Bound from the route — `/projects/:projectId/day-labour`. */
  readonly projectId = input.required<string>();

  private readonly api = inject(DayLabourApi);
  private readonly auth = inject(AuthService);

  protected readonly i18n = inject(I18nService);

  /** `DayLabourRateManage` is `ProjectScoped` (D-153 §1) — read off this project's own entry. */
  protected readonly canSeeRates = (): boolean => {
    const project = this.auth.current()?.projects.find((entry) => entry.projectId === this.projectId());
    return project?.permissions.includes('DayLabourRateManage') ?? false;
  };

  protected frequencyLabel(worker: PoolWorker): string {
    return worker.frequency > 0
      ? this.i18n.formatNumber(worker.frequency)
      : this.i18n.t('hr.worker.pool.never_engaged');
  }

  protected averageRatingLabel(worker: PoolWorker): string {
    return worker.averageRating === null
      ? this.i18n.t('hr.worker.pool.never_engaged')
      : this.i18n.formatNumber(worker.averageRating, { maximumFractionDigits: 2 });
  }

  protected readonly workers = signal<readonly PoolWorker[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  private readonly engagements = signal<ReadonlyMap<string, TrackedEngagement>>(new Map());
  private readonly busyWorkerId = signal<string | null>(null);
  protected readonly rowFailure = signal<{ readonly workerId: string; readonly key: string } | null>(null);

  private readonly ratingDrafts = signal<ReadonlyMap<string, string>>(new Map());

  constructor() {
    // Deferred to an `effect`: `projectId` is a required input, and reading it straight from the
    // constructor body throws `NG0951` before Angular (or a test's `setInput`) has applied it — see
    // `worker-register-page.ts`'s own note.
    effect(() => {
      const id = this.projectId();
      void this.reload(id);
    });
  }

  protected trackRow(_index: number, worker: PoolWorker): string {
    return worker.id;
  }

  protected engagementFor(workerId: string): TrackedEngagement | null {
    return this.engagements().get(workerId) ?? null;
  }

  protected isBusy(workerId: string): boolean {
    return this.busyWorkerId() === workerId;
  }

  protected ratingDraft(workerId: string): string {
    return this.ratingDrafts().get(workerId) ?? '';
  }

  protected onRatingInput(workerId: string, event: Event): void {
    const target = event.target;
    if (target instanceof HTMLInputElement) {
      this.ratingDrafts.update((current) => new Map(current).set(workerId, target.value));
    }
  }

  protected async onEngage(worker: PoolWorker): Promise<void> {
    this.rowFailure.set(null);
    this.busyWorkerId.set(worker.id);

    try {
      const opened = await this.api.openEngagement(this.projectId(), worker.id);
      this.engagements.update((current) =>
        new Map(current).set(worker.id, {
          id: opened.id,
          openedOn: opened.openedOn,
          closedOn: null,
          rating: null,
        }),
      );
    } catch (error) {
      this.rowFailure.set({ workerId: worker.id, key: toProblem(error).messageKey });
    } finally {
      this.busyWorkerId.set(null);
    }
  }

  protected async onClose(worker: PoolWorker): Promise<void> {
    const engagement = this.engagementFor(worker.id);
    if (engagement === null) {
      return;
    }

    this.rowFailure.set(null);
    this.busyWorkerId.set(worker.id);

    try {
      const closed = await this.api.closeEngagement(this.projectId(), engagement.id);
      this.engagements.update((current) =>
        new Map(current).set(worker.id, { ...engagement, closedOn: closed.closedOn }),
      );
    } catch (error) {
      this.rowFailure.set({ workerId: worker.id, key: toProblem(error).messageKey });
    } finally {
      this.busyWorkerId.set(null);
    }
  }

  /** Refuses `0` and `6` client-side (`AC-210-F`'s echo) — the server refuses the same range anyway. */
  protected async onRate(worker: PoolWorker): Promise<void> {
    const engagement = this.engagementFor(worker.id);
    if (engagement === null) {
      return;
    }

    const rating = Number(this.ratingDraft(worker.id));

    if (ratingOutOfRange(rating)) {
      this.rowFailure.set({ workerId: worker.id, key: 'hr.worker.engagement.rating_out_of_range' });
      return;
    }

    this.rowFailure.set(null);
    this.busyWorkerId.set(worker.id);

    try {
      const rated = await this.api.rateEngagement(this.projectId(), engagement.id, rating);
      this.engagements.update((current) =>
        new Map(current).set(worker.id, { ...engagement, rating: rated.rating }),
      );
    } catch (error) {
      this.rowFailure.set({ workerId: worker.id, key: toProblem(error).messageKey });
    } finally {
      this.busyWorkerId.set(null);
    }
  }

  private async reload(projectId: string): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);

    try {
      this.workers.set(await this.api.listPool(projectId));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.workers.set([]);
    } finally {
      this.loading.set(false);
    }
  }
}
