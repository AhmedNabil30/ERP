# KAFF-922 · Sidebar rebuild to match Main.dc.html

<!-- kaff id=KAFF-922 slice=design-system points=3 state=READY -->

**Slice:** Design system · **Epic:** Apple-grade restyle · **Points:** 3 — layout rebuild, no new
behaviour, no new permission.
**Depends on:** `KAFF-900` (BUILT), `KAFF-901` (BUILT), `KAFF-215` (`navRowsFor`, BUILT).
**Files:** `src/Web/src/app/app.html` (lines ~13-30), `src/Web/src/app/app.css` (lines ~144-198),
`src/Web/src/app/core/navigation/nav-rows.ts`, `src/Web/src/app/app.ts`.
**Reference:** `.design/Main.dc.html` lines 20-91 — read it, it is the contract.

## Story
As any staff user, I want the sidebar to look like the agreed design (brand mark, grouped nav,
icons, account block at the bottom) instead of a flat list of text links, so the shell matches what
Nabil approved.

## Gap, as found (do not re-derive — build it)
Current `.side-nav` is a flat `@for (row of navRows())` of plain `<a>` text links. No brand mark, no
grouping, no icons, no account block — the account block currently lives in the header instead.

Mockup (`Main.dc.html` 20-91), top to bottom:
1. Brand row: 32px orange (`--color-brand`) rounded square holding Latin `K`, beside `KAAF` wordmark
   (`<bdi dir="ltr" translate="no">`) with tagline `مقاولات وتشطيبات` under it in
   `--sidebar-text-muted`.
2. Nav rows grouped under small muted section labels: `البيانات الأساسية` (catalogue, babs,
   employees, subcontractors, suppliers, clients) and `الإدارة` (users, audit).
3. Each row carries an 18px stroked SVG icon, `aria-hidden="true"` (decorative — the row's own text
   is the accessible name).
4. User block pinned to the bottom (`margin-block-start: auto`) above a `--sidebar-border` hairline:
   round avatar with initials, display name, role beneath.

## What to do
1. **`nav-rows.ts`**: add a `group` field to `NavRow` (e.g. `'core' | 'admin'`), one value per
   existing row, matching the mockup's two groups. This is a static field on the existing fixed
   array — it does not touch `navRowsFor`'s permission filter or its order-stability contract
   (the doc comment above `NAV_ROWS` — read it, don't break rule 6). Add one i18n key per group
   label (`nav.group.core`, `nav.group.admin` or your naming — pick one, be consistent) to **both**
   `ar.json` and `en.json`.
2. **`nav-rows.ts`**: add an icon identifier per row (a string key, not inline SVG in the data — keep
   markup in the template). Map it to the mockup's SVG per route: catalogue (stacked lines), babs
   (door/frame), employees (person), subcontractors (building), suppliers (crate), clients
   (invoice), users (people+cog), audit (clock/history). Reuse the exact `<path>` data from
   `Main.dc.html` — do not invent new icon art.
3. **`app.html`**: rewrite the `<nav class="side-nav">` block to render brand row, then
   `@for` over groups (derived from `navRows()` — **grouping must come from the row model, group by
   `row.group`, never a hardcoded template list** — a permission set with no row in a group must
   render no heading for that group either), each row with its icon (`aria-hidden="true"`) and label,
   then the account block.
4. **Move the account block from the header into the sidebar.** Keep `data-testid="account-menu"`,
   `data-testid="account-name"`, `data-testid="sign-out"` unchanged wherever they land. Keep the
   comment at `app.html:63` intact in spirit — sign-out must stay reachable during a forced
   password-change screen (`AC-125-D`), so the account block's visibility condition (`@if (session();
   as session)`) does not change.
5. Avatar initials: derive from `session.displayName` (first letter of first two words, Arabic-safe —
   check what the mockup shows, `أن` for `أحمد نبيل`, i.e. first letter of each of the first two
   words). Role beneath: `session`'s role field — check `Session` interface in `auth.service.ts` for
   the field name, do not invent one.
6. `app.css`: replace `.side-nav-brand`/`.nav-item` rules with the grouped structure — brand row,
   group label, icon+label row, account block, hairline border-block-start using `--sidebar-border`.
   Zero colour literals; every colour a `--sidebar-*` or `--color-*` token already in `:root` from
   KAFF-900. If a token is missing, say so — don't invent one.

## Acceptance criteria
**AC-922-A** Sidebar renders: brand mark + wordmark + tagline, two grouped sections with muted
labels, one 18px `aria-hidden` icon per row, account block pinned to bottom above a hairline.
**AC-922-B** Grouping is data-driven off `row.group` — a session with zero rows in a group renders no
heading for it. Prove it: a role missing `UserManage` and `AuditRead` renders no `الإدارة` heading.
**AC-922-C** `data-testid="account-menu"`/`account-name`/`sign-out` all present, in the sidebar now,
same behaviour including during forced password-change (`AC-125-D` still holds — check
`app.spec.ts`'s existing test for it still passes).
**AC-922-D** `nav-rows.spec.ts`'s existing completeness/order tests still pass unmodified in intent
(update only for the new `group`/`icon` fields, not the permission/order logic).
**AC-922-E** Zero colour literals introduced; screenshot light+dark, desktop+390px — actually
rendered and looked at, not reasoned about.
**AC-922-F** 0px horizontal overflow at 390px. Sidebar becomes the existing drawer at this width
(unchanged mechanism, `isNavOpen()`/`nav-backdrop`) — only its internal content changes.
**AC-922-G** No new string outside i18n; new keys added to both `ar.json` and `en.json`.
**AC-922-H** `ng build` clean, `npm test` green.

## Not in this story
Icon set beyond the 8 rows shown in the mockup. Day-labour nav (KAFF-215 Question 1, still open,
not this story's problem). Top bar, tables — separate stories (`KAFF-923`, `KAFF-924`).

## Questions for Karim
None — this is a layout conversion of an already-approved design, no business rule involved.
