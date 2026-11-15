import { I18nService } from '../i18n/i18n.service';

// ponytail: the list awaits Karim (Q-225-3). Change it here and in Kaff.Domain.DailyLogs.MaterialUnits, with the daily_log.unit.* strings.
/** The keys a material entry is recorded in; the label is `daily_log.unit.<key>`. */
export const MATERIAL_UNIT_KEYS: readonly string[] = ['bag', 'm3', 'm2', 'ton', 'metre', 'piece', 'litre'];

/** A stored key shows as its label; a stored unit that is not a key (a row from before the keys) shows as it was stored. */
export function materialUnitLabel(i18n: I18nService, unit: string): string {
  return MATERIAL_UNIT_KEYS.includes(unit) ? i18n.t(`daily_log.unit.${unit}`) : unit;
}
