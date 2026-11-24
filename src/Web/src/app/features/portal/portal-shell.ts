import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';

import { I18nService } from '../../core/i18n/i18n.service';
import { PortalApi } from './portal.api';

/**
 * The portal's own frame: a green top bar with the wordmark and, once signed in, the sign-out button. No staff
 * shell, no navigation, no language or theme control (the mock draws none). Phone first at every width.
 */
@Component({
  selector: 'kaff-portal-shell',
  imports: [RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      flex: 1;
      min-inline-size: 0;
      min-block-size: 100dvh;
    }

    .top {
      flex: none;
      display: flex;
      align-items: center;
      justify-content: space-between;
      block-size: 56px;
      padding: 0 16px;
      background: var(--g900);
      color: var(--c200);
    }

    .top img {
      display: block;
      inline-size: 88px;
      block-size: auto;
      aspect-ratio: 519 / 155;
    }

    .out {
      inline-size: 44px;
      block-size: 44px;
      display: grid;
      place-items: center;
      border: 0;
      background: transparent;
      color: var(--c200);
      border-radius: var(--radius);
      cursor: pointer;
    }

    .out:hover {
      background: rgb(230 217 209 / 12%);
    }

    .out:focus-visible {
      outline: 3px solid var(--c200);
      outline-offset: 2px;
    }

    .out svg {
      inline-size: 22px;
      block-size: 22px;
      fill: none;
      stroke: currentColor;
      stroke-width: 1.8;
      stroke-linecap: round;
      stroke-linejoin: round;
    }

    main {
      flex: 1;
      display: flex;
      flex-direction: column;
      min-inline-size: 0;
    }
  `,
  template: `
    <header class="top">
      <img src="/brand/kaaf-wordmark.png" width="519" height="155" alt="KAAF" />
      @if (signedIn()) {
        <button type="button" class="out" data-testid="portal-sign-out" [attr.aria-label]="i18n.t('portal.sign_out')" (click)="signOut()">
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 4H5v16h4M16 8l4 4-4 4M20 12H9" /></svg>
        </button>
      }
    </header>
    <main><router-outlet /></main>
  `,
})
export class PortalShell {
  private readonly api = inject(PortalApi);
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18nService);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** The sign-in door is the only screen with no session behind it. */
  protected readonly signedIn = computed(() => !this.url().startsWith('/portal/sign-in'));

  protected async signOut(): Promise<void> {
    try {
      await this.api.signOut();
    } finally {
      await this.router.navigateByUrl('/portal/sign-in');
    }
  }
}
