import type { ChequeState, ExtractStatus, WaitingOnBlock } from '../../core/extracts/extracts.api';
import type { AdjustmentStatus, ChangeOrderStatus, LineStaff, WaitingLine } from '../../core/extracts/chain.api';
import type { I18nService } from '../../core/i18n/i18n.service';
import type { KaffIconName } from '../../shared/kaff-icon/kaff-icon';
import type { PillTone } from '../../shared/kaff-status-pill/kaff-status-pill';

/**
 * String-only display helpers for the extract chain (D-135: the browser does no arithmetic on money).
 * Every "may I" stays the server's: nothing here derives a permission or a state.
 */

/**
 * A rate arrives as a fraction string ("0.200000"). The percentage is the same digits with the point moved
 * two places, so no number is ever built. "1" and "1.000000" are the same value and print the same.
 */
export function percentOfFraction(fraction: string): string {
  const [whole = '0', rest = ''] = fraction.trim().split('.');
  const padded = rest.padEnd(2, '0');
  const integer = `${whole}${padded.slice(0, 2)}`.replace(/^0+(?=\d)/, '');
  const tail = padded.slice(2).replace(/0+$/, '');
  return tail ? `${integer}.${tail}` : integer;
}

/** True when rounding to `decimals` would hide a non-zero digit. */
export function hasHiddenPrecision(value: string, decimals: number): boolean {
  const rest = value.trim().split('.')[1] ?? '';
  return /[1-9]/.test(rest.slice(decimals));
}

/** The last element matching `match` (the build's library has no `findLast`). */
export function lastOf<T>(items: readonly T[] | undefined, match: (item: T) => boolean): T | undefined {
  return items === undefined ? undefined : [...items].reverse().find(match);
}

/** True for a wire amount that is above zero ("0", "0.0000" and "" are not). */
export function isPositive(value: string | null | undefined): boolean {
  return value !== null && value !== undefined && !value.trim().startsWith('-') && /[1-9]/.test(value);
}

/** The wire string for a deduction: the same digits with a minus, so `kaff-figure` prints a real minus. */
export function negated(value: string): string {
  return isPositive(value) ? `-${value.trim()}` : value;
}

const ARABIC_DIGITS = /[٠-٩]/g;

/**
 * A typed amount as the wire string the server reads (D-135), or null when it is not one: Arabic-Indic digits
 * become Western, thousands separators and spaces go, and what is left must be digits with an optional point.
 */
export function wireAmount(typed: string): string | null {
  const text = typed
    .trim()
    .replace(ARABIC_DIGITS, (d) => String(d.charCodeAt(0) - 0x0660))
    .replace(/[٬,\s]/g, '')
    .replace('٫', '.');
  return /^\d+(\.\d+)?$/.test(text) ? text : null;
}

/** Today in the site's own zone (D-234: "today" on every document is the site day), as `yyyy-MM-dd`. */
export function siteToday(now: Date = new Date()): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Cairo', year: 'numeric', month: '2-digit', day: '2-digit' }).format(now);
}

/** A `yyyy-MM-dd` wire date as a local date, so the day does not shift with the time zone. */
export function parseDay(value: string): Date {
  const [year = 0, month = 1, day = 1] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}

/** DD/MM/YYYY, the mock's one date form, Western digits. */
export function shortDay(value: string): string {
  const [year = '', month = '', day = ''] = value.slice(0, 10).split('-');
  return `${day}/${month}/${year}`;
}

interface Pill {
  readonly tone: PillTone;
  readonly icon: KaffIconName;
  readonly key: string;
}

/** The extract status pill set (README "Extract chain", kit item 1). Literal keys, so the catalogue scan sees each. */
export const EXTRACT_PILL: Readonly<Record<ExtractStatus, Pill>> = {
  Draft: { tone: 'none', icon: 'ring', key: 'extract.status.Draft' },
  QcSubmitted: { tone: 'run', icon: 'dot', key: 'extract.status.QcSubmitted' },
  PartiallyApproved: { tone: 'run', icon: 'dot', key: 'extract.status.PartiallyApproved' },
  TechnicalApproved: { tone: 'run', icon: 'dot', key: 'extract.status.TechnicalApproved' },
  DeductionsApplied: { tone: 'run', icon: 'dot', key: 'extract.status.DeductionsApplied' },
  OwnerApproved: { tone: 'run', icon: 'dot', key: 'extract.status.OwnerApproved' },
  Issued: { tone: 'out', icon: 'arrow', key: 'extract.status.Issued' },
  Collected: { tone: 'done', icon: 'check', key: 'extract.status.Collected' },
  Disputed: { tone: 'stuck', icon: 'alert', key: 'extract.status.Disputed' },
  Void: { tone: 'post', icon: 'x', key: 'extract.status.Void' },
};

export const CHEQUE_PILL: Readonly<Record<ChequeState, Pill>> = {
  Received: { tone: 'none', icon: 'ring', key: 'cheque.state.Received' },
  Deposited: { tone: 'run', icon: 'dot', key: 'cheque.state.Deposited' },
  Cleared: { tone: 'done', icon: 'check', key: 'cheque.state.Cleared' },
  Bounced: { tone: 'stuck', icon: 'alert', key: 'cheque.state.Bounced' },
};

export const CHANGE_ORDER_PILL: Readonly<Record<ChangeOrderStatus, Pill>> = {
  Draft: { tone: 'none', icon: 'ring', key: 'change_order.status.Draft' },
  TechnicalReview: { tone: 'run', icon: 'dot', key: 'change_order.status.TechnicalReview' },
  ClientApproval: { tone: 'run', icon: 'dot', key: 'change_order.status.ClientApproval' },
  OwnerApproval: { tone: 'run', icon: 'dot', key: 'change_order.status.OwnerApproval' },
  Approved: { tone: 'done', icon: 'check', key: 'change_order.status.Approved' },
  Rejected: { tone: 'stuck', icon: 'alert', key: 'change_order.status.Rejected' },
  Withdrawn: { tone: 'post', icon: 'x', key: 'change_order.status.Withdrawn' },
  Logged: { tone: 'done', icon: 'check', key: 'change_order.status.Logged' },
};

export const CREDIT_NOTE_PILL: Readonly<Record<AdjustmentStatus, Pill>> = {
  Raised: { tone: 'run', icon: 'dot', key: 'credit_note.status.Raised' },
  Approved: { tone: 'done', icon: 'check', key: 'credit_note.status.Approved' },
  Rejected: { tone: 'stuck', icon: 'alert', key: 'credit_note.status.Rejected' },
  Reversed: { tone: 'post', icon: 'x', key: 'credit_note.status.Reversed' },
};

/** The seven steps of the chain line (README "Extract chain", kit item 2). */
export const CHAIN_STEP_KEYS: readonly string[] = [
  'extract.chain.prepare',
  'extract.chain.qc',
  'extract.chain.technical',
  'extract.chain.accounts',
  'extract.chain.owner',
  'extract.chain.issue',
  'extract.chain.collect',
];

export const CHANGE_ORDER_STEP_KEYS: readonly string[] = [
  'change_order.chain.draft',
  'change_order.chain.review',
  'change_order.chain.client',
  'change_order.chain.owner',
];

/**
 * Where the extract stands on the chain line, by its status. `awaitsRevision`: a quantity rejection sent it back to
 * the site engineer, so preparation is current again. Null: a cancelled extract has no chain line.
 */
export function chainIndex(status: ExtractStatus, awaitsRevision: boolean): number | null {
  switch (status) {
    case 'Draft':
      return awaitsRevision ? 0 : 1;
    case 'QcSubmitted':
      return 2;
    case 'PartiallyApproved':
    case 'TechnicalApproved':
      return 3;
    case 'DeductionsApplied':
      return 4;
    case 'OwnerApproved':
      return 5;
    case 'Issued':
    case 'Disputed':
      return 6;
    case 'Collected':
      return 7;
    case 'Void':
      return null;
  }
}

export function changeOrderIndex(status: ChangeOrderStatus): number {
  switch (status) {
    case 'Draft':
      return 0;
    case 'TechnicalReview':
      return 1;
    case 'ClientApproval':
      return 2;
    case 'OwnerApproval':
    case 'Rejected':
      return 3;
    default:
      return 4;
  }
}

/**
 * The task an extract waits for, by the permission the server says the next step needs (and the status, where one
 * permission takes two steps). `chequeInHand`: an issued extract whose next move is the cheque's.
 */
export function extractTaskKey(waiting: WaitingOnBlock, chequeInHand: boolean): string {
  switch (waiting.permission) {
    case 'ExtractPrepare':
      return 'extract.task.revise';
    case 'ExtractQcReportSubmit':
      return 'extract.task.qc';
    case 'QuantityGateApprove':
      return 'extract.task.technical';
    case 'ExtractDeductionsApply':
      return 'extract.task.deductions';
    case 'FinancialMovementApprove':
      return waiting.status === 'OwnerApproved' ? 'extract.task.issue' : 'extract.task.owner';
    case 'ExtractCollect':
      return chequeInHand ? 'extract.task.deposit' : 'extract.task.collect';
    case 'ExtractDisputeResolve':
      return 'extract.task.resolve';
    default:
      return 'projects.workspace.waiting.step.other';
  }
}

/** Names joined as the language joins alternatives ("أ أو ب"). */
export function names(i18n: I18nService, people: readonly { readonly fullName: string }[]): string {
  return new Intl.ListFormat(i18n.locale(), { style: 'long', type: 'disjunction' }).format(people.map((p) => p.fullName));
}

/** The one form of the waiting line: «بانتظار {person}: {task}». The client is the holder with no person. */
export function waitingText(i18n: I18nService, holders: readonly { readonly fullName: string }[], taskKey: string): string {
  return holders.length === 0
    ? i18n.t('waiting.line.nobody', { task: i18n.t(taskKey) })
    : i18n.t('waiting.line', { who: names(i18n, holders), task: i18n.t(taskKey) });
}

/** The dim second line: who holds the role but may not act. Null when nobody is barred. */
export function barredText(i18n: I18nService, barred: readonly LineStaff[] | readonly { readonly fullName: string }[]): string | null {
  return barred.length === 0 ? null : i18n.t('waiting.barred', { who: names(i18n, barred) });
}

/** The waiting line of a change order, a cheque or a credit note, as the server words its task. */
export function lineText(i18n: I18nService, line: WaitingLine | null): string | null {
  return line === null ? null : waitingText(i18n, line.holders, line.taskKey);
}

// Moved from workspace-reading.ts: the project workspace names the same steps.
/** The step an extract waits for, by the status it is in now. Literal keys, so the catalogue scan sees each. */
export const WAITING_STEP_KEY: Readonly<Record<string, string>> = {
  Draft: 'projects.workspace.waiting.step.Draft',
  QcSubmitted: 'projects.workspace.waiting.step.QcSubmitted',
  PartiallyApproved: 'projects.workspace.waiting.step.TechnicalApproved',
  TechnicalApproved: 'projects.workspace.waiting.step.TechnicalApproved',
  DeductionsApplied: 'projects.workspace.waiting.step.DeductionsApplied',
  OwnerApproved: 'projects.workspace.waiting.step.OwnerApproved',
  Issued: 'projects.workspace.waiting.step.Issued',
  Disputed: 'projects.workspace.waiting.step.Disputed',
};
export const WAITING_STEP_FALLBACK_KEY = 'projects.workspace.waiting.step.other';

/** What this session holds on this project, as the sign-in sent it. Convenience only: every route checks again. */
export function projectPermissions(
  session: { readonly projects: readonly { readonly projectId: string; readonly permissions: readonly string[] }[] } | null,
  projectId: string,
): ReadonlySet<string> {
  return new Set(session?.projects.find((p) => p.projectId === projectId)?.permissions ?? []);
}
