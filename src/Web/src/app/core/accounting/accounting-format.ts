import { dmyOf } from '../format/site-day';

/** `dd/MM/yyyy` of a date or an instant, read in the site day (Africa/Cairo). D-273 #15. */
export function dmy(iso: string | null): string {
  return dmyOf(iso);
}

/** A `kaff-date-field` month value `yyyy-MM` to the first day of that month. */
export function monthStart(month: string): string {
  return `${month}-01`;
}

/** The current month as `yyyy-MM`, in the viewer's own calendar. */
export function thisMonth(now: Date = new Date()): string {
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
}

export function today(now: Date = new Date()): string {
  return `${thisMonth(now)}-${String(now.getDate()).padStart(2, '0')}`;
}

/** Reads one scalar member out of a request's stored payload. A payload that does not parse names nothing. */
export function payloadText(payload: string, key: string): string | null {
  try {
    const value = (JSON.parse(payload) as Record<string, unknown>)[key];
    return typeof value === 'string' || typeof value === 'number' ? String(value) : null;
  } catch {
    return null;
  }
}

/** `yyyy-MM-dd` to `MM/yyyy` for a month a run names. */
export function monthOf(iso: string | null): string {
  if (!iso) return '';
  const [y, m] = iso.slice(0, 7).split('-');
  return `${m}/${y}`;
}

/** A decimal string that is zero at any scale ("0", "0.0000"). String test, no float. */
export function isZero(amount: string): boolean {
  return /^-?0+(\.0+)?$/.test(amount);
}

/** "10.000000" to "10", "12.500000" to "12.5": trailing zeros of a decimal string, no float. */
export function trimPercent(value: string | number): string {
  const text = String(value);
  return text.includes('.') ? text.replace(/0+$/, '').replace(/\.$/, '') : text;
}
