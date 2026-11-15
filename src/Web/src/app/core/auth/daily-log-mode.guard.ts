import { inject } from '@angular/core';
import { CanMatchFn, UrlSegment } from '@angular/router';

import { DailyLogMode, ProjectsApi } from '../projects/projects.api';
import { AuthService } from './auth.service';
import { SessionResolver } from './session-resolver';

/**
 * Picks the page behind `/projects/:projectId/daily-log` by the mode the server sent on the project read
 * (`tabs.dailyLogMode`, D-225 item 1): `write` is the entry page, `review` is the owner's read-only page. The
 * guard never looks at a role name. A caller with no mode matches neither route and falls to the refusal route.
 * **Convenience, not the control**: every `…/daily-logs` endpoint checks permission and assignment.
 *
 * Without a session the `write` route matches, so its `sessionGuard` sends the caller to sign in.
 */
export function dailyLogModeMatch(wanted: DailyLogMode): CanMatchFn {
  return async (_route, segments: UrlSegment[]) => {
    const auth = inject(AuthService);
    const projects = inject(ProjectsApi);
    await inject(SessionResolver).ensureResolved();

    if (auth.current() === null) {
      return wanted === 'write';
    }

    // The route is `projects/:projectId/daily-log`: the id is the second segment.
    const projectId = segments[1]?.path;
    if (projectId === undefined) {
      return false;
    }
    try {
      return (await projects.get(projectId)).tabs.dailyLogMode === wanted;
    } catch {
      return false;
    }
  };
}
