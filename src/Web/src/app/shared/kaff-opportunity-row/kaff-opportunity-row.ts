import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { I18nService } from '../../core/i18n/i18n.service';
import { OpportunitySummary } from '../../core/sales/opportunities.api';
import { agoKey, daysQuiet, needsFollowUp, STATUS_PILL, stageKey, statusKey } from './opportunity-display';
import { KaffBadge } from '../kaff-badge/kaff-badge';
import { KaffIcon } from '../kaff-icon/kaff-icon';
import { KaffStatusPill } from '../kaff-status-pill/kaff-status-pill';

/**
 * The opportunity row (`opp` in `sales.html` S1): client over title, stage badge, a status pill only when the
 * opportunity is not active, the last-activity line and the follow-up badge. One link, 76px high on a phone;
 * five columns from 821px up. The quotation total column waits on R3-11.
 */
@Component({
  selector: 'kaff-opportunity-row',
  imports: [RouterLink, KaffBadge, KaffIcon, KaffStatusPill],
  templateUrl: './kaff-opportunity-row.html',
  styleUrl: './kaff-opportunity-row.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffOpportunityRow {
  protected readonly i18n = inject(I18nService);

  readonly opportunity = input.required<OpportunitySummary>();
  readonly now = input.required<Date>();

  protected readonly stage = computed(() => stageKey(this.opportunity().stage));
  protected readonly showStatus = computed(() => ['OnHold', 'ClosedLost', 'ClosedWon'].includes(this.opportunity().status));
  protected readonly status = computed(() => statusKey(this.opportunity().status));
  protected readonly pill = computed(() => STATUS_PILL[this.opportunity().status]);
  protected readonly ago = computed(() => {
    const { key, params } = agoKey(daysQuiet(this.opportunity().lastActivityAt, this.now()));
    return this.i18n.t(key, params);
  });
  protected readonly followUp = computed(() => needsFollowUp(this.opportunity().inactivity));
}
