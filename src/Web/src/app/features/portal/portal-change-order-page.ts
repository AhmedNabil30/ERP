import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffBanner } from '../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffField } from '../../shared/kaff-field/kaff-field';
import { KaffState } from '../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../shared/kaff-status-pill/kaff-status-pill';
import { PortalApi, PortalChangeOrder } from './portal.api';
import { changeOrderPill } from './portal-format';

type Step = 'view' | 'confirm' | 'refuse';

/**
 * P5. One change order for the client to approve or refuse. Approve restates the total in place before it sends;
 * refuse asks for the reason, which goes to Kaff. The decision is the server's: this page offers it only when the
 * list says `canDecide`. There is no single-order endpoint, so the order is picked from the project's list.
 */
@Component({
  selector: 'kaff-portal-change-order-page',
  imports: [KaffBanner, KaffButton, KaffField, KaffState, KaffStatusPill, RouterLink],
  styleUrl: './portal.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap" [class.has-bar]="showBar()">
      <a class="back" [routerLink]="['/portal/projects', projectId()]" data-testid="portal-back">
        <svg class="i s flip" viewBox="0 0 24 24" aria-hidden="true"><path d="M19 12H5M11 6l-6 6 6 6" /></svg>
        {{ i18n.t('portal.back') }}
      </a>

      @if (loading()) {
        <kaff-state mode="loading" titleKey="portal.loading" />
      } @else if (failed()) {
        <kaff-state mode="refusal" titleKey="portal.load_failed" testId="portal-failed">
          <kaff-button stateAction variant="secondary" testId="portal-retry" (clicked)="load(projectId())">{{ i18n.t('portal.retry') }}</kaff-button>
        </kaff-state>
      } @else if (order(); as order) {
        <h1>{{ order.title }}</h1>
        @if (!order.canDecide) {
          <div>
            <kaff-status-pill [tone]="pill().tone" [icon]="pill().icon" testId="portal-co-status">{{ i18n.t(pill().key) }}</kaff-status-pill>
          </div>
        }

        @if (decided()) {
          <kaff-banner tone="ok" icon="check" testId="portal-co-decided">{{ i18n.t(pill().key) }}</kaff-banner>
        }
        @if (actionFailed()) {
          <kaff-banner tone="refusal" icon="alert" testId="portal-co-failed">{{ i18n.t(failedKey()) }}</kaff-banner>
        }

        <section class="sec">
          <h2>{{ i18n.t('portal.co.lines') }}</h2>
          <ul class="lst">
            @for (line of order.lines; track line.lineNumber) {
              <li>
                <div class="it" style="display: grid; gap: 4px; padding-block: 10px">
                  <div class="ln">
                    <b style="font-size: 1rem" dir="auto">{{ line.description }}</b>
                    <span class="fig">{{ i18n.formatAmount(line.total) }}</span>
                  </div>
                  <small>{{ i18n.formatNumber(line.quantity) }} {{ line.unit }} × {{ i18n.formatAmount(line.unitPrice) }}</small>
                </div>
              </li>
            }
          </ul>
        </section>

        <div class="kvs">
          <div class="kv t">
            <span>{{ i18n.t('portal.co.total') }}</span>
            <span class="fig" data-testid="portal-co-total">{{ i18n.formatMoney(order.total) }}</span>
          </div>
        </div>

        @if (order.canDecide) {
          @switch (step()) {
            @case ('confirm') {
              <div class="cfm" role="group" data-testid="portal-co-confirm">
                <b>{{ i18n.t('portal.co.confirm_title') }}</b>
                <div class="kv" style="padding: 0; border: 0; min-block-size: 0">
                  <span>{{ i18n.t('portal.co.total') }}</span>
                  <span class="fig">{{ i18n.formatMoney(order.total) }}</span>
                </div>
                <div class="row2">
                  <kaff-button class="fill" variant="primary" testId="portal-co-confirm-approve" [busy]="busy()" (clicked)="approve()">
                    {{ i18n.t(busy() ? 'portal.co.sending' : 'portal.co.confirm') }}
                  </kaff-button>
                  <kaff-button class="fill" variant="secondary" testId="portal-co-back" [disabled]="busy()" (clicked)="step.set('view')">{{ i18n.t('portal.co.back') }}</kaff-button>
                </div>
              </div>
            }
            @case ('refuse') {
              <div class="cfm" role="group" data-testid="portal-co-refuse">
                <kaff-field labelKey="portal.co.reason" controlId="portal-co-reason">
                  <div class="ctl ta">
                    <textarea id="portal-co-reason" data-testid="portal-co-reason" [value]="reason()" (input)="onReason($event)"></textarea>
                  </div>
                </kaff-field>
                <div class="row2">
                  <kaff-button class="fill" variant="secondary" testId="portal-co-confirm-reject" [busy]="busy()" [disabled]="reason().trim().length === 0" (clicked)="reject()">
                    {{ i18n.t(busy() ? 'portal.co.sending' : 'portal.co.confirm_reject') }}
                  </kaff-button>
                  <kaff-button class="fill" variant="ghost" testId="portal-co-back" [disabled]="busy()" (clicked)="step.set('view')">{{ i18n.t('portal.co.back') }}</kaff-button>
                </div>
              </div>
            }
            @default {
              <kaff-button class="fill" variant="secondary" testId="portal-co-reject" (clicked)="step.set('refuse')">{{ i18n.t('portal.co.reject') }}</kaff-button>
            }
          }
        }
      } @else {
        <kaff-state mode="empty" icon="info" titleKey="portal.co.not_found" testId="portal-co-not-found" />
      }
    </div>

    @if (showBar()) {
      <div class="abar">
        <div>
          <kaff-button class="fill" variant="primary" testId="portal-co-approve" (clicked)="step.set('confirm')">{{ i18n.t('portal.co.approve') }}</kaff-button>
        </div>
      </div>
    }
  `,
})
export class PortalChangeOrderPage {
  readonly projectId = input.required<string>();
  readonly orderId = input.required<string>();

  private readonly api = inject(PortalApi);
  protected readonly i18n = inject(I18nService);

  protected readonly order = signal<PortalChangeOrder | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly step = signal<Step>('view');
  protected readonly reason = signal('');
  protected readonly busy = signal(false);
  protected readonly actionFailed = signal(false);
  protected readonly decided = signal(false);
  private readonly lastAction = signal<'approve' | 'reject'>('approve');

  protected readonly pill = computed(() => changeOrderPill(this.order()?.status ?? ''));
  protected readonly failedKey = computed(() => (this.lastAction() === 'approve' ? 'portal.co.approve_failed' : 'portal.co.reject_failed'));
  /** The one filled action sits in the bar only while the choice itself is open (not while a confirm group is). */
  protected readonly showBar = computed(() => this.order()?.canDecide === true && this.step() === 'view');

  constructor() {
    effect(() => {
      const projectId = this.projectId();
      const orderId = this.orderId();
      untracked(() => void this.load(projectId, orderId));
    });
  }

  protected onReason(event: Event): void {
    this.reason.set((event.target as HTMLTextAreaElement).value);
  }

  protected async load(projectId: string, orderId = this.orderId()): Promise<void> {
    this.loading.set(true);
    this.failed.set(false);
    try {
      const orders = await this.api.changeOrders(projectId);
      this.order.set(orders.find((candidate) => candidate.id === orderId) ?? null);
    } catch (error) {
      if (this.api.routeRefusal(error)) {
        return;
      }
      this.failed.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  protected async approve(): Promise<void> {
    await this.decide('approve', () => this.api.approve(this.projectId(), this.orderId()));
  }

  protected async reject(): Promise<void> {
    const reason = this.reason().trim();
    await this.decide('reject', () => this.api.reject(this.projectId(), this.orderId(), reason));
  }

  private async decide(kind: 'approve' | 'reject', send: () => Promise<PortalChangeOrder>): Promise<void> {
    this.lastAction.set(kind);
    this.busy.set(true);
    this.actionFailed.set(false);
    try {
      this.order.set(await send());
      this.decided.set(true);
      this.step.set('view');
    } catch (error) {
      if (this.api.routeRefusal(error)) {
        return;
      }
      // A refused decision leaves the order as it was, so the client can try again.
      this.actionFailed.set(true);
      this.step.set('view');
    } finally {
      this.busy.set(false);
    }
  }
}
