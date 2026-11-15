import { Injectable, computed, effect, signal } from '@angular/core';

/**
 * KAFF-936 AC-936-H: one count of the overlays that are open (confirm dialog, detail panel). The
 * shell hides the phone bottom bar while any is open, so every overlay shares one mechanism and the
 * shell names none of them. Call `track` from an overlay's constructor.
 */
@Injectable({ providedIn: 'root' })
export class OverlayService {
  private readonly count = signal(0);
  readonly anyOpen = computed(() => this.count() > 0);

  /** Counts this overlay as open while `open()` is true; released when it closes or is destroyed. */
  track(open: () => boolean): void {
    effect((onCleanup) => {
      if (open()) {
        this.count.update((n) => n + 1);
        onCleanup(() => this.count.update((n) => n - 1));
      }
    });
  }
}
