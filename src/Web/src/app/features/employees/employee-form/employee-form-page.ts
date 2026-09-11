import { ChangeDetectionStrategy, Component, HostListener, computed, effect, inject, input, signal } from '@angular/core';
import { FormField, form, required, schema, submit, validate } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { Bab, BabsApi } from '../../../core/catalogue/babs.api';
import { EmployeeCreate, EmployeeKind, EmployeeSummary, EmployeesApi } from '../../../core/employees/employees.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { UnsavedChangesAware } from '../../../core/navigation/unsaved-changes.guard';

interface EmployeeDraft {
  fullName: string;
  phone: string;
  kind: EmployeeKind;
  babId: string;
  specialty: string;
  nationalId: string;
  jobTitle: string;
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
 * ⛔ **A reported gap, not a workaround: this form cannot safely pre-fill `nationalId`/`jobTitle`/
 * `hiredOn` on edit.** `GET /api/employees` (the only read this feature has — there is no
 * `GET /api/employees/{id}`) returns `EmployeeSummary`, which does not carry those three fields at
 * all, and `EditEmployee` is a full-body `PUT`: `EditEmployee.Handler` calls
 * `employee.SetStaffDetails(request.NationalId, request.JobTitle, request.HiredOn)` unconditionally,
 * so submitting a blank value for a field this screen could not load **overwrites the stored one with
 * null**. Rather than submit stale data silently, the edit form renders those three fields blank with
 * a visible notice (`hr.employee.staff_fields_not_returned_notice`) so HR is not surprised by a value
 * disappearing. Fixing this for real needs `GET /api/employees/{id}`, or those three fields added to
 * `EmployeeSummary` — Backend's, not this screen's, and flagged in the session report.
 *
 * ⛔ **A second reported gap: `GET /api/babs` may 403 for HR.** `Permission.BabManage` is granted to
 * `Role.Owner` and `Role.TechnicalOffice` only [Verified @ `PermissionCatalogue.cs`] — `Role.Hr` is not
 * in that list, even though KAFF-207 rule 3 has a day labourer name a باب picked from exactly that
 * endpoint. The Owner's own session is unaffected; an HR session hits the catch below and the باب
 * picker degrades to a disabled field with `hr.employee.babs_unavailable`, rather than the whole form
 * failing to load. Flagged in the session report — either `BabManage` grows a read grant for `Hr`, or a
 * narrower "read أبواب" permission is introduced.
 */
@Component({
  selector: 'kaff-employee-form-page',
  imports: [FormField, RouterLink],
  templateUrl: './employee-form-page.html',
  styleUrl: './employee-form-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeeFormPage implements UnsavedChangesAware {
  /** Absent when creating. The route supplies it when editing — `/employees/:employeeId`. */
  readonly employeeId = input<string | undefined>(undefined);

  private readonly api = inject(EmployeesApi);
  private readonly babsApi = inject(BabsApi);
  private readonly router = inject(Router);

  protected readonly i18n = inject(I18nService);

  private readonly model = signal<EmployeeDraft>({ ...BLANK_DRAFT });
  private readonly pristine = signal<EmployeeDraft | null>(null);
  private readonly code = signal<string | null>(null);
  private readonly loadedKind = signal<EmployeeKind | null>(null);

  protected readonly employeeForm = form(this.model, draft);

  protected readonly babs = signal<readonly Bab[]>([]);
  protected readonly babsUnavailable = signal(false);

  protected readonly refusal = signal<string | null>(null);
  protected readonly loadFailed = signal(false);
  private readonly employeesLoaded = signal(false);

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
        this.refusal.set(toProblem(error).messageKey);
      }

      return undefined;
    });
  }

  protected trackBab(_index: number, bab: Bab): string {
    return bab.id;
  }

  /** Arabic name in Arabic, English name in English — never both, never the raw field. */
  protected babLabel(bab: Bab): string {
    return this.i18n.locale() === 'en' ? bab.nameEn : bab.nameAr;
  }

  private applyLoaded(employee: EmployeeSummary & { readonly nationalId?: string | null; readonly jobTitle?: string | null; readonly hiredOn?: string | null }): void {
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
      hiredOn: value.hiredOn.length > 0 ? value.hiredOn : null,
    };
  }

  /**
   * `S-024`'s load. **No `GET /api/employees/{id}` exists** — see the class doc's first reported gap.
   * `GET /api/employees` (`filter: 'all'`, so an archived record can still be opened) returns every
   * row, so the record is found in that list the same way `bab-form-page.ts` finds a باب — but the row
   * it finds is `EmployeeSummary`, missing the three staff-only fields, which is exactly the gap.
   */
  private async load(id: string): Promise<void> {
    try {
      const items = await this.api.list('all');
      const found = items.find((employee) => employee.id === id);

      if (found) {
        this.applyLoaded(found);
      } else if (this.employeesLoaded()) {
        this.loadFailed.set(true);
      }
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
    } finally {
      this.employeesLoaded.set(true);
    }
  }

  private async loadBabs(): Promise<void> {
    try {
      this.babs.set(await this.babsApi.list('active'));
    } catch {
      // The second reported gap in the class doc: `BabManage` may refuse an HR session outright. The
      // form still renders; the باب picker degrades instead of the whole screen failing.
      this.babs.set([]);
      this.babsUnavailable.set(true);
    }
  }
}

/** Blank means absent. The server trims and nulls too; this keeps the payload honest on the way out. */
function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}
