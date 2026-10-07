import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  untracked,
} from '@angular/core';

import { Bab, BabCreate, BabEdit } from '../../../core/catalogue/babs.api';
import { flattenBabTree } from '../../../core/catalogue/bab-tree';
import {
  PERCENT_INPUT_PATTERN,
  fractionToPercent,
  percentToFraction,
} from '../../../core/catalogue/percent-wire';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffMoneyField } from '../../../shared/kaff-money-field/kaff-money-field';
import { KaffPickerButton } from '../../../shared/kaff-picker-button/kaff-picker-button';
import { KaffReadonlyField } from '../../../shared/kaff-readonly-field/kaff-readonly-field';
import { KaffTreePicker, TreePickerNode } from '../../../shared/kaff-tree-picker/kaff-tree-picker';

/** Latin letters only: the README's rule for a باب code (`ux_babs_code` makes it unique, case-insensitively). */
const CODE_PATTERN = /^[A-Za-z]+$/;
/** The longest code the domain accepts (`Bab.MaxCodeLength`). */
const CODE_MAX_LENGTH = 32;
const SORT_PATTERN = /^[0-9]{1,9}$/;

interface Draft {
  readonly nameAr: string;
  readonly nameEn: string;
  readonly code: string;
  readonly sortOrder: string;
  readonly markup: string;
  /** `''` is "no parent". */
  readonly parentId: string;
}

interface Seed {
  readonly bab: Bab | null;
  readonly creating: boolean;
  readonly presetParentId: string | null;
}

const CODE_TAKEN_KEY = 'errors.master.bab_code_taken';

/**
 * The باب details panel, in its two shapes (README "The أبواب module, finished"): create and edit are
 * one surface with two headings, a side panel on a desktop and a bottom sheet on a phone, over the tree.
 *
 * **Create** takes the Arabic name, the English name, the code, the parent, a sort order and the default
 * markup. The code is TYPED, Latin, and immutable afterwards; the sort order exists on create only. The
 * parent is chosen in a SECOND STEP inside this panel (a back link, a tree picker, a sentence saying what
 * will happen, "Done"), never a dialog on top of the panel. The markup is typed outright: a child does
 * not inherit its parent's (`KAFF-204` rule 2), and there is no suggested value.
 *
 * **Edit** is `EditBab`'s request and nothing else: the two names. The code is read-only with a lock,
 * the parent is a read-only path with a "Move to…" button (the move has its own endpoint and picker),
 * and the markup is read-only here with a pointer to the row editor, which is its one edit path. The
 * sort order is not drawn on edit: `GET /api/babs` carries none, and a locked field with no value would
 * say nothing (see decisions.md D-181).
 *
 * There is no duplicate-name error anywhere here. No rule says two أبواب cannot share a name.
 */
@Component({
  selector: 'kaff-bab-panel',
  imports: [
    KaffBadge,
    KaffBanner,
    KaffButton,
    KaffDetailPanel,
    KaffField,
    KaffIcon,
    KaffMoneyField,
    KaffPickerButton,
    KaffReadonlyField,
    KaffTreePicker,
  ],
  templateUrl: './bab-panel.html',
  styleUrl: './bab-panel.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BabPanel {
  /** The باب being edited, or `null` when creating or closed. */
  readonly bab = input<Bab | null>(null);
  readonly creating = input(false);
  /** The parent a "add a child" entry presets; `null` for a root. */
  readonly presetParentId = input<string | null>(null);
  readonly babs = input.required<readonly Bab[]>();
  /** The باب's own active items, or `null` when the count could not be read. */
  readonly ownItems = input<number | null>(null);
  readonly childCount = input(0);
  readonly saving = input(false);
  readonly saved = input(false);
  /** The message key of a refused create or save, rendered in place. */
  readonly refusalKey = input<string | null>(null);

  readonly createRequested = output<BabCreate>();
  readonly saveRequested = output<BabEdit>();
  readonly moveRequested = output<void>();
  readonly markupRequested = output<void>();
  readonly archiveRequested = output<void>();
  readonly unarchiveRequested = output<void>();
  readonly closed = output<void>();

  protected readonly i18n = inject(I18nService);

  protected readonly open = computed(() => this.creating() || this.bab() !== null);
  protected readonly isEdit = computed(() => !this.creating() && this.bab() !== null);

  private readonly seed = computed<Seed>(() => ({
    bab: this.bab(),
    creating: this.creating(),
    presetParentId: this.presetParentId(),
  }));

  protected readonly draft = linkedSignal<Seed, Draft>({
    source: this.seed,
    computation: (seed) =>
      seed.bab !== null && !seed.creating
        ? {
            nameAr: seed.bab.nameAr,
            nameEn: seed.bab.nameEn,
            code: seed.bab.code,
            sortOrder: '',
            markup: fractionToPercent(seed.bab.defaultMarkup),
            parentId: seed.bab.parentBabId ?? '',
          }
        : {
            nameAr: '',
            nameEn: '',
            code: '',
            sortOrder: '',
            markup: '',
            parentId: seed.presetParentId ?? '',
          },
  });

  /** Errors only appear after a save was attempted, so a half-typed value is not scolded. */
  protected readonly attempted = linkedSignal<Seed, boolean>({
    source: this.seed,
    computation: () => false,
  });

  /** `form` is the panel's normal body; `parent` is the second step. */
  protected readonly step = linkedSignal<Seed, 'form' | 'parent'>({
    source: this.seed,
    computation: () => 'form',
  });

  /** True while the preset parent still stands, so the heading says "new child" until another parent is chosen. */
  protected readonly isChild = linkedSignal<Seed, boolean>({
    source: this.seed,
    computation: (seed) => seed.creating && seed.presetParentId !== null,
  });

  /** The parent the second step is choosing; only becomes the draft's parent on "Done". */
  protected readonly pick = signal<string | null>(null);

  private readonly names = computed(() => {
    const english = this.i18n.locale() === 'en';
    return new Map(this.babs().map((b) => [b.id, english ? b.nameEn : b.nameAr]));
  });

  private nameOf(id: string): string {
    return this.names().get(id) ?? '';
  }

  protected readonly title = computed(() => {
    if (this.step() === 'parent') {
      return this.i18n.t('bab.panel.pick_parent');
    }
    if (this.creating()) {
      return this.i18n.t(this.isChild() ? 'bab.panel.create_child_title' : 'bab.create_title');
    }
    const bab = this.bab();
    return bab === null ? '' : this.nameOf(bab.id);
  });

  protected readonly parentName = computed(() => {
    const id = this.draft().parentId;
    return id === '' ? this.i18n.t('bab.field.no_parent') : this.nameOf(id);
  });

  /** "Concrete works / Reinforced concrete": the path to the parent, for the read-only "belongs to" field. */
  protected readonly parentPath = computed(() => {
    const bab = this.bab();
    if (bab === null || bab.parentBabId === null) {
      return this.i18n.t('bab.field.no_parent');
    }
    const byId = new Map(this.babs().map((b) => [b.id, b]));
    const path: string[] = [];
    let cursor: Bab | undefined = byId.get(bab.parentBabId);
    for (let guard = 0; cursor !== undefined && guard < 32; guard += 1) {
      path.unshift(this.nameOf(cursor.id));
      cursor = cursor.parentBabId === null ? undefined : byId.get(cursor.parentBabId);
    }
    return path.join(' / ');
  });

  protected readonly pickNodes = computed<readonly TreePickerNode[]>(() =>
    flattenBabTree(this.babs().filter((b) => b.isActive)).map(({ bab, depth }) => ({
      id: bab.id,
      label: this.nameOf(bab.id),
      code: bab.code,
      depth,
    })),
  );

  protected readonly pickSummary = computed(() => {
    const picked = this.pick();
    return picked === null || picked === ''
      ? this.i18n.t('bab.panel.pick_summary_root')
      : this.i18n.t('bab.panel.pick_summary_child', { parent: this.nameOf(picked) });
  });

  protected readonly meta = computed(() => {
    if (!this.creating()) {
      const bab = this.bab();
      return bab === null ? '' : this.countsText();
    }
    return this.isChild() && this.draft().parentId !== ''
      ? this.i18n.t('bab.panel.create_child_meta', { parent: this.nameOf(this.draft().parentId) })
      : this.i18n.t('bab.panel.create_meta');
  });

  private countsText(): string {
    const items = this.ownItems();
    const kids = this.childCount();
    const parts: string[] = [];
    if (items !== null) {
      parts.push(this.i18n.t('bab.count.items', { count: items }));
    }
    if (kids > 0) {
      parts.push(
        kids === 1
          ? this.i18n.t('bab.count.child_one')
          : kids === 2
            ? this.i18n.t('bab.count.child_two')
            : this.i18n.t('bab.count.children', { count: kids }),
      );
    }
    return parts.join(' · ');
  }

  protected readonly markupDisplay = computed(() => {
    const bab = this.bab();
    return bab === null
      ? ''
      : `${this.i18n.formatNumber(fractionToPercent(bab.defaultMarkup), { maximumFractionDigits: 4 })}%`;
  });

  protected readonly dirty = computed(() => {
    const bab = this.bab();
    const d = this.draft();
    if (this.creating()) {
      return d.nameAr !== '' || d.nameEn !== '' || d.code !== '' || d.markup !== '' || d.sortOrder !== '';
    }
    return bab !== null && (d.nameAr !== bab.nameAr || d.nameEn !== bab.nameEn);
  });

  // ---- errors ------------------------------------------------------------------------------------

  protected readonly nameArError = computed(() =>
    this.attempted() && this.draft().nameAr.trim().length === 0 ? 'bab.error.name_ar_required' : null,
  );

  protected readonly nameEnError = computed(() =>
    this.attempted() && this.draft().nameEn.trim().length === 0 ? 'bab.error.name_en_required' : null,
  );

  /** The code the person typed, uppercased the way the server stores it. */
  private readonly typedCode = computed(() => this.draft().code.trim().toUpperCase());

  /** The code the server last refused as taken, so the error follows that code and not whatever is typed next. */
  private readonly refusedCode = linkedSignal<string | null, string | null>({
    source: this.refusalKey,
    computation: (key) => (key === CODE_TAKEN_KEY ? untracked(() => this.typedCode()) : null),
  });

  protected readonly codeError = computed<string | null>(() => {
    if (!this.creating() || !this.attempted()) {
      return null;
    }
    const code = this.typedCode();
    if (code.length === 0) {
      return 'bab.error.code_required';
    }
    if (!CODE_PATTERN.test(code)) {
      return 'bab.error.code_letters';
    }
    // A collision shows from the list already loaded, and again when the server refuses the save.
    return this.babs().some((b) => b.code.toUpperCase() === code) || this.refusedCode() === code
      ? 'bab.error.code_taken'
      : null;
  });

  protected readonly codeErrorParams = computed(() => ({ code: this.typedCode() }));

  protected readonly markupError = computed<string | null>(() => {
    if (!this.attempted() || !this.creating()) {
      return null;
    }
    const text = this.draft().markup.trim();
    if (text.length === 0) {
      return 'bab.error.markup_required';
    }
    return PERCENT_INPUT_PATTERN.test(text) ? null : 'bab.error.markup_number';
  });

  protected readonly sortError = computed<string | null>(() => {
    const text = this.draft().sortOrder.trim();
    return this.attempted() && this.creating() && text.length > 0 && !SORT_PATTERN.test(text)
      ? 'bab.error.sort_integer'
      : null;
  });

  protected readonly hasFieldErrors = computed(
    () =>
      this.nameArError() !== null ||
      this.nameEnError() !== null ||
      this.codeError() !== null ||
      this.markupError() !== null ||
      this.sortError() !== null,
  );

  /** A refusal that is not already drawn on a field. */
  protected readonly bannerRefusal = computed(() => {
    const key = this.refusalKey();
    return key !== null && key !== CODE_TAKEN_KEY ? key : null;
  });

  protected readonly codeMaxLength = CODE_MAX_LENGTH;

  // ---- editing -----------------------------------------------------------------------------------

  protected patch(change: Partial<Draft>): void {
    this.draft.update((d) => ({ ...d, ...change }));
  }

  protected onInput(field: 'nameAr' | 'nameEn' | 'code' | 'sortOrder', event: Event): void {
    this.patch({ [field]: (event.target as HTMLInputElement).value });
  }

  protected onCreate(): void {
    this.attempted.set(true);
    if (this.hasFieldErrors()) {
      return;
    }
    const d = this.draft();
    const sort = d.sortOrder.trim();
    this.createRequested.emit({
      code: this.typedCode(),
      nameAr: d.nameAr.trim(),
      nameEn: d.nameEn.trim(),
      parentBabId: d.parentId === '' ? null : d.parentId,
      defaultMarkup: percentToFraction(d.markup),
      sortOrder: sort.length === 0 ? 0 : Number.parseInt(sort, 10),
    });
  }

  protected onSave(): void {
    const bab = this.bab();
    if (bab === null) {
      return;
    }
    this.attempted.set(true);
    const d = this.draft();
    if (d.nameAr.trim().length === 0 || d.nameEn.trim().length === 0) {
      return;
    }
    // The markup travels back exactly as the server sent it: its one edit path is the row editor.
    this.saveRequested.emit({
      nameAr: d.nameAr.trim(),
      nameEn: d.nameEn.trim(),
      defaultMarkup: bab.defaultMarkup,
    });
  }

  // ---- the parent step ---------------------------------------------------------------------------

  protected onOpenParentStep(): void {
    this.pick.set(this.draft().parentId);
    this.step.set('parent');
  }

  protected onParentStepDone(): void {
    const picked = this.pick();
    if (picked !== null) {
      this.patch({ parentId: picked });
      this.isChild.set(false);
    }
    this.step.set('form');
  }

  protected onBack(): void {
    this.step.set('form');
  }

  /** Escape and the scrim leave the second step first, so one stray press does not drop the whole form. */
  protected onPanelClosed(): void {
    if (this.step() === 'parent') {
      this.onBack();
    } else {
      this.closed.emit();
    }
  }
}
