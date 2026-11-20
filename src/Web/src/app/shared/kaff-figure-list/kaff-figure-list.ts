import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { KaffFigure } from '../kaff-figure/kaff-figure';

/** One row: a label, and either a money figure (a wire string) or a text value. */
export interface FigureRow {
  readonly label: string;
  /** A money wire string (D-135). A deduction is passed negative and prints with a real minus. */
  readonly amount?: string | null;
  /** A text value instead of a figure (a reason, a name). */
  readonly text?: string | null;
  /** A dim line under the label. */
  readonly note?: string | null;
  /** The ruled total row. */
  readonly total?: boolean;
  readonly testId?: string;
}

/**
 * The figure list (README "Extract chain", kit item 4): label at the start, figure at the end, real minus, ruled
 * total row. The unit is named once, in the section heading above it, never per row.
 */
@Component({
  selector: 'kaff-figure-list',
  imports: [KaffFigure],
  template: `<dl class="fl" [attr.data-testid]="testId()">
    @for (row of rows(); track $index) {
      <div [class.tot]="row.total === true" [attr.data-testid]="row.testId ?? null">
        <dt>
          <span dir="auto">{{ row.label }}</span>
          @if (row.note; as n) {
            <small dir="auto">{{ n }}</small>
          }
        </dt>
        <dd>
          @if (row.amount; as a) {
            <kaff-figure [value]="a" />
          } @else if (row.text; as t) {
            <span dir="auto">{{ t }}</span>
          }
        </dd>
      </div>
    }
  </dl>`,
  styles: `
    :host {
      display: block;
      min-inline-size: 0;
    }

    .fl {
      margin: 0;
      border: 1px solid var(--line);
      border-radius: var(--radius);
      background: var(--surface);
    }

    .fl > div {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      align-items: center;
      gap: 4px 16px;
      min-block-size: 48px;
      padding: 6px 16px;
      border-block-start: 1px solid var(--line);
    }

    .fl > div:first-child {
      border-block-start: 0;
    }

    dt {
      flex: 0 1 auto;
      min-inline-size: 0;
    }

    dd {
      margin: 0;
      margin-inline-start: auto;
      text-align: end;
      min-inline-size: 0;
      overflow-wrap: anywhere;
    }

    small {
      display: block;
      color: var(--dim);
      font-size: 0.8125rem;
    }

    .fl > div.tot {
      background: var(--head);
      border-block-start: 2px solid var(--fieldline);
      font-weight: 600;
      min-block-size: 52px;
    }

    .tot dd {
      font-weight: 700;
      font-size: 1.125rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffFigureList {
  readonly rows = input.required<readonly FigureRow[]>();
  readonly testId = input<string | null>(null);
}
