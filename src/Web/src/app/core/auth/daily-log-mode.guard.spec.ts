import { EnvironmentInjector, runInInjectionContext, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Route, UrlSegment } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { DailyLogMode, ProjectsApi } from '../projects/projects.api';
import { AuthService } from './auth.service';
import { dailyLogModeMatch } from './daily-log-mode.guard';
import { SessionResolver } from './session-resolver';

const SEGMENTS = ['projects', 'p-1', 'daily-log'].map((path) => new UrlSegment(path, {}));

async function run(wanted: DailyLogMode, mode: DailyLogMode | null | 'fails', signedIn = true): Promise<unknown> {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      { provide: AuthService, useValue: { current: signal(signedIn ? { userId: 'u' } : null) } },
      { provide: SessionResolver, useValue: { ensureResolved: async () => undefined } },
      {
        provide: ProjectsApi,
        useValue: {
          get: async () => {
            if (mode === 'fails') {
              throw new Error('refused');
            }
            return { tabs: { dailyLogMode: mode } };
          },
        },
      },
    ],
  });
  return runInInjectionContext(TestBed.inject(EnvironmentInjector), () => dailyLogModeMatch(wanted)({} as Route, SEGMENTS, undefined as never));
}

describe('dailyLogModeMatch', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('sends a write caller to the entry route only', async () => {
    expect(await run('write', 'write')).toBe(true);
    expect(await run('review', 'write')).toBe(false);
  });

  it('sends a review caller to the read-only route only', async () => {
    expect(await run('review', 'review')).toBe(true);
    expect(await run('write', 'review')).toBe(false);
  });

  it('matches neither route when the server sent no mode, or the read fails', async () => {
    expect(await run('write', null)).toBe(false);
    expect(await run('review', null)).toBe(false);
    expect(await run('write', 'fails')).toBe(false);
    expect(await run('review', 'fails')).toBe(false);
  });

  it('lets the sign-in guard take a caller with no session', async () => {
    expect(await run('write', 'write', false)).toBe(true);
    expect(await run('review', 'review', false)).toBe(false);
  });
});
