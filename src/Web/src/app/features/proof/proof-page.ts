import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../core/api/problem-details';
import { I18nService } from '../../core/i18n/i18n.service';
import {
  CheckStatus,
  ProofApi,
  ProofCheck,
  ProofColumns,
  ProofEvent,
  ProofPosting,
  ProofResponse,
} from '../../core/proof/proof.api';
import { isZeroAmount } from '../../shared/kaff-figure/amount';
import { KaffFigure } from '../../shared/kaff-figure/kaff-figure';
import { KaffIcon, KaffIconName } from '../../shared/kaff-icon/kaff-icon';
import { ProofLedgerStrip } from './ledger-strip/ledger-strip';
import { KaffProvenance } from '../../shared/kaff-provenance/kaff-provenance';
import { KaffState } from '../../shared/kaff-state/kaff-state';

/** The word on a match mark. Never colour alone: every mark carries one of these and an icon. */
export type Mark = 'ok' | 'no' | 'na';

interface ColumnDef {
  readonly key: keyof ProofColumns;
  readonly labelKey: string;
  readonly ruleKey: string;
  /** The assumption the column rests on, shown beside its header. */
  readonly assumption: string | null;
  readonly types: readonly string[];
}

/**
 * The five comparison columns of the design (`proof.html`), in its order. Which posting types feed which
 * column is the server's rule (`ProofOfNumbers.Handler.Columns`); `types` is only used to show the
 * postings behind a cell, never to compute a figure.
 */
const COLUMNS: readonly ColumnDef[] = [
  { key: 'work', labelKey: 'proof.col.work', ruleKey: 'proof.rule.work', assumption: null, types: ['RevenueRecognition'] },
  { key: 'hold', labelKey: 'proof.col.hold', ruleKey: 'proof.rule.hold', assumption: null, types: ['HoldAccrual', 'HoldRelease'] },
  { key: 'advance', labelKey: 'proof.col.advance', ruleKey: 'proof.rule.advance', assumption: 'A11', types: ['ClientAdvanceRecovery'] },
  {
    key: 'materialAdvance',
    labelKey: 'proof.col.material',
    ruleKey: 'proof.rule.material',
    assumption: 'A2',
    types: ['MaterialAdvanceIssue', 'MaterialAdvanceRecovery'],
  },
  {
    key: 'collection',
    labelKey: 'proof.col.collection',
    ruleKey: 'proof.rule.collection',
    assumption: null,
    types: ['ClientCollection', 'ClientAdvanceReceipt'],
  },
];

/** The assumptions whose chip appears on this screen, in register order. */
const REGISTER = ['A1', 'A2', 'A5', 'A6', 'A7', 'A8', 'A9', 'A11'] as const;

/** Assumptions that the server reports as ruled or assumed; the rest are structural and always shown. */
const RULING_FOR: Readonly<Record<string, string>> = {
  A1: 'extractPostingMoment',
  A2: 'materialAdvanceRecovery',
};

const KNOWN_DOCS = new Set(['Extract', 'Collection', 'OpeningBalance', 'Payment', 'ChangeOrder', 'Adjustment']);

const MARK: Readonly<Record<Mark, { readonly icon: KaffIconName; readonly key: string }>> = {
  ok: { icon: 'check', key: 'proof.mark.ok' },
  no: { icon: 'x', key: 'proof.mark.no' },
  na: { icon: 'pause', key: 'proof.mark.na' },
};

const CHECK_MARK: Readonly<Record<CheckStatus, { readonly mark: Mark; readonly key: string }>> = {
  Match: { mark: 'ok', key: 'proof.mark.ok' },
  Mismatch: { mark: 'no', key: 'proof.mark.no' },
  Pending: { mark: 'na', key: 'proof.mark.pending' },
  Unavailable: { mark: 'na', key: 'proof.mark.unavailable' },
};

interface CellVm {
  readonly id: string;
  readonly column: ColumnDef;
  readonly actual: string;
  readonly expected: string | null;
  readonly difference: string | null;
  readonly bad: boolean;
  readonly open: boolean;
  readonly postings: readonly ProofPosting[];
}

interface EventVm {
  readonly index: number;
  readonly label: string;
  readonly reference: string | null;
  readonly date: string;
  readonly mark: Mark;
  readonly hasExpected: boolean;
  readonly cells: readonly CellVm[];
}

interface CheckVm {
  readonly check: ProofCheck;
  readonly titleKey: string;
  readonly ruleKey: string;
  readonly mark: Mark;
  readonly markKey: string;
  readonly floor: boolean;
  readonly postings: readonly string[];
}

type Verdict = 'ok' | 'bad' | 'none';

/**
 * إثبات الأرقام — the proof screen. Read-only; Finance and the Owner.
 *
 * One job: show a project's actual postings beside the figures the contract rules expect, and say line
 * by line, with a word, whether they match. The server does every sum and every difference; this page
 * lays strings side by side and does no arithmetic (D-135).
 *
 * **The expected side is only ever the server's.** While the billing calculators return
 * `CalculatorNotImplemented`, `expectedState` says so and no event carries an expectation; the page
 * renders that as a designed state and never derives an "expected" from the postings, which would make
 * every row match by construction.
 */
@Component({
  selector: 'kaff-proof-page',
  imports: [RouterLink, KaffFigure, KaffIcon, KaffProvenance, KaffState, ProofLedgerStrip],
  templateUrl: './proof-page.html',
  styleUrl: './proof-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProofPage {
  private readonly api = inject(ProofApi);

  protected readonly i18n = inject(I18nService);
  protected readonly mark = MARK;
  protected readonly register = REGISTER;
  protected readonly columns = COLUMNS;

  /** Bound from `:projectId` by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();

  protected readonly data = signal<ProofResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly expandedCells = signal<ReadonlySet<string>>(new Set());

  private readonly postingsById = computed(
    () => new Map((this.data()?.postings ?? []).map((posting) => [posting.id, posting])),
  );

  protected readonly calculatorMissing = computed(
    () => this.data()?.expectedState === 'CalculatorNotImplemented',
  );

  protected readonly events = computed<readonly EventVm[]>(() => {
    const data = this.data();
    if (data === null) {
      return [];
    }
    const open = this.expandedCells();

    return data.events.map((event, index) => {
      const cells = COLUMNS.map((column): CellVm => {
        const id = `${index}:${column.key}`;
        const difference = event.difference?.[column.key] ?? null;
        return {
          id,
          column,
          actual: event.actual[column.key],
          expected: event.expected?.[column.key] ?? null,
          difference,
          bad: difference !== null && !isZeroAmount(difference),
          open: open.has(id),
          postings: this.postingsOf(event, column),
        };
      });
      const mark: Mark = event.expected === null ? 'na' : cells.some((cell) => cell.bad) ? 'no' : 'ok';

      return {
        index,
        label: this.documentLabel(event.sourceDocumentType),
        reference: event.reference,
        date: event.date,
        mark,
        hasExpected: event.expected !== null,
        cells,
      };
    });
  });

  protected readonly checks = computed<readonly CheckVm[]>(() =>
    (this.data()?.checks ?? []).map((check) => ({
      check,
      titleKey: `proof.check.${check.key}.title`,
      ruleKey: `proof.check.${check.key}.rule`,
      mark: CHECK_MARK[check.status].mark,
      markKey: CHECK_MARK[check.status].key,
      floor: check.key === 'safeNeverNegative',
      postings: check.postingIds.slice(-3).map((id) => this.shortId(id)),
    })),
  );

  protected readonly verdict = computed<{
    readonly tone: Verdict;
    readonly icon: KaffIconName;
    readonly titleKey: string;
    readonly bodyKey: string;
    readonly params: Readonly<Record<string, number>>;
  }>(() => {
    const data = this.data();
    const checks = data?.checks ?? [];
    const total = checks.length;
    const failed = checks.filter((check) => check.status === 'Mismatch').length;
    const matched = checks.filter((check) => check.status === 'Match').length;
    const badEvents = this.events().filter((event) => event.mark === 'no').length;
    const params = { failed, total, matched, waiting: total - matched - failed, events: badEvents };

    if (data === null || data.events.length === 0) {
      return { tone: 'none', icon: 'info', titleKey: 'proof.verdict.empty.title', bodyKey: 'proof.verdict.empty.body', params };
    }
    if (failed > 0 || badEvents > 0) {
      return { tone: 'bad', icon: 'x', titleKey: 'proof.verdict.bad.title', bodyKey: 'proof.verdict.bad.body', params };
    }
    if (this.calculatorMissing()) {
      return matched === total
        ? { tone: 'none', icon: 'info', titleKey: 'proof.verdict.nocalc.title', bodyKey: 'proof.verdict.nocalc.body', params }
        : { tone: 'none', icon: 'pause', titleKey: 'proof.verdict.partial.title', bodyKey: 'proof.verdict.partial.body', params };
    }
    return matched === total
      ? { tone: 'ok', icon: 'check', titleKey: 'proof.verdict.ok.title', bodyKey: 'proof.verdict.ok.body', params }
      : { tone: 'none', icon: 'pause', titleKey: 'proof.verdict.partial.title', bodyKey: 'proof.verdict.partial.body', params };
  });

  protected readonly readAtText = computed(() => {
    const data = this.data();
    return data === null
      ? ''
      : this.i18n.formatDate(new Date(data.readAt), { dateStyle: 'medium', timeStyle: 'short' });
  });

  constructor() {
    queueMicrotask(() => void this.reload());
  }

  protected isAssumed(id: string): boolean {
    const ruling = RULING_FOR[id];
    if (ruling === undefined) {
      return true;
    }
    return this.data()?.rulings.find((row) => row.key === ruling)?.status !== 'Ruled';
  }

  protected shortId(id: string): string {
    return id.slice(0, 8);
  }

  protected documentLabel(type: string): string {
    return this.i18n.t(KNOWN_DOCS.has(type) ? `proof.doc.${type}` : 'proof.doc.other');
  }

  protected toggleCell(id: string): void {
    this.expandedCells.update((current) => {
      const next = new Set(current);
      if (!next.delete(id)) {
        next.add(id);
      }
      return next;
    });
  }

  private postingsOf(event: ProofEvent, column: ColumnDef): readonly ProofPosting[] {
    const byId = this.postingsById();
    return event.postingIds.flatMap((id) => {
      const posting = byId.get(id);
      return posting !== undefined && column.types.includes(posting.type) ? [posting] : [];
    });
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);

    try {
      this.data.set(await this.api.get(this.projectId()));
    } catch (error) {
      // The server decides who may read this; its own message key is what the refusal shows.
      this.failure.set(toProblem(error).messageKey);
      this.data.set(null);
    } finally {
      this.loading.set(false);
    }
  }
}
