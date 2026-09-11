import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormField, form, pattern, required, schema, submit } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { Bab, BabCreate, BabEdit, BabsApi } from '../../../core/catalogue/babs.api';
import {
  PERCENT_INPUT_PATTERN,
  fractionToPercent,
  percentToFraction,
} from '../../../core/catalogue/percent-wire';
import { I18nService } from '../../../core/i18n/i18n.service';
import { UnsavedChangesAware } from '../../../core/navigation/unsaved-changes.guard';

interface BabDraft {
  code: string;
  nameAr: string;
  nameEn: string;
  parentBabId: string;
  /** A percent an operator reads, e.g. `"15"` — converted to the wire fraction only at submission. */
  markupPercent: string;
  sortOrder: string;
}

const BLANK_DRAFT: BabDraft = {
  code: '',
  nameAr: '',
  nameEn: '',
  parentBabId: '',
  markupPercent: '',
  sortOrder: '0',
};

/**
 * `code`, `nameAr`, `nameEn` and `markupPercent` required — `KAFF-204` rule 2: every باب carries its
 * own default markup, with no null and no inheritance. `code` is a no-op check on edit, the same
 * reasoning `catalogue-form-page.ts`'s own `draft` schema states: it arrives pre-filled and read-only
 * there, so the check is live only on create.
 */
const draft = schema<BabDraft>((path) => {
  required(path.code);
  required(path.nameAr);
  required(path.nameEn);
  required(path.markupPercent, { error: { kind: 'default_markup_required' } });
  pattern(path.markupPercent, PERCENT_INPUT_PATTERN, { error: { kind: 'markup_format_invalid' } });
});

/**
 * `S-022` · create and edit one باب, one component — `KAFF-204`.
 *
 * **The parent is offered on create; edit shows none.** `PUT /api/babs/{id}` (`EditBab.Request`)
 * carries no `parentBabId` member at all — re-parenting is `KAFF-205`'s own endpoint, on the tree
 * (`bab-tree-page.ts`), through `Bab.SetParent`'s cycle guard. Rendering a parent picker on edit that
 * cannot save would be worse than not rendering it, the same reasoning `catalogue-form-page.ts` gives
 * for its own باب picker.
 *
 * **Unlike the catalogue item form, this one has a real `GET` to load from.** `GET /api/babs` returns
 * every row — أبواب number "roughly 40" (`KAFF-204` rule 6), small enough to fetch whole — so a hard
 * load of `/babs/{id}` finds the record in that list rather than depending on router `state`. That is
 * a real gap `catalogue-form-page.ts` reports rather than works around; this screen does not have it.
 */
@Component({
  selector: 'kaff-bab-form-page',
  imports: [FormField, RouterLink],
  templateUrl: './bab-form-page.html',
  styleUrl: './bab-form-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BabFormPage implements UnsavedChangesAware {
  /** Absent when creating. The route supplies it when editing — `/babs/:babId`. */
  readonly babId = input<string | undefined>(undefined);

  private readonly api = inject(BabsApi);
  private readonly router = inject(Router);

  protected readonly i18n = inject(I18nService);

  private readonly model = signal<BabDraft>({ ...BLANK_DRAFT });
  private readonly pristine = signal<BabDraft | null>(null);

  protected readonly babForm = form(this.model, draft);

  /** `'all'` — editing an archived باب must still be possible; only the tree's default view hides it. */
  protected readonly allBabs = signal<readonly Bab[]>([]);
  private readonly babsLoaded = signal(false);

  protected readonly refusal = signal<string | null>(null);
  protected readonly loadFailed = signal(false);

  protected readonly isEdit = computed(() => this.babId() !== undefined);
  protected readonly titleKey = computed(() => (this.isEdit() ? 'bab.edit_title' : 'bab.create_title'));
  protected readonly canSubmit = computed(() => this.babForm().valid() && !this.babForm().submitting());
  protected readonly displayCode = computed(() => this.model().code);

  /** Every باب except this one — offering itself as its own parent is a self-cycle the server refuses anyway. */
  protected readonly parentCandidates = computed(() =>
    this.allBabs().filter((bab) => bab.id !== this.babId()),
  );

  protected readonly markupErrorKey = computed(() => this.markupErrorKeyFor(this.babForm.markupPercent()));

  constructor() {
    void this.loadBabs();

    // Same shape as `catalogue-form-page.ts`'s own load effect, and the same reason for the
    // microtask defer: writing `this.model` synchronously during the component's first render pass
    // reassigns the signal `form(this.model, draft)` wraps out from under `FormField` mid-init.
    effect(() => {
      const id = this.babId();
      const babs = this.allBabs(); // tracked here, not inside the microtask below, so a later
      const loaded = this.babsLoaded(); // arrival of the list re-runs this effect.

      void Promise.resolve().then(() => {
        if (id === undefined) {
          if (this.pristine() === null) {
            this.pristine.set({ ...BLANK_DRAFT });
          }
          return;
        }

        const found = babs.find((bab) => bab.id === id);

        if (found) {
          this.applyLoaded(found);
        } else if (loaded) {
          this.loadFailed.set(true);
        }
      });
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

  protected onParentChange(event: Event): void {
    const target = event.target;

    if (target instanceof HTMLSelectElement) {
      this.model.update((current) => ({ ...current, parentBabId: target.value }));
    }
  }

  protected async onSubmit(): Promise<void> {
    this.refusal.set(null);

    await submit(this.babForm, async () => {
      try {
        if (this.isEdit()) {
          const saved = await this.api.edit(this.babId()!, this.payloadEdit());
          this.allBabs.update((current) => current.map((bab) => (bab.id === saved.id ? saved : bab)));
          this.applyLoaded(saved);
        } else {
          const created = await this.api.create(this.payloadCreate());
          this.allBabs.update((current) => [...current, created]);
          this.applyLoaded(created);
          await this.router.navigateByUrl(`/babs/${created.id}`);
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

  private applyLoaded(bab: Bab): void {
    const loaded: BabDraft = {
      code: bab.code,
      nameAr: bab.nameAr,
      nameEn: bab.nameEn,
      parentBabId: bab.parentBabId ?? '',
      markupPercent: fractionToPercent(bab.defaultMarkup),
      sortOrder: '0', // not shown on edit — EditBab.Request carries no SortOrder to save it back to.
    };

    this.model.set(loaded);
    this.pristine.set(loaded);
    this.loadFailed.set(false);
  }

  private payloadCreate(): BabCreate {
    const value = this.model();
    const sortOrder = Number.parseInt(value.sortOrder.trim(), 10);

    return {
      code: value.code.trim(),
      nameAr: value.nameAr.trim(),
      nameEn: value.nameEn.trim(),
      parentBabId: value.parentBabId.length > 0 ? value.parentBabId : null,
      defaultMarkup: percentToFraction(value.markupPercent),
      sortOrder: Number.isFinite(sortOrder) ? sortOrder : 0,
    };
  }

  private payloadEdit(): BabEdit {
    const value = this.model();

    return {
      nameAr: value.nameAr.trim(),
      nameEn: value.nameEn.trim(),
      defaultMarkup: percentToFraction(value.markupPercent),
    };
  }

  private markupErrorKeyFor(field: {
    readonly touched: () => boolean;
    readonly valid: () => boolean;
    readonly errors: () => readonly { readonly kind: string }[];
  }): string | null {
    if (!field.touched() || field.valid()) {
      return null;
    }

    if (field.errors().some((error) => error.kind === 'default_markup_required')) {
      return 'errors.master.default_markup_required';
    }

    return 'bab.field.markup_format_invalid';
  }

  private async loadBabs(): Promise<void> {
    try {
      this.allBabs.set(await this.api.list('all'));
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
    } finally {
      this.babsLoaded.set(true);
    }
  }
}
