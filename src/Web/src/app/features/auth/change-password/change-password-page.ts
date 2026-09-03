import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormField, form, minLength, required, schema, submit, validate } from '@angular/forms/signals';
import { Router } from '@angular/router';

import { AuthApi } from '../../../core/auth/auth.api';
import { AuthService } from '../../../core/auth/auth.service';
import { SessionResolver } from '../../../core/auth/session-resolver';
import { I18nService } from '../../../core/i18n/i18n.service';
import { toProblem } from '../../../core/api/problem-details';

/** D-049 ruling 3, restated exactly as the sign-in screen restates it — the whole rule. */
const MINIMUM_PASSWORD_LENGTH = 8;

interface ChangePasswordFields {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

/**
 * The form's rules, and all of them.
 *
 * `required` and `minLength(8)` on the new password — no `pattern`, no strength meter. D-049 ruling 3
 * and `AC-103-E`'s "and nothing more" fail if anything else is added here, the same discipline
 * `sign-in-page.ts` documents for its own password field.
 *
 * `confirmPassword` never leaves this component. The server is handed exactly `currentPassword` and
 * `newPassword` — `Api/Features/Auth/ChangePassword/Request.cs` takes no third field — the same shape
 * the setup screen's own confirm field already established (`CreateOwner.Request`: "`ConfirmPassword`
 * is a client-side-only check; the server never sees a second copy to compare"). The mismatch check
 * lives in the schema, not a template `computed`, so an unsubmittable mismatch cannot reach the
 * network at all: `changeForm().valid()` is false while it holds.
 */
const changePasswordSchema = schema<ChangePasswordFields>((path) => {
  required(path.currentPassword);

  required(path.newPassword);
  minLength(path.newPassword, MINIMUM_PASSWORD_LENGTH);

  required(path.confirmPassword);
  validate(path.confirmPassword, (ctx) =>
    ctx.value() === ctx.valueOf(path.newPassword) ? undefined : { kind: 'mismatch' },
  );
});

/**
 * The screen `AC-103-I` builds and `AC-101b-F` moved here: a forced-change session's only reachable
 * destination, and the ordinary "change my password" screen for everyone else — `SetOwnPassword` is
 * the same call either way (`ChangePassword/Handler.cs`'s own remark).
 *
 * **`currentPassword` is required, not a confirmation field.** `AC-103-D`: a form that asks only for
 * the new password twice lets anyone with a borrowed unlocked session take the account. The current
 * password is the first field on this form and nothing here makes it optional.
 *
 * **The server's refusal is one page-level message, never a field error** — the same reasoning
 * `sign-in-page.ts` and decisions.md D-091 already carry for the sign-in screen, for the same reason:
 * this form has two password fields that came from the server's own decision (wrong current password,
 * password too short), and attaching either to a field says which one is wrong. No `switch` on status
 * appears here, and none may be added.
 *
 * **On success, this device is not signed out by its own change** (`AC-103-A`) — the endpoint mints a
 * fresh cookie — but `AC-103-F` already ended every *other* session. `GET /api/auth/me` is re-fetched
 * so `AuthService` reads `mustChangePassword: false` before routing anywhere a permission gate would
 * otherwise refuse (decisions.md D-086), then the router goes to `/` — the one landing route slice 1
 * has; KAFF-105b's shell and KAFF-115's HR landing do not exist and are not invented here.
 */
@Component({
  selector: 'kaff-change-password-page',
  imports: [FormField],
  templateUrl: './change-password-page.html',
  styleUrl: './change-password-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangePasswordPage {
  private readonly api = inject(AuthApi);
  private readonly auth = inject(AuthService);
  private readonly resolver = inject(SessionResolver);
  private readonly router = inject(Router);

  private readonly model = signal<ChangePasswordFields>({
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  });

  /**
   * The server's refusal, held beside the form rather than inside it — see the class comment and
   * decisions.md D-091.
   */
  private readonly refusal = signal<string | null>(null);

  /**
   * True for the brief window between a successful change and the redirect. The `GET /api/auth/me`
   * round trip that follows is what actually gives this a moment on screen; there is no artificial
   * delay here.
   */
  private readonly success = signal(false);

  protected readonly i18n = inject(I18nService);

  protected readonly changeForm = form(this.model, changePasswordSchema);

  protected readonly refusalKey = this.refusal.asReadonly();

  protected readonly changed = this.success.asReadonly();

  protected readonly isSubmitting = computed(() => this.changeForm().submitting());

  protected readonly canSubmit = computed(
    () => this.changeForm().valid() && !this.changeForm().submitting(),
  );

  /**
   * Why this screen, unprompted — shown only when the session says a change was forced. A voluntary
   * visit (KAFF-103's other path through the same endpoint) gets the form with no such banner.
   */
  protected readonly mustChangePassword = computed(
    () => this.auth.current()?.mustChangePassword ?? false,
  );

  /**
   * A plain UX nudge, read from the model rather than the schema's own error list so the template
   * does not have to know the validator's error `kind`. `changeForm().valid()` — not this signal — is
   * what actually blocks submission; this only decides whether to say why.
   */
  protected readonly passwordsMismatch = computed(() => {
    const { newPassword, confirmPassword } = this.model();
    return confirmPassword.length > 0 && newPassword !== confirmPassword;
  });

  constructor() {
    // A direct reload of this route (rather than arriving through the sign-in redirect, which
    // already calls `AuthService.set`) starts with no session at all. Resolved here only for
    // `mustChangePassword`'s banner text, through the same `SessionResolver` every other entry point
    // uses (KAFF-125) rather than a second hand-rolled fetch — a failure here changes nothing: the
    // form still works, and a caller with no cookie at all finds out the honest way, from the
    // change-password call itself.
    void this.resolver.ensureResolved();
  }

  protected async onSubmit(): Promise<void> {
    this.refusal.set(null);
    this.success.set(false);

    await submit(this.changeForm, async (field) => {
      const { currentPassword, newPassword } = field().value();

      try {
        await this.api.changePassword(currentPassword, newPassword);
      } catch (error) {
        // One region for every refusal — a wrong current password and a too-short new one both land
        // here, never against a field. See the class comment.
        this.refusal.set(toProblem(error).messageKey);
        this.model.update((current) => ({ ...current, currentPassword: '' }));
        return undefined;
      }

      this.success.set(true);

      try {
        this.auth.set(await this.api.me());
      } catch (error) {
        // Changed, but the fresh cookie this endpoint just minted did not authenticate the very next
        // call — showing the refusal is the safe outcome, the same reasoning `sign-in-page.ts` gives
        // for the identical shape.
        this.refusal.set(toProblem(error).messageKey);
        this.success.set(false);
        return undefined;
      }

      await this.router.navigateByUrl('/');

      return undefined;
    });
  }
}
