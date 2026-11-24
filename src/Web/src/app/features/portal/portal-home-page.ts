import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { projectWorkspaceStatusKey } from '../../core/i18n/enum-keys';
import { I18nService } from '../../core/i18n/i18n.service';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffState } from '../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../shared/kaff-status-pill/kaff-status-pill';
import { PortalApi, PortalChangeOrder, PortalHome } from './portal.api';
import { changeOrderPill, percentOf, portalDate, projectPill } from './portal-format';

/**
 * P3. Where the project stands, for a client: what waits on them first, then progress, what is paid and what remains.
 * "Waiting on you" is the change orders the client can decide, matching the server's `pendingCount`. An unpaid extract is not
 * waiting on the client (D-273 #11, status only): it keeps its unpaid pill in the extracts list. With exactly one such change order
 * the bottom bar carries the one filled action; with more, the rows act.
 */
@Component({
  selector: 'kaff-portal-home-page',
  imports: [KaffButton, KaffState, KaffStatusPill, RouterLink],
  styleUrl: './portal.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap" [class.has-bar]="barOrder() !== null">
      <a class="back" routerLink="/portal" data-testid="portal-back">
        <svg class="i s flip" viewBox="0 0 24 24" aria-hidden="true"><path d="M19 12H5M11 6l-6 6 6 6" /></svg>
        {{ i18n.t('portal.projects.title') }}
      </a>

      @if (loading()) {
        <kaff-state mode="loading" titleKey="portal.loading" />
      } @else if (failed()) {
        <kaff-state mode="refusal" titleKey="portal.load_failed" testId="portal-failed">
          <kaff-button stateAction variant="secondary" testId="portal-retry" (clicked)="load(projectId())">{{ i18n.t('portal.retry') }}</kaff-button>
        </kaff-state>
      } @else if (home(); as home) {
        <h1>{{ home.name }}</h1>
        <div>
          <kaff-status-pill [tone]="pill().tone" [icon]="pill().icon">{{ i18n.t(statusKey()) }}</kaff-status-pill>
        </div>

        @if (pendingCount() > 0) {
          <section class="sec" data-testid="portal-pending">
            <h2>{{ i18n.t('portal.home.pending') }}</h2>
            <ul class="lst pend">
              @for (order of pendingOrders(); track order.id) {
                <li>
                  <a [routerLink]="['/portal/projects', projectId(), 'change-orders', order.id]">
                    <span class="grow">
                      <b>{{ order.title }}</b>
                      <small>{{ i18n.t('portal.home.change_order') }}</small>
                    </span>
                    <span class="fig">{{ i18n.formatMoney(order.total) }}</span>
                    <svg class="i fwd" viewBox="0 0 24 24" aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg>
                  </a>
                </li>
              }
            </ul>
          </section>
        }

        @if (home.sections.length > 0) {
          <section class="sec" data-testid="portal-progress">
            <h2>{{ i18n.t('portal.home.progress') }}</h2>
            <ul class="lst">
              @for (section of home.sections; track section.nameEn) {
                <li>
                  <div class="it" style="display: grid; gap: 8px; padding-block: 12px">
                    <div class="ln">
                      <b>{{ i18n.locale() === 'ar' ? section.nameAr : section.nameEn }}</b>
                      <span class="fig">{{ percent(section.completion) }}</span>
                    </div>
                    <div class="meter" aria-hidden="true"><i [style.inline-size.%]="wholePercent(section.completion)"></i></div>
                  </div>
                </li>
              }
            </ul>
          </section>
        }

        <section class="sec" data-testid="portal-money">
          <h2>{{ i18n.t('portal.home.money') }}</h2>
          <div class="card">
            @if (home.contractValue; as contract) {
              <div class="meter" aria-hidden="true"><i [style.inline-size.%]="paidShare(home.paid, contract)"></i></div>
            }
            <div class="kv" style="padding-block: 0; border: 0; min-block-size: 0">
              <span>{{ i18n.t('portal.home.paid') }}</span>
              <span class="fig" data-testid="portal-paid">{{ i18n.formatMoney(home.paid) }}</span>
            </div>
            @if (home.remaining; as remaining) {
              <div class="kv" style="padding-block: 0; border: 0; min-block-size: 0">
                <span>{{ i18n.t('portal.home.remaining') }}</span>
                <span class="fig" data-testid="portal-remaining">{{ i18n.formatMoney(remaining) }}</span>
              </div>
            }
            @if (home.contractValue; as contract) {
              <div class="kv" style="padding-block: 0; border: 0; min-block-size: 0">
                <span>{{ i18n.t('portal.home.contract') }}</span>
                <span class="fig">{{ i18n.formatMoney(contract) }}</span>
              </div>
            }
          </div>
        </section>

        @if (otherOrders().length > 0) {
          <section class="sec" data-testid="portal-orders">
            <h2>{{ i18n.t('portal.home.change_orders') }}</h2>
            <ul class="lst">
              @for (order of otherOrders(); track order.id) {
                <li>
                  <a [routerLink]="['/portal/projects', projectId(), 'change-orders', order.id]">
                    <span class="grow"><b>{{ order.title }}</b></span>
                    <kaff-status-pill [tone]="orderPill(order).tone" [icon]="orderPill(order).icon">{{ i18n.t(orderPill(order).key) }}</kaff-status-pill>
                    <svg class="i fwd" viewBox="0 0 24 24" aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg>
                  </a>
                </li>
              }
            </ul>
          </section>
        }

        @if (home.plannedStart || home.plannedEnd) {
          <section class="sec" data-testid="portal-plan">
            <h2>{{ i18n.t('portal.home.plan') }}</h2>
            <div class="kvs">
              @if (home.plannedStart; as start) {
                <div class="kv">
                  <span>{{ i18n.t('portal.home.start') }}</span>
                  <span class="fig">{{ date(start) }}</span>
                </div>
              }
              @if (home.plannedEnd; as end) {
                <div class="kv">
                  <span>{{ i18n.t('portal.home.planned_end') }}</span>
                  <span class="fig">{{ date(end) }}</span>
                </div>
              }
              @if (home.isStalled) {
                <div class="kv">
                  <span></span>
                  <kaff-status-pill tone="stuck" icon="alert" testId="portal-stalled">{{ i18n.t('status.kaff.troubled') }}</kaff-status-pill>
                </div>
              }
            </div>
          </section>
        }

        <ul class="lst">
          <li>
            <a [routerLink]="['/portal/projects', projectId(), 'extracts']" data-testid="portal-link-extracts">
              <svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="M7 3h8l4 4v14H7zM15 3v4h4M10 12h6M10 16h6" /></svg>
              <span class="grow"><b>{{ i18n.t('portal.extracts.title') }}</b></span>
              <svg class="i fwd" viewBox="0 0 24 24" aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg>
            </a>
          </li>
          <li>
            <a [routerLink]="['/portal/projects', projectId(), 'photos']" data-testid="portal-link-photos">
              <svg class="i" viewBox="0 0 24 24" aria-hidden="true"><path d="M3 5h18v14H3zM3 16l5-5 4 4 3-3 6 6M8.5 9.5h.01" /></svg>
              <span class="grow"><b>{{ i18n.t('portal.photos.title') }}</b></span>
              <svg class="i fwd" viewBox="0 0 24 24" aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg>
            </a>
          </li>
        </ul>
      }
    </div>

    @if (barOrder(); as order) {
      <div class="abar">
        <div>
          <kaff-button class="fill" variant="primary" testId="portal-bar-action" (clicked)="review(order.id)">{{ i18n.t('portal.home.review_change_order') }}</kaff-button>
        </div>
      </div>
    }
  `,
})
export class PortalHomePage {
  readonly projectId = input.required<string>();

  private readonly api = inject(PortalApi);
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18nService);

  protected readonly home = signal<PortalHome | null>(null);
  protected readonly orders = signal<readonly PortalChangeOrder[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  protected readonly pill = computed(() => projectPill(this.home()?.status ?? 'Setup'));
  protected readonly statusKey = computed(() => projectWorkspaceStatusKey(this.home()?.status ?? 'Setup'));
  protected readonly pendingOrders = computed(() => this.orders().filter((order) => order.canDecide));
  protected readonly otherOrders = computed(() => this.orders().filter((order) => !order.canDecide));
  protected readonly pendingCount = computed(() => this.pendingOrders().length);

  /** Exactly one waiting item and it is a change order: the bar carries the one action. Otherwise the rows act. */
  protected readonly barOrder = computed(() =>
    this.pendingOrders().length === 1 ? this.pendingOrders()[0] : null,
  );

  constructor() {
    // A required input is not readable in the constructor (NG0950).
    effect(() => {
      const id = this.projectId();
      untracked(() => void this.load(id));
    });
  }

  protected review(orderId: string): void {
    void this.router.navigate(['/portal/projects', this.projectId(), 'change-orders', orderId]);
  }

  protected orderPill(order: PortalChangeOrder) {
    return changeOrderPill(order.status);
  }

  protected date(value: string): string {
    return portalDate(this.i18n, value);
  }

  protected percent(completion: string): string {
    return `${this.i18n.formatNumber(completion, { minimumFractionDigits: 0, maximumFractionDigits: 2 })}%`;
  }

  protected wholePercent(completion: string): number {
    return Math.max(0, Math.min(100, Math.round(Number(completion))));
  }

  protected paidShare(paid: string, contract: string): number {
    return percentOf(paid, contract);
  }

  protected async load(projectId: string): Promise<void> {
    this.loading.set(true);
    this.failed.set(false);
    try {
      const [home, orders] = await Promise.all([
        this.api.home(projectId),
        // The list only adds rows to the page; a failure there leaves the rest readable.
        this.api.changeOrders(projectId).catch(() => []),
      ]);
      this.home.set(home);
      this.orders.set(orders);
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
