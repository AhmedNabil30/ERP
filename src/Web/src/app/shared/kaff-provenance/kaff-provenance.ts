import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffIcon, KaffIconName } from '../kaff-icon/kaff-icon';

export type ProvenanceKind = 'calc' | 'ent' | 'car';

const LABEL: Readonly<Record<ProvenanceKind, string>> = {
  calc: 'figure.kind.calc',
  ent: 'figure.kind.ent',
  car: 'figure.kind.car',
};

const ICON: Readonly<Record<ProvenanceKind, KaffIconName>> = {
  calc: 'calc',
  ent: 'pen',
  car: 'link',
};

/**
 * Where a figure came from. Every figure on a money screen is calculated (its derivation can be shown),
 * entered (someone typed it, with a name and a time) or carried (taken from a named document). With an
 * `assumption` id it is instead the fourth mark: the figure rests on a rule nobody has ruled yet.
 *
 * Kind is carried by the word and the border style (plain, filled, dotted, dashed), never by colour.
 * An assumption chip is a button: it scrolls to its row in the assumption register on the same page.
 */
@Component({
  selector: 'kaff-provenance',
  imports: [KaffIcon],
  templateUrl: './kaff-provenance.html',
  styleUrl: './kaff-provenance.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffProvenance {
  readonly kind = input<ProvenanceKind>('calc');
  /** The named document, person or date after the kind word. */
  readonly detail = input<string | null>(null);
  /** An assumption's id ("A2"). When set, the chip is the assumption mark and `kind` is ignored. */
  readonly assumption = input<string | null>(null);

  private readonly document = inject(DOCUMENT);
  protected readonly i18n = inject(I18nService);

  protected readonly label = computed(() => this.i18n.t(LABEL[this.kind()]));
  protected readonly icon = computed(() => ICON[this.kind()]);
  protected readonly ariaLabel = computed(() =>
    this.i18n.t('proof.asm.aria', { id: this.assumption() ?? '' }),
  );

  protected goToRegister(): void {
    const row = this.document.getElementById(`asm-${this.assumption() ?? ''}`);
    row?.scrollIntoView({ block: 'center' });
    row?.classList.add('asm-flash');
    setTimeout(() => row?.classList.remove('asm-flash'), 1800);
  }
}
