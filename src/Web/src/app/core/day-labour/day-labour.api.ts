import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { PhoneMatch } from '../../shared/phone-match';
import { BabOption } from '../employees/employees.api';

export type { BabOption, PhoneMatch };

/**
 * `POST …/day-labour/phone-check`'s masked branch — decisions.md D-140 point 6. No id, code or name:
 * a salaried match is salaried-register data and this route never exposes it, only that it exists.
 */
export interface RestrictedPhoneMatch {
  readonly restricted: true;
}

/** `PhoneCheck.Response.Matches` — each element is one of the two shapes, no discriminator field. */
export type WorkerPhoneMatch = PhoneMatch | RestrictedPhoneMatch;

export function isRestrictedMatch(match: WorkerPhoneMatch): match is RestrictedPhoneMatch {
  return (match as RestrictedPhoneMatch).restricted === true;
}

/** `POST …/day-labour`'s body — KAFF-209. No `Kind` member; the server always forces `DayLabour`. */
export interface WorkerRegister {
  readonly fullName: string;
  readonly phone: string;
  readonly babId: string | null;
  readonly specialty: string | null;
  readonly acknowledgedDuplicatePhone: boolean;
}

/** `POST …/day-labour`'s response. No day rate, wage or money member anywhere — rule 8, AC-209-G. */
export interface WorkerFile {
  readonly id: string;
  readonly code: string;
  readonly fullName: string;
  readonly phone: string;
  readonly babId: string | null;
  readonly specialty: string | null;
  readonly isActive: boolean;
}

/**
 * `GET …/day-labour`'s pool row — D-140 point 4's allow-list, plus the two money-free figures
 * D-153 §1 point 5 added. **No money member here** — the average day rate is a pool figure but stays
 * on the rate-gated `ListEngagements` read alone (KAFF-210, D-153 §1).
 */
export interface PoolWorker extends WorkerFile {
  /** A count of engagements (D-139 §3), not days and not projects. `0` renders as "never engaged". */
  readonly frequency: number;
  /** The average of the recorded ratings, as a wire decimal string, or `null` when none is rated. */
  readonly averageRating: string | null;
}

/** One engagement in a worker's history, on the route's project. KAFF-210. */
export interface EngagementEntry {
  readonly id: string;
  readonly projectId: string;
  readonly openedOn: string;
  readonly closedOn: string | null;
  /** `Money`, a wire decimal string — never `float`/`double`/`Number()` — or `null` if not yet agreed. */
  readonly dayRate: string | null;
  readonly rating: number | null;
}

/**
 * `GET …/engagements?workerId=` — one worker's engagement history on the route's project, and the
 * pool's three figures for him, derived on every read and never stored (KAFF-210 rule 2). Gated
 * `DayLabourRateManage` (D-153 §1) — the only read that carries a money member.
 */
export interface WorkerEngagementHistory {
  readonly workerId: string;
  readonly items: readonly EngagementEntry[];
  /** A wire decimal string, or `null` when no engagement carries a rate. */
  readonly averageDayRate: string | null;
  readonly frequency: number;
  readonly averageRating: string | null;
}

/** `POST …/day-labour/engagements`'s response. No money member — Q76 unruled. */
export interface EngagementOpened {
  readonly id: string;
  readonly workerId: string;
  readonly projectId: string;
  readonly openedOn: string;
}

export interface EngagementClosed {
  readonly id: string;
  readonly closedOn: string;
}

export interface EngagementRated {
  readonly id: string;
  readonly rating: number;
}

/**
 * The day-labour routes, all under `/api/projects/{projectId}/day-labour` — KAFF-209, KAFF-210.
 * Every call is `ProjectScoped` server-side; this class enforces nothing, CLAUDE.md's own rule for
 * the client half of any permission. Most of it is gated `Permission.DayLabourSiteManage` — the
 * exceptions are {@link listEngagements} and {@link setEngagementDayRate}, gated
 * `Permission.DayLabourRateManage` instead (D-153 §1), because that row also reaches Finance, who
 * holds no `DayLabourSiteManage`.
 */
@Injectable({ providedIn: 'root' })
export class DayLabourApi {
  private readonly http = inject(HttpClient);

  private base(projectId: string): string {
    return `api/projects/${projectId}/day-labour`;
  }

  /** `GET …/day-labour/babs` — D-140 point 7, the same `BabOption` shape as the employee register. */
  async listBabs(projectId: string): Promise<readonly BabOption[]> {
    const response = await firstValueFrom(
      this.http.get<{ items: BabOption[] }>(`${this.base(projectId)}/babs`),
    );
    return response.items;
  }

  /** `POST …/day-labour/phone-check`. `200` either way — an empty array means nobody holds this number. */
  async phoneCheck(projectId: string, phone: string): Promise<readonly WorkerPhoneMatch[]> {
    const response = await firstValueFrom(
      this.http.post<{ matches: WorkerPhoneMatch[] }>(`${this.base(projectId)}/phone-check`, { phone }),
    );
    return response.matches;
  }

  /** `POST …/day-labour` — `201`, or `409 errors.master.duplicate_phone_not_acknowledged`. */
  async register(projectId: string, worker: WorkerRegister): Promise<WorkerFile> {
    return await firstValueFrom(this.http.post<WorkerFile>(this.base(projectId), worker));
  }

  /** `GET …/day-labour` — the company-wide pool (D-140 point 3), for picking a worker. */
  async listPool(projectId: string): Promise<readonly PoolWorker[]> {
    const response = await firstValueFrom(
      this.http.get<{ items: PoolWorker[] }>(this.base(projectId)),
    );
    return response.items;
  }

  /** `POST …/day-labour/engagements` — `WorkerId` only, no day rate (Q76 unruled). */
  async openEngagement(projectId: string, workerId: string): Promise<EngagementOpened> {
    return await firstValueFrom(
      this.http.post<EngagementOpened>(`${this.base(projectId)}/engagements`, { workerId }),
    );
  }

  /** `POST …/engagements/{id}/close` — explicit manual close, never a timeout (rule 6). */
  async closeEngagement(projectId: string, engagementId: string): Promise<EngagementClosed> {
    return await firstValueFrom(
      this.http.post<EngagementClosed>(`${this.base(projectId)}/engagements/${engagementId}/close`, {}),
    );
  }

  /** `POST …/engagements/{id}/rate` — out of 5 (D-139 §3). Server refuses outside 1-5 either way. */
  async rateEngagement(projectId: string, engagementId: string, rating: number): Promise<EngagementRated> {
    return await firstValueFrom(
      this.http.post<EngagementRated>(`${this.base(projectId)}/engagements/${engagementId}/rate`, { rating }),
    );
  }

  /**
   * `GET …/engagements?workerId=` — `S-027`'s history load. Gated `DayLabourRateManage`, **not**
   * `DayLabourSiteManage` (D-153 §1) — the class comment's own blanket claim does not cover this one
   * call, which is why `WorkerHistoryPage` sits behind `dayLabourRateManageGuard` instead of this
   * feature's usual parent route.
   */
  async listEngagements(projectId: string, workerId: string): Promise<WorkerEngagementHistory> {
    const params = new HttpParams().set('workerId', workerId);
    return await firstValueFrom(
      this.http.get<WorkerEngagementHistory>(`${this.base(projectId)}/engagements`, { params }),
    );
  }

  /**
   * `PUT …/engagements/{id}/day-rate` — records the agreed day rate. `dayRate` is the wire decimal
   * string (D-135) — never `Number()` it going in or reading the response. `404`/`403` on a mismatched
   * project (rule 6a), `400 errors.master.engagement_day_rate_required` on an omitted rate,
   * `400 errors.master.engagement_not_responsible_engineer` on a non-opener, non-Owner caller.
   */
  async setEngagementDayRate(
    projectId: string,
    engagementId: string,
    dayRate: string,
  ): Promise<{ readonly engagementId: string; readonly dayRate: string }> {
    return await firstValueFrom(
      this.http.put<{ engagementId: string; dayRate: string }>(
        `${this.base(projectId)}/engagements/${engagementId}/day-rate`,
        { dayRate },
      ),
    );
  }
}
