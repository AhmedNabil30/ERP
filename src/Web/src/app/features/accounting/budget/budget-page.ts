import { ChangeDetectionStrategy, Component, DestroyRef, TemplateRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';

import { AccountingApi, Baseline } from '../../../core/accounting/accounting.api';
import { dmy, trimPercent } from '../../../core/accounting/accounting-format';
import { toProblem } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffState } from '../../../shared/kaff-state/kaff-state';

const NOT_SET = 'errors.accounting.baseline_not_set';

/**
 * موازنة المشروع (`accounting.html` A13). The baseline is the signed BOQ's cost total; the Owner names the tolerance once
 * and the server fixes the limit. There is no edit and no delete: once set, the figures are read-only for everyone.
 * The Owner (`BudgetBaselineSet`) sees the tolerance field and the one filled button while no baseline exists; Finance
 * (`BudgetBaselineRead`) only reads. The server decides both; this page only hides what the session may not use.
 *
 * Not drawn here because the server sends no field for it: the status pill, percent of the limit used, committed and paid
 * (A13's upper block) and the baseline history. Reported as a gap.
 */
@Component({
  selector: 'kaff-budget-page',
  imports: [KaffBanner, KaffButton, KaffField, KaffPageHead, KaffState],
  templateUrl: './budget-page.html',
  styleUrl: '../accounting.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BudgetPage {
  private readonly api = inject(AccountingApi);
  private readonly auth = inject(AuthService);
  private readonly headerActions = inject(HeaderActionsService);

  protected readonly i18n = inject(I18nService);

  /** Bound from `:projectId` by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();

  protected readonly baseline = signal<Baseline | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly tolerance = signal('10');
  protected readonly busy = signal(false);
  protected readonly refusal = signal<string | null>(null);

  protected readonly canSet = computed(
    () => this.auth.current()?.projects.find((p) => p.projectId === this.projectId())?.permissions.includes('BudgetBaselineSet') ?? false,
  );
  protected readonly notSet = computed(() => this.failure() === NOT_SET);
  protected readonly setDate = computed(() => dmy(this.baseline()?.setAt ?? null));

  private readonly setAction = viewChild<TemplateRef<unknown>>('setAction');

  constructor() {
    // The one filled button lives in the shell's primary slot, and only while the Owner may still set the baseline.
    effect(() => this.headerActions.set(this.notSet() && this.canSet() ? (this.setAction() ?? null) : null, 'primary'));
    inject(DestroyRef).onDestroy(() => this.headerActions.clear());
    effect(() => {
      this.projectId();
      untracked(() => void this.reload());
    });
  }

  protected readonly trim = trimPercent;

  protected val(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    this.baseline.set(null);
    try {
      this.baseline.set(await this.api.baseline(this.projectId()));
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected async save(): Promise<void> {
    this.busy.set(true);
    this.refusal.set(null);
    try {
      this.baseline.set(await this.api.setBaseline(this.projectId(), this.tolerance()));
      this.failure.set(null);
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
    } finally {
      this.busy.set(false);
    }
  }
}
