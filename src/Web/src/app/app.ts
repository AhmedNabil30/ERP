import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { I18nService, Locale } from './core/i18n/i18n.service';

interface LocaleOption {
  readonly code: Locale;
  readonly label: string;
}

/**
 * The application shell.
 *
 * Standalone, zoneless, signal-driven, new control flow. CLAUDE.md is explicit that mixing Angular
 * eras is the main frontend risk on this project — there are no NgModules here and none may be added.
 */
@Component({
  selector: 'kaff-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly i18n = inject(I18nService);

  /** Held as a field, not an inline template literal, so the list is not rebuilt on every render. */
  protected readonly locales: readonly LocaleOption[] = [
    { code: 'ar', label: 'العربية' },
    { code: 'en', label: 'English' },
  ];

  protected async switchLocale(locale: Locale): Promise<void> {
    await this.i18n.use(locale);
  }
}
