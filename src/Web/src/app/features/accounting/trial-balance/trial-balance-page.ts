import { ChangeDetectionStrategy, Component, DestroyRef, TemplateRef, computed, effect, inject, signal, viewChild } from '@angular/core';

import { AccountingApi, TrialBalance, TrialBalanceLine } from '../../../core/accounting/accounting.api';
import { isZero } from '../../../core/accounting/accounting-format';
import { toProblem } from '../../../core/api/problem-details';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

/**
 * ميزان المراجعة (`accounting.html` A8). Every figure is the server's sum of postings dated inside the range; the page adds
 * nothing and compares nothing, it reads `isBalanced`. The export is the server's CSV, saved as it arrives. The five
 * ledgers are rows of their own, never netted. An account is named, never coded; account names repeat across projects
 * and the mock draws them the same way.
 */
@Component({
  selector: 'kaff-trial-balance-page',
  imports: [KaffBanner, KaffButton, KaffField, KaffFigure, KaffState, KaffTable, KaffTableRow],
  templateUrl: './trial-balance-page.html',
  styleUrl: '../accounting.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrialBalancePage {
  private readonly api = inject(AccountingApi);
  private readonly headerActions = inject(HeaderActionsService);

  protected readonly i18n = inject(I18nService);

  protected readonly rowColumns = 'minmax(0, 1fr) 10rem 10rem';
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'accounting.field.account' },
    { labelKey: 'accounting.field.debit', align: 'end' },
    { labelKey: 'accounting.field.credit', align: 'end' },
  ];

  protected readonly from = signal('');
  protected readonly to = signal('');
  protected readonly data = signal<TrialBalance | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly exporting = signal(false);
  protected readonly exportFailure = signal<string | null>(null);

  protected readonly lines = computed<readonly TrialBalanceLine[]>(() => this.data()?.lines ?? []);

  private readonly exportAction = viewChild<TemplateRef<unknown>>('exportAction');

  constructor() {
    effect(() => this.headerActions.set(this.exportAction() ?? null, 'primary'));
    inject(DestroyRef).onDestroy(() => this.headerActions.clear());
    void this.reload();
  }

  protected name(line: TrialBalanceLine): string {
    return this.i18n.locale() === 'en' ? line.nameEn : line.nameAr;
  }

  protected zero(amount: string): boolean {
    return isZero(amount);
  }

  protected val(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected async setFrom(event: Event): Promise<void> {
    this.from.set(this.val(event));
    await this.reload();
  }

  protected async setTo(event: Event): Promise<void> {
    this.to.set(this.val(event));
    await this.reload();
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      this.data.set(await this.api.trialBalance(this.from(), this.to()));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.data.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  protected async download(): Promise<void> {
    this.exporting.set(true);
    this.exportFailure.set(null);
    try {
      const blob = await this.api.trialBalanceCsv(this.from(), this.to());
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'trial-balance.csv';
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      this.exportFailure.set(toProblem(error).messageKey);
    } finally {
      this.exporting.set(false);
    }
  }
}
