import { describe, expect, it } from 'vitest';

import { ProjectStatus } from '../../core/projects/projects.api';
import { shownStatus } from './project-status-pill';

describe('shownStatus (D-228 item 4)', () => {
  it('shows an Active project with no entry as Setup, the «not started» word', () => {
    expect(shownStatus({ status: 'Active' })).toBe('Setup');
    expect(shownStatus({ status: 'Active', workStarted: false })).toBe('Setup');
  });

  it('shows Active from the first entry', () => {
    expect(shownStatus({ status: 'Active', workStarted: true })).toBe('Active');
    expect(shownStatus({ status: 'Active', contractType: 'Design' })).toBe('Active');
    expect(shownStatus({ status: 'Active', contractType: 'LumpSum' })).toBe('Setup');
  });

  it('leaves every other status as it is, started or not', () => {
    const others: ProjectStatus[] = ['Setup', 'HandoverPending', 'Handover', 'UnderWarranty', 'Closed', 'Stopped', 'Terminated'];
    for (const status of others) {
      expect(shownStatus({ status })).toBe(status);
      expect(shownStatus({ status, workStarted: true })).toBe(status);
    }
  });
});
