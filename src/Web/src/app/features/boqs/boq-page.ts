import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormField, form } from '@angular/forms/signals';

import { toProblem } from '../../core/api/problem-details';
import { AuthService } from '../../core/auth/auth.service';
import { Boq, BoqLine, BoqsApi } from '../../core/boqs/boqs.api';
import { PERCENT_INPUT_PATTERN, fractionToPercent, percentToFraction } from '../../core/catalogue/percent-wire';
import { CatalogueApi, CatalogueItem } from '../../core/catalogue/catalogue.api';
import { I18nService } from '../../core/i18n/i18n.service';
import { ProjectDetail, ProjectsApi } from '../../core/projects/projects.api';
import { KaffBanner } from '../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffField } from '../../shared/kaff-field/kaff-field';
import { KaffFigure } from '../../shared/kaff-figure/kaff-figure';
import { KaffInlineConfirm } from '../../shared/kaff-inline-confirm/kaff-inline-confirm';
import { KaffPageHead } from '../../shared/kaff-page-head/kaff-page-head';
import { KaffRowMenu, RowMenuItem } from '../../shared/kaff-row-menu/kaff-row-menu';
import { KaffState } from '../../shared/kaff-state/kaff-state';
import { KaffStatusPill } from '../../shared/kaff-status-pill/kaff-status-pill';
import { KaffTable } from '../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../shared/kaff-table-row/kaff-table-row';

/** A non-negative decimal as typed. The server owns every other rule (a negative quantity is refused there). */
const QUANTITY_PATTERN = /^[0-9]+(\.[0-9]+)?$/;

/** How many catalogue rows the picker draws; the search narrows the rest. */
const PICKER_LIMIT = 30;

const SEARCH_DELAY_MS = 250;

const DEFAULT_HOLD_PERCENT = '20';
const DEFAULT_ADVANCE_PERCENT = '25';

interface TermsDraft {
  hold: string;
  advance: string;
}

interface QuantityDraft {
  surveyed: string;
  estimated: string;
}

/**
 * The project's BOQ: built by clicking, then signed. It shows cost-bearing data, so the server gates every call
 * `ProjectManage` and assignment; this page never decides who may read or write. `canManage` only decides what
 * to RENDER, from the session's per-project permission list.
 *
 * **Before the signature** (Lump Sum only) the page offers add a line from the catalogue, edit its quantities,
 * remove it, and sign. **After it** (`frozenOn` set) none of those controls exist in the DOM: the lines are the
 * frozen copy the contract was signed on, and the database refuses to alter one. A control the user may not use
 * is absent, not greyed out.
 *
 * **Signing is the one irreversible act.** It is a separate step behind an irreversible-tone confirmation that
 * states the exact total about to become the contract value, and that neither the value nor the lines can change
 * afterwards. The value is never typed anywhere; it is the BOQ total the server computes at signature.
 *
 * Only Lump Sum has a BOQ here (`projects/:id/boq` is linked from the workspace for no other type). Design has
 * none by spec.md 5.3; Cost Plus gets the same honest empty state, because its workspace group declares no BOQ.
 *
 * Custom lines (a line not from the catalogue) are not offered: they need a باب picker and a review queue this
 * screen does not build. The server supports them.
 */
@Component({
  selector: 'kaff-boq-page',
  imports: [
    FormField,
    KaffBanner,
    KaffButton,
    KaffField,
    KaffFigure,
    KaffInlineConfirm,
    KaffPageHead,
    KaffRowMenu,
    KaffState,
    KaffStatusPill,
    KaffTable,
    KaffTableRow,
  ],
  templateUrl: './boq-page.html',
  styleUrl: './boq-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BoqPage {
  private readonly api = inject(BoqsApi);
  private readonly projects = inject(ProjectsApi);
  private readonly catalogue = inject(CatalogueApi);
  private readonly auth = inject(AuthService);

  protected readonly i18n = inject(I18nService);

  /** Bound from `:projectId` by `withComponentInputBinding`. */
  readonly projectId = input.required<string>();

  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly boq = signal<Boq | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly note = signal<string | null>(null);
  /** KAFF-415: signing raised the design credit. Labelled by its type; the adjustment's stored reason is never shown. */
  protected readonly creditRaised = signal(false);

  // ---- who may do what (convenience; the server decides) ------------------------------------------
  protected readonly canManage = computed(
    () =>
      this.auth.current()?.projects.find((entry) => entry.projectId === this.projectId())?.permissions.includes('ProjectManage') ??
      false,
  );
  protected readonly hasBoq = computed(() => this.project()?.contractType === 'LumpSum');
  protected readonly frozen = computed(() => this.boq()?.frozenOn ?? null);
  /** Build and sign controls exist only on a Lump Sum BOQ that is not yet frozen, for someone who may manage it. */
  protected readonly editable = computed(() => this.canManage() && this.hasBoq() && this.boq() !== null && this.frozen() === null);

  /** Item first (the pinned identity), then three end-aligned figure columns; the currency is named in each money header. */
  protected readonly columns = computed(() => 'minmax(14rem, 1fr) 8rem 9rem 10rem' + (this.editable() ? ' 3rem' : ''));
  protected readonly headerColumns = computed<readonly TableColumnDef[]>(() => [
    { labelKey: 'boq.column.item', pinned: true },
    { labelKey: 'boq.column.quantity', align: 'end' },
    { labelKey: 'boq.column.sell_rate', align: 'end' },
    { labelKey: 'boq.column.total', align: 'end' },
    ...(this.editable() ? [{}] : []),
  ]);

  protected readonly rowMenu: readonly RowMenuItem[] = [
    { labelKey: 'boq.line.edit', action: 'edit', icon: 'pen' },
    { labelKey: 'boq.line.remove', action: 'remove', icon: 'x', tone: 'danger' },
  ];

  // ---- add a line ---------------------------------------------------------------------------------
  protected readonly adding = signal(false);
  protected readonly search = signal('');
  protected readonly results = signal<readonly CatalogueItem[]>([]);
  protected readonly pickerLoading = signal(false);
  protected readonly pickerFailure = signal<string | null>(null);
  protected readonly picked = signal<CatalogueItem | null>(null);
  protected readonly addAttempted = signal(false);
  private readonly addModel = signal<QuantityDraft>({ surveyed: '', estimated: '' });
  protected readonly addForm = form(this.addModel);
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  protected readonly shownResults = computed(() => this.results().slice(0, PICKER_LIMIT));
  protected readonly moreResults = computed(() => this.results().length > PICKER_LIMIT);
  protected readonly addSurveyedError = computed(() => (this.addAttempted() ? this.quantityError(this.addModel().surveyed, true) : null));
  protected readonly addEstimatedError = computed(() => (this.addAttempted() ? this.quantityError(this.addModel().estimated, false) : null));

  // ---- edit and remove a line ---------------------------------------------------------------------
  protected readonly editingId = signal<string | null>(null);
  protected readonly editAttempted = signal(false);
  private readonly editModel = signal<QuantityDraft>({ surveyed: '', estimated: '' });
  protected readonly editForm = form(this.editModel);
  protected readonly removingId = signal<string | null>(null);
  protected readonly editSurveyedError = computed(() => (this.editAttempted() ? this.quantityError(this.editModel().surveyed, true) : null));
  protected readonly editEstimatedError = computed(() => (this.editAttempted() ? this.quantityError(this.editModel().estimated, false) : null));

  // ---- sign ---------------------------------------------------------------------------------------
  protected readonly confirmingSign = signal(false);
  /** An archived project cannot be signed until it is reopened (AC-417-J); the server refuses too. */
  protected readonly archived = computed(() => this.project()?.isArchived === true);
  protected readonly canSign = computed(
    () => this.editable() && !this.archived() && (this.boq()?.lines.length ?? 0) > 0 && this.boq()?.total != null,
  );
  /** Hold and advance are asked here, at signature, for Lump Sum only (AC-417-D). Percent units as typed. */
  private readonly termsModel = signal<TermsDraft>({ hold: DEFAULT_HOLD_PERCENT, advance: DEFAULT_ADVANCE_PERCENT });
  protected readonly termsForm = form(this.termsModel);
  protected readonly holdError = computed(() => this.rateError(this.termsModel().hold));
  protected readonly advanceError = computed(() => this.rateError(this.termsModel().advance));

  /** The unsigned BOQ as the client's quotation (D-208): lines grouped by باب, printed with the browser's print. */
  protected readonly printGroups = computed(() => {
    const groups: { code: string; name: string; lines: readonly BoqLine[] }[] = [];
    for (const line of this.boq()?.lines ?? []) {
      const name = this.i18n.locale() === 'ar' ? line.babNameAr : line.babNameEn;
      const last = groups[groups.length - 1];
      if (last !== undefined && last.code === line.babCode) {
        (last.lines as BoqLine[]).push(line);
      } else {
        groups.push({ code: line.babCode, name, lines: [line] });
      }
    }
    return groups;
  });
  protected readonly canPrint = computed(() => this.canManage() && this.hasBoq() && (this.boq()?.lines.length ?? 0) > 0);
  protected readonly today = computed(() => this.i18n.formatDate(new Date(), { dateStyle: 'long' }));
  protected readonly designArea = computed(() => this.project()?.designAreaSquareMetres ?? null);

  protected readonly signDetail = computed(() => {
    const total = this.boq()?.total;
    return total == null ? null : this.i18n.t('boq.sign.confirm_detail', { total: this.i18n.formatAmount(total) });
  });

  constructor() {
    // A required input is set after construction: read it in an effect, reload untracked (see ExtractListPage).
    effect(() => {
      this.projectId();
      untracked(() => void this.reload());
    });
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      const project = await this.projects.get(this.projectId());
      this.project.set(project);
      this.termsModel.set({
        hold: project.holdRate != null ? fractionToPercent(project.holdRate) : DEFAULT_HOLD_PERCENT,
        advance: project.advanceRate != null ? fractionToPercent(project.advanceRate) : DEFAULT_ADVANCE_PERCENT,
      });
      await this.loadBoq();
    } catch (error) {
      this.boq.set(null);
      this.failure.set(toProblem(error).messageKey);
    } finally {
      this.loading.set(false);
    }
  }

  /** A Lump Sum project with no line yet has no BOQ row: that is an empty, buildable BOQ, not a failure. */
  private async loadBoq(): Promise<void> {
    if (!this.hasBoq()) {
      this.boq.set(null);
      return;
    }

    try {
      this.boq.set(await this.api.get(this.projectId()));
    } catch (error) {
      const problem = toProblem(error);
      if (problem.code !== 'boq.not_found') {
        throw error;
      }
      this.boq.set({ id: '', projectId: this.projectId(), frozenOn: null, total: null, lines: [] });
    }
  }

  protected day(value: string): string {
    const [year = 0, month = 1, day = 1] = value.split('-').map(Number);
    return this.i18n.formatDate(new Date(year, month - 1, day), { dateStyle: 'medium' });
  }

  protected quantity(value: string | null): string {
    return value === null ? '-' : this.i18n.formatNumber(value);
  }

  // ---- add ----------------------------------------------------------------------------------------
  protected openAdd(): void {
    this.closeEdit();
    this.removingId.set(null);
    this.note.set(null);
    this.actionError.set(null);
    this.adding.set(true);
    this.picked.set(null);
    this.addAttempted.set(false);
    this.addModel.set({ surveyed: '', estimated: '' });
    this.search.set('');
    void this.runSearch();
  }

  protected closeAdd(): void {
    this.adding.set(false);
    this.picked.set(null);
  }

  protected onSearchInput(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
    if (this.searchTimer !== null) {
      clearTimeout(this.searchTimer);
    }
    this.searchTimer = setTimeout(() => void this.runSearch(), SEARCH_DELAY_MS);
  }

  private async runSearch(): Promise<void> {
    this.pickerLoading.set(true);
    this.pickerFailure.set(null);
    try {
      this.results.set(await this.catalogue.list(this.search(), 'active'));
    } catch (error) {
      this.results.set([]);
      this.pickerFailure.set(toProblem(error).messageKey);
    } finally {
      this.pickerLoading.set(false);
    }
  }

  protected pick(item: CatalogueItem): void {
    this.picked.set(item);
    this.addAttempted.set(false);
  }

  protected unpick(): void {
    this.picked.set(null);
  }

  protected async submitAdd(): Promise<void> {
    const item = this.picked();
    const draft = this.addModel();
    this.addAttempted.set(true);

    if (item === null || this.quantityError(draft.surveyed, true) !== null || this.quantityError(draft.estimated, false) !== null) {
      return;
    }

    await this.mutate(async () => {
      await this.api.addLine(this.projectId(), {
        catalogueItemId: item.id,
        quantityEstimated: draft.estimated.trim() === '' ? null : draft.estimated.trim(),
        quantitySurveyed: draft.surveyed.trim(),
      });
      this.closeAdd();
      this.note.set('boq.note.line_added');
    });
  }

  // ---- edit and remove ----------------------------------------------------------------------------
  protected onRowAction(line: BoqLine, action: string): void {
    this.note.set(null);
    this.actionError.set(null);
    this.confirmingSign.set(false);
    this.closeAdd();

    if (action === 'edit') {
      this.removingId.set(null);
      this.editAttempted.set(false);
      this.editModel.set({ surveyed: plain(line.quantitySurveyed), estimated: plain(line.quantityEstimated) });
      this.editingId.set(line.id);
    } else if (action === 'remove') {
      this.closeEdit();
      this.removingId.set(line.id);
    }
  }

  protected closeEdit(): void {
    this.editingId.set(null);
  }

  protected async submitEdit(line: BoqLine): Promise<void> {
    const draft = this.editModel();
    this.editAttempted.set(true);

    if (this.quantityError(draft.surveyed, true) !== null || this.quantityError(draft.estimated, false) !== null) {
      return;
    }

    await this.mutate(async () => {
      await this.api.editLine(this.projectId(), line.id, {
        quantityEstimated: draft.estimated.trim() === '' ? null : draft.estimated.trim(),
        quantitySurveyed: draft.surveyed.trim(),
        conditionsRate: line.conditionsRate,
        lineMarkup: line.lineMarkup,
      });
      this.closeEdit();
      this.note.set('boq.note.line_saved');
    });
  }

  protected async confirmRemove(line: BoqLine): Promise<void> {
    await this.mutate(async () => {
      await this.api.removeLine(this.projectId(), line.id);
      this.removingId.set(null);
      this.note.set('boq.note.line_removed');
    });
  }

  // ---- sign ---------------------------------------------------------------------------------------
  protected askSign(): void {
    if (this.holdError() !== null || this.advanceError() !== null) {
      return;
    }
    this.closeAdd();
    this.closeEdit();
    this.removingId.set(null);
    this.note.set(null);
    this.actionError.set(null);
    this.confirmingSign.set(true);
  }

  protected async confirmSign(): Promise<void> {
    if (this.holdError() !== null || this.advanceError() !== null) {
      this.confirmingSign.set(false);
      return;
    }
    const terms = this.termsModel();
    await this.mutate(async () => {
      const signed = await this.api.sign(this.projectId(), {
        holdPercentage: percentToFraction(terms.hold),
        clientAdvance: Number(terms.advance.trim()),
      });
      this.confirmingSign.set(false);
      this.note.set('boq.note.signed');
      this.creditRaised.set(!!signed.designCreditAdjustmentId);
      this.project.set(await this.projects.get(this.projectId()));
    });
  }

  /** One path for every write: busy, run, refresh the BOQ from the server, or show the server's own refusal key. */
  private async mutate(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.actionError.set(null);
    try {
      await action();
      await this.loadBoq();
    } catch (error) {
      this.actionError.set(toProblem(error).messageKey);
      this.note.set(null);
      this.confirmingSign.set(false);
    } finally {
      this.busy.set(false);
    }
  }

  private rateError(raw: string): string | null {
    return PERCENT_INPUT_PATTERN.test(raw.trim()) ? null : 'boq.sign.error.rate_format';
  }

  protected print(): void {
    window.print();
  }

  private quantityError(raw: string, requiredValue: boolean): string | null {
    const text = raw.trim();
    if (text === '') {
      return requiredValue ? 'boq.error.quantity_required' : null;
    }
    return QUANTITY_PATTERN.test(text) ? null : 'boq.error.quantity_format';
  }
}

/** `"2.0000"` -> `"2"`; the wire string with its padding zeros removed, for an input to show. */
function plain(value: string | null): string {
  return value === null ? '' : value.includes('.') ? value.replace(/0+$/, '').replace(/\.$/, '') : value;
}
