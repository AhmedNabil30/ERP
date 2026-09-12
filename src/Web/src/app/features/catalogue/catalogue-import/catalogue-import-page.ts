import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import {
  CATALOGUE_IMPORT_TEMPLATE_FILENAME,
  CATALOGUE_IMPORT_TEMPLATE_URL,
  CatalogueApi,
  CatalogueImportResult,
  CatalogueReimportPreview,
  CatalogueReimportResult,
} from '../../../core/catalogue/catalogue.api';
import { I18nService } from '../../../core/i18n/i18n.service';

/** The template's own column names (`CatalogueTemplate.cs`) mapped to the labels the form already uses. */
const FIELD_LABEL_KEYS: Readonly<Record<string, string>> = {
  code: 'catalogue.field.code',
  descriptionAr: 'catalogue.field.description',
  descriptionEn: 'catalogue.field.description_en',
  unit: 'catalogue.field.unit',
  bab: 'catalogue.field.bab',
  costPrice: 'catalogue.field.cost_price',
  baseSellRate: 'catalogue.field.base_sell_rate',
};

/** A domain-factory refusal that names no one column (`RowFailure.Column` empty). */
function fieldLabelKey(column: string): string | null {
  return FIELD_LABEL_KEYS[column] ?? null;
}

/**
 * S-019 · `KAFF-200` — download the template, upload the filled file as-is, read back the report.
 *
 * **Nothing here parses the spreadsheet.** The file goes to `CatalogueApi.import` untouched; every
 * row-level refusal is the server's own read of it (D-136). This component's only job is the upload
 * and translating `messageKey` back into words the Technical Office can act on.
 */
@Component({
  selector: 'kaff-catalogue-import-page',
  imports: [RouterLink],
  templateUrl: './catalogue-import-page.html',
  styleUrl: './catalogue-import-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogueImportPage {
  private readonly api = inject(CatalogueApi);

  protected readonly i18n = inject(I18nService);
  protected readonly templateUrl = CATALOGUE_IMPORT_TEMPLATE_URL;
  protected readonly templateFilename = CATALOGUE_IMPORT_TEMPLATE_FILENAME;

  protected readonly selectedFile = signal<File | null>(null);
  protected readonly uploading = signal(false);
  protected readonly refusal = signal<string | null>(null);
  protected readonly result = signal<CatalogueImportResult | null>(null);

  // KAFF-201 — re-import is its own action, not KAFF-200's flow reused. AC-201-B: preview names what
  // would change before anything does; confirming applies exactly that plan. The server is stateless
  // (Handler.cs), so `reimportFile` pins the exact File the preview was drawn from — the input can be
  // re-chosen underneath without the confirm silently sending a different file.
  protected readonly reimportFile = signal<File | null>(null);
  protected readonly previewing = signal(false);
  protected readonly confirming = signal(false);
  protected readonly reimportRefusal = signal<string | null>(null);
  protected readonly reimportPreview = signal<CatalogueReimportPreview | null>(null);
  protected readonly reimportResult = signal<CatalogueReimportResult | null>(null);

  protected readonly canSubmit = computed(() => this.selectedFile() !== null && !this.uploading());
  protected readonly canReimport = computed(
    () => this.selectedFile() !== null && !this.previewing() && this.reimportPreview() === null,
  );

  protected onFileChosen(event: Event): void {
    const target = event.target;
    const file = target instanceof HTMLInputElement ? (target.files?.item(0) ?? null) : null;

    this.selectedFile.set(file);
    this.refusal.set(null);
    this.result.set(null);
    this.reimportFile.set(null);
    this.reimportRefusal.set(null);
    this.reimportPreview.set(null);
    this.reimportResult.set(null);
  }

  protected async onSubmit(): Promise<void> {
    const file = this.selectedFile();
    if (file === null) {
      return;
    }

    this.uploading.set(true);
    this.refusal.set(null);
    this.result.set(null);

    try {
      this.result.set(await this.api.import(file));
    } catch (error) {
      // AC-200-I / the file-level 400: `errors.master.catalogue_import_failed`, surfaced, not swallowed.
      this.refusal.set(toProblem(error).messageKey);
    } finally {
      this.uploading.set(false);
    }
  }

  /** AC-201-B — asks what a second import would do. Nothing changes here; `Handler.PreviewAsync` writes nothing. */
  protected async onPreviewReimport(): Promise<void> {
    const file = this.selectedFile();
    if (file === null) {
      return;
    }

    this.previewing.set(true);
    this.reimportRefusal.set(null);
    this.reimportResult.set(null);

    try {
      this.reimportPreview.set(await this.api.previewReimport(file));
      this.reimportFile.set(file);
    } catch (error) {
      this.reimportRefusal.set(toProblem(error).messageKey);
    } finally {
      this.previewing.set(false);
    }
  }

  /** AC-201-B — cancelling sends no request; the preview is simply discarded. */
  protected onCancelReimport(): void {
    this.reimportPreview.set(null);
    this.reimportFile.set(null);
  }

  /**
   * AC-201-F — applies exactly the plan the preview named. The server is stateless (Handler.cs docs):
   * it re-parses the file rather than remembering the preview, so this resends `reimportFile`, the
   * exact File the operator confirmed against, not whatever the input currently holds.
   */
  protected async onConfirmReimport(): Promise<void> {
    const file = this.reimportFile();
    if (file === null) {
      return;
    }

    this.confirming.set(true);
    this.reimportRefusal.set(null);

    try {
      this.reimportResult.set(await this.api.confirmReimport(file));
      this.reimportPreview.set(null);
      this.reimportFile.set(null);
    } catch (error) {
      this.reimportRefusal.set(toProblem(error).messageKey);
    } finally {
      this.confirming.set(false);
    }
  }

  protected fieldLabel(column: string): string {
    const key = fieldLabelKey(column);
    return key === null ? '' : this.i18n.t(key);
  }

  protected trackFailure(index: number): number {
    return index;
  }
}
