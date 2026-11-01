import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';

import { I18nService } from '../../../core/i18n/i18n.service';
import { LedgerKey, ProofPosting, ProofResponse } from '../../../core/proof/proof.api';
import { isZeroAmount, sameAmount } from '../../../shared/kaff-figure/amount';
import { KaffFigure } from '../../../shared/kaff-figure/kaff-figure';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffProvenance } from '../../../shared/kaff-provenance/kaff-provenance';

interface TileDef {
  readonly key: LedgerKey;
  /** The assumption the tile's wording rests on, shown beside its floor note. */
  readonly assumption: string | null;
}

/** The five ledgers of spec.md section 6.4, in the order the design draws them. */
const TILES: readonly TileDef[] = [
  { key: 'ClientAdvance', assumption: null },
  { key: 'Hold', assumption: null },
  { key: 'FirmAdvance', assumption: 'A9' },
  { key: 'PettyCashAdvance', assumption: null },
  { key: 'OwnerCurrentAccount', assumption: 'A7' },
];

const KNOWN_TYPES = new Set([
  'ClientAdvanceReceipt',
  'ClientCollection',
  'RevenueRecognition',
  'HoldAccrual',
  'HoldRelease',
  'MaterialAdvanceIssue',
  'MaterialAdvanceRecovery',
  'ClientAdvanceRecovery',
]);

/** What the drill-down table is showing: one ledger's postings, or the unclassified bucket's. */
type Drill = LedgerKey | 'unclassified';

/**
 * The five-ledger strip of the proof screen. **Presentation, not a guard.**
 *
 * The no-netting rule only trips when both accounts of a posting carry a ledger kind, and the worked
 * example deliberately routes hold, advance and تشوينات through an account that carries none. So the
 * strip adds what the rule cannot: a drill-down to the postings behind each tile, a reconciliation line
 * (each balance against the sum of its own postings), and an unclassified bucket that must stay empty.
 *
 * There is no total, by design: no figure here adds or offsets one ledger against another.
 */
@Component({
  selector: 'kaff-proof-ledger-strip',
  imports: [KaffFigure, KaffIcon, KaffProvenance],
  templateUrl: './ledger-strip.html',
  styleUrl: './ledger-strip.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProofLedgerStrip {
  readonly proof = input.required<ProofResponse>();

  protected readonly i18n = inject(I18nService);
  protected readonly drill = signal<Drill | null>(null);

  protected readonly tiles = computed(() => {
    const ledgers = new Map(this.proof().ledgers.map((ledger) => [ledger.key, ledger]));
    const drill = this.drill();

    return TILES.map((def) => ({
      def,
      balance: ledgers.get(def.key)?.balance ?? '0',
      count: ledgers.get(def.key)?.postingIds.length ?? 0,
      open: drill === def.key,
    }));
  });

  protected readonly material = computed(
    () => this.proof().ledgers.find((row) => row.key === 'MaterialAdvance')?.balance ?? '0',
  );

  /** Ledgers whose displayed balance does not equal the sum of their own postings. */
  protected readonly unreconciled = computed(() =>
    this.proof().ledgers.filter((ledger) => !sameAmount(ledger.balance, ledger.postingsSum)),
  );

  protected readonly unclassifiedClean = computed(() => isZeroAmount(this.proof().unclassified.amount));

  protected readonly drillRows = computed<readonly ProofPosting[]>(() => {
    const proof = this.proof();
    const drill = this.drill();
    if (drill === null) {
      return [];
    }
    const ids =
      drill === 'unclassified'
        ? proof.unclassified.postingIds
        : (proof.ledgers.find((ledger) => ledger.key === drill)?.postingIds ?? []);
    const byId = new Map(proof.postings.map((posting) => [posting.id, posting]));
    return ids.flatMap((id) => byId.get(id) ?? []);
  });

  protected shortId(id: string): string {
    return id.slice(0, 8);
  }

  protected typeLabel(type: string): string {
    return KNOWN_TYPES.has(type) ? this.i18n.t(`proof.type.${type}`) : type;
  }

  protected ledgerEffect(posting: ProofPosting, key: Drill): string | null {
    return key === 'unclassified' ? null : (posting.effects.find((row) => row.ledgerKey === key)?.effect ?? null);
  }

  protected toggleDrill(key: Drill): void {
    this.drill.update((current) => (current === key ? null : key));
  }
}
