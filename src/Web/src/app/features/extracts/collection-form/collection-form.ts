import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';

import { CollectRequest, CollectionMethod } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { Choice, KaffChoiceGroup } from '../../../shared/kaff-choice-group/kaff-choice-group';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffMoneyField } from '../../../shared/kaff-money-field/kaff-money-field';
import { siteToday, wireAmount } from '../extract-display';

const METHODS: readonly Choice[] = [
  { value: 'Cash', labelKey: 'collection.method.Cash' },
  { value: 'Cheque', labelKey: 'collection.method.Cheque' },
  { value: 'Transfer', labelKey: 'collection.method.Transfer' },
];

/** The reference's label follows the method (README "Server truth applied" 9). */
const REFERENCE_KEYS: Readonly<Record<CollectionMethod, string>> = {
  Cash: 'collection.reference.Cash',
  Cheque: 'collection.reference.Cheque',
  Transfer: 'collection.reference.Transfer',
};

const REFERENCE_REQUIRED_KEYS: Readonly<Record<CollectionMethod, string>> = {
  Cash: 'collection.reference_required.Cash',
  Cheque: 'collection.reference_required.Cheque',
  Transfer: 'collection.reference_required.Transfer',
};

interface Errors {
  readonly method: string | null;
  readonly date: string | null;
  readonly reference: string | null;
  readonly cash: string | null;
  readonly withheld: string | null;
}

const NONE: Errors = { method: null, date: null, reference: null, cash: null, withheld: null };

/**
 * One payment, as it arrived (D-238 item 3): method, date (today or earlier, D-240 item 3), reference, the amount
 * received and what the client withheld at source. **The same part records a payment of an extract (E5) and of a
 * released hold (D-244 item 2)**; the host places the primary and sends what `request()` returns.
 *
 * It holds no button of its own: the primary stays live, and a missing field is named under the field on submit.
 * A refusal the server words about a field (`exceeds_expected`, a future date) is shown under that field, not as a
 * banner; any other refusal is the host's.
 */
@Component({
  selector: 'kaff-collection-form',
  imports: [KaffChoiceGroup, KaffField, KaffMoneyField],
  templateUrl: './collection-form.html',
  styleUrl: '../chain-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CollectionForm {
  protected readonly i18n = inject(I18nService);
  protected readonly methods = METHODS;

  /** False when the contract withholds nothing: the withheld field is absent (E5 `nowh`). */
  readonly withholding = input(true);
  /** What may still be recorded, for the «أكبر من المتبقي (…)» line. */
  readonly remaining = input.required<string>();
  /** The server's refusal of the last submit, when it is about a field. */
  readonly refusalKey = input<string | null>(null);
  readonly testIdPrefix = input('collect');

  readonly method = signal('');
  protected readonly date = signal(siteToday());
  protected readonly reference = signal('');
  protected readonly cash = signal('');
  protected readonly withheld = signal('');
  protected readonly errors = signal<Errors>(NONE);
  protected readonly today = siteToday();

  protected readonly referenceKey = computed(() => REFERENCE_KEYS[(this.method() || 'Transfer') as CollectionMethod]);

  /** The server's field refusals, placed under the field they are about. */
  protected readonly cashRefusal = computed(() =>
    this.refusalKey() === 'errors.collection.exceeds_expected' ? 'collection.exceeds' : null,
  );
  protected readonly remainingText = computed(() => this.i18n.formatAmount(this.remaining()));

  constructor() {
    // A new method asks a new reference; the old error no longer applies.
    effect(() => {
      this.method();
      this.errors.update((e) => ({ ...e, method: null, reference: null }));
    });
  }

  /** The request, or null with every missing or wrong field named. */
  request(): CollectRequest | null {
    const method = this.method() as CollectionMethod | '';
    const reference = this.reference().trim();
    const cash = wireAmount(this.cash());
    const withheldText = this.withheld().trim();
    const withheld = withheldText === '' ? '0' : wireAmount(withheldText);
    const date = this.date();
    const errors: Errors = {
      method: method === '' ? 'collection.method_required' : null,
      date: date === '' ? 'collection.date_required' : date > this.today ? 'collection.date_future' : null,
      reference: reference === '' ? REFERENCE_REQUIRED_KEYS[(method || 'Transfer') as CollectionMethod] : null,
      cash: cash === null ? 'collection.cash_required' : null,
      withheld: withheld === null ? 'collection.withheld_invalid' : null,
    };
    this.errors.set(errors);
    if (method === '' || Object.values(errors).some((e) => e !== null) || cash === null || withheld === null) {
      return null;
    }
    return { method, date, reference, cashReceived: cash, withheldAtSource: this.withholding() ? withheld : '0' };
  }

  protected onDate(event: Event): void {
    this.date.set((event.target as HTMLInputElement).value);
    this.errors.update((e) => ({ ...e, date: null }));
  }

  protected onReference(event: Event): void {
    this.reference.set((event.target as HTMLInputElement).value);
    this.errors.update((e) => ({ ...e, reference: null }));
  }
}
