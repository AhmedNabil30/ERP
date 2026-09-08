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

## §2 · The defect this skill exists to stop

`:root` in `styles.css` defines a **complete dark palette** under
`@media (prefers-color-scheme: dark)`. Six of the ten feature stylesheets read **zero or one**
token and hardcode `rgb(0 0 0 / 25%)` borders instead:

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
- **Every interactive element is at least 44px** (`--tap-target`, `ux/components.md` — the token
  is specified there and is **not yet declared** in `styles.css`; declare it). That includes a
  list row acting as a link, a filter chip, and a checkbox **label** — the label is the target,
  not the 16px box.
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

## §7 · Definition of done for a design change

- [ ] Zero colour literals in the stylesheets you touched; every colour is a token
- [ ] Correct in **both** light and dark — actually rendered, not reasoned about
- [ ] Correct at **390px** with 0px horizontal overflow, and at desktop width
- [ ] Every `<bdi>` still isolates, and no grid-child `<bdi>` stretches
- [ ] Every `data-testid` that existed before still exists
- [ ] No new string outside i18n; no new key missing from either catalogue
- [ ] Build clean, `vitest` green, E2E green — run them through `/run-kaff-erp`, never hand-rolled
- [ ] Any new token is declared in `:root` **and** in the dark block
