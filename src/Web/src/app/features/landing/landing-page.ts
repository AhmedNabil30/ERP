import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { Router } from '@angular/router';

import { AuthService, ProjectEntry, Session, TeamProjectEntry } from '../../core/auth/auth.service';
import {
  assignmentLevelKey,
  departmentKey,
  operationsSubDepartmentKey,
  projectAccessPathKey,
  roleKey,
} from '../../core/i18n/enum-keys';
import { I18nService } from '../../core/i18n/i18n.service';
import { Landing, landingFor } from '../../core/navigation/landing';

/**
 * D-100's ruling, applied: `[RefCode] Project Name`. The payload deliberately does not concatenate
 * this (decisions.md D-103) — that was left to the rendering story, and this is that story.
 */
function refCodeAndName(code: string, name: string): string {
  return `[${code}] ${name}`;
}

@Component({
  selector: 'kaff-landing-page',
  templateUrl: './landing-page.html',
  styleUrl: './landing-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LandingPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly i18n = inject(I18nService);

  protected readonly session = computed<Session | null>(() => this.auth.current());

  protected readonly landing = computed<Landing>(() => {
    const role = this.session()?.role;
    return role ? landingFor(role) : { kind: 'forbidden' };
  });

  constructor() {
    // MarketingSales lands on S-011, which is a route of its own rather than a branch of this page:
    // it has its own URL, its own guard and its own back-stack behaviour, and a list rendered inside
    // the landing could not be linked to. Redirect rather than duplicate.
    effect(() => {
      if (this.landing().kind === 'clients') {
        void this.router.navigateByUrl('/clients');
      }
    });
  }

  /**
   * Read out as its own signal, rather than accessed as `landing().titleKey` in the template, so the
   * `@case ('pending')` branch needs no narrowing the template type-checker cannot perform on a
   * `@switch` the way `@if (x; as y)` narrows an `@if`.
   */
  protected readonly landingPendingTitle = computed(() => {
    const current = this.landing();
    return current.kind === 'pending' ? current.titleKey : '';
  });

  protected readonly roleKey = roleKey;
  protected readonly departmentKey = departmentKey;
  protected readonly operationsSubDepartmentKey = operationsSubDepartmentKey;
  protected readonly assignmentLevelKey = assignmentLevelKey;
  protected readonly projectAccessPathKey = projectAccessPathKey;
  protected readonly refCodeAndName = refCodeAndName;

  protected trackProject(_index: number, project: ProjectEntry): string {
    return project.projectId;
  }

  protected trackTeamProject(_index: number, project: TeamProjectEntry): string {
    return project.code;
  }
}
