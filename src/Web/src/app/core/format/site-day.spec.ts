import { describe, expect, it } from 'vitest';

import { dmyOf, siteDayOf } from './site-day';

describe('site day', () => {
  it('keeps a bare date as it is', () => {
    expect(siteDayOf('2026-11-24')).toBe('2026-11-24');
    expect(dmyOf('2026-11-24')).toBe('24/11/2026');
  });

  it('reads an instant in Cairo, not in UTC (a request made at 01:30 Cairo time is that day)', () => {
    // 2026-11-23T23:30Z is 01:30 on the 24th in Cairo (UTC+2 in winter).
    expect(dmyOf('2026-11-23T23:30:00+00:00')).toBe('24/11/2026');
    expect(dmyOf('2026-11-24T10:00:00+00:00')).toBe('24/11/2026');
  });

  it('is empty for nothing', () => {
    expect(dmyOf(null)).toBe('');
  });
});
