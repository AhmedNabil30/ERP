/**
 * The date text rules of `kaff-date-field`, pure so they can be tested without a component. Dates travel as
 * `yyyy-MM-dd` (or `yyyy-MM` for a month) strings. There is no `Date` here: a day is valid when the calendar says so,
 * and the calendar is three integers and the leap-year rule.
 */

const ARABIC_INDIC = /[٠-٩]/g;
const EXTENDED_INDIC = /[۰-۹]/g;

/** Digits only, Western, whatever the keyboard typed (Arabic-Indic and Persian digits included). */
export function digitsOf(raw: string): string {
  return raw
    .replace(ARABIC_INDIC, (d) => String(d.charCodeAt(0) - 0x0660))
    .replace(EXTENDED_INDIC, (d) => String(d.charCodeAt(0) - 0x06f0))
    .replace(/\D/g, '');
}

/** `24112026` to `24/11/2026`, as the person types. A month takes `112026` to `11/2026`. */
export function maskDate(raw: string, month: boolean): string {
  const digits = digitsOf(raw).slice(0, month ? 6 : 8);
  const parts = month ? [digits.slice(0, 2), digits.slice(2)] : [digits.slice(0, 2), digits.slice(2, 4), digits.slice(4)];
  return parts.filter((part) => part !== '').join('/');
}

export function isLeap(year: number): boolean {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

export function daysIn(year: number, month: number): number {
  return [31, isLeap(year) ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][month - 1] ?? 0;
}

export type DateParse =
  | { readonly kind: 'empty' }
  | { readonly kind: 'partial' }
  | { readonly kind: 'invalid' }
  | { readonly kind: 'ok'; readonly iso: string };

/** Reads the typed text. `partial` is a text still being typed; `invalid` is a complete text no calendar has. */
export function parseDate(text: string, month: boolean): DateParse {
  const digits = digitsOf(text);
  if (digits === '') {
    return { kind: 'empty' };
  }
  if (digits.length < (month ? 6 : 8)) {
    return { kind: 'partial' };
  }
  const day = month ? 1 : Number(digits.slice(0, 2));
  const monthNumber = Number(month ? digits.slice(0, 2) : digits.slice(2, 4));
  const year = Number(month ? digits.slice(2, 6) : digits.slice(4, 8));
  if (year < 1900 || monthNumber < 1 || monthNumber > 12 || day < 1 || day > daysIn(year, monthNumber)) {
    return { kind: 'invalid' };
  }
  const two = (n: number): string => String(n).padStart(2, '0');
  return { kind: 'ok', iso: month ? `${year}-${two(monthNumber)}` : `${year}-${two(monthNumber)}-${two(day)}` };
}

/** `2026-11-24` to `24/11/2026`; `2026-11` to `11/2026`. Anything else is shown as nothing. */
export function displayOf(iso: string, month: boolean): string {
  const match = (month ? /^(\d{4})-(\d{2})$/ : /^(\d{4})-(\d{2})-(\d{2})$/).exec(iso);
  if (!match) {
    return '';
  }
  return month ? `${match[2]}/${match[1]}` : `${match[3]}/${match[2]}/${match[1]}`;
}
