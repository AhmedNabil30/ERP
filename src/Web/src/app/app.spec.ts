import { TemplateRef, Component, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { App } from './app';
import { AuthService, Session } from './core/auth/auth.service';
import { SessionResolver } from './core/auth/session-resolver';
import { HeaderActionsService } from './core/layout/header-actions.service';

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

function mount(phone: boolean, declare: boolean): HTMLElement {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: query.includes('820') ? phone : false,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
  }));
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideHttpClient(), { provide: SessionResolver, useValue: { ensureResolved: async () => undefined } }],
  });
  TestBed.inject(AuthService).set(SESSION);
  if (declare) {
    const d = TestBed.createComponent(Declarer);
    d.detectChanges();
    TestBed.inject(HeaderActionsService).set(d.componentInstance.tpl());
  }
  const fixture = TestBed.createComponent(App);
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
});
