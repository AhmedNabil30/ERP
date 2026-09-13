# KAFF-900 · Design tokens and the app shell (sidebar, header, page frame)

<!-- kaff id=KAFF-900 slice=design-system points=5 state=BUILT -->

**Slice:** Design system (cross-cutting, not slice-numbered) · **Epic:** Apple-grade restyle ·
**Points:** 5 — touches every screen indirectly through shared tokens and the one shell component,
no money, no permission change.
**Source:** Nabil's approved mockups, `D:\ERP\.design\Main.dc.html`, `CatalogueForm.dc.html`,
`BabTree.dc.html`, `WorkerMobile.dc.html` (visual contract) · `.claude/skills/apple-erp-design/SKILL.md`
(rules) · `ux/components.md` (component shape) — this is a design-system directive, not a business
rule, so it cites the design artefacts rather than `spec.md`.
**Depends on:** nothing. This is the first story of the restyle; every other design-system story
depends on it.

## Story
As any staff user, I want the app's chrome (sidebar, header, page frame) and its colour/spacing
tokens to match the approved Kaff brand design, so the product looks intentional instead of default
Angular/Bootstrap grey.

## What ships

1. **New tokens in `src/Web/src/styles.css`, extending `:root` — never replacing the existing scale.**
   - `--color-brand: #d76833` — the mark colour. Used only for the logo mark and any place the brief
     shows the raw orange (e.g. the badge dot on a margin chip). **Never on text or an interactive
     control** — `#d76833` on white measures ≈3.0:1, below the 4.5:1 text floor `apple-erp-design`
     §Baseline requires.
   - `--color-interactive: #b4551f` — replaces `--color-accent`'s role for buttons, links and active
     nav rows. **Do not delete `--color-accent`** if anything still reads it for a different meaning
     (success/active semantic per §3) — check every call site before touching the token name itself;
     if `--color-accent` and `--color-interactive` should be the same value, that is a decision to
     record in `decisions.md`, not a silent merge.
   - `--color-interactive-hover: #8f3f19` (from the mockups' `a:hover`).
   - Dark-mode block: both tokens get a value verified 4.5:1 against `--color-background`'s dark
     value (`#16181a`) — the mockups do not show a dark shell for the *page*, only for the sidebar
     (see point 2), so the interactive colour still needs its own dark-mode legibility check.
   - `--sidebar-*` tokens for the dark sidebar surface, so it never depends on `prefers-color-scheme`:
     `--sidebar-background: #16181a`, `--sidebar-text: #ececec`, `--sidebar-text-muted: #8b9490`,
     `--sidebar-border: #34383c`, `--sidebar-active-bg: rgb(215 104 51 / 16%)`. These are **not** the
     same as `--color-background`/`--color-text` — the sidebar stays dark in both themes because it is
     brand chrome, not page surface (mockup `Main.dc.html`, the `#16181a` side-nav column).

2. **The shell (`app.html`/`app.css`) restyled to the `Main.dc.html` layout:**
   - Two-column grid: dark sidebar (fixed width, ~248px desktop) + main column. Below the breakpoint
     where `ux/components.md`'s mobile card pattern applies, the sidebar collapses to the existing
     drawer/hamburger behaviour already wired in `app.ts` (`isNavOpen`, `toggleNav`) — **do not touch
     that signal logic**, only its visual presentation.
   - Sidebar top: the wordmark. `<bdi dir="ltr" translate="no">KAAF</bdi>` exactly, per Nabil's ruling
     that كف does not read as a name — never render the Arabic transliteration as the primary mark.
   - Sidebar nav rows: iterate the existing `navRows` signal (`app.ts` -> `navRows`) unchanged — this
     story is visual only, it adds no route and reorders nothing. Active row gets
     `background: var(--sidebar-active-bg)` and the `box-shadow: inset` accent bar from the mockup;
     inactive rows get `--sidebar-text-muted`.
   - Header: page title left where `app.html`'s `<h1>` (or route-level `<h1>`, check current markup)
     already renders; do not introduce a second `<h1>` per page — `apple-erp-design` §7.1 requires
     exactly one.
   - Page frame: the content area gets the token-driven background/padding so every screen inherits it
     without a per-screen override.

3. **Font.** Keep the system stack (`--font-body` unchanged). Do **not** add the Google Fonts link the
   mockups load — `apple-erp-design` §1 forbids a CDN webfont, and self-hosting IBM Plex Sans Arabic is
   out of scope for this story (no `woff2` asset exists yet, no `decisions.md` entry authorizes the
   dependency). If a future story self-hosts it, that story writes the `decisions.md` entry per the
   skill; this one does not.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | Every colour is a token; zero colour literals in any stylesheet touched | `apple-erp-design` §2, §8 |
| 2 | The sidebar is dark in both light and dark theme | Nabil's brief, `Main.dc.html` |
| 3 | Wordmark is Latin "KAAF" in `<bdi dir="ltr" translate="no">`, even in Arabic | Nabil's ruling, 2026-09-13 |
| 4 | `#d76833` never appears on text or a control — only as the mark/decorative colour | contrast measured ≈3.0:1, below the 4.5:1 floor (`apple-erp-design` Baseline requirements) |
| 5 | No CDN webfont | `apple-erp-design` §1 |
| 6 | Permission-driven nav rows are unchanged — this story is visual only | `CLAUDE.md` server-side permission rule; do not touch `nav-rows.ts` |

## Acceptance criteria

**AC-900-A — tokens exist and are used**
Given `src/Web/src/styles.css`
When the design-system tokens are added
Then `--color-brand`, `--color-interactive`, `--color-interactive-hover` and the `--sidebar-*` tokens
exist in `:root`, each with a dark-mode counterpart where the value differs, and no stylesheet under
`src/Web/src/app/` contains a hex or `rgb()` colour literal that duplicates one of these values.

**AC-900-B — sidebar renders dark in both themes**
Given the app is rendered with the OS in light mode, then in dark mode
When the shell mounts for a signed-in staff session
Then the sidebar's background, text and border colours are visually identical in both — screenshotted
via `driver.mjs shot` in both `prefers-color-scheme` states.

**AC-900-C — wordmark**
Given the sidebar renders
When the wordmark is inspected
Then it is `<bdi dir="ltr" translate="no">KAAF</bdi>`, not كف and not a plain `<span>`.

**AC-900-D — no regression on existing behaviour**
Given `showStaffNav`, `navRows`, `toggleNav`, `signOut` in `app.ts`
When the shell is restyled
Then none of these signals or methods change, and every existing `data-testid` in `app.html` keeps its
name.

**AC-900-E — mobile, 0px overflow**
Given the shell at 390px width
When any signed-in screen renders
Then the page body has 0px horizontal overflow, measured, not assumed (`apple-erp-design` §5, §7.4).

**AC-900-F — contrast**
Given `--color-interactive` on `--color-background`, in both light and dark
When measured with a contrast checker
Then the ratio is at least 4.5:1 for text use and 3:1 for a control boundary.

## Not in this story
The shared components (table row, segmented filter, form field, button, badge) — `KAFF-901`. Any
per-screen conversion — the screen stories that follow `KAFF-901`. Self-hosting a webfont — open,
not scheduled.

## Questions for Karim
None — this is a visual/technical story, not a business rule.
