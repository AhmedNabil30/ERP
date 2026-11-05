import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ProjectStatus } from '../projects/projects.api';

/**
 * The closure chain of a project (KAFF-800, 802, 803, 804, 807): practical completion, the snag list,
 * handover, the hold release, warranty callbacks and closing. Field for field with
 * `src/Api/Features/Closure`. Every act is gated by role and by assignment on the server; the screen reads
 * the session only to decide what to render.
 *
 * **No act takes an amount.** The hold release in particular has no body: the server sums the hold from
 * its own postings and returns what it released as a string (D-135).
 */
export type SnagStatus = 'Open' | 'Fixed' | 'Verified';
export type SnagPriority = 'Minor' | 'Major';
export type SnagOwnerKind = 'Kaff' | 'Subcontractor';
export type SnagResolutionKind = 'DebitNote' | 'Absorbed';

export interface Snag {
  readonly id: string;
  readonly number: number;
  readonly description: string;
  readonly location: string;
  readonly ownerKind: SnagOwnerKind;
  readonly subcontractorId: string | null;
  readonly priority: SnagPriority;
  readonly status: SnagStatus;
  readonly blocksHandover: boolean;
  readonly resolutionKind: SnagResolutionKind | null;
  readonly resolutionReason: string | null;
  readonly raisedAt: string;
  readonly fixedAt: string | null;
  readonly verifiedAt: string | null;
  readonly resolvedAt: string | null;
}

export interface NewSnag {
  readonly description: string;
  readonly location: string;
  readonly ownerKind: SnagOwnerKind;
  readonly subcontractorId: string | null;
  readonly priority: SnagPriority;
}

export interface WarrantyCallback {
  readonly id: string;
  readonly number: number;
  readonly description: string;
  readonly raisedOn: string;
  readonly isResolved: boolean;
  readonly resolvedAt: string | null;
}

/** The project after a closure act (`ProjectClosureResponse`). */
export interface ProjectClosure {
  readonly id: string;
  readonly status: ProjectStatus;
  readonly handoverOn: string | null;
  readonly warrantyEndsOn: string | null;
  readonly closedOn: string | null;
}

/** What the hold release returned: the amount the server summed, never one the caller typed. */
export interface HoldReleased {
  readonly projectId: string;
  readonly released: string;
  readonly postingIds: readonly string[];
}

@Injectable({ providedIn: 'root' })
export class ClosureApi {
  private readonly http = inject(HttpClient);

  private base(projectId: string): string {
    return `api/projects/${projectId}`;
  }

  async declarePracticalCompletion(projectId: string): Promise<ProjectClosure> {
    return await firstValueFrom(this.http.post<ProjectClosure>(`${this.base(projectId)}/practical-completion`, {}));
  }

  async handOver(projectId: string): Promise<ProjectClosure> {
    return await firstValueFrom(this.http.post<ProjectClosure>(`${this.base(projectId)}/handover`, {}));
  }

  async beginWarranty(projectId: string): Promise<ProjectClosure> {
    return await firstValueFrom(this.http.post<ProjectClosure>(`${this.base(projectId)}/warranty`, {}));
  }

  async close(projectId: string): Promise<ProjectClosure> {
    return await firstValueFrom(this.http.post<ProjectClosure>(`${this.base(projectId)}/close`, {}));
  }

  /** Always refused by the server today (Q108): termination settlement is unruled. */
  async terminate(projectId: string, reason: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/termination`, { reason }));
  }

  /** No body: the amount is read from the hold ledger on the server. */
  async releaseHold(projectId: string): Promise<HoldReleased> {
    return await firstValueFrom(this.http.post<HoldReleased>(`${this.base(projectId)}/hold-release`, {}));
  }

  async listSnags(projectId: string): Promise<readonly Snag[]> {
    return await firstValueFrom(this.http.get<Snag[]>(`${this.base(projectId)}/snags`));
  }

  async raiseSnag(projectId: string, snag: NewSnag): Promise<Snag> {
    return await firstValueFrom(this.http.post<Snag>(`${this.base(projectId)}/snags`, snag));
  }

  async fixSnag(projectId: string, id: string): Promise<Snag> {
    return await firstValueFrom(this.http.post<Snag>(`${this.base(projectId)}/snags/${id}/fix`, {}));
  }

  async verifySnag(projectId: string, id: string): Promise<Snag> {
    return await firstValueFrom(this.http.post<Snag>(`${this.base(projectId)}/snags/${id}/verification`, {}));
  }

  async resolveSnag(projectId: string, id: string, kind: SnagResolutionKind, reason: string): Promise<Snag> {
    return await firstValueFrom(
      this.http.post<Snag>(`${this.base(projectId)}/snags/${id}/resolution`, { kind, reason }),
    );
  }

  async listCallbacks(projectId: string): Promise<readonly WarrantyCallback[]> {
    return await firstValueFrom(this.http.get<WarrantyCallback[]>(`${this.base(projectId)}/warranty-callbacks`));
  }

  async raiseCallback(projectId: string, description: string): Promise<WarrantyCallback> {
    return await firstValueFrom(
      this.http.post<WarrantyCallback>(`${this.base(projectId)}/warranty-callbacks`, { description }),
    );
  }

  async resolveCallback(projectId: string, id: string): Promise<WarrantyCallback> {
    return await firstValueFrom(
      this.http.post<WarrantyCallback>(`${this.base(projectId)}/warranty-callbacks/${id}/resolution`, {}),
    );
  }
}
