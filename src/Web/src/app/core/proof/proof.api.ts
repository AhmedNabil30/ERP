import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ContractType } from '../projects/projects.api';

/**
 * `GET /api/projects/{projectId}/treasury/proof` — `ProofOfNumbers.Response`, field for field.
 *
 * **Every amount is a string (D-135).** Pass it to `I18nService.formatAmount` or `<kaff-figure>`
 * directly; never `Number()` it. The browser does no arithmetic on any of these: the server computes
 * every sum, expected figure and difference, and the screen only lays them side by side.
 */

/** A posting's `PostingType` member name, as the server serialises the enum. */
export type PostingTypeName = string;

export type SourceDocumentTypeName = string;

export type CheckStatus = 'Match' | 'Mismatch' | 'Pending' | 'Unavailable';

export type LedgerKey =
  | 'ClientAdvance'
  | 'Hold'
  | 'FirmAdvance'
  | 'PettyCashAdvance'
  | 'OwnerCurrentAccount'
  | 'MaterialAdvance';

export interface ProofProject {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly contractType: ContractType;
  /** Null until a BOQ is signed (D-ruling: contract value is derived, never typed). */
  readonly contractValue: string | null;
  readonly holdRatePercent: string;
  readonly advanceRecoveryRatePercent: string;
}

/** The five comparison columns. Sign is the effect on the payment: a deduction is negative. */
export interface ProofColumns {
  readonly work: string;
  readonly hold: string;
  readonly advance: string;
  readonly materialAdvance: string;
  readonly collection: string;
}

export interface ProofEvent {
  readonly sourceDocumentType: SourceDocumentTypeName;
  readonly sourceDocumentId: string;
  readonly reference: string | null;
  readonly date: string;
  readonly actual: ProofColumns;
  /** Null while `expectedState` is `CalculatorNotImplemented`. */
  readonly expected: ProofColumns | null;
  /** Actual minus expected, computed by the server. Null whenever `expected` is. */
  readonly difference: ProofColumns | null;
  readonly postingIds: readonly string[];
}

export interface ProofCheck {
  readonly key: string;
  readonly status: CheckStatus;
  readonly expected: string | null;
  readonly actual: string | null;
  readonly difference: string | null;
  readonly postingIds: readonly string[];
}

export interface ProofLedger {
  readonly key: LedgerKey;
  /** The balance as the account view derives it. */
  readonly balance: string;
  /** The sum of this ledger's own postings' effect. The reconciliation line compares the two. */
  readonly postingsSum: string;
  readonly postingIds: readonly string[];
}

export interface ProofPostingEffect {
  readonly ledgerKey: LedgerKey;
  readonly effect: string;
}

export interface ProofPosting {
  readonly id: string;
  readonly date: string;
  readonly type: PostingTypeName;
  readonly sourceDocumentType: SourceDocumentTypeName;
  readonly sourceDocumentId: string;
  readonly sourceDocumentReference: string | null;
  readonly fromAccount: string;
  readonly toAccount: string;
  readonly amount: string;
  readonly reversesId: string | null;
  readonly effects: readonly ProofPostingEffect[];
}

export interface ProofResponse {
  readonly readAt: string;
  readonly project: ProofProject;
  /** `CalculatorNotImplemented` is a designed state, not a failure. */
  readonly expectedState: 'CalculatorNotImplemented' | 'Available';
  readonly projectCashNet: string;
  readonly events: readonly ProofEvent[];
  readonly checks: readonly ProofCheck[];
  readonly ledgers: readonly ProofLedger[];
  readonly unclassified: { readonly amount: string; readonly postingIds: readonly string[] };
  readonly rulings: readonly { readonly key: string; readonly status: 'Assumed' | 'Ruled' }[];
  readonly postings: readonly ProofPosting[];
}

/** Gated `TreasuryReadProject` + the route's project: role and assignment, server-side (D-173). */
@Injectable({ providedIn: 'root' })
export class ProofApi {
  private readonly http = inject(HttpClient);

  async get(projectId: string): Promise<ProofResponse> {
    return await firstValueFrom(
      this.http.get<ProofResponse>(`api/projects/${projectId}/treasury/proof`),
    );
  }
}
