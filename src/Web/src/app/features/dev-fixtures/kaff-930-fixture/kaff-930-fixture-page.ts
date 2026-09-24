import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';

import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffInlineEdit } from '../../../shared/kaff-inline-edit/kaff-inline-edit';
import { KaffRowMenu, RowMenuItem } from '../../../shared/kaff-row-menu/kaff-row-menu';
import { KaffState, KaffStateMode } from '../../../shared/kaff-state/kaff-state';

/**
 * KAFF-930 render proof, not a product screen. Renders all four interaction-kit components together
 * — `kaff-row-menu`, `kaff-detail-panel`, `kaff-inline-edit`, `kaff-state` (all four modes) — so they
 * can be screenshotted (light/dark, desktop/390px) before `KAFF-931` wires them into the real
 * screens. Unguarded route, dev-only; delete when a product screen renders all four.
 */
@Component({
  selector: 'app-kaff-930-fixture-page',
  imports: [KaffDetailPanel, KaffInlineEdit, KaffRowMenu, KaffState],
  templateUrl: './kaff-930-fixture-page.html',
  styleUrl: './kaff-930-fixture-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Kaff930FixturePage {
  protected readonly i18n = inject(I18nService);

  protected readonly rowMenuItems: readonly RowMenuItem[] = [
    { labelKey: 'devFixtures.kaff930.rowMenu.edit', action: 'edit' },
    { labelKey: 'devFixtures.kaff930.rowMenu.archive', action: 'archive', tone: 'danger' },
  ];

  protected readonly panelOpen = signal(true);
  protected readonly unitPrice = signal('1850.00');

  protected readonly stateModes: readonly {
    readonly mode: KaffStateMode;
    readonly titleKey: string;
    readonly bodyKey: string | null;
    readonly hiddenCount: number | null;
  }[] = [
    {
      mode: 'loading',
      titleKey: 'devFixtures.kaff930.state.loading.title',
      bodyKey: null,
      hiddenCount: null,
    },
    {
      mode: 'empty',
      titleKey: 'devFixtures.kaff930.state.empty.title',
      bodyKey: 'devFixtures.kaff930.state.empty.body',
      hiddenCount: null,
    },
    {
      mode: 'filtered-empty',
      titleKey: 'devFixtures.kaff930.state.filteredEmpty.title',
      bodyKey: 'devFixtures.kaff930.state.filteredEmpty.body',
      hiddenCount: 4,
    },
    {
      mode: 'refusal',
      titleKey: 'devFixtures.kaff930.state.refusal.title',
      bodyKey: 'devFixtures.kaff930.state.refusal.body',
      hiddenCount: null,
    },
  ];

  protected onRowMenuSelected(action: string): void {
    // Dev fixture only — nothing to wire the selection to.
    console.info(`[kaff-930-fixture] row menu selected: ${action}`);
  }

  protected onInlineEditCommitted(value: string): void {
    this.unitPrice.set(value);
  }
}
