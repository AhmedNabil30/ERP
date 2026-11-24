import { describe, expect, it } from 'vitest';

import { agoKey, daysQuiet, nextStage, statusKey } from './opportunity-display';

describe('opportunity display', () => {
  it('next stage stops at Negotiation: no manual move to Contract (D-268)', () => {
    expect(nextStage('Quotation')).toBe('Negotiation');
    expect(nextStage('Negotiation')).toBeNull();
    expect(nextStage('Contract')).toBeNull();
  });

  it('a stalled opportunity reads as active', () => {
    expect(statusKey('Stalled')).toBe('sales.status.Active');
    expect(statusKey('OnHold')).toBe('sales.status.OnHold');
  });

  it('counts whole quiet days and never goes negative', () => {
    const now = new Date('2026-11-24T12:00:00Z');
    expect(daysQuiet('2026-11-21T11:00:00Z', now)).toBe(3);
    expect(daysQuiet('2026-11-25T11:00:00Z', now)).toBe(0);
  });

  it('picks the wording for the day count', () => {
    expect(agoKey(0).key).toBe('sales.ago.today');
    expect(agoKey(2).key).toBe('sales.ago.two');
    expect(agoKey(6)).toEqual({ key: 'sales.ago.few', params: { n: 6 } });
    expect(agoKey(12).key).toBe('sales.ago.many');
  });
});
