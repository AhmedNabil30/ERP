import { TemplateRef, Component, signal, viewChild } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import { App } from './app';
import { AuthService, Session } from './core/auth/auth.service';
import { SessionResolver } from './core/auth/session-resolver';
import { HeaderActionsService } from './core/layout/header-actions.service';
import { KaffConfirmDialog } from './shared/kaff-confirm-dialog/kaff-confirm-dialog';
import { KaffDetailPanel } from './shared/kaff-detail-panel/kaff-detail-panel';

/** KAFF-936 / S-3: where the shell draws a screen's declared primary action. */
@Component({ template: `<ng-template #t><button data-testid="declared">go</button></ng-template>` })
class Declarer {
  readonly tpl = viewChild.required<TemplateRef<unknown>>('t');
}

const SESSION: Session = {
  userId: 'u',
  displayName: 'Test',
  role: 'Owner',
  departmentId: null,
  operationsSubDepartment: null,
  mustChangePassword: false,
  permissions: [],
  projects: [],
  teamProjects: [],
};

/** An overlay a screen could open over the bar. */
@Component({
  imports: [KaffDetailPanel, KaffConfirmDialog],
  template: `
    <kaff-detail-panel [open]="panel()" title="t" />
    <kaff-confirm-dialog [open]="confirm()" title="t" confirmLabel="y" cancelLabel="n" />
  `,
})
class Overlays {
  readonly panel = signal(false);
  readonly confirm = signal(false);
}

let mounted: ComponentFixture<App>;

function mount(phone: boolean, declare: boolean, session: Session = SESSION): HTMLElement {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: query.includes('820') ? phone : false,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
  }));
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideHttpClient(), { provide: SessionResolver, useValue: { ensureResolved: async () => undefined } }],
  });
  TestBed.inject(AuthService).set(session);
  if (declare) {
    const d = TestBed.createComponent(Declarer);
    d.detectChanges();
    TestBed.inject(HeaderActionsService).set(d.componentInstance.tpl(), 'primary');
  }
  const fixture = TestBed.createComponent(App);
  mounted = fixture;
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('App shell primary-action slot (KAFF-936)', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('on a phone the declared action is in the bottom bar beside the menu, not at the top', () => {
    const el = mount(true, true);
    expect(el.querySelector('.abar [data-testid="declared"]')).not.toBeNull();
    expect(el.querySelector('.abar [data-testid="nav-toggle"]')).not.toBeNull();
    expect(el.querySelector('.page-actions')).toBeNull();
    expect(el.querySelectorAll('[data-testid="declared"]').length).toBe(1);
  });

  it('at desktop width the declared action is in the page header and not in the bar', () => {
    const el = mount(false, true);
    expect(el.querySelector('.page-actions [data-testid="declared"]')).not.toBeNull();
    expect(el.querySelector('.abar [data-testid="declared"]')).toBeNull();
  });

  it('on a phone with no declared action the bar holds the menu alone', () => {
    const el = mount(true, false);
    expect(el.querySelector('.abar [data-testid="nav-toggle"]')).not.toBeNull();
    expect(el.querySelector('.abar-action')).toBeNull();
    expect(el.querySelector('.page-actions')).toBeNull();
  });

  it('AC-936-D: with no declared action the menu fills the bar', () => {
    const el = mount(true, false);
    const menu = el.querySelector<HTMLElement>('.abar [data-testid="nav-toggle"]')!;
    expect(getComputedStyle(menu).flexGrow).toBe('1');
  });

  it('AC-936-D: with a declared action the menu does not grow, the action fills the rest', () => {
    const el = mount(true, true);
    const menu = el.querySelector<HTMLElement>('.abar [data-testid="nav-toggle"]')!;
    expect(getComputedStyle(menu).flexGrow).not.toBe('1');
  });

  it('AC-936-E: an action the session may not use is absent, and the menu fills the bar', () => {
    // The screen declares nothing for a session that may not act: `clear()`, never a disabled button.
    const el = mount(true, true);
    expect(el.querySelector('[data-testid="declared"]')).not.toBeNull();
    TestBed.inject(HeaderActionsService).clear();
    mounted.detectChanges();
    expect(el.querySelector('[data-testid="declared"]')).toBeNull();
    expect(el.querySelector('.abar-action')).toBeNull();
    expect(getComputedStyle(el.querySelector<HTMLElement>('[data-testid="nav-toggle"]')!).flexGrow).toBe('1');
  });

  describe('AC-936-H: an open overlay hides the bar', () => {
    beforeAll(() => {
      // jsdom has no <dialog> modal.
      HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
        this.setAttribute('open', '');
      };
      HTMLDialogElement.prototype.close ??= function close(this: HTMLDialogElement) {
        this.removeAttribute('open');
      };
    });

    function hidden(app: HTMLElement): boolean {
      return app.querySelector('.app')!.getAttribute('data-overlay') === '1';
    }

    it('the bar is shown with no overlay, hidden under the detail panel and under the confirm dialog', () => {
      const el = mount(true, true);
      const app = TestBed.createComponent(App);
      const overlays = TestBed.createComponent(Overlays);
      app.detectChanges();
      overlays.detectChanges();
      const root = app.nativeElement as HTMLElement;
      expect(hidden(root)).toBe(false);

      overlays.componentInstance.panel.set(true);
      overlays.detectChanges();
      app.detectChanges();
      expect(hidden(root)).toBe(true);

      overlays.componentInstance.panel.set(false);
      overlays.componentInstance.confirm.set(true);
      overlays.detectChanges();
      app.detectChanges();
      expect(hidden(root)).toBe(true);

      overlays.componentInstance.confirm.set(false);
      overlays.detectChanges();
      app.detectChanges();
      expect(hidden(root)).toBe(false);
      expect(el).toBeTruthy();
    });
  });
});

describe('App shell account block (KAFF-922)', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('AC-922-C / AC-125-D: during a forced password change the account block and sign-out show, the nav does not', () => {
    const el = mount(false, false, { ...SESSION, mustChangePassword: true });
    expect(el.querySelector('[data-testid="account-menu"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="account-name"]')?.textContent).toContain('Test');
    expect(el.querySelector('[data-testid="sign-out"]')).not.toBeNull();
    expect(el.querySelector('nav.nav')).toBeNull();
  });
});
