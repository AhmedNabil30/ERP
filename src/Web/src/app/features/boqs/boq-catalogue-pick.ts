import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';

import { Bab } from '../../core/catalogue/babs.api';
import { CatalogueItem } from '../../core/catalogue/catalogue.api';
import { I18nService } from '../../core/i18n/i18n.service';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffFigure } from '../../shared/kaff-figure/kaff-figure';
import { KaffIcon } from '../../shared/kaff-icon/kaff-icon';
import { KaffSearchField } from '../../shared/kaff-search-field/kaff-search-field';

/** The first باب sections open without a search; a search opens every section that has a match. */
const OPEN_SECTIONS = 3;

interface Section {
  readonly id: string;
  readonly name: string;
  readonly items: readonly CatalogueItem[];
}

/**
 * The catalogue pane of the BOQ screen (README "The ways to add a line"): a name search, the items under
 * their باب, a tick and a "+" on each row. On a phone the same list sits in a sheet with ticks only
 * (`ticksOnly`). Items already in the BOQ show a check mark and cannot be picked again. It holds only
 * the search text, the ticks and which sections are open; the page owns the data and does the adding.
 */
@Component({
  selector: 'boq-catalogue-pick',
  imports: [KaffButton, KaffFigure, KaffIcon, KaffSearchField],
  templateUrl: './boq-catalogue-pick.html',
  styleUrl: './boq-catalogue-pick.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BoqCataloguePick {
  protected readonly i18n = inject(I18nService);

  readonly items = input.required<readonly CatalogueItem[]>();
  readonly babs = input.required<readonly Bab[]>();
  /** Catalogue item ids already on a line. */
  readonly addedIds = input<ReadonlySet<string>>(new Set());
  readonly busy = input(false);
  /** Phone sheet: no "+" and no drag, and the footer button is the filled one (the sheet covers the page primary). */
  readonly ticksOnly = input(false);
  /** The page's own test-id prefix, so the pane and the sheet never share one. */
  readonly testPrefix = input('boq-pick');

  readonly addRequested = output<readonly string[]>();
  readonly customRequested = output<void>();

  protected readonly search = signal('');
  protected readonly ticked = signal<ReadonlySet<string>>(new Set());
  private readonly toggled = signal<ReadonlyMap<string, boolean>>(new Map());

  private readonly lowerSearch = computed(() => this.search().trim().toLocaleLowerCase());

  protected readonly sections = computed<readonly Section[]>(() => {
    const term = this.lowerSearch();
    const byBab = new Map<string, CatalogueItem[]>();
    for (const item of this.items()) {
      const name = this.itemName(item).toLocaleLowerCase();
      if (term !== '' && !name.includes(term)) {
        continue;
      }
      const list = byBab.get(item.babId);
      if (list === undefined) {
        byBab.set(item.babId, [item]);
      } else {
        list.push(item);
      }
    }
    return this.babs()
      .filter((bab) => byBab.has(bab.id))
      .map((bab) => ({
        id: bab.id,
        name: this.i18n.locale() === 'ar' ? bab.nameAr : bab.nameEn,
        items: byBab.get(bab.id)!,
      }));
  });

  protected isOpen(index: number, id: string): boolean {
    if (this.lowerSearch() !== '') {
      return true;
    }
    return this.toggled().get(id) ?? index < OPEN_SECTIONS;
  }

  protected toggleSection(index: number, id: string): void {
    const next = new Map(this.toggled());
    next.set(id, !this.isOpen(index, id));
    this.toggled.set(next);
  }

  protected itemName(item: CatalogueItem): string {
    return this.i18n.locale() === 'ar' ? item.descriptionAr : (item.descriptionEn ?? item.descriptionAr);
  }

  protected tick(id: string, on: boolean): void {
    const next = new Set(this.ticked());
    if (on) {
      next.add(id);
    } else {
      next.delete(id);
    }
    this.ticked.set(next);
  }

  protected addOne(id: string): void {
    this.addRequested.emit([id]);
  }

  /** Adds the ticked rows, then clears the ticks (a failed add keeps the page's banner, not stale ticks). */
  protected addTicked(): void {
    this.addRequested.emit([...this.ticked()]);
    this.ticked.set(new Set());
  }

  protected onDragStart(event: DragEvent, id: string): void {
    event.dataTransfer?.setData('text/plain', id);
  }
}
