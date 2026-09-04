import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormField, form, required, schema, submit } from '@angular/forms/signals';
import { Router } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import {
  ClientFile,
  ClientKind,
  ClientWrite,
  ClientsApi,
  PhoneMatch,
} from '../../../core/clients/clients.api';
import { I18nService } from '../../../core/i18n/i18n.service';

interface ClientDraft {
  phone: string;
  name: string;
  kind: ClientKind;
  alternatePhone: string;
  email: string;
  address: string;
  notes: string;
  taxRegistrationNumber: string;
}

/**
 * The form's rules, and all of them.
 *
 * **Two `required`s and nothing else.** Every other rule belongs to the server: the phone's shape is
 * `PhoneNumber.Create`'s, the name's length is `Client.Create`'s, and "an individual does not
 * withhold" is `Client.SetClassification`'s (decisions.md D-109 §1). A second copy here is a copy
 * that disagrees with the entity eventually, and the entity is what every other caller goes through.
 * **The duplicate check is deliberately not a validator** — it is a warning, and a validator that
 * blocks submission would re-impose the refusal Karim reversed on 2026-08-21.
 */
const draft = schema<ClientDraft>((path) => {
  required(path.phone);
  required(path.name);
});

/** Blank means absent. The server trims and nulls too; this keeps the payload honest on the way out. */
function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

/**
 * S-012 · create, S-013 · the duplicate warning, and S-014 · edit. One component.
 *
 * **One form serves create and edit** because the API takes the same members either way and the two
 * screens differ by which call they end in. Building them twice is how the create form grows a field
 * the edit form does not have.
 *
 * **The phone is the first field, and the hint says why.** `S-012`: it is how clients are matched, so
 * it is entered first and checked on blur before the operator has typed a name they may not need.
 *
 * **The warning asks; it never blocks.** spec.md §2, amended, and D-049 ruling 8: a repeated number
 * shows which client already holds it and asks whether to proceed. Karim's reason is that a corporate
 * client and its CEO legitimately share a number. So the save button stays live with a match on
 * screen, and proceeding sends `acknowledgedDuplicatePhone`.
 *
 * **A 409 is a question, not a failure.** If a client appears on the number between the blur check
 * and the save, the server answers `409` — this re-runs the check and shows the warning again rather
 * than rendering S-016's "Failed" mode. Treating it as an error would tell the operator something
 * broke when what actually happened is that the system asked them something.
 *
 * **Kind and tax registration move together.** §6.7 constrains the pair, so switching to Individual
 * clears the number here rather than submitting a combination the server is going to refuse
 * (decisions.md D-109 §1). If the server refuses anyway, the key it sends is shown.
 */
@Component({
  selector: 'kaff-client-form-page',
  imports: [FormField],
  templateUrl: './client-form-page.html',
  styleUrl: './client-form-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClientFormPage {
  /** Absent when creating. The route supplies it when editing — `/clients/:clientId`. */
  readonly clientId = input<string | undefined>(undefined);

  private readonly api = inject(ClientsApi);
  private readonly router = inject(Router);

  private readonly model = signal<ClientDraft>({
    phone: '',
    name: '',
    kind: 'Corporate',
    alternatePhone: '',
    email: '',
    address: '',
    notes: '',
    taxRegistrationNumber: '',
  });

  private readonly matches = signal<readonly PhoneMatch[]>([]);
  private readonly acknowledged = signal(false);
  private readonly refusal = signal<string | null>(null);
  private readonly loaded = signal<ClientFile | null>(null);
  private readonly loading = signal(false);

  protected readonly i18n = inject(I18nService);
  protected readonly clientForm = form(this.model, draft);

  protected readonly duplicateMatches = this.matches.asReadonly();
  protected readonly refusalKey = this.refusal.asReadonly();
  protected readonly existing = this.loaded.asReadonly();
  protected readonly isLoading = this.loading.asReadonly();
  protected readonly archived = signal(false);
  protected readonly confirmingArchive = signal(false);

  protected readonly isEdit = computed(() => this.clientId() !== undefined);

  constructor() {
    // The route's id is an input signal, so the load reacts to it rather than running once in a
    // constructor that would miss a navigation from one client's file straight to another's.
    effect(() => {
      const id = this.clientId();

      if (id !== undefined) {
        void this.load(id);
      }
    });
  }

  /**
   * Loads the file S-014 edits.
   *
   * The list row carries six fields and this form has nine, so the row cannot populate it — and
   * S-014 is reachable by URL, so router state cannot either. `GET /api/clients/{id}` exists for
   * exactly this (decisions.md D-113 §1).
   */
  private async load(id: string): Promise<void> {
    this.loading.set(true);
    this.refusal.set(null);

    try {
      const file = await this.api.get(id);

      this.loaded.set(file);
      this.archived.set(!file.isActive);
      this.model.set({
        phone: file.phone,
        name: file.name,
        kind: file.kind,
        alternatePhone: file.alternatePhone ?? '',
        email: file.email ?? '',
        address: file.address ?? '',
        notes: file.notes ?? '',
        taxRegistrationNumber: file.taxRegistrationNumber ?? '',
      });
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  protected readonly titleKey = computed(() =>
    this.isEdit() ? 'clients.edit.title' : 'clients.create.title',
  );

  protected readonly isIndividual = computed(() => this.model().kind === 'Individual');

  protected readonly canSubmit = computed(
    () => this.clientForm().valid() && !this.clientForm().submitting(),
  );

  /**
   * Refreshes the warning when the operator leaves the phone field. `S-013` — on blur, not on every
   * keystroke: a check per character asks the server about numbers nobody has finished typing.
   */
  protected async onPhoneBlur(): Promise<void> {
    await this.refreshMatches();
  }

  /**
   * §6.7's pair, kept legal on the way in rather than on the way back.
   *
   * Switching to Individual clears the tax registration number here, so the request describes a legal
   * end state. The server still refuses the illegal combination — that guard is the entity's and this
   * does not replace it — but an operator who has to read a refusal to learn that a field had to be
   * emptied has been made to do the system's work.
   */
  protected onKindChange(kind: ClientKind): void {
    this.model.update((current) => ({
      ...current,
      kind,
      taxRegistrationNumber: kind === 'Individual' ? '' : current.taxRegistrationNumber,
    }));
  }

  protected async onSubmit(): Promise<void> {
    this.refusal.set(null);

    await submit(this.clientForm, async () => {
      const payload = this.payload();

      try {
        const saved = this.isEdit()
          ? await this.api.edit(this.clientId()!, payload)
          : await this.api.create(payload);

        await this.router.navigateByUrl(`/clients/${saved.id}`);
      } catch (error) {
        const problem = toProblem(error);

        if (problem.code === 'master.duplicate_phone_not_acknowledged') {
          // Not a failure — the server is asking. A client appeared on this number between the blur
          // check and the save, so re-run the check and show the current matches rather than the
          // stale ones the operator already dismissed.
          await this.refreshMatches();
          this.acknowledged.set(false);
          return undefined;
        }

        this.refusal.set(problem.messageKey);
      }

      return undefined;
    });
  }

  /**
   * The operator saying "I saw who holds this number and I am proceeding anyway."
   *
   * The event is read here rather than in the template, because `$any` in a template is exactly what
   * leaves `strictTemplates` nothing to check (Nabil, 2026-08-28).
   */
  protected onAcknowledgeChange(event: Event): void {
    const target = event.target;
    this.acknowledged.set(target instanceof HTMLInputElement && target.checked);
  }

  protected onStartArchive(): void {
    this.confirmingArchive.set(true);
  }

  protected onCancelArchive(): void {
    this.confirmingArchive.set(false);
  }

  protected readonly hasUnacknowledgedMatch = computed(
    () => this.matches().length > 0 && !this.acknowledged(),
  );

  protected async onArchive(): Promise<void> {
    const id = this.clientId();

    if (id === undefined) {
      return;
    }

    this.refusal.set(null);

    try {
      await this.api.archive(id);
      this.archived.set(true);
      this.confirmingArchive.set(false);
      await this.router.navigateByUrl('/clients');
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
      this.confirmingArchive.set(false);
    }
  }

  protected trackMatch(_index: number, match: PhoneMatch): string {
    return match.id;
  }

  private async refreshMatches(): Promise<void> {
    const phone = this.model().phone.trim();

    if (phone.length === 0) {
      this.matches.set([]);
      return;
    }

    try {
      const found = await this.api.phoneCheck(phone);

      // Editing a client whose phone has not changed must not warn about itself. The server already
      // excludes it on save (decisions.md D-107 §2); phone-check has no client to exclude, so the one
      // row that could be this client is dropped here — for the warning only.
      this.matches.set(found.filter((match) => match.id !== this.clientId()));
    } catch {
      // A failed check must not stop a save. The server re-runs the match anyway and will ask again
      // with a 409 if there is one, so the warning is a convenience and its absence is not a licence.
      this.matches.set([]);
    }
  }

  private payload(): ClientWrite {
    const value = this.model();

    return {
      name: value.name.trim(),
      phone: value.phone.trim(),
      kind: value.kind,
      alternatePhone: orNull(value.alternatePhone),
      email: orNull(value.email),
      address: orNull(value.address),
      notes: orNull(value.notes),
      taxRegistrationNumber: value.kind === 'Individual' ? null : orNull(value.taxRegistrationNumber),
      acknowledgedDuplicatePhone: this.acknowledged(),
    };
  }
}
