import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** spec.md §5 — three types, one project entity. Type dispatches, it does not fork. */
export type ContractType = 'LumpSum' | 'CostPlus' | 'Design';

/**
 * spec.md §13's project state machine, verbatim and complete.
 *
 * **This is not Kaff's Arabic status vocabulary and must never become it** — D-014, and
 * `ProjectStatus.cs`'s own remarks. متعثرة and تم تأجيلها are health tags with no state here
 * (D-044 §7), and what انتهت means is still open (`stories/questions-for-karim.md` Q18). See
 * `projectStatusKey` in `core/i18n/enum-keys.ts` for what this screen is allowed to render today.
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

  /** The rows, and the server's own total — read rather than recounted on the client. */
  async list(): Promise<{ readonly projects: readonly ProjectSummary[]; readonly total: number }> {
    return await firstValueFrom(
      this.http.get<{ projects: ProjectSummary[]; total: number }>('api/projects'),
    );
  }
}
