import { describe, expect, it } from 'vitest';

import { PERCENT_INPUT_PATTERN, fractionToPercent, percentToFraction } from './percent-wire';

/**
 * `AC-204-B`: "15% is 15%, not 15" and "12.75% survives the round trip exactly, at both the storage
 * and the display precision D-044 ruling 6 sets." Pure string arithmetic — no `Number()` anywhere —
 * for the same reason `money-wire.spec.ts` pins `toWireDecimal`.
 */
describe('percentToFraction', () => {
  it('turns a whole-number percent into its fraction — 15% is 0.15, not 1600% of anything', () => {
    expect(percentToFraction('15')).toBe('0.15');
  });

  it('round-trips 12.75% exactly — AC-204-B', () => {
    expect(percentToFraction('12.75')).toBe('0.1275');
  });

  it('handles a single-digit percent below one', () => {
    expect(percentToFraction('5')).toBe('0.05');
  });

  it('accepts the fixture markup, zero', () => {
    expect(percentToFraction('0')).toBe('0');
  });

  it('refuses a negative — Percentage throws on one, this refuses before the request is sent', () => {
    expect(() => percentToFraction('-5')).toThrow();
  });

  it('refuses Arabic-Indic digits, matching money-wire’s own refusal', () => {
    expect(() => percentToFraction('١٥')).toThrow();
  });
});

describe('fractionToPercent', () => {
  it('turns a wire fraction back into the percent an operator reads', () => {
    expect(fractionToPercent('0.15')).toBe('15');
  });

  it('round-trips the six-decimal storage form exactly — AC-204-B', () => {
    expect(fractionToPercent('0.1275')).toBe('12.75');
    expect(fractionToPercent('0.150000')).toBe('15');
  });

  it('round-trips percentToFraction’s own output for every case above', () => {
    for (const percent of ['15', '12.75', '5', '0']) {
      expect(fractionToPercent(percentToFraction(percent))).toBe(percent);
    }
  });
});

describe('PERCENT_INPUT_PATTERN', () => {
  it('accepts a whole number and a decimal', () => {
    expect(PERCENT_INPUT_PATTERN.test('15')).toBe(true);
    expect(PERCENT_INPUT_PATTERN.test('12.75')).toBe(true);
  });

  it('rejects a negative and an exponent', () => {
    expect(PERCENT_INPUT_PATTERN.test('-5')).toBe(false);
    expect(PERCENT_INPUT_PATTERN.test('1e3')).toBe(false);
  });
});
