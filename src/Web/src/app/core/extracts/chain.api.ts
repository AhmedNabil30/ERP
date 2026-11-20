import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { AssignmentLevel, Role } from '../auth/auth.service';
import type { ChequeState, CollectionMethod, CollectRequest } from './extracts.api';

/**
 * The money and change-order reads around an extract (D-238, D-243, D-245): cheques in hand, credit notes,
 * change orders and the released-hold collection. Field for field with the server's responses; every amount is a
 * string (D-135) and every "may I" is the server's answer.
 */

/** A person a waiting line names. No contact data. */
export interface LineStaff {
  readonly userId: string;
  readonly fullName: string;
  readonly role: Role;
  readonly level: AssignmentLevel | null;
}

/** `WaitingLineResponse` (D-245 item 5): the task, who may take it now, and who holds the role but is barred. */
export interface WaitingLine {
  readonly taskKey: string;
  readonly permission: string;
  readonly holders: readonly LineStaff[];
  readonly barredHolders: readonly LineStaff[];
}

export interface ChequeCollection {
  readonly id: string;
  readonly extractId: string | null;
  readonly method: CollectionMethod;
  readonly date: string;
  /** The cheque number. */
  readonly reference: string;
  /** The face value. */
  readonly cashReceived: string;
  readonly withheldAtSource: string;
}

export interface Cheque {
  readonly id: string;
  readonly state: ChequeState;
  readonly stateChangedAt: string;
  /** Null for a cheque paying the released hold. */
  readonly extractNumber: number | null;
  readonly collection: ChequeCollection;
  readonly bounceReason: string | null;
  readonly waitingOn: WaitingLine | null;
  /** What the caller may do to the cheque now (D-246). */
  readonly actions?: ChequeActions | null;
}

export interface ChequeActions {
  readonly deposit: boolean;
  readonly clear: boolean;
  readonly bounce: boolean;
}

export type AdjustmentStatus = 'Raised' | 'Approved' | 'Rejected' | 'Reversed';

/** A credit note is an adjustment of type `ClientCreditNote` whose source document is the extract. */
export interface Adjustment {
  readonly id: string;
  readonly projectId: string;
  readonly number: number;
  readonly type: string;
  readonly status: AdjustmentStatus;
  readonly reason: string;
  readonly sourceDocumentType: string;
  readonly sourceDocumentId: string;
  readonly amount: string;
  readonly raisedByUserId: string;
  readonly raisedAt: string;
  readonly decidedByUserId: string | null;
  readonly decidedAt: string | null;
  readonly decisionReason: string | null;
  readonly disputeReason: string | null;
  /** Null once decided. */
  readonly waitingOn: WaitingLine | null;
  /** What the caller may decide now (D-246). */
  readonly actions?: AdjustmentActions | null;
  /** D-260 item 4: for a credit note on an issued extract, what the extract expects after it («بعد الإشعار»). */
  readonly extractDueAfter?: string | null;
}

export interface AdjustmentActions {
  readonly approve: boolean;
  readonly reject: boolean;
  readonly reverse: boolean;
}

export type ChangeOrderStatus =
  | 'Draft'
  | 'TechnicalReview'
  | 'ClientApproval'
  | 'OwnerApproval'
  | 'Approved'
  | 'Rejected'
  | 'Withdrawn'
  | 'Logged';

export interface ChangeOrderActions {
  readonly submit: boolean;
  readonly approveTechnicalReview: boolean;
  readonly rejectAtTechnicalReview: boolean;
  readonly recordClientApproval: boolean;
  readonly rejectAtClientApproval: boolean;
  readonly approveAsOwner: boolean;
  readonly rejectAtOwner: boolean;
  readonly withdraw: boolean;
  readonly log: boolean;
  readonly callerMayDecide: boolean;
}

export interface ChangeOrderLine {
  readonly lineNumber: number;
  readonly description: string;
  readonly unit: string;
  readonly quantity: number | string;
  readonly unitPrice: string;
  readonly total: string;
}

export interface ChangeOrderGateRecord {
  readonly gate: string;
  readonly outcome: string;
  readonly actorUserId: string;
  readonly occurredAt: string;
  readonly reason: string | null;
}

export interface ChangeOrder {
  readonly id: string;
  readonly projectId: string;
  readonly number: number;
  readonly title: string;
  readonly status: ChangeOrderStatus;
  readonly isPriced: boolean;
  readonly createdByUserId: string;
  readonly createdAt: string;
  readonly approvedAt: string | null;
  readonly gateHistory: readonly ChangeOrderGateRecord[];
  readonly actions: ChangeOrderActions;
  /** Null when the caller may not see money. */
  readonly lines: readonly ChangeOrderLine[] | null;
  readonly total: string | null;
  readonly waitingOn: WaitingLine | null;
}

/** A line as the editor sends it. The quantity is not money, so it travels as a JSON number. */
export interface NewChangeOrderLine {
  readonly description: string;
  readonly unit: string;
  readonly quantity: number;
  readonly unitPrice: string;
}

/** The change-order steps that take no body. */
export type ChangeOrderStep = 'submit' | 'technical-review' | 'client-approval' | 'owner-approval' | 'withdrawal';
export type ChangeOrderRejection = 'technical-rejection' | 'client-rejection' | 'owner-rejection';

/** `GET …/hold-release/collection` (D-245 item 2). */
export interface ReleasedHoldCollection {
  readonly projectId: string;
  readonly expected: string;
  readonly received: string;
  readonly withheld: string;
  readonly inCheques: string;
  readonly remaining: string;
}

@Injectable({ providedIn: 'root' })
export class ChainApi {
  private readonly http = inject(HttpClient);

  private project(projectId: string): string {
    return `api/projects/${projectId}`;
  }

  async cheques(projectId: string): Promise<readonly Cheque[]> {
    return await firstValueFrom(this.http.get<Cheque[]>(`${this.project(projectId)}/cheques`));
  }

  async deposit(projectId: string, id: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.project(projectId)}/cheques/${id}/deposit`, {}));
  }

  async clear(projectId: string, id: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.project(projectId)}/cheques/${id}/clearance`, {}));
  }

  async bounce(projectId: string, id: string, reason: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.project(projectId)}/cheques/${id}/bounce`, { reason }));
  }

  async adjustments(projectId: string): Promise<readonly Adjustment[]> {
    return await firstValueFrom(this.http.get<Adjustment[]>(`${this.project(projectId)}/adjustments`));
  }

  async adjustment(projectId: string, id: string): Promise<Adjustment> {
    return await firstValueFrom(this.http.get<Adjustment>(`${this.project(projectId)}/adjustments/${id}`));
  }

  async approveAdjustment(projectId: string, id: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.project(projectId)}/adjustments/${id}/approval`, {}));
  }

  async rejectAdjustment(projectId: string, id: string, reason: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.project(projectId)}/adjustments/${id}/rejection`, { reason }));
  }

  async changeOrders(projectId: string): Promise<readonly ChangeOrder[]> {
    return await firstValueFrom(this.http.get<ChangeOrder[]>(`${this.project(projectId)}/change-orders`));
  }

  async changeOrder(projectId: string, id: string): Promise<ChangeOrder> {
    return await firstValueFrom(this.http.get<ChangeOrder>(`${this.project(projectId)}/change-orders/${id}`));
  }

  async createChangeOrder(projectId: string, title: string, lines: readonly NewChangeOrderLine[]): Promise<ChangeOrder> {
    return await firstValueFrom(this.http.post<ChangeOrder>(`${this.project(projectId)}/change-orders`, { title, lines }));
  }

  async changeOrderStep(projectId: string, id: string, step: ChangeOrderStep): Promise<void> {
    await firstValueFrom(this.http.post(`${this.project(projectId)}/change-orders/${id}/${step}`, {}));
  }

  async rejectChangeOrder(projectId: string, id: string, gate: ChangeOrderRejection, reason: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.project(projectId)}/change-orders/${id}/${gate}`, { reason }));
  }

  async releasedHold(projectId: string): Promise<ReleasedHoldCollection> {
    return await firstValueFrom(this.http.get<ReleasedHoldCollection>(`${this.project(projectId)}/hold-release/collection`));
  }

  async collectReleasedHold(projectId: string, request: CollectRequest): Promise<void> {
    await firstValueFrom(this.http.post(`${this.project(projectId)}/hold-release/collect`, request));
  }
}
