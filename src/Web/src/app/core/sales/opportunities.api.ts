import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** The six stages, in pipeline order. `Contract` is reached only by conversion to a project (D-268). */
export const OPPORTUNITY_STAGES = ['Lead', 'Meeting', 'SiteVisit', 'Quotation', 'Negotiation', 'Contract'] as const;
export type OpportunityStage = (typeof OPPORTUNITY_STAGES)[number];

export type OpportunityStatus = 'Active' | 'Stalled' | 'OnHold' | 'ClosedWon' | 'ClosedLost';
export type InactivityLevel = 'None' | 'FirstAlert' | 'SecondAlert' | 'Stalled';

/** One row of `GET /api/opportunities`, field for field. */
export interface OpportunitySummary {
  readonly id: string;
  readonly code: string;
  readonly clientId: string;
  readonly clientName: string;
  readonly clientPhone: string;
  readonly title: string;
  readonly stage: OpportunityStage;
  readonly status: OpportunityStatus;
  readonly inactivity: InactivityLevel;
  readonly closedLostReason: string | null;
  readonly lastActivityAt: string;
}

export interface OpportunityFilter {
  readonly stage: OpportunityStage | '';
  readonly status: OpportunityStatus | '';
  readonly search: string;
  readonly followUp: boolean;
}

export type OpportunityAct = 'advance' | 'hold' | 'resume' | 'close-lost' | 'reopen';

/**
 * The opportunity calls (R3-10). Every call is gated `OpportunityManage` by the server.
 * There is no single-opportunity read, so {@link get} reads the list and picks the row.
 */
@Injectable({ providedIn: 'root' })
export class OpportunitiesApi {
  private readonly http = inject(HttpClient);

  async list(filter: Partial<OpportunityFilter> = {}): Promise<readonly OpportunitySummary[]> {
    let params = new HttpParams();
    if (filter.stage) params = params.set('stage', filter.stage);
    if (filter.status) params = params.set('status', filter.status);
    if (filter.search?.trim()) params = params.set('search', filter.search.trim());
    if (filter.followUp) params = params.set('followUp', 'true');

    const response = await firstValueFrom(
      this.http.get<{ opportunities: OpportunitySummary[] }>('api/opportunities', { params }),
    );
    return response.opportunities;
  }

  // ponytail: no GET by id on the server; the list is read and filtered here. Add the endpoint when the list outgrows one page.
  async get(id: string): Promise<OpportunitySummary | null> {
    return (await this.list()).find((o) => o.id === id) ?? null;
  }

  async create(clientId: string, title: string): Promise<{ id: string }> {
    return await firstValueFrom(this.http.post<{ id: string }>('api/opportunities', { clientId, title }));
  }

  /** `204`. `close-lost` carries the reason; `reopen` an optional note. */
  async act(id: string, act: OpportunityAct, reason: string | null = null): Promise<void> {
    const body = act === 'close-lost' || act === 'reopen' ? { reason } : {};
    await firstValueFrom(this.http.post<void>(`api/opportunities/${id}/${act}`, body));
  }
}
