import { Injectable, TemplateRef, signal } from '@angular/core';

/** The variants `kaff-button` draws; a destructive one may never take the primary slot (AC-936-G). */
export type PrimaryActionVariant = 'primary' | 'secondary' | 'ghost';

/**
 * KAFF-923 / KAFF-936: the shell's one primary-action slot. A screen declares its action template and
 * the variant it draws; the shell renders it in the page header on desktop and in the phone bottom
 * bar, and clears it on destroy so the next page does not inherit a stale action.
 */
@Injectable({ providedIn: 'root' })
export class HeaderActionsService {
  private readonly slot = signal<TemplateRef<unknown> | null>(null);
  readonly template = this.slot.asReadonly();

  /**
   * AC-936-G: the type refuses `danger` and `danger-outline` at compile time; a widened value is
   * refused here at runtime.
   */
  set(template: TemplateRef<unknown> | null, variant: PrimaryActionVariant): void {
    if ((variant as string).startsWith('danger')) {
      throw new Error(`A destructive action (variant "${variant}") cannot take the primary-action slot.`);
    }
    this.slot.set(template);
  }

  clear(): void {
    this.slot.set(null);
  }
}
