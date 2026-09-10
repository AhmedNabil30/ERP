# Brief — Frontend, the catalogue screens

**Written 2026-09-10 by the Scrum Master.** Branch `main`.

**Model: `sonnet`**, per `agents.md` §M — *"Frontend — implementing a story whose criteria are already
written — **mid**."* The criteria are written. This is not a design exploration; it is three screens
against an API that already exists and is already green.

---

## Why you exist

Yesterday `KAFF-202`, `KAFF-203` and `KAFF-206` were moved to `state=BUILT` on backend gates alone.
An independent Verifier looked this morning and returned **NOT VERIFIED** on all three
(`qa/slice-2/verification-2026-09-10.md`, finding **`V-35-S`, HIGH**):

> **There is no catalogue frontend at all.** No `src/Web/src/app/features/catalogue`, no catalogue
> E2E file. `AC-202-J`, `AC-203-H`, `AC-206-I` and half of `AC-203-D` are **unbuilt, not merely
> untested**, under three trailers all reading `BUILT`.

Nabil's ruling, verbatim, and it is why this is a defect and not a gap: ***"You cannot discharge a UI
rendering dependency with a JSON response."***

**You are building the layer that makes those three stories real.** All nine points are currently
`verdict=REJECTED`.

---

## Read before you write a line

1. `CLAUDE.md` — completely. The Angular conventions section is binding: standalone only, **no
   NgModules**, signals (**no `BehaviorSubject` for component state**), zoneless (**do not add
   Zone.js**), signal forms, `strictTemplates`, `inject()`, `@if`/`@for` (**not** `*ngIf`/`*ngFor`).
2. **`.claude/skills/apple-erp-design/SKILL.md`** — invoke it. It governs how the screen looks and it
   ends in a Definition of Done you will be checked against.
3. **`ux/components.md`** — it governs how the screen is *built*, and **where the two overlap, it
   wins.**
4. `ux/rtl-and-i18n.md` — before touching anything with a Latin run in it. Item codes are Latin runs.
5. `spec.md` §4.1, §4.2, §4.4, §4.5 — the business truth.
6. The three stories in `stories/slice-2-masters/` — `KAFF-202`, `KAFF-203`, `KAFF-206`. Build to the
   `AC-` criteria, not to this brief's summary of them.

**Copy the shape from `src/Web/src/app/features/clients/`** — `client-list/` and `client-form/`, three
files each (`.ts`, `.html`, `.css`). They are the closest sibling, they are accepted, and they are the
house style. Read them before inventing anything.

---

## The API — it exists, it is green, do not change it

| | |
|---|---|
| `GET /api/catalogue-items?search=&status=` | list + search. `status` is `Active` (default) · `Archived` · `All`; **an unknown value is refused, not defaulted** |
| `POST /api/catalogue-items` | create — `Code · DescriptionAr · DescriptionEn? · Unit · BabId · CostPrice · BaseSellRate` |
| `PUT /api/catalogue-items/{id}` | edit — `DescriptionAr · DescriptionEn? · Unit · CostPrice · BaseSellRate`. **No `Code`, no `BabId`** — the code is immutable and moving an item between أبواب is `KAFF-205`, unbuilt |
| `POST /api/catalogue-items/{id}/archive` | archive |
| `POST /api/catalogue-items/{id}/unarchive` | un-archive |
| `GET /api/babs` | ⚠️ **built today by the Backend agent immediately before you.** Flat list — `{ items: [{ id, code, nameAr, nameEn, parentBabId, defaultMarkup, isActive }] }`, ordered by `SortOrder` then `Code`. **Read its `Response.cs` for the real shape rather than trusting this table.** |

The five catalogue endpoints are gated `Permission.CatalogueManage`. **`GET /api/babs` is gated
`Permission.BabManage`** — a correction to this brief's first draft, made by the agent that built it:
أبواب have carried their own permission row since `PermissionCatalogue.cs` was written, and
`KAFF-204` rule 9 names it. Both rows are `CompanyWide` and both grant `[owner, technicalOffice]`, so
in practice the same people reach both — **but they are two permissions, and a client-side guard that
assumes one implies the other is true only by coincidence today and silently wrong the day either row
moves.**

`defaultMarkup` arrives as a **fraction** — `0.15` means 15%, matching the raw-value convention
`ListCatalogueItems` uses for money. Format it for display; do not store the formatted value.

Item summaries carry `Code · DescriptionAr · DescriptionEn? · Unit · BabId · CostPrice ·
BaseSellRate · Status`.

**⛔ Do not touch anything under `src/Api`, `src/Domain`, `src/Infrastructure` or `tests/Api.Tests`.**
If the API is wrong or missing something you need, **stop and report it** — do not work around it in
the client, and do not add a second source of truth.

---

## The three screens

### 1. Catalogue list — `/catalogue` · `KAFF-203` + `KAFF-206`

The price list is **a ~600-row screen**. Design for that, not for the six rows your fixture has.

- Search box over code **and** Arabic description, one field, bound to the `search` param.
- **Three-state filter as a segmented control** — Active · Archived · All. Design skill §4: *one
  bordered track, the active segment filled*, not three loose buttons. The existing `.chip` markup
  with `aria-pressed` is the right base and needs the track around it.
- **Grouped by باب, then ordered by code** — `AC-203-I`. This is why `GET /api/babs` was built: a
  group header cannot render a guid. An item whose `BabId` matches no باب must still appear, not
  vanish.
- **Two empty states, and they are different** — `AC-203-D`: *"nothing matches this search"* is not
  *"the catalogue is empty"*. Both need `aria-live="polite"`, per design skill §7.1.
- Archive / un-archive from the row. **`AC-206-E`: archiving twice is refused** — surface the refusal,
  do not swallow it. An archived row is `--color-muted` and reduced opacity, **never a second hue**.
- **Rows separate by a hairline, not a card each** (design skill §3). Forty items must not read as
  forty documents.
- `content-visibility: auto` + `contain-intrinsic-size` for the long list — §7.5, and it is the
  no-dependency option. **Do not add a virtual-scroll package.**
- **Filter and search go in the URL as query params** — §7.6. `F-127-1` is an open finding precisely
  because the user list put them in signals only; do not repeat it one screen later.

### 2. Catalogue form — `/catalogue/new` and `/catalogue/:id` · `KAFF-202`

- **Create takes a باب; edit does not show one.** `PUT` has no `BabId` field — rendering a picker that
  cannot save is worse than not rendering it.
- **`Code` is immutable on edit.** Show it, do not let it be typed.
- **Prices are `inputmode="decimal"`** (§4). `spellcheck="false"` on `Code` and `Unit` (§7.3).
- ⚠️ **Four decimals.** `AC-202-C`. Finding `V-35-Q` this morning: `Money`'s constructor **already
  rounds away-from-zero above four decimals, system-wide**, and `AC-200-B` — the ruling that decides
  whether that is correct — **is still open with Nabil.** So: **do not add client-side rounding, and
  do not add a client-side "max 4 decimals" validator.** Let the server be the single answer. If you
  find yourself writing a rounding rule in TypeScript, stop — you would be deciding Nabil's open
  question in a place nobody will look for it.
- **A negative price is refused; a loss-making one is not** — `AC-202-D`. Sell below cost is legal.
  Do not add a warning nobody asked for.
- Errors inline next to the field; submitting focuses the first error; **the submit button stays
  enabled until the request starts** (§7.3). **Never block paste.**
- Warn before navigating away from unsaved changes (§7.3).

### 3. Navigation

Add the catalogue to the landing screen and the nav the same way `clients` and `users` are, with a
route guard mirroring `client-manage.guard.ts`. **The guard is convenience, not security** — the
server decides, and it already does.

---

## The invariants — a template can break these, and you are editing templates

- **No hardcoded user-facing string.** Every new key goes in **both** `src/Web/public/locales/ar.json`
  **and** `en.json`. A key in one catalogue only is a red test.
- **`costPrice` is internal.** Every endpoint here is `CatalogueManage`-gated, so a user who reaches
  this screen may see it — but label it as internal and **never** carry it onto a surface a client
  can reach. "Not shown" means absent from the response, never CSS-hidden.
- **`<bdi>` on every code and every money figure.** A `<bdi>` that is a **direct grid child stretches
  to the full column** and lands its LTR content at the physical left — `justify-self: start` is the
  fix and it is load-bearing. Read the comment in `client-list-page.css` before you copy the pattern.
  Use `.figure` on every numeric cell; that is what lines the decimal points up.
- **`translate="no"` on item codes** (§7.7).
- **Zero colour literals.** Every colour is a token from `styles.css`. If a token you need does not
  exist, add it to `:root` **and** to the dark block — never inline the value. Six stylesheets shipped
  with 45 literals and dark mode silently did not work; that is what design skill §2 is about.
- **44px minimum on every interactive element** — `var(--tap-target)`. That includes a list row acting
  as a link and a filter chip.
- **`data-testid` attributes are contracts.** Add them for every element a QA case will need to cite.

---

## Gates — run them, do not reason about them

Through `/run-kaff-erp`, **never hand-rolled**. ⚠️ **The SPA test command is `npm test`** (Angular's
`@angular/build:unit-test` target). `npx vitest run` is **wrong** and produces false failures — that
mistake was made on this repo and reported before it was caught.

- `npm run build` clean under `strictTemplates`
- `npm test` green
- **Rendered at 390px and at desktop, in both light and dark — actually rendered, screenshots taken
  and looked at.** Design skill §8 says *actually rendered, not reasoned about*, and it says that
  because a screenshot was taken on this project and the defect in it was still missed.
- **Zero horizontal overflow at 390px. Measure it.**

**Do not run the .NET suites** — the Scrum Master owns those gates and one actor measures them.

Write frontend unit tests for the behaviour, and **watch each one fail first**: break the thing it
names, confirm red **for the right reason**, revert. ⚠️ Deleting a line outright often fails
`TS6133` and runs **no tests at all**, which reads as green (D-114 §5). Say what you mutated.

Windows. The Bash tool has a broken PATH; prefix with `export PATH="/usr/bin:/bin:/mingw64/bin:$PATH";`
or use the PowerShell tool.

---

## What you must not do

- ⛔ Touch any story `<!-- kaff ... -->` trailer, `STATUS.md`, or `decisions.md`. The Scrum Master
  moves the board.
- ⛔ Add an npm dependency. Seven runtime dependencies is the whole list and `CLAUDE.md` forbids
  duplicating what the framework does. **No Tailwind** — design skill §0 translates the brief's class
  names into this project's tokens.
- ⛔ Add a webfont silently. If you believe one is needed it is a `decisions.md` entry with a
  self-hosted answer, and **you report it rather than adding it.**
- ⛔ Invent a business rule. `spec.md` is the truth; if it does not answer, **stop and ask** — an
  invented rule is always plausible, which is why it survives review.
- ⛔ Enforce a permission in the client alone.
- ⛔ Push. Commit locally, `git commit -F <a message file>`.

---

## Report back

Files added · which `AC-` criterion each screen discharges and which it does not · your build and
`npm test` figures · **the screenshots you took, at 390px and desktop, light and dark, and what you
saw in them** · what you mutated to watch a test fail · every new i18n key and confirmation it is in
both catalogues · every `data-testid` you added · anything you had to leave undone and why.

**Correct this brief in writing where it is wrong.** It was written from a grep and a Verifier's
report, by a session that has not built these screens.
