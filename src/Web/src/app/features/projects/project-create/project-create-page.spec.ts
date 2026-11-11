import { HttpErrorResponse } from '@angular/common/http';
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

const created = { id: 'new-1', code: 'PRJ-1', name: 'x', status: 'Setup' };

interface Setup {
  create?: ReturnType<typeof vi.fn>;
  quickAdd?: ReturnType<typeof vi.fn>;
  quickPhoneCheck?: ReturnType<typeof vi.fn>;
  clients?: unknown[];
}

async function render(setup: Setup = {}) {
  const create = setup.create ?? vi.fn().mockResolvedValue(created);
  const quickAdd = setup.quickAdd ?? vi.fn();
  const quickPhoneCheck = setup.quickPhoneCheck ?? vi.fn().mockResolvedValue([]);
  const session = { projects: [{ projectId: 'new-1', permissions: ['ProjectManage'] }], permissions: ['ProjectCreate'] };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: I18nService, useClass: FakeI18n },
      { provide: ProjectCreateApi, useValue: { create } },
      {
        provide: ClientsApi,
        useValue: {
          list: vi.fn().mockResolvedValue(setup.clients ?? [{ id: 'c-1', name: 'شركة النيل' }]),
          quickAdd,
          quickPhoneCheck,
        },
      },
      { provide: AuthApi, useValue: { me: vi.fn().mockResolvedValue(session) } },
      { provide: AuthService, useValue: { current: signal(session), set: vi.fn() } },
    ],
  });
  const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  fixture = TestBed.createComponent(ProjectCreatePage);
  await fixture.whenStable();
  fixture.detectChanges();
  return { root: fixture.nativeElement as HTMLElement, create, navigate, quickAdd, quickPhoneCheck };
}

async function settle(): Promise<void> {
  await fixture.whenStable();
  fixture.detectChanges();
}

function type(root: HTMLElement, testId: string, value: string): void {
  const input = root.querySelector<HTMLInputElement>(`[data-testid="${testId}"]`)!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function click(root: HTMLElement, testId: string): void {
  root.querySelector<HTMLElement>(`[data-testid="${testId}"]`)!.click();
}

async function chooseClient(root: HTMLElement): Promise<void> {
  root.querySelector<HTMLInputElement>('[data-testid="project-client"]')!.dispatchEvent(new Event('focus'));
  await settle();
  click(root, 'project-client-option');
  await settle();
}

function conflict(code: string): HttpErrorResponse {
  return new HttpErrorResponse({ status: 409, error: { code, messageKey: `errors.${code}` } });
}

describe('ProjectCreatePage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('has no contract value, hold or advance field, and no explanatory note', async () => {
    const { root } = await render();

    expect(root.querySelector('[data-testid*="value"]:is(input, select)')).toBeNull();
    expect(root.querySelector('[data-testid="project-hold"]')).toBeNull();
    expect(root.querySelector('[data-testid="project-advance"]')).toBeNull();
    expect(root.querySelector('[data-testid="project-value-note"]')).toBeNull();
  });

  it('preselects no contract type', async () => {
    const { root } = await render();

    for (const type of ['LumpSum', 'CostPlus', 'Design']) {
      expect(root.querySelector<HTMLInputElement>(`[data-testid="project-type-${type}"]`)!.checked).toBe(false);
    }
  });

  it('refuses an empty submit with a field error each, and sends nothing', async () => {
    const { root, create } = await render();

    click(root, 'project-create-submit');
    await settle();

    expect(create).not.toHaveBeenCalled();
    expect(root.querySelector('[data-testid="project-name-error"]')?.textContent).toContain('errors.project.name_required');
    expect(root.querySelector('[data-testid="project-client-error"]')?.textContent).toContain('errors.project.client_required');
    expect(root.querySelector('[data-testid="project-type-error"]')?.textContent).toContain('errors.project.contract_type_required');
  });

  it('sends the typed name, the chosen client and the chosen type, then opens the workspace', async () => {
    const { root, create, navigate } = await render();

    type(root, 'project-name', '  فيلا الشروق ');
    await chooseClient(root);
    click(root, 'project-type-CostPlus');
    await settle();
    click(root, 'project-create-submit');
    await settle();

    expect(create).toHaveBeenCalledExactlyOnceWith({ name: 'فيلا الشروق', clientId: 'c-1', contractType: 'CostPlus' });
    expect(navigate).toHaveBeenCalledWith('/projects/new-1');
  });

  it('shows a name already used for this client on the name field', async () => {
    const create = vi.fn().mockRejectedValue(conflict('project.name_taken_for_client'));
    const { root, navigate } = await render({ create });

    type(root, 'project-name', 'فيلا');
    await chooseClient(root);
    click(root, 'project-type-LumpSum');
    await settle();
    click(root, 'project-create-submit');
    await settle();

    expect(root.querySelector('[data-testid="project-name-error"]')?.textContent).toContain('errors.project.name_taken_for_client');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('shows the server refusal of a missing type on the type group', async () => {
    const create = vi.fn().mockRejectedValue(
      new HttpErrorResponse({ status: 400, error: { code: 'project.contract_type_required', messageKey: 'errors.project.contract_type_required' } }),
    );
    const { root } = await render({ create });

    type(root, 'project-name', 'فيلا');
    await chooseClient(root);
    click(root, 'project-type-LumpSum');
    await settle();
    // The form believes a type is chosen; the server refuses anyway.
    click(root, 'project-create-submit');
    await settle();

    expect(root.querySelector('[data-testid="project-type-error"]')?.textContent).toContain('errors.project.contract_type_required');
  });

  it('adds a client from the panel and selects it', async () => {
    const quickAdd = vi.fn().mockResolvedValue({ id: 'c-9', name: 'عميل تجربة' });
    const { root } = await render({ quickAdd, clients: [] });

    click(root, 'project-new-client');
    await settle();
    type(root, 'quick-name', ' عميل تجربة ');
    type(root, 'quick-phone', '01000000000');
    click(root, 'quick-kind-Individual');
    await settle();
    click(root, 'quick-client-save');
    await settle();

    expect(quickAdd).toHaveBeenCalledExactlyOnceWith({
      name: 'عميل تجربة',
      phone: '01000000000',
      kind: 'Individual',
      acknowledgedDuplicatePhone: false,
    });
    expect(root.querySelector('[data-testid="quick-client-panel"]')).toBeNull();
    expect(root.querySelector<HTMLInputElement>('[data-testid="project-client"]')!.value).toBe('عميل تجربة');
  });

  it('warns about a phone another client holds, shows no code, and sends the acknowledgement', async () => {
    const quickAdd = vi.fn().mockResolvedValue({ id: 'c-9', name: 'عميل تجربة' });
    const quickPhoneCheck = vi.fn().mockResolvedValue([{ id: 'c-1', code: 'C-10001', name: 'شركة النيل', isArchived: false }]);
    const { root } = await render({ quickAdd, quickPhoneCheck });

    root.querySelector<HTMLInputElement>('[data-testid="project-client"]')!.dispatchEvent(new Event('focus'));
    type(root, 'project-client', 'حسن');
    await settle();
    click(root, 'project-new-client');
    await settle();
    type(root, 'quick-name', 'عميل تجربة');
    type(root, 'quick-phone', '01000000000');
    root.querySelector<HTMLElement>('[data-testid="quick-phone"]')!.dispatchEvent(new Event('blur'));
    await settle();

    const warning = root.querySelector('[data-testid="quick-client-duplicate-warning"]');
    expect(warning?.textContent).toContain('شركة النيل');
    expect(warning?.textContent).not.toContain('C-10001');

    click(root, 'quick-kind-Corporate');
    root.querySelector<HTMLInputElement>('[data-testid="quick-client-duplicate-acknowledge"]')!.click();
    await settle();
    click(root, 'quick-client-save');
    await settle();

    expect(quickAdd).toHaveBeenCalledWith(expect.objectContaining({ kind: 'Corporate', acknowledgedDuplicatePhone: true }));
  });
});
