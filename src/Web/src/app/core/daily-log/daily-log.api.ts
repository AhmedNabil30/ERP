import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * `DailyLogField` on the wire (`JsonStringEnumConverter`). Check-in and check-out take no text. Weather is gone
 * (D-217 item 16): the server refuses it, so the client cannot send it.
 */
export type DailyLogField = 'CheckIn' | 'CheckOut' | 'Note' | 'StoppageReason';

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
  /** A second note or stoppage reason the same day is kept beside the first (D-217 items 12 and 16). */
  readonly notes: readonly TextLine[];
  readonly stoppageReasons: readonly TextLine[];
  readonly quantities: readonly QuantityLine[];
  readonly dayLabour: readonly DayLabourLine[];
  readonly materials: readonly MaterialLine[];
  readonly photos: readonly PhotoLine[];
  readonly expenseDrafts: readonly ExpenseDraftLine[];
  /** KAFF-613: each check-in and check-out with its place. Optional so older fixtures still type-check. */
  readonly checkIns: readonly CheckInLine[];
  /**
   * D-217 item 8: present only when the server sends the back-dated flag, which it does for the supervising site
   * engineer and the owner. Absent for everyone else.
   */
  readonly backDated?: boolean;
}

/** A note or a stoppage reason. Free text. */
export interface TextLine {
  readonly entryId: string;
  readonly text: string;
  readonly occurredAt: string;
  readonly backDated?: boolean;
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
  /** The corrected figure: the saved quantity plus every signed delta that corrects it (D-218). */
  readonly quantity: number;
  readonly occurredAt: string;
  readonly corrected: boolean;
  /** Whether this caller may correct the line now: the verdict the correction endpoint would give. */
  readonly canCorrect?: boolean;
  readonly backDated?: boolean;
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
  /** The name of the line's باب. */
  readonly babName?: string;
}

/** One engineer's log of a day, as the review read returns it. */
export interface EngineerLog {
  readonly engineerId: string;
  readonly engineerName: string;
  readonly log: DailyLog;
}

/** Decimals may arrive as strings; the screen formats numbers. */
function normalise(wire: DailyLog): DailyLog {
  return {
    ...wire,
    quantities: wire.quantities.map((line) => ({ ...line, quantity: Number(line.quantity) })),
    materials: wire.materials.map((line) => ({ ...line, quantity: Number(line.quantity) })),
    dayLabour: wire.dayLabour.map((line) => ({ ...line, hours: Number(line.hours) })),
  };
}

export interface DayLabourLine {
  readonly entryId: string;
  readonly employeeId: string;
  readonly workerName: string;
  readonly hours: number;
  readonly occurredAt: string;
  /** Whether this caller may correct the line now: the verdict the correction endpoint would give. */
  readonly canCorrect?: boolean;
  readonly backDated?: boolean;
}

export interface MaterialLine {
  readonly entryId: string;
  readonly materialType: string;
  readonly quantity: number;
  readonly unit: string;
  readonly isStockpile: boolean;
  readonly occurredAt: string;
  readonly backDated?: boolean;
}

export interface PhotoLine {
  readonly entryId: string;
  readonly fileReference: string;
  readonly caption: string | null;
  /** The route that fetches the file (`GET`, signed-in), or `null` for a reference that is not a stored id. Use `DailyLogApi.photoUrl` to build one. */
  readonly url: string | null;
  readonly isPublished: boolean;
  readonly occurredAt: string;
  readonly backDated?: boolean;
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
      const wire = await firstValueFrom(
        this.http.get<DailyLog>(`${this.base(projectId)}/mine`, { params: new HttpParams().set('date', date) }),
      );
      return normalise(wire);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        return null;
      }
      throw error;
    }
  }

  /**
   * `GET …/daily-logs?date=` — every engineer's log of one day, for the supervising site engineer and the owner
   * (`DailyLogReview`). It is how they reach a crew entered by someone else, to correct it.
   */
  async listDayLogs(projectId: string, date: string): Promise<readonly EngineerLog[]> {
    const wire = await firstValueFrom(
      this.http.get<EngineerLog[]>(this.base(projectId), { params: new HttpParams().set('date', date) }),
    );
    return wire.map((entry) => ({ ...entry, log: normalise(entry.log) }));
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

  /**
   * Corrects a saved quantity (D-218 item 14): sends the figure the quantity should read, not the difference.
   * The server stores a signed delta that names the entry; nothing is updated or deleted.
   */
  async correctQuantity(projectId: string, entryId: string, correction: { correctionId: string; quantity: number }): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/quantities/${entryId}/corrections`, correction));
  }

  async recordDayLabour(projectId: string, entry: EntryEnvelope & { employeeId: string; hours: number }): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/day-labour`, entry));
  }

  /**
   * Corrects saved crew hours (D-217 item 3, D-218 item 16): sends the hours the saved entry should read. The server
   * stores a signed delta that names the entry; nothing is updated or deleted.
   */
  async correctDayLabour(projectId: string, entryId: string, correction: { correctionId: string; hours: number }): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/day-labour/${entryId}/corrections`, correction));
  }

  async recordMaterial(
    projectId: string,
    entry: EntryEnvelope & { materialType: string; quantity: number; unit: string; isStockpile: boolean },
  ): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/materials`, entry));
  }

  /**
   * `POST …/photos/upload` — keeps one image (JPEG, PNG or WebP, up to 10 MB by default) on the server and returns the
   * reference to hand to `recordPhoto`. The body is the file itself; the server decides the type from its bytes.
   */
  async uploadPhoto(projectId: string, file: Blob): Promise<UploadedPhoto> {
    return firstValueFrom(this.http.post<UploadedPhoto>(`${this.base(projectId)}/photos/upload`, file));
  }

  /** `POST …/photos` — records an uploaded photo on the caller's log. `fileReference` is the id `uploadPhoto` returned. */
  async recordPhoto(
    projectId: string,
    entry: EntryEnvelope & { fileReference: string; caption?: string | null },
  ): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/photos`, entry));
  }

  /** The route a recorded photo is fetched from. Needs the signed-in session, so load it through `HttpClient`, not a bare `<img src>` link to another origin. */
  photoUrl(projectId: string, reference: string): string {
    return `${this.base(projectId)}/photos/${reference}`;
  }
}

/** `POST …/photos/upload` response. */
export interface UploadedPhoto {
  readonly reference: string;
  readonly url: string;
}
