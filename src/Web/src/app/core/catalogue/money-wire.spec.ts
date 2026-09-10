import { describe, expect, it } from 'vitest';

import { toWireDecimal } from './money-wire';

/**
 * `AC-202-C` / `V-35-Q`, made falsifiable. The client must never round a price — `Money`'s constructor
 * is the one place that happens, and `AC-200-B` (whether four-decimal rounding is even correct) is
 * still open with Nabil. Round here "for tidiness" and this file goes red.
 */
describe('toWireDecimal', () => {
  /**
   * The mutation this file exists to catch: round to 2 decimals (`Math.round(x * 100) / 100`) before
   * sending, the way a naive "clean up the input" instinct would. `987.6543` becomes `987.65` and this
   * assertion fails — which is the point, because the server round-trips all four digits (`AC-202-C`).
   */
  it('parses a four-decimal price exactly, without rounding', () => {
    expect(toWireDecimal('987.6543')).toBe(987.6543);
  });

  it('trims surrounding whitespace', () => {
    expect(toWireDecimal(' 12.5 ')).toBe(12.5);
  });

  it('a blank field becomes 0 rather than NaN', () => {
    expect(toWireDecimal('')).toBe(0);
  });

  it('does not clamp a negative value — refusing one is the server’s job, AC-202-D', () => {
    expect(toWireDecimal('-1')).toBe(-1);
  });
});
