import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { AuthService, Session } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { KaffProjectLinks } from './kaff-project-links';

function session(company: readonly string[], project: readonly string[]): Session {
  return {
    userId: 'u', displayName: 'x', role: 'Owner', departmentId: null, operationsSubDepartment: null,
    mustChangePassword: false, permissions: company, teamProjects: [],
    projects: [{ projectId: 'p-1', name: 'n', code: 'c', accessPath: 'Assignment', level: 'Standard', permissions: project }],
  };
}

function render(current: Session | null, screen: 'extracts' | 'proof' | 'daily-log'): string[] {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { current: signal(current) } },
      { provide: I18nService, useValue: { t: (key: string) => key } },
    ],
  });
  const fixture = TestBed.createComponent(KaffProjectLinks);
  fixture.componentRef.setInput('projectId', 'p-1');
  fixture.componentRef.setInput('current', screen);
  fixture.detectChanges();
  return Array.from(fixture.nativeElement.querySelectorAll('a')).map((a) => (a as HTMLElement).dataset['testid'] ?? '');
}

describe('KaffProjectLinks', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('shows every other permitted screen and never the current one', () => {
    const all = session(['TreasuryReadCompany'], ['ProjectRead', 'DailyLogWrite']);
    expect(render(all, 'proof')).toEqual(['project-link-extracts', 'project-link-daily-log']);
  });

  it('omits a screen the session may not open, and renders nothing when none remain', () => {
    expect(render(session([], ['ProjectRead']), 'proof')).toEqual(['project-link-extracts']);
    TestBed.resetTestingModule();
    expect(render(session([], []), 'extracts')).toEqual([]);
  });

  it('gates proof on the company-wide TreasuryReadCompany, as proofReadGuard does', () => {
    expect(render(session(['TreasuryReadCompany'], []), 'extracts')).toEqual(['project-link-proof']);
  });
});
