import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService, ProjectEntry, Session, TeamProjectEntry } from '../../core/auth/auth.service';
import { DepartmentSummary, DepartmentsApi } from '../../core/departments/departments.api';
import {
  assignmentLevelKey,
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
  imports: [RouterLink],
  templateUrl: './landing-page.html',
  styleUrl: './landing-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LandingPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly departmentsApi = inject(DepartmentsApi);

  protected readonly i18n = inject(I18nService);

  /** KAFF-321 — department names are master data now, fetched once rather than an exhaustive switch. */
  private readonly departments = signal<readonly DepartmentSummary[]>([]);

  protected readonly departmentLabel = computed<string | null>(() => {
    const id = this.session()?.departmentId;
    if (id === null || id === undefined) {
      return null;
    }

    const department = this.departments().find((candidate) => candidate.id === id);
    return department ? (this.i18n.locale() === 'en' ? department.nameEn : department.nameAr) : null;
  });

  protected readonly session = computed<Session | null>(() => this.auth.current());

  protected readonly landing = computed<Landing>(() => {
    const session = this.session();
    return session ? landingFor(session) : { kind: 'forbidden' };
  });

  constructor() {
    // MarketingSales lands on S-011 and the Owner on S-006, each a route of its own rather than a
    // branch of this page: each has its own URL, its own guard and its own back-stack behaviour, and
    // a list rendered inside the landing could not be linked to. Redirect rather than duplicate.
    effect(() => {
      const kind = this.landing().kind;

      if (kind === 'clients') {
        void this.router.navigateByUrl('/clients');
      } else if (kind === 'users') {
        void this.router.navigateByUrl('/users');
      } else if (kind === 'catalogue') {
        void this.router.navigateByUrl('/catalogue');
      }
    });

    void this.loadDepartments();
  }

  protected readonly roleKey = roleKey;
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

  private async loadDepartments(): Promise<void> {
    try {
      this.departments.set(await this.departmentsApi.list('all'));
    } catch {
      this.departments.set([]);
    }
  }
}
