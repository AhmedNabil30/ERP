import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';

import { AuthService } from './core/auth/auth.service';
import { SessionResolver } from './core/auth/session-resolver';
import { I18nService, Locale } from './core/i18n/i18n.service';
import { navLabelKeyFor } from './core/navigation/landing';

interface LocaleOption {
  readonly code: Locale;
  readonly label: string;
}

/**
 * The application shell. KAFF-125: S-004's dispatch made visible, and the staff chrome built on it.
 *
 * **The three session states of `ux/navigation.md` -> "The shell has three session states, not two",
 * rendered directly, not inferred.** `resolved()` false is `resolving` (the boot surface, `AC-125-A`);
 * `resolved()` true and no session is `signed-out` (whatever route was requested renders on its own —
 * `/sign-in`, `/change-password` — with no staff chrome around it); `resolved()` true and a session
 * that has finished a forced password change is `signed-in`, and only then does the staff chrome
 * (side nav, account menu) mount at all.
 *
 * **Resolves the session itself, once, in the constructor.** `SessionResolver.ensureResolved` is
 * idempotent, so a route guard that also calls it (KAFF-125's `sessionGuard`,
 * `must-change-password.guard.ts`) shares this same request rather than firing a second one — but
 * `/sign-in` and `/change-password` carry no guard of their own, and without a call here the shell
 * would sit on the boot surface forever on a direct load of either, because nothing would ever ask.
 *
 * Standalone, zoneless, signal-driven, new control flow. CLAUDE.md is explicit that mixing Angular
 * eras is the main frontend risk on this project — there are no NgModules here and none may be added.
 */
@Component({
  selector: 'kaff-root',
  imports: [RouterLink, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly resolver = inject(SessionResolver);
  private readonly router = inject(Router);

  protected readonly i18n = inject(I18nService);

  /** Held as a field, not an inline template literal, so the list is not rebuilt on every render. */
  protected readonly locales: readonly LocaleOption[] = [
    { code: 'ar', label: 'العربية' },
    { code: 'en', label: 'English' },
  ];

  private readonly navOpen = signal(false);
  protected readonly isNavOpen = this.navOpen.asReadonly();

  protected readonly resolved = this.auth.resolved;
  protected readonly session = this.auth.current;

  /**
   * The side nav (and its drawer/hamburger furniture) mounts only for a real signed-in staff session
   * with a real destination — never mid forced password change (`AC-125-D`: "no navigation item"),
   * and never for the defensive `Role.Client` / `Role.Subcontractor` fallback `navLabelKeyFor` answers
   * `null` for (`ux/navigation.md`: "mounts no staff chrome — not one frame, not empty").
   */
  protected readonly showStaffNav = computed(() => {
    const session = this.session();
    if (!session || session.mustChangePassword) {
      return false;
    }
    return navLabelKeyFor(session.role) !== null;
  });

  /** `null` only in the defensive fallback {@link showStaffNav} already excludes from the drawer. */
  protected readonly navLabelKey = computed(() => {
    const session = this.session();
    return session ? navLabelKeyFor(session.role) : null;
  });

  constructor() {
    void this.resolver.ensureResolved();
  }

  protected async switchLocale(locale: Locale): Promise<void> {
    await this.i18n.use(locale);
  }

  protected toggleNav(): void {
    this.navOpen.update((open) => !open);
  }

  protected closeNav(): void {
    this.navOpen.set(false);
  }

  /**
   * AC-125-E. `SessionResolver.signOut` does the actual work — ends the server session, forgets the
   * profile so completely the shell passes back through `resolving`, and asks again. This only closes
   * the drawer first (so a stale nav is not what briefly shows through the boot surface) and lands the
   * caller on `/sign-in` once the round trip settles.
   */
  protected async signOut(): Promise<void> {
    this.closeNav();
    await this.resolver.signOut();
    await this.router.navigateByUrl('/sign-in');
  }
}
