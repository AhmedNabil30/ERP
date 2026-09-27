import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';
import { ClientsApi } from '../../../core/clients/clients.api';
import { contractTypeKey, projectStatusKey } from '../../../core/i18n/enum-keys';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectSummary, ProjectsApi } from '../../../core/projects/projects.api';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

/**
 * S-050 · the project list. KAFF-401.
 *
 * **This screen exists because day labour was unreachable.** The whole day-labour feature — worker
 * pool, register-from-site, engage, close, rate — shipped behind `/projects/:projectId/day-labour`, a
 * URL carrying a project GUID that no screen rendered (`ux/research/flows.md`: *"The flow is right; the
 * way in does not exist"*). A row here links to it whenever the session's own project entry carries
 * `DayLabourSiteManage`, which is the same fact `dayLabourSiteManageGuard` reads and the same one
 * `GET /api/auth/me` has been returning since KAFF-105b. **No backend change and no new permission.**
 *
 * **The link is convenience; the server decides.** Every day-labour endpoint is gated
 * `Permission.DayLabourSiteManage` + `ProjectScope.FromRoute()` and answers `403` regardless of what
 * this template rendered.
 *
 * **No status label is invented here.** See `projectStatusKey` — six of the eight states of `spec.md`
 * §13 have no ruled Arabic label, and Q18 is the open question. Those rows render a placeholder.
 *
 * **No paging, no search and no filter strip.** `GET /api/projects` takes no parameters, so there is
 * nothing to send and a client-side filter over a server-filtered list is a list that lies. The fourth
 * `kaff-state` mode, `filtered-empty`, renders for the one narrowing this screen cannot avoid: the
 * server returned rows and none of them carries a screen this session can open. That is a different
 * sentence from "no projects", and telling an Owner "no projects" while eight exist is the failure
 * `clients.empty.filtered.*` was written to prevent. That panel sits above the rows and never replaces
 * them — see the template.
 */
@Component({
  selector: 'kaff-project-list-page',
  imports: [RouterLink, KaffBadge, KaffState, KaffTable, KaffTableRow],
  templateUrl: './project-list-page.html',
  styleUrl: './project-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectListPage {
  private readonly api = inject(ProjectsApi);
  private readonly clients = inject(ClientsApi);
  private readonly auth = inject(AuthService);

  protected readonly i18n = inject(I18nService);
  protected readonly contractTypeKey = contractTypeKey;
  protected readonly projectStatusKey = projectStatusKey;

  protected readonly projects = signal<readonly ProjectSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);

  /**
   * Client id → name, and empty whenever the session may not read the client master.
   *
   * **`ListProjects` carries `clientId` and no client name**, and this story may not change the API.
   * `ClientManage` is Owner and Marketing only (`PermissionCatalogue.cs:342`), so Finance, the
   * Technical Office and a Site Engineer cannot fill this column at all — for them the column is not
   * rendered rather than rendered blank, see {@link canReadClients}. The real fix is a `clientName` on
   * `ProjectSummary`; it is reported, not worked around by a per-row fetch.
   */
  private readonly clientNames = signal<ReadonlyMap<string, string>>(new Map());

  protected readonly canReadClients = computed(
    () => this.auth.current()?.permissions.includes('ClientManage') ?? false,
  );

  /** `ux/components.md:259` — identity first: name leads, code demotes inside the same cell. */
  protected readonly rowColumns = computed(() =>
    this.canReadClients()
      ? 'minmax(12rem, 1fr) minmax(8rem, 0.6fr) 8rem 8rem 9rem'
      : 'minmax(12rem, 1fr) 8rem 8rem 9rem',
  );

  protected readonly headerColumns = computed<readonly TableColumnDef[]>(() => [
    { labelKey: 'projects.field.name' },
    ...(this.canReadClients() ? [{ labelKey: 'projects.field.client' }] : []),
    { labelKey: 'projects.field.contract_type' },
    { labelKey: 'projects.field.status' },
    { labelKey: 'projects.field.contract_value', align: 'end' as const },
  ]);

  /** The server's own total (`ListProjects.Response.Total`), not a count of the rows on screen. */
  protected readonly total = signal(0);

  protected readonly countText = computed(() =>
    this.i18n.t('projects.list.count', { count: this.total() }),
  );

  /**
   * True when rows came back but none of them is a project this session can open. The list is not
   * empty and the operator is not lost — they are looking at projects that carry no screen for them
   * yet, which is a different sentence from "no projects".
   */
  protected readonly noneReachable = computed(
    () => this.projects().length > 0 && !this.projects().some((project) => this.canOpen(project)),
  );

  constructor() {
    void this.reload();
  }

  protected trackProject(_index: number, project: ProjectSummary): string {
    return project.id;
  }

  /**
   * Mirrors `dayLabourSiteManageGuard` exactly — `DayLabourSiteManage` is `ProjectScoped` (D-140
   * point 1), so the answer is on this project's own `Session.projects` entry and never on the
   * company-wide permission list.
   */
  protected canOpen(project: ProjectSummary): boolean {
    const entry = this.auth.current()?.projects.find((row) => row.projectId === project.id);
    return entry?.permissions.includes('DayLabourSiteManage') ?? false;
  }

  protected clientName(project: ProjectSummary): string | null {
    return this.clientNames().get(project.clientId) ?? null;
  }

  /** D-135: the wire string reaches `Intl` untouched. `formatAmount` — the column names the unit. */
  protected contractValueText(project: ProjectSummary): string {
    return project.contractValue === null
      ? this.i18n.t('projects.value.not_applicable')
      : this.i18n.formatAmount(project.contractValue);
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);

    try {
      const response = await this.api.list();
      this.projects.set(response.projects);
      this.total.set(response.total);
      void this.loadClientNames();
    } catch (error) {
      // A 403 here is the mechanism working, not a bug: the server decides, and the key is the
      // server's own — this file does not choose it.
      this.failure.set(toProblem(error).messageKey);
      this.projects.set([]);
      this.total.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * The client names, best effort and silent on failure: a column that cannot be filled is not a
   * reason to refuse the list the operator asked for. Called after the projects are on screen.
   */
  private async loadClientNames(): Promise<void> {
    if (!this.canReadClients()) {
      return;
    }

    try {
      const clients = await this.clients.list('', 'all');
      this.clientNames.set(new Map(clients.map((client) => [client.id, client.name])));
    } catch {
      this.clientNames.set(new Map());
    }
  }
}
