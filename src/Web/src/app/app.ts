import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NgTemplateOutlet } from '@angular/common';

import { AuthService } from './core/auth/auth.service';
import { SessionResolver } from './core/auth/session-resolver';
import { roleKey } from './core/i18n/enum-keys';
import { I18nService, Locale } from './core/i18n/i18n.service';
import { HeaderActionsService } from './core/layout/header-actions.service';
import { navLabelKeyFor } from './core/navigation/landing';
import { NavRowGroup, navGroupsFor } from './core/navigation/nav-rows';
import { ThemeService } from './core/theme/theme.service';

/**
 * The application shell, rebuilt to the approved layout (`.design/v2/team-a/layout.html` and the
 * README's "The application shell"). KAFF-125 built the session states; this keeps them and changes
 * only how they look.
 *
 * **The three session states of `ux/navigation.md` -> "The shell has three session states, not two",
 * rendered directly, not inferred.** `resolved()` false is `resolving` (the boot surface, `AC-125-A`);
 * `resolved()` true and no session is `signed-out` (whatever route was requested renders on its own —
 * `/sign-in`, `/change-password` — with only the slim brand header above it); `resolved()` true and a
 * session that has finished a forced password change is `signed-in`, and only then does the nav mount.
 *
 * **Resolves the session itself, once, in the constructor.** `SessionResolver.ensureResolved` is
 * idempotent, so a route guard that also calls it shares this same request rather than firing a
 * second one — but `/sign-in` and `/change-password` carry no guard of their own, and without a call
 * here the shell would sit on the boot surface forever on a direct load of either.
 *
 * Desktop: a 256px sidebar at the start edge, no top bar. 821 to 1100px the sidebar is the 76px icon
 * rail; the collapse button sets it by hand at any width. 820px and narrower: a slim green header, an
 * action bar at the thumb edge, and the destinations in a bottom sheet.
 *
 * Standalone, zoneless, signal-driven, new control flow. No NgModules, no Zone.js.
 */
@Component({
  selector: 'kaff-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, NgTemplateOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'closeNav()' },
})
export class App {
  private readonly auth = inject(AuthService);
  private readonly resolver = inject(SessionResolver);
  private readonly router = inject(Router);

  protected readonly i18n = inject(I18nService);
  protected readonly theme = inject(ThemeService);
  protected readonly headerActions = inject(HeaderActionsService);

  private readonly menuButton = viewChild<ElementRef<HTMLButtonElement>>('menuButton');
  private readonly sheetClose = viewChild<ElementRef<HTMLButtonElement>>('sheetClose');

  /** The phone's destinations sheet. */
  private readonly navOpen = signal(false);
  protected readonly isNavOpen = this.navOpen.asReadonly();

  /**
   * The desktop sidebar rail, set by hand (Nabil, 2026-09-26: "the side menue should be collapsable").
   *
   * **A chrome preference, so it persists in `localStorage`** — the same mechanism `i18n.service.ts`
   * uses for the chosen locale, and outside D-050's ban, which is on holding session or credential
   * material there. Per-device rather than per-user. Reads defensively: a private window, cleared site
   * data or a blocked storage origin makes the accessor throw rather than return null.
   */
  private static readonly NAV_COLLAPSED_KEY = 'kaff.nav.collapsed';

  private readonly navCollapsed = signal(App.readNavCollapsed());

  /** 821 to 1100px: the sidebar is a rail whatever the stored preference says (README). */
  private readonly narrowDesktop = signal(App.matchesNarrowDesktop());

  protected readonly isRail = computed(() => this.navCollapsed() || this.narrowDesktop());

  private static readNavCollapsed(): boolean {
    try {
      return localStorage.getItem(App.NAV_COLLAPSED_KEY) === 'true';
    } catch {
      return false;
    }
  }

  private static matchesNarrowDesktop(): boolean {
    return typeof matchMedia === 'function' && matchMedia('(max-width: 1100px)').matches;
  }

  protected toggleNavCollapsed(): void {
    const collapsed = !this.isRail();
    this.navCollapsed.set(collapsed);
    try {
      localStorage.setItem(App.NAV_COLLAPSED_KEY, String(collapsed));
    } catch {
      // A browser that refuses the write still gets the collapse — it just forgets it on reload.
    }
  }

  protected readonly resolved = this.auth.resolved;
  protected readonly session = this.auth.current;

  /**
   * The nav rows mount only for a real signed-in staff session with a real destination — never mid
   * forced password change (`AC-125-D`: "no navigation item"), and never for the defensive
   * `Role.Client` / `Role.Subcontractor` fallback `navLabelKeyFor` answers `null` for.
   */
  protected readonly showStaffNav = computed(() => {
    const session = this.session();
    if (!session || session.mustChangePassword) {
      return false;
    }
    return navLabelKeyFor(session) !== null;
  });

  /**
   * KAFF-215 / KAFF-922: the two destination groups, built by {@link navGroupsFor} off `navRowsFor`'s
   * own `group` field. A group with no visible row is dropped before the template sees it, so it
   * renders no heading (`AC-922-B`). The permission filtering lives in `core/navigation/` and is not
   * touched here.
   */
  protected readonly navGroups = computed<readonly NavRowGroup[]>(() => {
    const session = this.session();
    return session ? navGroupsFor(session) : [];
  });

  /** Exposed for the template — `roleKey` builds the i18n key by an exhaustive switch, never by
   *  keying on the raw role string (`enum-keys.ts` hard rule 4). */
  protected readonly roleKey = roleKey;

  /** The language button names the language it switches TO, in that language. */
  protected readonly otherLocale = computed<Locale>(() => (this.i18n.locale() === 'ar' ? 'en' : 'ar'));

  /** The rail's icon tooltip: a fixed-position overlay, so the scrolling nav cannot clip it. */
  protected readonly tip = signal<{ labelKey: string; top: number } | null>(null);

  /** First letter of the display name, Arabic-safe. `[...name]` spreads by code point rather than
   *  UTF-16 code unit, so it does not split a name whose first character is outside the BMP. */
  protected initialFor(displayName: string): string {
    return [...displayName.trim()][0] ?? '';
  }

  constructor() {
    void this.resolver.ensureResolved();

    if (typeof matchMedia === 'function') {
      matchMedia('(max-width: 1100px)').addEventListener('change', (event) => {
        this.narrowDesktop.set(event.matches);
      });
    }

    // Focus moves into the sheet when it opens.
    effect(() => {
      this.sheetClose()?.nativeElement.focus();
    });
  }

  protected async switchLocale(): Promise<void> {
    await this.i18n.use(this.otherLocale());
  }

  protected openNav(): void {
    this.navOpen.set(true);
  }

  /** Closes the sheet and, if it was open, returns focus to the menu button that opened it. */
  protected closeNav(): void {
    if (!this.navOpen()) {
      return;
    }
    this.navOpen.set(false);
    this.menuButton()?.nativeElement.focus();
  }

  /** Keeps Tab inside the open sheet. */
  protected trapTab(event: KeyboardEvent): void {
    if (event.key !== 'Tab') {
      return;
    }
    const focusable = Array.from(
      (event.currentTarget as HTMLElement).querySelectorAll<HTMLElement>('a[href], button:not([disabled])'),
    );
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (!first || !last) {
      return;
    }
    if (event.shiftKey && document.activeElement === first) {
      last.focus();
      event.preventDefault();
    } else if (!event.shiftKey && document.activeElement === last) {
      first.focus();
      event.preventDefault();
    }
  }

  protected showTip(event: Event, labelKey: string): void {
    // Only the sidebar rail has a tooltip; the phone sheet shows its labels.
    const row = event.currentTarget as HTMLElement;
    if (!this.isRail() || !row.closest('.side')) {
      return;
    }
    const rect = row.getBoundingClientRect();
    this.tip.set({ labelKey, top: rect.top + rect.height / 2 });
  }

  protected hideTip(): void {
    this.tip.set(null);
  }

  /**
   * AC-125-E. `SessionResolver.signOut` does the actual work — ends the server session, forgets the
   * profile so completely the shell passes back through `resolving`, and asks again. This only closes
   * the sheet first and lands the caller on `/sign-in` once the round trip settles.
   */
  protected async signOut(): Promise<void> {
    this.closeNav();
    await this.resolver.signOut();
    await this.router.navigateByUrl('/sign-in');
  }
}
