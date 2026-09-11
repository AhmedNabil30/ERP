import { Bab } from './babs.api';

/** One row of the flattened tree — `AC-204-I`'s indentation is `depth` turned into a logical property. */
export interface BabTreeRow {
  readonly bab: Bab;
  readonly depth: number;
}

/**
 * Flattens `ListBabs`'s flat, already-ordered array into tree order — pre-order, each باب immediately
 * followed by its own children before its next sibling. `S-021` renders this list directly, indenting
 * each row by `depth` through `margin-inline-start` (a logical property, `AC-204-I`) rather than
 * nesting `@for` inside `@for`, which is enough for an "at most ~40 trades" tree (`KAFF-204` rule 6).
 *
 * **Does no sorting of its own** — `ListBabs.Handler` orders by `SortOrder` then `Code`, and grouping
 * by parent with a `Map` preserves that relative order within each parent's own children, the same
 * reasoning `groupByBab` states for the catalogue list.
 *
 * **Guards against a cycle already in the data** rather than trusting `Bab.SetParent`'s guard to have
 * been run for every row that exists — a defensive bound, not a case this file expects to hit.
 */
export function flattenBabTree(babs: readonly Bab[]): readonly BabTreeRow[] {
  const childrenByParent = new Map<string | null, Bab[]>();

  for (const bab of babs) {
    const siblings = childrenByParent.get(bab.parentBabId) ?? [];
    siblings.push(bab);
    childrenByParent.set(bab.parentBabId, siblings);
  }

  const rows: BabTreeRow[] = [];
  const visited = new Set<string>();

  function visit(parentId: string | null, depth: number): void {
    for (const bab of childrenByParent.get(parentId) ?? []) {
      if (visited.has(bab.id)) {
        continue;
      }
      visited.add(bab.id);
      rows.push({ bab, depth });
      visit(bab.id, depth + 1);
    }
  }

  visit(null, 0);

  // A باب whose ParentBabId points nowhere reachable from a root (should not happen once
  // `Bab.SetParent`'s guard has run against every row, but this is a rendering function, not the
  // guard) still renders, at the top level, rather than silently vanishing from the tree.
  for (const bab of babs) {
    if (!visited.has(bab.id)) {
      visited.add(bab.id);
      rows.push({ bab, depth: 0 });
    }
  }

  return rows;
}
