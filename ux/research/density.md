# Desktop density and chrome — research and proposal

**Author:** UX researcher (1 of 3 — angle: desktop density and chrome). Read-only investigation; no source
file was changed by this document.
**Date:** 2026-09-26. Demo 2026-09-27.
**Trigger:** Nabil, verbatim: *"the font is very big everything is very big the side menue should be
collapsable i dont know what to say exept i am very disapointed"*.

This file proposes a mechanism and a token table. It does not decide anything that belongs to Nabil or
Karim; open questions are listed in §7.

---

## 0. The diagnosis in one paragraph

Nothing in `src/Web` is wrong for a phone. Everything in `src/Web` is a phone. `styles.css:10-11`
states the intent explicitly — *"Mobile first. The daily log is designed for a phone on a building
site; every other screen inherits that baseline. Tap targets are at least 44px"* — and the token block
at `styles.css:17-38` has no desktop counterpart anywhere in the file. So a 27-inch monitor renders a
16px body (`styles.css:28`, `--text-md: 1rem`; applied at `styles.css:224-225`) at a 1.7 line-height
(`styles.css:89`), with every row and control floored at 44px (`styles.css:38`), and a 248px sidebar
that cannot be put away (`app/app.css:116-117`, `inline-size: 15.5rem`; the only collapse in the
codebase is the phone drawer at `app/app.css:291-307`, scoped `@media (max-width: 640px)`).

That is not a styling defect screen by screen. It is one missing breakpoint.

---

## 1. What real data-dense desktop products actually do

Every product below ships **two or three densities**, not one, and all of them treat 44px as the
*touch* figure rather than the universal figure.

### SAP Fiori — the closest analogue (enterprise, ERP, mouse-primary back office)

| Mode | Row / control size | Target device | Font size |
|---|---|---|---|
| Cozy (`sapUiSizeCozy`) | **2.75rem / 44px** touch area | smartphones, touch devices | base |
| Compact (`sapUiSizeCompact`) | **2rem / 32px** rows and toolbars | desktop, mouse-operated | **unchanged from cozy** |
| Condensed (`sapUiSizeCondensed`) | smaller again, table rows only; must be set *in addition to* compact, never alone | desktop tables needing maximum rows | unchanged |

Two findings matter for us:

1. **Fiori's cozy figure is literally our number** — 2.75rem / 44px — and Fiori treats it as the
   *touch* density, switching desktop to 32px. We have adopted the touch value and never shipped the
   desktop one.
2. **Fiori does not shrink the font between cozy and compact.** It shrinks controls and spacing and
   leaves type alone. That is a significant data point against an aggressive type cut, and it is the
   reason the table in §4 shrinks spacing and row height harder than it shrinks type.

Fiori also forbids mixing densities inside one DOM hierarchy — the class goes on `<body>` and cascades.

- https://www.sap.com/design-system/fiori-design-web/v1-145/foundations/visual/cozy-compact
- https://github.com/SAP-docs/sapui5/blob/main/docs/04_Essentials/content-densities-e54f729.md
- https://help.sap.com/docs/ABAP_PLATFORM_NEW/468a97775123488ab3345a0c48cadd8f/f3cc057e405c4fd58ee2ed42c557797c.html

### IBM Carbon — five documented row heights

Carbon's data table ships row heights of **24px (xs) · 32px (sm) · 40px (md) · 48px (lg) · 64px (xl)**,
with the rule that the header row always matches the body row size, and a paired toolbar height per
tier (small toolbar with xs/sm, large toolbar with lg/xl). Carbon 11 added the 40px middle tier; v10's
vocabulary was compact/short/normal/tall.

- https://carbondesignsystem.com/components/data-table/style/
- https://v10.carbondesignsystem.com/components/data-table/usage/
- https://github.com/carbon-design-system/carbon/issues/8874

### Ant Design — two table sizes, and a documented sidebar rail

- Table cell padding: **`middle` = 12px 8px**, **`small` = 8px**; virtual-scroll rows are fixed at
  **36px**. https://ant.design/components/table/
- `Layout.Sider` defaults: **`width = 200`** expanded, **`collapsedWidth = 80`** collapsed, with a
  `breakpoint` prop that collapses it automatically and a special floating trigger when
  `collapsedWidth = 0`. https://ant.design/components/layout/ ·
  https://github.com/ant-design/ant-design/blob/master/components/layout/Sider.tsx

### Microsoft Fluent 2 / Dynamics 365

Fluent's base unit is 4px, and the DataGrid's **medium row height is 32px** (the Blazor
`FluentDataGrid` `ItemSize` default renders 32px rows).

- https://fluent2.microsoft.design/layout
- https://github.com/microsoft/fluentui/issues/34495
- https://fluentui-blazor.azurewebsites.net/datagrid

### Material Design 3 — navigation widths

- **Navigation rail: 80dp wide** by default, collapsed state shows icons without labels.
  https://m3.material.io/components/navigation-rail/guidelines
- **Navigation drawer: 360dp** wide; M3 Expressive now steers new work to an expanded navigation rail
  instead of a drawer. https://m3.material.io/components/navigation-drawer/overview

80dp is a *touch* rail. For a mouse, 48–56px is the common figure (Ant's 80px collapsed sider is the
outlier, and it is 80px because it keeps a centred icon plus optional label room).

### Oracle Redwood

Checked and **discarded as a citation**: the public Redwood toolkit pages
(https://redwood.oracle.com/) render their component specs through JavaScript and expose no fetchable
row-height or rail-width numbers. Nothing in this document rests on Redwood.

---

## 2. The Arabic floor — how small is too small

This is the constraint that stops us simply copying Fiori compact or Carbon xs.

### The two Arabic-first government design systems are the strongest evidence

| System | Body minimum | Smallest step in the scale | Line height at body |
|---|---|---|---|
| **UAE Design System 2.0** | *"We recommend a minimum of 16px, unless the context necessitates less"* | `text-xs` 0.75rem/12px with 1rem/16px leading | `text-base` 16px / 24px = **1.5** |
| **Dubai Design System** | *"Body text is never reduced below 14px, even for secondary content, to preserve legibility"* | small text 12px / 16px leading | body 16px / 24px = **1.5** desktop; 14px / 20px mobile |

- https://designsystem.gov.ae/guidelines/typography
- https://designsystem.dubai.ae/foundations/typography

Both systems run **one scale for Arabic and Latin** and both land on **1.5** for body leading, not 1.7.
Neither states that Arabic needs more leading than Latin. UAE goes further and says its Arabic face
(Noto Kufi Arabic) is *"compatible with the letter and word spacing by default"*.

So: **our `--line-height: 1.7` (`styles.css:89`) is above the floor, not at it.** The comment on that
line — *"Arabic needs a little more line height than Latin at the same size"* — is directionally true
but 1.7 is a comfortable-reading figure for prose, not a floor, and it is being applied to
single-line table cells and nav rows where there is no second line for the leading to serve.

### Why Arabic genuinely needs *some* extra vertical room

W3C *Arabic & Persian Layout Requirements*, §7.4 "Baselines, line height, etc.":
*"Arabic ascenders and descenders extend much further than those of the Latin script, and care must be
taken to correctly align text in the different scripts when they appear together."*
The document raises the difference and deliberately supplies **no metric**.
https://www.w3.org/TR/alreq/

Practitioner guidance (weaker authority — a portfolio case study, not a standard — cited because it is
the only source found that gives numbers) recommends **160% leading for UI (headings, buttons, labels,
inputs) and 180% for Arabic body prose**, and a size correction at small sizes: **14px Latin → 15px
Arabic, 12px → 14px**, on the grounds that diacritics and dots need vertical clearance Latin does not.
https://sunsetbisma.com/arabic-designsystem

Treat that 12px→14px correction as a **caution flag, not a rule**: it conflicts with both government
systems above, which ship a 12px step. It is enough reason not to introduce any *new* step below 12px.

### What WCAG does and does not require — stated precisely, because both are commonly misquoted

- **SC 1.4.12 Text Spacing (AA)** names line height 1.5×, paragraph spacing 2×, letter spacing 0.12×,
  word spacing 0.16× — but it **does not require authors to set those values**. The requirement is that
  content survives a *user* override to them without loss of content or functionality.
  https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html
  → Consequence: a desktop line-height of 1.5–1.6 is conformant, and even below 1.5 would be
  conformant *provided* a user override to 1.5 does not clip anything. We should still not go below
  1.5 on Arabic prose, for the reasons in §2 above rather than for WCAG.
- **SC 2.5.8 Target Size (Minimum) is 24×24 CSS px at AA.** The 44×44 figure is **SC 2.5.5 Target Size
  (Enhanced), level AAA**, plus documented exceptions (spacing, equivalent, inline, user-agent,
  essential).
  https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
  → Consequence: a 32px desktop row-action button is comfortably AA-conformant. Dropping a *touch*
  control below 44px is what breaks a documented commitment, not dropping a mouse-only one.

### The Arabic floor, as a decision

| Use | Floor | Why |
|---|---|---|
| Arabic prose (hints, empty states, notes, paragraphs) | **14px / 0.875rem, line-height ≥ 1.5** | Dubai's explicit never-below-14px; UAE's 1.5 |
| Arabic single-line data (table cell, nav label, chip, button) | **13px / 0.8125rem, line-height 1.35** | single line — no leading to serve; still above the 12px step both systems ship |
| Arabic small labels (column header, metadata, group label) | **12px / 0.75rem** — already our `--text-xs` | lowest step either government system ships. **Do not go below.** |
| Latin/numeric only (codes, amounts via `.figure`) | 12px | not Arabic script; `.figure` already isolates and tabularises it |

**Do not create a new scale step below 0.75rem.** `app/app.css:178` already has a hand-rolled
`0.6875rem` (11px) on `.nav-group-label` — below every floor named above, and off the token scale.
Flagged, not fixed, in §8.

---

## 3. The mechanism

### The real conflict, stated fairly

`CLAUDE.md` and `spec.md` make the daily log a phone on a building site, and `ux/screen-inventory.md:21-22`
already classifies every screen: **M1** (mobile-first, designed at 390px — S-073/S-074 daily log,
S-026 register worker from site, S-111 conflict review, S-003a reset link), **M2** (must be comfortable
at 390px), **M3** (desktop-primary, must still be correct at 390px — the master-data lists and forms
Nabil is looking at). A 44px target on an M1 screen is not padding; it is a gloved thumb on scaffolding.
A 44px row in an M3 list on a monitor is wasted screen.

### Rejected options, with reasons

1. **Shrink the global tokens.** One line, and it breaks the daily log. No.
2. **A per-component `density` input.** Every component gains an input, every caller passes it, and
   `styles.css`'s single source of truth fragments. Rejected as over-engineering.
3. **A user-facing density toggle (Fiori's launchpad setting, Gmail's comfortable/cozy/compact).** The
   right long-term answer, and the wrong one for a demo tomorrow: it needs a settings surface, an i18n
   catalogue entry, persistence, and a decision from Nabil about whether it is per-user or per-device.
   Keep it as the §5 upgrade path, not the shipped mechanism.
4. **Viewport width alone (`@media (min-width: 64rem)`).** Nearly right, and wrong on exactly one
   device class: a tablet or touch laptop in landscape is wide *and* touched. It would take the 44px
   floor away from a finger.

### Proposed mechanism — a media-scoped token override on `:root`, gated on **two** axes

```
Type, spacing and row height   ←  scale with available width      →  @media (min-width: 64rem)
The 44px interactive floor     ←  scales with pointer accuracy     →  @media (pointer: fine)
```

`pointer: fine` tests the **primary** pointing device; on a touch laptop the browser usually reports
the touchscreen as primary, so `(pointer: coarse)` matches even with a mouse attached, and our 44px
floor survives. `any-pointer` is the wrong feature here — it matches if *any* device is fine, which
would strip the floor from exactly the hybrid device we are protecting. Widely available since
December 2018, Media Queries Level 4.
https://developer.mozilla.org/en-US/docs/Web/CSS/@media/pointer

One rule block, in `styles.css`, next to the tokens it overrides:

```
@media (min-width: 64rem) and (pointer: fine) { :root { /* the desktop column of §4 */ } }
```

**Why this is cheap here, concretely:**

- `--tap-target` is consumed as `var(--tap-target)` in **63 places across 27 files** (verified by
  grep), so a token override reaches essentially the whole app with no per-component edit. Custom
  property inheritance already crosses Angular's emulated-encapsulation boundary in this codebase —
  `kaff-table` sets `--kaff-columns` on its own host (`shared/kaff-table/kaff-table.ts:27`,
  `'[style.--kaff-columns]': 'columns()'`) and `kaff-table-row.css:5` / `kaff-table-header.css:5` read
  it from inside their own scopes. `:root` is strictly easier than that: it is above every component.
- No `::ng-deep`. No JavaScript. No new dependency. Zoneless and signals are untouched — a media query
  is not change detection.

**Costs, named:**

1. **A desktop browser narrowed below 64rem snaps back to phone sizing mid-session.** That is the
   correct behaviour for a responsive app and it will still surprise someone. Mitigation: the cut-over
   is a single breakpoint, and both sides are legible.
2. **Nabil cannot choose.** The breakpoint decides. If he wants Fiori's user setting, that is the §5
   upgrade path and a separate story.
3. **Screenshot and E2E baselines shift** for every M3 screen at desktop width. Playwright specs that
   assert a computed `min-block-size` of `44px` at desktop viewport will fail. That is the failure we
   *want* — it is the mechanism proving it reached those components.
4. **Four hardcoded `2.75rem` literals do not participate**: `app/app.css:51`, `:52` (`.nav-toggle`),
   `:78` (`.sign-out`), `:195` (`.nav-item`). Every other 44px in the app is the token. These four must
   be converted to `var(--tap-target)` for the mechanism to be honest — a one-line-each change, and
   `.nav-item` needs it anyway for §6.
5. **Mixed density inside one row.** Fiori forbids mixing densities in a hierarchy for a reason. Our
   override is on `:root`, so nothing mixes — but see the `--row-target` note in §4 for the one place
   two figures legitimately meet.

---

## 4. The token table

`styles.css:17-38` is the phone column and **does not change**. The desktop column is the body of one
new `@media (min-width: 64rem) and (pointer: fine)` block on `:root`.

### Type

| Token | Phone (unchanged) | Desktop | Note |
|---|---|---|---|
| `--text-xs` | `0.75rem` (12px) | `0.75rem` (12px) | **floor — unchanged.** Lowest step either Arabic government system ships |
| `--text-sm` | `0.875rem` (14px) | `0.8125rem` (13px) | single-line labels and metadata only |
| `--text-md` | `1rem` (16px) | `0.875rem` (14px) | body and data. Dubai's never-below-14px, exactly |
| `--text-lg` | `1.25rem` (20px) | `1.0625rem` (17px) | page titles |
| `--text-xl` | `1.5rem` (24px) | `1.25rem` (20px) | the one display size |
| `--line-height` | `1.7` | `1.55` | above WCAG 1.4.12's 1.5 reference, above the UAE/Dubai 1.5 body figure |
| `--line-height-tight` | **new** `1.35` | `1.3` | **new token.** Single-line contexts only: table cells, nav rows, buttons, chips, column headers. There is no second line for leading to serve, and 1.7 on a single line is 27px of box around 16px of glyph. Applying this to `.row`, `.nav-item`, `kaff-table-header` and `kaff-button` is the single largest visual win available, and it is available **on the phone too** without touching one tap target |

Net body reduction on desktop: 16px × 1.7 = 27.2px of line box → 14px × 1.55 = 21.7px. **20% less
vertical space per line of text**, before any spacing change.

### Spacing

| Token | Phone (unchanged) | Desktop |
|---|---|---|
| `--space-1` | `0.25rem` | `0.25rem` |
| `--space-2` | `0.5rem` | `0.375rem` |
| `--space-3` | `0.75rem` | `0.5rem` |
| `--space-4` | `1rem` | `0.75rem` |
| `--space-5` | `1.5rem` | `1rem` |
| `--space-6` | `2rem` | `1.5rem` |

`--space-1` holds: it is already a hairline and halving it rounds to nothing.

### The interactive floor — and the one new size token

| Token | Phone | Desktop | Rule |
|---|---|---|---|
| `--tap-target` | `2.75rem` (44px) | **`2.75rem` (44px) — NEVER overridden** | see the list below |
| `--row-target` | **new** — `var(--tap-target)` | `2rem` (32px) | list-row height and controls *embedded in a row* |

**`--tap-target` must stay 2.75rem everywhere, on every viewport and every pointer.** It is a
prohibition, not a token default. It backs:

- every standalone button — `shared/kaff-button/kaff-button.css:8`
- every form control — the `min-block-size: var(--tap-target)` in all `*-form-page.css`
  (`client-form-page.css:78,106`, `user-form-page.css:78,108`, `bab-form-page.css:34,92,127`,
  `catalogue-form-page.css:33,78`, `supplier-form-page.css:39,97,120,158,170`,
  `subcontractor-form-page.css:39,101,136,174,186`, `employee-form-page.css:34,108`,
  `change-password-page.css:110`)
- the segmented filter — `shared/kaff-segmented-filter/kaff-segmented-filter.css:15`
- the detail-panel close — `shared/kaff-detail-panel/kaff-detail-panel.css:36-37`
- inline edit — `shared/kaff-inline-edit/kaff-inline-edit.css:11,27`
- the empty/loading state control — `shared/kaff-state/kaff-state.css:16`
- the nav drawer furniture — `app/app.css:51-52` (`.nav-toggle`), `:78` (`.sign-out`), `:195`
  (`.nav-item`), once those four literals become the token
- **every control on every M1 screen** — `worker-register-page.css:31`, `worker-pool-page.css:20,91`,
  `worker-history-page.css`, and the daily log when it is built. These are the building-site screens
  from `screen-inventory.md:134-135` and `:67`. Even on a wide monitor with a mouse they should not
  compact; an engineer demonstrating the log to a foreman is the exact user we cannot shrink for.
  **This is the one place where `(pointer: fine)` is not protection enough** — see Q-DENSITY-2.

**`--row-target` replaces `--tap-target` in exactly two kinds of place**, and nowhere else:

1. `shared/kaff-table-row/kaff-table-row.css:12` — the list row itself.
2. In-row icon controls: `shared/kaff-row-menu/kaff-row-menu.css:12-13` (the `⋮` trigger). A 44px
   trigger inside a 32px row makes the row height unachievable, so this one must move or the row
   cannot compact. At 32px with a mouse it is AA-conformant under SC 2.5.8 (24px) and gives up only
   AAA SC 2.5.5. **Cost, named:** we trade AAA for density on mouse-only row menus. On touch it stays
   44px, because `--row-target` resolves to `var(--tap-target)` outside the query.

Resulting desktop list row: 32px minimum, `--space-2` (6px) block padding — i.e. **Fiori compact's
2rem and Fluent's 32px, landing between Carbon sm (32px) and md (40px)**. Not Carbon xs (24px): 24px
cannot hold a 13px Arabic glyph with its descenders and a 1px border.

### Chrome

| Token / rule | Phone | Desktop |
|---|---|---|
| `.side-nav` `inline-size` (`app/app.css:117`) | `15.5rem` drawer | **`13.5rem`** (216px) expanded — between Ant's 200px and today's 248px |
| `--nav-rail-size` | **new** — n/a (drawer instead) | **`3.5rem`** (56px) collapsed rail |
| `.app-header` block padding (`app/app.css:27`) | `--space-3` | inherits the `--space-3` override → 8px |
| `.nav-item` font-size | inherits 16px today | **`var(--text-sm)`** (13px). It has no `font-size` at all today (`app/app.css:184-196`), so it inherits body 16px at weight 600 — a visible part of "everything is very big" |

56px rail: an 18px icon (`app.html:50-51`) centred with 19px each side. Material's 80dp rail is a
*touch* figure; 56px is the mouse figure and still clears SC 2.5.8 by more than double.

---

## 5. Upgrade path, if the breakpoint is not enough

If Nabil wants to choose rather than be decided for, the same token block moves from a media query to
an attribute selector on `:root` — `:root[data-density="compact"]` — set from a signal in `App`, with
the media query as the default when nothing is stored. Zero change to the token values in §4, zero
change to any component. That is why the mechanism is a token override and not per-component inputs:
the cheap version and the full version are the same CSS block behind a different selector.

Do not build it for the demo unless asked (Q-DENSITY-4).

---

## 6. The collapsible sidebar

### Geometry

| State | `inline-size` | Contents |
|---|---|---|
| Expanded (default) | `13.5rem` (216px) desktop / `15.5rem` drawer on phone | as today: brand row, group labels, icon + label rows, account block |
| Collapsed rail | `3.5rem` (56px) | brand mark only (`app/app.css:135-149`, the 2rem tile — the `.brand-text` word and tagline hide); icons only; group **labels** hidden; account block reduced to the 2.125rem avatar (`app/app.css:229-241`) with the name/role hidden; sign-out becomes an icon button |
| Phone (< 641px) | unchanged | the drawer at `app/app.css:291-307` wins. **The rail rule must live inside `@media (min-width: 641px)`** so a collapsed-then-narrowed window cannot produce a 56px rail where the drawer belongs |

### What survives collapse — and the accessibility trap in the current markup

`app.html:48-58` renders the nav icon with `aria-hidden="true"`, so **the nav item's entire accessible
name is the label text**. Hiding the label with `display: none` on the rail would leave every
navigation link nameless to a screen reader.

Required: keep the label in the DOM and clip it visually — `position: absolute; inline-size: 1px;
block-size: 1px; overflow: hidden; clip-path: inset(50%)` — or bind `[attr.aria-label]` on the anchor.
Clipping is preferable: one class, no second source of truth for the label, no extra i18n key.

Sighted discoverability: `[attr.title]="i18n.t(row.labelKey)"` on the anchor while collapsed. Native
tooltip, no popup component, no dependency, no positioning code, and it is not the accessible name —
the clipped label is. `--nav-rail-size` also makes `nav-group-label` (`app/app.css:175-182`) a
structural problem: a group heading has no icon, so on the rail it collapses to a 1px border between
groups instead of text.

### The toggle

- **Where:** in the sidebar's own brand row (`app.html`'s `.side-nav-brand`, `app/app.css:128-133`), at
  its inline-end. It travels with the thing it controls, and it is the only element that stays in the
  same place in both states. It must **not** go in `.app-header` beside `.nav-toggle`
  (`app.html:129-137`) — two hamburger-ish buttons in one header, one for the phone drawer and one for
  the desktop rail, is the ambiguity that gets clicked wrong.
- **Markup:** a real `<button type="button">`, `min-block-size` / `min-inline-size:
  var(--tap-target)`, carrying `[attr.aria-expanded]="!navCollapsed()"`, `aria-controls` pointing at
  the `<nav>`'s `id` (the element at `app.html:28` has a `data-testid` but no `id` — one must be
  added), and `[attr.aria-label]` switching between two new i18n keys, `shell.nav.collapse` /
  `shell.nav.expand`, in **both** `ar.json` and `en.json` (`ux/rtl-and-i18n.md` §6 hard rule 2).
  Mirrors the existing pattern at `app.html:131-135` and `shared/kaff-row-menu/kaff-row-menu.html:6`.
- **Keyboard:** a `<button>` needs no key handling — Enter and Space are native. No new shortcut, no
  focus trap: the rail keeps every nav anchor focusable and in the same DOM order, so Tab behaves
  identically in both states. `ux/rtl-and-i18n.md` §1's arrow-key rule does not apply; this is not a
  horizontal widget.
- **Icon direction:** if the toggle uses a chevron, it is a *collapse/expand along the reading
  direction*, which `ux/rtl-and-i18n.md` §5 puts in the **flips-in-RTL** list — so it must use the
  documented `.icon-directional:dir(rtl) { transform: scaleX(-1) }` mechanism, not a second SVG. A
  non-directional glyph (`☰`, or a panel icon) avoids the question entirely and is the lazier choice.

### Persistence

A signal beside the existing drawer state in `app.ts:63-64` (`private readonly navOpen = signal(false)`
/ `isNavOpen`), initialised from `localStorage` and written on toggle — exactly the shape
`I18nService` already uses for the locale (`core/i18n/i18n.service.ts:82` writes, `:195` reads,
under one `LOCALE_STORAGE_KEY` constant). Proposed key: `kaff.nav.collapsed`.

**Why `localStorage` is allowed here:** D-050's prohibition is on the *session* —
`core/auth/auth.service.ts:105-106` records that the token used to live in `localStorage` "where any
injected script could read it", and `features/users/user-form/user-form-page.ts:128` repeats the ban
for a generated password. A chrome preference is neither a credential nor personal data, and the
locale precedent is already in the tree. Per-device, not per-user, and therefore no API, no
migration, no user-preferences table. If Nabil wants it to follow a user between machines, that is a
server-side preference and a different story (Q-DENSITY-3).

### RTL

Logical properties only, per `ux/rtl-and-i18n.md` §1. The rail is an `inline-size` change on an
element already inside a `display: flex` row (`app/app.css:7-12`), so **nothing needs to move** — the
sidebar sits at the inline-start in both directions with no rule mentioning a side. Animate
`inline-size`, or do not animate at all; never `transform: translateX`, which §1 lists as
non-logical and which the drawer at `app/app.css:296-307` already correctly avoids in favour of
`inset-inline-start`.

---

## 7. Questions — for Nabil or Karim. Not decided here.

- **Q-DENSITY-1 — Is the desktop breakpoint 64rem (1024px) the right cut-over?** A 1024px-wide iPad
  in landscape is exactly on it, and `(pointer: coarse)` is what saves it. If Kaff staff use tablets
  with keyboards and trackpad cases, the pointer test may report `fine` and they would get the dense
  layout on a screen they also touch. **Who uses what hardware in the office is a fact I do not have.**
- **Q-DENSITY-2 — Do the M1 field screens stay at 44px even on a desktop browser?** §4 asserts yes,
  because an engineer may demo the daily log on a laptop and then use it on a phone an hour later.
  But `(min-width: 64rem) and (pointer: fine)` on `:root` cannot know which route is rendered, so
  honouring this needs one extra rule — the M1 feature stylesheets re-declaring
  `--row-target: var(--tap-target)` on their own page root. That is a real decision about whether
  density is global (Fiori's rule: one density per hierarchy) or per-screen. **Ask before building it.**
- **Q-DENSITY-3 — Is the collapsed/expanded sidebar a per-device preference or a per-user setting?**
  §6 proposes per-device `localStorage`, which needs no API. Per-user needs a server preference.
- **Q-DENSITY-4 — Does Nabil want a density *control* (Fiori/Gmail style), or just the right default?**
  The verbatim complaint asks for smaller and for a collapsible menu; it does not ask for a switch.
  §5 keeps the switch cheap to add later.
- **Q-DENSITY-5 — Does the rail default to collapsed or expanded on a first visit?** Fiori and Ant
  default to expanded. Collapsed-by-default shows more data but hides the product's structure from a
  new user on the demo's first screen.

---

## 8. Adjacent defects found while measuring. Reported, not fixed — outside this angle.

1. **`app/app.css:205` — `box-shadow: inset 2px 0 0 var(--color-brand)` on `.nav-item.is-active` is a
   physical offset.** `box-shadow` has no logical form (`ux/rtl-and-i18n.md` §1 names it), so the
   active-row marker sits on the **left** edge of the row in Arabic, where the row's inline-start is
   the right. `border-inline-start: 2px solid` (with a matching transparent border on inactive rows so
   the label does not shift) is the logical equivalent.
2. **`app/app.css:180` — `letter-spacing: 0.06em` on `.nav-group-label`, which renders Arabic**
   (`app.html:40`, `i18n.t(group.labelKey)`). `ux/rtl-and-i18n.md` §8 prohibits `letter-spacing` on
   Arabic text: it is "meaningless-to-harmful for Arabic script" and breaks the join.
3. **`app/app.css:178` — `font-size: 0.6875rem` (11px), off the token scale and below every floor in
   §2.** Also `app/app.css:165` and `:257` (`0.75rem` hardcoded rather than `var(--text-xs)`).
4. **Four hardcoded `2.75rem` literals** — `app/app.css:51`, `:52`, `:78`, `:195` — where the other 63
   occurrences in the app use `var(--tap-target)`. They silently opt out of any future change to the
   floor, in either direction.
5. **`shared/kaff-table-row/kaff-table-row.css:35` and `:48` use `::ng-deep`** with a comment
   explaining that projected content keeps the caller's scope. Noted because this document's brief
   forbids `::ng-deep`, and the §3 mechanism deliberately needs none — custom property inheritance
   crosses the same boundary without it (`kaff-table.ts:27`).

---

## 9. Sources

- SAP Fiori content density (cozy/compact/condensed): https://www.sap.com/design-system/fiori-design-web/v1-145/foundations/visual/cozy-compact
- SAPUI5 content densities (class names, device targets, one-density-per-hierarchy rule): https://github.com/SAP-docs/sapui5/blob/main/docs/04_Essentials/content-densities-e54f729.md
- SAP condensed table layout: https://help.sap.com/docs/ABAP_PLATFORM_NEW/468a97775123488ab3345a0c48cadd8f/f3cc057e405c4fd58ee2ed42c557797c.html
- IBM Carbon data table style (row heights, paired toolbars): https://carbondesignsystem.com/components/data-table/style/
- Carbon v10 data table usage (compact/short/normal/tall): https://v10.carbondesignsystem.com/components/data-table/usage/
- Carbon pagination/row-height issue (the 24/32/40/48/64 set): https://github.com/carbon-design-system/carbon/issues/8874
- Ant Design Table (cell padding per size, 36px virtual rows): https://ant.design/components/table/
- Ant Design Layout / Sider (200px, collapsedWidth 80): https://ant.design/components/layout/
- Ant Design Sider source (defaults): https://github.com/ant-design/ant-design/blob/master/components/layout/Sider.tsx
- Fluent 2 layout (4px base unit): https://fluent2.microsoft.design/layout
- Fluent UI DataGrid row heights (32px medium): https://github.com/microsoft/fluentui/issues/34495
- Fluent Blazor DataGrid (ItemSize 32px): https://fluentui-blazor.azurewebsites.net/datagrid
- Material 3 navigation rail (80dp): https://m3.material.io/components/navigation-rail/guidelines
- Material 3 navigation drawer (360dp): https://m3.material.io/components/navigation-drawer/overview
- UAE Design System 2.0 typography (16px recommended minimum, 1.5 body leading, 12px smallest step): https://designsystem.gov.ae/guidelines/typography
- Dubai Design System typography (never below 14px body): https://designsystem.dubai.ae/foundations/typography
- W3C Arabic & Persian Layout Requirements §7.4 (ascenders/descenders): https://www.w3.org/TR/alreq/
- Arabic design system practitioner guidance (160%/180% leading, 12px→14px correction — weak authority, cited for its numbers): https://sunsetbisma.com/arabic-designsystem
- WCAG 2.2 SC 1.4.12 Text Spacing (AA; override-survival, not authoring requirement): https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html
- WCAG 2.2 SC 2.5.8 Target Size (Minimum) — 24px AA, vs 2.5.5 Enhanced 44px AAA: https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- MDN `pointer` media feature (primary device, coarse on touch laptops, vs `any-pointer`): https://developer.mozilla.org/en-US/docs/Web/CSS/@media/pointer
- Oracle Redwood — checked, no fetchable specs, nothing cited: https://redwood.oracle.com/
