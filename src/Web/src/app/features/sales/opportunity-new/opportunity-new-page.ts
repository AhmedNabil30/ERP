import { ChangeDetectionStrategy, Component, DestroyRef, TemplateRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { ClientSummary, ClientsApi, PhoneMatch } from '../../../core/clients/clients.api';
import { I18nService } from '../../../core/i18n/i18n.service';
import { HeaderActionsService } from '../../../core/layout/header-actions.service';
import { OpportunitiesApi } from '../../../core/sales/opportunities.api';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffField } from '../../../shared/kaff-field/kaff-field';
import { KaffPageHead } from '../../../shared/kaff-page-head/kaff-page-head';
import { KaffSearchField } from '../../../shared/kaff-search-field/kaff-search-field';

/**
 * R3-10 · S2, a new opportunity. A client is found by name or phone; if nobody matches, the typed name can be
 * added as a new client right here. A phone that already belongs to a client offers the two ways forward (use
 * that client, or add a new one with the same phone) and never blocks. Then a title, and «إنشاء».
 */
@Component({
  selector: 'kaff-opportunity-new-page',
  imports: [KaffBanner, KaffButton, KaffField, KaffPageHead, KaffSearchField],
  templateUrl: './opportunity-new-page.html',
  styleUrl: './opportunity-new-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OpportunityNewPage {
  private readonly clients = inject(ClientsApi);
  private readonly opportunities = inject(OpportunitiesApi);
  private readonly router = inject(Router);
  private readonly headerActions = inject(HeaderActionsService);
  protected readonly i18n = inject(I18nService);

  protected readonly query = signal('');
  protected readonly results = signal<readonly ClientSummary[]>([]);
  /** True once a search has answered, so "nobody matches" is never shown before the first answer. */
  protected readonly searched = signal(false);
  protected readonly searching = signal(false);

  protected readonly picked = signal<{ id: string; name: string; phone: string } | null>(null);
  protected readonly adding = signal(false);
  protected readonly newName = signal('');
  protected readonly newPhone = signal('');
  protected readonly matches = signal<readonly PhoneMatch[]>([]);
  protected readonly acknowledged = signal(false);

  protected readonly title = signal('');
  protected readonly attempted = signal(false);
  protected readonly busy = signal(false);
  protected readonly refusal = signal<string | null>(null);

  protected readonly clientReady = computed(() => {
    if (this.picked() !== null) return true;
    return (
      this.adding() &&
      this.newName().trim() !== '' &&
      this.newPhone().trim() !== '' &&
      (this.matches().length === 0 || this.acknowledged())
    );
  });
  protected readonly titleError = computed(() =>
    this.attempted() && this.title().trim() === '' ? 'sales.new.title_required' : null,
  );
  protected readonly showDuplicate = computed(() => this.adding() && this.matches().length > 0 && !this.acknowledged());

  private readonly createAction = viewChild<TemplateRef<unknown>>('createAction');

  constructor() {
    effect(() => this.headerActions.set(this.createAction() ?? null, 'primary'));
    inject(DestroyRef).onDestroy(() => this.headerActions.clear());
  }

  protected async onSearch(term: string): Promise<void> {
    this.query.set(term);
    this.adding.set(false);
    if (term.trim() === '') {
      this.results.set([]);
      this.searched.set(false);
      return;
    }
    this.searching.set(true);
    try {
      this.results.set(await this.clients.list(term, 'active'));
      this.searched.set(true);
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
    } finally {
      this.searching.set(false);
    }
  }

  protected pick(client: { id: string; name: string; phone: string }): void {
    this.picked.set({ id: client.id, name: client.name, phone: client.phone });
    this.adding.set(false);
    this.refusal.set(null);
  }

  protected changeClient(): void {
    this.picked.set(null);
  }

  protected startAdding(): void {
    const typed = this.query().trim();
    const isPhone = /^[\d\s+()-]+$/.test(typed);
    this.newName.set(isPhone ? '' : typed);
    this.newPhone.set(isPhone ? typed : '');
    this.matches.set([]);
    this.acknowledged.set(false);
    this.adding.set(true);
  }

  protected onText(target: EventTarget | null, into: { set(value: string): void }): void {
    into.set((target as HTMLInputElement).value);
  }

  protected async checkPhone(): Promise<void> {
    this.acknowledged.set(false);
    const phone = this.newPhone().trim();
    try {
      this.matches.set(phone === '' ? [] : await this.clients.phoneCheck(phone));
    } catch {
      this.matches.set([]);
    }
  }

  protected useMatch(match: PhoneMatch): void {
    this.pick({ id: match.id, name: match.name, phone: this.newPhone().trim() });
  }

  protected async submit(): Promise<void> {
    this.attempted.set(true);
    this.refusal.set(null);
    if (!this.clientReady() || this.title().trim() === '' || this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      let clientId = this.picked()?.id;
      if (clientId === undefined) {
        const created = await this.clients.create({
          name: this.newName().trim(),
          phone: this.newPhone().trim(),
          kind: 'Individual',
          alternatePhone: null,
          email: null,
          address: null,
          notes: null,
          taxRegistrationNumber: null,
          acknowledgedDuplicatePhone: this.acknowledged(),
        });
        clientId = created.id;
        this.picked.set({ id: created.id, name: created.name, phone: created.phone });
        this.adding.set(false);
      }
      const opportunity = await this.opportunities.create(clientId, this.title().trim());
      await this.router.navigate(['/sales', opportunity.id]);
    } catch (error) {
      this.refusal.set(toProblem(error).messageKey);
    } finally {
      this.busy.set(false);
    }
  }
}
