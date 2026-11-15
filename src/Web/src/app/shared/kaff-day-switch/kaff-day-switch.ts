import { ChangeDetectionStrategy, Component, computed, inject, input, model } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffSegmentedFilter, SegmentedFilterOption } from '../kaff-segmented-filter/kaff-segmented-filter';
import { DAY_VALUES, DayValue, dayDate, dayWord } from './site-days';

/**
 * The day switch (`seg days` in `.design/v2/team-a/daily-log.html` and `day-labour.html`): today, yesterday and the
 * names of the two days before, with the full date written once beside it. Anything projected (the back-dated tag)
 * sits after the date. Shared by the daily log and the crew screen so the two never drift.
 */
@Component({
  selector: 'kaff-day-switch',
  imports: [KaffSegmentedFilter],
  template: `<div class="drow">
    <kaff-segmented-filter [options]="options()" [(value)]="value" [ariaLabelKey]="ariaLabelKey()" [testIdPrefix]="testIdPrefix()" />
    <span class="dt" [attr.data-testid]="dateTestId()">{{ dateLine() }}</span>
    <ng-content />
  </div>`,
  styles: `
    :host {
      display: block;
    }
    .drow {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px 12px;
    }
    .dt {
      font-weight: 600;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffDaySwitch {
  readonly value = model.required<DayValue>();
  readonly today = input.required<Date>();
  readonly ariaLabelKey = input('daily_log.day.switch');
  readonly testIdPrefix = input('day');
  readonly dateTestId = input<string | null>(null);

  private readonly i18n = inject(I18nService);

  protected readonly options = computed<readonly SegmentedFilterOption<DayValue>[]>(() =>
    DAY_VALUES.map((value) => ({ value, labelKey: '', labelText: dayWord(this.i18n, this.today(), Number(value)) })),
  );

  protected readonly dateLine = computed(() =>
    this.i18n.formatDate(dayDate(this.today(), Number(this.value())), { weekday: 'long', day: 'numeric', month: 'long' }),
  );
}
