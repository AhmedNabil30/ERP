/**
 * Converts a price field's raw text into the number `decimal CostPrice` / `BaseSellRate` need on the
 * wire. **No rounding, no clamping.**
 *
 * `V-35-Q`: `Money`'s constructor already rounds away-from-zero above four decimals, system-wide, and
 * `AC-200-B` — whether that is correct — is still open with Nabil. Rounding here, even to "just" clean
 * up a value before sending it, would answer his open question in a file nobody will read. The server
 * is the single place a price may be rounded.
 */
export function toWireDecimal(raw: string): number {
  return Number(raw.trim());
}
