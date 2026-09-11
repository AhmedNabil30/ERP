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
import { Bab, BabsApi } from '../../../core/catalogue/babs.api';
import {
  CatalogueApi,
  CatalogueItem,
  CatalogueItemCreate,
  CatalogueItemEdit,
} from '../../../core/catalogue/catalogue.api';
import { WIRE_DECIMAL_PATTERN, toWireDecimal } from '../../../core/catalogue/money-wire';
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
 * `code`, `descriptionAr`, `unit` and `babId` required, and nothing else. Every other rule — code
 * shape, non-negative price — belongs to `CatalogueItem.Create`/`Reprice` on the server; a second copy
 * here is a copy that eventually disagrees with the entity every other caller goes through (the same
 * reasoning `client-form-page.ts`'s own `draft` schema states for the client). `code` and `babId` are
 * required even though edit mode never lets either be typed: both arrive pre-filled from the loaded
 * item, so the check is a no-op there and a real one on create.
 *
 * **`costPrice` and `baseSellRate` are required too — `V-36-H`.** Before this, the schema required
 * only four fields, both price labels carried a `*` nobody enforced, and an empty field went through
 * `Number("")` as `0` — a money value invented by the system. `required` alone stops a blank submit;
 * `pattern` against the exact wire grammar (`WIRE_DECIMAL_PATTERN`, D-135) additionally refuses hex,
 * an exponent and Arabic-Indic digits (`V-36-K`) before either ever reaches `toWireDecimal` or the
 * server. Each carries its own error kind so the template can show a price-specific message rather
 * than the server's generic "unexpected error".
 */
const draft = schema<CatalogueItemDraft>((path) => {
  required(path.code);
  required(path.descriptionAr);
  required(path.unit);
  required(path.babId);
  required(path.costPrice, { error: { kind: 'cost_price_required' } });
  required(path.baseSellRate, { error: { kind: 'sell_rate_required' } });
  pattern(path.costPrice, WIRE_DECIMAL_PATTERN, { error: { kind: 'money_format_invalid' } });
  pattern(path.baseSellRate, WIRE_DECIMAL_PATTERN, { error: { kind: 'money_format_invalid' } });
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

  /** `KAFF-205` — moving this item to a different باب. Edit-only: create already offers a باب picker. */
  protected readonly movingBab = signal(false);
  protected readonly moveTargetBabId = signal('');
  protected readonly moveFailure = signal<string | null>(null);
  protected readonly moveBusy = signal(false);

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

  /**
   * `V-36-K`, the message half: a price-specific key rather than the generic "unexpected error" — the
   * cost/sell fields never reach the server invalid now that `draft` refuses them first (`V-36-H`'s
   * `required`, plus `pattern` against `WIRE_DECIMAL_PATTERN`), but a touched, still-invalid field
   * needs its own visible reason.
   */
  protected readonly costPriceErrorKey = computed(() => this.priceErrorKey(this.itemForm.costPrice()));
  protected readonly sellRateErrorKey = computed(() => this.priceErrorKey(this.itemForm.baseSellRate()));

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
          // V-36-J: re-baseline before navigating, exactly as the edit branch does. Without this,
          // `pristine` was still the blank draft when `confirmUnsavedChangesGuard` ran on the
          // navigation below, so a successful create asked the operator to discard the changes they
          // had just saved — the guard was comparing against a baseline this branch never updated.
          this.applyLoaded(created);
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

  /** Arabic name in Arabic, English name in English — never both, matching `catalogue-list-page.ts`. */
  protected currentBabLabel(): string {
    const bab = this.babs().find((candidate) => candidate.id === this.model().babId);

    if (!bab) {
      return this.i18n.t('catalogue.list.bab_unknown');
    }

    return this.i18n.locale() === 'en' ? bab.nameEn : bab.nameAr;
  }

  /** Every باب except the one the item is already in — moving it there would not be a move. */
  protected moveCandidates(): readonly Bab[] {
    return this.babs().filter((bab) => bab.id !== this.model().babId);
  }

  protected onStartMove(): void {
    this.moveFailure.set(null);
    this.moveTargetBabId.set('');
    this.movingBab.set(true);
  }

  protected onCancelMove(): void {
    this.movingBab.set(false);
  }

  protected onMoveTargetChange(event: Event): void {
    const target = event.target;

    if (target instanceof HTMLSelectElement) {
      this.moveTargetBabId.set(target.value);
    }
  }

  protected async onConfirmMove(): Promise<void> {
    const id = this.catalogueItemId();
    const targetBabId = this.moveTargetBabId();

    if (id === undefined || targetBabId.length === 0) {
      return;
    }

    this.moveFailure.set(null);
    this.moveBusy.set(true);

    try {
      const moved = await this.api.moveToBab(id, targetBabId);
      // `MoveCatalogueItem.Response` carries only `{ id, babId }` — patch the loaded draft and its
      // baseline rather than re-fetching, since there is no `GET /api/catalogue-items/{id}` to re-fetch
      // from (see this file's own class doc on that gap).
      this.model.update((current) => ({ ...current, babId: moved.babId }));
      this.pristine.update((current) => (current ? { ...current, babId: moved.babId } : current));
      this.movingBab.set(false);
    } catch (error) {
      // AC-205-D/G surfaced, not swallowed.
      this.moveFailure.set(toProblem(error).messageKey);
    } finally {
      this.moveBusy.set(false);
    }
  }

  private applyLoaded(item: CatalogueItem): void {
    const loaded: CatalogueItemDraft = {
      code: item.code,
      descriptionAr: item.descriptionAr,
      descriptionEn: item.descriptionEn ?? '',
      unit: item.unit,
      babId: item.babId,
      // D-135 / V-36-I: costPrice and baseSellRate already arrive as wire strings — String(...) here
      // was reformatting a value the browser had already turned into a double on the way in.
      costPrice: item.costPrice,
      baseSellRate: item.baseSellRate,
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

  /** Maps a touched, invalid price field's own error kind to a message key — `V-36-H` / `V-36-K`. */
  private priceErrorKey(field: {
    readonly touched: () => boolean;
    readonly valid: () => boolean;
    readonly errors: () => readonly { readonly kind: string }[];
  }): string | null {
    if (!field.touched() || field.valid()) {
      return null;
    }

    if (field.errors().some((error) => error.kind === 'cost_price_required')) {
      return 'errors.master.cost_price_required';
    }

    if (field.errors().some((error) => error.kind === 'sell_rate_required')) {
      return 'errors.master.sell_rate_required';
    }

    return 'catalogue.field.price_format_invalid';
  }

  private async loadBabs(): Promise<void> {
    try {
      this.babs.set(await this.babsApi.list());
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
    }
  }
}
