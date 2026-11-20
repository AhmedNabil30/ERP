import { Component, TemplateRef, signal } from '@angular/core';
import { Routes } from '@angular/router';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Extract, ExtractActions, WaitingOnBlock } from '../../../core/extracts/extracts.api';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';

/**
 * Test fixtures for the extract chain's specs. No test framework here: the specs bring their own. The translator
 * returns the key and its parameters, so a spec can assert which words a screen chose without a catalogue.
 */
export class FakeI18n {
  readonly locale = signal<'ar' | 'en'>('ar');

  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    return params === undefined ? key : `${key}${JSON.stringify(params)}`;
  }

  formatAmount(value: string): string {
    return value;
  }

  formatMoney(value: string): string {
    return value;
  }

  formatNumber(value: number | string): string {
    return String(value);
  }

  formatDate(): string {
    return 'DATE';
  }
}

export const NO_ACTIONS: ExtractActions = {
  submitQcReport: false,
  approveTechnicalOffice: false,
  approvePortion: false,
  rejectAtTechnicalOffice: false,
  applyDeductions: false,
  rejectAtAccounts: false,
  approveAsOwner: false,
  rejectAtOwner: false,
  issue: false,
  reviseEvidence: false,
  callerMayDecide: true,
  recordCollection: false,
  recordChequeMove: false,
  reverseCollection: false,
  raiseDispute: false,
  resolveDispute: false,
};

export function waiting(overrides: Partial<WaitingOnBlock> = {}): WaitingOnBlock {
  return {
    status: 'QcSubmitted',
    permission: 'QuantityGateApprove',
    roles: [],
    isDecisionGate: true,
    isTerminal: false,
    holders: [{ userId: 'u-to', fullName: 'كريم حسن', role: 'TechnicalOffice', level: 'Standard' }],
    barredHolders: [],
    ...overrides,
  };
}

export function extract(overrides: Partial<Extract> = {}, actions: Partial<ExtractActions> = {}): Extract {
  return {
    id: 'e-1',
    projectId: 'p-1',
    number: 3,
    periodStart: '2026-09-01',
    periodEnd: '2026-09-30',
    status: 'QcSubmitted',
    createdByUserId: 'u-se',
    createdAt: '2026-10-02T10:00:00Z',
    approvedPortion: '1',
    heldForNcrId: null,
    gateHistory: [],
    actions: { ...NO_ACTIONS, ...actions },
    evidence: null,
    document: null,
    changeOrders: [],
    waitingOn: waiting(),
    collection: null,
    ...overrides,
  };
}

/** The §15 extract 3 document: work 400,000, hold 80,000, advance 100,000, material net 30,000 recovered, net 190,000. */
export const DOCUMENT: NonNullable<Extract['document']> = {
  figures: [
    { key: 'holdToDate', labelKey: 'billing.component.HoldToDate', kind: 'Calculated', amount: '200000.0000', isMemo: true, derivation: [] },
  ],
  column: [
    { key: 'workValue', labelKey: 'billing.component.WorkValue', isDeduction: false, amount: '400000.0000' },
    { key: 'materialAdvance', labelKey: 'billing.component.MaterialAdvance', isDeduction: true, amount: '30000.0000' },
    { key: 'holdThisPeriod', labelKey: 'billing.component.Hold', isDeduction: true, amount: '80000.0000' },
    { key: 'advanceRecovered', labelKey: 'billing.component.AdvanceRecovery', isDeduction: true, amount: '100000.0000' },
  ],
  printedTotal: '190000.0000',
  columnSum: '190000.0000',
  foots: true,
  decimals: 2,
};

export const POSITION: NonNullable<Extract['collection']> = {
  expected: '190000.0000',
  expectedWithheld: '1900.0000',
  collected: '100000.0000',
  inCheques: '90000.0000',
  remaining: '0',
  received: '99000.0000',
  withheld: '1000.0000',
};

/** A session holding `permissions` on project p-1. */
export function session(permissions: readonly string[], userId = 'u-me') {
  return { current: signal({ userId, role: 'Finance', permissions: [], projects: [{ projectId: 'p-1', permissions }] }) };
}

export function byTestId(root: HTMLElement, testId: string): HTMLElement | null {
  return root.querySelector(`[data-testid="${testId}"]`);
}

/** The primary the screen placed in the shell's slot, rendered: null when there is none. */
export function primary(fixture: ComponentFixture<unknown>): HTMLElement | null {
  const template = TestBed.inject(HeaderActionsService).template() as TemplateRef<unknown> | null;
  if (template === null) {
    return null;
  }
  const view = template.createEmbeddedView(null);
  view.detectChanges();
  fixture.detectChanges();
  const host = document.createElement('div');
  view.rootNodes.forEach((node: Node) => host.appendChild(node));
  return host.querySelector('button');
}

export async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

/** Types into an input or a textarea the way a person does. */
export function type(element: HTMLElement | null, value: string): void {
  const control = (element?.tagName === 'INPUT' || element?.tagName === 'TEXTAREA' ? element : element?.querySelector('input,textarea')) as HTMLInputElement | null;
  if (control === null || control === undefined) {
    throw new Error('no control to type into');
  }
  control.value = value;
  control.dispatchEvent(new Event('input'));
}

@Component({ selector: 'kaff-test-blank', template: '' })
class Blank {}

/** Every path lands on a blank page, so a screen that navigates after a write does not fail the run. */
export const ANY_ROUTE: Routes = [{ path: '**', component: Blank }];
