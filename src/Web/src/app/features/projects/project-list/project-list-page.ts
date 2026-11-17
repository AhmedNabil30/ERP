import { ChangeDetectionStrategy, Component, DestroyRef, TemplateRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';
import { ClientsApi } from '../../../core/clients/clients.api';
import { shownStatus } from '../project-status-pill';
import { contractTypeKey, projectWorkspaceStatusKey } from '../../../core/i18n/enum-keys';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ProjectSummary, ProjectsApi } from '../../../core/projects/projects.api';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffState } from '../../../shared/kaff-state/kaff-state';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

/**
 * S-050 · the project list. KAFF-401.
 *
 * **A row opens the project workspace** (`/projects/:projectId`, the hub) and nothing else. It used to
 * link straight to day labour, which made "open a project" mean "register work"; the hub now carries
 * the onward links (BOQ, daily log, extracts, proof, day labour) grouped by what the person is doing.
 * The link is convenience; the server decides on every read the hub makes.
 *
 * **The state word is the hub's.** `projectWorkspaceStatusKey` serves both screens, so a project reads the
 * same on the list and in its workspace (Closed reads انتهت on both; Q18 still open).
 *
 * **No paging, no search and no filter strip.** `GET /api/projects` takes no parameters, so there is
 * nothing to send and a client-side filter over a server-filtered list is a list that lies.
 */
@Component({
  selector: 'kaff-project-list-page',
  imports: [RouterLink, KaffBadge, KaffButton, KaffState, KaffTable, KaffTableRow],
  templateUrl: './project-list-page.html',
  styleUrl: './project-list-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectListPage {
  private readonly api = inject(ProjectsApi);
  private readonly clients = inject(ClientsApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly headerActions = inject(HeaderActionsService);

  protected readonly i18n = inject(I18nService);
  protected readonly contractTypeKey = contractTypeKey;
  protected readonly projectStatusKey = projectWorkspaceStatusKey;
  protected readonly shown = shownStatus;

  protected readonly projects = signal<readonly ProjectSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  /** AC-417-J: archived (unsigned, follow-up ended) projects leave the default list; this brings them back. */
  protected readonly showArchived = signal(false);

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

  /**
   * False for the site engineer, whose every other screen is money-free (consultant review D4). Convenience:
   * the figure is not rendered, and the server still decides what `GET /api/projects` carries.
   */
  protected readonly seesMoney = computed(() => this.auth.current()?.role !== 'SiteEngineer');

  /** `ux/components.md:259` — identity first: the name leads (the project code is not shown). */
  protected readonly rowColumns = computed(
    () =>
      `minmax(12rem, 1fr)${this.canReadClients() ? ' minmax(8rem, 0.6fr)' : ''} 8rem 8rem${this.seesMoney() ? ' 9rem' : ''}`,
  );

  protected readonly headerColumns = computed<readonly TableColumnDef[]>(() => [
    { labelKey: 'projects.field.name' },
    ...(this.canReadClients() ? [{ labelKey: 'projects.field.client' }] : []),
    { labelKey: 'projects.field.contract_type' },
    { labelKey: 'projects.field.status' },
    ...(this.seesMoney() ? [{ labelKey: 'projects.field.contract_value', align: 'end' as const }] : []),
  ]);

  /** The server's own total (`ListProjects.Response.Total`), not a count of the rows on screen. */
  protected readonly total = signal(0);

  protected readonly countText = computed(() =>
    this.i18n.t('projects.list.count', { count: this.total() }),
  );

  /**
   * "Create a project" (the Owner and the Technical Office hold `ProjectCreate`). Convenience: the button is
   * not rendered for anyone else, and `POST /api/projects` refuses them regardless. It projects into the shell's
   * action strip the way the client list's does, and again inside the empty state, where the first project starts.
   */
  protected readonly canCreate = computed(
    () => this.auth.current()?.permissions.includes('ProjectCreate') ?? false,
  );
  private readonly createAction = viewChild<TemplateRef<unknown>>('createAction');

  constructor() {
    void this.reload();
    effect(() => this.headerActions.set(this.canCreate() ? (this.createAction() ?? null) : null, 'primary'));
    inject(DestroyRef).onDestroy(() => this.headerActions.clear());
  }

  protected toggleArchived(): void {
    this.showArchived.update((on) => !on);
    void this.reload();
  }

  protected day(value: string): string {
    const [year = 0, month = 1, day = 1] = value.split('-').map(Number);
    return this.i18n.formatDate(new Date(year, month - 1, day), { dateStyle: 'medium' });
  }

  protected async onCreate(): Promise<void> {
    await this.router.navigate(['/projects/new']);
  }

  protected trackProject(_index: number, project: ProjectSummary): string {
    return project.id;
  }

  protected clientName(project: ProjectSummary): string | null {
    return this.clientNames().get(project.clientId) ?? null;
  }

  /** D-135: the wire string reaches `Intl` untouched. `formatAmount` — the column names the unit. */
  protected contractValueText(project: ProjectSummary): string {
    return project.contractValue == null
      ? this.i18n.t('projects.value.not_applicable')
      : this.i18n.formatAmount(project.contractValue);
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);

    try {
      const response = await this.api.list(this.showArchived());
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
