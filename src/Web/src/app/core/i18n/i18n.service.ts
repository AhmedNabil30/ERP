import { HttpClient } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** Locales the application ships. Arabic is the product language; English is for development. */
export type Locale = 'ar' | 'en';

export type Direction = 'rtl' | 'ltr';

type Catalogue = Readonly<Record<string, string>>;

const LOCALE_STORAGE_KEY = 'kaff.locale';

/**
 * `-u-nu-latn` is load-bearing, not decoration.
 *
 * The default numbering system for `ar-EG` is `arab`, so `Intl` would render every money figure in
 * Arabic-Indic digits with U+066B as the decimal separator — ١٬٢٣٤٫٥٠ rather than 1,234.50. Kaff's
 * staff read money in Western digits, and a CSS `font-variant-numeric` cannot change which digits
 * `Intl` emits. See decisions.md D-036.
 */
const INTL_LOCALE: Readonly<Record<Locale, string>> = {
  ar: 'ar-EG-u-nu-latn',
  en: 'en-GB',
};

/** First-strong isolate and pop-directional-isolate, U+2068 and U+2069. */
const ISOLATE_START = '⁨';
const ISOLATE_END = '⁩';

const DIRECTION: Readonly<Record<Locale, Direction>> = {
  ar: 'rtl',
  en: 'ltr',
};

/**
 * Translation, direction and locale-aware formatting.
 *
 * A runtime catalogue rather than Angular's compile-time i18n, for one concrete reason: the API
 * returns error identifiers, not sentences — a ProblemDetails carries `messageKey`, because
 * CLAUDE.md forbids the server sending user-facing prose. Resolving a key that is only known at
 * runtime is exactly what `$localize` cannot do. One mechanism for both server keys and template
 * text beats two.
 *
 * `t()` reads the `locale` and `catalogue` signals, so any template expression that calls it is
 * tracked and re-renders when the language changes. That works without Zone.js.
 */
@Injectable({ providedIn: 'root' })
export class I18nService {
  private readonly http = inject(HttpClient);

  private readonly catalogue = signal<Catalogue>({});
  private readonly missingKeys = new Set<string>();

  readonly locale = signal<Locale>(readStoredLocale());

  readonly direction = computed<Direction>(() => DIRECTION[this.locale()]);

  constructor() {
    // The document element carries direction and language. CSS uses logical properties throughout,
    // so this single attribute flips the whole layout — nothing is mirrored by hand.
    effect(() => {
      const locale = this.locale();
      const root = document.documentElement;
      root.setAttribute('lang', locale);
      root.setAttribute('dir', DIRECTION[locale]);
    });
  }

  /** Loads the active catalogue. Awaited during bootstrap so nothing renders untranslated. */
  async initialise(): Promise<void> {
    await this.load(this.locale());
  }

  async use(locale: Locale): Promise<void> {
    if (locale === this.locale() && Object.keys(this.catalogue()).length > 0) {
      return;
    }

    await this.load(locale);
    this.locale.set(locale);
    localStorage.setItem(LOCALE_STORAGE_KEY, locale);
  }

  /**
   * Resolves a key. Placeholders are written `{name}`.
   *
   * A missing key returns the key itself rather than an empty string, so a gap is visible on screen
   * and in the console instead of silently rendering nothing.
   */
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    const template = this.catalogue()[key];

    if (template === undefined) {
      if (!this.missingKeys.has(key)) {
        this.missingKeys.add(key);
        console.warn(`[i18n] missing key: ${key}`);
      }
      return key;
    }

    if (!params) {
      return template;
    }

    return template.replace(/\{(\w+)\}/g, (match, name: string) => {
      const value = params[name];

      if (value === undefined) {
        return match;
      }

      // Wrap every substituted value in a bidi isolate. Codes, amounts, phone numbers and email
      // addresses are Latin runs inside an Arabic sentence, and without isolation the bidi algorithm
      // moves their trailing punctuation to the wrong visual end — KF-2026-014 renders as
      // 014-2026-KF. These are plain characters rather than markup, so they survive text
      // interpolation where a <bdi> element could not. See decisions.md D-036.
      return `${ISOLATE_START}${String(value)}${ISOLATE_END}`;
    });
  }

  /** Locale-aware number formatting. Money is formatted to two decimals for display. */
  formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
    return new Intl.NumberFormat(INTL_LOCALE[this.locale()], options).format(value);
  }

  /**
   * Formats an amount in EGP. spec.md §1: one currency, no conversion.
   *
   * Exactly two decimals, minimum and maximum. Karim, 2026-08-20: "Calculations and database storage
   * must maintain 4 decimal places for precision, but client-facing extracts and UI displays must be
   * rounded to 2 decimal places." The backend stores decimal(18,4) and the rounding happens here, at
   * the last possible moment — rounding earlier would let display precision leak back into the
   * arithmetic, which is the mistake the 4/2 split exists to prevent. See decisions.md D-044.
   */
  formatMoney(value: number): string {
    return this.formatNumber(value, {
      style: 'currency',
      currency: 'EGP',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
  }

  /**
   * Gregorian is pinned rather than assumed. `ar-EG` already defaults to it, and a contractual
   * document silently switching to Hijri after a browser or ICU update is not a risk worth carrying
   * for the sake of omitting one property.
   */
  formatDate(value: Date, options?: Intl.DateTimeFormatOptions): string {
    return new Intl.DateTimeFormat(INTL_LOCALE[this.locale()], {
      calendar: 'gregory',
      ...options,
    }).format(value);
  }

  private async load(locale: Locale): Promise<void> {
    const catalogue = await firstValueFrom(
      this.http.get<Catalogue>(`locales/${locale}.json`),
    );
    this.catalogue.set(catalogue);
  }
}

function readStoredLocale(): Locale {
  const stored = localStorage.getItem(LOCALE_STORAGE_KEY);
  return stored === 'en' ? 'en' : 'ar';
}
