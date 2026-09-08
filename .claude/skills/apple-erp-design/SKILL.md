---
name: apple-erp-design
description: Use when building, restyling, or reviewing any Kaff ERP screen — Apple-HIG visual rigor at ERP data density, in Arabic RTL. Covers typography and bidi isolation, the token vocabulary, surfaces and elevation, table and list density, filter controls, and mobile field screens. Invoke for any work touching src/Web CSS or templates.
---

# Apple-grade design for Kaff ERP

Nabil's brief, translated into what this codebase actually is.

**Read `ux/components.md` first — it is the authority on component shape** (Angular 22 idiom,
prohibitions, tap targets). This skill governs how a screen *looks*; that file governs how it is
*built*. Where they overlap, `ux/components.md` wins.

---

## §0 · The one translation you must make

**Nabil's brief is written in Tailwind class names. This project has no Tailwind and is not
getting one.** `src/Web/package.json` has seven runtime dependencies, all `@angular/*` plus
`rxjs` and `tslib`. `CLAUDE.md`: *"Do not add a package that duplicates something the framework
already does."* Plain CSS with custom properties already does this.

Read the brief's classes as a **specification of values**, then spell them in the token
vocabulary:

| Brief says | Here it is |
|---|---|
| `bg-slate-50` / `bg-white` | `var(--color-surface)` / `var(--color-background)` |
| `border-slate-200` | `var(--color-border)` |
| emerald success / active | `var(--color-accent)` |
| `px-6 py-4` | `padding: var(--space-3) var(--space-5)` |
| `rounded-2xl` (cards) | `var(--radius-lg)` |
| `rounded-xl` (inputs) | `var(--radius-md)` |
| `shadow-xs` / `shadow-sm` | `var(--elevation-1)` / `var(--elevation-2)` |
| `ms-*` / `me-*` | `margin-inline-start` / `margin-inline-end` |

**If a token you need does not exist, add it to `:root` in `src/Web/src/styles.css` — do not
inline the value.** That is the whole mechanism by which dark mode works here, and §2 is about
what happens when a screen skips it.

---

## §1 · Typography and bidi

**Banned:** Inter, Roboto, Arial, Space Grotesk, or any generic web-default sans. They read as
AI slop and none of them has a serious Arabic face.

**Tahoma is on the same list and currently ships** in `--font-body`. It is the Arial of Arabic —
present on every Windows box, chosen by nobody. Keep it as the last fallback if you must; do not
let it be the resolving face.

⚠️ **Adding a webfont is a dependency and a decision.** A Google Fonts link is a third-party
request on every page load of a production system with real money in it, and an offline field
screen that waits on it renders in the fallback anyway. **Do not add one silently.** If you
believe the app needs IBM Plex Sans Arabic or Cairo, write it into `decisions.md` with the
hosting answer (self-hosted `woff2` under the app's own assets, never a CDN) and say so in your
report.

**Bidi isolation — this codebase is already ahead of the brief.** Read `ux/rtl-and-i18n.md`
before touching anything with a Latin run in it. What ships and must not regress:

- Every dynamic identifier, code, phone number and money figure is wrapped in `<bdi>`.
- `<bdi>` resolves its own direction by **first-strong**. A run with no strong character —
  `0100-123-4567` — resolves **`ltr`, not the parent**. Do not repeat the claim that it inherits
  the parent direction; it was measured false (D-126).
- A `<bdi>` that is a **direct child of a grid container** stretches to the full column, and its
  LTR content then lands at the physical left of a right-aligned card. `justify-self: start` on
  `.row-link > bdi` is the fix and is load-bearing — read the comment in `client-list-page.css`
  before touching it. It moves the box, not the text inside it.
- `.figure` (`tabular-nums` + `unicode-bidi: isolate` + `text-align: end`) is what makes a money
  column's decimal points line up. Use it on every numeric cell.
- Money formatted through `Intl` with `style: 'currency'` arrives **already carrying direction
  marks on its leading character**. A currency amount inside a `<bdi>` needs an explicit
  `dir="ltr"`.
- Which digits appear is decided by the `Intl` numbering system, not by CSS. The locale is pinned
  to `ar-EG-u-nu-latn` in `i18n.service.ts`. `font-variant-numeric: lining-nums` does not force
  Western digits and was removed for saying it did (D-036).

**Direction:** `dir="rtl"` is set once, at the document. Do not re-declare it per container —
that is what breaks a nested `<bdi>`. Logical properties only: there is no `left` or `right` in
this codebase, and adding one breaks Arabic silently rather than loudly.

---

## §2 · The defect this skill exists to stop — ✅ **fixed 2026-09-09, `b9246b2`**

> **This section described a live defect when the skill was written and no longer does.** All 45
> literals were converted and the count is 0 across all eleven stylesheets. It is kept because the
> *rule* it produced is permanent and the reasoning is why. **Do not re-open it as work.**

`:root` in `styles.css` defines a **complete dark palette** under
`@media (prefers-color-scheme: dark)`. Six of the ten feature stylesheets read **zero or one**
token and hardcoded `rgb(0 0 0 / 25%)` borders instead:

```
                          colour literals   tokens
  user-form-page.css            12             0
  client-form-page.css          10             0
  audit-trail-page.css           9             1
  client-list-page.css           8             0
  user-list-page.css             4             0
  sign-in-page.css               0             0
```

A `rgb(0 0 0 / 25%)` border on a `#16181a` background is **invisible**. Dark mode is declared and
does not work on the screens that matter. Nothing catches it, because every automated gate is
green: the strings are right, the direction is right, the overflow is 0px.

**The rule: a colour literal in a feature stylesheet is a defect.** Every colour comes from a
token. If you are restyling a screen, converting its literals is not optional polish — it is the
bug you were sent to fix.

---

## §3 · Surfaces, depth and density

**Apple HIG for the chrome; ERP density for the data.** A consumer app's whitespace applied to a
600-row price list produces a screen an operator scrolls for a minute to read what should have
fitted once.

- **Depth comes from a 1px border and an ultra-subtle shadow, never a heavy drop shadow.**
  Two elevations is the whole scale. A card is `--elevation-1`; something floating over the page
  (a menu, a dialog) is `--elevation-2`. There is no third.
- **Radii:** `--radius-lg` on cards and containers, `--radius-md` on inputs and buttons,
  `--radius-sm` on inline badges. Pills (`999px`) are for filter chips only.
- **Rows:** compact padding, one strong column, quiet metadata. Adjacent rows separate by a
  hairline border, not by a gap plus a full bordered box each — the current list screens draw a
  card per row, which is what makes forty clients feel like forty documents.
- **Colour is semantic, never decorative.** `--color-accent` means active or succeeded;
  `--color-danger` means refused. **Zero purple gradients. Zero timid primary-blue themes.**
  An archived row is `--color-muted` and reduced opacity, not a second hue.
- **Hierarchy is weight and size before it is colour.** One `600` per row.

---

## §4 · Controls

- **Filters are a segmented control**, not three loose buttons: one bordered track, the active
  segment filled. Three states where the domain has three (Active / Archived / All) — the
  existing `.chip` markup with `aria-pressed` is correct and needs only the track around it.
- **Every interactive element is at least 44px** — `var(--tap-target)`, specified in
  `ux/components.md` and declared in `styles.css` since `b9246b2`. That includes a list row acting
  as a link, a filter chip, and a checkbox **label** — the label is the target, not the 16px box.
- **Inputs** get `--radius-md`, a `--color-border` hairline, and a `--color-accent` ring on
  `:focus-visible` — never a removed outline.
- **Numeric fields carry the right keyboard.** A phone field is `type="tel"` with
  `inputmode="tel"`; a quantity is `inputmode="decimal"`. On a building site this is the
  difference between a working screen and a broken one.

---

## §5 · Mobile and field screens

390px is the width that must work — the daily log is designed for a phone held in one hand on a
site.

- **Zero horizontal overflow at 390px.** Measure it; do not assume it.
- **One-handed reach:** the primary action sits at the bottom of the viewport on narrow widths,
  full-bleed. It is already `align-self: stretch` on the list screens; keep it.
- A table that cannot fit becomes **stacked rows**, not a horizontal scroller — except a genuine
  ledger, which scrolls inside its own `overflow-x: auto` container while the page body never
  does.

---

## §6 · The invariants a restyle must not touch

Visual work sits on top of rules that are not visual. **A stylesheet cannot violate these, but a
template can, and a restyle edits templates.**

- **No hardcoded user-facing string.** Everything through `i18n.t('key')`, and a new key must
  exist in **both** catalogues or `TranslationCatalogueTests` goes red.
- **Kaff's status vocabulary is verbatim**, never translated and never abbreviated to fit a chip:
  لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها.
- **Permission is server-side.** Hiding a control is presentation. Never introduce a UI-only gate,
  and never remove a server one because the button is hidden now.
- **Internal metrics stay segregated.** `costPrice` is not rendered to a role that may not see it,
  and "not rendered" means the field is absent from the response — not that CSS hides it.
- **`data-testid` attributes are contracts.** E2E and QA cases cite them by name. Renaming one
  breaks a test that is asserting a real thing; if a restyle must move an element, its id moves
  with it unchanged.

---

## §7 · Interface rules borrowed from the Web Interface Guidelines

Taken from `vercel-labs/web-interface-guidelines` on 2026-09-09 and filtered to what applies to an
Arabic-RTL Angular ERP. **The wrapper skill was not kept** — it fetched its ruleset from a URL at
review time, and a rule that can change under you without a commit is not a rule this project can
cite. What survived the filter is written out here instead. What was dropped, and why, is §7.8.

### 7.1 Accessibility — the largest gap in the current screens

- **Icon-only buttons need an `aria-label`.** Decorative icons need `aria-hidden="true"`.
- **Semantic HTML before ARIA.** `<button>` for an action, `<a>`/`routerLink` for navigation. A
  `<div>` with a click handler is a defect: it is not focusable, not keyboard-operable, and not
  announced.
- **Async updates need `aria-live="polite"`** — a loading state, a validation message, a saved
  confirmation. A refusal that only appears visually is invisible to a screen reader. The existing
  `role="alert"` on the refusal paragraphs is correct; the loading and empty states have nothing.
- **Headings are hierarchical `<h1>`–`<h6>`**, and every screen has exactly one `<h1>`. Include a
  skip link to main content.
- **Every form control has a `<label>` or an `aria-label`.** A placeholder is not a label.

### 7.2 Focus

- **Never `outline: none` without a replacement.** The global `:focus-visible` rule in `styles.css`
  is the replacement; do not override it away in a feature stylesheet.
- Use `:focus-visible`, not `:focus` — a ring on mouse click is noise.
- `:focus-within` for compound controls, so a group shows focus when a child holds it.
- **A sticky header or bottom action bar must not cover the focused element.** The primary action is
  bottom-anchored on narrow widths (§5); check that tabbing to a field behind it still scrolls it
  into view.

### 7.3 Forms

- **Never block paste.** Not on the password field, not on a code field, not anywhere. Password
  managers paste, and site engineers paste.
- Inputs carry `autocomplete` and a meaningful `name`.
- **Disable spellcheck on codes, usernames and identifiers** — `spellcheck="false"`. A red squiggle
  under a client code is noise, and on an Arabic keyboard it is worse.
- The label and its control share **one** hit target — no dead zone between a checkbox and its text.
- **The submit button stays enabled until the request starts**, then shows a spinner. Disabling it
  on invalid input hides *which* field is wrong.
- **Errors appear inline, next to the field**, and submitting focuses the first error.
- **Warn before navigating away from unsaved changes** — a half-typed extract lost to a stray back
  gesture on a phone is a real loss on a building site.

### 7.4 Content that does not fit

- **Text containers must handle long content** — `text-overflow: ellipsis` with `overflow: hidden`,
  or `overflow-wrap: break-word`. An Arabic company name is routinely longer than its English
  equivalent.
- ⚠️ **A flex or grid child needs `min-inline-size: 0` before it will truncate.** The initial
  `min-width: auto` refuses to shrink below content, so the row overflows instead of ellipsing. This
  is the single most common cause of the horizontal overflow §5 forbids.
- Design against **short, average and very long** input, not the demo value.

### 7.5 Long lists

- **Over ~50 rows, do not render them all.** `content-visibility: auto` with
  `contain-intrinsic-size` is the no-dependency option and is the one to reach for first. This is not
  hypothetical: the price list in slice 2 is a 600-row screen.
- **No layout reads during render** — `getBoundingClientRect`, `offsetHeight`, `scrollTop`. Batch
  reads and writes; never interleave them.

### 7.6 State belongs in the URL

- **Filters, search text, pagination and expanded panels belong in query params.** Today the client
  list's filter and search live only in signals: the state cannot be linked, bookmarked, or restored
  by a refresh, and an operator who taps a row and comes back has lost their filter.
- Navigation uses `<a>`/`routerLink`, never a click handler calling `router.navigate` — that breaks
  Ctrl-click, middle-click, and "open in new tab".
- **A destructive action needs a confirmation or an undo window, never immediate execution.** Note
  what this does *not* mean: it is a UI affordance, not a delete path. Postings remain append-only;
  a correction is a new reversing posting.

### 7.7 Platform details that bite

- **Native `<select>` needs an explicit `background-color` and `color`** or it renders unreadable in
  Windows dark mode. Kaff runs on Windows.
- **`<meta name="theme-color">` should match the page background**, per theme.
- `overscroll-behavior: contain` on any modal, drawer or sheet, so scrolling it does not scroll the
  page behind.
- Full-bleed layouts need `env(safe-area-inset-*)` — the bottom-anchored primary action sits under
  the home indicator on a notched phone without it.
- `-webkit-tap-highlight-color` set deliberately rather than left to the browser's grey flash.
- **`translate="no"` on codes, identifiers and brand names.** Auto-translation garbles a client code
  the same way the bidi algorithm does, and this pairs with the `<bdi>` rule in §1 rather than
  replacing it.

### 7.8 Animation and small typography

- **Honour `prefers-reduced-motion`** — provide a reduced variant or none.
- Animate `transform` and `opacity` only. **Never `transition: all`** — list the properties.
- Animations stay interruptible and respond to input mid-flight.
- `…` (one character), never three dots. Loading states end with it.
- `text-wrap: balance` on headings to prevent a one-word last line.

### 7.9 What was deliberately dropped

Named so the next session does not re-import it believing it was missed:

- **Everything React and Next.js** — hydration safety, `suppressHydrationWarning`, `defaultValue`,
  `htmlFor`, the `priority` prop, `nuqs`, `virtua`. This is Angular 22 with signals.
- **Every Tailwind class name.** Translated to CSS per §0, or dropped.
- **The English copy rules** — Title Case headings, Chicago style, active voice, second person,
  "8 deployments" not "eight", `&` over "and". **The UI is Arabic.** Title case does not exist in
  Arabic, and Kaff's status vocabulary is fixed verbatim by `CLAUDE.md` regardless.
- **The whole Vercel deployment, React Native and view-transitions bundle** — 2.2 MB across roughly
  300 files for a stack this project does not have. Kaff deploys from `deploy/`, and the mobile
  stack is MAUI or Flutter, undecided.

---

## §8 · Definition of done for a design change

- [ ] Zero colour literals in the stylesheets you touched; every colour is a token
- [ ] Correct in **both** light and dark — actually rendered, not reasoned about
- [ ] Correct at **390px** with 0px horizontal overflow, and at desktop width
- [ ] Every `<bdi>` still isolates, and no grid-child `<bdi>` stretches
- [ ] Every `data-testid` that existed before still exists
- [ ] No new string outside i18n; no new key missing from either catalogue
- [ ] Build clean, `vitest` green, E2E green — run them through `/run-kaff-erp`, never hand-rolled
- [ ] Any new token is declared in `:root` **and** in the dark block
- [ ] §7.1 checked on any screen you touched: icon buttons labelled, live regions announced, one `<h1>`
- [ ] No flex or grid child truncates without `min-inline-size: 0` (§7.4)
