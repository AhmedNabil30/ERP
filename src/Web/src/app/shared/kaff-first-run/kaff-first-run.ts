import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { KaffIcon, KaffIconName } from '../kaff-icon/kaff-icon';

/**
 * A first-run state (README "The catalogue's first run"): an empty screen that teaches. A 56px icon tile,
 * a title, one sentence, the actions (`[frActions]`: one primary, or two paths side by side), numbered
 * steps (a real sequence, so the numbers are structure), and a labelled example that is visibly not data
 * (`[frExample]`, drawn in a dashed box with its own tag). Everything but the icon is already-resolved text.
 */
@Component({
  selector: 'kaff-first-run',
  imports: [KaffIcon],
  templateUrl: './kaff-first-run.html',
  styleUrl: './kaff-first-run.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffFirstRun {
  readonly icon = input.required<KaffIconName>();
  readonly title = input.required<string>();
  readonly body = input.required<string>();
  readonly steps = input<readonly string[]>([]);
  readonly exampleTitle = input.required<string>();
  readonly exampleTag = input.required<string>();
  readonly testId = input<string | null>(null);
  readonly exampleTestId = input<string | null>(null);
}
