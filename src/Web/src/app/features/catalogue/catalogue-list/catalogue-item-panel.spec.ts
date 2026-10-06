import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { CatalogueItem, CatalogueItemEdit } from '../../../core/catalogue/catalogue.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { CatalogueItemPanel } from './catalogue-item-panel';

class FakeI18nService implements Pick<I18nService, 't' | 'locale' | 'formatAmount'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    return params ? `${key}::${JSON.stringify(params)}` : key;
  }
  formatAmount(value: string): string {
    return `#${value}`;
  }
}

const ITEM: CatalogueItem = {
  id: 'item-1',
  code: 'CON-1015',
  descriptionAr: 'خرسانة مسلحة للقواعد',
  descriptionEn: 'Reinforced concrete for footings',
  unit: 'م³',
  babId: 'bab-1',
  costPrice: '3150.0000',
  baseSellRate: '3720.0000',
  status: 'Active',
};

function render(options: { canEditPrices: boolean; item?: CatalogueItem }) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [{ provide: I18nService, useClass: FakeI18nService }],
  });
  const fixture = TestBed.createComponent(CatalogueItemPanel);
  fixture.componentRef.setInput('item', options.item ?? ITEM);
  fixture.componentRef.setInput('babName', 'أعمال خرسانة');
  fixture.componentRef.setInput('canEditPrices', options.canEditPrices);
  fixture.detectChanges();

  const saves: CatalogueItemEdit[] = [];
  fixture.componentInstance.saveRequested.subscribe((edit) => saves.push(edit));
  const root: HTMLElement = fixture.nativeElement;
  const type = (selector: string, value: string): void => {
    const input = root.querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };
  const click = (testId: string): void => {
    root.querySelector<HTMLElement>(`[data-testid="${testId}"] button, button[data-testid="${testId}"]`)!.click();
    fixture.detectChanges();
  };
  return { fixture, root, saves, type, click };
}

/**
 * AC-940-F: the UI reflects the rule and the server enforces it. These pin the UI half: what each
 * role's panel draws, and that a non-Owner's save can never carry a different price on the wire.
 */
describe('CatalogueItemPanel · the Owner shape', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('draws cost and sell as two editable money fields and no read-only reason', () => {
    const { root } = render({ canEditPrices: true });

    expect(root.querySelector('#catalogue-detail-cost')).toBeTruthy();
    expect(root.querySelector('#catalogue-detail-sell')).toBeTruthy();
    expect(root.querySelector('[data-testid="catalogue-detail-read-only-reason"]')).toBeNull();
  });

  it('starts from the stored figure, trimmed to two decimals, and Save stays disabled until something changes', () => {
    const { root } = render({ canEditPrices: true });

    expect(root.querySelector<HTMLInputElement>('#catalogue-detail-cost')?.value).toBe('3150.00');
    expect(root.querySelector<HTMLButtonElement>('[data-testid="catalogue-detail-save"]')?.disabled).toBe(true);
  });

  it('sends the edited cost and the untouched sell rate exactly as the server sent it; neither explains the other', () => {
    const { saves, type, click, root } = render({ canEditPrices: true });

    type('#catalogue-detail-cost', '3300.50');
    expect(root.querySelector('[data-testid="catalogue-detail-unsaved"]')).toBeTruthy();
    click('catalogue-detail-save');

    expect(saves).toHaveLength(1);
    expect(saves[0].costPrice).toBe('3300.50');
    expect(saves[0].baseSellRate).toBe('3720.0000');
  });

  it('refuses a malformed price before it reaches the wire', () => {
    const { saves, type, click, root } = render({ canEditPrices: true });

    type('#catalogue-detail-cost', '12,5');
    click('catalogue-detail-save');

    expect(saves).toHaveLength(0);
    expect(root.textContent).toContain('catalogue.field.price_format_invalid');
  });
});

describe('CatalogueItemPanel · the read-only shape (AC-940-F)', () => {
  it('draws cost and sell as read-only fields with the reason, and as no input at all', () => {
    const { root } = render({ canEditPrices: false });

    expect(root.querySelector('#catalogue-detail-cost')).toBeNull();
    expect(root.querySelector('#catalogue-detail-sell')).toBeNull();
    expect(root.querySelector('[data-testid="catalogue-detail-read-only-reason"]')?.textContent).toContain(
      'catalogue.panel.read_only_reason',
    );
    const cost = root.querySelector('[data-testid="catalogue-detail-cost-CON-1015"]');
    expect(cost?.textContent?.trim()).toBe('#3150.0000');
    expect(cost?.getAttribute('aria-readonly')).toBe('true');
  });

  it('never uses a disabled control for a value the user may see but not change', () => {
    const { root } = render({ canEditPrices: false });

    const prices = root.querySelectorAll('kaff-money-field');
    expect(prices.length).toBe(2);
    expect(root.querySelectorAll('kaff-money-field [disabled]').length).toBe(0);
  });

  it('keeps the internal marker on cost and says nothing derived about the sell price', () => {
    const { root } = render({ canEditPrices: false });

    expect(root.textContent).toContain('catalogue.badge.internal');
    const note = root.querySelector('[data-testid="catalogue-detail-sell-note"]');
    expect(note?.textContent?.trim()).toBe('catalogue.panel.sell_hint');
    expect(root.textContent).not.toContain('markup');
    expect(root.textContent).not.toContain('sell_rate_note');
  });

  it('lets the Technical Office edit a description, and the save carries both stored prices unchanged', () => {
    const { saves, type, click } = render({ canEditPrices: false });

    type('#catalogue-detail-description-ar', 'خرسانة مسلحة للقواعد المنفصلة');
    click('catalogue-detail-save');

    expect(saves).toHaveLength(1);
    expect(saves[0].descriptionAr).toBe('خرسانة مسلحة للقواعد المنفصلة');
    expect(saves[0].costPrice).toBe('3150.0000');
    expect(saves[0].baseSellRate).toBe('3720.0000');
  });
});
