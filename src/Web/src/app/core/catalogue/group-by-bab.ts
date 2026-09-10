import { CatalogueItem } from './catalogue.api';
import { Bab } from './babs.api';

/** One باب's run of items. `bab` is `null` when `babId` matches no باب — it must still render, not vanish. */
export interface CatalogueGroup {
  readonly babId: string;
  readonly bab: Bab | null;
  readonly items: readonly CatalogueItem[];
}

/**
 * Buckets an already-ordered item list into consecutive باب runs. `AC-203-I`.
 *
 * **Does no sorting of its own.** `ListCatalogueItems.Handler` orders by `bab.SortOrder` then
 * `item.Code` server-side; this only has to notice where one باب's run ends and the next begins,
 * which a single linear pass does. Re-sorting here would be a second, and possibly disagreeing,
 * ordering rule.
 */
export function groupByBab(
  items: readonly CatalogueItem[],
  babs: readonly Bab[],
): readonly CatalogueGroup[] {
  const babById = new Map(babs.map((bab) => [bab.id, bab]));
  const groups: CatalogueGroup[] = [];

  for (const item of items) {
    const last = groups.at(-1);

    if (last && last.babId === item.babId) {
      (last.items as CatalogueItem[]).push(item);
      continue;
    }

    groups.push({ babId: item.babId, bab: babById.get(item.babId) ?? null, items: [item] });
  }

  return groups;
}
