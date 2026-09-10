import { describe, expect, it } from 'vitest';

import { Bab } from './babs.api';
import { CatalogueItem } from './catalogue.api';
import { groupByBab } from './group-by-bab';

function bab(id: string, sortOrder: number): Bab {
  return {
    id,
    code: `B-${sortOrder}`,
    nameAr: `باب ${sortOrder}`,
    nameEn: `Bab ${sortOrder}`,
    parentBabId: null,
    defaultMarkup: 0.15,
    isActive: true,
  };
}

function item(id: string, code: string, babId: string): CatalogueItem {
  return {
    id,
    code,
    descriptionAr: `بند ${code}`,
    descriptionEn: null,
    unit: 'م٢',
    babId,
    costPrice: 10,
    baseSellRate: 15,
    status: 'Active',
  };
}

describe('groupByBab', () => {
  const babA = bab('bab-a', 1);
  const babB = bab('bab-b', 2);

  it('buckets consecutive items sharing a باب into one group, in arrival order', () => {
    const items = [
      item('1', 'A-100', babA.id),
      item('2', 'A-200', babA.id),
      item('3', 'B-100', babB.id),
    ];

    const groups = groupByBab(items, [babA, babB]);

    expect(groups).toHaveLength(2);
    expect(groups[0].babId).toBe(babA.id);
    expect(groups[0].items.map((entry) => entry.code)).toEqual(['A-100', 'A-200']);
    expect(groups[1].babId).toBe(babB.id);
    expect(groups[1].items.map((entry) => entry.code)).toEqual(['B-100']);
  });

  /**
   * `AC-203-I`'s own requirement, restated in the brief: "An item whose `BabId` matches no باب must
   * still appear, not vanish." Mutate this to `.filter((g) => g.bab !== null)` and the assertion below
   * catches it — the group is still there, `bab` is simply `null`.
   */
  it('an item whose babId matches no باب still appears, with bab set to null', () => {
    const orphan = item('9', 'X-1', 'no-such-bab');

    const groups = groupByBab([orphan], [babA, babB]);

    expect(groups).toHaveLength(1);
    expect(groups[0].bab).toBeNull();
    expect(groups[0].items).toEqual([orphan]);
  });

  it('does not re-sort — two runs of the same باب separated by another stay two groups', () => {
    const items = [
      item('1', 'A-100', babA.id),
      item('2', 'B-100', babB.id),
      item('3', 'A-900', babA.id),
    ];

    const groups = groupByBab(items, [babA, babB]);

    expect(groups.map((g) => g.babId)).toEqual([babA.id, babB.id, babA.id]);
  });

  it('an empty item list produces no groups', () => {
    expect(groupByBab([], [babA])).toEqual([]);
  });
});
