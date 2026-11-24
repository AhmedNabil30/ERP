import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormField, form, minLength, required, schema, submit, validate } from '@angular/forms/signals';
import { Router } from '@angular/router';

import { toProblem } from '../../core/api/problem-details';
import { I18nService } from '../../core/i18n/i18n.service';
import { KaffBanner } from '../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffField } from '../../shared/kaff-field/kaff-field';
import { PortalApi } from './portal.api';

interface Fields {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

const MINIMUM_PASSWORD_LENGTH = 8;

const fields = schema<Fields>((path) => {
  required(path.currentPassword);
  required(path.newPassword);
  minLength(path.newPassword, MINIMUM_PASSWORD_LENGTH);
  required(path.confirmPassword);
  validate(path.confirmPassword, (ctx) => (ctx.value() === ctx.valueOf(path.newPassword) ? undefined : { kind: 'mismatch' }));
});

/**
 * P1 `change`. The temporary password the Owner handed over is replaced before anything else opens. The mock draws two
 * fields; the endpoint also needs the current (temporary) password, so a third field is drawn first.
 */
@Component({
  selector: 'kaff-portal-change-password-page',
  imports: [FormField, KaffBanner, KaffButton, KaffField],
  styleUrl: './portal.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap narrow">
      <h1>{{ i18n.t('portal.password.title') }}</h1>
      <form class="form" (submit)="$event.preventDefault(); onSubmit()" novalidate>
        @if (refusalKey(); as key) {
          <kaff-banner tone="refusal" icon="alert" testId="portal-password-refused">{{ i18n.t(key) }}</kaff-banner>
        }
        <kaff-field labelKey="portal.password.current" controlId="portal-current">
          <div class="ctl ltr">
            <input id="portal-current" type="password" autocomplete="current-password" data-testid="portal-current" [formField]="changeForm.currentPassword" />
          </div>
        </kaff-field>
        <kaff-field labelKey="portal.password.new" controlId="portal-new" hintKey="portal.password.min">
          <div class="ctl ltr">
            <input id="portal-new" type="password" autocomplete="new-password" data-testid="portal-new" [formField]="changeForm.newPassword" />
          </div>
        </kaff-field>
        <kaff-field labelKey="portal.password.confirm" controlId="portal-confirm" [errorKey]="mismatch() ? 'portal.password.mismatch' : null">
          <div class="ctl ltr">
            <input id="portal-confirm" type="password" autocomplete="new-password" data-testid="portal-confirm" [formField]="changeForm.confirmPassword" />
          </div>
        </kaff-field>
        <kaff-button class="fill" variant="primary" type="submit" testId="portal-password-save" [busy]="isSubmitting()" [disabled]="!canSubmit()">
          {{ i18n.t('portal.password.save') }}
        </kaff-button>
      </form>
    </div>
  `,
})
export class PortalChangePasswordPage {
  private readonly api = inject(PortalApi);
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18nService);

  private readonly model = signal<Fields>({ currentPassword: '', newPassword: '', confirmPassword: '' });
  private readonly refusal = signal<string | null>(null);

  protected readonly changeForm = form(this.model, fields);
  protected readonly refusalKey = this.refusal.asReadonly();
  protected readonly isSubmitting = computed(() => this.changeForm().submitting());
  protected readonly canSubmit = computed(() => this.changeForm().valid() && !this.isSubmitting());
  protected readonly mismatch = computed(() => {
    const { newPassword, confirmPassword } = this.model();
    return confirmPassword.length > 0 && newPassword !== confirmPassword;
  });

  protected async onSubmit(): Promise<void> {
    this.refusal.set(null);
    await submit(this.changeForm, async (field) => {
      const { currentPassword, newPassword } = field().value();
      try {
        await this.api.changePassword(currentPassword, newPassword);
      } catch (error) {
        this.refusal.set(toProblem(error).messageKey);
        this.model.update((current) => ({ ...current, currentPassword: '' }));
        return undefined;
      }
      await this.router.navigateByUrl('/portal', { replaceUrl: true });
      return undefined;
    });
  }
}
