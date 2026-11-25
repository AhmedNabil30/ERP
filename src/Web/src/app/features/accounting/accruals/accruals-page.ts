import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  TemplateRef,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';

import { AccountingApi, AccrualRow, PostingOption, PrepaymentRow } from '../../../core/accounting/accounting.api';
import { dmy, monthStart, thisMonth, today } from '../../../core/accounting/accounting-format';
import { toProblem } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffConfirmDialog } from '../../../shared/kaff-confirm-dialog/kaff-confirm-dialog';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffMoneyField } from '../../../shared/kaff-money-field/kaff-money-field';
import { KaffSegmentedFilter, SegmentedFilterOption } from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';
import { KaffWaitingLine } from '../../../shared/kaff-waiting-line/kaff-waiting-line';
import { KaffDateField } from '../../../shared/kaff-date-field/kaff-date-field';

type Kind = 'accrual' | 'prepayment';
type Sheet = 'add' | 'amortise' | null;

interface Row {
  readonly key: string;
  readonly kind: Kind;
  readonly id: string;
  readonly title: string;
  readonly amount: string;
  /** What is left: the prepayment's remaining amount; null for an accrual. */
  readonly remaining: string | null;
  readonly stateText: string | null;
  readonly canRelease: boolean;
}

/**
 * المستحقات والمقدمات (`accounting.html` A4). One list of what is owed and not yet invoiced, and of what was paid ahead
 * and is moving to expense month by month. Finance adds, releases and runs the month; each of those is a request the
 * Owner decides, so the screen says «بانتظار المالك» after it and nothing is shown as posted (D-267). The Owner reads.
 *
 * Accounts come from the trial balance by type (`AccountingApi.accountsOfType`): no endpoint lists accounts. When one is
 * missing the sheet says so and offers no save, rather than guessing an id.
 */
@Component({
  selector: 'kaff-accruals-page',
  imports: [
    KaffDateField,
    KaffBanner,
    KaffButton,
    KaffConfirmDialog,
    KaffDetailPanel,
    KaffField,
    KaffFigure,
    KaffIcon,
    KaffMoneyField,
    KaffSegmentedFilter,
    KaffState,
    KaffStatusPill,
    KaffTable,
    KaffTableRow,
    KaffWaitingLine,
  ],
  templateUrl: './accruals-page.html',
  styleUrl: '../accounting.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccrualsPage {
  private readonly api = inject(AccountingApi);
  private readonly headerActions = inject(HeaderActionsService);

  protected readonly i18n = inject(I18nService);
  private readonly auth = inject(AuthService);
  protected readonly canPost = computed(() => this.auth.current()?.permissions.includes('TreasuryPostCompany') ?? false);

  protected readonly rowColumns = 'minmax(0, 1fr) 9rem 9rem 7rem';
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'accounting.field.description' },
    { labelKey: 'accounting.field.amount', align: 'end' },
    { labelKey: 'accounting.field.remaining', align: 'end' },
    {},
  ];
  protected readonly kindOptions: readonly SegmentedFilterOption<Kind>[] = [
    { value: 'accrual', labelKey: 'accounting.kind.accrual' },
    { value: 'prepayment', labelKey: 'accounting.kind.prepayment' },
  ];

  protected readonly accruals = signal<readonly AccrualRow[]>([]);
  protected readonly prepayments = signal<readonly PrepaymentRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  protected readonly sheet = signal<Sheet>(null);
  protected readonly kind = signal<Kind>('accrual');
  protected readonly description = signal('');
  protected readonly amount = signal('');
  protected readonly date = signal(today());
  protected readonly months = signal('12');
  protected readonly month = signal(thisMonth());
  protected readonly funding = signal('');
  protected readonly fundings = signal<readonly PostingOption[]>([]);
  protected readonly accountsMissing = signal(false);
  protected readonly attempted = signal(false);
  protected readonly busy = signal(false);
  protected readonly refusal = signal<string | null>(null);
  protected readonly waiting = signal(false);
  protected readonly releasing = signal<Row | null>(null);

  private accounts = new Map<string, string>();

  protected readonly rows = computed<readonly Row[]>(() => [
    ...this.accruals().map(
      (accrual): Row => ({
        key: `a-${accrual.id}`,
        kind: 'accrual',
        id: accrual.id,
        title: accrual.description ?? this.i18n.t('accounting.kind.accrual'),
        amount: accrual.amount,
        remaining: null,
        stateText: accrual.isOpen
          ? this.i18n.t('accounting.accrual.open')
          : this.i18n.t('accounting.accrual.released', { date: dmy(accrual.releasedOn) }),
        canRelease: accrual.isOpen,
      }),
    ),
    ...this.prepayments().map(
      (prepayment): Row => ({
        key: `p-${prepayment.id}`,
        kind: 'prepayment',
        id: prepayment.id,
        title: prepayment.description ?? this.i18n.t('accounting.kind.prepayment'),
        amount: prepayment.totalAmount,
        remaining: prepayment.remaining,
        stateText: null,
        canRelease: false,
      }),
    ),
  ]);

  protected readonly amountError = computed(() =>
    this.attempted() && this.kind() === 'accrual' && !(Number(this.amount()) > 0) ? 'accounting.error.amount' : null,
  );
  protected readonly fundingError = computed(() =>
    this.attempted() && this.kind() === 'prepayment' && this.funding() === '' ? 'accounting.error.funding' : null,
  );
  protected readonly descriptionError = computed(() =>
    this.attempted() && this.description().trim() === '' ? 'accounting.error.description' : null,
  );

  private readonly addAction = viewChild<TemplateRef<unknown>>('addAction');

  constructor() {
    effect(() => this.headerActions.set(this.canPost() ? (this.addAction() ?? null) : null, 'primary'));
    inject(DestroyRef).onDestroy(() => this.headerActions.clear());
    void this.reload();
  }

  protected val(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      const [accruals, prepayments] = await Promise.all([this.api.accruals(), this.api.prepayments()]);
      this.accruals.set(accruals);
      this.prepayments.set(prepayments);
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected async openAdd(): Promise<void> {
    this.reset();
    this.sheet.set('add');
    await this.loadAccounts();
  }

  protected openAmortise(): void {
    this.reset();
    this.sheet.set('amortise');
  }

  protected closeSheet(): void {
    this.sheet.set(null);
  }

  protected async pickKind(kind: Kind): Promise<void> {
    this.kind.set(kind);
    this.attempted.set(false);
    await this.loadAccounts();
  }

  protected pickFunding(id: string): void {
    this.funding.set(id);
    const posting = this.fundings().find((item) => item.id === id);
    if (posting) {
      this.amount.set(posting.inflow);
    }
  }

  protected fundingLabel(posting: PostingOption): string {
    return `${dmy(posting.postingDate)} · ${this.i18n.formatMoney(posting.inflow)}`;
  }

  protected async save(): Promise<void> {
    this.attempted.set(true);
    if (this.amountError() || this.fundingError() || this.descriptionError() || this.accountsMissing()) {
      return;
    }
    const expense = this.accounts.get('CompanyExpense') ?? '';
    await this.run(async () => {
      if (this.kind() === 'accrual') {
        await this.api.recordAccrual({
          accruedExpenseAccountId: this.accounts.get('AccruedExpense') ?? '',
          expenseAccountId: expense,
          amount: this.amount().trim(),
          accruedOn: this.date(),
          description: this.description().trim(),
        });
      } else {
        await this.api.createPrepayment({
          prepaymentAccountId: this.accounts.get('Prepayment') ?? '',
          expenseAccountId: expense,
          fundingPostingId: this.funding(),
          totalAmount: this.amount().trim(),
          months: Number(this.months()),
          firstMonth: this.date(),
          description: this.description().trim(),
        });
      }
    });
  }

  protected async saveAmortise(): Promise<void> {
    await this.run(() => this.api.amortise(monthStart(this.month())));
  }

  protected async confirmRelease(): Promise<void> {
    const row = this.releasing();
    if (row === null) return;
    await this.run(() => this.api.releaseAccrual(row.id, today()));
    this.releasing.set(null);
  }

  private async run(act: () => Promise<unknown>): Promise<void> {
    this.busy.set(true);
    this.refusal.set(null);
    try {
      await act();
      // A prepayment schedule posts nothing and needs no approval; every other act is a request the Owner decides.
      this.waiting.set(!(this.sheet() === 'add' && this.kind() === 'prepayment'));
      this.sheet.set(null);
      await this.reload();
    } catch (error) {
      const key = toProblem(error).messageKey;
      this.refusal.set(key);
      this.releasing.set(null);
    } finally {
      this.busy.set(false);
    }
  }

  private reset(): void {
    this.attempted.set(false);
    this.refusal.set(null);
    this.waiting.set(false);
    this.description.set('');
    this.amount.set('');
    this.date.set(today());
    this.months.set('12');
    this.funding.set('');
    this.kind.set('accrual');
  }

  private async loadAccounts(): Promise<void> {
    this.accountsMissing.set(false);
    const wanted = this.kind() === 'accrual' ? ['AccruedExpense', 'CompanyExpense'] : ['Prepayment', 'CompanyExpense'];
    try {
      this.accounts = new Map(await this.api.accountsOfType(wanted));
      this.accountsMissing.set(wanted.some((type) => !this.accounts.has(type)));
      if (this.kind() === 'prepayment' && this.accounts.has('Prepayment')) {
        this.fundings.set(await this.api.fundingPostings(this.accounts.get('Prepayment') ?? ''));
      }
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
      this.accountsMissing.set(true);
    }
  }
}
