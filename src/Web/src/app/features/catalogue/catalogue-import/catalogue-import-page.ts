import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { toProblem } from '../../../core/api/problem-details';
import {
  CATALOGUE_IMPORT_TEMPLATE_FILENAME,
  CATALOGUE_IMPORT_TEMPLATE_URL,
  CatalogueApi,
  CatalogueImportResult,
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

  protected readonly canSubmit = computed(() => this.selectedFile() !== null && !this.uploading());

  protected onFileChosen(event: Event): void {
    const target = event.target;
    const file = target instanceof HTMLInputElement ? (target.files?.item(0) ?? null) : null;

    this.selectedFile.set(file);
    this.refusal.set(null);
    this.result.set(null);
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

  protected fieldLabel(column: string): string {
    const key = fieldLabelKey(column);
    return key === null ? '' : this.i18n.t(key);
  }

  protected trackFailure(index: number): number {
    return index;
  }
}
