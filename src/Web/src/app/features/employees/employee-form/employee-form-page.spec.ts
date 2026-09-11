import { describe, expect, it } from 'vitest';

import { babRequiredButMissing } from './employee-form-page';

/**
 * KAFF-207 rule 3 / `AC-207-B`'s client-side echo: a day labourer must name a باب. Pinned as a pure
 * function so a regression here fails a unit test rather than only a much slower E2E run.
 */
describe('babRequiredButMissing', () => {
  it('is missing when kind is DayLabour and no باب was chosen', () => {
    expect(babRequiredButMissing('DayLabour', '')).toBe(true);
  });

  it('is missing when the باب field is only whitespace', () => {
    expect(babRequiredButMissing('DayLabour', '   ')).toBe(true);
  });

  it('is satisfied once a باب id is present', () => {
    expect(babRequiredButMissing('DayLabour', 'some-bab-id')).toBe(false);
  });

  it('never applies to salaried staff, باب or not', () => {
    expect(babRequiredButMissing('Salaried', '')).toBe(false);
    expect(babRequiredButMissing('Salaried', 'some-bab-id')).toBe(false);
  });
});
