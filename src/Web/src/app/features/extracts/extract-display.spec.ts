import { describe, expect, it } from 'vitest';

import { chainIndex, extractTaskKey, hasHiddenPrecision, isPositive, negated, percentOfFraction, wireAmount } from './extract-display';
import { waiting } from './testing/chain-fixtures';

describe('percentOfFraction', () => {
  it('shows a fraction as a percentage by moving the point, never by arithmetic', () => {
    expect(percentOfFraction('0.200000')).toBe('20');
    expect(percentOfFraction('1.000000')).toBe('100');
    expect(percentOfFraction('1')).toBe('100');
    expect(percentOfFraction('0.125')).toBe('12.5');
    expect(percentOfFraction('0.050000')).toBe('5');
  });
});

describe('hasHiddenPrecision', () => {
  it('is true only when rounding would hide a non-zero digit', () => {
    expect(hasHiddenPrecision('1231.6500', 2)).toBe(false);
    expect(hasHiddenPrecision('1231.6550', 2)).toBe(true);
  });
});

describe('wireAmount', () => {
  it('turns what a person types into the wire string, or refuses it', () => {
    expect(wireAmount('99,000.00')).toBe('99000.00');
    expect(wireAmount(' ٤٠٠٠٠ ')).toBe('40000');
    expect(wireAmount('0')).toBe('0');
    expect(wireAmount('')).toBeNull();
    expect(wireAmount('-5')).toBeNull();
    expect(wireAmount('12a')).toBeNull();
  });
});

describe('isPositive and negated', () => {
  it('reads a wire amount without building a number', () => {
    expect(isPositive('0.0000')).toBe(false);
    expect(isPositive('0.0100')).toBe(true);
    expect(isPositive(null)).toBe(false);
    expect(negated('80000.0000')).toBe('-80000.0000');
    expect(negated('0')).toBe('0');
  });
});

describe('chainIndex', () => {
  it('places each status on the seven-step chain, and a cancelled extract on none', () => {
    expect(chainIndex('Draft', true)).toBe(0);
    expect(chainIndex('Draft', false)).toBe(1);
    expect(chainIndex('QcSubmitted', false)).toBe(2);
    expect(chainIndex('TechnicalApproved', false)).toBe(3);
    expect(chainIndex('DeductionsApplied', false)).toBe(4);
    expect(chainIndex('OwnerApproved', false)).toBe(5);
    expect(chainIndex('Issued', false)).toBe(6);
    expect(chainIndex('Collected', false)).toBe(7);
    expect(chainIndex('Void', false)).toBeNull();
  });
});

describe('extractTaskKey', () => {
  it('names the task by the permission the next step needs', () => {
    expect(extractTaskKey(waiting({ permission: 'ExtractPrepare' }), false)).toBe('extract.task.revise');
    expect(extractTaskKey(waiting({ permission: 'FinancialMovementApprove', status: 'DeductionsApplied' }), false)).toBe('extract.task.owner');
    expect(extractTaskKey(waiting({ permission: 'FinancialMovementApprove', status: 'OwnerApproved' }), false)).toBe('extract.task.issue');
    expect(extractTaskKey(waiting({ permission: 'ExtractCollect' }), true)).toBe('extract.task.deposit');
    expect(extractTaskKey(waiting({ permission: 'ExtractCollect' }), false)).toBe('extract.task.collect');
  });
});
