/**
 * The wire grammar for a `decimal`, D-135: ASCII digits only, an optional leading `-`, an optional
 * `.`, no exponent, no thousands separator, no Arabic-Indic digits. Exported so the form's validator
 * checks the exact grammar the server enforces, rather than a second guess at it.
 */
export const WIRE_DECIMAL_PATTERN = /^-?[0-9]+(\.[0-9]+)?$/;

/**
 * Converts a price field's raw text into the string `decimal CostPrice` / `BaseSellRate` travel as on
 * the wire — D-135. **No rounding, no clamping, no `Number()`.** The text is returned unchanged
 * (only trimmed) once it matches the grammar; a caller that already validated with
 * `WIRE_DECIMAL_PATTERN` (the form's `pattern` validator) never hits the throw below in practice.
 *
 * `V-35-Q`: `Money`'s constructor already rounds away-from-zero above four decimals, system-wide, and
 * `AC-200-B` — whether that is correct — is still open with Nabil. Rounding here, even to "just" clean
 * up a value before sending it, would answer his open question in a file nobody will read. The server
 * is the single place a price may be rounded.
 */
export function toWireDecimal(raw: string): string {
  const trimmed = raw.trim();

  if (!WIRE_DECIMAL_PATTERN.test(trimmed)) {
    throw new Error(`toWireDecimal: "${raw}" is not a wire decimal`);
  }

  return trimmed;
}
