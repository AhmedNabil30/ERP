import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { KaffIcon } from '../kaff-icon/kaff-icon';

/**
 * The forward link row (README "Extract chain", kit item 9): the back link's face with the chevron pointing
 * forward and an optional count badge, for sub-sections (the extracts tab) and «الدفعات» on the extract.
 */
@Component({
  selector: 'kaff-forward-link',
  imports: [RouterLink, KaffIcon],
  template: `<a class="fwd" [routerLink]="link()" [attr.data-testid]="testId()">
    <span dir="auto">{{ label() }}</span>
    @if (count(); as c) {
      <span class="badge">{{ c }}</span>
    }
    <kaff-icon class="chev" name="back" [small]="true" />
  </a>`,
  styles: `
    :host {
      display: inline-flex;
    }

    .fwd {
      display: inline-flex;
      align-items: center;
      gap: 10px;
      min-block-size: 44px;
      color: var(--ink);
      text-decoration: none;
      font-weight: 600;
    }

    .fwd:hover {
      text-decoration: underline;
      text-underline-offset: 4px;
    }

    .fwd:focus-visible {
      outline: 3px solid var(--focus);
      outline-offset: 3px;
    }

    /* The back chevron points to the start edge; forward points to the end, so it flips in LTR only. */
    :host-context([dir='ltr']) .chev {
      transform: scaleX(-1);
    }

    .badge {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-inline-size: 24px;
      block-size: 24px;
      padding: 0 8px;
      border-radius: var(--radius);
      border: 1.5px solid var(--line);
      color: var(--dim);
      font: 600 0.8125rem / 1 var(--body);
      font-variant-numeric: tabular-nums;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffForwardLink {
  readonly link = input.required<readonly string[]>();
  readonly label = input.required<string>();
  readonly count = input<string | null>(null);
  readonly testId = input<string | null>(null);
}
