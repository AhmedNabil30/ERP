import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormField, form, minLength, required, schema, submit } from '@angular/forms/signals';
import { Router } from '@angular/router';

import { AuthApi } from '../../../core/auth/auth.api';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { toProblem } from '../../../core/api/problem-details';

/** Karim, D-049 ruling 3. The only rule this form imposes. */
const MINIMUM_PASSWORD_LENGTH = 8;

interface Credentials {
  userName: string;
  password: string;
}

/**
 * The form's rules, and all of them.
 *
 * `minLength` and `required` — no `pattern`, no symbol rule, no digit rule, and nothing that scores a
 * password as it is typed. D-049 ruling 3, and `AC-101b-E` fails if anything else is added here: a
 * strength meter is a policy statement wearing a progress bar, and this screen imposes nothing the
 * server does not.
 */
const credentials = schema<Credentials>((path) => {
  required(path.userName);
  required(path.password);
  minLength(path.password, MINIMUM_PASSWORD_LENGTH);
});

/**
 * The staff front door.
 *
 * **Every refusal reads the same.** A wrong password, a user name nobody holds, a `Role.Client`
 * credential at this origin and a subcontractor's all come back `401` with one `messageKey`
 * (decisions.md D-065, D-072 §1). This component renders the key the server sent and derives nothing
 * from the status code beyond it. Nabil's reason, on the subcontractor case: returning a specific
 * `errors.auth.role_cannot_log_in` "is explicitly telling the attacker: this account exists and
 * belongs to a subcontractor. That is a security breach." A friendlier message here re-opens the
 * account enumeration the whole API design closes — so there is no `switch` on `status` in this file
 * and none may be added. `423` is the one other shape, and only when the submitted password was
 * correct, so it names no account that a correct password did not already name.
 *
 * **Nothing here mentions the client portal.** Karim ruled it "a completely isolated interface"
 * (D-051 Q33): a client signs in at a different host and never learns this address exists. There is
 * no "are you a client?" affordance, and `AC-101b-C` searches the bundle for one.
 *
 * **Signal forms, not plain signals with `(input)` handlers.** Nabil overturned the shortcut on
 * 2026-08-28: this is the first screen and it sets the precedent for the whole ERP, so the form
 * primitive is the one CLAUDE.md mandates rather than the one that was smaller for two fields.
 */
@Component({
  selector: 'kaff-sign-in-page',
  imports: [FormField],
  templateUrl: './sign-in-page.html',
  styleUrl: './sign-in-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignInPage {
  private readonly api = inject(AuthApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  private readonly model = signal<Credentials>({ userName: '', password: '' });

  /**
   * The server's refusal, held beside the form rather than inside it.
   *
   * **It is deliberately not attached to a field.** A field-level error renders next to the input it
   * belongs to, and that placement is itself an answer: "wrong password" under the password box says
   * the user name was found. That is exactly the distinction D-065 refuses to make. One page-level
   * message, one key, no field named.
   */
  private readonly refusal = signal<string | null>(null);

  protected readonly i18n = inject(I18nService);

  protected readonly loginForm = form(this.model, credentials);

  protected readonly refusalKey = this.refusal.asReadonly();

  protected readonly isSubmitting = computed(() => this.loginForm().submitting());

  protected readonly canSubmit = computed(
    () => this.loginForm().valid() && !this.loginForm().submitting(),
  );

  protected async onSubmit(): Promise<void> {
    this.refusal.set(null);

    await submit(this.loginForm, async (field) => {
      const { userName, password } = field().value();

      try {
        await this.api.signIn(userName.trim(), password);
      } catch (error) {
        // One branch for every refusal. The key comes from the server; this file does not choose it.
        this.refusal.set(toProblem(error).messageKey);
        this.model.update((current) => ({ ...current, password: '' }));
        return undefined;
      }

      // The cookie now exists. Who it belongs to is a question for the server, not an inference from
      // what was typed — CLAUDE.md, and decisions.md D-075: read the actor fresh rather than from a
      // claim. A `Role.Client` never reaches this line, because the API refuses at the door rather
      // than issuing a session and letting the client redirect itself away (rule 3, `AC-101b-B`).
      let session;

      try {
        session = await this.api.me();
      } catch (error) {
        // Signed in, but we cannot say as whom. Showing the refusal is the safe outcome: the cookie
        // exists and a second attempt will succeed, whereas navigating on without a profile would
        // put an unidentified user in front of a shell whose contents are supposed to come from
        // `/api/auth/me` and nowhere else (rule 10).
        this.refusal.set(toProblem(error).messageKey);
        this.model.update((current) => ({ ...current, password: '' }));
        return undefined;
      }

      this.auth.set(session);

      if (session.mustChangePassword) {
        // ⚠️ **Deliberately not a redirect, because there is nowhere to redirect to.**
        //
        // Rule 8 sends this user straight to the change-password screen. That screen is KAFF-103's
        // (`AC-103-I`) and is not built. Navigating to `/change-password` today would fall through
        // `app.routes.ts`'s wildcard onto the landing page — a signed-in user reaching the
        // application with a password the Owner set, which is the one outcome rule 8 exists to
        // prevent, arrived at by code that looks correct.
        //
        // Holding them here with the server's own key is honest and safe: they cannot proceed, and
        // nothing pretends the screen exists. The server is what actually stops them — D-086 put the
        // check inside `PermissionEvaluator`, so every permission-gated route refuses this session by
        // construction. `AC-101b-F` moves to KAFF-103 with the screen.
        this.refusal.set('errors.auth.password_change_required');
        this.model.update((current) => ({ ...current, password: '' }));
        return undefined;
      }

      // ⚠️ Rules 9 and 10's destinations do not exist either. `Role.Hr` is ruled to land on the
      // Project Team surface rather than a project dashboard (D-051 Q32) — that is KAFF-115 — and the
      // staff shell is KAFF-105b. Routing everyone to `/` is not a decision that HR and Finance share
      // a landing page; it is the statement that there is one page. `AC-101b-A` and `AC-101b-D` move
      // with those stories rather than being closed against screens nobody has built.
      await this.router.navigateByUrl('/');

      return undefined;
    });
  }
}
