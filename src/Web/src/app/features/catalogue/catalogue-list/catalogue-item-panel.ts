import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
} from '@angular/core';

import { CatalogueItem, CatalogueItemEdit } from '../../../core/catalogue/catalogue.api';
import { WIRE_DECIMAL_PATTERN, sameDecimal, toEditValue } from '../../../core/catalogue/money-wire';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffConfirmDialog } from '../../../shared/kaff-confirm-dialog/kaff-confirm-dialog';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffMoneyField } from '../../../shared/kaff-money-field/kaff-money-field';
import { KaffReadonlyField } from '../../../shared/kaff-readonly-field/kaff-readonly-field';

interface Draft {
  readonly descriptionAr: string;
  readonly descriptionEn: string;
  readonly unit: string;
  readonly costPrice: string;
  readonly baseSellRate: string;
}

/**
 * The catalogue item's detail panel, in its two shapes (README "The two shapes of the detail panel").
 *
 * `costPrice` and `baseSellRate` are two independent STORED figures, guarded by
 * `Permission.CataloguePriceEdit` (Owner only). The Owner edits both. Everyone else sees both as the
 * kit's read-only field under one line saying who changes them — never a disabled control. **This is
 * presentation, not the control**: `canEditPrices` only decides what is drawn, and the server refuses a
 * changed price from anyone without the permission whatever this component sends (KAFF-940, AC-940-F).
 * When the price fields are read-only the unchanged wire strings travel back untouched, so a Technical
 * Office edit of a description is the whole-record `PUT` the API takes (D-178).
 *
 * Nothing here derives one price from the other, and the باب's markup is not shown: it is `lineMarkup`
 * on a BOQ line, not a catalogue concept (D-179).
 *
 * The panel is always mounted by its page and shown when `item` is non-null, so closing returns focus
 * to the row that opened it (`kaff-detail-panel`). The draft is a `linkedSignal`: it resets to the saved
 * item whenever the item changes, including the refresh after a save.
 */
@Component({
  selector: 'kaff-catalogue-item-panel',
  imports: [
    KaffBadge,
    KaffBanner,
    KaffButton,
    KaffConfirmDialog,
    KaffDetailPanel,
    KaffField,
    KaffIcon,
    KaffMoneyField,
    KaffReadonlyField,
  ],
  templateUrl: './catalogue-item-panel.html',
  styleUrl: './catalogue-item-panel.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogueItemPanel {
  readonly item = input<CatalogueItem | null>(null);
  readonly babName = input<string | null>(null);
  /** The Owner's shape. Mirrors `Permission.CataloguePriceEdit`; the server is the control. */
  readonly canEditPrices = input(false);
  readonly saving = input(false);
  readonly archiving = input(false);
  readonly saved = input(false);
  /** The message key of a refused save or archive, rendered in place above the actions. */
  readonly refusalKey = input<string | null>(null);

  readonly saveRequested = output<CatalogueItemEdit>();
  readonly archiveConfirmed = output<void>();
  readonly unarchiveRequested = output<void>();
  readonly closed = output<void>();

  protected readonly i18n = inject(I18nService);

  protected readonly draft = linkedSignal<CatalogueItem | null, Draft>({
    source: this.item,
    computation: (item) => ({
      descriptionAr: item?.descriptionAr ?? '',
      descriptionEn: item?.descriptionEn ?? '',
      unit: item?.unit ?? '',
      costPrice: toEditValue(item?.costPrice ?? '0'),
      baseSellRate: toEditValue(item?.baseSellRate ?? '0'),
    }),
  });

  /** Both reset when another item (or the refreshed same item) arrives. */
  protected readonly archiveConfirmOpen = linkedSignal<CatalogueItem | null, boolean>({
    source: this.item,
    computation: () => false,
  });
  private readonly attempted = linkedSignal<CatalogueItem | null, boolean>({
    source: this.item,
    computation: () => false,
  });

  protected readonly costDirty = computed(() => {
    const item = this.item();
    return item !== null && this.canEditPrices() && !sameDecimal(this.draft().costPrice, item.costPrice);
  });

  protected readonly sellDirty = computed(() => {
    const item = this.item();
    return (
      item !== null && this.canEditPrices() && !sameDecimal(this.draft().baseSellRate, item.baseSellRate)
    );
  });

  protected readonly dirty = computed(() => {
    const item = this.item();
    if (item === null) {
      return false;
    }
    const d = this.draft();
    return (
      this.costDirty() ||
      this.sellDirty() ||
      d.descriptionAr !== item.descriptionAr ||
      d.descriptionEn !== (item.descriptionEn ?? '') ||
      d.unit !== item.unit
    );
  });

  /** Field errors only appear after a save was attempted, so a half-typed figure is not scolded. */
  protected readonly descriptionError = computed(() =>
    this.attempted() && this.draft().descriptionAr.trim().length === 0
      ? 'errors.master.description_required'
      : null,
  );
  protected readonly unitError = computed(() =>
    this.attempted() && this.draft().unit.trim().length === 0 ? 'errors.master.unit_required' : null,
  );
  protected readonly costError = computed(() => this.priceError(this.draft().costPrice));
  protected readonly sellError = computed(() => this.priceError(this.draft().baseSellRate));

  protected patch(change: Partial<Draft>): void {
    this.draft.update((d) => ({ ...d, ...change }));
  }

  protected onInput(field: 'descriptionAr' | 'descriptionEn' | 'unit', event: Event): void {
    this.patch({ [field]: (event.target as HTMLInputElement).value });
  }

  protected onSave(): void {
    const item = this.item();
    if (item === null) {
      return;
    }
    this.attempted.set(true);
    const d = this.draft();
    const invalid =
      d.descriptionAr.trim().length === 0 ||
      d.unit.trim().length === 0 ||
      this.costError() !== null ||
      this.sellError() !== null;
    if (invalid) {
      return;
    }

    // An unchanged price goes back exactly as the server sent it, and a read-only price is never
    // re-typed from the draft: only the Owner's own edit can put a different figure on the wire.
    this.saveRequested.emit({
      descriptionAr: d.descriptionAr.trim(),
      descriptionEn: d.descriptionEn.trim().length > 0 ? d.descriptionEn.trim() : null,
      unit: d.unit.trim(),
      costPrice: this.costDirty() ? d.costPrice.trim() : item.costPrice,
      baseSellRate: this.sellDirty() ? d.baseSellRate.trim() : item.baseSellRate,
    });
  }

  protected onArchiveConfirmed(): void {
    this.archiveConfirmed.emit();
  }

  protected openArchiveConfirm(): void {
    this.archiveConfirmOpen.set(true);
  }

  protected closeArchiveConfirm(): void {
    this.archiveConfirmOpen.set(false);
  }

  private priceError(text: string): string | null {
    if (!this.attempted() || !this.canEditPrices()) {
      return null;
    }
    return WIRE_DECIMAL_PATTERN.test(text.trim()) ? null : 'catalogue.field.price_format_invalid';
  }
}
