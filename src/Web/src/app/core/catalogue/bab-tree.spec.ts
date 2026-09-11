import { describe, expect, it } from 'vitest';

import { Bab } from './babs.api';
import { flattenBabTree } from './bab-tree';

function bab(id: string, parentBabId: string | null): Bab {
  return {
    id,
    code: id,
    nameAr: id,
    nameEn: id,
    parentBabId,
    defaultMarkup: '0.1',
    isActive: true,
  };
}

describe('flattenBabTree', () => {
  it('puts a root immediately followed by its own children, before the next root', () => {
    const rows = flattenBabTree([
      bab('root-a', null),
      bab('root-b', null),
      bab('child-of-a', 'root-a'),
    ]);

    expect(rows.map((row) => row.bab.id)).toEqual(['root-a', 'child-of-a', 'root-b']);
  });

  it('gives each row the depth AC-204-I indents by', () => {
    const rows = flattenBabTree([
      bab('a', null),
      bab('b', 'a'),
      bab('c', 'b'),
    ]);

    expect(rows.map((row) => row.depth)).toEqual([0, 1, 2]);
  });

  it('AC-205-B: a cleared parent renders at the top level with its own children unchanged', () => {
    const rows = flattenBabTree([bab('parent', null), bab('freed', null), bab('kept-child', 'freed')]);

    expect(rows.map((row) => [row.bab.id, row.depth])).toEqual([
      ['parent', 0],
      ['freed', 0],
      ['kept-child', 1],
    ]);
  });

  it('does not hang on a cycle already in the data, and still renders every row once', () => {
    const rows = flattenBabTree([bab('a', 'b'), bab('b', 'a')]);

    expect(rows).toHaveLength(2);
  });
});
