import { signal } from '@angular/core';
import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthApi } from '../../../core/auth/auth.api';
import { AuthService } from '../../../core/auth/auth.service';
import { ClientsApi } from '../../../core/clients/clients.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectCreateApi } from './project-create.api';
import { ProjectCreatePage } from './project-create-page';

class FakeI18n implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');

  t(key: string): string {
    return key;
  }
}

let fixture: ComponentFixture<ProjectCreatePage>;

async function render(create = vi.fn().mockResolvedValue({ id: 'new-1', code: 'PRJ-1', name: 'x', status: 'Setup' })): Promise<{ root: HTMLElement; create: ReturnType<typeof vi.fn>; navigate: ReturnType<typeof vi.fn> }> {
  const session = { projects: [{ projectId: 'new-1', permissions: ['ProjectManage'] }], permissions: ['ProjectCreate'] };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: I18nService, useClass: FakeI18n },
      { provide: ProjectCreateApi, useValue: { create } },
      { provide: ClientsApi, useValue: { list: vi.fn().mockResolvedValue([{ id: 'c-1', name: 'عميل' }]) } },
      { provide: AuthApi, useValue: { me: vi.fn().mockResolvedValue(session) } },
      { provide: AuthService, useValue: { current: signal(session), set: vi.fn() } },
    ],
  });
  const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  fixture = TestBed.createComponent(ProjectCreatePage);
  await fixture.whenStable();
  fixture.detectChanges();
  return { root: fixture.nativeElement as HTMLElement, create, navigate };
}

function type(root: HTMLElement, testId: string, value: string): void {
  const input = root.querySelector<HTMLInputElement>(`[data-testid="${testId}"]`)!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

describe('ProjectCreatePage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('has no contract value field: the value comes from the signed BOQ', async () => {
    const { root } = await render();

    expect(root.querySelector('[data-testid*="value"]:is(input, select)')).toBeNull();
    expect(root.querySelector('[data-testid="project-value-note"]')).not.toBeNull();
  });

  it('refuses an empty submit with the server key for the name, and sends nothing', async () => {
    const { root, create } = await render();

    root.querySelector<HTMLButtonElement>('button[data-testid="project-create-submit"]')!.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(create).not.toHaveBeenCalled();
    expect(root.querySelector('[data-testid="project-name-error"]')?.textContent).toContain('errors.project.name_required');
    expect(root.querySelector('[data-testid="project-client-error"]')?.textContent).toContain('errors.project.client_required');
  });

  it('sends the typed name, client and type, turns the hold percent into a fraction, and opens the BOQ', async () => {
    const { root, create, navigate } = await render();

    type(root, 'project-name', '  فيلا الشروق ');
    type(root, 'project-hold', '12.5');
    const select = root.querySelector<HTMLSelectElement>('[data-testid="project-client"]')!;
    select.value = 'c-1';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    root.querySelector<HTMLButtonElement>('button[data-testid="project-create-submit"]')!.click();
    await fixture.whenStable();

    expect(create).toHaveBeenCalledExactlyOnceWith({
      name: 'فيلا الشروق',
      clientId: 'c-1',
      contractType: 'LumpSum',
      holdPercentage: '0.125',
      clientAdvance: null,
    });
    expect(navigate).toHaveBeenCalledWith('/projects/new-1/boq');
  });

  it('hides the Lump Sum rates for Cost Plus and sends neither', async () => {
    const { root } = await render();

    root.querySelector<HTMLButtonElement>('button[data-testid="project-type-CostPlus"]')!.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(root.querySelector('[data-testid="project-hold"]')).toBeNull();
    expect(root.querySelector('[data-testid="project-advance"]')).toBeNull();
  });
});
