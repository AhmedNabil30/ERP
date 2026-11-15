import { ChangeDetectionStrategy, Component, computed, inject, input, model } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffIcon } from '../kaff-icon/kaff-icon';
import { KaffSegmentedFilter, SegmentedFilterOption } from '../kaff-segmented-filter/kaff-segmented-filter';
import { DAY_VALUES, DayValue, dayDate, dayWord } from './site-days';

/**
 * The day switch (`seg days` in `.design/v2/team-a/daily-log.html` and `day-labour.html`): today, yesterday and the
 * names of the two days before, with the full date written once beside it. Anything projected (the back-dated tag)
 * sits after the date. Shared by the daily log and the crew screen so the two never drift.
 */
@Component({
  selector: 'kaff-day-switch',
  imports: [KaffIcon, KaffSegmentedFilter],
  template: `<div class="drow">
    <kaff-segmented-filter [class.with-other]="allowOther()" [options]="options()" [value]="shown()" (valueChange)="choose($event)" [ariaLabelKey]="ariaLabelKey()" [testIdPrefix]="testIdPrefix()" />
    @if (allowOther()) {
      <label class="dpick" [class.on]="otherDate() !== null">
        <kaff-icon name="cal" [small]="true" /><span>{{ i18n.t('daily_log.day.other') }}</span>
        <input type="date" [max]="maxDate()" [value]="otherDate() ?? ''" [attr.aria-label]="i18n.t('daily_log.day.other')" [attr.data-testid]="testIdPrefix() + '-other'" (change)="chooseOther($event)" />
      </label>
    }
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
    /* The other-day control (README "The owner's daily log"): a native date input behind a button face. */
    .with-other {
      flex: 1 1 auto;
      min-inline-size: 0;
    }
    .dpick {
      position: relative;
      display: inline-flex;
      align-items: center;
      gap: 8px;
      min-block-size: 3.25rem;
      padding-inline: 14px;
      border: 2px solid var(--fieldline);
      border-radius: var(--radius);
      background: var(--field);
      font: 600 var(--text-sm) / 1 var(--body);
      cursor: pointer;
    }
    .dpick:hover {
      background: var(--hover);
    }
    .dpick.on {
      background: var(--ink);
      color: var(--bg);
      font-weight: 700;
    }
    .dpick:focus-within {
      outline: 3px solid var(--focus);
      outline-offset: 3px;
    }
    .dpick input {
      position: absolute;
      inset: 0;
      inline-size: 100%;
      block-size: 100%;
      opacity: 0;
      cursor: pointer;
      border: 0;
      padding: 0;
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
  /** Shows the «يوم آخر» control (the owner's read-only log, D-226 item 4). Off: the switch is the four days alone. */
  readonly allowOther = input(false);
  /** The chosen other day, `yyyy-MM-dd`; `null` while one of the four chips is chosen. */
  readonly otherDate = model<string | null>(null);
  /** The latest day the control offers, `yyyy-MM-dd`: today on the site's clock. */
  readonly maxDate = input<string | null>(null);

  protected readonly i18n = inject(I18nService);

  protected readonly options = computed<readonly SegmentedFilterOption<DayValue>[]>(() =>
    DAY_VALUES.map((value) => ({ value, labelKey: '', labelText: dayWord(this.i18n, this.today(), Number(value)) })),
  );

  /** While another day is open no chip is pressed. */
  protected readonly shown = computed<DayValue>(() => (this.otherDate() === null ? this.value() : ('' as DayValue)));

  protected readonly dateLine = computed(() => {
    const other = this.otherDate();
    const date = other === null ? dayDate(this.today(), Number(this.value())) : new Date(`${other}T12:00:00`);
    return this.i18n.formatDate(date, { weekday: 'long', day: 'numeric', month: 'long' });
  });

  protected choose(value: DayValue): void {
    this.otherDate.set(null);
    this.value.set(value);
  }

  protected chooseOther(event: Event): void {
    const picked = (event.target as HTMLInputElement).value;
    if (picked !== '') {
      this.otherDate.set(picked);
    }
  }
}
