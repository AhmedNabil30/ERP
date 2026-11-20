import { ChangeDetectionStrategy, Component, TemplateRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { Router } from '@angular/router';

import { ChainApi, NewChangeOrderLine } from '../../../core/extracts/chain.api';
import { ProjectDetail, ProjectsApi } from '../../../core/projects/projects.api';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import { KaffMoneyField } from '../../../shared/kaff-money-field/kaff-money-field';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { ChainPage } from '../chain-page';
import { wireAmount } from '../extract-display';

interface Line {
  readonly key: number;
  description: string;
  unit: string;
  quantity: string;
  unitPrice: string;
}

interface LineErrors {
  description: string | null;
  unit: string | null;
  quantity: string | null;
  unitPrice: string | null;
}

let nextKey = 0;
const blank = (): Line => ({ key: nextKey++, description: '', unit: '', quantity: '', unitPrice: '' });

/**
 * E12, «أمر تغيير جديد» (`extracts.html#e12`): the Technical Office writes a title and its lines. A Cost Plus or
 * Design project's change order is logged with a title only: the server refuses price lines there
 * (`change_order.lines_not_allowed`). **No total is drawn**: the browser adds no money (D-135), and the change
 * order's own read carries its total once saved.
 */
@Component({
  selector: 'kaff-change-order-new-page',
  imports: [KaffBanner, KaffButton, KaffField, KaffIcon, KaffMoneyField, KaffPageHead],
  templateUrl: './change-order-new-page.html',
  styleUrls: ['../chain-page.css', './change-order-new-page.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangeOrderNewPage extends ChainPage {
  private readonly chain = inject(ChainApi);
  private readonly projects = inject(ProjectsApi);
  private readonly router = inject(Router);

  readonly projectId = input.required<string>();

  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly title = signal('');
  protected readonly titleError = signal<string | null>(null);
  protected readonly lines = signal<readonly Line[]>([blank()]);
  protected readonly lineErrors = signal<readonly LineErrors[]>([]);
  protected readonly noLines = signal(false);

  /** A Lump Sum change order is priced; the other types log one (spec §5.1). */
  protected readonly priced = computed(() => (this.project()?.contractType ?? 'LumpSum') === 'LumpSum');

  protected override readonly primaryTemplate = viewChild<TemplateRef<unknown>>('primaryAction');
  protected override readonly hasPrimary = signal(true);

  constructor() {
    super();
    effect(() => {
      this.projectId();
      untracked(() => void this.projects.get(this.projectId()).then((p) => this.project.set(p), () => this.project.set(null)));
    });
  }

  protected add(): void {
    this.lines.update((lines) => [...lines, blank()]);
    this.noLines.set(false);
  }

  protected remove(key: number): void {
    this.lines.update((lines) => lines.filter((l) => l.key !== key));
  }

  protected edit(key: number, field: 'description' | 'unit' | 'quantity' | 'unitPrice', value: string): void {
    this.lines.update((lines) => lines.map((l) => (l.key === key ? { ...l, [field]: value } : l)));
  }

  protected errorOf(index: number, field: keyof LineErrors): string | null {
    return this.lineErrors()[index]?.[field] ?? null;
  }

  protected async save(): Promise<void> {
    const title = this.title().trim();
    this.titleError.set(title === '' ? 'change_order.title_required' : null);
    const priced = this.priced();
    const lines = priced ? this.lines() : [];
    const errors = lines.map<LineErrors>((l) => {
      const quantity = wireAmount(l.quantity);
      return {
        description: l.description.trim() === '' ? 'change_order.line.description_required' : null,
        unit: l.unit.trim() === '' ? 'change_order.line.unit_required' : null,
        quantity: quantity === null || !/[1-9]/.test(quantity) ? 'change_order.line.quantity_required' : null,
        unitPrice: wireAmount(l.unitPrice) === null ? 'change_order.line.price_required' : null,
      };
    });
    this.lineErrors.set(errors);
    this.noLines.set(priced && lines.length === 0);
    if (title === '' || this.noLines() || errors.some((e) => Object.values(e).some((v) => v !== null))) {
      return;
    }
    const request: NewChangeOrderLine[] = lines.map((l) => ({
      description: l.description.trim(),
      unit: l.unit.trim(),
      quantity: Number(wireAmount(l.quantity)),
      unitPrice: wireAmount(l.unitPrice)!,
    }));
    let id: string | null = null;
    if (await this.act(async () => (id = (await this.chain.createChangeOrder(this.projectId(), title, request)).id))) {
      void this.router.navigate(['/projects', this.projectId(), 'extracts', 'change-orders', id]);
    }
  }
}
