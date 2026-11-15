import { TestBed } from '@angular/core/testing';
import { TemplateRef } from '@angular/core';
import { describe, expect, it } from 'vitest';

import { HeaderActionsService, PrimaryActionVariant } from './header-actions.service';

describe('HeaderActionsService (KAFF-936 AC-936-G)', () => {
  const template = {} as TemplateRef<unknown>;

  it('takes a constructive action', () => {
    const service = TestBed.inject(HeaderActionsService);
    service.set(template, 'primary');
    expect(service.template()).toBe(template);
  });

  it.each(['danger', 'danger-outline'])('refuses a %s action loudly and leaves the slot empty', (variant) => {
    const service = TestBed.inject(HeaderActionsService);
    expect(() => service.set(template, variant as PrimaryActionVariant)).toThrow(/destructive/);
    expect(service.template()).toBeNull();
  });
});
