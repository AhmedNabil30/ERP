import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { I18nService } from './i18n.service';

/** U+200F RIGHT-TO-LEFT MARK — a *strong* RTL character, which is the whole point of `V-35-I`. */
const RTL_MARK = '‏';

/**
 * `V-35-I`, pinned: **why the audit timestamp needs `dir="ltr"`.**
 *
 * The recorded reason was wrong for four days and was written into shipped source, into `STATUS.md`
 * and into a brief, and it produced a false prediction that reached the board as scheduled work
 * (`V-35-H`). The wrong reason: *"a `<bdi>` defaults to `dir="auto"`, which is first-strong, and a
 * timestamp contains no strong character at all, so first-strong finds nothing and falls back to the
 * paragraph's RTL."* `dir="auto"` with no strong character falls back to **`ltr`**, not to the
 * parent — a bare `<bdi>` already handles `::1`, `/api/auth/sign-in` and a slash-separated date.
 *
 * The real reason is one line of `formatDate`: **an Arabic locale makes `Intl.DateTimeFormat` inject
 * `U+200F` into the string it returns, and `U+200F` is a strong RTL character**, so first-strong
 * finds one immediately and resolves the isolate to RTL.
 *
 * **This file exists because that is a property of the formatter, not of the value.** Nobody can see
 * it by looking at the digits and separators — which is exactly how the wrong rule survived review —
 * so it is asserted here rather than left as prose next to correct code. A wrong explanation beside
 * a right fix is a defect, because the next person reasons from it.
 *
 * **What it does not claim.** It asserts the cause, not the rendering: resolved direction and visual
 * order are a layout fact, and the evidence for those is `V-35-I`'s runtime measurement on the live
 * page (removing `dir="ltr"` from the six shipped `bdi[dir="ltr"]` timestamps reordered all six).
 * A unit test cannot see that, and pretending otherwise would be the shape this project keeps paying
 * for.
 */
describe('an Arabic-locale formatter injects the strong character that forces dir="ltr"', () => {
  let i18n: I18nService;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideHttpClient()] });
    i18n = TestBed.inject(I18nService);
  });

  /** The audit trail's own call shape — `audit-trail-page.ts` -> `timestamp`. */
  function auditTimestamp(): string {
    i18n.locale.set('ar');

    return i18n.formatDate(new Date(Date.UTC(2026, 8, 7, 20, 34, 20)), {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    });
  }

  /**
   * The mechanism. Delete `dir="ltr"` from `bdi.row-when` and nothing here moves — this is not that
   * test, and `V-35-I` is. What this catches is the day the premise stops being true: an ICU or
   * locale change that drops the mark makes the attribute unnecessary and the comment beside it
   * wrong again, and a red test is how anyone finds out.
   */
  it('formatDate under ar returns a string carrying U+200F', () => {
    expect(auditTimestamp()).toContain(RTL_MARK);
  });

  /**
   * The control. Without it the assertion above would also pass on a mark this code put there, or on
   * one the `Date` somehow carried. `en-GB` formats the same instant with no mark at all, so the
   * `U+200F` is the Arabic formatter's and nothing else's.
   */
  it('the same call under en carries no mark, so the mark is the formatter and not the value', () => {
    i18n.locale.set('en');

    const english = i18n.formatDate(new Date(Date.UTC(2026, 8, 7, 20, 34, 20)), {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    });

    expect(english).not.toContain(RTL_MARK);
    expect(english).toMatch(/\d/);
  });

  /**
   * ⛔ **`V-35-I`'s forward-looking half is wrong, and writing it down as a check is what found it.**
   *
   * The report said: *"`formatNumber` / `formatMoney` has no call site yet; I checked
   * `Intl.NumberFormat('ar-EG')` directly and it emits Arabic-Indic digits with no `U+200F`, so money
   * is not pre-exposed. Slice 3 should re-check that the day it formats its first amount."*
   *
   * **It checked a different call from the one this codebase ships.** `formatMoney` passes
   * `style: 'currency', currency: 'EGP'`, and the currency style is what injects the marks — it
   * isolates the amount from the `ج.م.` symbol beside it. Measured 2026-09-08, all four combinations:
   *
   * | call | result | `U+200F` |
   * |---|---|---|
   * | `Intl.NumberFormat('ar-EG')` — what the report checked | `١٬٢٣٤٫٥` | no |
   * | `formatNumber`, i.e. `ar-EG-u-nu-latn` plain | `1,234.5` | no |
   * | **`formatMoney`, i.e. `ar-EG-u-nu-latn` + currency EGP** | `<RLM>1,234.50 ج.م.<RLM>` | **yes** |
   * | `ar-EG` + currency EGP | `<RLM>١٬٢٣٤٫٥٠ ج.م.<RLM>` | **yes** |
   *
   * So **money is pre-exposed today**, on the leading character, which is the one first-strong reads.
   * The first amount slice 3 renders inside a `<bdi>` will resolve RTL exactly as the audit timestamp
   * does, and will need `dir="ltr"` for the same reason. It is a defect in nothing shipped —
   * `formatMoney` has no call site — and it is asserted here rather than left as a note, because the
   * note is what was wrong. **The day this goes red is the day money stops needing the attribute.**
   */
  it('formatMoney under ar already carries the mark — money IS pre-exposed', () => {
    i18n.locale.set('ar');

    const money = i18n.formatMoney(1234.5);

    expect(money.startsWith(RTL_MARK)).toBe(true);

    // The control, and the half the report measured correctly: plain number formatting is clean, so
    // the mark comes from `style: 'currency'` and not from the locale or the Latin numbering system.
    expect(i18n.formatNumber(1234.5)).not.toContain(RTL_MARK);
  });
});
