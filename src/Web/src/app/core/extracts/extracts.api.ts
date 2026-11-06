import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { AssignmentLevel, Role } from '../auth/auth.service';

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
  | 'Collected';

export type ExtractGate =
  | 'Prepared'
  | 'QcReport'
  | 'TechnicalOffice'
  | 'Accounts'
  | 'Owner'
  | 'Issue'
  | 'Collect'
  | 'HeldPortionRelease'
  | 'EvidenceRevised';

export type ExtractGateOutcome = 'Passed' | 'PartiallyPassed' | 'Rejected';

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
}

/** `ExtractMoney` on the list row. Certified value and net payable are null until deductions are applied; collected and outstanding until the extract is issued. */
export interface ExtractSummaryMoney {
  readonly certifiedValue: string | null;
  readonly netPayable: string | null;
  readonly collected: string | null;
  readonly outstanding: string | null;
}

export interface GateRecord {
  readonly gate: ExtractGate;
  readonly outcome: ExtractGateOutcome;
  readonly actorUserId: string;
  readonly occurredAt: string;
  readonly reason: string | null;
  readonly approvedPortion: string | null;
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
  | 'qc-report'
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

  async reject(projectId: string, id: string, gate: RejectionGate, reason: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base(projectId)}/${id}/${gate}`, { reason }));
  }

  async reviseEvidence(
    projectId: string,
    id: string,
    materialValueOnSite: string,
    materialInstalled: string,
  ): Promise<void> {
    await firstValueFrom(
      this.http.post(`${this.base(projectId)}/${id}/evidence-revision`, {
        materialValueOnSite,
        materialInstalled,
      }),
    );
  }
}
