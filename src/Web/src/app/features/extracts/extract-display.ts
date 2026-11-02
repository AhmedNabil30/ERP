import { ExtractStatus } from '../../core/extracts/extracts.api';
import { KaffIconName } from '../../shared/kaff-icon/kaff-icon';

/**
 * String-only display helpers for the extract screens (D-135: the browser does no arithmetic on money).
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

/**
 * True when rounding to `decimals` would hide a non-zero digit. The page prints every figure to the
 * document's `decimals`; when that would conceal part of a value it shows the value as sent instead of a
 * quietly rounded one.
 */
export function hasHiddenPrecision(value: string, decimals: number): boolean {
  const rest = value.trim().split('.')[1] ?? '';
  return /[1-9]/.test(rest.slice(decimals));
}

type PillTone = 'new' | 'overwrite' | 'error' | 'locked' | 'same';

/** The status pill is word, shape and colour: a draft is quiet, a stage in the chain is outlined, a closed one is green. */
export const EXTRACT_STATUS_PILL: Readonly<Record<ExtractStatus, { tone: PillTone; icon: KaffIconName }>> = {
  Draft: { tone: 'same', icon: 'file' },
  QcSubmitted: { tone: 'overwrite', icon: 'info' },
  PartiallyApproved: { tone: 'overwrite', icon: 'info' },
  TechnicalApproved: { tone: 'overwrite', icon: 'info' },
  DeductionsApplied: { tone: 'overwrite', icon: 'info' },
  OwnerApproved: { tone: 'overwrite', icon: 'check' },
  Issued: { tone: 'new', icon: 'check' },
  Collected: { tone: 'new', icon: 'check' },
};
