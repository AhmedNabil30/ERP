const CAIRO_DAY = new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Cairo', year: 'numeric', month: '2-digit', day: '2-digit' });

/**
 * The site day (`yyyy-MM-dd`) of a wire value. A bare date (10 characters) is already a day and is returned as it is. An instant
 * (`createdAt`, `decidedAt`) is read in Africa/Cairo, so a request made at 01:00 Cairo time shows its own day, not the UTC one
 * before it. No `Date` arithmetic: the instant is only formatted.
 */
export function siteDayOf(value: string): string {
  return value.length <= 10 ? value : CAIRO_DAY.format(new Date(value));
}

/** `dd/MM/yyyy` of a wire date or instant (D-273 #15), Western digits. */
export function dmyOf(value: string | null): string {
  if (!value) return '';
  const [year = '', month = '', day = ''] = siteDayOf(value).split('-');
  return `${day}/${month}/${year}`;
}
