import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormField, form, required, schema, submit } from '@angular/forms/signals';
import { Router } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import {
  BabOption,
  DayLabourApi,
  WorkerPhoneMatch,
  isRestrictedMatch,
} from '../../../core/day-labour/day-labour.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { DuplicatePhoneWarning } from '../../../shared/duplicate-phone-warning/duplicate-phone-warning';
import { PhoneMatch } from '../../../shared/phone-match';

interface WorkerDraft {
  fullName: string;
  phone: string;
  babId: string;
  specialty: string;
}

const BLANK_DRAFT: WorkerDraft = { fullName: '', phone: '', babId: '', specialty: '' };

/** `fullName`, `phone` and `babId` required — §10 lists exactly four fields and الباب is one of them. */
const draft = schema<WorkerDraft>((path) => {
  required(path.fullName);
  required(path.phone);
  required(path.babId);
});

/**
 * `S-026` · register a worker from site — `KAFF-209`, `ux/screen-inventory.md` -> `S-026`, `M1`.
 *
 * Mobile-first, one hand, at 390px, RTL. `POST /api/projects/{projectId}/day-labour`, gated
 * `DayLabourSiteManage` server-side (`dayLabourSiteManageGuard` is the client-side convenience).
 *
 * **No day rate anywhere** — rule 8, `AC-209-G`: the request and response carry no money member and
 * this form adds no field for one.
 *
 * **The phone-check warning reuses `shared/duplicate-phone-warning`** the same way `S-024`'s employee
 * form does (decisions.md D-141), except a match can also come back masked — D-140 point 6's
 * `{ restricted: true }` for a salaried collision, carrying no name. That branch is rendered here
 * directly rather than taught to the shared component, which only knows the named-match shape.
 */
@Component({
  selector: 'kaff-worker-register-page',
  imports: [FormField, DuplicatePhoneWarning],
  templateUrl: './worker-register-page.html',
  styleUrl: './worker-register-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkerRegisterPage {
  /** Bound from the route — `/projects/:projectId/day-labour/new`. */
  readonly projectId = input.required<string>();

  private readonly api = inject(DayLabourApi);
  private readonly router = inject(Router);

  protected readonly i18n = inject(I18nService);

  private readonly model = signal<WorkerDraft>({ ...BLANK_DRAFT });
  protected readonly workerForm = form(this.model, draft);

  private readonly matches = signal<readonly WorkerPhoneMatch[]>([]);
  private readonly acknowledged = signal(false);

  protected readonly namedMatches = computed<readonly PhoneMatch[]>(() =>
    this.matches().filter((match): match is PhoneMatch => !isRestrictedMatch(match)),
  );
  protected readonly hasRestrictedMatch = computed(() => this.matches().some(isRestrictedMatch));

  private readonly allBabs = signal<readonly BabOption[]>([]);
  protected readonly babs = this.allBabs.asReadonly();
  protected readonly babsUnavailable = signal(false);

  protected readonly refusal = signal<string | null>(null);

  protected readonly canSubmit = computed(() => this.workerForm().valid() && !this.workerForm().submitting());

  constructor() {
    // Deferred to an `effect`, not called straight from the constructor body: `projectId` is a
    // required input, and reading it before Angular applies the route-bound value (or, in a test,
    // before `setInput` runs) throws `NG0951`. `effect` defers the first read to the next change
    // detection pass, by which point the value is there — the same shape `employee-form-page.ts`
    // uses for its own optional `employeeId`.
    effect(() => {
      const id = this.projectId();
      void this.loadBabs(id);
    });
  }

  /** Fires on blur of the phone field, mirroring `S-024`'s employee form (decisions.md D-141). */
  protected async onPhoneBlur(): Promise<void> {
    await this.refreshMatches();
  }

  /** The operator saying "I saw who holds this number and I am proceeding anyway." */
  protected onAcknowledgeChange(acknowledged: boolean): void {
    this.acknowledged.set(acknowledged);
  }

  /** Same acknowledgement, for the masked-match branch that has no `DuplicatePhoneWarning` to emit it. */
  protected onRestrictedAcknowledgeChange(event: Event): void {
    const target = event.target;
    this.acknowledged.set(target instanceof HTMLInputElement && target.checked);
  }

  protected onBabChange(event: Event): void {
    const target = event.target;
    if (target instanceof HTMLSelectElement) {
      this.model.update((current) => ({ ...current, babId: target.value }));
    }
  }

  protected trackBab(_index: number, bab: BabOption): string {
    return bab.id;
  }

  /** Arabic name in Arabic, English name in English — never both, never the raw field. */
  protected babLabel(bab: BabOption): string {
    return this.i18n.locale() === 'en' ? bab.nameEn : bab.nameAr;
  }

  protected async onSubmit(): Promise<void> {
    this.refusal.set(null);

    await submit(this.workerForm, async () => {
      try {
        const value = this.model();
        const created = await this.api.register(this.projectId(), {
          fullName: value.fullName.trim(),
          phone: value.phone.trim(),
          babId: value.babId.length > 0 ? value.babId : null,
          specialty: orNull(value.specialty),
          acknowledgedDuplicatePhone: this.acknowledged(),
        });

        await this.router.navigate(['/projects', this.projectId(), 'day-labour'], {
          state: { registered: created },
        });
      } catch (error) {
        const problem = toProblem(error);

        if (problem.code === 'master.duplicate_phone_not_acknowledged') {
          // Not a failure — the server is asking. Re-run the check and show current matches rather
          // than the stale ones the operator already dismissed (mirrors S-024, D-141).
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
      this.matches.set(await this.api.phoneCheck(this.projectId(), phone));
    } catch {
      // A failed check must not stop a save — the server re-runs the match anyway on submit.
      this.matches.set([]);
    }
  }

  private async loadBabs(projectId: string): Promise<void> {
    try {
      this.allBabs.set(await this.api.listBabs(projectId));
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
