/**
 * String-only helpers for money on the wire. D-135: an amount crosses the wire as a decimal string and
 * is never turned into a JavaScript number, so these compare and classify strings and do no arithmetic.
 */

/** True for "0", "0.00", "-0.0000" and the like. */
export function isZeroAmount(value: string): boolean {
  return /^[-+]?0*(\.0*)?$/.test(value.trim());
}

/** Splits a wire amount into its sign and magnitude, for rendering the sign as its own glyph. */
export function splitSigned(value: string): { readonly negative: boolean; readonly magnitude: string } {
  const trimmed = value.trim();
  const negative = trimmed.startsWith('-');
  const magnitude = trimmed.replace(/^[-+]/, '');
  return { negative: negative && !isZeroAmount(trimmed), magnitude };
}

/** Numeric equality of two wire amounts that may differ in trailing zeros ("120000" vs "120000.0000"). */
export function sameAmount(left: string, right: string): boolean {
  return canonical(left) === canonical(right);
}

function canonical(value: string): string {
  if (isZeroAmount(value)) {
    return '0';
  }
  const { negative, magnitude } = splitSigned(value);
  const [whole = '0', fraction = ''] = magnitude.split('.');
  const integer = whole.replace(/^0+(?=\d)/, '');
  const decimals = fraction.replace(/0+$/, '');
  return `${negative ? '-' : ''}${integer}${decimals ? `.${decimals}` : ''}`;
}
