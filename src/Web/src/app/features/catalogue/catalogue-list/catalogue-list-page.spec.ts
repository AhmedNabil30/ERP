import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { BabsApi } from '../../../core/catalogue/babs.api';
import { CatalogueApi } from '../../../core/catalogue/catalogue.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { CatalogueListPage } from './catalogue-list-page';

/** A fake matching only what the list page calls — no rows, no أبواب, nothing this test needs. */
class FakeCatalogueApi implements Pick<CatalogueApi, 'list'> {
  async list(): Promise<[]> {
    return [];
  }
}

class FakeBabsApi implements Pick<BabsApi, 'list'> {
  async list(): Promise<[]> {
    return [];
  }
}

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return key === 'nav.subcontractors' ? 'مقاولو الباطن' : key;
  }
}

/**
 * `V-38-A`/`V-38-D`: `nav.subcontractors` was in both catalogues with no template reading it, and
 * TechnicalOffice — who holds `SubcontractorManage` alongside `CatalogueManage` — had no way to reach
 * `/subcontractors` except by typing the URL. Fixed the same way `nav.babs` already was: a link on
 * this screen, TechnicalOffice's own landing (S-017).
 */
describe('CatalogueListPage · subcontractors link', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: CatalogueApi, useClass: FakeCatalogueApi },
        { provide: BabsApi, useClass: FakeBabsApi },
        { provide: I18nService, useClass: FakeI18nService },
      ],
    });
  });

  it('renders a translated link to /subcontractors', async () => {
    const fixture = TestBed.createComponent(CatalogueListPage);
    fixture.detectChanges();
    await Promise.resolve();
    fixture.detectChanges();

    const link: HTMLAnchorElement = fixture.nativeElement.querySelector(
      '[data-testid="catalogue-manage-subcontractors"]',
    );
    expect(link).toBeTruthy();
    expect(link.textContent?.trim()).toBe('مقاولو الباطن');
    expect(link.textContent).not.toContain('nav.subcontractors');
  });
});
