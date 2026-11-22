import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeAll, beforeEach, describe, expect, it } from 'vitest';

import { I18nService } from '../../../core/i18n/i18n.service';
import { AssignRequest, ProjectsApi, TeamCandidate, TeamMember, TeamRead } from '../../../core/projects/projects.api';
import { ProjectTeamTab } from './project-team-tab';

/** The team tab: what each caller sees, the add sheet's gaps and refusal, the remove confirmation. */

class FakeI18nService implements Pick<I18nService, 't' | 'locale'> {
  readonly locale = signal<'ar' | 'en'>('ar');
  t(key: string, params?: Readonly<Record<string, string | number>>): string {
    return params === undefined ? key : `${key} ${JSON.stringify(params)}`;
  }
}

const MEMBERS: readonly TeamMember[] = [
  { assignmentId: 'a-fin', userId: 'u-fin', fullName: 'سارة', role: 'Finance', level: 'Standard', origin: 'Automatic', canRemove: false, assignedAt: 't' },
  { assignmentId: 'a-to', userId: 'u-to', fullName: 'منى', role: 'TechnicalOffice', level: 'Standard', origin: 'Creator', canRemove: true, assignedAt: 't' },
  { assignmentId: 'a-se', userId: 'u-se', fullName: 'هاني', role: 'SiteEngineer', level: 'Supervisor', origin: 'Added', canRemove: true, assignedAt: 't' },
];

const CANDIDATES: readonly TeamCandidate[] = [
  { userId: 'c-eng', fullName: 'نورا', role: 'SiteEngineer', levelRequired: true },
  { userId: 'c-hr', fullName: 'علا', role: 'Hr', levelRequired: false },
];

class FakeApi implements Pick<ProjectsApi, 'teamRead' | 'teamCandidates' | 'assign' | 'removeMember'> {
  assigned: AssignRequest[] = [];
  removed: string[] = [];
  members: readonly TeamMember[] = MEMBERS;
  canManage = true;
  candidates: readonly TeamCandidate[] = CANDIDATES;
  assignFails: unknown = null;

  async teamRead(): Promise<TeamRead> {
    return { canManage: this.canManage, members: this.members };
  }
  async teamCandidates(): Promise<readonly TeamCandidate[]> {
    return this.candidates;
  }
  async assign(_id: string, request: AssignRequest): Promise<void> {
    if (this.assignFails !== null) {
      throw this.assignFails;
    }
    this.assigned.push(request);
  }
  async removeMember(_id: string, assignmentId: string): Promise<void> {
    this.removed.push(assignmentId);
    this.members = this.members.filter((m) => m.assignmentId !== assignmentId);
  }
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let i = 0; i < 10; i += 1) {
    await Promise.resolve();
    fixture.detectChanges();
  }
}

async function create(api: FakeApi, openAdd = false): Promise<ComponentFixture<ProjectTeamTab>> {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      { provide: ProjectsApi, useValue: api },
      { provide: I18nService, useClass: FakeI18nService },
    ],
  });
  const fixture = TestBed.createComponent(ProjectTeamTab);
  fixture.componentRef.setInput('projectId', 'p-1');
  fixture.componentRef.setInput('openAdd', openAdd);
  fixture.detectChanges();
  await settle(fixture);
  return fixture;
}

const el = (fixture: ComponentFixture<unknown>, id: string): HTMLElement | null =>
  (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${id}"]`);

/** The kit button's own `<button>` carries the test id. */
const click = async (fixture: ComponentFixture<unknown>, id: string): Promise<void> => {
  const node = el(fixture, id);
  expect(node, id).not.toBeNull();
  node!.click();
  await settle(fixture);
};

const choose = async (fixture: ComponentFixture<unknown>, id: string, value: string): Promise<void> => {
  const select = el(fixture, id) as HTMLSelectElement;
  select.value = value;
  select.dispatchEvent(new Event('change'));
  await settle(fixture);
};

describe('ProjectTeamTab', () => {
  beforeAll(() => {
    HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close ??= function close(this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
  });
  beforeEach(() => TestBed.resetTestingModule());

  it('draws no add primary and no remove control when the server says the caller cannot manage', async () => {
    const api = new FakeApi();
    api.canManage = false;
    api.members = MEMBERS.map((m) => ({ ...m, canRemove: false }));
    const fixture = await create(api);
    expect(fixture.componentInstance.declared()).toBeNull();
    expect(el(fixture, 'workspace-team-list')?.querySelectorAll('li').length).toBe(3);
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid^="team-remove-"]')).toBeNull();
  });

  it('draws the empty state, not a list shell, when the team has no rows (AC-115-F)', async () => {
    const api = new FakeApi();
    api.members = [];
    const fixture = await create(api);
    expect(el(fixture, 'workspace-team-list')).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('ul.lst')).toBeNull();
    expect(el(fixture, 'team-empty')).not.toBeNull();
    expect(el(fixture, 'team-no-engineer')).toBeNull();
  });

  it('declares the add primary for a caller who manages, and removes only rows the server marks removable', async () => {
    const fixture = await create(new FakeApi());
    expect(fixture.componentInstance.declared()).not.toBeNull();
    expect(el(fixture, 'team-remove-u-fin')).toBeNull();
    expect(el(fixture, 'team-remove-u-to')).not.toBeNull();
    expect(el(fixture, 'team-remove-u-se')).not.toBeNull();
  });

  it('marks the origin only where the server says Automatic or Creator, and draws no id', async () => {
    const fixture = await create(new FakeApi());
    expect(el(fixture, 'team-origin-u-fin')?.textContent).toContain('projects.team.origin.Automatic');
    expect(el(fixture, 'team-origin-u-to')?.textContent).toContain('projects.team.origin.Creator');
    expect(el(fixture, 'team-origin-u-se')).toBeNull();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('a-se');
  });

  it('asks for the seniority only after choosing a person who needs it', async () => {
    const fixture = await create(new FakeApi(), true);
    expect(el(fixture, 'team-level-Junior')).toBeNull();
    await choose(fixture, 'team-person', 'c-hr');
    expect(el(fixture, 'team-level-Junior')).toBeNull();
    await choose(fixture, 'team-person', 'c-eng');
    expect(el(fixture, 'team-level-Junior')).not.toBeNull();
    expect(el(fixture, 'team-level-Supervisor')).not.toBeNull();
  });

  it('pressing add with nothing chosen marks the person field and sends nothing', async () => {
    const api = new FakeApi();
    const fixture = await create(api, true);
    await click(fixture, 'team-add-confirm');
    expect(el(fixture, 'team-person-error')?.textContent).toContain('projects.team.error.person');
    expect(api.assigned).toEqual([]);
  });

  it('pressing add for an engineer with no seniority marks the seniority and sends nothing', async () => {
    const api = new FakeApi();
    const fixture = await create(api, true);
    await choose(fixture, 'team-person', 'c-eng');
    await click(fixture, 'team-add-confirm');
    expect(el(fixture, 'team-level-error')?.textContent).toContain('projects.team.error.level');
    expect(api.assigned).toEqual([]);
  });

  it('sends the user and the chosen seniority, then says who was added', async () => {
    const api = new FakeApi();
    const fixture = await create(api, true);
    await choose(fixture, 'team-person', 'c-eng');
    (el(fixture, 'team-level-Junior') as HTMLInputElement).click();
    await settle(fixture);
    await click(fixture, 'team-add-confirm');
    expect(api.assigned).toEqual([{ userId: 'c-eng', level: 'Junior' }]);
    expect(el(fixture, 'team-done')?.textContent).toContain('projects.team.added');
    expect(el(fixture, 'team-done')?.textContent).toContain('نورا');
  });

  it('sends Standard for a person who has no seniority', async () => {
    const api = new FakeApi();
    const fixture = await create(api, true);
    await choose(fixture, 'team-person', 'c-hr');
    await click(fixture, 'team-add-confirm');
    expect(api.assigned).toEqual([{ userId: 'c-hr', level: 'Standard' }]);
  });

  it('shows the server refusal inside the sheet and does not report success', async () => {
    const api = new FakeApi();
    api.assignFails = new HttpErrorResponse({
      status: 409,
      error: { code: 'errors.identity.user_already_assigned_to_project', title: 'x' },
    });
    const fixture = await create(api, true);
    await choose(fixture, 'team-person', 'c-hr');
    await click(fixture, 'team-add-confirm');
    expect(el(fixture, 'team-add-error')).not.toBeNull();
    expect(el(fixture, 'team-done')).toBeNull();
  });

  it('says nobody is available when the candidate list is empty', async () => {
    const api = new FakeApi();
    api.candidates = [];
    const fixture = await create(api, true);
    expect(el(fixture, 'team-none-available')).not.toBeNull();
    expect(el(fixture, 'team-add-confirm')).toBeNull();
  });

  it('asks before removing, then removes by assignment id and says who left', async () => {
    const api = new FakeApi();
    const fixture = await create(api);
    await click(fixture, 'team-remove-u-se');
    expect(api.removed).toEqual([]);
    expect((el(fixture, 'team-dialog') as HTMLDialogElement).hasAttribute('open')).toBe(true);
    await click(fixture, 'team-dialog-confirm');
    expect(api.removed).toEqual(['a-se']);
    expect(el(fixture, 'team-done')?.textContent).toContain('projects.team.removed');
    expect(el(fixture, 'workspace-member-u-se')).toBeNull();
  });

  it('cancelling the confirmation removes nobody', async () => {
    const api = new FakeApi();
    const fixture = await create(api);
    await click(fixture, 'team-remove-u-to');
    await click(fixture, 'team-dialog-cancel');
    expect(api.removed).toEqual([]);
  });
});
