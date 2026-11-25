import { ChangeDetectionStrategy, Component, computed, inject, input, model, signal } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffDateField } from '../kaff-date-field/kaff-date-field';
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
  imports: [KaffDateField, KaffIcon, KaffSegmentedFilter],
  template: `<div class="drow">
    <kaff-segmented-filter [class.with-other]="allowOther()" [options]="options()" [value]="shown()" (valueChange)="choose($event)" [ariaLabelKey]="ariaLabelKey()" [testIdPrefix]="testIdPrefix()" />
    @if (allowOther()) {
      <button
        type="button"
        class="dpick"
        [class.on]="otherDate() !== null"
        [attr.aria-expanded]="picking()"
        [attr.data-testid]="testIdPrefix() + '-other-toggle'"
        (click)="picking.set(!picking())"
      >
        <kaff-icon name="cal" [small]="true" /><span>{{ i18n.t('daily_log.day.other') }}</span>
      </button>
    }
    <span class="dt" [attr.data-testid]="dateTestId()">{{ dateLine() }}</span>
    <ng-content />
  </div>
  @if (allowOther() && (picking() || otherDate() !== null)) {
    <div class="dfield">
      <kaff-date-field
        labelKey="daily_log.day.other"
        [controlId]="testIdPrefix() + '-other-field'"
        [testId]="testIdPrefix() + '-other'"
        [max]="maxDate()"
        [value]="otherDate() ?? ''"
        (valueChange)="chooseOther($event)"
      />
    </div>
  }`,
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
    /* The other-day control (README "The owner's daily log"): a button face that opens the kit date field. */
    .with-other {
      flex: 1 1 auto;
      min-inline-size: 0;
    }
    .dpick {
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
    .dfield {
      max-inline-size: 16rem;
      margin-block-start: 8px;
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
  protected readonly picking = signal(false);

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

  protected chooseOther(picked: string): void {
    if (picked !== '') {
      this.otherDate.set(picked);
    }
  }
}
