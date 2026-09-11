import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';

import { I18nService } from '../../core/i18n/i18n.service';
import { PhoneMatch } from '../phone-match';

/**
 * S-013's warning, lifted out of the client form (decisions.md D-141/D-146) so `S-024`'s employee
 * form shows the same presentation instead of a second one. A warning, never a refusal — the caller
 * still decides what a submit does with it; this component only shows who holds the number and reports
 * whether the operator ticked the box.
 *
 * `titleKey`/`confirmKey` are the caller's own i18n keys — the client and the employee register phrase
 * the same fact differently (`clients.duplicate.*` vs `hr.worker.duplicate_phone_*`), and CLAUDE.md
 * bans inventing a shared string neither catalogue already has.
 */
@Component({
  selector: 'kaff-duplicate-phone-warning',
  templateUrl: './duplicate-phone-warning.html',
  styleUrl: './duplicate-phone-warning.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DuplicatePhoneWarning {
  readonly matches = input.required<readonly PhoneMatch[]>();
  readonly titleKey = input.required<string>();
  readonly confirmKey = input.required<string>();
  /** Prefixes every `data-testid` here, e.g. `client` -> `client-duplicate-warning`. */
  readonly testIdPrefix = input.required<string>();

  /** The operator saying "I saw who holds this number and I am proceeding anyway." */
  readonly acknowledgeChange = output<boolean>();

  protected readonly i18n = inject(I18nService);

  protected trackMatch(_index: number, match: PhoneMatch): string {
    return match.id;
  }

  /** Read here, not in the template — `$any` is exactly what leaves `strictTemplates` nothing to check. */
  protected onChange(event: Event): void {
    const target = event.target;
    this.acknowledgeChange.emit(target instanceof HTMLInputElement && target.checked);
  }
}
