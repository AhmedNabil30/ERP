import { describe, expect, it } from 'vitest';

import { hasHiddenPrecision, percentOfFraction } from './extract-display';

describe('percentOfFraction', () => {
  it('shows a fraction as a percentage by moving the point, never by arithmetic', () => {
    expect(percentOfFraction('0.200000')).toBe('20');
    expect(percentOfFraction('1.000000')).toBe('100');
    expect(percentOfFraction('1')).toBe('100');
    expect(percentOfFraction('0.125')).toBe('12.5');
    expect(percentOfFraction('0.75')).toBe('75');
    expect(percentOfFraction('0.050000')).toBe('5');
  });

  it('renders the same value the same way whatever trailing zeros the server sent', () => {
    expect(percentOfFraction('1')).toBe(percentOfFraction('1.000000'));
  });
});

describe('hasHiddenPrecision', () => {
  it('is false when only zeros follow the printed decimals', () => {
    expect(hasHiddenPrecision('1231.6500', 2)).toBe(false);
    expect(hasHiddenPrecision('985.32', 2)).toBe(false);
    expect(hasHiddenPrecision('5', 2)).toBe(false);
  });

  it('is true when a non-zero digit would be hidden by rounding to the printed decimals', () => {
    expect(hasHiddenPrecision('1231.6550', 2)).toBe(true);
    expect(hasHiddenPrecision('0.0001', 2)).toBe(true);
  });
});
