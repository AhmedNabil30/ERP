import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { AuthService, Session } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { LandingPage } from './landing-page';

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return key === 'nav.suppliers' ? 'الموردون' : key;
  }
}

function session(permissions: readonly string[]): Session {
  return {
    userId: '11111111-1111-1111-1111-111111111111',
    displayName: 'اختبار',
    role: 'Finance',
    departmentId: null,
    operationsSubDepartment: null,
    mustChangePassword: false,
    permissions,
    projects: [],
    teamProjects: [],
  };
}

class FakeAuthService implements Pick<AuthService, 'current'> {
  readonly current;
  constructor(value: Session | null) {
    this.current = signal(value);
  }
}

/**
 * `V-38-A`/`V-38-D`: `nav.suppliers` was in both catalogues with no template reading it. Finance's own
 * landing today is S-005 (this profile screen — nothing Finance-specific exists yet in this slice), so
 * the link lives here, gated on the `SupplierManage` permission per KAFF-125 rule 6 — not on the role.
 */
describe('LandingPage · suppliers link', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  async function createFixture(session: Session) {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: AuthService, useValue: new FakeAuthService(session) },
        { provide: I18nService, useClass: FakeI18nService },
      ],
    });

    const fixture = TestBed.createComponent(LandingPage);
    fixture.detectChanges();
    await Promise.resolve();
    fixture.detectChanges();
    return fixture;
  }

  it('shows a translated link to /suppliers for a caller holding SupplierManage', async () => {
    const fixture = await createFixture(session(['SupplierManage']));

    const link: HTMLAnchorElement = fixture.nativeElement.querySelector(
      '[data-testid="profile-manage-suppliers"]',
    );
    expect(link).toBeTruthy();
    expect(link.textContent?.trim()).toBe('الموردون');
  });

  it('hides the link from a caller who does not hold SupplierManage', async () => {
    const fixture = await createFixture(session([]));

    expect(fixture.nativeElement.querySelector('[data-testid="profile-manage-suppliers"]')).toBeNull();
  });
});
