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

import { ASSET_CATEGORIES, AccountingApi, AssetCategory, AssetRow } from '../../../core/accounting/accounting.api';
import { monthStart, thisMonth, today } from '../../../core/accounting/accounting-format';
import { toProblem } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffMoneyField } from '../../../shared/kaff-money-field/kaff-money-field';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';
import { KaffWaitingLine } from '../../../shared/kaff-waiting-line/kaff-waiting-line';

type Sheet = 'add' | 'rate' | 'run' | null;

const CATEGORY_KEY: Readonly<Record<AssetCategory, string>> = {
  ComputersSoftwareData: 'accounting.category.computers',
  OtherBusinessAssets: 'accounting.category.other',
  Buildings: 'accounting.category.buildings',
  PurchasedIntangibles: 'accounting.category.intangibles',
};

/**
 * الأصول (`accounting.html` A2, A3). The asset register: what each asset cost, what has been written off and what is left.
 * Accumulated depreciation and book value are the server's, derived from postings. Finance registers an asset, changes
 * its rate (refused by the server once depreciation is posted) and asks for a month's depreciation, which the Owner
 * approves. The Owner reads; the controls are absent for any session without `TreasuryPostCompany`.
 */
@Component({
  selector: 'kaff-assets-page',
  imports: [
    KaffBanner,
    KaffButton,
    KaffDetailPanel,
    KaffField,
    KaffFigure,
    KaffIcon,
    KaffMoneyField,
    KaffState,
    KaffStatusPill,
    KaffTable,
    KaffTableRow,
    KaffWaitingLine,
  ],
  templateUrl: './assets-page.html',
  styleUrl: '../accounting.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssetsPage {
  private readonly api = inject(AccountingApi);
  private readonly auth = inject(AuthService);
  private readonly headerActions = inject(HeaderActionsService);

  protected readonly i18n = inject(I18nService);
  protected readonly categories = ASSET_CATEGORIES;
  protected readonly canPost = computed(() => this.auth.current()?.permissions.includes('TreasuryPostCompany') ?? false);

  protected readonly rowColumns = 'minmax(0, 1fr) 8rem 8rem 8rem 8rem 8rem';
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'accounting.field.asset' },
    { labelKey: 'accounting.field.cost', align: 'end' },
    { labelKey: 'accounting.field.accumulated', align: 'end' },
    { labelKey: 'accounting.field.book_value', align: 'end' },
    { labelKey: 'accounting.field.monthly', align: 'end' },
    {},
  ];

  protected readonly assets = signal<readonly AssetRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  protected readonly sheet = signal<Sheet>(null);
  protected readonly name = signal('');
  protected readonly category = signal<AssetCategory>('OtherBusinessAssets');
  protected readonly cost = signal('');
  protected readonly rate = signal('');
  protected readonly date = signal(today());
  protected readonly month = signal(thisMonth());
  protected readonly editing = signal<AssetRow | null>(null);
  protected readonly accountsMissing = signal(false);
  protected readonly attempted = signal(false);
  protected readonly busy = signal(false);
  protected readonly refusal = signal<string | null>(null);
  protected readonly waiting = signal(false);

  private accounts = new Map<string, string>();

  protected readonly nameError = computed(() =>
    this.attempted() && this.name().trim() === '' ? 'accounting.error.name' : null,
  );
  protected readonly costError = computed(() =>
    this.attempted() && !(Number(this.cost()) > 0) ? 'accounting.error.amount' : null,
  );
  protected readonly rateError = computed(() => {
    const value = Number(this.rate());
    return this.attempted() && this.sheet() === 'rate' && !(value > 0 && value <= 100) ? 'accounting.error.rate' : null;
  });

  private readonly addAction = viewChild<TemplateRef<unknown>>('addAction');

  constructor() {
    effect(() => this.headerActions.set(this.canPost() ? (this.addAction() ?? null) : null, 'primary'));
    inject(DestroyRef).onDestroy(() => this.headerActions.clear());
    void this.reload();
  }

  protected categoryKey(category: AssetCategory): string {
    return CATEGORY_KEY[category];
  }

  /** The server sends a decimal string; a book value with nothing left reads as zero in any scale. */
  protected fullyWritten(asset: AssetRow): boolean {
    return /^0+(\.0+)?$/.test(asset.bookValue);
  }

  protected val(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected pickCategory(event: Event): void {
    this.category.set((event.target as HTMLSelectElement).value as AssetCategory);
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      this.assets.set(await this.api.assets());
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected async openAdd(): Promise<void> {
    this.begin('add');
    this.name.set('');
    this.cost.set('');
    this.rate.set('');
    this.category.set('OtherBusinessAssets');
    this.date.set(today());
    this.accountsMissing.set(false);
    try {
      this.accounts = new Map(await this.api.accountsOfType(['AccumulatedDepreciation', 'DepreciationExpense']));
      this.accountsMissing.set(!(this.accounts.has('AccumulatedDepreciation') && this.accounts.has('DepreciationExpense')));
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
      this.accountsMissing.set(true);
    }
  }

  protected openRate(asset: AssetRow): void {
    this.begin('rate');
    this.editing.set(asset);
    this.rate.set(String(asset.ratePercent));
  }

  protected openRun(): void {
    this.begin('run');
  }

  protected closeSheet(): void {
    this.sheet.set(null);
  }

  protected async saveAdd(): Promise<void> {
    this.attempted.set(true);
    if (this.nameError() || this.costError() || this.accountsMissing()) return;
    await this.run(
      () =>
        this.api.registerAsset({
          name: this.name().trim(),
          category: this.category(),
          cost: this.cost().trim(),
          ratePercent: this.rate().trim() === '' ? null : this.rate().trim(),
          purchaseDate: this.date(),
          accumulatedDepreciationAccountId: this.accounts.get('AccumulatedDepreciation') ?? '',
          depreciationExpenseAccountId: this.accounts.get('DepreciationExpense') ?? '',
        }),
      false,
    );
  }

  protected async saveRate(): Promise<void> {
    this.attempted.set(true);
    const asset = this.editing();
    if (asset === null || this.rateError()) return;
    await this.run(() => this.api.changeAssetRate(asset.id, this.rate().trim()), false);
  }

  protected async saveRun(): Promise<void> {
    await this.run(() => this.api.runDepreciation(monthStart(this.month())), true);
  }

  private begin(sheet: Exclude<Sheet, null>): void {
    this.attempted.set(false);
    this.refusal.set(null);
    this.waiting.set(false);
    this.sheet.set(sheet);
  }

  private async run(act: () => Promise<unknown>, asksOwner: boolean): Promise<void> {
    this.busy.set(true);
    this.refusal.set(null);
    try {
      await act();
      this.waiting.set(asksOwner);
      this.sheet.set(null);
      await this.reload();
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
    } finally {
      this.busy.set(false);
    }
  }
}
