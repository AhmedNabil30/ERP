import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';

import { AccountingApi, RequestKind, RequestRow, RequestStatus } from '../../../core/accounting/accounting.api';
import { dmy, monthOf, payloadText } from '../../../core/accounting/accounting-format';
import { toProblem } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffConfirmDialog } from '../../../shared/kaff-confirm-dialog/kaff-confirm-dialog';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill, PillTone } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffIconName } from '../../../shared/kaff-icon/kaff-icon';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

const KIND_KEY: Readonly<Record<RequestKind, string>> = {
  Accrual: 'accounting.request.kind.accrual',
  AccrualRelease: 'accounting.request.kind.release',
  PrepaymentAmortisation: 'accounting.request.kind.amortisation',
  Depreciation: 'accounting.request.kind.depreciation',
};

const STATUS: Readonly<Record<RequestStatus, { tone: PillTone; icon: KaffIconName; key: string }>> = {
  Requested: { tone: 'run', icon: 'clock', key: 'accounting.request.status.waiting' },
  Approved: { tone: 'done', icon: 'check', key: 'accounting.request.status.approved' },
  // A staff send-back reads «مُرجَع»; «مرفوض» stays a person's real no (D-273 #20, D-263).
  Rejected: { tone: 'stuck', icon: 'back', key: 'accounting.request.status.returned' },
};

interface Row {
  readonly request: RequestRow;
  readonly kindKey: string;
  readonly detail: string | null;
  readonly amount: string | null;
  readonly asked: string;
  readonly status: { tone: PillTone; icon: KaffIconName; key: string };
}

/**
 * طلبات المحاسبة. Finance's asks and what became of each. Nothing posts until the Owner approves (D-267): the Owner sees
 * «اعتماد» and «إرجاع» on a waiting row, Finance sees «بانتظار المالك». Return takes a reason, stored by the server and
 * shown on the row. A row's amount, description and month are read from the request's own stored payload.
 */
@Component({
  selector: 'kaff-accounting-requests-page',
  imports: [
    KaffBanner,
    KaffButton,
    KaffConfirmDialog,
    KaffDetailPanel,
    KaffField,
    KaffFigure,
    KaffState,
    KaffStatusPill,
    KaffTable,
    KaffTableRow,
  ],
  templateUrl: './requests-page.html',
  styleUrl: '../accounting.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountingRequestsPage {
  private readonly api = inject(AccountingApi);
  private readonly auth = inject(AuthService);

  protected readonly i18n = inject(I18nService);
  protected readonly canDecide = computed(() => this.auth.current()?.permissions.includes('AccountingEntryApprove') ?? false);

  protected readonly rowColumns = 'minmax(0, 1fr) 9rem 8rem 10rem 12rem';
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'accounting.field.request' },
    { labelKey: 'accounting.field.amount', align: 'end' },
    { labelKey: 'accounting.field.asked' },
    { labelKey: 'accounting.field.status' },
    {},
  ];

  protected readonly requests = signal<readonly RequestRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly approving = signal<Row | null>(null);
  protected readonly returning = signal<Row | null>(null);
  protected readonly reason = signal('');
  protected readonly attempted = signal(false);
  protected readonly busy = signal(false);
  protected readonly refusal = signal<string | null>(null);

  protected readonly reasonError = computed(() =>
    this.attempted() && this.reason().trim() === '' ? 'accounting.error.reason' : null,
  );

  protected readonly rows = computed<readonly Row[]>(() =>
    this.requests().map((request) => {
      const month = payloadText(request.payload, 'month');
      const description = payloadText(request.payload, 'description');
      return {
        request,
        kindKey: KIND_KEY[request.kind],
        detail: description ?? (month ? monthOf(month) : null),
        amount: payloadText(request.payload, 'amount'),
        asked: dmy(request.requestedAt),
        status: STATUS[request.status],
      };
    }),
  );

  constructor() {
    void this.reload();
  }

  protected val(event: Event): string {
    return (event.target as HTMLTextAreaElement).value;
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      this.requests.set(await this.api.requests());
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected openReturn(row: Row): void {
    this.reason.set('');
    this.attempted.set(false);
    this.refusal.set(null);
    this.returning.set(row);
  }

  protected async confirmApprove(): Promise<void> {
    const row = this.approving();
    if (row === null) return;
    await this.decide(() => this.api.approve(row.request.id));
    this.approving.set(null);
  }

  protected async confirmReturn(): Promise<void> {
    this.attempted.set(true);
    const row = this.returning();
    if (row === null || this.reasonError()) return;
    if (await this.decide(() => this.api.reject(row.request.id, this.reason().trim()))) {
      this.returning.set(null);
    }
  }

  private async decide(act: () => Promise<unknown>): Promise<boolean> {
    this.busy.set(true);
    this.refusal.set(null);
    try {
      await act();
      await this.reload();
      return true;
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
      return false;
    } finally {
      this.busy.set(false);
    }
  }
}
