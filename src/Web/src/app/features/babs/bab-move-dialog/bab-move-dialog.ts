import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  output,
  viewChild,
} from '@angular/core';

import { Bab } from '../../../core/catalogue/babs.api';
import { flattenBabTree } from '../../../core/catalogue/bab-tree';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffTreePicker, TreePickerNode } from '../../../shared/kaff-tree-picker/kaff-tree-picker';

/** The ids of a باب and everything under it — the targets a move must refuse, so no cycle can be built. */
export function subtreeIds(rootId: string, babs: readonly Bab[]): ReadonlySet<string> {
  const ids = new Set<string>([rootId]);
  let grew = true;
  while (grew) {
    grew = false;
    for (const bab of babs) {
      if (bab.parentBabId !== null && ids.has(bab.parentBabId) && !ids.has(bab.id)) {
        ids.add(bab.id);
        grew = true;
      }
    }
  }
  return ids;
}

/**
 * "Move to…" (README "Move to…"): the tree as a radio list, in a 520px dialog on a desktop and a bottom
 * sheet on a phone. The باب itself and all of its descendants are disabled with the reason, so a cycle
 * cannot be built from here; the server still refuses one at any depth (`AC-205-C`) and that refusal
 * renders in the dialog. Choosing a target shows what will happen in a plain sentence and enables "Move".
 * Not dragging: no drag and drop anywhere in the product for this.
 */
@Component({
  selector: 'kaff-bab-move-dialog',
  imports: [KaffBanner, KaffButton, KaffIcon, KaffTreePicker],
  templateUrl: './bab-move-dialog.html',
  styleUrl: './bab-move-dialog.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BabMoveDialog {
  /** The باب being moved; the dialog is open while this is set. */
  readonly bab = input<Bab | null>(null);
  readonly babs = input.required<readonly Bab[]>();
  readonly busy = input(false);
  readonly refusalKey = input<string | null>(null);

  readonly confirmed = output<string | null>();
  readonly cancelled = output<void>();

  protected readonly i18n = inject(I18nService);

  /** `null` until the person chooses; `''` is "no parent". Resets whenever another باب is opened. */
  protected readonly choice = linkedSignal<Bab | null, string | null>({
    source: this.bab,
    computation: () => null,
  });

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  private readonly names = computed(() => {
    const english = this.i18n.locale() === 'en';
    return new Map(this.babs().map((b) => [b.id, english ? b.nameEn : b.nameAr]));
  });

  protected readonly nodes = computed<readonly TreePickerNode[]>(() => {
    const moving = this.bab();
    if (moving === null) {
      return [];
    }
    const blocked = subtreeIds(moving.id, this.babs());
    return flattenBabTree(this.babs().filter((b) => b.isActive)).map(({ bab, depth }) => ({
      id: bab.id,
      label: this.names().get(bab.id) ?? bab.nameAr,
      code: bab.code,
      depth,
      disabledReason: blocked.has(bab.id)
        ? this.i18n.t(bab.id === moving.id ? 'bab.move.reason_self' : 'bab.move.reason_descendant')
        : null,
      current: bab.id === moving.parentBabId,
    }));
  });

  protected readonly name = computed(() => {
    const moving = this.bab();
    return moving === null ? '' : (this.names().get(moving.id) ?? moving.nameAr);
  });

  protected readonly rootCurrent = computed(() => this.bab()?.parentBabId === null);

  /** The chosen target differs from where the باب already sits. */
  protected readonly changed = computed(() => {
    const picked = this.choice();
    const moving = this.bab();
    return picked !== null && moving !== null && picked !== (moving.parentBabId ?? '');
  });

  protected readonly summary = computed(() => {
    const picked = this.choice();
    if (!this.changed() || picked === null) {
      return this.i18n.t('bab.move.hint');
    }
    return picked === ''
      ? this.i18n.t('bab.move.summary_root', { name: this.name() })
      : this.i18n.t('bab.move.summary_child', {
          name: this.name(),
          parent: this.names().get(picked) ?? '',
        });
  });

  constructor() {
    effect(() => {
      const el = this.dialog().nativeElement;
      if (this.bab() !== null && !el.open) {
        // jsdom has no modal dialog; the guard keeps the unit tests honest and costs nothing in a browser.
        if (typeof el.showModal === 'function') {
          el.showModal();
        } else {
          el.setAttribute('open', '');
        }
        // Focus the first choice you can make, not the dialog frame.
        queueMicrotask(() => el.querySelector<HTMLElement>('input[type="radio"]:not(:disabled)')?.focus());
      } else if (this.bab() === null && el.open) {
        if (typeof el.close === 'function') {
          el.close();
        } else {
          el.removeAttribute('open');
        }
      }
    });
  }

  protected onNativeCancel(event: Event): void {
    event.preventDefault();
    if (!this.busy()) {
      this.cancelled.emit();
    }
  }

  protected onConfirm(): void {
    const picked = this.choice();
    if (this.changed() && picked !== null) {
      this.confirmed.emit(picked === '' ? null : picked);
    }
  }
}
