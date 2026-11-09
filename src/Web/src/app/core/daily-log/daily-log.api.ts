import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** `DailyLogField` on the wire (`JsonStringEnumConverter`). Check-in and check-out take no text. */
export type DailyLogField = 'CheckIn' | 'CheckOut' | 'Weather' | 'Note' | 'StoppageReason';

/**
 * `GET …/daily-logs/mine` — KAFF-600, `AC-600-J`. **Money-free by construction**: the server projects
 * only these columns, and no member here is a rate, amount, cost or balance. Quantities and hours are
 * wire numbers (not `Money`), so they may be `number`.
 */
export interface DailyLog {
  readonly id: string;
  readonly logDate: string;
  readonly checkIn: string | null;
  readonly checkOut: string | null;
  readonly weather: string | null;
  readonly note: string | null;
  readonly stoppageReason: string | null;
  readonly quantities: readonly QuantityLine[];
  readonly dayLabour: readonly DayLabourLine[];
  readonly materials: readonly MaterialLine[];
  readonly photos: readonly PhotoLine[];
  readonly expenseDrafts: readonly ExpenseDraftLine[];
  /** KAFF-613: each check-in and check-out with its place. Optional so older fixtures still type-check. */
  readonly checkIns?: readonly CheckInLine[];
}

/** One check-in or check-out. `flagReason` is set when the place is far from the site (Q-207-3). */
export interface CheckInLine {
  readonly entryId: string;
  readonly kind: 'CheckIn' | 'CheckOut';
  readonly deviceTime: string;
  readonly serverTime: string;
  readonly latitude: number | null;
  readonly longitude: number | null;
  readonly accuracyMetres: number | null;
  readonly flagReason: 'FarFromReference' | null;
}

/** `POST …/daily-logs/check-ins`. The place is required; the server refuses a check-in without it. */
export interface CheckInEntry {
  readonly entryId: string;
  readonly occurredAt: string;
  readonly kind: 'CheckIn' | 'CheckOut';
  readonly latitude: number;
  readonly longitude: number;
  readonly accuracyMetres: number | null;
}

export interface QuantityLine {
  readonly entryId: string;
  readonly boqLineId: string;
  readonly itemCode: string | null;
  readonly descriptionAr: string;
  readonly unit: string;
  readonly quantity: number;
  readonly occurredAt: string;
}

/** `GET …/daily-logs/boq-lines` (`ListBoqLines.Response`). No rate, cost, amount or total member exists. */
export interface BoqLines {
  readonly boqId: string;
  readonly lines: readonly BoqPickLine[];
}

export interface BoqPickLine {
  readonly id: string;
  readonly position: number;
  readonly itemCode: string | null;
  readonly descriptionAr: string;
  readonly descriptionEn: string | null;
  readonly unit: string;
  readonly quantityEstimated: number | null;
  readonly quantitySurveyed: number | null;
  readonly quantityExecuted: number;
}

export interface DayLabourLine {
  readonly entryId: string;
  readonly employeeId: string;
  readonly workerName: string;
  readonly hours: number;
  readonly occurredAt: string;
}

export interface MaterialLine {
  readonly entryId: string;
  readonly materialType: string;
  readonly quantity: number;
  readonly unit: string;
  readonly isStockpile: boolean;
  readonly occurredAt: string;
}

export interface PhotoLine {
  readonly entryId: string;
  readonly fileReference: string;
  readonly caption: string | null;
  readonly isPublished: boolean;
  readonly occurredAt: string;
}

export interface ExpenseDraftLine {
  readonly entryId: string;
  readonly description: string;
  readonly occurredAt: string;
}

/** The envelope every entry carries: a device-generated id (replay-safe) and the site time. */
interface EntryEnvelope {
  readonly entryId: string;
  readonly occurredAt: string;
}

/**
 * The daily log routes under `/api/projects/{projectId}/daily-logs` — KAFF-600. Every call is gated
 * `DailyLogWrite` + `ProjectScope.FromRoute()` server-side; this class enforces nothing. The first
 * entry of the day opens the log on the server, so there is no separate "open" call here.
 */
@Injectable({ providedIn: 'root' })
export class DailyLogApi {
  private readonly http = inject(HttpClient);

  private base(projectId: string): string {
    return `api/projects/${projectId}/daily-logs`;
  }

  /** The caller's log for `date` (`yyyy-MM-dd`), or `null` when none is opened yet (`404`). */
  async getMine(projectId: string, date: string): Promise<DailyLog | null> {
    try {
      return await firstValueFrom(
        this.http.get<DailyLog>(`${this.base(projectId)}/mine`, { params: new HttpParams().set('date', date) }),
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        return null;
      }
      throw error;
    }
  }

  async recordCheckIn(projectId: string, entry: CheckInEntry): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/check-ins`, entry));
  }

  async recordField(projectId: string, entry: EntryEnvelope & { field: DailyLogField; text: string | null }): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/fields`, entry));
  }

  /**
   * `GET …/daily-logs/boq-lines` — the money-free line list to record a quantity against. `null` when
   * the project has no BOQ (`404 boq.not_found`), which is a state to show, not a failure.
   */
  async listBoqLines(projectId: string): Promise<BoqLines | null> {
    try {
      // Decimals arrive as strings on this API ("1180.0000"); the screen formats numbers.
      const wire = await firstValueFrom(this.http.get<BoqLines>(`${this.base(projectId)}/boq-lines`));
      const num = (value: number | string | null): number | null => (value === null ? null : Number(value));
      return {
        ...wire,
        lines: wire.lines.map((line) => ({
          ...line,
          quantityEstimated: num(line.quantityEstimated),
          quantitySurveyed: num(line.quantitySurveyed),
          quantityExecuted: Number(line.quantityExecuted),
        })),
      };
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        return null;
      }
      throw error;
    }
  }

  /** A period delta: what was done in this period, added to the line's executed total by the server. */
  async recordQuantity(projectId: string, entry: EntryEnvelope & { boqLineId: string; quantity: number }): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/quantities`, entry));
  }

  async recordDayLabour(projectId: string, entry: EntryEnvelope & { employeeId: string; hours: number }): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/day-labour`, entry));
  }

  async recordMaterial(
    projectId: string,
    entry: EntryEnvelope & { materialType: string; quantity: number; unit: string; isStockpile: boolean },
  ): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/materials`, entry));
  }
}
