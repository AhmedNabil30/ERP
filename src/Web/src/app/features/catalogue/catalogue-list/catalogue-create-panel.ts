import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  untracked,
} from '@angular/core';

import { Bab } from '../../../core/catalogue/babs.api';
import { flattenBabTree } from '../../../core/catalogue/bab-tree';
import { CatalogueItemCreate } from '../../../core/catalogue/catalogue.api';
import { WIRE_DECIMAL_PATTERN, toWireDecimal } from '../../../core/catalogue/money-wire';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffMoneyField } from '../../../shared/kaff-money-field/kaff-money-field';
import { KaffPickerButton } from '../../../shared/kaff-picker-button/kaff-picker-button';
import { KaffSelect, SelectOption } from '../../../shared/kaff-select/kaff-select';
import { KaffTreePicker, TreePickerNode } from '../../../shared/kaff-tree-picker/kaff-tree-picker';

/**
 * The units the create form offers, as the Technical Office writes them (`.design/v2/team-a/catalogue.html`
 * `UNITS`). The server takes any non-blank unit, so this list is the design's, not a rule of the domain.
 */
export const CATALOGUE_UNITS: readonly string[] = ['م³', 'م²', 'م.ط', 'عدد', 'طن', 'كجم', 'مقطوعية'];

/** `CatalogueItem.MaxCodeLength`. */
const CODE_MAX_LENGTH = 40;

/**
 * Latin capital letters, digits and hyphens, starting with a letter or digit (the design's `CON-1030`).
 * The server stores any non-blank code up to 40 characters; this is the design's shape, enforced here only.
 */
const CODE_PATTERN = /^[A-Z0-9][A-Z0-9-]*$/;

const CODE_TAKEN_KEY = 'errors.master.catalogue_item_code_taken';

interface Draft {
  readonly descriptionAr: string;
  readonly descriptionEn: string;
  readonly code: string;
  readonly babId: string;
  readonly unit: string;
  /** Strings, never numbers (D-135): nothing here parses or rounds a price. */
  readonly costPrice: string;
  readonly baseSellRate: string;
}

const BLANK: Draft = {
  descriptionAr: '',
  descriptionEn: '',
  code: '',
  babId: '',
  unit: '',
  costPrice: '',
  baseSellRate: '',
};

/**
 * The create panel (`catalogue.html`, "إنشاء"): a side panel on a desktop and a bottom sheet on a phone,
 * over the list. ONE form for every role that holds `CatalogueManage`.
 *
 * **The code is TYPED**, Latin capitals, unique across the whole system and immutable afterwards. It is
 * never generated. **Both prices are required, for every role** (`V-36-H`): `CreateCatalogueItem` refuses an
 * absent price (`CostPriceRequired`, `SellRateRequired`) and an explicit `0` is legal, so an empty field is
 * an error here and a typed `0` is not. There is no unpriced item and no create without prices.
 *
 * The باب is chosen in a SECOND STEP inside the panel (a back link, a radio tree, a sentence saying what
 * will happen, "Done"), never a dialog on top of it. Errors appear only after a create was attempted, so a
 * half-typed field is not scolded; the server's own refusals (a taken code, a negative price) render in
 * place and a refused code is remembered so the error follows that code and not whatever is typed next.
 */
@Component({
  selector: 'kaff-catalogue-create-panel',
  imports: [
    KaffBanner,
    KaffButton,
    KaffDetailPanel,
    KaffField,
    KaffIcon,
    KaffMoneyField,
    KaffPickerButton,
    KaffSelect,
    KaffTreePicker,
  ],
  templateUrl: './catalogue-create-panel.html',
  styleUrl: './catalogue-create-panel.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogueCreatePanel {
  readonly open = input(false);
  /** Every باب; only the active ones can be chosen. */
  readonly babs = input.required<readonly Bab[]>();
  /** Codes already in the catalogue, for an immediate collision error. The server's constraint is the authority. */
  readonly takenCodes = input<ReadonlySet<string>>(new Set());
  readonly saving = input(false);
  /** The message key of a refused create, rendered in place. */
  readonly refusalKey = input<string | null>(null);

  readonly createRequested = output<CatalogueItemCreate>();
  readonly closed = output<void>();

  protected readonly i18n = inject(I18nService);

  /** The form resets each time the panel opens. */
  protected readonly draft = linkedSignal<boolean, Draft>({
    source: this.open,
    computation: () => ({ ...BLANK }),
  });
  protected readonly attempted = linkedSignal<boolean, boolean>({
    source: this.open,
    computation: () => false,
  });
  /** `form` is the panel's normal body; `bab` is the second step. */
  protected readonly step = linkedSignal<boolean, 'form' | 'bab'>({
    source: this.open,
    computation: () => 'form',
  });
  /** The باب the second step is choosing; only becomes the draft's on "Done". */
  protected readonly pick = signal<string | null>(null);

  protected readonly codeMaxLength = CODE_MAX_LENGTH;

  protected readonly unitOptions = computed<readonly SelectOption[]>(() => [
    { value: '', label: this.i18n.t('catalogue.create.unit_placeholder') },
    ...CATALOGUE_UNITS.map((unit) => ({ value: unit, label: unit })),
  ]);

  private readonly babName = computed(() => {
    const english = this.i18n.locale() === 'en';
    return new Map(this.babs().map((b) => [b.id, english ? b.nameEn : b.nameAr]));
  });

  protected readonly pickNodes = computed<readonly TreePickerNode[]>(() =>
    flattenBabTree(this.babs().filter((b) => b.isActive)).map(({ bab, depth }) => ({
      id: bab.id,
      label: this.babName().get(bab.id) ?? bab.nameAr,
      code: bab.code,
      depth,
    })),
  );

  protected readonly babText = computed(() => {
    const id = this.draft().babId;
    return id === '' ? '' : (this.babName().get(id) ?? '');
  });

  protected readonly title = computed(() =>
    this.i18n.t(this.step() === 'bab' ? 'catalogue.create.pick_bab' : 'catalogue.create.title'),
  );

  protected readonly meta = computed(() =>
    this.babText() === ''
      ? this.i18n.t('catalogue.create.meta_none')
      : this.i18n.t('catalogue.create.meta_bab', { bab: this.babText() }),
  );

  protected readonly pickSummary = computed(() => {
    const picked = this.pick();
    return picked === null || picked === ''
      ? this.i18n.t('catalogue.create.pick_prompt')
      : this.i18n.t('catalogue.create.pick_summary', { bab: this.babName().get(picked) ?? '' });
  });

  // ---- errors ------------------------------------------------------------------------------------

  /** The code as the server stores it: trimmed and in capitals. */
  private readonly typedCode = computed(() => this.draft().code.trim().toUpperCase());

  private readonly refusedCode = linkedSignal<string | null, string | null>({
    source: this.refusalKey,
    computation: (key) => (key === CODE_TAKEN_KEY ? untracked(() => this.typedCode()) : null),
  });

  protected readonly descriptionError = computed(() =>
    this.attempted() && this.draft().descriptionAr.trim().length === 0
      ? 'catalogue.error.description_required'
      : null,
  );

  protected readonly codeError = computed<string | null>(() => {
    if (!this.attempted()) {
      return null;
    }
    const code = this.typedCode();
    if (code.length === 0) {
      return 'catalogue.error.code_required';
    }
    if (code.length > CODE_MAX_LENGTH || !CODE_PATTERN.test(code)) {
      return 'catalogue.error.code_format';
    }
    return this.takenCodes().has(code) || this.refusedCode() === code ? 'catalogue.error.code_taken' : null;
  });

  protected readonly codeErrorParams = computed(() => ({ code: this.typedCode() }));

  protected readonly babError = computed(() =>
    this.attempted() && this.draft().babId === '' ? 'catalogue.error.bab_required' : null,
  );

  protected readonly unitError = computed(() =>
    this.attempted() && this.draft().unit === '' ? 'catalogue.error.unit_required' : null,
  );

  protected readonly costError = computed(() =>
    this.priceError(this.draft().costPrice, 'errors.master.cost_price_required'),
  );

  protected readonly sellError = computed(() =>
    this.priceError(this.draft().baseSellRate, 'errors.master.sell_rate_required'),
  );

  protected readonly hasFieldErrors = computed(
    () =>
      this.descriptionError() !== null ||
      this.codeError() !== null ||
      this.babError() !== null ||
      this.unitError() !== null ||
      this.costError() !== null ||
      this.sellError() !== null,
  );

  /** A refusal that is not already drawn on a field. */
  protected readonly bannerRefusal = computed(() => {
    const key = this.refusalKey();
    return key !== null && key !== CODE_TAKEN_KEY ? key : null;
  });

  // ---- editing -----------------------------------------------------------------------------------

  protected patch(change: Partial<Draft>): void {
    this.draft.update((d) => ({ ...d, ...change }));
  }

  protected onInput(field: 'descriptionAr' | 'descriptionEn' | 'code', event: Event): void {
    this.patch({ [field]: (event.target as HTMLInputElement).value });
  }

  protected onUnit(unit: string): void {
    this.patch({ unit });
  }

  protected onCreate(): void {
    this.attempted.set(true);
    if (this.hasFieldErrors()) {
      return;
    }
    const d = this.draft();
    const english = d.descriptionEn.trim();
    this.createRequested.emit({
      code: this.typedCode(),
      descriptionAr: d.descriptionAr.trim(),
      descriptionEn: english.length > 0 ? english : null,
      unit: d.unit,
      babId: d.babId,
      costPrice: toWireDecimal(d.costPrice),
      baseSellRate: toWireDecimal(d.baseSellRate),
    });
  }

  // ---- the باب step ------------------------------------------------------------------------------

  protected onOpenBabStep(): void {
    this.pick.set(this.draft().babId === '' ? null : this.draft().babId);
    this.step.set('bab');
  }

  protected onBabStepDone(): void {
    const picked = this.pick();
    if (picked !== null && picked !== '') {
      this.patch({ babId: picked });
    }
    this.step.set('form');
  }

  protected onBack(): void {
    this.step.set('form');
  }

  /** Escape and the scrim leave the second step first, so one stray press does not drop the whole form. */
  protected onPanelClosed(): void {
    if (this.step() === 'bab') {
      this.onBack();
    } else {
      this.closed.emit();
    }
  }

  private priceError(text: string, requiredKey: string): string | null {
    if (!this.attempted()) {
      return null;
    }
    const trimmed = text.trim();
    if (trimmed.length === 0) {
      return requiredKey;
    }
    return WIRE_DECIMAL_PATTERN.test(trimmed) ? null : 'catalogue.field.price_format_invalid';
  }
}
