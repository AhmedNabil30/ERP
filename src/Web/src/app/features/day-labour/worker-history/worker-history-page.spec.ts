import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';

import { AuthService, Session } from '../../../core/auth/auth.service';
import { DayLabourApi, WorkerEngagementHistory } from '../../../core/day-labour/day-labour.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { WorkerHistoryPage } from './worker-history-page';

class FakeI18nService implements Pick<I18nService, 't' | 'locale' | 'formatNumber' | 'formatMoney'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string): string {
    return key;
  }
  formatNumber(value: number | string): string {
    return String(value);
  }
  formatMoney(value: string): string {
    return `EGP ${value}`;
  }
}

const OWNER_SESSION: Session = {
  userId: 'owner-1',
  displayName: 'المالك',
  role: 'Owner',
  departmentId: null,
  operationsSubDepartment: null,
  mustChangePassword: false,
  permissions: [],
  projects: [
    {
      projectId: 'project-1',
      name: 'مشروع الاختبار',
      code: 'P-01',
      accessPath: 'OwnerGlobal',
      level: 'Standard',
      permissions: ['DayLabourRateManage'],
    },
  ],
  teamProjects: [],
};

class FakeAuthService implements Pick<AuthService, 'current'> {
  readonly current = signal<Session | null>(OWNER_SESSION);
}

const EMPTY_HISTORY: WorkerEngagementHistory = {
  workerId: 'worker-1',
  items: [],
  averageDayRate: null,
  frequency: 0,
  averageRating: null,
};

const FILLED_HISTORY: WorkerEngagementHistory = {
  workerId: 'worker-1',
  items: [
    {
      id: 'engagement-1',
      projectId: 'project-1',
      openedOn: '2026-08-01',
      closedOn: '2026-08-20',
      dayRate: '487.6543',
      rating: 4,
    },
  ],
  averageDayRate: '487.6543',
  frequency: 1,
  averageRating: '4.00',
};

class FakeDayLabourApi implements Pick<DayLabourApi, 'listEngagements' | 'setEngagementDayRate'> {
  constructor(private readonly history: WorkerEngagementHistory) {}

  async listEngagements(): Promise<WorkerEngagementHistory> {
    return this.history;
  }

  async setEngagementDayRate(
    _projectId: string,
    engagementId: string,
    dayRate: string,
  ): Promise<{ engagementId: string; dayRate: string }> {
    return { engagementId, dayRate };
  }
}

async function createFixture(history: WorkerEngagementHistory) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([]),
      { provide: DayLabourApi, useValue: new FakeDayLabourApi(history) },
      { provide: I18nService, useClass: FakeI18nService },
      { provide: AuthService, useValue: new FakeAuthService() },
    ],
  });

  const fixture = TestBed.createComponent(WorkerHistoryPage);
  fixture.componentRef.setInput('projectId', 'project-1');
  fixture.componentRef.setInput('workerId', 'worker-1');
  fixture.detectChanges();
  await Promise.resolve();
  await Promise.resolve();
  fixture.detectChanges();

  return fixture;
}

describe('WorkerHistoryPage · AC-210-G, the never-engaged empty state', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('renders the translated never-engaged text for all three figures and the list, never a 0', async () => {
    const fixture = await createFixture(EMPTY_HISTORY);

    const figures = fixture.nativeElement.querySelector('[data-testid="worker-history-figures"]').textContent;
    expect(figures).toContain('hr.worker.pool.never_engaged');
    expect(figures).not.toContain('0.0000');

    expect(fixture.nativeElement.querySelector('[data-testid="worker-history-empty"]')).toBeTruthy();
  });
});

describe('WorkerHistoryPage · AC-210-C, a four-decimal day rate survives display', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
  });

  it('shows the stored four-decimal rate through the money helper, unchanged by any Number() pass', async () => {
    const fixture = await createFixture(FILLED_HISTORY);

    const rate = fixture.nativeElement.querySelector('[data-testid="worker-history-day-rate-engagement-1"]');
    expect(rate.textContent).toContain('487.6543');
  });
});
