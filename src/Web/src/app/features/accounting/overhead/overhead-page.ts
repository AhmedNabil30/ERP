import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';

import { AccountingApi, OverheadLine, OverheadSpread } from '../../../core/accounting/accounting.api';
import { monthStart, thisMonth } from '../../../core/accounting/accounting-format';
import { toProblem } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

/**
 * توزيع المصروفات العامة (`accounting.html` A11). A report for one month: company overhead spread across projects by
 * contract value, with each project's margin before and after its share. It writes no posting, and the one sentence
 * the mock keeps says so. A project is named from the session when the viewer is assigned to it; otherwise the
 * server's code is the only identifier it sent (reported as a gap).
 */
@Component({
  selector: 'kaff-overhead-page',
  imports: [KaffBanner, KaffButton, KaffField, KaffFigure, KaffState, KaffTable, KaffTableRow],
  templateUrl: './overhead-page.html',
  styleUrl: '../accounting.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OverheadPage {
  private readonly api = inject(AccountingApi);
  private readonly auth = inject(AuthService);

  protected readonly i18n = inject(I18nService);

  protected readonly rowColumns = 'minmax(0, 1fr) 9rem 9rem 9rem 9rem';
  protected readonly headerColumns: readonly TableColumnDef[] = [
    { labelKey: 'accounting.field.project' },
    { labelKey: 'accounting.field.contract_value', align: 'end' },
    { labelKey: 'accounting.field.overhead_share', align: 'end' },
    { labelKey: 'accounting.field.gross_margin', align: 'end' },
    { labelKey: 'accounting.field.net_margin', align: 'end' },
  ];

  protected readonly month = signal(thisMonth());
  protected readonly data = signal<OverheadSpread | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  private readonly names = computed(
    () => new Map((this.auth.current()?.projects ?? []).map((project) => [project.projectId, project.name])),
  );

  constructor() {
    void this.reload();
  }

  protected projectName(line: OverheadLine): string {
    return this.names().get(line.projectId) ?? line.projectCode;
  }

  protected async setMonth(event: Event): Promise<void> {
    const value = (event.target as HTMLInputElement).value;
    if (value) {
      this.month.set(value);
      await this.reload();
    }
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      this.data.set(await this.api.overheadSpread(monthStart(this.month())));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.data.set(null);
    } finally {
      this.loading.set(false);
    }
  }
}
