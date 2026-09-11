import { describe, expect, it } from 'vitest';

import { WIRE_DECIMAL_PATTERN, toWireDecimal } from './money-wire';

/**
 * D-135 / `V-36-I` / `V-36-K`. The client must never round a price, never carry one through a JS
 * double, and never accept hex or an exponent. `toWireDecimal` now returns the typed text unchanged —
 * the string IS the wire value, so there is no double for `987.6543` to be rounded or truncated by.
 */
describe('toWireDecimal', () => {
  it('returns a four-decimal price exactly, as a string, without rounding', () => {
    expect(toWireDecimal('987.6543')).toBe('987.6543');
  });

  it('round-trips a 17-significant-digit price exactly — V-36-I', () => {
    expect(toWireDecimal('12345678901234.5678')).toBe('12345678901234.5678');
  });

  it('trims surrounding whitespace', () => {
    expect(toWireDecimal(' 12.5 ')).toBe('12.5');
  });

  it('does not clamp a negative value — refusing one is the server’s job, AC-202-D', () => {
    expect(toWireDecimal('-1')).toBe('-1');
  });

  it('refuses a blank field rather than silently becoming 0 — V-36-H', () => {
    expect(() => toWireDecimal('')).toThrow();
  });

  it('refuses an exponent — V-36-K', () => {
    expect(() => toWireDecimal('1e3')).toThrow();
  });

  it('refuses hex — V-36-K', () => {
    expect(() => toWireDecimal('0x10')).toThrow();
  });

  it('refuses Arabic-Indic digits — V-36-K, UX not yet ruled, so refused rather than converted', () => {
    expect(() => toWireDecimal('١٢٣٫٥')).toThrow();
  });
});

describe('WIRE_DECIMAL_PATTERN', () => {
  it('accepts the grammar the server accepts', () => {
    expect(WIRE_DECIMAL_PATTERN.test('12345678901234.5678')).toBe(true);
    expect(WIRE_DECIMAL_PATTERN.test('-12.5')).toBe(true);
  });

  it('rejects a thousands separator', () => {
    expect(WIRE_DECIMAL_PATTERN.test('1,234.5')).toBe(false);
  });
});
