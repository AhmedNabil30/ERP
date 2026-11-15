import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffButton } from '../kaff-button/kaff-button';
import { KaffIcon } from '../kaff-icon/kaff-icon';
import { KaffStatusPill } from '../kaff-status-pill/kaff-status-pill';

/**
 * The check-in strip (`att` in `.design/v2/team-a/daily-log.html`): one bordered row, a tick, "الحضور" and the time at
 * the start; at the end the time of the check-out, or an outline "الانصراف" button while there is none. A check-in
 * from far away carries the tag "بعيد عن الموقع" beside the time. Times arrive formatted.
 */
@Component({
  selector: 'kaff-attendance-strip',
  imports: [KaffButton, KaffIcon, KaffStatusPill],
  template: `<div class="att" [attr.data-testid]="testId()">
    <span class="tm">
      <kaff-icon class="tick" name="check" [small]="true" />{{ i18n.t('daily_log.field.check_in') }}
      <bdi dir="ltr">{{ checkInTime() }}</bdi>
      @if (far()) {
        <kaff-status-pill tone="same" icon="alert" testId="daily-log-checkin-flag">{{ i18n.t('daily_log.checkin.far_from_site') }}</kaff-status-pill>
      }
    </span>
    @if (checkOutTime(); as time) {
      <span class="tm">
        <kaff-icon class="tick" name="check" [small]="true" />{{ i18n.t('daily_log.field.check_out') }} <bdi dir="ltr">{{ time }}</bdi>
      </span>
    } @else {
      <kaff-button class="end" variant="secondary" testId="daily-log-check-out" [busy]="busy()" [disabled]="busy()" (clicked)="out.emit()">
        {{ i18n.t('daily_log.field.check_out') }}
      </kaff-button>
    }
  </div>`,
  styleUrl: './kaff-attendance-strip.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffAttendanceStrip {
  readonly checkInTime = input.required<string>();
  readonly checkOutTime = input<string | null>(null);
  readonly far = input(false);
  readonly busy = input(false);
  readonly testId = input<string | null>(null);

  readonly out = output<void>();

  protected readonly i18n = inject(I18nService);
}
