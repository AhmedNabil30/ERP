import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { AssignmentLevel, Role } from '../auth/auth.service';
import type { WithholdingCategory } from '../projects/projects.api';
import type { AdjustmentStatus, WaitingLine } from './chain.api';

/**
 * `/api/projects/{projectId}/extracts` — KAFF-500, `ExtractResponse` field for field.
 *
 * **Every amount and every rate is a string (D-135).** The page never turns one into a number, and
 * never decides whether a control is allowed: `actions` is the server's answer, already combining
 * permission, assignment, state and separation of duties.
 */

export type ExtractStatus =
  | 'Draft'
  | 'QcSubmitted'
  | 'PartiallyApproved'
  | 'TechnicalApproved'
  | 'DeductionsApplied'
  | 'OwnerApproved'
  | 'Issued'
  | 'Collected'
  | 'Disputed'
  | 'Void';

export type ExtractGate =
  | 'Prepared'
  | 'QcReport'
  | 'TechnicalOffice'
  | 'Accounts'
  | 'Owner'
  | 'Issue'
  | 'Collect'
  | 'HeldPortionRelease'
  | 'EvidenceRevised'
  | 'Dispute'
  | 'DisputeResolution'
  | 'ChequeBounce'
  | 'Cancellation';

export type ExtractGateOutcome = 'Passed' | 'PartiallyPassed' | 'Rejected' | 'Reversed';

export type FigureKind = 'Calculated' | 'Entered' | 'Carried';

export interface ExtractSummary {
  readonly id: string;
  readonly number: number;
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly status: ExtractStatus;
  /**
   * Null when the caller holds none of the extract-money permissions (the server withholds it). Inside it,
   * every figure is a string or null; null means "does not exist yet", never zero.
   */
  readonly money: ExtractSummaryMoney | null;
  readonly waitingOn: WaitingOnBlock;
  /** On a revised extract: the cancelled original. */
  readonly revises?: ExtractLink | null;
  /** On a cancelled original: the extract that replaces it. */
  readonly revisedBy?: ExtractLink | null;
}

/** `ExtractMoney` on the list row. Certified value and net payable are null until deductions are applied; collected and outstanding until the extract is issued. */
export interface ExtractSummaryMoney {
  readonly certifiedValue: string | null;
  readonly netPayable: string | null;
  readonly collected: string | null;
  readonly outstanding: string | null;
  /** Cheques in hand, as the extract's own collection position reads them. Null before the extract is issued. */
  readonly inCheques?: string | null;
  /** What may still be recorded, from the same position. Null before the extract is issued. */
  readonly remaining?: string | null;
}

export interface GateRecord {
  readonly gate: ExtractGate;
  readonly outcome: ExtractGateOutcome;
  readonly actorUserId: string;
  readonly occurredAt: string;
  readonly reason: string | null;
  readonly approvedPortion: string | null;
  /** Who acted, by full name. */
  readonly actorName?: string | null;
}

export interface ExtractActions {
  readonly submitQcReport: boolean;
  readonly approveTechnicalOffice: boolean;
  readonly rejectAtTechnicalOffice: boolean;
  readonly applyDeductions: boolean;
  readonly rejectAtAccounts: boolean;
  readonly approveAsOwner: boolean;
  readonly rejectAtOwner: boolean;
  readonly issue: boolean;
  readonly reviseEvidence: boolean;
  readonly callerMayDecide: boolean;
  /** Record a payment or a cheque now (D-238 item 6). */
  readonly recordCollection: boolean;
  /** Deposit, clear or bounce a cheque still in hand. */
  readonly recordChequeMove: boolean;
  /** Reverse every standing payment at once. */
  readonly reverseCollection: boolean;
  readonly raiseDispute: boolean;
  readonly resolveDispute: boolean;
}

/** What an issued extract expects and has received, derived on every read (D-238 item 3, D-245 item 5). Money viewers only. */
export interface ExtractCollection {
  /** Net payable less approved credit notes. */
  readonly expected: string;
  readonly expectedWithheld: string;
  /** Received plus withheld. */
  readonly collected: string;
  /** What the cheques in hand will settle: face value plus what was withheld with them. */
  readonly inCheques: string;
  readonly remaining: string;
  readonly received: string;
  readonly withheld: string;
}

/** An approved change order billed on the extract. `amount` is null for a viewer who may not see money. */
export interface ExtractChangeOrder {
  readonly changeOrderId: string;
  readonly number: number;
  readonly title: string;
  readonly amount: string | null;
}

/** The other end of a revision link (D-241 item 2). */
export interface ExtractLink {
  readonly id: string;
  readonly number: number;
  readonly status: ExtractStatus;
}

/**
 * One BOQ line of `GET .../extracts/{id}/quantities` and of `GET .../extracts/quantities-preview` (D-248): previous,
 * current and cumulative quantity. Previous and current are null when the previous extract kept no quantities. The rate
 * and the amounts are null for a viewer who may not see extract money. An extract prepared before D-248 reads `[]`.
 */
export interface ExtractQuantityRow {
  readonly boqLineId: string;
  readonly itemCode?: string | null;
  readonly descriptionAr: string;
  readonly descriptionEn?: string | null;
  readonly unit: string;
  readonly previousQuantity?: number | null;
  readonly currentQuantity?: number | null;
  readonly cumulativeQuantity: number;
  readonly rate?: string | null;
  readonly previousAmount?: string | null;
  readonly currentAmount?: string | null;
  readonly cumulativeAmount?: string | null;
}

export interface ExtractEvidence {
  readonly cumulativeCertifiedWork: string;
  readonly previouslyCertifiedWork: string;
  readonly materialValueOnSite: string;
  readonly materialInstalled: string;
}

export interface FigureInput {
  readonly labelKey: string;
  readonly kind: FigureKind;
  readonly amount: string | null;
  readonly rate: string | null;
  readonly source: string | null;
  readonly enteredByUserId: string | null;
  readonly enteredAt: string | null;
}

export interface DocumentFigure {
  readonly key: string;
  readonly labelKey: string;
  readonly kind: FigureKind;
  readonly amount: string;
  readonly isMemo: boolean;
  readonly derivation: readonly FigureInput[];
}

export interface PrintedRow {
  readonly key: string;
  readonly labelKey: string;
  readonly isDeduction: boolean;
  readonly amount: string;
}

export interface ExtractDocument {
  readonly figures: readonly DocumentFigure[];
  readonly column: readonly PrintedRow[];
  readonly printedTotal: string;
  readonly columnSum: string;
  /** Computed by the server from the rows as printed. The page shows it and never recomputes it. */
  readonly foots: boolean;
  /** The decimals the contractual figures are printed to. */
  readonly decimals: number;
}

export interface Extract {
  readonly id: string;
  readonly projectId: string;
  readonly number: number;
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly status: ExtractStatus;
  readonly createdByUserId: string;
  readonly createdAt: string;
  readonly approvedPortion: string;
  readonly heldForNcrId: string | null;
  readonly gateHistory: readonly GateRecord[];
  readonly actions: ExtractActions;
  /** Null for a viewer who may not see money. */
  readonly evidence: ExtractEvidence | null;
  /** Null for a viewer who may not see money, and until Accounts has applied the deductions. */
  readonly document: ExtractDocument | null;
  readonly changeOrders: readonly ExtractChangeOrder[] | null;
  readonly waitingOn: WaitingOnBlock;
  /** Null for a viewer who may not see money, and before the extract is issued. */
  readonly collection: ExtractCollection | null;
  /** The client's dispute reason, once disputed. */
  readonly disputeReason?: string | null;
  /** On a revised extract: the cancelled original. */
  readonly revises?: ExtractLink | null;
  /** On a cancelled original: the extract that replaces it. */
  readonly revisedBy?: ExtractLink | null;
  /** The contract's withholding category. Null for a viewer who may not see money. */
  readonly withholdingCategory?: WithholdingCategory | null;
  /** Credit notes raised or approved against the extract. `amount` is null for a viewer who may not see money. */
  readonly creditNotes?: readonly ExtractCreditNote[] | null;
  /** Who a raised credit note waits on: the Owner, to approve it. Null when none is raised. */
  readonly creditNoteWaitingOn?: WaitingLine | null;
}

export interface ExtractCreditNote {
  readonly id: string;
  readonly number: number;
  readonly status: AdjustmentStatus;
  readonly amount: string | null;
}

export type CollectionMethod = 'Cash' | 'Cheque' | 'Transfer';
export type ChequeState = 'Received' | 'Deposited' | 'Cleared' | 'Bounced';
export type RejectionKind = 'Quantities' | 'MaterialValues';
export type DisputeResolutionKind = 'CreditNote' | 'RevisedExtract';

/** What a payment records (D-238 item 3): what arrived, and what the client withheld with it. Amounts are wire strings. */
export interface CollectRequest {
  readonly method: CollectionMethod;
  /** `yyyy-MM-dd`, the site day or earlier (D-240 item 3). */
  readonly date: string;
  readonly reference: string;
  readonly cashReceived: string;
  readonly withheldAtSource: string;
}

/** `GET …/extracts/{id}/collection`: one recorded payment. */
export interface CollectionView {
  readonly id: string;
  readonly method: CollectionMethod;
  readonly date: string;
  readonly reference: string;
  readonly cashReceived: string;
  readonly withheldAtSource: string;
  readonly reversed: boolean;
  readonly chequeId: string | null;
  readonly chequeState: ChequeState | null;
  readonly recordedAt: string;
}

/** A person on the project's staff, as the waiting-on read names them. No contact data. */
export interface WaitingStaff {
  readonly userId: string;
  readonly fullName: string;
  readonly role: Role;
  readonly level: AssignmentLevel | null;
}

/** What an extract waits for, as the list and the waiting-on read both carry it. */
export interface WaitingOnBlock {
  /** The extract's CURRENT status; the step waited for is the one that leaves it. */
  readonly status: string;
  /** The permission the next step needs; null once nothing waits. */
  readonly permission?: string | null;
  readonly roles: readonly { readonly role: Role; readonly minimumLevel: AssignmentLevel }[];
  readonly isDecisionGate: boolean;
  readonly isTerminal: boolean;
  /** Who may take the step now. */
  readonly holders: readonly WaitingStaff[];
  /** Hold the role but already acted this cycle, so separation of duties bars them. */
  readonly barredHolders: readonly WaitingStaff[];
}

/** `GET /api/projects/{id}/waiting-on`: one open extract and what it waits for. No money. */
export interface WaitingOnItem {
  readonly extractId: string;
  readonly number: number;
  readonly waitingOn: WaitingOnBlock;
}

/** The POST steps that carry no body of their own. */
export type ExtractStep =
  | 'technical-approval'
  | 'deductions'
  | 'owner-approval'
  | 'issue';

export type RejectionGate = 'technical-rejection' | 'accounts-rejection' | 'owner-rejection';

@Injectable({ providedIn: 'root' })
export class ExtractsApi {
  private readonly http = inject(HttpClient);

  private base(projectId: string): string {
    return `api/projects/${projectId}/extracts`;
  }

  async list(projectId: string): Promise<readonly ExtractSummary[]> {
    return await firstValueFrom(this.http.get<ExtractSummary[]>(this.base(projectId)));
  }

  async waitingOn(projectId: string): Promise<readonly WaitingOnItem[]> {
    return await firstValueFrom(this.http.get<WaitingOnItem[]>(`api/projects/${projectId}/waiting-on`));
  }

  async get(projectId: string, id: string): Promise<Extract> {
    return await firstValueFrom(this.http.get<Extract>(`${this.base(projectId)}/${id}`));
  }

  async step(projectId: string, id: string, step: ExtractStep): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/${id}/${step}`, {}));
  }

  /** D-242 item 2: a rejection names where the error is, so it lands on the person who can change it. */
  async reject(projectId: string, id: string, gate: RejectionGate, reason: string, kind: RejectionKind): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/${id}/${gate}`, { reason, kind }));
  }

  /** D-238 item 4: the supervising engineer enters both material values with the QC report. */
  async submitQcReport(projectId: string, id: string, materialValueOnSite: string, materialInstalled: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/${id}/qc-report`, { materialValueOnSite, materialInstalled }));
  }

  /** No body: the revision re-reads the corrected daily logs (D-238 item 4). */
  async reviseEvidence(projectId: string, id: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/${id}/evidence-revision`, {}));
  }

  /** The junior site engineer starts the extract with the period only (D-238 item 4, D-241 item 1). */
  async prepare(projectId: string, periodStart: string, periodEnd: string): Promise<Extract> {
    return await firstValueFrom(this.http.post<Extract>(this.base(projectId), { periodStart, periodEnd }));
  }

  async collect(projectId: string, id: string, request: CollectRequest): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/${id}/collect`, request));
  }

  async collections(projectId: string, id: string): Promise<readonly CollectionView[]> {
    return await firstValueFrom(this.http.get<CollectionView[]>(`${this.base(projectId)}/${id}/collection`));
  }

  async reverseCollection(projectId: string, id: string, reason: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/${id}/collection-reversal`, { reason }));
  }

  async raiseDispute(projectId: string, id: string, reason: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/${id}/dispute`, { reason }));
  }

  /** A price dispute ends in a credit note of `amount`; a quantity dispute in a revised extract (amount 0). */
  async resolveDispute(projectId: string, id: string, kind: DisputeResolutionKind, reason: string, amount: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/${id}/dispute-resolution`, { kind, reason, amount }));
  }
}
