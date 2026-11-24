import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormField, form, minLength, required, schema, submit } from '@angular/forms/signals';
import { Router } from '@angular/router';

import { I18nService } from '../../core/i18n/i18n.service';
import { KaffBanner } from '../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffField } from '../../shared/kaff-field/kaff-field';
import { KaffIcon } from '../../shared/kaff-icon/kaff-icon';
import { PortalApi } from './portal.api';

interface Credentials {
  userName: string;
  password: string;
}

const MINIMUM_PASSWORD_LENGTH = 8;

const credentials = schema<Credentials>((path) => {
  required(path.userName);
  required(path.password);
  minLength(path.password, MINIMUM_PASSWORD_LENGTH);
});

/** P1. Phone plus password. One refusal line for every refusal (no hint of which half was wrong). No reset link: the Owner resets. */
@Component({
  selector: 'kaff-portal-sign-in-page',
  imports: [FormField, KaffBanner, KaffButton, KaffField, KaffIcon],
  styleUrl: './portal.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap narrow">
      <h1>{{ i18n.t('portal.sign_in.title') }}</h1>
      <form class="form" (submit)="$event.preventDefault(); onSubmit()" novalidate>
        @if (refused()) {
          <kaff-banner tone="refusal" icon="alert" testId="portal-sign-in-refused">
            {{ i18n.t('portal.sign_in.refused') }}
          </kaff-banner>
        }
        <kaff-field labelKey="portal.sign_in.phone" controlId="portal-phone">
          <div class="ctl ltr">
            <input
              id="portal-phone"
              type="tel"
              inputmode="tel"
              autocomplete="username"
              data-testid="portal-phone"
              [formField]="signInForm.userName" />
          </div>
        </kaff-field>
        <kaff-field labelKey="portal.sign_in.password" controlId="portal-password">
          <div class="ctl ltr">
            <input
              id="portal-password"
              [type]="shown() ? 'text' : 'password'"
              autocomplete="current-password"
              data-testid="portal-password"
              [formField]="signInForm.password" />
            <button
              type="button"
              class="eye"
              [attr.aria-label]="i18n.t(shown() ? 'portal.sign_in.hide' : 'portal.sign_in.show')"
              [attr.aria-pressed]="shown()"
              (click)="shown.set(!shown())">
              <kaff-icon [name]="shown() ? 'eyeoff' : 'eye'" />
            </button>
          </div>
        </kaff-field>
        <kaff-button class="fill" variant="primary" type="submit" testId="portal-sign-in-submit" [busy]="isSubmitting()">
          {{ i18n.t(isSubmitting() ? 'portal.sign_in.submitting' : 'portal.sign_in.submit') }}
        </kaff-button>
      </form>
    </div>
  `,
})
export class PortalSignInPage {
  private readonly api = inject(PortalApi);
  private readonly router = inject(Router);
  protected readonly i18n = inject(I18nService);

  private readonly model = signal<Credentials>({ userName: '', password: '' });
  protected readonly signInForm = form(this.model, credentials);
  protected readonly shown = signal(false);
  protected readonly refused = signal(false);
  protected readonly isSubmitting = computed(() => this.signInForm().submitting());

  protected async onSubmit(): Promise<void> {
    this.refused.set(false);

    // Too short or empty: no request goes out, and the same one refusal answers it.
    if (!this.signInForm().valid()) {
      this.refused.set(true);
      return;
    }

    await submit(this.signInForm, async (field) => {
      const { userName, password } = field().value();
      try {
        await this.api.signIn(userName.trim(), password);
      } catch {
        this.refused.set(true);
        this.model.update((current) => ({ ...current, password: '' }));
        return undefined;
      }
      // The list page skips itself for a one-project client, and sends a temporary password to its change screen.
      await this.router.navigateByUrl('/portal');
      return undefined;
    });
  }
}
