import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffFigure } from '../kaff-figure/kaff-figure';

/**
 * The collection position (README "Extract chain", kit item 5): five figures, المستحق, المستلم, المخصوم من المنبع,
 * في شيكات, المتبقي, with المتبقي in the head fill. Five across on desktop; two by two on a phone with المتبقي full
 * width. Every value is the server's wire string (D-135); this part adds nothing up.
 */
@Component({
  selector: 'kaff-collection-position',
  imports: [KaffFigure],
  template: `<dl class="pos" [attr.data-testid]="testId()">
    <div>
      <dt>{{ i18n.t('collection.due') }}</dt>
      <dd data-testid="pos-due">
        <kaff-figure [value]="expected()" />
        @if (expectedNote(); as n) {
          <small>{{ n }}</small>
        }
      </dd>
    </div>
    <div>
      <dt>{{ i18n.t('collection.received') }}</dt>
      <dd data-testid="pos-received"><kaff-figure [value]="received()" /></dd>
    </div>
    <div>
      <dt>{{ i18n.t('collection.withheld') }}</dt>
      <dd data-testid="pos-withheld"><kaff-figure [value]="withheld()" /></dd>
    </div>
    <div>
      <dt>{{ i18n.t('collection.in_cheques') }}</dt>
      <dd data-testid="pos-cheques"><kaff-figure [value]="inCheques()" /></dd>
    </div>
    <div class="rem">
      <dt>{{ i18n.t('collection.remaining') }}</dt>
      <dd data-testid="pos-remaining"><kaff-figure [value]="remaining()" /></dd>
    </div>
  </dl>`,
  styles: `
    :host {
      display: block;
      min-inline-size: 0;
    }

    .pos {
      margin: 0;
      display: grid;
      grid-template-columns: repeat(5, minmax(0, 1fr));
      border: 1px solid var(--line);
      border-radius: var(--radius);
      background: var(--surface);
      overflow: hidden;
    }

    .pos > div {
      display: grid;
      gap: 2px;
      align-content: start;
      padding: 12px 16px;
      min-inline-size: 0;
    }

    .pos > div + div {
      border-inline-start: 1px solid var(--line);
    }

    dt {
      color: var(--dim);
      font-size: 0.8125rem;
    }

    dd {
      margin: 0;
      font: 700 1.25rem / 1.3 var(--body);
      overflow-wrap: anywhere;
    }

    dd small {
      display: block;
      font: 400 0.8125rem / 1.4 var(--body);
      color: var(--dim);
    }

    .rem {
      background: var(--head);
    }

    @media (width <= 820px) {
      .pos {
        grid-template-columns: 1fr 1fr;
      }

      .pos > div {
        border-block-start: 1px solid var(--line);
      }

      .pos > div:nth-child(-n + 2) {
        border-block-start: 0;
      }

      .pos > div:nth-child(odd) {
        border-inline-start: 0;
      }

      .rem {
        grid-column: 1 / -1;
      }

      dd {
        font-size: 1.0625rem;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffCollectionPosition {
  protected readonly i18n = inject(I18nService);

  readonly expected = input.required<string>();
  readonly received = input.required<string>();
  readonly withheld = input.required<string>();
  readonly inCheques = input.required<string>();
  readonly remaining = input.required<string>();
  /** «بعد إشعار دائن …» under المستحق when an approved credit note applies. */
  readonly expectedNote = input<string | null>(null);
  readonly testId = input<string | null>(null);
}
