import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
} from '@angular/core';

import { toProblem } from '../../../core/api/problem-details';
import {
  BabOption,
  EmployeeCreate,
  EmployeeFile,
  EmployeeKind,
  EmployeeSummary,
  EmployeesApi,
} from '../../../core/employees/employees.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { DuplicatePhoneWarning } from '../../../shared/duplicate-phone-warning/duplicate-phone-warning';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffFigurePair } from '../../../shared/kaff-figure-pair/kaff-figure-pair';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffSelect, SelectOption } from '../../../shared/kaff-select/kaff-select';
import { PhoneMatch } from '../../../shared/phone-match';

/** What the panel is open on: a blank register form, or one person's file with the list row it came from. */
export type EmployeePanelTarget =
  | { readonly mode: 'create' }
  | { readonly mode: 'edit'; readonly file: EmployeeFile; readonly summary: EmployeeSummary };

interface Draft {
  readonly fullName: string;
  readonly phone: string;
  readonly babId: string;
  readonly specialty: string;
  readonly nationalId: string;
  readonly jobTitle: string;
  readonly department: string;
  readonly hiredOn: string;
}

const BLANK: Draft = {
  fullName: '',
  phone: '',
  babId: '',
  specialty: '',
  nationalId: '',
  jobTitle: '',
  department: '',
  hiredOn: '',
};

const PHONE_TAKEN = 'errors.master.employee_phone_taken';
const NAME_REQUIRED = 'errors.master.name_required';
const NEEDS_ACK = 'master.duplicate_phone_not_acknowledged';

interface ServerError {
  readonly key: string;
  /** The phone as typed when the server refused, so a refusal does not follow a changed number. */
  readonly phone: string;
}

/** Blank means absent. The server trims and nulls too; this keeps the payload honest on the way out. */
function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

/**
 * Register and edit one person, in the side panel (a bottom sheet on a phone) over the list. D-231, D-232,
 * `people.html`. The list the person came from fixes `kind`: there is no kind field, no code and no rate.
 *
 * A salaried person has the name, phone, national id, job title, department and hire date; a day-labour
 * worker has the name, phone, باب (required) and specialty. The save is never disabled for a fixable gap
 * (D-229 item 3): pressing it marks the field. Edit's save is disabled only while nothing has changed.
 *
 * A phone another person already holds is a warning with a checkbox, except a new salaried person against
 * an active salaried one, which the server refuses on the phone field with no way past it.
 */
@Component({
  selector: 'kaff-employee-panel',
  imports: [
    DuplicatePhoneWarning,
    KaffBadge,
    KaffBanner,
    KaffButton,
    KaffDetailPanel,
    KaffField,
    KaffFigurePair,
    KaffIcon,
    KaffSelect,
  ],
  templateUrl: './employee-panel.html',
  styleUrl: './employee-panel.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeePanel {
  readonly kind = input.required<EmployeeKind>();
  /** `null` is closed. */
  readonly target = input<EmployeePanelTarget | null>(null);
  readonly babs = input<readonly BabOption[]>([]);

  /** The saved file, after a create or an edit. */
  readonly saved = output<EmployeeFile>();
  readonly closed = output<void>();
  readonly archiveRequested = output<void>();
  readonly unarchiveRequested = output<void>();

  protected readonly i18n = inject(I18nService);
  private readonly api = inject(EmployeesApi);

  protected readonly open = computed(() => this.target() !== null);
  protected readonly isEdit = computed(() => this.target()?.mode === 'edit');
  protected readonly isWorker = computed(() => this.kind() === 'DayLabour');

  private readonly loaded = computed<Draft>(() => {
    const target = this.target();
    if (target === null || target.mode === 'create') {
      return BLANK;
    }
    const file = target.file;
    return {
      fullName: file.fullName,
      phone: file.phone,
      babId: file.babId ?? '',
      specialty: file.specialty ?? '',
      nationalId: file.nationalId ?? '',
      jobTitle: file.jobTitle ?? '',
      department: file.department ?? '',
      hiredOn: file.hiredOn ?? '',
    };
  });

  protected readonly draft = linkedSignal<EmployeePanelTarget | null, Draft>({
    source: this.target,
    computation: () => ({ ...this.loaded() }),
  });
  /** Errors appear only after a save was pressed, so a half-typed value is not scolded. */
  protected readonly attempted = linkedSignal<EmployeePanelTarget | null, boolean>({
    source: this.target,
    computation: () => false,
  });
  protected readonly saving = signal(false);

  private readonly matches = linkedSignal<EmployeePanelTarget | null, readonly PhoneMatch[]>({
    source: this.target,
    computation: () => [],
  });
  protected readonly acknowledged = linkedSignal<EmployeePanelTarget | null, boolean>({
    source: this.target,
    computation: () => false,
  });
  protected readonly ackMissing = linkedSignal<EmployeePanelTarget | null, boolean>({
    source: this.target,
    computation: () => false,
  });
  private readonly serverError = linkedSignal<EmployeePanelTarget | null, ServerError | null>({
    source: this.target,
    computation: () => null,
  });

  protected readonly title = computed(() => {
    const target = this.target();
    if (target === null) {
      return '';
    }
    if (target.mode === 'edit') {
      return target.file.fullName;
    }
    return this.i18n.t(this.isWorker() ? 'hr.worker.action.register' : 'hr.employee.action.create');
  });

  protected readonly subline = computed(() => {
    const target = this.target();
    if (target === null || target.mode === 'create') {
      return '';
    }
    const summary = target.summary;
    return this.isWorker() ? this.babLabelFor(summary.babNameAr, summary.babNameEn) : (summary.jobTitle ?? '');
  });

  protected readonly archived = computed(() => {
    const target = this.target();
    return target?.mode === 'edit' && !target.file.isActive;
  });

  protected readonly figures = computed(() => {
    const target = this.target();
    if (target === null || target.mode === 'create') {
      return null;
    }
    const { timesEngaged, averageRating } = target.summary;
    return {
      times: String(timesEngaged ?? 0),
      rating: averageRating === null ? '—' : `${Number(averageRating).toFixed(1)}/5`,
    };
  });

  protected readonly babOptions = computed<readonly SelectOption[]>(() => {
    const current = this.draft().babId;
    return [
      { value: '', label: this.i18n.t('people.field.bab_placeholder') },
      ...this.babs()
        .filter((bab) => bab.isActive || bab.id === current)
        .map((bab) => ({ value: bab.id, label: this.babLabelFor(bab.nameAr, bab.nameEn) })),
    ];
  });

  protected readonly dirty = computed(() => {
    const now = this.draft();
    const was = this.loaded();
    return (Object.keys(now) as (keyof Draft)[]).some((key) => now[key] !== was[key]);
  });

  /** A refusal nobody can tick past replaces the warning: showing both would offer a way through. */
  protected readonly duplicates = computed(() => {
    const refused = this.serverError();
    return refused !== null && refused.key === PHONE_TAKEN && refused.phone === this.draft().phone
      ? []
      : this.matches();
  });

  protected readonly nameError = computed(() =>
    this.attempted() && this.draft().fullName.trim().length === 0 ? NAME_REQUIRED : null,
  );

  protected readonly phoneError = computed<string | null>(() => {
    if (this.attempted() && this.draft().phone.trim().length === 0) {
      return 'errors.phone.required';
    }
    const refused = this.serverError();
    if (refused === null || refused.phone !== this.draft().phone) {
      return null;
    }
    if (refused.key === PHONE_TAKEN) {
      return PHONE_TAKEN;
    }
    return refused.key.startsWith('errors.phone.') ? 'people.error.phone_invalid' : null;
  });

  protected readonly babError = computed(() =>
    this.attempted() && this.isWorker() && this.draft().babId === ''
      ? 'errors.master.day_labour_requires_trade'
      : null,
  );

  /** A refusal that is not already drawn on a field. */
  protected readonly bannerRefusal = computed(() => {
    const key = this.serverError()?.key ?? null;
    return key !== null && key !== PHONE_TAKEN && key !== NAME_REQUIRED && !key.startsWith('errors.phone.')
      ? key
      : null;
  });

  protected readonly hasFieldErrors = computed(
    () => this.nameError() !== null || this.phoneError() !== null || this.babError() !== null,
  );

  protected onInput(field: keyof Draft, event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.draft.update((current) => ({ ...current, [field]: value }));
  }

  protected onBab(babId: string): void {
    this.draft.update((current) => ({ ...current, babId }));
  }

  protected onAcknowledge(acknowledged: boolean): void {
    this.acknowledged.set(acknowledged);
    this.ackMissing.set(false);
  }

  /** The warning's data: who holds the number now. Fires on blur of the phone field. */
  protected async onPhoneBlur(): Promise<void> {
    await this.refreshMatches();
  }

  private async refreshMatches(): Promise<void> {
    const phone = this.draft().phone.trim();
    if (phone.length === 0) {
      this.matches.set([]);
      return;
    }
    const target = this.target();
    const ownId = target?.mode === 'edit' ? target.file.id : null;
    try {
      // Editing someone whose phone has not changed must not warn about himself.
      this.matches.set((await this.api.phoneCheck(phone)).filter((match) => match.id !== ownId));
    } catch {
      // A failed check must not stop a save: the server runs the match again on submit.
      this.matches.set([]);
    }
  }

  protected async onSave(): Promise<void> {
    this.attempted.set(true);
    this.serverError.set(null);

    if (this.hasFieldErrors()) {
      return;
    }
    if (this.duplicates().length > 0 && !this.acknowledged()) {
      this.ackMissing.set(true);
      return;
    }

    const target = this.target();
    if (target === null) {
      return;
    }
    this.saving.set(true);
    try {
      const payload = this.payload(target);
      const file =
        target.mode === 'edit' ? await this.api.edit(target.file.id, payload) : await this.api.create(payload);
      this.saved.emit(file);
    } catch (error) {
      const problem = toProblem(error);
      if (problem.code === NEEDS_ACK) {
        // Not a failure: the server is asking. Show who holds the number now and wait for the box.
        await this.refreshMatches();
        this.acknowledged.set(false);
        this.ackMissing.set(this.duplicates().length > 0);
      } else {
        this.serverError.set({ key: problem.messageKey, phone: this.draft().phone });
      }
    } finally {
      this.saving.set(false);
    }
  }

  private payload(target: EmployeePanelTarget): EmployeeCreate {
    const draft = this.draft();
    const worker = this.isWorker();
    return {
      fullName: draft.fullName.trim(),
      phone: draft.phone.trim(),
      // Fixed by the list the person is on, and on edit by the stored value: never chosen by the user.
      kind: target.mode === 'edit' ? target.file.kind : this.kind(),
      babId: worker && draft.babId.length > 0 ? draft.babId : null,
      specialty: worker ? orNull(draft.specialty) : null,
      nationalId: worker ? null : orNull(draft.nationalId),
      jobTitle: worker ? null : orNull(draft.jobTitle),
      department: worker ? null : orNull(draft.department),
      hiredOn: !worker && draft.hiredOn.length > 0 ? draft.hiredOn : null,
      acknowledgedDuplicatePhone: this.acknowledged(),
    };
  }

  /** Arabic name in Arabic, English name in English; never both. */
  private babLabelFor(nameAr: string | null, nameEn: string | null): string {
    return (this.i18n.locale() === 'en' ? nameEn : nameAr) ?? '';
  }
}
