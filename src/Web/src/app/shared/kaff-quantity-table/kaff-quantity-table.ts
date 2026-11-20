import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import type { ExtractQuantityRow } from '../../core/extracts/extracts.api';
import { KaffFigure } from '../kaff-figure/kaff-figure';

/**
 * The quantities table (README "Extract chain", kit item 6): each BOQ line with its unit under it, «هذه الفترة» and
 * «حتى تاريخه». Decimals appear only when the value has them. A money line shows under its quantity only when the
 * server sent it (it sends null to a viewer without extract money).
 */
@Component({
  selector: 'kaff-quantity-table',
  imports: [KaffFigure],
  template: `<div class="qt" [attr.data-testid]="testId()">
    <div class="qh">
      <span>{{ i18n.t('extract.qty.item') }}</span>
      <span>{{ i18n.t('extract.qty.period') }}</span>
      <span>{{ i18n.t('extract.qty.cumulative') }}</span>
    </div>
    @for (band of bands(); track $index) {
      @if (band.name) {
        <div class="qg" data-testid="quantity-band" dir="auto">{{ band.name }}</div>
      }
      @for (row of band.rows; track row.boqLineId) {
      <div class="qr" data-testid="quantity-row">
        <span dir="auto">{{ row.descriptionAr }}<span class="u" dir="auto">{{ row.unit }}</span></span>
        <span class="c">
          @if (row.currentQuantity != null) {
            <bdi dir="ltr" class="fx">{{ qty(row.currentQuantity) }}</bdi>
          }
          @if (row.currentAmount; as a) {
            <small><kaff-figure [value]="a" /></small>
          }
        </span>
        <span class="c">
          <bdi dir="ltr" class="fx">{{ qty(row.cumulativeQuantity) }}</bdi>
          @if (row.cumulativeAmount; as a) {
            <small><kaff-figure [value]="a" /></small>
          }
        </span>
      </div>
      }
    }
  </div>`,
  styles: `
    :host {
      display: block;
      min-inline-size: 0;
    }

    .qt {
      border: 1px solid var(--line);
      border-radius: var(--radius);
      background: var(--surface);
      overflow: hidden;
    }

    .qh,
    .qr {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(72px, 112px) minmax(72px, 112px);
      gap: 4px 12px;
      align-items: center;
      padding: 8px 16px;
    }

    .qh {
      font: 600 0.8125rem / 1.3 var(--body);
      color: var(--dim);
      background: var(--head);
    }

    .qh span + span,
    .qr .c {
      text-align: end;
    }

    .qg {
      font: 600 0.9375rem / 1.4 var(--display);
      padding: 8px 16px;
      border-block-start: 1px solid var(--line);
    }

    .qr {
      border-block-start: 1px solid var(--line);
      min-block-size: 56px;
    }

    .u,
    small {
      display: block;
      color: var(--dim);
      font-size: 0.8125rem;
    }

    /* On a phone the name takes the whole row, so it never wraps one word per line (consultant N2). */
    @media (max-width: 560px) {
      .qh,
      .qr {
        grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
      }

      .qh span:first-child {
        display: none;
      }

      .qr > span:first-child {
        grid-column: 1 / -1;
      }

      .qh span + span,
      .qr .c {
        text-align: start;
      }
    }

    .fx {
      font-variant-numeric: tabular-nums;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffQuantityTable {
  protected readonly i18n = inject(I18nService);

  readonly rows = input.required<readonly ExtractQuantityRow[]>();
  readonly testId = input<string | null>(null);

  /** Consecutive rows of one باب form a band. A row with no باب name draws no band header. */
  protected readonly bands = computed(() => {
    const english = this.i18n.locale() === 'en';
    const out: { key: string; name: string; rows: ExtractQuantityRow[] }[] = [];
    for (const row of this.rows()) {
      const name = (english ? row.babNameEn : row.babNameAr) || row.babNameAr || '';
      const last = out.at(-1);
      if (last !== undefined && last.key === row.babCode) {
        last.rows.push(row);
      } else {
        out.push({ key: row.babCode, name, rows: [row] });
      }
    }
    return out;
  });

  protected qty(value: number): string {
    return this.i18n.formatNumber(value, { maximumFractionDigits: 3 });
  }
}
