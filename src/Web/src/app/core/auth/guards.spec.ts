import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { describe, expect, it } from 'vitest';

import { AuthApi } from './auth.api';
import { Session } from './auth.service';
import { clientManageGuard } from './client-manage.guard';
import { userManageGuard } from './user-manage.guard';

/**
 * `V-33-C`, and the first frontend unit tests in this repository (`V-32-D`).
 *
 * **What the Verifier found, and why an E2E test cannot replace this file.**
 * `await resolver.ensureResolved()` in `clientManageGuard` is D-113 §2's fix for a real user-visible
 * defect — a bookmarked `/clients/new` bounced to `/`, and the landing forwarded to `/clients`, so
 * the operator asked for a form and silently got a list. Deleting that line leaves the E2E suite
 * **11/11 green**, because `app.routes.ts` runs `sessionGuard` first and `sessionGuard` resolves the
 * session before this guard ever reads it. The guard's own defence is therefore redundant *today* and,
 * until this file, unasserted *always*: the day somebody reorders that array, drops `sessionGuard`
 * from the route, or copies the guard onto a route that has no `sessionGuard` in front of it, the
 * defect returns and every suite stays green.
 *
 * **So each guard is run here with nothing in front of it.** That is the only arrangement in which
 * the `await` does any work, and it is exactly the arrangement a future edit could produce by
 * accident. `A_bookmarked_client_form_url_loads_the_form_and_not_the_list` in `tests/E2E.Tests` is the
 * outcome test; this is the mechanism test. The Verifier's judgement was that a test which appears to
 * pin a mechanism and pins only an outcome is worse than an absent one, because it stops anybody
 * looking.
 *
 * **`SessionResolver` and `AuthService` are real, not stubbed.** Stubbing the resolver would test
 * that the guard calls a double — a claim about this file rather than about the mechanism. What is
 * faked is one level lower, `AuthApi.me()`, held open on a promise this test releases by hand, so
 * "the session has not resolved yet" is a state that genuinely exists while the guard runs, exactly
 * as it does on a hard page load.
 */
describe('the permission guards resolve the session themselves', () => {
  const owner: Session = {
    userId: '11111111-1111-1111-1111-111111111111',
    displayName: 'نبيل',
    role: 'Owner',
    department: null,
    operationsSubDepartment: null,
    mustChangePassword: false,
    permissions: [],
    projects: [],
    teamProjects: [],
  };

  const finance: Session = { ...owner, role: 'Finance', department: 'Finance' };

  /**
   * Boots an injector in which `GET /api/auth/me` has been asked for and has **not yet answered** —
   * the state a hard load of a deep URL starts in, and the one `AuthService.current()` reports as
   * `null` for a signed-in user exactly as often as for a signed-out one.
   */
  function pending(session: Session | null): {
    readonly run: (guard: CanActivateFn) => Promise<boolean | UrlTree>;
    readonly answer: () => void;
  } {
    let release!: () => void;
    const answered = new Promise<void>((resolve) => {
      release = resolve;
    });

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthApi,
          useValue: {
            me: async (): Promise<Session> => {
              await answered;

              if (session === null) {
                throw new Error('no session');
              }

              return session;
            },
            signOut: async (): Promise<void> => undefined,
          },
        },
      ],
    });

    const injector = TestBed.inject(EnvironmentInjector);

    return {
      run: (guard) =>
        runInInjectionContext(injector, async () =>
          guard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
        ) as Promise<boolean | UrlTree>,
      answer: release,
    };
  }

  /** `true` stays `true`; a `UrlTree` renders as the path it redirects to. */
  function decided(result: boolean | UrlTree): string {
    return typeof result === 'boolean' ? String(result) : result.toString();
  }

  const guards: readonly (readonly [string, CanActivateFn])[] = [
    ['clientManageGuard', clientManageGuard],
    ['userManageGuard', userManageGuard],
  ];

  for (const [name, guard] of guards) {
    /**
     * The regression this file exists for. Delete `await resolver.ensureResolved()` from the guard
     * under test and this fails: it reads `null` before the fetch answers, concludes "not a holder",
     * and refuses a caller the server would have let through — which is D-113 §2's defect exactly.
     */
    it(`${name} waits for the session before deciding, with no guard in front of it`, async () => {
      const context = pending(owner);

      // The guard is now suspended inside its own await. Nothing else will resolve the session for
      // it: there is no sessionGuard on this route, which is the whole point of the arrangement.
      const decision = context.run(guard);
      context.answer();

      expect(decided(await decision)).toBe('true');
    });

    it(`${name} refuses a role without the permission to /forbidden and not to /`, async () => {
      const context = pending(finance);
      const decision = context.run(guard);
      context.answer();

      // ux/navigation.md: a refusal "must not render as a crash, a blank page, or a redirect that
      // hides what happened". `/` is the third of those, and it is what shipped on 2026-09-04
      // before D-114 §3 replaced it with the /forbidden route.
      expect(decided(await decision)).toBe('/forbidden');
    });
  }

  it('a session fetch that fails refuses visibly rather than silently allowing', async () => {
    const context = pending(null);
    const decision = context.run(userManageGuard);
    context.answer();

    expect(decided(await decision)).toBe('/forbidden');
  });

  /**
   * The positive control for the two refusal assertions above (D-116 §3).
   *
   * `decided()` stringifies a `UrlTree`, so those assertions would also pass against a guard that
   * returned the literal string `'/forbidden'` — which no `CanActivateFn` may do. This proves the
   * shape they are actually reading is a parsed URL, so "the guard redirected" is a fact and not an
   * artefact of the helper.
   */
  it('a refusal is a parsed UrlTree and not a string that happens to look like one', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});

    const tree = TestBed.inject(Router).parseUrl('/forbidden');

    expect(tree).toBeInstanceOf(UrlTree);
    expect(decided(tree)).toBe('/forbidden');
  });
});
