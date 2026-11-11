import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { AssignmentLevel, Role } from '../auth/auth.service';

/** spec.md §5 — three types, one project entity. Type dispatches, it does not fork. */
export type ContractType = 'LumpSum' | 'CostPlus' | 'Design';

/**
 * spec.md §13's project state machine, verbatim and complete.
 *
 * **This is not Kaff's Arabic status vocabulary and must never become it** — D-014, and
 * `ProjectStatus.cs`'s own remarks. متعثرة and تم تأجيلها are health tags with no state here
 * (D-044 §7), and what انتهت means is still open (`stories/questions-for-karim.md` Q18). See
 * `projectWorkspaceStatusKey` in `core/i18n/enum-keys.ts` for the word each state renders.
 */
export type ProjectStatus =
  | 'Setup'
  | 'Active'
  | 'HandoverPending'
  | 'Handover'
  | 'UnderWarranty'
  | 'Closed'
  | 'Stopped'
  | 'Terminated';

/**
 * One project row — `ListProjects.Response`'s `ProjectSummary`, field for field.
 *
 * **`contractValue` is a string, and stays one.** D-135: money crosses the wire as a JSON string both
 * directions, because a JSON number becomes a JavaScript `double` and `decimal(18,4)` does not survive
 * that round trip. Pass it to `I18nService.formatAmount` directly; never `Number()` it.
 *
 * **It is `null` for Cost Plus**, which has no signed lump value to carry (spec.md §5.2), and for a
 * Design project before the value is set.
 *
 * **There is no client name here, only `clientId`** — the server sends what it sends, and this story
 * does not change the API.
 */
export interface ProjectSummary {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly clientId: string;
  readonly contractType: ContractType;
  readonly status: ProjectStatus;
  readonly contractValue: string | null;
  /** AC-417-J: true for an unsigned project whose follow-up period ended. Listed only with `includeArchived`. */
  readonly isArchived?: boolean;
  /** The day the follow-up period ends (`yyyy-mm-dd`); null once signed. */
  readonly followUpEndsOn?: string | null;
}

/**
 * `GET /api/projects/{projectId}` — `GetProject.Response`, field for field. The project workspace's header
 * and the facts the contract type declares. Money and rates are strings (D-135; a rate is the wire
 * fraction, `"0.15"` is 15%); never `Number()` one. `contractValue` is null for Cost Plus and for a
 * project whose BOQ is not signed. `holdRate` and `advanceRate` are Lump Sum only.
 */
export interface ProjectDetail {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly clientId: string;
  readonly clientName: string;
  readonly contractType: ContractType;
  readonly status: ProjectStatus;
  readonly contractValue: string | null;
  readonly holdRate: string | null;
  readonly advanceRate: string | null;
  readonly teamSize: number;
  /**
   * The dates and money the API added with the overview (`GetProject.Response`). Optional here only so
   * older fixtures still type-check; the server always sends them (a date as null until it happens).
   */
  readonly signedOn?: string | null;
  readonly startedOn?: string | null;
  readonly handoverOn?: string | null;
  readonly warrantyEndsOn?: string | null;
  readonly closedOn?: string | null;
  readonly lastDailyLogOn?: string | null;
  /** Null for a caller who holds none of the extract-money permissions. Every figure is a string (D-135). */
  readonly money?: ProjectMoney | null;
  readonly isArchived?: boolean;
  readonly followUpEndsOn?: string | null;
  /** KAFF-415: the link both ways between a design project and the execution project made from it. */
  readonly designProjectId?: string | null;
  readonly executionProjectId?: string | null;
  /** A reference only (decimal string); no BOQ line is seeded from it. */
  readonly designAreaSquareMetres?: string | null;
  /** The furthest stage reached (the stage line). The server sends it for every status. */
  readonly stage: ProjectStage;
  readonly isSigned: boolean;
  /** True when a daily log exists for today's site day. */
  readonly loggedToday: boolean;
  /** Which tabs this caller may open. The server decides; a false tab is absent from the frame. */
  readonly tabs: ProjectTabs;
}

/** `ProjectStage` on the wire: the three stages the stage line draws (D-218 item 3). */
export type ProjectStage = 'Boq' | 'Execution' | 'Handover';

export interface ProjectTabs {
  readonly overview: boolean;
  readonly boq: boolean;
  readonly dailyLog: boolean;
  readonly extracts: boolean;
  readonly team: boolean;
}

/** `POST /api/projects/{id}/convert-to-execution` response (KAFF-415). */
export interface ConvertedProject {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly contractType: ContractType;
  readonly designProjectId: string;
}

export interface ReopenResult {
  readonly projectId: string;
  readonly followUpStartedOn: string;
  readonly followUpEndsOn: string | null;
}

/** What the client has been billed, has paid and still owes (issued extracts only). */
export interface ProjectMoney {
  readonly certifiedToDate: string;
  readonly billedToDate: string;
  readonly collectedToDate: string;
  readonly outstanding: string;
}

/** `ProgressKind` on the wire. A Cost Plus reading is `MonetaryOnly` and never carries a `completion`. */
export type ProgressKind = 'Percentage' | 'MonetaryOnly' | 'StageWeighted';

/** One Bab of a Lump Sum project. `completion` is a fraction string and is ABSENT when there is nothing to measure against. */
export interface BabProgress {
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly completion?: string;
  readonly executedValue?: string;
  readonly isActive: boolean;
}

/**
 * `GET /api/projects/{id}/progress`. **Every member after `asOf` is absent, not zero, when it does not
 * apply**: a missing `completion` means draw no bar. Money members are absent for a caller who may not see money.
 */
export interface ProjectProgress {
  readonly kind: ProgressKind;
  readonly asOf: string;
  readonly completion?: string;
  readonly executedValue?: string;
  readonly contractValue?: string;
  readonly certifiedToDate?: string;
  readonly costToDate?: string;
  readonly supervisionToDate?: string;
  readonly babs?: readonly BabProgress[];
}

/** `GET /api/projects/{id}/team`: one active assignment. */
export interface TeamMember {
  readonly userId: string;
  readonly fullName: string;
  readonly role: Role;
  readonly level: AssignmentLevel;
  readonly assignedAt: string;
}

/**
 * `GET /api/projects` — S-050's rows. KAFF-401.
 *
 * Gated `Permission.ProjectList` (company-wide: Owner, Finance, Technical Office, Marketing, Site
 * Engineer) and the handler still filters the rows to what the caller is assigned to, every project
 * for the Owner. There is one call and no paging: `ListProjects` carries `total` but has no page
 * parameter to send.
 */
@Injectable({ providedIn: 'root' })
export class ProjectsApi {
  private readonly http = inject(HttpClient);

  /** One project's header and terms. Gated `ProjectRead` + assignment; HR is refused, not redacted. */
  async get(projectId: string): Promise<ProjectDetail> {
    return await firstValueFrom(this.http.get<ProjectDetail>(`api/projects/${projectId}`));
  }

  /** Where the project stands in the form its contract type allows. A Design project answers 409 `billing.calculator_not_implemented`. */
  async progress(projectId: string): Promise<ProjectProgress> {
    return await firstValueFrom(this.http.get<ProjectProgress>(`api/projects/${projectId}/progress`));
  }

  /** Who is assigned, with names. Gated `ProjectRead` + assignment. */
  async team(projectId: string): Promise<readonly TeamMember[]> {
    return await firstValueFrom(this.http.get<TeamMember[]>(`api/projects/${projectId}/team`));
  }

  /** The rows, and the server's own total — read rather than recounted on the client. */
  async list(includeArchived = false): Promise<{ readonly projects: readonly ProjectSummary[]; readonly total: number }> {
    return await firstValueFrom(
      this.http.get<{ projects: ProjectSummary[]; total: number }>('api/projects', {
        params: includeArchived ? { includeArchived: true } : {},
      }),
    );
  }

  /** AC-417-J: restart the follow-up period of an archived project. 409 `errors.project.not_archived` otherwise. */
  async reopen(projectId: string): Promise<ReopenResult> {
    return await firstValueFrom(this.http.post<ReopenResult>(`api/projects/${projectId}/reopen`, {}));
  }

  /** AC-415-A: a new execution project linked to this design project. The design project is not changed. */
  async convertToExecution(designProjectId: string, name: string, contractType: 'LumpSum' | 'CostPlus'): Promise<ConvertedProject> {
    return await firstValueFrom(
      this.http.post<ConvertedProject>(`api/projects/${designProjectId}/convert-to-execution`, { name, contractType }),
    );
  }
}
