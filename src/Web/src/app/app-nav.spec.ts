import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { App } from './app';
import { AuthService, Session } from './core/auth/auth.service';
import { SessionResolver } from './core/auth/session-resolver';
import { activeNavPath, navRowsFor } from './core/navigation/nav-rows';

@Component({ template: '' })
class Blank {}

const SESSION: Session = {
  userId: 'u',
  displayName: 'Test',
  role: 'Owner',
  departmentId: null,
  operationsSubDepartment: null,
  mustChangePassword: false,
  permissions: ['ProjectList', 'ProjectCreate', 'CatalogueManage', 'UserManage'],
  projects: [],
  teamProjects: [],
};

let fixture: ComponentFixture<App>;

async function mount(phone: boolean, url: string): Promise<HTMLElement> {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: query.includes('820') ? phone : false,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
  }));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'projects', component: Blank },
        { path: 'projects/new', component: Blank },
        { path: 'projects/:id', component: Blank },
        { path: 'catalogue', component: Blank },
      ]),
      provideHttpClient(),
      { provide: SessionResolver, useValue: { ensureResolved: async () => undefined } },
    ],
  });
  TestBed.inject(AuthService).set(SESSION);
  await TestBed.inject(Router).navigateByUrl(url);
  fixture = TestBed.createComponent(App);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('sidebar active row (F-215-1)', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('/projects/new lights exactly one row, the more specific one', async () => {
    const el = await mount(false, '/projects/new');
    const active = el.querySelectorAll('aside a.row.is-active');
    expect(active.length).toBe(1);
    expect(active[0].getAttribute('href')).toBe('/projects/new');
  });

  it('/projects/<id> lights Projects only', async () => {
    const el = await mount(false, '/projects/abc?x=1');
    const active = el.querySelectorAll('aside a.row.is-active');
    expect(active.length).toBe(1);
    expect(active[0].getAttribute('href')).toBe('/projects');
  });

  it('for every row path, exactly one row of the full list is active', () => {
    const every = [
      'ProjectList', 'ProjectCreate', 'CatalogueManage', 'BabManage', 'EmployeeManage', 'SubcontractorManage',
      'SupplierManage', 'ClientManage', 'UserManage', 'DepartmentManage', 'AuditRead',
    ];
    const rows = navRowsFor({ ...SESSION, permissions: every });
    for (const row of rows) {
      expect(activeNavPath(rows, row.path)).toBe(row.path);
      expect(activeNavPath(rows, `${row.path}/child`)).toBe(row.path);
    }
  });
});

describe('phone nav sheet (AC-215-F/G)', () => {
  afterEach(() => vi.unstubAllGlobals());

  async function open(): Promise<HTMLElement> {
    const el = await mount(true, '/projects');
    el.querySelector<HTMLButtonElement>('[data-testid="nav-toggle"]')!.click();
    fixture.detectChanges();
    return el;
  }

  it('opens as a dialog listing the permitted rows', async () => {
    const el = await open();
    const sheet = el.querySelector('.sheet')!;
    expect(sheet.getAttribute('role')).toBe('dialog');
    expect(sheet.querySelectorAll('a.row').length).toBe(navRowsFor(SESSION).length);
  });

  it('closes on backdrop tap', async () => {
    const el = await open();
    el.querySelector<HTMLElement>('[data-testid="nav-backdrop"]')!.click();
    fixture.detectChanges();
    expect(el.querySelector('.sheet')).toBeNull();
  });

  it('closes on row tap', async () => {
    const el = await open();
    el.querySelector<HTMLElement>('.sheet a.row[href="/catalogue"]')!.click();
    fixture.detectChanges();
    expect(el.querySelector('.sheet')).toBeNull();
  });
});
