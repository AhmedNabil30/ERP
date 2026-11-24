import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * The accounting calls (R4). Every amount is a string off the wire and goes back as one: the browser does no
 * arithmetic on money (D-135). Dates are `yyyy-MM-dd` strings, never `Date` objects, so a time zone cannot move a day.
 *
 * Only the company routes are called. A project-tagged accrual, prepayment or asset needs the project's own
 * cost account, and no endpoint lists accounts (reported as a server gap).
 */
export type AssetCategory = 'ComputersSoftwareData' | 'OtherBusinessAssets' | 'Buildings' | 'PurchasedIntangibles';
export const ASSET_CATEGORIES: readonly AssetCategory[] = [
  'ComputersSoftwareData',
  'OtherBusinessAssets',
  'Buildings',
  'PurchasedIntangibles',
];

export type RequestKind = 'Accrual' | 'AccrualRelease' | 'PrepaymentAmortisation' | 'Depreciation';
export type RequestStatus = 'Requested' | 'Approved' | 'Rejected';

export interface AccrualRow {
  readonly id: string;
  readonly amount: string;
  readonly accruedOn: string;
  readonly description: string | null;
  readonly isOpen: boolean;
  readonly releasedOn: string | null;
}

export interface PrepaymentRow {
  readonly id: string;
  readonly description: string | null;
  readonly totalAmount: string;
  readonly months: number;
  readonly firstMonth: string;
  readonly amortised: string;
  readonly remaining: string;
}

export interface AssetRow {
  readonly id: string;
  readonly name: string;
  readonly category: AssetCategory;
  readonly cost: string;
  readonly ratePercent: number;
  readonly purchaseDate: string;
  readonly accumulated: string;
  readonly bookValue: string;
  readonly monthlyDepreciation: string;
}

export interface RequestRow {
  readonly id: string;
  readonly kind: RequestKind;
  readonly status: RequestStatus;
  /** The request's own JSON, as stored. Read only for the amount, description and month the row names. */
  readonly payload: string;
  readonly requestedAt: string;
  readonly decidedAt: string | null;
  readonly rejectionReason: string | null;
  /** The dry run's total, stored when Finance asked; the Owner approves this figure (D-275). Null on older rows. */
  readonly amount: string | null;
  readonly lineCount: number | null;
  readonly askedByName: string | null;
  readonly decidedByName: string | null;
}

export interface TrialBalanceLine {
  readonly accountId: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly type: string;
  readonly projectId: string | null;
  readonly projectName: string | null;
  readonly debit: string;
  readonly credit: string;
}

export interface TrialBalance {
  readonly lines: readonly TrialBalanceLine[];
  readonly totalDebit: string;
  readonly totalCredit: string;
  readonly isBalanced: boolean;
}

export interface OverheadLine {
  readonly projectId: string;
  readonly projectCode: string;
  readonly projectName: string | null;
  readonly size: string;
  readonly overheadShare: string;
  readonly revenue: string;
  readonly projectCost: string;
  readonly grossMargin: string;
  readonly netMargin: string;
}

export interface OverheadSpread {
  readonly month: string;
  readonly overhead: string;
  readonly projects: readonly OverheadLine[];
  readonly sharesTotal: string;
}

export interface Baseline {
  readonly costTotal: string;
  readonly tolerancePercent: string;
  readonly limit: string;
  readonly setAt: string;
}

export interface PostingOption {
  readonly id: string;
  readonly postingDate: string;
  readonly inflow: string;
  readonly isReversal: boolean;
  readonly reversesId: string | null;
}

@Injectable({ providedIn: 'root' })
export class AccountingApi {
  private readonly http = inject(HttpClient);

  accruals(): Promise<readonly AccrualRow[]> {
    return firstValueFrom(this.http.get<AccrualRow[]>('api/treasury/accruals'));
  }

  prepayments(): Promise<readonly PrepaymentRow[]> {
    return firstValueFrom(this.http.get<PrepaymentRow[]>('api/treasury/prepayments'));
  }

  assets(): Promise<readonly AssetRow[]> {
    return firstValueFrom(this.http.get<AssetRow[]>('api/treasury/assets'));
  }

  requests(): Promise<readonly RequestRow[]> {
    return firstValueFrom(this.http.get<RequestRow[]>('api/treasury/accounting-requests'));
  }

  trialBalance(from: string, to: string): Promise<TrialBalance> {
    return firstValueFrom(this.http.get<TrialBalance>('api/treasury/trial-balance', { params: this.range(from, to) }));
  }

  /** The CSV is the server's file, UTF-8 with a byte-order mark. The page hands the blob to the browser unchanged. */
  trialBalanceCsv(from: string, to: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get('api/treasury/trial-balance/export', { params: this.range(from, to), responseType: 'blob' }),
    );
  }

  overheadSpread(month: string): Promise<OverheadSpread> {
    return firstValueFrom(this.http.get<OverheadSpread>('api/treasury/overhead-spread', { params: { month } }));
  }

  baseline(projectId: string): Promise<Baseline> {
    return firstValueFrom(this.http.get<Baseline>(`api/projects/${projectId}/budget-baseline`));
  }

  setBaseline(projectId: string, tolerancePercent: string): Promise<Baseline> {
    const tolerance = tolerancePercent.trim();
    return firstValueFrom(
      this.http.post<Baseline>(`api/projects/${projectId}/budget-baseline`, {
        tolerancePercent: tolerance === '' ? null : tolerance,
      }),
    );
  }

  /** Company accounts the forms need, by type, from `GET /api/treasury/accounts`. The first of each type wins. */
  async accountsOfType(types: readonly string[]): Promise<ReadonlyMap<string, string>> {
    const found = new Map<string, string>();
    for (const type of types) {
      const rows = await firstValueFrom(
        this.http.get<{ id: string; type: string; projectId: string | null }[]>('api/treasury/accounts', { params: { type } }),
      );
      const company = rows.find((row) => row.projectId === null);
      if (company) found.set(type, company.id);
    }
    return found;
  }

  async fundingPostings(accountId: string): Promise<readonly PostingOption[]> {
    const page = await firstValueFrom(
      this.http.get<{ items: PostingOption[] }>('api/treasury/postings', { params: { accountId, pageSize: 50 } }),
    );
    return page.items.filter((item) => !item.isReversal && item.reversesId === null && item.inflow !== '0');
  }

  recordAccrual(body: {
    accruedExpenseAccountId: string;
    expenseAccountId: string;
    amount: string;
    accruedOn: string;
    description: string;
  }): Promise<unknown> {
    return firstValueFrom(this.http.post('api/treasury/accruals', body));
  }

  releaseAccrual(id: string, releasedOn: string): Promise<unknown> {
    return firstValueFrom(this.http.post(`api/treasury/accruals/${id}/release`, { releasedOn }));
  }

  createPrepayment(body: {
    prepaymentAccountId: string;
    expenseAccountId: string;
    fundingPostingId: string;
    totalAmount: string;
    months: number;
    firstMonth: string;
    description: string;
  }): Promise<unknown> {
    return firstValueFrom(this.http.post('api/treasury/prepayments', body));
  }

  amortise(month: string): Promise<unknown> {
    return firstValueFrom(this.http.post('api/treasury/prepayment-amortisations', { month }));
  }

  registerAsset(body: {
    name: string;
    category: AssetCategory;
    cost: string;
    ratePercent: string | null;
    purchaseDate: string;
    accumulatedDepreciationAccountId: string;
    depreciationExpenseAccountId: string;
  }): Promise<unknown> {
    return firstValueFrom(this.http.post('api/treasury/assets', body));
  }

  changeAssetRate(id: string, ratePercent: string): Promise<unknown> {
    return firstValueFrom(this.http.post(`api/treasury/assets/${id}/rate`, { ratePercent }));
  }

  runDepreciation(month: string): Promise<unknown> {
    return firstValueFrom(this.http.post('api/treasury/depreciation-runs', { month }));
  }

  approve(id: string): Promise<unknown> {
    return firstValueFrom(this.http.post(`api/treasury/accounting-requests/${id}/approval`, {}));
  }

  reject(id: string, reason: string): Promise<unknown> {
    return firstValueFrom(this.http.post(`api/treasury/accounting-requests/${id}/rejection`, { reason }));
  }

  private range(from: string, to: string): HttpParams {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return params;
  }
}
