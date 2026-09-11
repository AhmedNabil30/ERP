/**
 * `Percentage.DefaultMarkup` crosses the wire as **the fraction** (D-135, KAFF-204 rule 8):
 * `"0.15"` is 15%. Every screen shows the operator a percent, never a fraction, so this is the one
 * place that shifts the decimal point between the two — by string manipulation, never `Number()`,
 * for the same reason `money-wire.ts` never floats a price: a `decimal(18,6)` fraction does not
 * survive a round trip through a JavaScript `double`, and `AC-204-B` requires 12.75% to survive that
 * round trip exactly.
 */

/** What an operator may type into the markup field: no negative (`Percentage` throws on one), no exponent. */
export const PERCENT_INPUT_PATTERN = /^[0-9]+(\.[0-9]+)?$/;

/** Shifts a non-negative decimal string's point by `places` (positive = right), trimming to canonical form. */
function shiftDecimal(raw: string, places: number): string {
  const [wholePart, fractionPart = ''] = raw.split('.');
  let digits = wholePart + fractionPart;
  let point = wholePart.length + places;

  if (point <= 0) {
    digits = '0'.repeat(1 - point) + digits;
    point = 1;
  } else if (point > digits.length) {
    digits = digits + '0'.repeat(point - digits.length);
  }

  const wholeOut = digits.slice(0, point).replace(/^0+(?=\d)/, '');
  const fractionOut = digits.slice(point).replace(/0+$/, '');

  return fractionOut.length > 0 ? `${wholeOut}.${fractionOut}` : wholeOut;
}

/** `"0.15"` (the wire fraction) -> `"15"` (the percent an operator reads). */
export function fractionToPercent(fraction: string): string {
  if (!/^[0-9]+(\.[0-9]+)?$/.test(fraction)) {
    throw new Error(`fractionToPercent: "${fraction}" is not a wire fraction`);
  }
  return shiftDecimal(fraction, 2);
}

/** `"15"` (what an operator typed) -> `"0.15"` (the wire fraction). No rounding — the server rounds. */
export function percentToFraction(percent: string): string {
  const trimmed = percent.trim();

  if (!PERCENT_INPUT_PATTERN.test(trimmed)) {
    throw new Error(`percentToFraction: "${percent}" is not a percent`);
  }
  return shiftDecimal(trimmed, -2);
}
