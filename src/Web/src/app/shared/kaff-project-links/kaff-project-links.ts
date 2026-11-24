import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';

type Screen = 'extracts' | 'proof' | 'daily-log' | 'boq' | 'budget';

/**
 * Onward links between the project-scoped screens (extracts, proof, daily log, BOQ). A link renders only when
 * the session may open its destination, mirroring the route guards: `ProjectRead`, `ProjectManage` and `DailyLogWrite` on
 * this project's own session entry, `TreasuryReadCompany` company-wide (`proofReadGuard`). Absent, never
 * disabled. Convenience only: the server decides every request.
 */
@Component({
  selector: 'kaff-project-links',
  imports: [RouterLink],
  templateUrl: './kaff-project-links.html',
  styleUrl: './kaff-project-links.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffProjectLinks {
  private readonly auth = inject(AuthService);

  protected readonly i18n = inject(I18nService);

  readonly projectId = input.required<string>();
  /** The screen the viewer is on; its own link is not rendered. */
  readonly current = input.required<Screen>();

  protected readonly links = computed(() => {
    const session = this.auth.current();
    const entry = session?.projects.find((row) => row.projectId === this.projectId());
    const all: readonly { screen: Screen; labelKey: string; allowed: boolean }[] = [
      { screen: 'extracts', labelKey: 'extract.list.title', allowed: entry?.permissions.includes('ProjectRead') ?? false },
      { screen: 'proof', labelKey: 'proof.title', allowed: session?.permissions.includes('TreasuryReadCompany') ?? false },
      { screen: 'daily-log', labelKey: 'daily_log.title', allowed: entry?.permissions.includes('DailyLogWrite') ?? false },
      { screen: 'boq', labelKey: 'boq.title', allowed: entry?.permissions.includes('ProjectManage') ?? false },
      { screen: 'budget', labelKey: 'accounting.budget.title', allowed: entry?.permissions.includes('BudgetBaselineRead') ?? false },
    ];
    return all.filter((link) => link.allowed && link.screen !== this.current());
  });
}
