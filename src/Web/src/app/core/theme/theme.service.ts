import { Injectable, computed, effect, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const THEME_STORAGE_KEY = 'kaff.theme';

/**
 * Light or dark (README "Colour tokens": follow `prefers-color-scheme`, and provide a user-visible
 * override in the product).
 *
 * The design tokens are `light-dark()` values, so the whole theme is the document's `color-scheme`.
 * With no stored choice this service sets nothing and the system preference decides; once the user
 * toggles, it writes `data-theme` on `<html>`, which `styles.css` turns into `color-scheme: light |
 * dark`.
 *
 * A chrome preference, so it persists in `localStorage` like the locale and the sidebar rail — per
 * device, outside D-050's ban on session material. Every storage access is guarded: a private window
 * or blocked site data makes the accessor throw, and the shell must still render.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly chosen = signal<Theme | null>(readStoredTheme());
  private readonly systemDark = signal(readSystemDark());

  /** What is actually showing: the user's choice, else the system preference. */
  readonly isDark = computed(() => (this.chosen() ?? (this.systemDark() ? 'dark' : 'light')) === 'dark');

  constructor() {
    effect(() => {
      const chosen = this.chosen();
      const root = document.documentElement;
      if (chosen) {
        root.setAttribute('data-theme', chosen);
      } else {
        root.removeAttribute('data-theme');
      }
    });

    if (typeof matchMedia === 'function') {
      matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (event) => {
        this.systemDark.set(event.matches);
      });
    }
  }

  toggle(): void {
    const next: Theme = this.isDark() ? 'light' : 'dark';
    this.chosen.set(next);
    try {
      localStorage.setItem(THEME_STORAGE_KEY, next);
    } catch {
      // A browser that refuses the write still gets the theme — it just forgets it on reload.
    }
  }
}

function readStoredTheme(): Theme | null {
  try {
    const value = localStorage.getItem(THEME_STORAGE_KEY);
    return value === 'light' || value === 'dark' ? value : null;
  } catch {
    return null;
  }
}

function readSystemDark(): boolean {
  return typeof matchMedia === 'function' && matchMedia('(prefers-color-scheme: dark)').matches;
}
