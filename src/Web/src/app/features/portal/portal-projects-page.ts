import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { projectWorkspaceStatusKey } from '../../core/i18n/enum-keys';
import { I18nService } from '../../core/i18n/i18n.service';
import { KaffBadge } from '../../shared/kaff-badge/kaff-badge';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffState } from '../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../shared/kaff-status-pill/kaff-status-pill';
import { PortalApi, PortalProject } from './portal.api';
import { percentOf, projectPill } from './portal-format';

/** P2. The client's projects. A client with one project never sees this page: it hands on to that project. */
@Component({
  selector: 'kaff-portal-projects-page',
  imports: [KaffBadge, KaffButton, KaffState, KaffStatusPill, RouterLink],
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
        <div class="sec" data-testid="portal-projects">
          @for (project of projects(); track project.id) {
            <a class="card" [class.pend]="project.pendingCount > 0" [routerLink]="['/portal/projects', project.id]" data-testid="portal-project">
              <div class="ln">
                <b>{{ project.name }}</b>
                @if (project.pendingCount > 0) {
                  <kaff-badge tone="brand" testId="portal-pending-badge">{{ i18n.t('portal.projects.pending', { count: project.pendingCount }) }}</kaff-badge>
                }
              </div>
              <div>
                <kaff-status-pill [tone]="pill(project).tone" [icon]="pill(project).icon">{{ i18n.t(statusKey(project)) }}</kaff-status-pill>
              </div>
              @if (project.remaining; as remaining) {
                <div class="meter" aria-hidden="true"><i [style.inline-size.%]="share(project, remaining)"></i></div>
              }
              <div class="ln dim">
                <span>{{ i18n.t('portal.home.paid') }} <span class="fig">{{ i18n.formatAmount(project.paid) }}</span></span>
                @if (project.remaining; as remaining) {
                  <span>{{ i18n.t('portal.home.remaining') }} <span class="fig">{{ i18n.formatMoney(remaining) }}</span></span>
                }
              </div>
            </a>
          }
        </div>
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

  /** Paid over paid plus remaining: the bar only, drawn from whole piastres. */
  protected share(project: PortalProject, remaining: string): number {
    const sum = (BigInt(project.paid.replace('.', '')) + BigInt(remaining.replace('.', ''))).toString().padStart(3, '0');
    return percentOf(project.paid, `${sum.slice(0, -2)}.${sum.slice(-2)}`);
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
