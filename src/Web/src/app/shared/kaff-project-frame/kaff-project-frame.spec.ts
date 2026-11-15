import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { AuthService } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { ProjectDetail, ProjectsApi } from '../../core/projects/projects.api';
import { FrameTab, KaffProjectFrame } from './kaff-project-frame';

const DETAIL: ProjectDetail = {
  id: 'p-1',
  code: 'KF-P-0001',
  name: 'فيلا',
  clientId: 'c-1',
  clientName: 'عميل',
  contractType: 'LumpSum',
  status: 'Active',
  contractValue: '1000.0000',
  holdRate: '0.05',
  advanceRate: '0.1',
  teamSize: 1,
  stage: 'Execution',
  isSigned: true,
  loggedToday: false,
  tabs: { overview: true, boq: true, dailyLog: true, extracts: true, team: true, dailyLogMode: 'write' },
};

async function render(detail: ProjectDetail | Error, current: FrameTab = 'boq') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      {
        provide: ProjectsApi,
        useValue: {
          get: async () => {
            if (detail instanceof Error) {
              throw detail;
            }
            return detail;
          },
        },
      },
      { provide: AuthService, useValue: { current: signal({ permissions: [] }) } },
      { provide: I18nService, useValue: { t: (key: string) => key } },
    ],
  });
  const fixture = TestBed.createComponent(KaffProjectFrame);
  fixture.componentRef.setInput('projectId', 'p-1');
  fixture.componentRef.setInput('current', current);
  fixture.detectChanges();
  for (let i = 0; i < 6; i += 1) {
    await Promise.resolve();
    fixture.detectChanges();
  }
  return fixture.nativeElement as HTMLElement;
}

const hrefs = (root: HTMLElement) =>
  Array.from(root.querySelectorAll<HTMLAnchorElement>('.tab')).map((a) => a.getAttribute('href'));

describe('KaffProjectFrame', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('draws each tab as a link to its screen and marks the current one', async () => {
    const root = await render(DETAIL, 'boq');
    expect(hrefs(root)).toEqual([
      '/projects/p-1',
      '/projects/p-1/boq',
      '/projects/p-1/daily-log',
      '/projects/p-1/extracts',
      '/projects/p-1?tab=team',
    ]);
    expect(root.querySelector('[data-testid="project-tab-boq"]')?.getAttribute('aria-current')).toBe('page');
    expect(root.querySelectorAll('[aria-current="page"]').length).toBe(1);
  });

  it('leaves out a tab the server says is false, rather than greying it', async () => {
    const root = await render({ ...DETAIL, tabs: { ...DETAIL.tabs, boq: false, extracts: false } }, 'overview');
    expect(hrefs(root)).toEqual(['/projects/p-1', '/projects/p-1/daily-log', '/projects/p-1?tab=team']);
  });

  it('draws only the back link and a neutral title when the read fails', async () => {
    const root = await render(new Error('refused'));
    expect(root.querySelector('.tabs')).toBeNull();
    expect(root.querySelector('.stl')).toBeNull();
    expect(root.querySelector('h1')?.textContent).toContain('projects.frame.title_neutral');
  });

  it('draws the three stages only, the current one marked', async () => {
    const root = await render({ ...DETAIL, stage: 'Handover' });
    const stages = Array.from(root.querySelectorAll('.stl li'));
    expect(stages.length).toBe(3);
    expect(stages.map((s) => s.classList.contains('done'))).toEqual([true, true, false]);
    expect(stages[2].getAttribute('aria-current')).toBe('step');
  });
});
