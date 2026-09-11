import { ChangeDetectionStrategy, Component, HostListener, computed, effect, inject, input, signal } from '@angular/core';
import { FormField, form, required, schema, submit, validate } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { BabOption, EmployeeCreate, EmployeeFile, EmployeeKind, EmployeesApi } from '../../../core/employees/employees.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { UnsavedChangesAware } from '../../../core/navigation/unsaved-changes.guard';
import { DuplicatePhoneWarning } from '../../../shared/duplicate-phone-warning/duplicate-phone-warning';
import { PhoneMatch } from '../../../shared/phone-match';

interface EmployeeDraft {
  fullName: string;
  phone: string;
  kind: EmployeeKind;
  babId: string;
  specialty: string;
  nationalId: string;
  jobTitle: string;
  department: string;
  hiredOn: string;
}

const BLANK_DRAFT: EmployeeDraft = {
  fullName: '',
  phone: '',
  kind: 'Salaried',
  babId: '',
  specialty: '',
  nationalId: '',
  jobTitle: '',
  department: '',
  hiredOn: '',
};

/**
 * KAFF-207 rule 3, the database check constraint's client-side echo: a day labourer must name a باب.
 * Exported and pure so `employee-form-page.spec.ts` pins it without booting the signal-forms machinery
 * — `AC-207-B` still refuses server-side either way; this only saves the round trip.
 */
export function babRequiredButMissing(kind: EmployeeKind, babId: string): boolean {
  return kind === 'DayLabour' && babId.trim().length === 0;
}

/** `fullName` and `phone` required — `Employee.Create`'s own guards. */
const draft = schema<EmployeeDraft>((path) => {
  required(path.fullName);
  required(path.phone);
  validate(path.babId, (ctx) =>
    babRequiredButMissing(ctx.valueOf(path.kind), ctx.value()) ? { kind: 'day_labour_requires_bab' } : undefined,
  );
});

/**
 * `S-024` · create and edit one employee, one component — `KAFF-207`, `KAFF-208`.
 *
 * **The code is generated and shown read-only on edit** — D-130 §6, `AC-207-G`. There is no code field
 * on create.
 *
 * **`kind` is chosen once, on create, and frozen on edit** — KAFF-208 rule 3/D-130 §7: no `SetKind`
 * exists and this form does not invent one. The edit screen renders the stored kind as text and always
 * resubmits it unchanged; `EditEmployee.Handler` refuses a request that names a different one
 * (`errors.master.employee_kind_immutable`, `AC-208-C`) before this form could ever reach it, because
 * the value it sends back is never anything else.
 *
 * **Edit loads through `GET /api/employees/{id}` (`EmployeesApi.get`)**, which returns the same
 * `EmployeeFile` shape `create`/`edit` do — `nationalId`, `jobTitle` and `hiredOn` included — so the
 * two gaps `employees.api.ts` used to report here are closed, D-137.
 *
 * **The باب picker loads from `EmployeesApi.listBabOptions()`, not `BabsApi`** — D-137: a lookup
 * scoped to the employees feature, gated `EmployeeManage`, carrying no markup. It offers active أبواب
 * for a new selection; on edit, an already-assigned archived باب still shows by name (`babs`'s
 * `computed`, below, keeps whichever option is the current `babId` regardless of `isActive`).
 * `hr.employee.babs_unavailable` stays for a network failure — the permission gap D-137 closed no
 * longer applies.
 */
@Component({
  selector: 'kaff-employee-form-page',
  imports: [FormField, RouterLink, DuplicatePhoneWarning],
  templateUrl: './employee-form-page.html',
  styleUrl: './employee-form-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeeFormPage implements UnsavedChangesAware {
  /** Absent when creating. The route supplies it when editing — `/employees/:employeeId`. */
  readonly employeeId = input<string | undefined>(undefined);

  private readonly api = inject(EmployeesApi);
  private readonly router = inject(Router);

  protected readonly i18n = inject(I18nService);

  private readonly model = signal<EmployeeDraft>({ ...BLANK_DRAFT });
  private readonly pristine = signal<EmployeeDraft | null>(null);
  private readonly code = signal<string | null>(null);
  private readonly loadedKind = signal<EmployeeKind | null>(null);

  private readonly matches = signal<readonly PhoneMatch[]>([]);
  private readonly acknowledged = signal(false);
  protected readonly duplicateMatches = this.matches.asReadonly();

  protected readonly employeeForm = form(this.model, draft);

  private readonly allBabs = signal<readonly BabOption[]>([]);
  protected readonly babsUnavailable = signal(false);

  /**
   * Active أبواب, plus the currently selected one even when archived — so an edit screen still names
   * an already-assigned archived باب, while a fresh create only ever offers active options (D-137).
   */
  protected readonly babs = computed(() => {
    const currentBabId = this.model().babId;
    return this.allBabs().filter((bab) => bab.isActive || bab.id === currentBabId);
  });

  protected readonly refusal = signal<string | null>(null);
  protected readonly loadFailed = signal(false);

  protected readonly isEdit = computed(() => this.employeeId() !== undefined);
  protected readonly titleKey = computed(() => (this.isEdit() ? 'hr.employee.edit_title' : 'hr.employee.create_title'));
  protected readonly canSubmit = computed(() => this.employeeForm().valid() && !this.employeeForm().submitting());
  protected readonly displayCode = computed(() => this.code());
  protected readonly isDayLabour = computed(() => this.model().kind === 'DayLabour');

  protected readonly babIdErrorKey = computed(() => {
    const field = this.employeeForm.babId();
    if (!field.touched() || field.valid()) {
      return null;
    }
    return field.errors().some((error) => error.kind === 'day_labour_requires_bab')
      ? 'errors.master.day_labour_requires_trade'
      : null;
  });

  constructor() {
    void this.loadBabs();

    // Same deferred-write shape as `bab-form-page.ts` and `catalogue-form-page.ts`: writing `model`
    // synchronously during the component's first render pass reassigns the signal `form(this.model)`
    // wraps out from under `FormField` mid-init.
    effect(() => {
      const id = this.employeeId();

      if (id === undefined) {
        void Promise.resolve().then(() => {
          if (this.pristine() === null) {
            this.pristine.set({ ...BLANK_DRAFT });
          }
        });
        return;
      }

      void this.load(id);
    });
  }

  hasUnsavedChanges(): boolean {
    const baseline = this.pristine();
    return baseline !== null && JSON.stringify(baseline) !== JSON.stringify(this.model());
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) {
      event.preventDefault();
    }
  }

  /**
   * `S-024`'s warning. Fires on blur of the phone field, mirroring `S-013`'s client form
   * (decisions.md D-141 §5).
   */
  protected async onPhoneBlur(): Promise<void> {
    await this.refreshMatches();
  }

  /** The operator saying "I saw who holds this number and I am proceeding anyway." */
  protected onAcknowledgeChange(acknowledged: boolean): void {
    this.acknowledged.set(acknowledged);
  }

  private async refreshMatches(): Promise<void> {
    const phone = this.model().phone.trim();

    if (phone.length === 0) {
      this.matches.set([]);
      return;
    }

    try {
      const found = await this.api.phoneCheck(phone);

      // Editing an employee whose phone has not changed must not warn about itself.
      this.matches.set(found.filter((match) => match.id !== this.employeeId()));
    } catch {
      // A failed check must not stop a save — the server re-runs the match anyway on submit.
      this.matches.set([]);
    }
  }

  protected onKindChange(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLSelectElement) {
      const kind = target.value === 'DayLabour' ? 'DayLabour' : 'Salaried';
      this.model.update((current) => ({ ...current, kind, babId: kind === 'DayLabour' ? current.babId : '' }));
    }
  }

  protected onBabChange(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLSelectElement) {
      this.model.update((current) => ({ ...current, babId: target.value }));
    }
  }

  protected async onSubmit(): Promise<void> {
    this.refusal.set(null);

    await submit(this.employeeForm, async () => {
      try {
        const payload = this.payload();

        if (this.isEdit()) {
          const saved = await this.api.edit(this.employeeId()!, payload);
          this.applyLoaded(saved);
        } else {
          const created = await this.api.create(payload);
          this.applyLoaded(created);
          await this.router.navigateByUrl(`/employees/${created.id}`);
        }
      } catch (error) {
        const problem = toProblem(error);

        if (problem.code === 'master.duplicate_phone_not_acknowledged') {
          // Not a failure — the server is asking. Re-run the check and show current matches rather
          // than the stale ones the operator already dismissed (mirrors the client form, D-141 §5).
          await this.refreshMatches();
          this.acknowledged.set(false);
          return undefined;
        }

        // Includes `errors.master.employee_phone_taken` — D-146 point 3's salaried-to-salaried
        // refusal, never acknowledgeable, so it renders as a plain refusal with no confirm offered.
        this.refusal.set(problem.messageKey);
      }

      return undefined;
    });
  }

  protected trackBab(_index: number, bab: BabOption): string {
    return bab.id;
  }

  /** Arabic name in Arabic, English name in English — never both, never the raw field. */
  protected babLabel(bab: BabOption): string {
    return this.i18n.locale() === 'en' ? bab.nameEn : bab.nameAr;
  }

  private applyLoaded(employee: EmployeeFile): void {
    this.code.set(employee.code);
    this.loadedKind.set(employee.kind);

    const loaded: EmployeeDraft = {
      fullName: employee.fullName,
      phone: employee.phone,
      kind: employee.kind,
      babId: employee.babId ?? '',
      specialty: employee.specialty ?? '',
      nationalId: employee.nationalId ?? '',
      jobTitle: employee.jobTitle ?? '',
      department: employee.department ?? '',
      hiredOn: employee.hiredOn ?? '',
    };

    this.model.set(loaded);
    this.pristine.set(loaded);
    this.loadFailed.set(false);
  }

  private payload(): EmployeeCreate {
    const value = this.model();
    // `kind` is always the loaded value on edit — see the class doc. `Employee.Kind` at BLANK_DRAFT's
    // default (`Salaried`) is what create sends when the operator has not touched the picker.
    const kind = this.isEdit() ? (this.loadedKind() ?? value.kind) : value.kind;

    return {
      fullName: value.fullName.trim(),
      phone: value.phone.trim(),
      kind,
      babId: kind === 'DayLabour' && value.babId.length > 0 ? value.babId : null,
      specialty: orNull(value.specialty),
      nationalId: orNull(value.nationalId),
      jobTitle: orNull(value.jobTitle),
      department: orNull(value.department),
      hiredOn: value.hiredOn.length > 0 ? value.hiredOn : null,
      acknowledgedDuplicatePhone: this.acknowledged(),
    };
  }

  /** `S-024`'s edit load — `GET /api/employees/{id}`, D-137. `404` carries `errors.master.employee_not_found`. */
  private async load(id: string): Promise<void> {
    try {
      this.applyLoaded(await this.api.get(id));
    } catch (error) {
      const problem = toProblem(error);
      if (problem.status === 404) {
        this.loadFailed.set(true);
      } else {
        this.refusal.set(problem.messageKey);
      }
    }
  }

  /** D-137's lookup. A network failure — not a permission refusal, this endpoint matches HR's gate — degrades the picker. */
  private async loadBabs(): Promise<void> {
    try {
      this.allBabs.set(await this.api.listBabOptions());
    } catch {
      this.allBabs.set([]);
      this.babsUnavailable.set(true);
    }
  }
}

/** Blank means absent. The server trims and nulls too; this keeps the payload honest on the way out. */
function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}
