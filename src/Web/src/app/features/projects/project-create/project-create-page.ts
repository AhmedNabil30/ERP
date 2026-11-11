import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { AuthApi } from '../../../core/auth/auth.api';
import { AuthService } from '../../../core/auth/auth.service';
import { ClientKind, ClientOption, ClientsApi, PhoneMatch } from '../../../core/clients/clients.api';
import { clientKindKey, contractTypeKey } from '../../../core/i18n/enum-keys';
import { I18nService } from '../../../core/i18n/i18n.service';
import { ContractType } from '../../../core/projects/projects.api';
import { DuplicatePhoneWarning } from '../../../shared/duplicate-phone-warning/duplicate-phone-warning';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { Choice, KaffChoiceGroup } from '../../../shared/kaff-choice-group/kaff-choice-group';
import { KaffCombobox } from '../../../shared/kaff-combobox/kaff-combobox';
import { KaffDetailPanel } from '../../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { SelectOption } from '../../../shared/kaff-select/kaff-select';
import { ProjectCreate, ProjectCreateApi } from './project-create.api';

const TYPES: readonly ContractType[] = ['LumpSum', 'CostPlus', 'Design'];
const KINDS: readonly ClientKind[] = ['Individual', 'Corporate'];

/**
 * Create a project (`.design/v2/team-a/project-create.html`, D-217, D-218). Three inputs: a name, a client
 * and a contract type. **Nothing is preselected** (D-217 item 34); the type is the person's choice.
 *
 * **There is no contract value, hold or advance field, and that is the point.** The value comes from the
 * signed BOQ; hold and advance are sent as absent, so the server applies the spec defaults. Finance is
 * assigned by the server (AC-5), so there is nothing here for it.
 *
 * The server owns every rule. The form checks only what it can ask for before sending: a name, a client and
 * a type. Errors show after the first attempt. A refused name for the same client
 * (`errors.project.name_taken_for_client`) renders on the name field, a missing type on the type group.
 *
 * A client who is not registered is added in a side panel (a sheet at 390px) without leaving the form.
 */
@Component({
  selector: 'kaff-project-create-page',
  imports: [
    DuplicatePhoneWarning,
    KaffBanner,
    KaffButton,
    KaffChoiceGroup,
    KaffCombobox,
    KaffDetailPanel,
    KaffField,
    KaffPageHead,
  ],
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

  protected readonly types: readonly Choice[] = TYPES.map((value) => ({ value, labelKey: contractTypeKey(value) }));
  protected readonly kinds: readonly Choice[] = KINDS.map((value) => ({ value, labelKey: clientKindKey(value) }));

  protected readonly name = signal('');
  protected readonly clientId = signal('');
  protected readonly contractType = signal('');

  protected readonly clients = signal<readonly ClientOption[]>([]);
  protected readonly clientsFailure = signal<string | null>(null);
  protected readonly loadingClients = signal(true);
  protected readonly attempted = signal(false);
  protected readonly busy = signal(false);
  protected readonly refusal = signal<string | null>(null);
  /** Set by the server's answer and cleared the moment the name or the client changes. */
  protected readonly serverNameKey = signal<string | null>(null);
  protected readonly serverTypeKey = signal<string | null>(null);

  protected readonly clientOptions = computed<readonly SelectOption[]>(() =>
    this.clients().map((client) => ({ value: client.id, label: client.name })),
  );

  protected readonly nameError = computed(
    () => this.serverNameKey() ?? (this.attempted() && this.name().trim().length === 0 ? 'errors.project.name_required' : null),
  );
  protected readonly clientError = computed(() =>
    this.attempted() && this.clientId() === '' ? 'errors.project.client_required' : null,
  );
  protected readonly typeError = computed(
    () => this.serverTypeKey() ?? (this.attempted() && this.contractType() === '' ? 'errors.project.contract_type_required' : null),
  );

  // The quick-add client panel.
  protected readonly quickOpen = signal(false);
  protected readonly quickName = signal('');
  protected readonly quickPhone = signal('');
  protected readonly quickKind = signal('');
  protected readonly quickAttempted = signal(false);
  protected readonly quickBusy = signal(false);
  protected readonly quickRefusal = signal<string | null>(null);
  protected readonly quickMatches = signal<readonly PhoneMatch[]>([]);
  protected readonly quickAcknowledged = signal(false);
  private typed = '';

  protected readonly quickNameError = computed(() =>
    this.quickAttempted() && this.quickName().trim().length === 0 ? 'errors.master.name_required' : null,
  );
  protected readonly quickPhoneError = computed(() =>
    this.quickAttempted() && this.quickPhone().trim().length === 0 ? 'projects.quick.phone_required' : null,
  );
  protected readonly quickKindError = computed(() =>
    this.quickAttempted() && this.quickKind() === '' ? 'projects.quick.kind_required' : null,
  );
  protected readonly quickNeedsAck = computed(() => this.quickMatches().length > 0 && !this.quickAcknowledged());

  constructor() {
    void this.loadClients();
  }

  protected onText(target: EventTarget | null, into: { set(value: string): void }): void {
    into.set((target as HTMLInputElement).value);
    this.serverNameKey.set(null);
  }

  protected setClient(id: string): void {
    this.clientId.set(id);
    this.serverNameKey.set(null);
  }

  protected setType(value: string): void {
    this.contractType.set(value);
    this.serverTypeKey.set(null);
  }

  protected async onSubmit(): Promise<void> {
    if (this.busy()) {
      return;
    }

    this.attempted.set(true);
    this.refusal.set(null);

    if (this.name().trim().length === 0 || this.clientId() === '' || this.contractType() === '') {
      return;
    }

    const payload: ProjectCreate = {
      name: this.name().trim(),
      clientId: this.clientId(),
      contractType: this.contractType() as ContractType,
    };

    this.busy.set(true);
    try {
      const created = await this.api.create(payload);
      await this.refreshSession();
      await this.router.navigateByUrl(`/projects/${created.id}`);
    } catch (error) {
      const problem = toProblem(error);

      if (problem.code === 'project.name_taken_for_client') {
        this.serverNameKey.set(problem.messageKey);
      } else if (problem.code === 'project.contract_type_required') {
        this.serverTypeKey.set(problem.messageKey);
      } else {
        this.refusal.set(problem.messageKey);
      }
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * The session lists the projects a caller may open, and it was read before this one existed: the workspace
   * guard would refuse the new project's own URL until it is read again. Best effort.
   */
  private async refreshSession(): Promise<void> {
    try {
      this.auth.set(await this.authApi.me());
    } catch {
      // the workspace guard decides
    }
  }

  protected reloadClients(): void {
    this.clientsFailure.set(null);
    this.loadingClients.set(true);
    void this.loadClients();
  }

  private async loadClients(): Promise<void> {
    try {
      this.clients.set(await this.clientsApi.picker());
    } catch (error) {
      this.clientsFailure.set(toProblem(error).messageKey);
    } finally {
      this.loadingClients.set(false);
    }
  }

  // ---- quick add ----

  protected onTyped(text: string): void {
    this.typed = text;
  }

  protected openQuick(): void {
    this.quickName.set(this.typed.trim());
    this.quickPhone.set('');
    this.quickKind.set('');
    this.quickAttempted.set(false);
    this.quickRefusal.set(null);
    this.quickMatches.set([]);
    this.quickAcknowledged.set(false);
    this.quickOpen.set(true);
  }

  protected closeQuick(): void {
    this.quickOpen.set(false);
  }

  protected onQuickPhone(target: EventTarget | null): void {
    this.quickPhone.set((target as HTMLInputElement).value);
    this.quickMatches.set([]);
    this.quickAcknowledged.set(false);
  }

  /** The warning half (AC-4): runs when the phone field loses focus. A failed check never blocks the save. */
  protected async checkQuickPhone(): Promise<void> {
    const phone = this.quickPhone().trim();

    if (phone.length === 0) {
      return;
    }

    try {
      this.quickMatches.set(await this.clientsApi.quickPhoneCheck(phone));
    } catch {
      this.quickMatches.set([]);
    }
  }

  protected async saveQuick(): Promise<void> {
    if (this.quickBusy()) {
      return;
    }

    this.quickAttempted.set(true);
    this.quickRefusal.set(null);

    if (this.quickName().trim().length === 0 || this.quickPhone().trim().length === 0 || this.quickKind() === '') {
      return;
    }

    this.quickBusy.set(true);
    try {
      const saved = await this.clientsApi.quickAdd({
        name: this.quickName().trim(),
        phone: this.quickPhone().trim(),
        kind: this.quickKind() as ClientKind,
        acknowledgedDuplicatePhone: this.quickAcknowledged(),
      });

      this.clients.update((list) => [...list, { id: saved.id, name: saved.name }]);
      this.setClient(saved.id);
      this.quickOpen.set(false);
    } catch (error) {
      const problem = toProblem(error);

      if (problem.code === 'master.duplicate_phone_not_acknowledged') {
        // A client took this number between the blur check and the save: show who holds it now.
        await this.checkQuickPhone();
        this.quickAcknowledged.set(false);
      } else {
        this.quickRefusal.set(problem.messageKey);
      }
    } finally {
      this.quickBusy.set(false);
    }
  }
}
