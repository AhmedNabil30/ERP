import { I18nService } from '../../core/i18n/i18n.service';

export type DayValue = '0' | '1' | '2' | '3';

/** The open window (D-217 item 7): today and three days back. The server judges it again. */
export const DAY_VALUES: readonly DayValue[] = ['0', '1', '2', '3'];

/** The date `index` days before `today`. */
export function dayDate(today: Date, index: number): Date {
  const date = new Date(today);
  date.setDate(date.getDate() - index);
  return date;
}

/** Today, yesterday, then the weekday name. */
export function dayWord(i18n: I18nService, today: Date, index: number): string {
  if (index === 0) {
    return i18n.t('daily_log.day.today');
  }
  if (index === 1) {
    return i18n.t('daily_log.day.yesterday');
  }
  return i18n.formatDate(dayDate(today, index), { weekday: 'long' });
}

/** The device's calendar date, `yyyy-MM-dd`. */
export function localDate(now: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** ISO-8601 in the device's own offset, not UTC. `day` puts the device's time of day on another date. */
export function localIso(now: Date, day: string | null = null): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  const offset = -now.getTimezoneOffset();
  const sign = offset >= 0 ? '+' : '-';
  return (
    `${day ?? localDate(now)}T${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}` +
    `${sign}${pad(Math.floor(Math.abs(offset) / 60))}:${pad(Math.abs(offset) % 60)}`
  );
}
