import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormField, form, required, schema, submit } from '@angular/forms/signals';
import { Router } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import {
  AuthService,
  Department,
  OperationsSubDepartment,
  Role,
} from '../../../core/auth/auth.service';
import { ClientSummary, ClientsApi } from '../../../core/clients/clients.api';
import {
  departmentKey,
  operationsSubDepartmentKey,
  roleKey,
} from '../../../core/i18n/enum-keys';
import { I18nService } from '../../../core/i18n/i18n.service';
import { UserSummary, UsersApi } from '../../../core/users/users.api';

/** Every role the select offers, in `spec.md` §9's order with D-044's `Hr` where the ruling put it. */
const ROLES: readonly Role[] = [
  'Owner',
  'Finance',
  'TechnicalOffice',
  'SiteEngineer',
  'HeadOfDesign',
  'MarketingSales',
  'Hr',
  'Client',
  'Subcontractor',
];

/** `spec.md` §9: "Finance, HR, Marketing, Operations." */
const DEPARTMENTS: readonly Department[] = ['Finance', 'Hr', 'Marketing', 'Operations'];

/** `spec.md` §9 — only Operations subdivides. */
const SUB_DEPARTMENTS: readonly OperationsSubDepartment[] = [
  'Technical',
  'Financial',
  'Administrative',
];

/** The server's own key for the pair `ValidateDepartment` refuses. `AC-127-C`. */
const HR_PAIR_REFUSAL = 'identity.hr_role_requires_hr_department';

interface UserDraft {
  fullName: string;
  userName: string;
  phone: string;
  email: string;
  role: Role;
  department: Department | '';
  operationsSubDepartment: OperationsSubDepartment | '';
  clientId: string;
  temporaryPassword: string;
  confirmPassword: string;
}

/**
 * The form's rules, and all of them.
 *
 * **Three `required`s and nothing else.** Every other rule belongs to the server: the phone's shape is
 * `PhoneNumber.Create`'s, the password's length is `User.MinimumPasswordLength`'s, and every
 * role/department pairing is `User.ValidateDepartment`'s. A second copy here is a copy that disagrees
 * with the entity eventually, and the entity is what every other caller goes through
 * (decisions.md D-109 §1). What this file *does* do is keep the illegal pair unassemblable — see
 * {@link UserFormPage.onRoleChange} — which is a different thing from re-implementing the refusal.
 */
const draft = schema<UserDraft>((path) => {
  required(path.fullName);
  required(path.userName);
  required(path.phone);
});

/**
 * The chosen option of a `<select>`, read from the event rather than through `$any` in the template.
 *
 * Nabil, 2026-08-28: `$any` is exactly what leaves `strictTemplates` nothing to check. Returns the
 * empty string for anything that is not a select, which every caller then narrows against its own
 * closed list — so a value outside the enum is discarded here and never sent as one.
 */
function readSelect(event: Event): string {
  const target = event.target;
  return target instanceof HTMLSelectElement ? target.value : '';
}

/** Blank means absent. The server trims and nulls too; this keeps the payload honest on the way out. */
function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

/**
 * S-007 · create a user, and S-008 · the user's record with its danger zone. One component.
 *
 * **One form serves both** because the create payload and the edit acts touch the same fields, and
 * building them twice is how the create form grows a field the edit form does not have (D-113 §1's
 * reasoning for the client form, which this mirrors).
 *
 * **⚠️ The most privileged screen in the system.** Slice-1 kickoff §2.1: *"because grants may be
 * written against a department, whoever can set a user's department can grant project-assignment
 * power."* Everything here is gated `UserManage` — `Role.Owner` alone — server-side; the guard on the
 * route is convenience.
 *
 * **The HR pair is kept legal on the way in, not submitted to be refused** (`AC-127-C`, rule 6).
 * Choosing `Role.Hr` pins the department to `Department.Hr` and renders it as a fixed value with a
 * hint rather than as a select with one option (S-007). If the server refuses the pair anyway — a
 * stale bundle, a hand-made request — its own key is shown **against the department field**, not in a
 * form-level banner, because that is where the reader can act on it.
 *
 * **Both destructive acts state their consequence before they happen** (`AC-127-D`, `AC-127-E`,
 * rule 8). A role change and a deactivation each revoke every active project assignment (D-051 Q27,
 * D-049 ruling 5), and the confirmation names the projects — **counted and named by the server**, in
 * `UserSummary.activeProjectNames`, never derived here (S-008).
 *
 * **A deactivation reason is required by this screen** (rule 7). The server accepts a deactivation
 * without one; `AC-118-G` asserts the reason lands on the audit record verbatim, and a deactivation
 * with no stated reason is a trail row that answers "who and when" and not "why".
 *
 * **Nobody edits their own role and nobody deactivates themselves** (rule 9, spec.md §9). Those
 * controls are not rendered on the signed-in user's own record — hiding them is not the enforcement,
 * and the server is not asked to prove that here.
 *
 * **⚠️ The temporary password is typed, shown once, and written to nothing** (`AC-127-F`, rule 10).
 * `POST /api/users` **returns no password member at all**, so there is no response to echo: the one
 * moment the credential exists in the clear is while the Owner is typing it, and the reveal toggle is
 * that moment. On success the model is cleared and the component navigates away, so the value does
 * not survive to a second screen. It reaches no `localStorage` and no `sessionStorage` — D-050
 * prohibits both for the token and this is the credential that mints one.
 */
@Component({
  selector: 'kaff-user-form-page',
  imports: [FormField],
  templateUrl: './user-form-page.html',
  styleUrl: './user-form-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserFormPage {
  /** Absent when creating. The route supplies it when editing — `/users/:userId`. */
  readonly userId = input<string | undefined>(undefined);

  private readonly api = inject(UsersApi);
  private readonly clientsApi = inject(ClientsApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  private readonly model = signal<UserDraft>({
    fullName: '',
    userName: '',
    phone: '',
    email: '',
    role: 'SiteEngineer',
    department: 'Operations',
    operationsSubDepartment: 'Technical',
    clientId: '',
    temporaryPassword: '',
    confirmPassword: '',
  });

  private readonly loaded = signal<UserSummary | null>(null);
  private readonly loading = signal(false);
  private readonly refusalCode = signal<string | null>(null);
  private readonly refusalMessageKey = signal<string | null>(null);

  protected readonly i18n = inject(I18nService);
  protected readonly userForm = form(this.model, draft);

  protected readonly roles = ROLES;
  protected readonly departments = DEPARTMENTS;
  protected readonly subDepartments = SUB_DEPARTMENTS;
  protected readonly roleKey = roleKey;
  protected readonly departmentKey = departmentKey;
  protected readonly operationsSubDepartmentKey = operationsSubDepartmentKey;

  /**
   * The draft, for the three `<select>`s.
   *
   * **The selects are bound `[value]` + `(change)` rather than `[formField]`**, and the change is read
   * from the event **in this file** — `ux/rtl-and-i18n.md` §6 hard rule 4 and Nabil, 2026-08-28:
   * `$any` in a template is exactly what leaves `strictTemplates` nothing to check. Each handler
   * narrows the raw `string` the DOM gives back against the closed list it came from, so a value that
   * is not a member of the enum is discarded here rather than sent to the server as one.
   */
  protected readonly draft = this.model.asReadonly();

  protected readonly existing = this.loaded.asReadonly();
  protected readonly isLoading = this.loading.asReadonly();
  protected readonly clients = signal<readonly ClientSummary[]>([]);
  protected readonly passwordRevealed = signal(false);
  protected readonly confirming = signal<'role' | 'deactivate' | 'reactivate' | null>(null);
  protected readonly reason = signal('');
  protected readonly reactivationPassword = signal('');

  protected readonly isEdit = computed(() => this.userId() !== undefined);

  /** rule 9 — the Owner may not change his own role or deactivate himself. */
  protected readonly isSelf = computed(() => this.userId() === this.auth.current()?.userId);

  protected readonly isHr = computed(() => this.model().role === 'Hr');

  /** `Role.Client` and `Role.Subcontractor` hold no department at all — D-035, and it nearly leaked the portal. */
  protected readonly isExternal = computed(
    () => this.model().role === 'Client' || this.model().role === 'Subcontractor',
  );

  protected readonly isPortalClient = computed(() => this.model().role === 'Client');

  /** `User.SetPasswordHash` refuses a subcontractor, so the field is not rendered rather than disabled. */
  protected readonly canHoldPassword = computed(() => this.model().role !== 'Subcontractor');

  protected readonly needsSubDepartment = computed(() => this.model().department === 'Operations');

  /** `AC-127-C` — the server's key, rendered against the department field rather than in a banner. */
  protected readonly departmentRefusalKey = computed(() =>
    this.refusalCode() === HR_PAIR_REFUSAL ? this.refusalMessageKey() : null,
  );

  /** Everything else: a refusal that belongs to no single field. */
  protected readonly formRefusalKey = computed(() =>
    this.refusalCode() === HR_PAIR_REFUSAL ? null : this.refusalMessageKey(),
  );

  protected readonly passwordsMatch = computed(() => {
    const value = this.model();
    return value.temporaryPassword === value.confirmPassword;
  });

  protected readonly canSubmit = computed(
    () => this.userForm().valid() && !this.userForm().submitting() && this.passwordsMatch(),
  );

  /** The projects the pending act would revoke — the server's list, never counted here (S-008). */
  protected readonly revokedProjects = computed(() => this.loaded()?.activeProjectNames ?? []);

  constructor() {
    // The route's id is an input signal, so the load reacts to it rather than running once in a
    // constructor that would miss a navigation from one user's record straight to another's.
    effect(() => {
      const id = this.userId();

      if (id !== undefined) {
        void this.load(id);
      }
    });
  }

  protected readonly titleKey = computed(() =>
    this.isEdit() ? 'users.edit.title' : 'users.create.title',
  );

  /**
   * Keeps every role/department pairing legal on the way in.
   *
   * `User.ValidateDepartment` refuses four combinations and the server still refuses them — this does
   * not replace that guard. What it prevents is an operator being made to read a refusal to learn
   * that a field had to be emptied, which is the system asking a person to do its work (D-109 §1).
   */
  protected onRoleSelect(event: Event): void {
    const chosen = ROLES.find((role) => role === readSelect(event));

    if (chosen !== undefined) {
      this.onRoleChange(chosen);
    }
  }

  protected onDepartmentSelect(event: Event): void {
    const raw = readSelect(event);

    this.onDepartmentChange(DEPARTMENTS.find((department) => department === raw) ?? '');
  }

  protected onSubDepartmentSelect(event: Event): void {
    const raw = readSelect(event);
    const chosen = SUB_DEPARTMENTS.find((subDepartment) => subDepartment === raw);

    if (chosen !== undefined) {
      this.onSubDepartmentChange(chosen);
    }
  }

  protected onClientSelect(event: Event): void {
    this.onClientChange(readSelect(event));
  }

  protected onRoleChange(role: Role): void {
    this.model.update((current) => ({
      ...current,
      role,
      // rule 6 — HR is pinned to the HR department, because an HR user placed in
      // Operations/Administrative would inherit SiteExpenseConfirm through a department-only grant.
      department: role === 'Hr' ? 'Hr' : role === 'Client' || role === 'Subcontractor' ? '' : current.department,
      operationsSubDepartment:
        role === 'Hr' || role === 'Client' || role === 'Subcontractor'
          ? ''
          : current.operationsSubDepartment,
      clientId: role === 'Client' ? current.clientId : '',
      temporaryPassword: role === 'Subcontractor' ? '' : current.temporaryPassword,
      confirmPassword: role === 'Subcontractor' ? '' : current.confirmPassword,
    }));

    this.clearRefusal();

    if (role === 'Client' && this.clients().length === 0) {
      void this.loadClients();
    }
  }

  protected onDepartmentChange(department: Department | ''): void {
    this.model.update((current) => ({
      ...current,
      department,
      // spec.md §9: only Operations subdivides, and it must.
      operationsSubDepartment:
        department === 'Operations' ? current.operationsSubDepartment || 'Technical' : '',
    }));

    this.clearRefusal();
  }

  protected onSubDepartmentChange(subDepartment: OperationsSubDepartment): void {
    this.model.update((current) => ({ ...current, operationsSubDepartment: subDepartment }));
  }

  protected onClientChange(clientId: string): void {
    this.model.update((current) => ({ ...current, clientId }));
  }

  protected onTogglePasswordReveal(): void {
    this.passwordRevealed.update((revealed) => !revealed);
  }

  /**
   * The event is read here rather than in the template, because `$any` in a template is exactly what
   * leaves `strictTemplates` nothing to check (Nabil, 2026-08-28).
   */
  protected onReasonInput(event: Event): void {
    const target = event.target;
    this.reason.set(target instanceof HTMLTextAreaElement ? target.value : '');
  }

  protected onReactivationPasswordInput(event: Event): void {
    const target = event.target;
    this.reactivationPassword.set(target instanceof HTMLInputElement ? target.value : '');
  }

  protected onStartConfirm(kind: 'role' | 'deactivate' | 'reactivate'): void {
    this.reason.set('');
    this.reactivationPassword.set('');
    this.clearRefusal();
    this.confirming.set(kind);
  }

  protected onCancelConfirm(): void {
    this.confirming.set(null);
  }

  protected async onSubmitCreate(): Promise<void> {
    this.clearRefusal();

    await submit(this.userForm, async () => {
      const value = this.model();

      try {
        const created = await this.api.create({
          fullName: value.fullName.trim(),
          userName: value.userName.trim(),
          phone: value.phone.trim(),
          email: orNull(value.email),
          role: value.role,
          department: value.department === '' ? null : value.department,
          operationsSubDepartment:
            value.operationsSubDepartment === '' ? null : value.operationsSubDepartment,
          clientId: value.role === 'Client' ? orNull(value.clientId) : null,
          temporaryPassword: value.role === 'Subcontractor' ? null : orNull(value.temporaryPassword),
        });

        // AC-127-F. The credential is forgotten here, before the navigation rather than because of
        // it: a component destroyed by the router takes its signals with it, but relying on that
        // would make "never stored" a property of Angular's lifecycle instead of this file's.
        this.model.update((current) => ({ ...current, temporaryPassword: '', confirmPassword: '' }));
        this.passwordRevealed.set(false);

        await this.router.navigateByUrl(`/users/${created.id}`);
      } catch (error) {
        this.setRefusal(error);
      }

      return undefined;
    });
  }

  /** `AC-127-E`. The confirmation has already named the assignments; this is the act it described. */
  protected async onConfirmRoleChange(): Promise<void> {
    const id = this.userId();

    if (id === undefined) {
      return;
    }

    try {
      await this.api.changeRole(id, this.model().role);
      this.confirming.set(null);
      await this.load(id);
    } catch (error) {
      this.setRefusal(error);
      this.confirming.set(null);
    }
  }

  protected async onMoveDepartment(): Promise<void> {
    const id = this.userId();

    if (id === undefined) {
      return;
    }

    this.clearRefusal();

    const value = this.model();

    try {
      await this.api.moveDepartment(
        id,
        value.department === '' ? null : value.department,
        value.operationsSubDepartment === '' ? null : value.operationsSubDepartment,
      );
      await this.load(id);
    } catch (error) {
      this.setRefusal(error);
    }
  }

  /** `AC-127-D`. The reason is required by the screen and stored verbatim (rule 7, `AC-118-G`). */
  protected async onConfirmDeactivate(): Promise<void> {
    const id = this.userId();

    if (id === undefined || this.reason().trim().length === 0) {
      return;
    }

    try {
      await this.api.deactivate(id, this.reason().trim());
      this.confirming.set(null);
      await this.load(id);
    } catch (error) {
      this.setRefusal(error);
      this.confirming.set(null);
    }
  }

  protected async onConfirmReactivate(): Promise<void> {
    const id = this.userId();

    if (id === undefined || this.reason().trim().length === 0) {
      return;
    }

    try {
      await this.api.reactivate(id, orNull(this.reactivationPassword()), this.reason().trim());
      this.confirming.set(null);
      this.reactivationPassword.set('');
      await this.load(id);
    } catch (error) {
      this.setRefusal(error);
      this.confirming.set(null);
    }
  }

  /**
   * Loads the record S-008 edits.
   *
   * **From the list, not from a `GET /api/users/{id}` that does not exist.** `UserSummary` already
   * carries every member this screen edits *and* the assignment names the confirmations need, so a
   * second endpoint would be a second whitelist to keep narrow for no field the first one lacks —
   * the opposite call to D-113 §1's, made on the opposite fact.
   */
  private async load(id: string): Promise<void> {
    this.loading.set(true);
    this.clearRefusal();

    try {
      const user = (await this.api.list()).find((candidate) => candidate.id === id) ?? null;

      this.loaded.set(user);

      if (user !== null) {
        this.model.set({
          fullName: user.fullName,
          userName: user.userName,
          phone: user.phone,
          email: '',
          role: user.role,
          department: user.department ?? '',
          operationsSubDepartment: user.operationsSubDepartment ?? '',
          clientId: '',
          temporaryPassword: '',
          confirmPassword: '',
        });
      } else {
        this.refusalCode.set('identity.user_not_found');
        this.refusalMessageKey.set('errors.identity.user_not_found');
      }
    } catch (error) {
      this.setRefusal(error);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadClients(): Promise<void> {
    try {
      this.clients.set(await this.clientsApi.list('', 'active'));
    } catch {
      // A portal user cannot be created without a client, and the refusal that says so is the
      // server's — `errors.identity.client_user_requires_client`. An empty picker is honest about
      // having nothing to offer; inventing a fallback here would not be.
      this.clients.set([]);
    }
  }

  private setRefusal(error: unknown): void {
    const problem = toProblem(error);
    this.refusalCode.set(problem.code);
    this.refusalMessageKey.set(problem.messageKey);
  }

  private clearRefusal(): void {
    this.refusalCode.set(null);
    this.refusalMessageKey.set(null);
  }

  protected trackClient(_index: number, client: ClientSummary): string {
    return client.id;
  }
}
