import { describe, expect, it } from 'vitest';

import { ProjectProgress } from '../../../core/projects/projects.api';
import { PROGRESS_KINDS, progressViewOf, waitingViewOf } from './workspace-reading';

/**
 * The overview's pure readings. The point of the first test: a kind added to `ProgressKind` and to the
 * `Record` that draws it must be handled here too, and a kind the client has never heard of must be drawn
 * as "unavailable", never as a bar and never as 0%.
 */
describe('progressViewOf', () => {
  it('draws no bar for any kind that carries no completion, whatever the kind', () => {
    for (const kind of PROGRESS_KINDS) {
      const view = progressViewOf({ kind, asOf: '2026-11-06' } as ProjectProgress, 'ar');
      expect(view.kind, kind).not.toBe('bar');
    }
  });

  it('Cost Plus never becomes a bar, even if a completion were sent beside the money', () => {
    const view = progressViewOf(
      { kind: 'MonetaryOnly', asOf: '2026-11-06', completion: '0.5', costToDate: '1.0000' },
      'ar',
    );
    expect(view.kind).toBe('money');
  });

  it('a kind this client does not know is unavailable, not a bar', () => {
    const view = progressViewOf({ kind: 'Brand New', asOf: '2026-11-06', completion: '0.5' } as unknown as ProjectProgress, 'ar');
    expect(view.kind).toBe('unavailable');
  });

  it('parses the wire fraction by string shift, clamps the width, and rejects junk', () => {
    const at = (completion: string) => progressViewOf({ kind: 'Percentage', asOf: 'x', completion }, 'ar');
    expect(at('0.375')).toMatchObject({ kind: 'bar', percent: 37.5, percentText: '37.5' });
    expect(at('1.2')).toMatchObject({ kind: 'bar', percent: 100 });
    expect(at('nope').kind).toBe('unavailable');
  });

  it('picks the bab name for the locale and leaves a bab without completion without a percent', () => {
    const view = progressViewOf(
      {
        kind: 'Percentage',
        asOf: 'x',
        completion: '0.1',
        babs: [{ code: 'B1', nameAr: 'الحفر', nameEn: 'Excavation', isActive: true }],
      },
      'en',
    );
    expect(view).toMatchObject({ kind: 'bar', babs: [{ name: 'Excavation', percent: null, percentText: null, isActive: true }] });
  });
});

describe('waitingViewOf', () => {
  const staff = (userId: string) => ({ userId, fullName: userId, role: 'SiteEngineer', level: 'Supervisor' }) as const;
  const item = (number: number, status: string, terminal = false, holders = [staff('me')], barred = [staff('x')]) => ({
    extractId: `e-${number}`,
    number,
    waitingOn: {
      status,
      roles: [{ role: 'SiteEngineer' as const, minimumLevel: 'Supervisor' as const }],
      isDecisionGate: false,
      isTerminal: terminal,
      holders,
      barredHolders: barred,
    },
  });

  it('takes the newest open extract and ignores terminal ones', () => {
    const view = waitingViewOf([item(1, 'Draft'), item(3, 'Collected', true), item(2, 'QcSubmitted')], 'me');
    expect(view?.number).toBe(2);
  });

  it('knows the viewer holds the step, is barred from it, or neither', () => {
    expect(waitingViewOf([item(1, 'Draft')], 'me')).toMatchObject({ viewerIsHolder: true, viewerIsBarred: false, hasScreen: true });
    expect(waitingViewOf([item(1, 'Draft')], 'x')).toMatchObject({ viewerIsHolder: false, viewerIsBarred: true });
    expect(waitingViewOf([item(1, 'Draft')], 'other')).toMatchObject({ viewerIsHolder: false, viewerIsBarred: false });
  });

  it('marks supervisor-level site engineer as the supervisor, and a step without a web screen as having none', () => {
    const view = waitingViewOf([item(1, 'Issued')], 'me');
    expect(view?.roles[0]).toEqual({ role: 'SiteEngineer', supervisor: true });
    expect(view?.hasScreen).toBe(false);
  });

  it('an unknown status falls back to the generic step instead of throwing', () => {
    expect(waitingViewOf([item(1, 'Mystery')], 'me')?.stepKey).toBe('projects.workspace.waiting.step.other');
  });

  it('is null when nothing is open', () => {
    expect(waitingViewOf([item(1, 'Collected', true)], 'me')).toBeNull();
  });
});
