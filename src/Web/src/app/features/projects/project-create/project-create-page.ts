import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormField, form, required, schema } from '@angular/forms/signals';
import { Router } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { AuthApi } from '../../../core/auth/auth.api';
import { AuthService } from '../../../core/auth/auth.service';
import { ClientSummary, ClientsApi } from '../../../core/clients/clients.api';
import { contractTypeKey, projectWorkspaceStatusKey } from '../../../core/i18n/enum-keys';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ContractType } from '../../../core/projects/projects.api';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffSelect, SelectOption } from '../../../shared/kaff-select/kaff-select';
import { ProjectCreate, ProjectCreateApi } from './project-create.api';

interface ProjectDraft {
  name: string;
  clientId: string;
  contractType: ContractType;
}

const rules = schema<ProjectDraft>((path) => {
  required(path.name);
});

/** spec.md section 16 assumption 1, shown as the placeholder and the hint; the SERVER applies the default, not this file. */
const TYPES: readonly ContractType[] = ['LumpSum', 'CostPlus', 'Design'];

/**
 * Create a project. **Signal forms** (`form()` + `[formField]`) for the three typed fields; the client and the
 * contract type are choices from a closed list and are set on the same model signal, so there is one model and
 * one payload.
 *
 * **There is no contract value field, and that is the point.** The value is derived from the signed BOQ
 * (decisions.md, the BOQ work of 2026-11-01); this form opens the project shell and sends the user on to the BOQ,
 * where building and signing it is what produces the value.
 *
 * The server owns every rule. The form checks only what it can ask the user for before sending: a name, a
 * client, and that a rate field holds a number. Errors show after the first attempt, so a half-typed field is
 * not scolded; a refusal from the server renders its own message key in place.
 */
@Component({
  selector: 'kaff-project-create-page',
  imports: [FormField, KaffBanner, KaffButton, KaffField, KaffPageHead, KaffSelect],
  templateUrl: './project-create-page.html',
  styleUrl: './project-create-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectCreatePage {
  private readonly api = inject(ProjectCreateApi);
  private readonly clientsApi = inject(ClientsApi);
  private readonly authApi = inject(AuthApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly i18n = inject(I18nService);
  protected readonly setupWordKey = projectWorkspaceStatusKey('Setup');

  private readonly model = signal<ProjectDraft>({
    name: '',
    clientId: '',
    contractType: 'LumpSum',
  });

  protected readonly projectForm = form(this.model, rules);

  protected readonly types = TYPES.map((value) => ({ value, labelKey: contractTypeKey(value) }));
  protected readonly clients = signal<readonly ClientSummary[]>([]);
  protected readonly clientsFailure = signal<string | null>(null);
  protected readonly loadingClients = signal(true);
  protected readonly attempted = signal(false);
  protected readonly busy = signal(false);
  protected readonly refusal = signal<string | null>(null);

  protected readonly contractType = computed(() => this.model().contractType);
  protected readonly clientId = computed(() => this.model().clientId);

  protected readonly clientOptions = computed<readonly SelectOption[]>(() => [
    { value: '', label: this.i18n.t('projects.create.client_placeholder') },
    ...this.clients().map((client) => ({ value: client.id, label: client.name })),
  ]);

  protected readonly nameError = computed(() =>
    this.attempted() && this.model().name.trim().length === 0 ? 'errors.project.name_required' : null,
  );
  protected readonly clientError = computed(() =>
    this.attempted() && this.model().clientId === '' ? 'errors.project.client_required' : null,
  );

  constructor() {
    void this.loadClients();
  }

  protected setContractType(type: ContractType): void {
    this.model.update((current) => ({ ...current, contractType: type }));
  }

  protected setClient(id: string): void {
    this.model.update((current) => ({ ...current, clientId: id }));
  }

  protected async onSubmit(): Promise<void> {
    if (this.busy()) {
      return;
    }

    this.attempted.set(true);
    this.refusal.set(null);

    const value = this.model();

    if (value.name.trim().length === 0 || value.clientId === '') {
      return;
    }

    const payload: ProjectCreate = {
      name: value.name.trim(),
      clientId: value.clientId,
      contractType: value.contractType,
    };

    this.busy.set(true);
    try {
      const created = await this.api.create(payload);
      await this.refreshSession();
      await this.router.navigateByUrl(this.destination(created.id, value.contractType));
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * The session lists the projects a caller may open, and it was read before this one existed: the workspace and
   * BOQ guards would refuse the new project's own URL until it is read again. Best effort; a failure leaves the
   * list route as the destination.
   */
  private async refreshSession(): Promise<void> {
    try {
      this.auth.set(await this.authApi.me());
    } catch {
      // the destination falls back to the list below
    }
  }

  /** A Lump Sum project goes straight to its BOQ, the next thing it needs. A caller who cannot open it goes to the list. */
  private destination(projectId: string, type: ContractType): string {
    const entry = this.auth.current()?.projects.find((project) => project.projectId === projectId);

    if (entry === undefined) {
      return '/projects';
    }

    return type === 'LumpSum' && entry.permissions.includes('ProjectManage')
      ? `/projects/${projectId}/boq`
      : `/projects/${projectId}`;
  }

  private async loadClients(): Promise<void> {
    try {
      this.clients.set(await this.clientsApi.list('', 'active'));
    } catch (error) {
      this.clientsFailure.set(toProblem(error).messageKey);
    } finally {
      this.loadingClients.set(false);
    }
  }
}
