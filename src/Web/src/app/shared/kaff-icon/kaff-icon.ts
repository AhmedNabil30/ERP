import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * One stroke family for every glyph (README "Rules that apply to every part": 24px grid, 1.8px stroke,
 * round caps). The path data is copied from `.design/v2/team-a/catalogue.html`'s icon table; the shell's
 * own navigation icons stay in `app.html` until that file adopts this component.
 *
 * Decorative by default (`aria-hidden`): the label is always text next to the icon, because colour and
 * shape are never the only carrier of meaning.
 */
const PATHS = {
  chev: 'M6 9l6 6 6-6',
  dots: 'M12 5.5v.01M12 12v.01M12 18.5v.01',
  x: 'M6 6l12 12M18 6L6 18',
  eye: 'M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12zM12 15a3 3 0 100-6 3 3 0 000 6z',
  eyeoff:
    'M3 3l18 18M10.6 5.1A9.7 9.7 0 0112 5c6 0 10 7 10 7a17 17 0 01-3.2 3.9M6.6 6.6C3.7 8.4 2 12 2 12s4 7 10 7c1.7 0 3.2-.5 4.5-1.2M9.9 9.9a3 3 0 004.2 4.2',
  arch: 'M3 5h18v4H3zM5 9v10h14V9M10 13h4',
  unarch: 'M3 5h18v4H3zM5 9v10h14V9M12 17v-5M9.5 14.5L12 12l2.5 2.5',
  pen: 'M4 20h4L19 9l-4-4L4 16v4z',
  lock: 'M6 11h12v9H6zM8 11V8a4 4 0 018 0v3',
  search: 'M11 18a7 7 0 100-14 7 7 0 000 14zM21 21l-4.5-4.5',
  check: 'M5 12.5l4.5 4.5L19 7.5',
  info: 'M12 21a9 9 0 100-18 9 9 0 000 18zM12 11v5M12 8v.01',
  alert: 'M12 4l9 16H3zM12 10v4M12 17.5v.5',
  upload: 'M12 15V4M8 8l4-4 4 4M5 14v6h14v-6',
  plus: 'M12 5v14M5 12h14',
  back: 'M15 6l-6 6 6 6',
  move: 'M3 7h6l2 2h10v10H3zM12 14h6M16 12l2 2-2 2',
  child: 'M5 4v10a3 3 0 003 3h8M13 13l3 4-3 4M5 4h5',
  layers: 'M12 3l9 5-9 5-9-5 9-5zM3 13l9 5 9-5',
  swap: 'M4 8h13l-3-3M20 16H7l3 3',
  file: 'M6 3h9l4 4v14H6zM14 3v5h5',
  download: 'M12 4v11M8 11l4 4 4-4M5 20h14',
  catalog: 'M3 11V4h7l11 11-7 7L3 11zM7.5 7.5h.01',
  error: 'M12 21a9 9 0 100-18 9 9 0 000 18zM12 7.5v5.5M12 16.5v.01',
  calc: 'M5 9h14M5 15h14',
  link: 'M10 14a4 4 0 005.7 0l3-3a4 4 0 00-5.7-5.7l-1 1M14 10a4 4 0 00-5.7 0l-3 3a4 4 0 005.7 5.7l1-1',
  help: 'M12 21a9 9 0 100-18 9 9 0 000 18zM9.5 9.5a2.5 2.5 0 015 .5c0 1.5-2.5 2-2.5 3.5M12 17v.01',
  pause: 'M9 5v14M15 5v14',
  list: 'M4 7h16M4 12h16M4 17h10',
  cam: 'M4 8h3l2-3h6l2 3h3v11H4zM12 17a3.5 3.5 0 100-7 3.5 3.5 0 000 7z',
  clock: 'M12 21a9 9 0 100-18 9 9 0 000 18zM12 7v5l3 2',
  pin: 'M12 21s7-6.2 7-11a7 7 0 10-14 0c0 4.8 7 11 7 11zM12 12a2 2 0 100-4 2 2 0 000 4z',
  note: 'M6 3h9l4 4v14H6zM14 3v5h5M9 13h6M9 17h6',
  qty: 'M3 17L17 3l4 4L7 21zM8 12l2 2M11 9l2 2M14 6l2 2',
  mat: 'M3 8l9-5 9 5v8l-9 5-9-5zM3 8l9 5 9-5M12 13v8',
  stop: 'M12 21a9 9 0 100-18 9 9 0 000 18zM9.5 9.5h5v5h-5z',
  out: 'M9 5H5v14h4M16 8l4 4-4 4M20 12H9',
  crew: 'M16 20v-1.5a3.5 3.5 0 00-3.5-3.5h-5A3.5 3.5 0 004 18.5V20M10 11a3.5 3.5 0 100-7 3.5 3.5 0 000 7zM20 20v-1.5a3.5 3.5 0 00-2.6-3.4M15.5 4.2a3.5 3.5 0 010 6.6',
  folder: 'M3 7a2 2 0 012-2h4l2 2h8a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V7z',
} as const;

export type KaffIconName = keyof typeof PATHS;

@Component({
  selector: 'kaff-icon',
  template: `<svg
    class="i"
    [class.s]="small()"
    viewBox="0 0 24 24"
    aria-hidden="true"
    focusable="false"
  >
    <path [attr.d]="path()" />
  </svg>`,
  styles: `
    :host {
      display: inline-flex;
      flex: none;
    }

    .i {
      inline-size: 22px;
      block-size: 22px;
      fill: none;
      stroke: currentColor;
      stroke-width: 1.8;
      stroke-linecap: round;
      stroke-linejoin: round;
    }

    .s {
      inline-size: 18px;
      block-size: 18px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KaffIcon {
  readonly name = input.required<KaffIconName>();
  /** The 18px size the README calls `.s`. */
  readonly small = input(false);

  protected readonly path = computed(() => PATHS[this.name()]);
}
