import { ChangeDetectionStrategy, Component, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffState } from '../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../shared/kaff-status-pill/kaff-status-pill';
import { PortalApi, PortalExtract } from './portal.api';
import { portalDate } from './portal-format';

/**
 * P4. The extracts, newest first. Per extract: certified work, the net due, and the payment status (D-273 #11: status
 * only, no next step). The API sends no deductions line and no this-period figure, so neither is drawn.
 */
@Component({
  selector: 'kaff-portal-extracts-page',
  imports: [KaffButton, KaffState, KaffStatusPill, RouterLink],
  styleUrl: './portal.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap">
      <a class="back" [routerLink]="['/portal/projects', projectId()]" data-testid="portal-back">
        <svg class="i s flip" viewBox="0 0 24 24" aria-hidden="true"><path d="M19 12H5M11 6l-6 6 6 6" /></svg>
        {{ i18n.t('portal.back') }}
      </a>
      <h1>{{ i18n.t('portal.extracts.title') }}</h1>

      @if (loading()) {
        <kaff-state mode="loading" titleKey="portal.loading" />
      } @else if (failed()) {
        <kaff-state mode="refusal" titleKey="portal.load_failed" testId="portal-failed">
          <kaff-button stateAction variant="secondary" testId="portal-retry" (clicked)="load(projectId())">{{ i18n.t('portal.retry') }}</kaff-button>
        </kaff-state>
      } @else if (extracts().length === 0) {
        <kaff-state mode="empty" icon="file" titleKey="portal.extracts.empty" />
      } @else {
        <ul class="sec" style="list-style: none; margin: 0; padding: 0" data-testid="portal-extracts">
          @for (extract of extracts(); track extract.id) {
            <li class="kvs">
              <div class="kv t">
                <span>{{ i18n.t('portal.extracts.item', { number: extract.number }) }}</span>
                @if (extract.isPaid) {
                  <kaff-status-pill tone="done" icon="check">{{ i18n.t('portal.extracts.paid') }}</kaff-status-pill>
                } @else {
                  <kaff-status-pill tone="none" icon="clock">{{ i18n.t('portal.extracts.unpaid') }}</kaff-status-pill>
                }
              </div>
              <div class="kv">
                <span>{{ i18n.t('portal.extracts.period') }}</span>
                <span class="fig">{{ date(extract.periodStart) }} - {{ date(extract.periodEnd) }}</span>
              </div>
              <div class="kv">
                <span>{{ i18n.t('portal.extracts.work') }}</span>
                <span class="fig">{{ i18n.formatMoney(extract.certifiedWork) }}</span>
              </div>
              <div class="kv t">
                <span>{{ i18n.t('portal.extracts.net') }}</span>
                <span class="fig">{{ i18n.formatMoney(extract.netPayable) }}</span>
              </div>
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export class PortalExtractsPage {
  readonly projectId = input.required<string>();

  private readonly api = inject(PortalApi);
  protected readonly i18n = inject(I18nService);

  protected readonly extracts = signal<readonly PortalExtract[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  constructor() {
    effect(() => {
      const id = this.projectId();
      untracked(() => void this.load(id));
    });
  }

  protected date(value: string): string {
    return portalDate(this.i18n, value);
  }

  protected async load(projectId: string): Promise<void> {
    this.loading.set(true);
    this.failed.set(false);
    try {
      const list = await this.api.extracts(projectId);
      this.extracts.set([...list].sort((a, b) => b.number - a.number));
    } catch (error) {
      if (this.api.routeRefusal(error)) {
        return;
      }
      this.failed.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
