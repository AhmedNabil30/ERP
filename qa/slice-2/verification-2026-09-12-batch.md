# Verification — slice 2, second batch (2026-09-12)

**Verifier:** fresh session, `opus`. Wrote none of this code.
**Scope:** `7117704..35b0550` — `KAFF-200`, `201`, `207`, `208`, `209`, `210`, `211`, `212`.
**Rulings in force:** D-135, D-136, D-139, D-140, D-141, D-142, D-144, D-145, D-146, D-147, D-148, D-149.
**Measured at:** `610bf9e` (working tree clean before this report was written). `610bf9e` is the
Board's trailer-move commit on top of `35b0550`; the code under test is identical.

> This file is written section by section as the pass runs. If it ends without a
> **"Pass complete"** line at the bottom, the pass was cut short and everything below the last
> completed section is **unreached**, not passed.

---

## 1. Gates — measured in this session

Order per `run-kaff-erp` "the gate order": stranded hosts killed first, then build, then suites.

| Gate | Command | Result (measured here) |
|---|---|---|
| Build | `dotnet build KaffErp.sln -c Release --nologo` | **exit 0 · 0 Warning(s) · 0 Error(s)** |
| Format | `dotnet format KaffErp.sln --verify-no-changes --no-restore` | **exit 0**, no diff |
| Domain.Tests | `tests\Domain.Tests\bin\Release\net10.0\Kaff.Domain.Tests.exe` | **total 229 · failed 1 · succeeded 228 · skipped 0** — exit 2 |
| Api.Tests | `tests\Api.Tests\bin\Release\net10.0\Kaff.Api.Tests.exe` | **total 539 · failed 0 · succeeded 538 · skipped 1** — exit 0 |
| `npm run build` | `npm run build` in `src\Web` | **exit 0**, bundle generated, **1 warning** (CSS budget) |
| `npm test` | `npm test` in `src\Web` | **15 files · 96 tests · 96 passed** — exit 0 |

Every one of these numbers was measured in this session against the binaries produced by the build
above. None is copied from a story, from `STATUS.md`, or from a builder report.

**The Domain suite is red at `HEAD`.** See `V-38-A`. Every builder figure in `STATUS.md` for this
batch reports Domain green (`229/229` at step 5i); that figure does not reproduce.

The one Api skip is `CreateEmployeeTests.A_day_labourer_cannot_be_registered_again_as_salaried_with_the_same_phone`
— the `Q80` hold. That is a HELD criterion that is **visibly** held, which is the correct shape.

The `npm run build` warning is `catalogue-list-page.css` over its 4.00 kB budget by 305 bytes. It is
outside this batch's scope (`KAFF-203`'s screen) but it means `npm run build` is **not** warning-free,
and the project's warnings-as-errors posture does not extend to the Angular budget. Recorded as
`V-38-B` (LOW) so that no later reader reports "npm build clean" off this page.

### 1a. `V-38-A` — the failing Domain test, named

Section 1 recorded the red suite without naming it. It is:

`Kaff.Domain.Tests.TranslationCatalogueTests.Every_screen_key_in_the_catalogues_is_read_by_a_template_or_a_component`

> *Expected collection to be empty because `AC-127-H` — a key no screen reads is a translation
> somebody maintains for nothing … but found at least one item `{"nav.subcontractors"}`.*

**Why it fails.** `KAFF-211` and `KAFF-212` added `nav.subcontractors` and `nav.suppliers` to both
catalogues, and **no template or component reads either one.** `ux/navigation.md` names both as
sidebar entries; the sidebar rows were not built, so the keys are orphans exactly as `AC-127-H`
defines one. FluentAssertions' `BeEmpty` message prints only the first offender — **the second,
`nav.suppliers`, is equally unread** and the test will still be red after only the first is
addressed. Confirmed by search: neither string appears anywhere under `src/Web/src`.

This is a real assertion catching a real orphan, not a broken test. It is not a money or permission
defect, but **the Domain suite is red at `HEAD` and the batch's own build reports claim it green.**

**`V-38-A` · MEDIUM** · `tests/Domain.Tests/TranslationCatalogueTests.cs` ->
`Every_screen_key_in_the_catalogues_is_read_by_a_template_or_a_component`; `src/Web/public/locales/ar.json`
and `en.json` -> `nav.subcontractors`, `nav.suppliers`. **What I did:** reproduced it, named it,
named the second orphan the message hides. Fixed nothing.

---

## 2. Rendered screens — Arabic, 390px, both palettes

### How they were driven

- **A scratch database, `kaff_verifier_v38`**, created in the `kaff-db` container and never `kaff`.
  The API ran on 5080 against it; `GET /api/setup` answered `{"available":true}` before setup, which
  is only possible on a database with no Owner, so the API was demonstrably not on `kaff`.
- **Every fixture was created through the API**, labelled `VERIFIER-FIXTURE` / `VRF38-*`: the Owner,
  seven role users (Finance, Technical Office, two Site Engineers, Head of Design, Marketing/Sales,
  HR), one client, two employees, one worker registered from site, one open engagement, two
  subcontractors, one supplier. **Two project rows were inserted with SQL** — there is no projects
  endpoint on this API, so a project cannot be created through it, and `KAFF-209`/`210`'s routes are
  all under `/api/projects/{projectId}`.
- **UI:** `run-kaff-erp`'s `driver.mjs eval` only. The in-page script signs in with
  `fetch('/api/auth/sign-in')` and loads each route in a same-origin `<iframe>` pinned at exactly
  **390px** wide.
- **Palettes.** The app has **no theme toggle and no `data-theme`** — `src/Web/src/styles.css` carries
  one bare `:root` block and one `@media (prefers-color-scheme: dark)` block, so the palette is the
  host browser's. Each palette was forced by copying that block's own custom properties onto the
  frame's root: **28 properties** for light, **11** for dark. The chromium the driver launches sits in
  dark by default, which is why the first pass measured dark twice — caught and redone. Both palettes
  below are measured, not assumed.
- ⚠️ **Nothing was screenshotted.** `driver.mjs shot` starts a fresh browser with no session, so an
  authenticated screen cannot be photographed by it, and the driver is not mine to change. Geometry,
  rendered text, form values and computed colours stand in for looking, **which is weaker for visual
  crowding and for overlap that does not change `scrollWidth`.** Recorded in §6 as not reached.

### What rendered

Every screen below: `dir="rtl"`, `lang="ar"`, `documentElement.scrollWidth === 390` at a 390px frame
(**no horizontal body scroll**), in **both** palettes.

| Screen | Route | Raw i18n keys | `[object …]` / `undefined` / `NaN` |
|---|---|---|---|
| Catalogue import (`S-019`) | `/catalogue/import` | none | none |
| Employee create (`S-024`) | `/employees/new` | none | none |
| Employee edit — salaried | `/employees/{id}` | none | none |
| Employee edit — day labour | `/employees/{id}` | none | none |
| Employee list (`S-023`) | `/employees` | none | none |
| Worker register (`S-026`) | `/projects/{A}/day-labour/new` | none | none |
| Worker pool (`S-025`) | `/projects/{A}/day-labour` | none | none |
| Subcontractor list (`S-028`) | `/subcontractors` | none | none |
| Subcontractor create (`S-029`) | `/subcontractors/new` | none | none |
| Subcontractor edit (`S-029`) | `/subcontractors/{id}` | none | none |
| Supplier list (`S-030`) | `/suppliers` | none | none |
| Supplier create (`S-030`) | `/suppliers/new` | none | none |
| Supplier edit (`S-030`) | `/suppliers/{id}` | none | none |

**`V-37-D`'s shape does not recur.** The scan matched every whitespace-delimited token against
`^[a-z][a-zA-Z0-9_]*(\.[a-zA-Z0-9_]+)+$` and against `[object` / `undefined` / `NaN`, on the rendered
`innerText` of each screen in each palette. Thirteen screens × two palettes, **zero hits**. The
employee kind renders as `موظف بالراتب` / `يومية`, not as a key and not as an object.

Bidi isolation is present where the stories name it: every phone number is inside a `<bdi dir="ltr">`
on the employee list, the pool, the subcontractor list and the supplier list; the phone input carries
`dir="ltr" inputmode="tel"` on all four forms; `nationalId` carries `dir="ltr" inputmode="numeric"`;
the retention field carries `dir="ltr" inputmode="decimal"`. Arabic free-text fields carry
`dir="auto"`. That satisfies the *mechanism* `AC-207-J`, `AC-209-I`, `AC-211-M` and `AC-212-L` name;
it does not satisfy the part of them about how it looks, which was not reached.

### `V-38-C` — the employee edit screen shows a day labourer's باب as unset

**HIGH.** `src/Web/src/app/features/employees/employee-form/employee-form-page.ts` (with
`.../employee-form-page.html`).

Fixture `E-10002` is a day labourer stored with `babId` = the `CON` باب (`أعمال خرسانة`) — confirmed
in the `POST /api/employees` response and in `GET /api/employees/{id}`. Opening
`/employees/{that id}` renders the باب select **with its value `""`, sitting on `بدون باب`**, while
the select's own option list contains all eight أبواب including `CON` by the exact id the record
holds. Name, phone, specialty and the immutable kind all load correctly on the same screen; only the
باب does not.

**Failure scenario.** HR opens a day labourer to correct his specialty, changes nothing else and
presses `حفظ`. The form submits the باب it is showing — none — against a record whose باب is
required (`AC-207-B`, `ck_employees_day_labour_has_trade`). The user is either refused a save they
made no change to, or the trade is lost; in neither case does the screen tell them their man had a
باب. **The screen states, in Arabic, that a stored required field is empty when it is not.**

This is `AC-207-G`'s *"the form renders … the staff file"* and `AC-208`'s costing-source display
failing on the one field `spec.md` §10 makes mandatory for this population.

### `V-38-D` — the two orphan nav keys are the other half of `V-38-A`

**LOW**, recorded here because it is a rendered-surface fact: `nav.subcontractors` and
`nav.suppliers` exist in both catalogues and **neither appears in the rendered shell on any of the
thirteen screens above**, in either palette. The shell's nav shows `المستخدمون` / `الكتالوج` /
`المشاريع` / `الملف الشخصي` depending on role, and never a subcontractor or supplier entry, so both
master screens are reachable **by URL only**. Same root cause as `V-38-A`; separate because a reader
of `ux/navigation.md` would expect the rows to exist.

---

*(pass in progress — sections 3 onward not yet written)*
