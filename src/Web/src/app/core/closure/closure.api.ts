import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ExtractStatus } from '../extracts/extracts.api';
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

export type HoldReleaseRequestStatus = 'Requested' | 'Approved' | 'Rejected';

/**
 * A hold release request (`HoldReleaseRequestResponse`, AC-802-L, D-262). Finance asks, the Owner approves
 * and the posting is written then, or rejects with a reason that returns to Finance. It carries no amount.
 */
export interface HoldReleaseRequest {
  readonly id: string;
  readonly projectId: string;
  readonly status: HoldReleaseRequestStatus;
  readonly requestedByUserId: string;
  readonly requestedAt: string;
  readonly decidedByUserId: string | null;
  readonly decidedAt: string | null;
  readonly rejectionReason: string | null;
}

/** What the Owner's approval returned: the amount the server summed, never one the caller typed. */
export interface HoldReleased {
  readonly projectId: string;
  readonly released: string;
  readonly postingIds: readonly string[];
}

/** An extract still short of Collected or Void (`OpenExtract`). */
export interface OpenExtract {
  readonly number: number;
  readonly status: ExtractStatus;
}

/**
 * The closure read (`GetClosure`, D-257): the hold the release would move, summed from postings, and every
 * extract not yet Collected or Void. `hold` is null for a viewer who does not see money.
 */
export interface ClosureSummary {
  readonly projectId: string;
  readonly hold: string | null;
  readonly openExtracts: readonly OpenExtract[];
  /** The latest hold release request; null when none was made. Always sent by the server. */
  readonly holdReleaseRequest?: HoldReleaseRequest | null;
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

  async close(projectId: string): Promise<ProjectClosure> {
    return await firstValueFrom(this.http.post<ProjectClosure>(`${this.base(projectId)}/close`, {}));
  }

  /** Always refused by the server today (Q108): termination settlement is unruled. */
  async terminate(projectId: string, reason: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/termination`, { reason }));
  }

  async getClosure(projectId: string): Promise<ClosureSummary> {
    return await firstValueFrom(this.http.get<ClosureSummary>(`${this.base(projectId)}/closure`));
  }

  /** Finance asks for the release (D-262). No body, and nothing is posted until the Owner approves. */
  async releaseHold(projectId: string): Promise<HoldReleaseRequest> {
    return await firstValueFrom(this.http.post<HoldReleaseRequest>(`${this.base(projectId)}/hold-release`, {}));
  }

  /** The Owner approves the waiting request; the server sums the hold and writes the one release posting. */
  async approveHoldRelease(projectId: string): Promise<HoldReleased> {
    return await firstValueFrom(this.http.post<HoldReleased>(`${this.base(projectId)}/hold-release/approval`, {}));
  }

  /** The Owner rejects the waiting request with a reason; it returns to Finance. */
  async rejectHoldRelease(projectId: string, reason: string): Promise<HoldReleaseRequest> {
    return await firstValueFrom(
      this.http.post<HoldReleaseRequest>(`${this.base(projectId)}/hold-release/rejection`, { reason }),
    );
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
