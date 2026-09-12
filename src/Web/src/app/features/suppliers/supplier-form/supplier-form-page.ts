import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormField, form, required, schema, submit } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { I18nService } from '../../../core/i18n/i18n.service';
import { SupplierFile, SupplierWrite, SuppliersApi } from '../../../core/suppliers/suppliers.api';
import { DuplicatePhoneWarning } from '../../../shared/duplicate-phone-warning/duplicate-phone-warning';
import { PhoneMatch } from '../../../shared/phone-match';

interface SupplierDraft {
  name: string;
  phone: string;
  address: string;
  taxRegistrationNumber: string;
}

const BLANK_DRAFT: SupplierDraft = {
  name: '',
  phone: '',
  address: '',
  taxRegistrationNumber: '',
};

/** `name` and `phone` required — `Supplier.Create`'s own guards. */
const draft = schema<SupplierDraft>((path) => {
  required(path.name);
  required(path.phone);
});

function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

/**
 * `S-030` · create, edit and the duplicate-phone warning, one component (the list shares the screen
 * per its own `ux/screen-inventory.md` row; this is the form half, at `/suppliers/new` and
 * `/suppliers/:id`). KAFF-212.
 *
 * **One record, one account, no project field to carry one** — KAFF-212 rule 2, AC-212-A/B. **No
 * balance, no withholding rate, no bank field** — rules 3/4/13.
 *
 * **The tax registration number is entered here, unlike the subcontractor's** — D-147 point 5:
 * `SupplierManage` is already Finance-and-Owner-only, so no split permission is needed.
 *
 * **The phone is entered first and checked on blur** (D-141), mirroring the client and subcontractor
 * forms — a repeated number warns and is acknowledged, never refused (AC-212-G).
 */
@Component({
  selector: 'kaff-supplier-form-page',
  imports: [FormField, RouterLink, DuplicatePhoneWarning],
  templateUrl: './supplier-form-page.html',
  styleUrl: './supplier-form-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SupplierFormPage {
  /** Absent when creating. The route supplies it when editing — `/suppliers/:supplierId`. */
  readonly supplierId = input<string | undefined>(undefined);

  private readonly api = inject(SuppliersApi);
  private readonly router = inject(Router);

  protected readonly i18n = inject(I18nService);

  private readonly model = signal<SupplierDraft>({ ...BLANK_DRAFT });
  private readonly code = signal<string | null>(null);
  private readonly archived = signal(false);
  protected readonly confirmingArchive = signal(false);

  private readonly matches = signal<readonly PhoneMatch[]>([]);
  private readonly acknowledged = signal(false);
  protected readonly duplicateMatches = this.matches.asReadonly();

  protected readonly supplierForm = form(this.model, draft);

  protected readonly refusal = signal<string | null>(null);
  protected readonly loadFailed = signal(false);

  protected readonly isEdit = computed(() => this.supplierId() !== undefined);
  protected readonly titleKey = computed(() =>
    this.isEdit() ? 'supplier.edit_title' : 'supplier.create_title',
  );
  protected readonly canSubmit = computed(
    () => this.supplierForm().valid() && !this.supplierForm().submitting(),
  );
  protected readonly displayCode = computed(() => this.code());

  constructor() {
    effect(() => {
      const id = this.supplierId();

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

  protected onStartArchive(): void {
    this.confirmingArchive.set(true);
  }

  protected onCancelArchive(): void {
    this.confirmingArchive.set(false);
  }

  protected async onArchive(): Promise<void> {
    const id = this.supplierId();

    if (id === undefined) {
      return;
    }

    this.refusal.set(null);

    try {
      await this.api.archive(id);
      this.archived.set(true);
      this.confirmingArchive.set(false);
      await this.router.navigateByUrl('/suppliers');
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
      this.confirmingArchive.set(false);
    }
  }

  protected async onSubmit(): Promise<void> {
    this.refusal.set(null);

    await submit(this.supplierForm, async () => {
      try {
        const payload = this.payload();

        const saved = this.isEdit()
          ? await this.api.edit(this.supplierId()!, payload)
          : await this.api.create(payload);

        await this.router.navigateByUrl(`/suppliers/${saved.id}`);
      } catch (error) {
        const problem = toProblem(error);

        if (problem.code === 'master.duplicate_phone_not_acknowledged') {
          // Not a failure — the server is asking (D-139 §1, D-141).
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
      this.matches.set(found.filter((match) => match.id !== this.supplierId()));
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

  private applyLoaded(file: SupplierFile): void {
    this.code.set(file.code);
    this.archived.set(!file.isActive);
    this.model.set({
      name: file.name,
      phone: file.phone,
      address: file.address ?? '',
      taxRegistrationNumber: file.taxRegistrationNumber ?? '',
    });
    this.loadFailed.set(false);
  }

  private payload(): SupplierWrite {
    const value = this.model();

    return {
      name: value.name.trim(),
      phone: value.phone.trim(),
      address: orNull(value.address),
      taxRegistrationNumber: orNull(value.taxRegistrationNumber),
      acknowledgedDuplicatePhone: this.acknowledged(),
    };
  }
}
