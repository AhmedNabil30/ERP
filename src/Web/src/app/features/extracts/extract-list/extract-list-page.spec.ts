import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../../../core/auth/auth.service';
import { ExtractsApi } from '../../../core/extracts/extracts.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ExtractListPage } from './extract-list-page';

class FakeI18n implements Pick<I18nService, 't' | 'locale' | 'formatDate'> {
  readonly locale = signal<'ar' | 'en'>('ar');

  t(key: string): string {
    return key;
  }

  formatDate(): string {
    return 'DATE';
  }
}

describe('ExtractListPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('loads on first render from the route input, with no failure state', async () => {
    const list = vi.fn().mockResolvedValue([]);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: I18nService, useClass: FakeI18n },
        { provide: ExtractsApi, useValue: { list } },
        { provide: AuthService, useValue: { current: signal(null) } },
      ],
    });
    const fixture = TestBed.createComponent(ExtractListPage);
    fixture.componentRef.setInput('projectId', 'p-1');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(list).toHaveBeenCalledWith('p-1');
    const root: HTMLElement = fixture.nativeElement;
    expect(root.querySelector('[data-testid="extract-list-failure"]')).toBeNull();
    expect(root.querySelector('[data-testid="extract-list-empty"]')).not.toBeNull();
  });
});
