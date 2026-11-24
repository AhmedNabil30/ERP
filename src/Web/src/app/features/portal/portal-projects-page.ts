import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { projectWorkspaceStatusKey } from '../../core/i18n/enum-keys';
import { I18nService } from '../../core/i18n/i18n.service';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffState } from '../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../shared/kaff-status-pill/kaff-status-pill';
import { PortalApi, PortalProject } from './portal.api';
import { projectPill } from './portal-format';

/** P2. The client's projects. A client with one project never sees this page: it hands on to that project. */
@Component({
  selector: 'kaff-portal-projects-page',
  imports: [KaffButton, KaffState, KaffStatusPill, RouterLink],
  styleUrl: './portal.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap">
      <h1>{{ i18n.t('portal.projects.title') }}</h1>
      @if (loading()) {
        <kaff-state mode="loading" titleKey="portal.loading" />
      } @else if (failed()) {
        <kaff-state mode="refusal" titleKey="portal.load_failed" testId="portal-failed">
          <kaff-button stateAction variant="secondary" testId="portal-retry" (clicked)="load()">{{ i18n.t('portal.retry') }}</kaff-button>
        </kaff-state>
      } @else if (projects().length === 0) {
        <kaff-state mode="empty" icon="info" titleKey="portal.projects.empty" />
      } @else {
        <ul class="lst" data-testid="portal-projects">
          @for (project of projects(); track project.id) {
            <li>
              <a [routerLink]="['/portal/projects', project.id]">
                <span class="grow"><b>{{ project.name }}</b></span>
                <kaff-status-pill [tone]="pill(project).tone" [icon]="pill(project).icon">{{ i18n.t(statusKey(project)) }}</kaff-status-pill>
                <svg class="i fwd" viewBox="0 0 24 24" aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg>
              </a>
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export class PortalProjectsPage {
  private readonly api = inject(PortalApi);
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18nService);

  protected readonly projects = signal<readonly PortalProject[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  constructor() {
    void this.load();
  }

  protected pill(project: PortalProject) {
    return projectPill(project.status);
  }

  protected statusKey(project: PortalProject): string {
    return projectWorkspaceStatusKey(project.status);
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.failed.set(false);
    try {
      const projects = await this.api.projects();
      if (projects.length === 1) {
        await this.router.navigate(['/portal/projects', projects[0].id], { replaceUrl: true });
        return;
      }
      this.projects.set(projects);
    } catch (error) {
      if (this.api.routeRefusal(error, true)) {
        return;
      }
      this.failed.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
