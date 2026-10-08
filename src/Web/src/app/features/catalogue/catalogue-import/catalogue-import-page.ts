import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import { AuthService } from '../../../core/auth/auth.service';
import { Bab, BabsApi } from '../../../core/catalogue/babs.api';
import {
  CATALOGUE_IMPORT_TEMPLATE_FILENAME,
  CATALOGUE_IMPORT_TEMPLATE_URL,
  CatalogueApi,
  CatalogueImportRowFailure,
  CatalogueItem,
  CatalogueReimportPreview,
  CatalogueReimportResult,
} from '../../../core/catalogue/catalogue.api';
import { sameDecimal } from '../../../core/catalogue/money-wire';
import { I18nService } from '../../../core/i18n/i18n.service';
import { KaffApplyBar } from '../../../shared/kaff-apply-bar/kaff-apply-bar';
import { KaffBadge } from '../../../shared/kaff-badge/kaff-badge';
import { KaffBanner } from '../../../shared/kaff-banner/kaff-banner';
import { KaffButton } from '../../../shared/kaff-button/kaff-button';
import { KaffConfirmDialog } from '../../../shared/kaff-confirm-dialog/kaff-confirm-dialog';
import { KaffDiff } from '../../../shared/kaff-diff/kaff-diff';
import { KaffIcon } from '../../../shared/kaff-icon/kaff-icon';
import {
  KaffSegmentedFilter,
  SegmentedFilterOption,
} from '../../../shared/kaff-segmented-filter/kaff-segmented-filter';
import { KaffStat } from '../../../shared/kaff-stat/kaff-stat';
import { KaffStatusPill } from '../../../shared/kaff-status-pill/kaff-status-pill';
import { KaffStepper } from '../../../shared/kaff-stepper/kaff-stepper';
import { KaffTable } from '../../../shared/kaff-table/kaff-table';
import { TableColumnDef } from '../../../shared/kaff-table-header/kaff-table-header';
import { KaffTableRow } from '../../../shared/kaff-table-row/kaff-table-row';

type Step = 'choose' | 'preview' | 'result';
type Tab = 'all' | 'new' | 'price' | 'error';

/** Rows shown before the "show more" row. The preview returns every row; only the drawing is sliced. */
const PAGE = 50;

const PHONE_QUERY = '(max-width: 820px)';

/** The server's own file ceiling (`ImportCatalogue.Handler.MaxUploadBytes`, D-136). Said in words, never enforced here. */
const FILE_LIMIT_MB = 10;

/** The refusals the server gives with NOTHING saved, so the screen may say nothing changed. */
const NOTHING_CHANGED_KEYS: ReadonlySet<string> = new Set([
  'errors.master.catalogue_price_owner_only',
  'errors.master.catalogue_import_failed',
]);

/** The template's own column names (`CatalogueTemplate.cs`), in the template's order. The file must carry exactly these. */
export const TEMPLATE_COLUMNS: readonly { readonly name: string; readonly labelKey: string; readonly internal?: boolean }[] =
  [
    { name: 'code', labelKey: 'catalogue.field.code' },
    { name: 'descriptionAr', labelKey: 'catalogue.field.description' },
    { name: 'descriptionEn', labelKey: 'catalogue.field.description_en' },
    { name: 'unit', labelKey: 'catalogue.field.unit' },
    { name: 'bab', labelKey: 'catalogue.field.bab' },
    { name: 'costPrice', labelKey: 'catalogue.field.cost_price', internal: true },
    { name: 'baseSellRate', labelKey: 'catalogue.panel.sell_price' },
  ];

const FIELD_LABEL_KEYS: Readonly<Record<string, string>> = {
  code: 'catalogue.field.code',
  descriptionAr: 'catalogue.field.description',
  descriptionEn: 'catalogue.field.description_en',
  unit: 'catalogue.field.unit',
  bab: 'catalogue.field.bab',
  costPrice: 'catalogue.field.cost_price',
  baseSellRate: 'catalogue.panel.sell_price',
};

const SUPERSCRIPTS: Readonly<Record<string, string>> = { '²': '2', '³': '3' };

/** One price cell of a row: the figure the file carries and, for an existing item, the stored one. */
interface PriceCell {
  readonly next: string;
  readonly previous: string | null;
}

/** One row of the preview, whatever its kind: what the file would do, drawn in one table. */
export interface PreviewRow {
  readonly key: string;
  /** `new`: the file adds it. `price`: an existing code whose stored price moves. `same`: an existing code the file restates unchanged. `error`: refused. */
  readonly kind: 'new' | 'price' | 'same' | 'error';
  /** An existing price the signed-in person may not move: drawn as a lock, and the whole confirm is refused. */
  readonly locked: boolean;
  readonly code: string | null;
  readonly rowNumber: number | null;
  readonly name: string | null;
  readonly babName: string | null;
  readonly unit: string | null;
  readonly cost: PriceCell | null;
  readonly sell: PriceCell | null;
  readonly changes: readonly ('cost' | 'sell')[];
  readonly failure: CatalogueImportRowFailure | null;
}

/**
 * S-019 · the catalogue import, three steps: choose the file, preview, result (`catalogue-import.html`).
 *
 * **The API is the contract, and it differs from the design in four places** (decisions.md D-183):
 * 1. Codes are TYPED, never generated. A row with a code the catalogue does not hold is a NEW item with that
 *    code; a row with an existing code re-prices that item; a row with no code is refused (`code_required`).
 * 2. An existing code is re-priced ONLY (`CatalogueItem.Reprice`): the description, unit and باب in the file
 *    are ignored for it, so there is no "description changed" row and no "was: …" line.
 * 3. The preview names an overwrite by code and its old and new prices, with no description, باب, unit or file
 *    row number. The screen reads the existing item from the list for those; a new row and a refused row carry
 *    what the API gives them (a refused row has only its row number, the column, the value and the reason).
 * 4. There is no report download endpoint, so none is offered.
 *
 * The preview and the confirm are both stateless on the server: the confirm re-parses the SAME file, so the
 * `File` the preview was drawn from is pinned here and resent. Nothing about the file is parsed in the browser.
 *
 * Moving an existing price is `CataloguePriceEdit` (the Owner alone, D-177/D-178) and the server refuses the
 * WHOLE confirm if any row would. The preview already shows every old and new price, so a person without the
 * permission is refused BEFORE the confirm: there is no apply button at all, not a disabled one, and no
 * "apply the rest". The permission read here is presentation; the server still decides at the confirm, and its
 * refusal has its own state.
 */
@Component({
  selector: 'kaff-catalogue-import-page',
  imports: [
    RouterLink,
    KaffApplyBar,
    KaffBadge,
    KaffBanner,
    KaffButton,
    KaffConfirmDialog,
    KaffDiff,
    KaffIcon,
    KaffSegmentedFilter,
    KaffStat,
    KaffStatusPill,
    KaffStepper,
    KaffTable,
    KaffTableRow,
  ],
  templateUrl: './catalogue-import-page.html',
  styleUrl: './catalogue-import-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogueImportPage {
  private readonly api = inject(CatalogueApi);
  private readonly babsApi = inject(BabsApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly i18n = inject(I18nService);
  protected readonly templateUrl = CATALOGUE_IMPORT_TEMPLATE_URL;
  protected readonly templateFilename = CATALOGUE_IMPORT_TEMPLATE_FILENAME;
  protected readonly templateColumns = TEMPLATE_COLUMNS;
  protected readonly fileLimitMb = FILE_LIMIT_MB;

  protected readonly step = signal<Step>('choose');
  protected readonly file = signal<File | null>(null);
  /** The preview is being read: nothing has changed in the catalogue yet. */
  protected readonly parsing = signal(false);
  /** The file-level refusal (`errors.master.catalogue_import_failed`), with the name of the file it was about. */
  protected readonly fileRefusal = signal<{ readonly key: string; readonly fileName: string } | null>(null);
  protected readonly preview = signal<CatalogueReimportPreview | null>(null);
  protected readonly tab = signal<Tab>('all');
  protected readonly shown = signal(PAGE);
  protected readonly dragging = signal(false);

  protected readonly confirmOpen = signal(false);
  protected readonly applying = signal(false);
  /** The key of a failed confirm; `null` until one. */
  protected readonly applyRefusal = signal<string | null>(null);
  protected readonly result = signal<CatalogueReimportResult | null>(null);

  private readonly existing = signal<ReadonlyMap<string, CatalogueItem>>(new Map());
  private readonly babs = signal<readonly Bab[]>([]);

  protected readonly isPhone = signal(typeof matchMedia === 'function' && matchMedia(PHONE_QUERY).matches);

  /** Mirrors `Permission.CataloguePriceEdit` (Owner only). Presentation; the server refuses at the confirm too. */
  protected readonly canEditPrices = computed(
    () => this.auth.current()?.permissions.includes('CataloguePriceEdit') ?? false,
  );
  protected readonly canReadAudit = computed(
    () => this.auth.current()?.permissions.includes('AuditRead') ?? false,
  );

  protected readonly stepLabels = computed(() => [
    this.i18n.t('catalogue.import.step_choose'),
    this.i18n.t('catalogue.import.step_preview'),
    this.i18n.t('catalogue.import.step_result'),
  ]);
  protected readonly stepIndex = computed(() => (this.step() === 'choose' ? 0 : this.step() === 'preview' ? 1 : 2));

  protected readonly columnDefs: readonly TableColumnDef[] = [
    { labelKey: 'catalogue.import.col_status' },
    { labelKey: 'catalogue.import.col_item' },
    { labelKey: 'catalogue.field.bab' },
    { labelKey: 'catalogue.column.unit' },
    {
      labelKey: 'catalogue.column.cost_price',
      align: 'end',
      markerIcon: 'eyeoff',
      markerKey: 'catalogue.badge.internal',
    },
    { labelKey: 'catalogue.column.sell_price', align: 'end' },
    { labelKey: 'catalogue.import.col_detail' },
  ];

  protected readonly resultColumnDefs: readonly TableColumnDef[] = [
    { labelKey: 'catalogue.import.column.row_number' },
    { labelKey: 'catalogue.import.column.field' },
    { labelKey: 'catalogue.import.column.value' },
    { labelKey: 'catalogue.import.column.reason' },
  ];

  // ---- the preview, as rows ----------------------------------------------------------------------

  private readonly babNameByCode = computed(() => {
    const english = this.i18n.locale() === 'en';
    return new Map(this.babs().map((bab) => [bab.code.toUpperCase(), english ? bab.nameEn : bab.nameAr]));
  });

  private readonly babNameById = computed(() => {
    const english = this.i18n.locale() === 'en';
    return new Map(this.babs().map((bab) => [bab.id, english ? bab.nameEn : bab.nameAr]));
  });

  /** Every row the file produced, in the order: refused rows first, then overwrites, then new rows. */
  protected readonly rows = computed<readonly PreviewRow[]>(() => {
    const preview = this.preview();
    if (preview === null) {
      return [];
    }
    const mayEdit = this.canEditPrices();
    const rows: PreviewRow[] = [];

    for (const failure of preview.failures) {
      rows.push({
        key: `e-${failure.rowNumber}-${failure.column}`,
        kind: 'error',
        locked: false,
        code: null,
        rowNumber: failure.rowNumber,
        name: null,
        babName: null,
        unit: null,
        cost: null,
        sell: null,
        changes: [],
        failure,
      });
    }

    for (const reprice of preview.reprices) {
      const item = this.existing().get(reprice.code.toUpperCase()) ?? null;
      const costMoves = !sameDecimal(reprice.oldCostPrice, reprice.newCostPrice);
      const sellMoves = !sameDecimal(reprice.oldBaseSellRate, reprice.newBaseSellRate);
      const changes: ('cost' | 'sell')[] = [];
      if (costMoves) {
        changes.push('cost');
      }
      if (sellMoves) {
        changes.push('sell');
      }
      rows.push({
        key: `r-${reprice.code}`,
        kind: changes.length > 0 ? 'price' : 'same',
        locked: changes.length > 0 && !mayEdit,
        code: reprice.code,
        rowNumber: null,
        name: item?.descriptionAr ?? null,
        babName: item === null ? null : (this.babNameById().get(item.babId) ?? null),
        unit: item?.unit ?? null,
        cost: { next: reprice.newCostPrice, previous: reprice.oldCostPrice },
        sell: { next: reprice.newBaseSellRate, previous: reprice.oldBaseSellRate },
        changes,
        failure: null,
      });
    }

    for (const create of preview.creates) {
      rows.push({
        key: `n-${create.code}`,
        kind: 'new',
        locked: false,
        code: create.code,
        rowNumber: null,
        name: create.descriptionAr,
        babName: this.babNameByCode().get(create.babCode.toUpperCase()) ?? create.babCode,
        unit: create.unit,
        cost: { next: create.costPrice, previous: null },
        sell: { next: create.baseSellRate, previous: null },
        changes: [],
        failure: null,
      });
    }

    return rows;
  });

  protected readonly newCount = computed(() => this.preview()?.creates.length ?? 0);
  protected readonly errorCount = computed(() => this.preview()?.failures.length ?? 0);
  /** Existing items whose stored price the file would move. */
  protected readonly priceCount = computed(() => this.rows().filter((r) => r.kind === 'price').length);
  /** Existing items the file restates at their stored price: the server counts them, and applies nothing for them. */
  protected readonly sameCount = computed(() => this.rows().filter((r) => r.kind === 'same').length);

  /** True when this person may not apply the file: it moves an existing price and the server would refuse all of it. */
  protected readonly refused = computed(() => this.priceCount() > 0 && !this.canEditPrices());
  protected readonly nothingToApply = computed(() => this.newCount() === 0 && this.priceCount() === 0);

  protected readonly tabOptions = computed<readonly SegmentedFilterOption<Tab>[]>(() => [
    { value: 'all', labelKey: 'catalogue.import.filter_all', labelText: this.i18n.t('catalogue.import.filter_all', { count: this.rows().length }) },
    { value: 'new', labelKey: 'catalogue.import.filter_new', labelText: this.i18n.t('catalogue.import.filter_new', { count: this.newCount() }) },
    { value: 'price', labelKey: 'catalogue.import.filter_price', labelText: this.i18n.t('catalogue.import.filter_price', { count: this.priceCount() }) },
    { value: 'error', labelKey: 'catalogue.import.filter_error', labelText: this.i18n.t('catalogue.import.filter_error', { count: this.errorCount() }) },
  ]);

  private readonly tabRows = computed<readonly PreviewRow[]>(() => {
    const tab = this.tab();
    return tab === 'all'
      ? this.rows()
      : this.rows().filter((row) => (tab === 'new' ? row.kind === 'new' : tab === 'price' ? row.kind === 'price' : row.kind === 'error'));
  });

  protected readonly visibleRows = computed(() => this.tabRows().slice(0, this.shown()));
  protected readonly hiddenCount = computed(() => Math.max(0, this.tabRows().length - this.shown()));

  protected readonly showingText = computed(() =>
    this.i18n.t('catalogue.import.showing', { shown: this.visibleRows().length, total: this.tabRows().length }),
  );

  protected readonly fileSizeText = computed(() => {
    const file = this.file();
    return file === null ? '' : this.i18n.t('catalogue.import.file_size', { kb: Math.max(1, Math.round(file.size / 1024)) });
  });

  protected readonly priceCaption = computed(() => {
    const base = this.i18n.t('catalogue.import.stat_price_caption');
    return this.sameCount() > 0
      ? `${base} · ${this.i18n.t('catalogue.import.stat_same_caption', { count: this.sameCount() })}`
      : base;
  });

  protected readonly applySummary = computed(() =>
    this.priceCount() > 0
      ? this.i18n.t('catalogue.import.apply_summary', { created: this.newCount(), changed: this.priceCount() })
      : this.i18n.t('catalogue.import.apply_summary_add', { created: this.newCount() }),
  );

  protected readonly dialogBody = computed(() =>
    this.priceCount() > 0
      ? this.i18n.t('catalogue.import.dlg_body', { created: this.newCount(), changed: this.priceCount() })
      : this.i18n.t('catalogue.import.dlg_body_add', { created: this.newCount() }),
  );

  protected readonly refusalReason = computed(() => {
    const key = this.applyRefusal();
    if (key === null) {
      return null;
    }
    return NOTHING_CHANGED_KEYS.has(key)
      ? { title: 'catalogue.import.refused_title', nothingChanged: true, key }
      : { title: 'catalogue.import.apply_failed', nothingChanged: false, key };
  });

  protected readonly skipped = computed(() => this.result()?.failures ?? []);

  constructor() {
    if (typeof matchMedia === 'function') {
      const query = matchMedia(PHONE_QUERY);
      const onChange = (event: MediaQueryListEvent): void => this.isPhone.set(event.matches);
      query.addEventListener('change', onChange);
      this.destroyRef.onDestroy(() => query.removeEventListener('change', onChange));
    }
  }

  // ---- step 1: the file --------------------------------------------------------------------------

  protected onFileChosen(event: Event): void {
    const target = event.target;
    const chosen = target instanceof HTMLInputElement ? (target.files?.item(0) ?? null) : null;
    if (chosen !== null) {
      void this.readPreview(chosen);
    }
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(true);
  }

  protected onDragLeave(): void {
    this.dragging.set(false);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    const dropped = event.dataTransfer?.files.item(0) ?? null;
    if (dropped !== null) {
      void this.readPreview(dropped);
    }
  }

  /** Asks what the file would do. Nothing changes to produce the answer (`AC-201-B`). */
  private async readPreview(file: File): Promise<void> {
    this.file.set(file);
    this.fileRefusal.set(null);
    this.applyRefusal.set(null);
    this.result.set(null);
    this.preview.set(null);
    this.parsing.set(true);
    try {
      const preview = await this.api.previewReimport(file);
      await this.loadLookups(preview);
      this.preview.set(preview);
      this.tab.set('all');
      this.shown.set(PAGE);
      this.step.set('preview');
    } catch (error) {
      this.fileRefusal.set({ key: toProblem(error).messageKey, fileName: file.name });
      this.step.set('choose');
    } finally {
      this.parsing.set(false);
    }
  }

  /**
   * The names the preview does not carry. An overwrite names only a code and two prices, so its description,
   * باب and unit come from the catalogue as it stands. A failed read leaves the row with its code alone.
   */
  private async loadLookups(preview: CatalogueReimportPreview): Promise<void> {
    const none: readonly never[] = [];
    const [items, babs] = await Promise.all([
      preview.reprices.length > 0 ? this.api.list('', 'all').catch(() => none) : Promise.resolve(none),
      this.babsApi.list('all').catch(() => none),
    ]);
    this.existing.set(new Map(items.map((item) => [item.code.toUpperCase(), item])));
    this.babs.set(babs);
  }

  // ---- step 2: the preview -----------------------------------------------------------------------

  protected onChooseAnother(): void {
    this.step.set('choose');
    this.file.set(null);
    this.preview.set(null);
    this.fileRefusal.set(null);
    this.applyRefusal.set(null);
    this.result.set(null);
    this.confirmOpen.set(false);
  }

  protected onTab(tab: Tab): void {
    this.tab.set(tab);
    this.shown.set(PAGE);
  }

  protected onShowMore(): void {
    this.shown.update((n) => n + PAGE);
  }

  protected onAskApply(): void {
    this.confirmOpen.set(true);
  }

  protected onCancelApply(): void {
    if (!this.applying()) {
      this.confirmOpen.set(false);
    }
  }

  /** Applies exactly the plan the preview named, by resending the pinned file. */
  protected async onConfirmApply(): Promise<void> {
    const file = this.file();
    if (file === null) {
      return;
    }
    this.applying.set(true);
    try {
      this.result.set(await this.api.confirmReimport(file));
      this.confirmOpen.set(false);
      this.step.set('result');
    } catch (error) {
      this.confirmOpen.set(false);
      this.applyRefusal.set(toProblem(error).messageKey);
    } finally {
      this.applying.set(false);
    }
  }

  // ---- step 3: the result ------------------------------------------------------------------------

  protected onViewCatalogue(): Promise<boolean> {
    return this.router.navigate(['/catalogue']);
  }

  protected onViewAudit(): Promise<boolean> {
    return this.router.navigate(['/audit']);
  }

  // ---- drawing helpers ---------------------------------------------------------------------------

  protected fieldLabel(column: string): string {
    const key = FIELD_LABEL_KEYS[column];
    return key === undefined ? '' : this.i18n.t(key);
  }

  protected unitParts(unit: string): { readonly base: string; readonly sup: string | null } {
    const last = unit.at(-1) ?? '';
    const sup = SUPERSCRIPTS[last];
    return sup === undefined ? { base: unit, sup: null } : { base: unit.slice(0, -1), sup };
  }

  /**
   * A price as the preview shows it: two decimals at least and four at most, so two prices that differ in
   * the third or fourth decimal never read as the same figure. The wire string goes to `Intl` untouched (D-135).
   */
  protected amount(value: string): string {
    return this.i18n.formatNumber(value, { minimumFractionDigits: 2, maximumFractionDigits: 4 });
  }

  protected changesText(row: PreviewRow): string {
    const parts = row.changes.map((c) => this.i18n.t(c === 'cost' ? 'catalogue.field.cost_price' : 'catalogue.panel.sell_price'));
    return parts.join(' · ');
  }

  protected trackRow(_index: number, row: PreviewRow): string {
    return row.key;
  }

  protected trackFailure(index: number): number {
    return index;
  }
}
