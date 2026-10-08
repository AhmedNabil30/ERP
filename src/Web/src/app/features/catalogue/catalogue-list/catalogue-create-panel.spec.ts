import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { Bab } from '../../../core/catalogue/babs.api';
import { CatalogueItemCreate } from '../../../core/catalogue/catalogue.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { CatalogueCreatePanel } from './catalogue-create-panel';

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    return params ? `${key}::${JSON.stringify(params)}` : key;
  }
}

const BABS: readonly Bab[] = [
  { id: 'b1', code: 'CON', nameAr: 'أعمال خرسانة', nameEn: 'Concrete', parentBabId: null, defaultMarkup: '0.15', isActive: true },
  { id: 'b2', code: 'OLD', nameAr: 'قديم', nameEn: 'Old', parentBabId: null, defaultMarkup: '0.1', isActive: false },
];

function render(inputs: Readonly<Record<string, unknown>> = {}) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: I18nService, useClass: FakeI18nService }] });
  const fixture: ComponentFixture<CatalogueCreatePanel> = TestBed.createComponent(CatalogueCreatePanel);
  fixture.componentRef.setInput('open', true);
  fixture.componentRef.setInput('babs', BABS);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  const emitted: CatalogueItemCreate[] = [];
  fixture.componentInstance.createRequested.subscribe((value) => emitted.push(value));
  fixture.detectChanges();
  const root: HTMLElement = fixture.nativeElement;
  const byId = (id: string) => root.querySelector<HTMLElement>(`[data-testid="${id}"]`);
  const type = (id: string, text: string) => {
    const input = byId(id) as HTMLInputElement;
    input.value = text;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };
  const click = (id: string) => {
    byId(id)!.click();
    fixture.detectChanges();
  };
  const chooseUnit = (unit: string) => {
    const select = byId('catalogue-create-unit') as HTMLSelectElement;
    select.value = unit;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };
  const chooseBab = (id: string) => {
    click('catalogue-create-bab');
    (byId('catalogue-create-bab-option-CON') as HTMLInputElement).click();
    expect(id).toBe('b1');
    click('catalogue-create-pick-done');
  };
  const fillValid = () => {
    type('catalogue-create-description-ar', 'خرسانة جاهزة');
    type('catalogue-create-code', 'con-1030');
    chooseBab('b1');
    chooseUnit('م³');
    type('catalogue-create-cost', '1850.00');
    type('catalogue-create-sell', '2300');
  };
  return { fixture, root, byId, type, click, chooseUnit, chooseBab, fillValid, emitted };
}

describe('CatalogueCreatePanel (S-018, KAFF-202)', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('creates with a TYPED code, uppercased, and both prices on the wire as the strings typed', () => {
    const { fillValid, click, emitted } = render();

    fillValid();
    click('catalogue-create-submit');

    expect(emitted).toEqual([
      {
        code: 'CON-1030',
        descriptionAr: 'خرسانة جاهزة',
        descriptionEn: null,
        unit: 'م³',
        babId: 'b1',
        costPrice: '1850.00',
        baseSellRate: '2300',
      },
    ]);
  });

  it('refuses an absent price: V-36-H, there is no unpriced item and no price-less create', () => {
    const { fillValid, type, click, byId, emitted } = render();

    fillValid();
    type('catalogue-create-cost', '');
    click('catalogue-create-submit');

    expect(emitted).toHaveLength(0);
    expect(byId('catalogue-create-errors')).toBeTruthy();
    expect(byId('catalogue-create-cost')!.closest('.field')!.textContent).toContain('errors.master.cost_price_required');

    type('catalogue-create-cost', '10');
    type('catalogue-create-sell', '');
    click('catalogue-create-submit');
    expect(emitted).toHaveLength(0);
    expect(byId('catalogue-create-sell')!.closest('.field')!.textContent).toContain('errors.master.sell_rate_required');
  });

  it('accepts an explicit zero: only the ABSENCE of a price is an error', () => {
    const { fillValid, type, click, emitted } = render();

    fillValid();
    type('catalogue-create-cost', '0');
    type('catalogue-create-sell', '0');
    click('catalogue-create-submit');

    expect(emitted).toHaveLength(1);
    expect(emitted[0].costPrice).toBe('0');
    expect(emitted[0].baseSellRate).toBe('0');
  });

  it('is the same form for every role: it does not read a permission', () => {
    // The panel takes no permission input at all (CataloguePriceEdit guards EDIT, not create).
    const { root } = render();
    expect(root.querySelector('[data-testid="catalogue-create-cost"]')).toBeTruthy();
    expect(root.querySelector('[data-testid="catalogue-create-sell"]')).toBeTruthy();
    expect(root.querySelector('[readonly]')).toBeNull();
  });

  it('refuses a malformed code, a blank one, and a missing باب and unit, each next to its field', () => {
    const { type, click, byId, emitted } = render();

    type('catalogue-create-code', 'بند');
    click('catalogue-create-submit');

    expect(emitted).toHaveLength(0);
    expect(byId('catalogue-create-code-error')!.textContent).toContain('catalogue.error.code_format');
    expect(byId('catalogue-create-bab')!.closest('.field')!.textContent).toContain('catalogue.error.bab_required');
    expect(byId('catalogue-create-unit')!.closest('.field')!.textContent).toContain('catalogue.error.unit_required');
  });

  it('names a collision with a code already in the catalogue, before the server is asked', () => {
    const { fillValid, click, byId, emitted } = render({ takenCodes: new Set(['CON-1030']) });

    fillValid();
    click('catalogue-create-submit');

    expect(emitted).toHaveLength(0);
    expect(byId('catalogue-create-code-error')!.textContent).toContain('catalogue.error.code_taken');
  });

  it('shows the server\'s code collision on the code field, not as a banner', () => {
    const { fillValid, click, fixture, byId, root } = render();

    fillValid();
    click('catalogue-create-submit');
    fixture.componentRef.setInput('refusalKey', 'errors.master.catalogue_item_code_taken');
    fixture.detectChanges();

    expect(byId('catalogue-create-code-error')!.textContent).toContain('catalogue.error.code_taken');
    expect(root.querySelector('[data-testid="catalogue-create-refusal"]')).toBeNull();
  });

  it('shows any other refusal in place, as a banner', () => {
    const { fixture, byId } = render();

    fixture.componentRef.setInput('refusalKey', 'errors.master.cost_price_negative');
    fixture.detectChanges();

    expect(byId('catalogue-create-refusal')!.textContent).toContain('errors.master.cost_price_negative');
  });

  it('chooses the باب in a second step with a back link, and lists active أبواب only', () => {
    const { click, byId, root } = render();

    click('catalogue-create-bab');

    expect(byId('catalogue-create-back')).toBeTruthy();
    expect(byId('catalogue-create-bab-option-CON')).toBeTruthy();
    expect(byId('catalogue-create-bab-option-OLD')).toBeNull();
    // A باب must be named: there is no "no parent" row.
    expect(root.querySelector('[data-testid="catalogue-create-bab-option-root"]')).toBeNull();

    click('catalogue-create-back');
    expect(byId('catalogue-create-description-ar')).toBeTruthy();
  });
});
