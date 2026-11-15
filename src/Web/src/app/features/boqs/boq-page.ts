import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, input, signal, viewChild, WritableSignal } from '@angular/core';

import { toProblem } from '../../core/api/problem-details';
import { AuthService } from '../../core/auth/auth.service';
import { Bab, BabsApi } from '../../core/catalogue/babs.api';
import { BoqBaseSummary, BoqLine, Boq, BoqsApi } from '../../core/boqs/boqs.api';
import { CatalogueApi, CatalogueItem } from '../../core/catalogue/catalogue.api';
import { PERCENT_INPUT_PATTERN, percentToFraction } from '../../core/catalogue/percent-wire';
import { I18nService } from '../../core/i18n/i18n.service';
import { ProjectDetail, ProjectSummary, ProjectsApi } from '../../core/projects/projects.api';
import { KaffBadge } from '../../shared/kaff-badge/kaff-badge';
import { KaffBanner } from '../../shared/kaff-banner/kaff-banner';
import { BoqQuantityState, KaffBoqLine } from '../../shared/kaff-boq-line/kaff-boq-line';
import { KaffButton } from '../../shared/kaff-button/kaff-button';
import { KaffDetailPanel } from '../../shared/kaff-detail-panel/kaff-detail-panel';
import { KaffField } from '../../shared/kaff-field/kaff-field';
import { KaffFigure } from '../../shared/kaff-figure/kaff-figure';
import { KaffIcon } from '../../shared/kaff-icon/kaff-icon';
import { KaffMoneyField } from '../../shared/kaff-money-field/kaff-money-field';
import { KaffProjectFrame } from '../../shared/kaff-project-frame/kaff-project-frame';
import { KaffSearchField } from '../../shared/kaff-search-field/kaff-search-field';
import { KaffSelect } from '../../shared/kaff-select/kaff-select';
import { KaffState } from '../../shared/kaff-state/kaff-state';
import { BoqCataloguePick } from './boq-catalogue-pick';

interface LineGroup {
  readonly code: string;
  readonly name: string;
  readonly lines: readonly BoqLine[];
  /** Null while any line of the باب has no quantity. */
  readonly subtotal: string | null;
}

/** A refusal to show: the i18n key and the values its text takes. */
interface Refusal {
  readonly key: string;
  readonly params?: Readonly<Record<string, string>>;
}

/** `"2.0000"` -> `"2"`: the wire string without padding zeros, for a box to show. */
export function plainQuantity(value: string | null): string {
  return value === null ? '' : value.includes('.') ? value.replace(/0+$/, '').replace(/\.$/, '') : value;
}

/**
 * Adds money strings without a float (D-135): each is read at four decimals as an integer. Null when any
 * value is null, so a subtotal never reads as a figure while a line has no quantity.
 */
export function sumAmounts(values: readonly (string | null)[]): string | null {
  let total = 0n;
  for (const value of values) {
    if (value === null) {
      return null;
    }
    const [whole = '0', fraction = ''] = value.split('.');
    total += BigInt(whole) * 10000n + BigInt(fraction.padEnd(4, '0').slice(0, 4)) * (whole.startsWith('-') ? -1n : 1n);
  }
  const digits = (total < 0n ? -total : total).toString().padStart(5, '0');
  return `${total < 0n ? '-' : ''}${digits.slice(0, -4)}.${digits.slice(-4)}`;
}

/**
 * The BOQ builder (`.design/v2/team-a/boq.html`, with the D-218 changes). The project frame sits above it.
 *
 * An empty BOQ shows one button, which opens one sheet: a blank BOQ, a prepared base, an earlier project.
 * Lines show the rate and one quantity box; the catalogue pane, "+", ticks, a drag and the typed custom item
 * add lines; a line is removed without asking. Signing is a sheet that asks for the hold and the advance
 * (both empty and required) and freezes the BOQ. A signed or archived BOQ draws no control.
 *
 * `canManage` and `editable` only decide what to RENDER; every call is gated on the server by role and
 * assignment. Only Lump Sum has a BOQ here; Design and Cost Plus get an honest empty state.
 */
/** Only Lump Sum has a BOQ here; the page's not-for-this-type state and the earlier-projects list both read this. */
function hasBoqType(type: string | undefined): boolean {
  return type === 'LumpSum';
}

@Component({
  selector: 'kaff-boq-page',
  imports: [
    BoqCataloguePick,
    KaffBadge,
    KaffBanner,
    KaffBoqLine,
    KaffButton,
    KaffDetailPanel,
    KaffField,
    KaffFigure,
    KaffIcon,
    KaffMoneyField,
    KaffProjectFrame,
    KaffSearchField,
    KaffSelect,
    KaffState,
  ],
  templateUrl: './boq-page.html',
  styleUrl: './boq-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BoqPage {
  private readonly api = inject(BoqsApi);
  private readonly projects = inject(ProjectsApi);
  private readonly catalogue = inject(CatalogueApi);
  private readonly babsApi = inject(BabsApi);
  private readonly auth = inject(AuthService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly i18n = inject(I18nService);

  /** Bound from `:projectId` by `withComponentInputBinding`. The frame reads the project and hands it back. */
  readonly projectId = input.required<string>();

  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly boq = signal<Boq | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly refusal = signal<Refusal | null>(null);
  /** Items the copy left out, by description. */
  protected readonly skipped = signal<readonly string[]>([]);
  protected readonly skippedNames = computed(() => new Intl.ListFormat(this.i18n.locale(), { type: 'conjunction' }).format(this.skipped()));
  /** "مقايسة فارغة" was chosen: the list is a drop zone beside the catalogue. */
  protected readonly started = signal(false);

  // ---- who may do what (convenience; the server decides) ------------------------------------------
  protected readonly canManage = computed(
    () =>
      this.auth.current()?.projects.find((entry) => entry.projectId === this.projectId())?.permissions.includes('ProjectManage') ??
      false,
  );
  private readonly frame = viewChild(KaffProjectFrame);
  protected readonly hasBoq = computed(() => hasBoqType(this.project()?.contractType));
  protected readonly frozen = computed(() => this.boq()?.frozenOn ?? null);
  protected readonly archived = computed(() => this.project()?.isArchived === true);
  /** Build and sign controls exist only on an unsigned, unarchived Lump Sum BOQ for someone who may manage it. */
  protected readonly editable = computed(
    () => this.canManage() && this.hasBoq() && this.boq() !== null && this.frozen() === null && !this.archived(),
  );

  protected readonly lines = computed(() => this.boq()?.lines ?? []);
  protected readonly missing = computed(() => this.lines().filter((line) => this.quantityOf(line) === null).length);
  protected readonly canSign = computed(() => this.editable() && this.lines().length > 0 && this.missing() === 0);
  protected readonly canPrint = computed(() => this.canManage() && this.hasBoq() && this.lines().length > 0);
  protected readonly showPane = computed(() => this.editable() && (this.lines().length > 0 || this.started()));
  protected readonly designArea = computed(() => this.project()?.designAreaSquareMetres ?? null);

  protected readonly groups = computed<readonly LineGroup[]>(() => {
    const byBab = new Map<string, { name: string; lines: BoqLine[] }>();
    for (const line of this.lines()) {
      let group = byBab.get(line.babCode);
      if (group === undefined) {
        group = { name: this.i18n.locale() === 'ar' ? line.babNameAr : line.babNameEn, lines: [] };
        byBab.set(line.babCode, group);
      }
      group.lines.push(line);
    }
    return [...byBab.entries()].map(([code, group]) => ({
      code,
      name: group.name,
      lines: group.lines,
      subtotal: sumAmounts(group.lines.map((line) => line.lineTotal)),
    }));
  });

  // ---- the catalogue pane ---------------------------------------------------------------------------
  protected readonly items = signal<readonly CatalogueItem[]>([]);
  protected readonly babs = signal<readonly Bab[]>([]);
  protected readonly pickerFailure = signal<string | null>(null);
  /** A catalogue item is "added" when a line carries its code. The code is only matched, never shown. */
  protected readonly addedIds = computed(() => {
    const codes = new Set(this.lines().map((line) => line.itemCode));
    return new Set(this.items().filter((item) => codes.has(item.code)).map((item) => item.id));
  });
  protected readonly addSheet = signal(false);
  protected readonly dropping = signal(false);

  // ---- the start sheet ------------------------------------------------------------------------------
  protected readonly startSheet = signal(false);
  protected readonly startSearch = signal('');
  protected readonly startLoading = signal(false);
  protected readonly startFailure = signal<string | null>(null);
  private readonly bases = signal<readonly BoqBaseSummary[]>([]);
  private readonly earlier = signal<readonly ProjectSummary[]>([]);
  protected readonly shownBases = computed(() => this.bases().filter((base) => this.matchesStartSearch(base.name)));
  protected readonly shownProjects = computed(() => this.earlier().filter((project) => this.matchesStartSearch(project.name)));

  // ---- the custom item ------------------------------------------------------------------------------
  protected readonly customSheet = signal(false);
  protected readonly customName = signal('');
  protected readonly customUnit = signal('');
  protected readonly customRate = signal('');
  protected readonly customBab = signal('');
  protected readonly customAttempted = signal(false);
  protected readonly babOptions = computed(() => [
    { value: '', label: '-' },
    ...this.babs().map((bab) => ({ value: bab.id, label: this.i18n.locale() === 'ar' ? bab.nameAr : bab.nameEn })),
  ]);
  protected readonly customErrors = computed(() => {
    const attempted = this.customAttempted();
    const rate = this.customRate().trim();
    return {
      name: attempted && this.customName().trim() === '' ? 'boq.error.required' : null,
      unit: attempted && this.customUnit().trim() === '' ? 'boq.error.required' : null,
      rate: attempted && !PERCENT_INPUT_PATTERN.test(rate) ? 'boq.error.required' : null,
      bab: attempted && this.customBab() === '' ? 'boq.error.required' : null,
    };
  });

  // ---- the sign sheet -------------------------------------------------------------------------------
  protected readonly signSheet = signal(false);
  /** Percent units as typed. Both start empty and are required (AC-9). */
  protected readonly hold = signal('');
  protected readonly advance = signal('');
  protected readonly signAttempted = signal(false);
  protected readonly signErrors = computed(() => ({
    hold: this.signAttempted() ? this.rateError(this.hold()) : null,
    advance: this.signAttempted() ? this.rateError(this.advance()) : null,
  }));

  /** The quotation as the client's paper (D-208): printed with the browser's print. */
  protected readonly today = computed(() => this.i18n.formatDate(new Date(), { dateStyle: 'long' }));

  // ---- loading --------------------------------------------------------------------------------------
  /** The frame read the project; this screen reads the BOQ for it. */
  protected onLoaded(project: ProjectDetail): void {
    this.project.set(project);
    void this.loadAll();
  }

  protected onFailed(messageKey: string): void {
    this.failure.set(messageKey);
    this.loading.set(false);
  }

  /** Retry after a failed read: the project again, then the BOQ. */
  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      this.project.set(await this.projects.get(this.projectId()));
      await this.loadAll();
    } catch (error) {
      this.failure.set(toProblem(error).messageKey);
      this.loading.set(false);
    }
  }

  private async loadAll(): Promise<void> {
    this.loading.set(true);
    this.failure.set(null);
    try {
      await this.loadBoq();
      if (this.editable()) {
        await this.loadCatalogue();
      }
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
      if (toProblem(error).code !== 'boq.not_found') {
        throw error;
      }
      this.boq.set({ id: '', projectId: this.projectId(), frozenOn: null, total: null, lines: [] });
    }
  }

  private async loadCatalogue(): Promise<void> {
    this.pickerFailure.set(null);
    try {
      const [items, babs] = await Promise.all([this.catalogue.list('', 'active'), this.babsApi.list('active')]);
      this.items.set(items);
      this.babs.set(babs);
    } catch (error) {
      this.pickerFailure.set(toProblem(error).messageKey);
    }
  }

  // ---- reading a line ---------------------------------------------------------------------------------
  /** One quantity: the surveyed value once a survey is recorded, the estimate until then (AC-5). */
  protected quantityOf(line: BoqLine): string | null {
    return line.quantitySurveyed ?? line.quantityEstimated;
  }

  protected quantityState(line: BoqLine): BoqQuantityState {
    if (!this.editable()) {
      return 'settled';
    }
    return line.quantitySurveyed !== null ? 'surveyed' : line.quantityEstimated !== null ? 'estimated' : 'none';
  }

  protected plain(value: string | null): string {
    return plainQuantity(value);
  }

  protected lineName(line: BoqLine): string {
    return this.i18n.locale() === 'ar' ? line.descriptionAr : (line.descriptionEn ?? line.descriptionAr);
  }

  protected day(value: string): string {
    const [year = 0, month = 1, day = 1] = value.split('-').map(Number);
    return this.i18n.formatDate(new Date(year, month - 1, day), { dateStyle: 'medium' });
  }

  // ---- writes -----------------------------------------------------------------------------------------
  /** One path for every write: busy, run, refresh the BOQ, or show the server's own refusal. */
  private async mutate(action: () => Promise<void>): Promise<boolean> {
    this.busy.set(true);
    this.refusal.set(null);
    this.skipped.set([]);
    try {
      await action();
      await this.loadBoq();
      return true;
    } catch (error) {
      this.refusal.set(this.refusalOf(error));
      return false;
    } finally {
      this.busy.set(false);
    }
  }

  /** The line the server named when it refused a signature for a missing quantity. */
  private missingLineId: string | null = null;

  private refusalOf(error: unknown): Refusal {
    const problem = toProblem(error);
    const name = problem.extensions['descriptionAr'];
    const lineId = problem.extensions['lineId'];
    this.missingLineId = typeof lineId === 'string' ? lineId : null;
    if (problem.code === 'boq.line_quantity_missing' && typeof name === 'string') {
      return { key: 'boq.error.line_missing', params: { name } };
    }
    return { key: problem.messageKey };
  }

  protected async changeQuantity(line: BoqLine, text: string): Promise<void> {
    const value = text === '' ? null : text;
    await this.mutate(async () => {
      await this.api.editLine(this.projectId(), line.id, {
        quantityEstimated: line.quantitySurveyed === null ? value : line.quantityEstimated,
        quantitySurveyed: line.quantitySurveyed === null ? null : value,
        conditionsRate: line.conditionsRate,
        lineMarkup: line.lineMarkup,
      });
    });
  }

  /** No confirmation (AC-12): the item can be added again from the catalogue. */
  protected async removeLine(line: BoqLine): Promise<void> {
    await this.mutate(async () => {
      await this.api.removeLine(this.projectId(), line.id);
    });
  }

  protected async addItems(ids: readonly string[]): Promise<void> {
    if (ids.length === 0 || this.busy()) {
      return;
    }
    const before = new Set(this.lines().map((line) => line.id));
    const done = await this.mutate(async () => {
      await this.api.addLines(
        this.projectId(),
        ids.map((id) => ({ catalogueItemId: id, quantityEstimated: null, quantitySurveyed: null })),
      );
    });
    if (done) {
      this.addSheet.set(false);
      this.focusNewLine(before);
    }
  }

  /** The new line takes focus in its quantity box. */
  private focusNewLine(before: ReadonlySet<string>): void {
    const added = this.lines().find((line) => !before.has(line.id));
    if (added !== undefined) {
      this.focusLine(added.id);
    }
  }

  private focusLine(lineId: string): void {
    setTimeout(() => {
      const box = this.host.nativeElement.querySelector<HTMLInputElement>(`[data-testid="boq-line-${lineId}-qty"]`);
      box?.scrollIntoView?.({ block: 'center' });
      box?.focus();
    });
  }

  protected onDragOver(event: DragEvent): void {
    if (this.editable()) {
      event.preventDefault();
      this.dropping.set(true);
    }
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dropping.set(false);
    const id = event.dataTransfer?.getData('text/plain');
    if (this.editable() && id) {
      void this.addItems([id]);
    }
  }

  // ---- start ------------------------------------------------------------------------------------------
  protected async openStart(): Promise<void> {
    this.startSearch.set('');
    this.startFailure.set(null);
    this.refusal.set(null);
    this.startSheet.set(true);
    this.startLoading.set(true);
    try {
      const [bases, listed] = await Promise.all([this.api.listBases(), this.projects.list()]);
      this.bases.set(bases);
      this.earlier.set(listed.projects.filter((project) => project.id !== this.projectId() && hasBoqType(project.contractType) && project.boqLineCount !== 0));
    } catch (error) {
      this.startFailure.set(toProblem(error).messageKey);
    } finally {
      this.startLoading.set(false);
    }
  }

  protected startBlank(): void {
    this.started.set(true);
    this.startSheet.set(false);
  }

  protected async copyFrom(source: { baseId: string } | { sourceProjectId: string }): Promise<void> {
    this.busy.set(true);
    this.startFailure.set(null);
    this.skipped.set([]);
    try {
      const result = await this.api.copy(this.projectId(), source);
      await this.loadBoq();
      this.startSheet.set(false);
      this.skipped.set(result.skipped.map((entry) => entry.descriptionAr ?? '').filter((name) => name !== ''));
    } catch (error) {
      this.startFailure.set(toProblem(error).messageKey);
    } finally {
      this.busy.set(false);
    }
  }

  private matchesStartSearch(name: string): boolean {
    const term = this.startSearch().trim().toLocaleLowerCase();
    return term === '' || name.toLocaleLowerCase().includes(term);
  }

  // ---- custom item ------------------------------------------------------------------------------------
  protected openCustom(): void {
    this.customName.set('');
    this.customUnit.set('');
    this.customRate.set('');
    this.customBab.set('');
    this.customAttempted.set(false);
    this.addSheet.set(false);
    this.customSheet.set(true);
  }

  protected onText(target: EventTarget | null, field: WritableSignal<string>): void {
    field.set((target as HTMLInputElement).value);
  }

  protected async submitCustom(): Promise<void> {
    this.customAttempted.set(true);
    const errors = this.customErrors();
    if (errors.name || errors.unit || errors.rate || errors.bab) {
      return;
    }
    const before = new Set(this.lines().map((line) => line.id));
    const rate = this.customRate().trim();
    const done = await this.mutate(async () => {
      // ponytail: the form has no cost field (mock), and the API requires one. Cost = the typed rate (no margin
      // is invented); the Technical Office sets the real cost when it reviews the item for the catalogue.
      await this.api.addCustomLine(this.projectId(), {
        descriptionAr: this.customName().trim(),
        unit: this.customUnit().trim(),
        babId: this.customBab(),
        costPrice: rate,
        baseSellRate: rate,
      });
    });
    if (done) {
      this.customSheet.set(false);
      this.focusNewLine(before);
    }
  }

  // ---- sign -------------------------------------------------------------------------------------------
  protected openSign(): void {
    this.hold.set('');
    this.advance.set('');
    this.signAttempted.set(false);
    this.refusal.set(null);
    this.signSheet.set(true);
  }

  private rateError(raw: string): string | null {
    const text = raw.trim();
    if (text === '') {
      return 'boq.error.required';
    }
    return PERCENT_INPUT_PATTERN.test(text) && Number(text) <= 100 ? null : 'boq.error.rate_range';
  }

  protected async confirmSign(): Promise<void> {
    this.signAttempted.set(true);
    const errors = this.signErrors();
    if (errors.hold || errors.advance) {
      return;
    }
    const done = await this.mutate(async () => {
      await this.api.sign(this.projectId(), {
        holdPercentage: percentToFraction(this.hold()),
        clientAdvance: Number(this.advance().trim()),
      });
      this.project.set(await this.projects.get(this.projectId()));
    });
    if (done) {
      void this.frame()?.reload(); // the frame's stage line and tabs change once signed
      this.signSheet.set(false);
    } else if (this.missingLineId !== null) {
      // The server named the line with no quantity: leave the sheet and take the user to that line.
      this.signSheet.set(false);
      this.focusLine(this.missingLineId);
    }
  }

  protected print(): void {
    window.print();
  }
}
