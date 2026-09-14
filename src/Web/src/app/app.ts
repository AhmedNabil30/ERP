import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NgTemplateOutlet } from '@angular/common';
import { filter, map } from 'rxjs';

import { AuthService } from './core/auth/auth.service';
import { SessionResolver } from './core/auth/session-resolver';
import { roleKey } from './core/i18n/enum-keys';
import { I18nService, Locale } from './core/i18n/i18n.service';
import { HeaderActionsService } from './core/layout/header-actions.service';
import { navLabelKeyFor } from './core/navigation/landing';
import { NavRow, NavRowGroup, navGroupsFor, navRowsFor } from './core/navigation/nav-rows';
import {
  KaffSegmentedFilter,
  SegmentedFilterOption,
} from './shared/kaff-segmented-filter/kaff-segmented-filter';

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
  imports: [RouterLink, RouterLinkActive, RouterOutlet, NgTemplateOutlet, KaffSegmentedFilter],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly resolver = inject(SessionResolver);
  private readonly router = inject(Router);

  protected readonly i18n = inject(I18nService);
  protected readonly headerActions = inject(HeaderActionsService);

  /**
   * KAFF-927: `kaff-segmented-filter`'s own option shape, reused rather than a second segmented
   * control (`ux/components.md`'s "one of each" rule). Held as a field, not an inline template
   * literal, so the list is not rebuilt on every render.
   */
  protected readonly localeOptions: readonly SegmentedFilterOption<Locale>[] = [
    { value: 'ar', labelKey: 'locale.ar' },
    { value: 'en', labelKey: 'locale.en' },
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
    return navLabelKeyFor(session) !== null;
  });

  /**
   * KAFF-215: every row the session's permission set reaches, not the single post-login landing —
   * `landing.ts`'s `landingFor`/`navPathFor`/`navLabelKeyFor` are untouched (rule 2, `AC-215-H`) and
   * answer a different question ("where does this session land right after sign-in"). This is the
   * separate, second list `app.html` iterates; the template names no path or permission of its own.
   */
  protected readonly navRows = computed<readonly NavRow[]>(() => {
    const session = this.session();
    return session ? navRowsFor(session) : [];
  });

  /**
   * KAFF-922: the mockup's two sections, built by {@link navGroupsFor} off `navRowsFor`'s own `group`
   * field — never a template-hardcoded list of headings. A section with no row in it is dropped
   * before the template ever sees it, which is what `AC-922-B` means by "renders no heading for it".
   */
  protected readonly navGroups = computed<readonly NavRowGroup[]>(() => {
    const session = this.session();
    return session ? navGroupsFor(session) : [];
  });

  /** Reactive current URL — a plain `router.url` read is not itself a signal, so this exists only
   *  to give {@link sectionLabelKey} something that changes on navigation. */
  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /**
   * KAFF-923: the top bar's section label, replacing the old `app.name` `<h1>` — the shell no
   * longer owns the page `<h1>` (see the story's ownership decision), each feature page keeps
   * rendering its own. Derived from which {@link navGroups} row's path prefixes the current URL,
   * never hardcoded per route.
   */
  protected readonly sectionLabelKey = computed<string | null>(() => {
    const url = this.currentUrl();
    const group = this.navGroups().find((candidate) =>
      candidate.rows.some((row) => url === row.path || url.startsWith(row.path + '/')),
    );
    return group?.labelKey ?? null;
  });

  /** Exposed for the template — `roleKey` builds the i18n key by an exhaustive switch, never by
   *  keying on the raw role string (`enum-keys.ts` hard rule 4). */
  protected readonly roleKey = roleKey;

  /**
   * Avatar initials — first letter of each of the first two words of the display name, Arabic-safe.
   * `[...word]` spreads by code point rather than UTF-16 code unit, so it does not split a name whose
   * first character is outside the BMP.
   */
  protected initialsFor(displayName: string): string {
    const words = displayName.trim().split(/\s+/u).filter((word) => word.length > 0);
    return words
      .slice(0, 2)
      .map((word) => [...word][0] ?? '')
      .join('');
  }

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
