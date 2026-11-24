import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type PettyCashStatus = 'Draft' | 'Submitted' | 'Returned';

/** One عهدة request (`ListRequests.Row`). The amount is a wire string (D-135); `createdAt` and `decidedAt` are ISO. */
export interface PettyCashRequest {
  readonly id: string;
  readonly amount: string;
  readonly purpose: string;
  readonly drafterUserId: string;
  readonly drafterName: string;
  readonly status: PettyCashStatus;
  readonly returnedReason: string | null;
  readonly createdAt: string;
  readonly decidedAt: string | null;
}

/**
 * `/api/projects/{projectId}/petty-cash-requests`. The supervisor (`DraftSubmit`) reads every request and submits or
 * returns a junior's draft; any other reader gets only the requests they drafted. Nothing posts on either act.
 */
@Injectable({ providedIn: 'root' })
export class PettyCashApi {
  private readonly http = inject(HttpClient);

  private base(projectId: string): string {
    return `api/projects/${projectId}/petty-cash-requests`;
  }

  async list(projectId: string, status?: PettyCashStatus): Promise<readonly PettyCashRequest[]> {
    const query = status === undefined ? '' : `?status=${status}`;
    return await firstValueFrom(this.http.get<PettyCashRequest[]>(`${this.base(projectId)}${query}`));
  }

  async submit(projectId: string, id: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/${id}/submission`, {}));
  }

  async returnDraft(projectId: string, id: string, reason: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/${id}/return`, { reason }));
  }
}
