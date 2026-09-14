# KAFF-926 · User list — beyond what KAFF-925 already gives it

<!-- kaff id=KAFF-926 slice=design-system points=3 state=READY -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 3.
**Depends on:** `KAFF-924` (BUILT, `3b25e40`) for `kaff-table-header`/`kaff-group-heading`. Independent
of `KAFF-922`/`923`/`927` — touches only `features/users/user-list/*`, no shared/app-shell file.
**Files:** `src/Web/src/app/features/users/user-list/user-list-page.ts` + `.html` + `.css`.
**Reference:** `client-list-page.css` (the sibling that already has the row-name overflow fix and no
narrow-column layout), `catalogue-list-page.ts`/`.css` (fixed-px + `minmax(0,1fr)` columns, URL
query-param filter sync).

## Story
Nabil, looking at the running app: the user list table is the worst screen. Findings below are
diagnosed by direct reading of `user-list-page.html`, `.css` and `.ts` — build them, do not
re-investigate.

## Findings and fixes
1. **`user-list-page.ts:49`** `rowColumns = '2fr 1.5fr 1fr 1fr'`. Fractional tracks drift with row
   content — nothing lines up down the page. Match catalogue's shape: fixed px per fixed-width
   column, `minmax(0, 1fr)` for the one column that should flex (name). Pick concrete widths by
   looking at the actual content (username/phone are fixed-width figures, role/department is
   variable-length Arabic text — size accordingly, don't guess a number with no basis).
2. **`.row-name` (css:65)** has no `overflow: hidden; text-overflow: ellipsis; white-space: nowrap`.
   Copy `client-list-page.css:106-113` exactly — same problem, already solved there.
3. **`.users` (css:7-14)** sets `max-inline-size: 60rem; margin-inline: auto` — a narrow centred
   column, while catalogue runs full width. **Pick one page shape for every list screen** — full
   width, matching the mockup. Remove the constraint here. If any other list screen (besides
   catalogue) also centres itself narrow, name it — don't silently leave an inconsistency you found
   but didn't fix, and don't silently fix screens outside this story's file list either; report and
   stop at this screen.
4. **`user-list-page.css:48-52`** — `kaff-table-row .row { grid-template-columns: 1fr !important }`,
   reaching into another component's internals with `!important`, aimed at a component `KAFF-924`
   just rebuilt with a real responsive story. Delete this override. Use `KAFF-924`'s
   `kaff-table-header`/fixed `kaff-table-row` mobile behaviour instead — same integration this
   story's sibling `KAFF-925` does for the other five lists. This screen was not one of `KAFF-925`'s
   six named screens; this story is where the user list gets that treatment plus the fixes below.
5. **`.create` (css:125-135)** — a grey outlined link at the bottom of the page. Every other primary
   action is an orange `kaff-button` in the top bar (mockup: `Main.dc.html` "بند جديد"). Move create
   to the top bar as a `kaff-button` `variant="primary"`, matching the pattern the other converted
   list screens use (check `client-list-page.html`/`catalogue-list-page.html` for the exact
   convention — if `KAFF-923`'s top-bar action slot exists by the time you build this, use it; if
   `KAFF-923` isn't built yet, place it consistent with how `catalogue-list-page` currently places its
   own action links in its `.header-row`, and note that it should move into `KAFF-923`'s slot later).
6. **Role badge on every row** — `<kaff-badge tone="accent">` on 100% of rows carries no signal
   (`apple-erp-design` §3: colour is semantic, never decorative). Change role to plain text in its
   column. Keep `kaff-badge` only for the inactive marker (`users.state.inactive`), which is the
   actual exception being flagged.
7. **No filter** — inactive users render mixed with active ones and `user-row-inactive` already
   exists as a marker, but there's no Active/Archived/All control. Add `kaff-segmented-filter`, same
   component `catalogue`/`client` lists use. **Put the state in a URL query param** (`ux/...` §7.6 —
   check the exact section reference in `ux/rtl-and-i18n.md` or wherever §7.6 lives before citing it
   in your commit) — copy `catalogue-list-page.ts`'s `ActivatedRoute`/`Router` `queryParamsHandling:
   'merge'` pattern (lines ~54-55, ~221-222) exactly, don't reinvent it.
   **Also check `client-list-page.ts`**: it uses `kaff-segmented-filter` but was found to have no
   query-param sync at all (confirmed — no `ActivatedRoute`/`queryParam` in that file). Add the same
   sync there too, as part of this story, since the finding names both screens explicitly. If that
   turns out to be a bigger diff than a story-3 budget comfortably covers, do the user list fully and
   flag the client-list gap as a follow-up rather than leaving it half-done.
8. **Spacing literals** (`1rem`, `1.5rem`, `0.75rem`, `2rem`, `0.35rem` throughout `.css`) — convert
   every one to the nearest `--space-*` token already in `:root`. If no token matches a given value
   exactly, use the nearest and say which literal had no exact match.

## Acceptance criteria
**AC-926-A** `rowColumns` is fixed-px + one `minmax(0, 1fr)`, matches visually down the page (proven
by screenshot, not by reading the string).
**AC-926-B** `.row-name` truncates with ellipsis instead of overflowing — proven with a long test name
if the fixture data allows it, or by review.
**AC-926-C** Page runs full width, no `max-inline-size`/centring.
**AC-926-D** No `!important` override of `kaff-table-row` internals; screen integrates `KAFF-924`'s
header/row mobile behaviour the same way `KAFF-925`'s six screens do.
**AC-926-E** Create action is a `kaff-button variant="primary"`, in the top bar area, not a bottom
outlined link.
**AC-926-F** Role renders as plain text; `kaff-badge` used only for the inactive marker.
**AC-926-G** Active/Archived/All `kaff-segmented-filter` present, state round-trips through a URL
query param (reload the page with `?status=archived` in the URL, confirm the filter reflects it).
**AC-926-H** Zero spacing literals left that have an exact `--space-*` match; report any that don't.
**AC-926-I** Zero colour literals. Every `data-testid` preserved — grep before/after, report both
counts. No new string outside i18n; new keys in both `ar.json`/`en.json`.
**AC-926-J** Screenshotted light+dark, desktop+390px, actually rendered.
**AC-926-K** `ng build` clean, `npm test` green, `BidiGeometryTests` green before commit (D-156).

## Not in this story
`KAFF-923`'s top-bar slot itself (if not yet built, this story places the button reasonably and notes
the follow-up). Any other screen's centred-width or filter gap beyond client-list's query-param sync
named explicitly above.

## Questions for Karim
None — this is a UI consistency fix, no business rule involved.
