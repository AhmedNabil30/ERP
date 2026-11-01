import { EnvironmentInjector, runInInjectionContext, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, convertToParamMap, provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { AuthService, Session } from './auth.service';
import { boqReadGuard } from './boq-read.guard';
import { SessionResolver } from './session-resolver';

function session(projectId: string, permissions: readonly string[]): Session {
  return {
    userId: 'u', displayName: 'x', role: 'TechnicalOffice', departmentId: null, operationsSubDepartment: null,
    mustChangePassword: false, permissions: [], teamProjects: [],
    projects: [{ projectId, name: 'n', code: 'c', accessPath: 'Assignment', level: 'Standard', permissions }],
  };
}

async function run(current: Session | null, routeProject: string): Promise<string> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: AuthService, useValue: { current: signal(current) } },
      { provide: SessionResolver, useValue: { ensureResolved: async () => undefined } },
    ],
  });
  const injector = TestBed.inject(EnvironmentInjector);
  const route = { paramMap: convertToParamMap({ projectId: routeProject }) } as ActivatedRouteSnapshot;
  const result = await runInInjectionContext(injector, () => boqReadGuard(route, {} as RouterStateSnapshot));
  return typeof result === 'boolean' ? String(result) : (result as UrlTree).toString();
}

describe('boqReadGuard', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('lets a ProjectManage holder on this project in', async () => {
    expect(await run(session('p-1', ['ProjectManage', 'ProjectRead']), 'p-1')).toBe('true');
  });

  it('refuses a caller without ProjectManage, a different project, or no session', async () => {
    expect(await run(session('p-1', ['ProjectRead', 'DailyLogWrite']), 'p-1')).toBe('/forbidden');
    TestBed.resetTestingModule();
    expect(await run(session('p-1', ['ProjectManage']), 'p-2')).toBe('/forbidden');
    TestBed.resetTestingModule();
    expect(await run(null, 'p-1')).toBe('/forbidden');
    expect(TestBed.inject(Router)).toBeTruthy();
  });
});
