import { Injectable, TemplateRef, signal } from '@angular/core';

/**
 * KAFF-923: the shell's top-bar action cluster (search / secondary / primary action, mockup
 * `Main.dc.html` 97-118) is a slot a feature page can project content into, not markup the shell
 * owns. This service is the slot itself — a page sets its template, the shell renders it via
 * `NgTemplateOutlet`, and clears it on destroy so the next page does not inherit stale actions.
 *
 * Wiring an actual screen's search/import/create controls into this slot is KAFF-924's per-screen
 * job, not this story's — this story only builds the slot.
 */
@Injectable({ providedIn: 'root' })
export class HeaderActionsService {
  private readonly slot = signal<TemplateRef<unknown> | null>(null);
  readonly template = this.slot.asReadonly();

  set(template: TemplateRef<unknown> | null): void {
    this.slot.set(template);
  }
}
