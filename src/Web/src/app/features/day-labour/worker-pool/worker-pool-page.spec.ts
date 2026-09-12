import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { DayLabourApi, EngagementClosed, EngagementOpened, EngagementRated, PoolWorker } from '../../../core/day-labour/day-labour.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ratingOutOfRange, WorkerPoolPage } from './worker-pool-page';

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return key;
  }
}

const WORKER: PoolWorker = {
  id: 'worker-1',
  code: 'DL-0001',
  fullName: 'محمد علي',
  phone: '01000000000',
  babId: 'bab-1',
  specialty: 'نجار',
  isActive: true,
};

/** A fake matching only what the pool screen calls, recording every open/close/rate call it makes. */
class FakeDayLabourApi implements Pick<DayLabourApi, 'listPool' | 'openEngagement' | 'closeEngagement' | 'rateEngagement'> {
  closeCalls: string[] = [];
  rateCalls: { engagementId: string; rating: number }[] = [];

  async listPool(): Promise<readonly PoolWorker[]> {
    return [WORKER];
  }

  async openEngagement(_projectId: string, workerId: string): Promise<EngagementOpened> {
    return { id: 'engagement-1', workerId, projectId: 'project-1', openedOn: '2026-09-12' };
  }

  async closeEngagement(_projectId: string, engagementId: string): Promise<EngagementClosed> {
    this.closeCalls.push(engagementId);
    return { id: engagementId, closedOn: '2026-09-13' };
  }

  async rateEngagement(_projectId: string, engagementId: string, rating: number): Promise<EngagementRated> {
    this.rateCalls.push({ engagementId, rating });
    return { id: engagementId, rating };
  }
}

async function createPage(api: FakeDayLabourApi) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: DayLabourApi, useValue: api },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });

  const fixture = TestBed.createComponent(WorkerPoolPage);
  fixture.componentRef.setInput('projectId', 'project-1');
  fixture.detectChanges();
  await Promise.resolve();
  await Promise.resolve();
  fixture.detectChanges();

  return fixture;
}

/** Rule 6/AC-210-K: closing is an explicit, one-shot manual act — never a timeout, never repeated. */
describe('ratingOutOfRange', () => {
  it('rejects 0 and 6', () => {
    expect(ratingOutOfRange(0)).toBe(true);
    expect(ratingOutOfRange(6)).toBe(true);
  });

  it('rejects a non-integer', () => {
    expect(ratingOutOfRange(3.5)).toBe(true);
  });

  it('accepts every whole number 1 through 5', () => {
    for (let rating = 1; rating <= 5; rating += 1) {
      expect(ratingOutOfRange(rating)).toBe(false);
    }
  });
});

describe('WorkerPoolPage · engage, close, rate', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('opens an engagement, then closing calls the close route exactly once', async () => {
    const api = new FakeDayLabourApi();
    const fixture = await createPage(api);

    const engageButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="worker-engage-DL-0001"]',
    );
    engageButton.click();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    const closeButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="worker-close-DL-0001"]',
    );
    expect(closeButton).toBeTruthy();

    closeButton.click();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(api.closeCalls).toEqual(['engagement-1']);
    // The close control is gone once closed — nothing left to click a second time from this screen.
    expect(fixture.nativeElement.querySelector('[data-testid="worker-close-DL-0001"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="worker-closed-DL-0001"]')).toBeTruthy();
  });

  it('rejects a rating of 0 or 6 client-side, sending nothing to the server', async () => {
    const api = new FakeDayLabourApi();
    const fixture = await createPage(api);

    fixture.nativeElement.querySelector('[data-testid="worker-engage-DL-0001"]').click();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    const ratingInput: HTMLInputElement = fixture.nativeElement.querySelector(
      '[data-testid="worker-rating-input-DL-0001"]',
    );
    const rateButton: HTMLButtonElement = fixture.nativeElement.querySelector(
      '[data-testid="worker-rate-DL-0001"]',
    );

    ratingInput.value = '0';
    ratingInput.dispatchEvent(new Event('input'));
    rateButton.click();
    await Promise.resolve();
    fixture.detectChanges();

    ratingInput.value = '6';
    ratingInput.dispatchEvent(new Event('input'));
    rateButton.click();
    await Promise.resolve();
    fixture.detectChanges();

    expect(api.rateCalls).toHaveLength(0);
    expect(fixture.nativeElement.querySelector('[data-testid="worker-row-refusal-DL-0001"]')).toBeTruthy();
  });

  it('sends a rating of 1-5 to the server', async () => {
    const api = new FakeDayLabourApi();
    const fixture = await createPage(api);

    fixture.nativeElement.querySelector('[data-testid="worker-engage-DL-0001"]').click();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    const ratingInput: HTMLInputElement = fixture.nativeElement.querySelector(
      '[data-testid="worker-rating-input-DL-0001"]',
    );
    ratingInput.value = '4';
    ratingInput.dispatchEvent(new Event('input'));

    fixture.nativeElement.querySelector('[data-testid="worker-rate-DL-0001"]').click();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();

    expect(api.rateCalls).toEqual([{ engagementId: 'engagement-1', rating: 4 }]);
    expect(
      fixture.nativeElement.querySelector('[data-testid="worker-rating-value-DL-0001"]').textContent,
    ).toContain('4/5');
  });
});
