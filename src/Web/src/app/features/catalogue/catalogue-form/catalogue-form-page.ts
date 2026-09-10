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
import { FormField, form, required, schema, submit } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { Bab, BabsApi } from '../../../core/catalogue/babs.api';
import {
  CatalogueApi,
  CatalogueItem,
  CatalogueItemCreate,
  CatalogueItemEdit,
} from '../../../core/catalogue/catalogue.api';
import { toWireDecimal } from '../../../core/catalogue/money-wire';
import { I18nService } from '../../../core/i18n/i18n.service';
import { UnsavedChangesAware } from '../../../core/navigation/unsaved-changes.guard';

interface CatalogueItemDraft {
  code: string;
  descriptionAr: string;
  descriptionEn: string;
  unit: string;
  babId: string;
  /** A STRING, never `parseFloat`ed — `ux/components.md` §2's `kaff-money-input` rule. */
  costPrice: string;
  baseSellRate: string;
}

const BLANK_DRAFT: CatalogueItemDraft = {
  code: '',
  descriptionAr: '',
  descriptionEn: '',
  unit: '',
  babId: '',
  costPrice: '',
  baseSellRate: '',
};

/**
 * Two `required`s short of four, and nothing else. Every other rule — code shape, non-negative price —
 * belongs to `CatalogueItem.Create`/`Reprice` on the server; a second copy here is a copy that
 * eventually disagrees with the entity every other caller goes through (the same reasoning
 * `client-form-page.ts`'s own `draft` schema states for the client). `code` and `babId` are required
 * even though edit mode never lets either be typed: both arrive pre-filled from the loaded item, so the
 * check is a no-op there and a real one on create.
 */
const draft = schema<CatalogueItemDraft>((path) => {
  required(path.code);
  required(path.descriptionAr);
  required(path.unit);
  required(path.babId);
});

/** Blank means absent — matches `EditCatalogueItem.Request.DescriptionEn` being optional. */
function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

/**
 * S-018 · create and edit, one component — `KAFF-202`.
 *
 * **Create takes a باب; edit does not show one.** `PUT /api/catalogue-items/{id}` carries no `babId`
 * member at all (moving an item to another باب is `KAFF-205`, unbuilt), so rendering a picker that
 * cannot save would be worse than not rendering it. **The code is immutable on edit** — shown as
 * isolated text, never a disabled input, matching `client-form-page.html`'s own reasoning for its code.
 *
 * **No client-side rounding, anywhere in this file.** `V-35-Q` / `AC-200-B`: `Money`'s constructor
 * already rounds away-from-zero above four decimals, system-wide, and whether that is correct is still
 * open with Nabil. Both price fields are bound as strings and only converted — never rounded — by
 * `toWireDecimal` at the moment of submission.
 *
 * **There is no `GET /api/catalogue-items/{id}` on this API**, unlike `GetClient` for the client
 * screen. This component loads an existing item from router `state` — passed by the list row's
 * `[state]` binding and by this page's own post-create redirect — and degrades honestly rather than
 * silently when that state is absent (a bookmark, a hard refresh, a pasted URL): see `loadFailed`.
 * **This is a real gap in the backend contract, reported rather than worked around**; the workaround
 * this file does *not* take is fetching the whole list to filter one item out of it, which would be
 * both a second source of truth and, on a ~600-row catalogue, wasteful.
 */
@Component({
  selector: 'kaff-catalogue-form-page',
  imports: [FormField, RouterLink],
  templateUrl: './catalogue-form-page.html',
  styleUrl: './catalogue-form-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogueFormPage implements UnsavedChangesAware {
  /** Absent when creating. The route supplies it when editing — `/catalogue/:catalogueItemId`. */
  readonly catalogueItemId = input<string | undefined>(undefined);

  private readonly api = inject(CatalogueApi);
  private readonly babsApi = inject(BabsApi);
  private readonly router = inject(Router);

  protected readonly i18n = inject(I18nService);

  private readonly model = signal<CatalogueItemDraft>({ ...BLANK_DRAFT });

  /** The baseline `hasUnsavedChanges` compares against — the loaded item, or the blank draft on create. */
  private readonly pristine = signal<CatalogueItemDraft | null>(null);

  protected readonly itemForm = form(this.model, draft);

  protected readonly babs = signal<readonly Bab[]>([]);
  protected readonly refusal = signal<string | null>(null);
  protected readonly loadFailed = signal(false);

  protected readonly isEdit = computed(() => this.catalogueItemId() !== undefined);

  protected readonly titleKey = computed(() =>
    this.isEdit() ? 'catalogue.item.edit_title' : 'catalogue.item.create_title',
  );

  protected readonly canSubmit = computed(
    () => this.itemForm().valid() && !this.itemForm().submitting(),
  );

  /** The code, read-only. Never bound through `[formField]` on the edit screen — there is nothing to
   * edit, and a disabled input would say "this could be edited, just not by you" when it cannot be
   * edited by any route through the API at all. */
  protected readonly displayCode = computed(() => this.model().code);

  constructor() {
    void this.loadBabs();

    // The route's id is an input signal, so this reacts to it rather than running once in a
    // constructor that would miss a navigation from one item straight to another's — same reasoning as
    // `client-form-page.ts`'s own load effect.
    //
    // **The body defers its `.set()` calls to a microtask, and that is load-bearing.** `applyLoaded`
    // writes `this.model`, the exact signal `form(this.model, draft)` wraps — calling that
    // synchronously, inside the effect's own execution during the component's first render pass,
    // reassigns the signal out from under `FormField` while it is still being initialised and throws
    // `TypeError: this.field(...) is not a function` out of the template, silently blanking every field
    // after it. `client-form-page.ts`'s own `load()` never hits this because it is `async` and its
    // `.set()` runs after a real `await this.api.get(id)` — well past the first render. There is no
    // API call here to await (see the class doc's remark on the missing `GET`), so a bare
    // `Promise.resolve()` buys the same one-microtask gap, deliberately, and is the whole fix — found
    // by rendering the screen and reading the console, not by reasoning about it.
    effect(() => {
      const id = this.catalogueItemId();

      void Promise.resolve().then(() => {
        if (id === undefined) {
          if (this.pristine() === null) {
            this.pristine.set({ ...BLANK_DRAFT });
          }
          return;
        }

        const passed = (history.state as { item?: CatalogueItem } | null)?.item;

        if (passed && passed.id === id) {
          this.applyLoaded(passed);
        } else {
          this.loadFailed.set(true);
        }
      });
    });
  }

  hasUnsavedChanges(): boolean {
    const baseline = this.pristine();
    return baseline !== null && JSON.stringify(baseline) !== JSON.stringify(this.model());
  }

  /** The tab-close / hard-reload half of "warn before navigating away"; `confirmUnsavedChangesGuard`
   * covers in-app navigation. Browsers show their own generic text regardless of the event's message. */
  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) {
      event.preventDefault();
    }
  }

  /**
   * A plain `(change)` handler rather than `[formField]`, the same shape `client-form-page.ts` uses for
   * its kind toggle — a native `<select>` has no shared-component precedent in this codebase yet, and
   * writing straight to the model signal `form()` wraps is the pattern already accepted there.
   */
  protected onBabChange(event: Event): void {
    const target = event.target;

    if (target instanceof HTMLSelectElement) {
      this.model.update((current) => ({ ...current, babId: target.value }));
    }
  }

  protected async onSubmit(): Promise<void> {
    this.refusal.set(null);

    await submit(this.itemForm, async () => {
      try {
        if (this.isEdit()) {
          const saved = await this.api.edit(this.catalogueItemId()!, this.payloadEdit());
          // Re-baselines against exactly what the server stored, so a second save has nothing stale to
          // warn about and `hasUnsavedChanges()` goes false without a separate "saved" flag to track.
          this.applyLoaded(saved);
        } else {
          const created = await this.api.create(this.payloadCreate());
          // The one navigation this page makes: from `/catalogue/new` to the item's own permanent URL,
          // carrying the created item as router state so the destination instance can render without a
          // `GET /api/catalogue-items/{id}` this API does not have.
          await this.router.navigateByUrl(`/catalogue/${created.id}`, { state: { item: created } });
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

  private applyLoaded(item: CatalogueItem): void {
    const loaded: CatalogueItemDraft = {
      code: item.code,
      descriptionAr: item.descriptionAr,
      descriptionEn: item.descriptionEn ?? '',
      unit: item.unit,
      babId: item.babId,
      costPrice: String(item.costPrice),
      baseSellRate: String(item.baseSellRate),
    };

    this.model.set(loaded);
    this.pristine.set(loaded);
    this.loadFailed.set(false);
  }

  private payloadCreate(): CatalogueItemCreate {
    const value = this.model();

    return {
      code: value.code.trim(),
      descriptionAr: value.descriptionAr.trim(),
      descriptionEn: orNull(value.descriptionEn),
      unit: value.unit.trim(),
      babId: value.babId,
      costPrice: toWireDecimal(value.costPrice),
      baseSellRate: toWireDecimal(value.baseSellRate),
    };
  }

  private payloadEdit(): CatalogueItemEdit {
    const value = this.model();

    return {
      descriptionAr: value.descriptionAr.trim(),
      descriptionEn: orNull(value.descriptionEn),
      unit: value.unit.trim(),
      costPrice: toWireDecimal(value.costPrice),
      baseSellRate: toWireDecimal(value.baseSellRate),
    };
  }

  private async loadBabs(): Promise<void> {
    try {
      this.babs.set(await this.babsApi.list());
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
    }
  }
}
