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
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { Bab, BabsApi } from '../../../core/catalogue/babs.api';
import { fractionToPercent, PERCENT_INPUT_PATTERN } from '../../../core/catalogue/percent-wire';
import { I18nService } from '../../../core/i18n/i18n.service';
import {
  SubcontractorFile,
  SubcontractorWrite,
  SubcontractorsApi,
} from '../../../core/subcontractors/subcontractors.api';
import { DuplicatePhoneWarning } from '../../../shared/duplicate-phone-warning/duplicate-phone-warning';
import { PhoneMatch } from '../../../shared/phone-match';

interface SubcontractorDraft {
  name: string;
  phone: string;
  tradeBabId: string;
  /** A percent an operator reads, e.g. `"5"` — the wire's own unit here (AC-211-C), not a fraction. */
  retentionPercent: string;
}

const DEFAULT_RETENTION_PERCENT = '5';

const BLANK_DRAFT: SubcontractorDraft = {
  name: '',
  phone: '',
  tradeBabId: '',
  retentionPercent: DEFAULT_RETENTION_PERCENT,
};

/**
 * `name` and `phone` required — `Subcontractor.Create`'s own guards. `retentionPercent` accepts the
 * same shape a markup percent does — no negative, no exponent (`Percentage` throws on either).
 */
const draft = schema<SubcontractorDraft>((path) => {
  required(path.name);
  required(path.phone);
});

/**
 * `S-029` · create, edit and the duplicate-phone warning, one component. KAFF-211.
 *
 * **No rate-card field of any kind — D-139 §4, AC-211-H.** Agreed rates live on each job's sub-BOQ,
 * slice 4/5, never on this profile. **No withholding rate or category — D-139 §5, AC-211-G.** Set per
 * contract/job, `KAFF-318`'s ground, not here.
 *
 * **The tax registration number is read-only, shown on edit only, and this form sends no such
 * member.** D-147 point 3 / AC-211-N: the Technical Office holds `SubcontractorManage` and not
 * `SubcontractorTaxRegistrationEdit`, so `SubcontractorWrite` carries no field it could use to write
 * one even if this screen tried — Finance's own screen for that route is HELD, `AC-211-O`, and is not
 * built here.
 *
 * **The phone is entered first and checked on blur**, mirroring `S-013`/`S-024`'s presentation
 * (D-141) — a repeated number warns and is acknowledged, never refused (AC-211-I).
 */
@Component({
  selector: 'kaff-subcontractor-form-page',
  imports: [FormField, RouterLink, DuplicatePhoneWarning],
  templateUrl: './subcontractor-form-page.html',
  styleUrl: './subcontractor-form-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubcontractorFormPage {
  /** Absent when creating. The route supplies it when editing — `/subcontractors/:subcontractorId`. */
  readonly subcontractorId = input<string | undefined>(undefined);

  private readonly api = inject(SubcontractorsApi);
  private readonly babsApi = inject(BabsApi);
  private readonly router = inject(Router);

  protected readonly i18n = inject(I18nService);

  private readonly model = signal<SubcontractorDraft>({ ...BLANK_DRAFT });
  private readonly code = signal<string | null>(null);
  private readonly taxRegistrationNumber = signal<string | null>(null);
  private readonly archived = signal(false);
  protected readonly confirmingArchive = signal(false);

  private readonly matches = signal<readonly PhoneMatch[]>([]);
  private readonly acknowledged = signal(false);
  protected readonly duplicateMatches = this.matches.asReadonly();

  protected readonly subcontractorForm = form(this.model, draft);

  private readonly allBabs = signal<readonly Bab[]>([]);
  protected readonly babsUnavailable = signal(false);

  /** Active أبواب, plus the currently selected one even when archived (same reasoning as D-137). */
  protected readonly babs = computed(() => {
    const currentBabId = this.model().tradeBabId;
    return this.allBabs().filter((bab) => bab.isActive || bab.id === currentBabId);
  });

  protected readonly refusal = signal<string | null>(null);
  protected readonly loadFailed = signal(false);

  protected readonly isEdit = computed(() => this.subcontractorId() !== undefined);
  protected readonly titleKey = computed(() =>
    this.isEdit() ? 'subcontractor.edit_title' : 'subcontractor.create_title',
  );
  protected readonly canSubmit = computed(
    () => this.subcontractorForm().valid() && !this.subcontractorForm().submitting(),
  );
  protected readonly displayCode = computed(() => this.code());
  protected readonly displayTaxRegistrationNumber = computed(() => this.taxRegistrationNumber());

  protected readonly retentionErrorKey = computed(() => {
    const field = this.subcontractorForm.retentionPercent();
    if (!field.touched() || field.valid()) {
      return null;
    }
    return PERCENT_INPUT_PATTERN.test(field.value().trim())
      ? null
      : 'bab.field.markup_format_invalid';
  });

  constructor() {
    void this.loadBabs();

    effect(() => {
      const id = this.subcontractorId();

      if (id === undefined) {
        return;
      }

      void this.load(id);
    });
  }

  protected async onPhoneBlur(): Promise<void> {
    await this.refreshMatches();
  }

  protected onAcknowledgeChange(acknowledged: boolean): void {
    this.acknowledged.set(acknowledged);
  }

  protected onBabChange(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLSelectElement) {
      this.model.update((current) => ({ ...current, tradeBabId: target.value }));
    }
  }

  protected trackBab(_index: number, bab: Bab): string {
    return bab.id;
  }

  protected babLabel(bab: Bab): string {
    return this.i18n.locale() === 'en' ? bab.nameEn : bab.nameAr;
  }

  protected onStartArchive(): void {
    this.confirmingArchive.set(true);
  }

  protected onCancelArchive(): void {
    this.confirmingArchive.set(false);
  }

  protected async onArchive(): Promise<void> {
    const id = this.subcontractorId();

    if (id === undefined) {
      return;
    }

    this.refusal.set(null);

    try {
      await this.api.archive(id);
      this.archived.set(true);
      this.confirmingArchive.set(false);
      await this.router.navigateByUrl('/subcontractors');
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
      this.confirmingArchive.set(false);
    }
  }

  protected async onSubmit(): Promise<void> {
    this.refusal.set(null);

    await submit(this.subcontractorForm, async () => {
      try {
        const payload = this.payload();

        const saved = this.isEdit()
          ? await this.api.edit(this.subcontractorId()!, payload)
          : await this.api.create(payload);

        await this.router.navigateByUrl(`/subcontractors/${saved.id}`);
      } catch (error) {
        const problem = toProblem(error);

        if (problem.code === 'master.duplicate_phone_not_acknowledged') {
          // Not a failure — the server is asking (D-139 §1, D-141). Re-run the check and show current
          // matches rather than the stale ones the operator already dismissed.
          await this.refreshMatches();
          this.acknowledged.set(false);
          return undefined;
        }

        this.refusal.set(problem.messageKey);
      }

      return undefined;
    });
  }

  private async refreshMatches(): Promise<void> {
    const phone = this.model().phone.trim();

    if (phone.length === 0) {
      this.matches.set([]);
      return;
    }

    try {
      const found = await this.api.phoneCheck(phone);
      this.matches.set(found.filter((match) => match.id !== this.subcontractorId()));
    } catch {
      this.matches.set([]);
    }
  }

  private async load(id: string): Promise<void> {
    try {
      const file = await this.api.get(id);
      this.applyLoaded(file);
    } catch (error) {
      const problem = toProblem(error);
      if (problem.status === 404) {
        this.loadFailed.set(true);
      } else {
        this.refusal.set(problem.messageKey);
      }
    }
  }

  private applyLoaded(file: SubcontractorFile): void {
    this.code.set(file.code);
    this.taxRegistrationNumber.set(file.taxRegistrationNumber);
    this.archived.set(!file.isActive);
    this.model.set({
      name: file.name,
      phone: file.phone,
      tradeBabId: file.tradeBabId ?? '',
      retentionPercent: fractionToPercent(String(file.retentionRate)),
    });
    this.loadFailed.set(false);
  }

  private payload(): SubcontractorWrite {
    const value = this.model();

    return {
      name: value.name.trim(),
      phone: value.phone.trim(),
      tradeBabId: value.tradeBabId.length > 0 ? value.tradeBabId : null,
      retentionRate: Number(value.retentionPercent.trim() || DEFAULT_RETENTION_PERCENT),
      acknowledgedDuplicatePhone: this.acknowledged(),
    };
  }

  private async loadBabs(): Promise<void> {
    try {
      this.allBabs.set(await this.babsApi.list('all'));
    } catch {
      this.allBabs.set([]);
      this.babsUnavailable.set(true);
    }
  }
}
