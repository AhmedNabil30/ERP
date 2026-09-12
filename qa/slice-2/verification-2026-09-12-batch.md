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

## 3. Acceptance criteria, per story

Every row below was exercised against the running app on the scratch database, or read out of the
code where the criterion is about a shape rather than a behaviour. **No verdict is `ACCEPTED`** —
that word is not available to a Verifier.

**A note that applies to all eight.** `git log 7117704..HEAD -- tests/E2E.Tests` is **empty**: not one
end-to-end test was written for any story in this batch, and no E2E file mentions an employee, a
worker, a subcontractor, a supplier or the catalogue import. **D-138's board policy caps every story
in this batch at `CONDITIONAL` on that ground alone**, before anything else below is weighed.

### `KAFF-200` — import the catalogue from Excel · **CONDITIONAL**

| AC | Held? | Evidence measured here |
|---|---|---|
| `AC-200-A` | ✅ | A three-row file imported; every item carries its code, both descriptions, unit, باب resolved by `Code`, both prices, and **`Status: Active`** |
| `AC-200-B` | ✅ | `costPrice 987.6543` / `baseSellRate 1234.5678` read back **exactly**. **A 17-digit price survives**: `1234567890123.4567` stored and read back unchanged. A five-decimal `1.00005` stored as `1.0001` — rounded at four through `Money`, **no refusal** (D-144 §6) |
| `AC-200-C` | ✅ | Row naming باب `ZZZ` refused `errors.master.bab_not_found`, naming `rowNumber: 3`, `column: "bab"`, `value: "ZZZ"`. **No باب was created** — the table still held exactly eight |
| `AC-200-D` | ⚠️ **not reachable** | There is no `/api/portal/*` route and no client-facing print or export on this API, so there is no surface on which `costPrice` could leak. The criterion is **vacuously true today** and will need re-running when a portal exists. Recorded, not claimed |
| `AC-200-E` | ✅ | `S-019` renders the not-a-sync sentence **in the page body**, above the file chooser, not in a tooltip |
| `AC-200-F` | ✅ | Only Owner and Technical Office reach `POST /api/catalogue-items/import`. Finance, both Site Engineers, Head of Design, Marketing/Sales and HR all `403` |
| `AC-200-G` | ✅ | One `CatalogueImported` audit row per import, `reason` = `Imported good.xlsx: 3 item(s) created`, with actor and time. A refused import created nothing |
| `AC-200-H` | ✅ | A five-row file with three bad rows imported the two good ones and returned a row-level report naming each failure's row number, column, message key and value |
| `AC-200-I` | ⚠️ **half** | An extra `status` column is refused and a missing column is refused. **But see `V-38-E`** — the refusal is a bare `errors.master.catalogue_import_failed` and names neither what the template requires nor what the file carried |

### `KAFF-201` — re-importing is not a sync · **CONDITIONAL**

| AC | Held? | Evidence |
|---|---|---|
| `AC-201-A` | ✅ | **No `IHostedService`, `BackgroundService`, `AddHostedService`, `Timer`, `FileSystemWatcher`, Quartz or Hangfire anywhere under `src/`** — searched, zero hits. Nothing can read a spreadsheet on its own |
| `AC-201-B` | ✅ | `POST …/import/preview` returned `willCreateCount: 1`, `willAffectCount: 1`, and per-item `oldCostPrice`/`oldBaseSellRate`/`newCostPrice`/`newBaseSellRate`. **The catalogue count was identical before and after the preview** |
| `AC-201-C` | ⚠️ **not reachable** | No BOQ exists in the system yet. Structurally the freeze holds — nothing this story writes has a path to a BOQ line — but it cannot be exercised. Recorded, not claimed |
| `AC-201-D` | ✅ | Nothing on the confirm path raises an alert; there is no estimate surface to raise one on |
| `AC-201-E` | ✅ | A confirm that moved a price wrote `Modified` with `before {"CostPrice":"987.6543","BaseSellRate":"1234.5678"}` and `after {"CostPrice":"555.1111","BaseSellRate":"777.2222"}`, `reason` naming the file and both counts |
| `AC-201-F` | ✅ | Confirm created the one new code and re-priced the one existing code, and every change appeared in the preview shown first |

### `KAFF-207` — employee register · **REJECTED**

| AC | Held? | Evidence |
|---|---|---|
| `AC-207-A` | ✅ | Salaried created, `E-10001`, active |
| `AC-207-B` | ✅ | Day labour with no باب refused `errors.master.day_labour_requires_trade`; `ck_employees_day_labour_has_trade` is present in the schema |
| `AC-207-C` | ✅ | `CreateEmployee.Request` has **no `Code` member**; codes came back `E-10001`…`E-10013` from the server |
| `AC-207-D` | ✅ | A second active salaried record on the same phone is refused `409 errors.master.employee_phone_taken` — and **still refused with `AcknowledgedDuplicatePhone: true`**, which is D-146 §1's partial unique index doing the refusing |
| `AC-207-E` | ✅ | No money-typed member on any employee request or response |
| `AC-207-F` | ✅ | Archive returns `204`, the row survives, and `ArchiveEmployeeTests` enumerates the host's own route table for a `DELETE` |
| `AC-207-G` | ❌ **broken** | The **create** form renders exactly D-139 §7 / D-144 §2's list and nothing more. The **edit** form does not render the record: **`V-38-C`** — a day labourer's stored باب shows as `بدون باب`, and the save the form would then submit is refused `400 errors.master.day_labour_requires_trade` (measured) |
| `AC-207-H` | ✅ | Only Owner and HR reach create, edit and archive. Site Engineer and Technical Office both `403` |
| `AC-207-I` | ✅ | One edit touching three fields wrote **one** record: `changedProperties ["Department","FullName","JobTitle"]` with both snapshots, actor `VERIFIER-FIXTURE Hr`, role `Hr`, its own time |
| `AC-207-J` | ⚠️ **mechanism only** | RTL, `lang=ar`, no horizontal scroll at 390px, phone and national id `dir="ltr"`, Arabic fields `dir="auto"`. **Not looked at** — see §2's screenshot limitation |

**Why REJECTED and not CONDITIONAL.** `V-38-C` is not a cosmetic gap. HR cannot save an edit to a
day labourer at all through the screen this story ships, and the screen states in Arabic that a
required stored field is empty when it is not. That is the register misreporting its own record,
which is the one thing `§2`'s *"exactly one record per costed person"* exists to make reliable.

### `KAFF-208` — nobody appears in both populations · **CONDITIONAL**

| AC | Held? | Evidence |
|---|---|---|
| `AC-208-A` | ✅ | Every row carries exactly one defined `Kind`; the column is a string enum in the schema, not an int with a zero value |
| `AC-208-B` | ⚠️ **held in the suite, answered in the product** | See `V-38-F`. The `Q80` hold is **visibly** held where a hold should be — the one skipped Api test. But the running system answers it: an active day labourer's phone on a salaried create is **accepted with an acknowledgement**, `201` |
| `AC-208-C` | ✅ | An edit carrying a different `kind` is refused `409 errors.master.employee_kind_immutable` — the error is reachable, not a dead declaration |
| `AC-208-D` | ✅ | No public member sets `Kind` after construction; `EmployeeKindInvariantTests` enumerates the allow-list by name |
| `AC-208-E` | ✅ | Measured end to end: a day labourer archived, then a salaried create on the same phone **warned** (`409 duplicate_phone_not_acknowledged`) and **succeeded on acknowledgement** (`201`), writing one `DuplicatePhoneAcknowledged` audit row. Both records exist, neither carries two kinds |
| `AC-208-F` | ✅ | Same matrix as `AC-207-H` |
| `AC-208-G` | ✅ | The list and both forms render **يومية** and **موظف بالراتب** verbatim from the catalogue, RTL, no horizontal scroll |

### `KAFF-209` — register a worker from site · **CONDITIONAL**

| AC | Held? | Evidence |
|---|---|---|
| `AC-209-A` | ✅ | Registered under `/api/projects/{A}/day-labour` with §10's four fields; the request record carries **no `Kind` member** |
| `AC-209-B` | ✅ | Refused `errors.master.day_labour_requires_trade`; the check constraint is in the schema |
| `AC-209-C` | ✅ | A worker stored as `+20 100 123 4567` was matched by `0100 123 4567`, `0020 100 123 4567`, `+20-100-123-4567`, `01001234567` and `+201001234567` — **all five** |
| `AC-209-D` | ✅ | `409 errors.master.duplicate_phone_not_acknowledged` naming the existing worker; `201` with the acknowledgement, one `DuplicatePhoneAcknowledged` audit row |
| `AC-209-E` | ✅ | The engineer assigned to A succeeds on A; the engineer assigned only to B is `403` on A's register, pool, phone-check and engagements — **all four** |
| `AC-209-F` | ✅ | Finance, Marketing/Sales, Technical Office, Head of Design and **HR** are all `403`. HR is refused, which is what D-140 point 1 asked to be caught |
| `AC-209-G` | ✅ | No money-typed member in the register request or response |
| `AC-209-H` | ✅ | `Employee Created` with `grantPath: Assignment` and `projectId: A`; the acknowledgement its own row |
| `AC-209-I` | ⚠️ **mechanism only** | RTL at 390px, no horizontal scroll, phone `inputmode="tel" dir="ltr"`. **One-handed reach was not measured** — no screenshot, no hit-target geometry |
| `AC-209-J` | ❌ **unbuilt** | The pool shows **no average day rate, no frequency and no rating at all**, so there is no empty state to be explicit. `GET /api/projects/{id}/day-labour` returns `id, code, fullName, phone, babId, specialty, isActive` and nothing else. See `V-38-G` |
| `AC-209-K` | ✅ | A phone-check against a salaried record returns exactly `{"matches":[{"restricted":true}]}` — no id, no name, no code. Against a day labourer it returns the full match |

### `KAFF-210` — worker engagement history · **REJECTED**

| AC | Held? | Evidence |
|---|---|---|
| `AC-210-A` | ❌ | An engagement opens with a worker and a project. **Its dates cannot be given and its day rate cannot be given** — `OpenEngagement.Request` is `(Guid? WorkerId)` and nothing else. **There is no history screen**: `app.routes.ts`'s day-labour tree has exactly `''` and `new`; `S-027` does not exist |
| `AC-210-B` | ❌ **unbuilt** | There are no pool figures to be derived or stored. Nothing to assert against — and **no test anywhere mentions `average`, `frequency` or `never_engaged`** |
| `AC-210-C` | ❌ **unreachable** | `Engagement.DayRate` exists on the entity as `Money?`, but **no route can set it**, so no value travels between a request body and the database |
| `AC-210-D` | ❌ | No average is computed anywhere |
| `AC-210-E` | ✅ | No `Posting` and no account was created by any engagement act; the mapped routes reach no treasury surface |
| `AC-210-F` | ⚠️ **half** | The rating range holds: `6` and `0` both refused `400 errors.master.engagement_rating_out_of_range`, `4` accepted. **Frequency is not rendered anywhere**, so the half about counting engagements rather than days is unbuilt |
| `AC-210-G` | ❌ **unbuilt** | Neither `S-025` nor a non-existent `S-027` shows the three figures, so none of them shows an empty state |
| `AC-210-H` | ✅ | Every role without `DayLabourSiteManage` is `403` on open, rate, close **and the pool read**; an engineer assigned elsewhere is `403` on all four |
| `AC-210-I` | ✅ | `Engagement Created`, `Modified ["Rating"]` and `Modified ["ClosedAt","ClosedOn"]`, each with actor, time, `grantPath: Assignment` and `projectId: A` |
| `AC-210-J` | ❌ | `S-027` does not exist. `S-025` renders RTL correctly but carries no rate to bidi-isolate |
| `AC-210-K` | ✅ | Closing succeeded for the assigned engineer. Pairing project B's route with project A's engagement id was refused **`403 errors.master.engagement_project_mismatch`** — rule 6a's check is present and works. No automatic process closes anything (no hosted service exists) |

Seven of eleven criteria unbuilt or unreachable. See `V-38-G`.

### `KAFF-211` — subcontractor master · **CONDITIONAL**

| AC | Held? | Evidence |
|---|---|---|
| `AC-211-A` | ✅ | Created with no retention given → stored `0.05`, active, code `SC-10001` generated |
| `AC-211-B` | ✅ | One firm at `0.025`, another still at `0.05`; no global default moved |
| `AC-211-C` | ⚠️ **half — see `V-38-H`** | Entering `5` stores the fraction `0.05` ✅ and `2.5` stores `0.025` ✅. **But the read shape is not the write shape**: `GET` returns `"0.050000"` (a fraction) while `PUT` consumes a percent, so echoing a read back divides the rate by 100 — measured, `5%` became `0.05%`. And **`101` is accepted**, stored `1.01`, displayed `101%` |
| `AC-211-D` | ✅ | No mapped subcontractor route creates a `User`, sets a credential or grants a role |
| `AC-211-E` | ✅ | No balance, outstanding, total or amount-typed member; no `Posting`, no account |
| `AC-211-F` | ✅ | No withholding rate on the record or on either screen |
| `AC-211-G` | ✅ | `WithholdingCategory` is gone from `Subcontractor` |
| `AC-211-H` | ✅ | Neither screen carries a price, a rate card or any per-item rate |
| `AC-211-I` | ✅ | `409 duplicate_phone_not_acknowledged` naming the existing firm; `201` on acknowledgement with one `DuplicatePhoneAcknowledged` audit row |
| `AC-211-J` | ✅ | **Finance is `403`** on create, edit, archive, get and list. So are HR, both engineers, Head of Design and Marketing/Sales |
| `AC-211-K` | ✅ | **Two** records: `Subcontractor Modified ["RetentionRate"]` by `VERIFIER-FIXTURE TechnicalOffice`, and `Subcontractor Modified ["TaxRegistrationNumber"]` by `VERIFIER-FIXTURE Finance`. Two endpoints, two actors, two rows |
| `AC-211-L` | ✅ | No bid, quotation, RFQ or comparison on either screen or on any mapped route |
| `AC-211-M` | ⚠️ **mechanism only** | RTL, **مقاول باطن** verbatim, no horizontal scroll, phones `dir="ltr"` inside `<bdi>`, percentages rendered `5%` / `12.75%`. Not looked at |
| `AC-211-N` | ✅ | Measured three ways: the Technical Office is `403` on `PUT …/tax-registration` **and** `403` on `GET /api/subcontractors/tax-registrations`; an edit request carrying a `taxRegistrationNumber` member is **ignored** (the member does not exist on `EditSubcontractor.Request`) and the stored number was unchanged. Finance's projection is exactly `id, code, name, taxRegistrationNumber, isActive` — nothing else — and Finance is `403` on `GET /api/subcontractors/{id}` |
| `AC-211-O` | ✅ **visibly held** | No Finance tax-registration screen exists and none was invented. The hold is real and observable |

### `KAFF-212` — supplier master · **CONDITIONAL**

| AC | Held? | Evidence |
|---|---|---|
| `AC-212-A` | ✅ | Created with code, name, phone, address; **no project member on the record** |
| `AC-212-B` | ✅ | One row, one company-scoped `SupplierPayable` account type; this story creates no account |
| `AC-212-C` | ✅ | No balance, outstanding, purchases or withholding member |
| `AC-212-D` | ⚠️ **story is stale** | The supplier code is now **generated** (`S-10001`…`S-10005`), so two suppliers cannot share a typed code — a create carrying a `code` member is ignored and the server's own code is returned. The criterion as written describes a typed code that no longer exists. **`V-38-I`** — a story-currency note for the BA, not a defect |
| `AC-212-E` | ✅ | No withholding rate on the record or the screen |
| `AC-212-F` | ✅ | `WithholdingCategory` is gone from `Supplier` |
| `AC-212-G` | ✅ | `409 duplicate_phone_not_acknowledged`; `201` on acknowledgement with one `DuplicatePhoneAcknowledged` audit row |
| `AC-212-H` | ✅ | **The Technical Office is `403`** on every supplier route, as are HR, both engineers, Head of Design and Marketing/Sales |
| `AC-212-I` | ✅ | Archive `204`, row intact, findable through the filter; the route-table enumeration finds no `DELETE` |
| `AC-212-J` | ✅ | One record, both fields, before and after |
| `AC-212-K` | ✅ | No bid, RFQ, quotation or comparison |
| `AC-212-L` | ⚠️ **mechanism only** | RTL, no horizontal scroll, name/phone/tax number bidi-isolated or `dir="ltr"`. Not looked at |

---

## 4. The named checks

### 4.1 `KAFF-210` is trailed `BUILT`. Is the trailer honest? — **No.**

**The brief's reading is correct against the repo and I confirm it, with more missing than it names.**

| What the story asks for | What exists |
|---|---|
| A read of a worker's engagement history | **No endpoint.** The complete `DayLabour` feature is `BabOptions`, `CloseEngagement`, `ListPool`, `OpenEngagement`, `PhoneCheck`, `RateEngagement`, `RegisterFromSite`. Nothing reads engagements |
| `S-027`, the history screen | **No route.** `app.routes.ts`'s `projects/:projectId/day-labour` has exactly two children, `''` (the pool) and `new` (the register) |
| The pool's three derived figures (`AC-210-B`/`D`/`G`) | **Neither side.** `ListPool`'s `PoolWorker` is `Id, Code, FullName, Phone, BabId, Specialty, IsActive` — measured live, those seven keys and no others. The rendered pool shows code, name, phone, specialty and an engage button |
| The day rate (`AC-210-A`/`C`) | `Engagement.DayRate` exists on the entity as `Money?`, and **no route can set it** — `OpenEngagement.Request` is `(Guid? WorkerId)`. Correct under rule 9's `Q76` hold, but it means `AC-210-A` and `AC-210-C` cannot be satisfied |
| The i18n keys the story lists | `hr.worker.history_title`, `hr.worker.engagement.project`, `hr.worker.engagement.dates`, `hr.worker.engagement.day_rate`, `hr.worker.pool.average_day_rate`, `hr.worker.pool.frequency`, `hr.worker.pool.rating`, `hr.worker.pool.never_engaged` — **not one of the eight is in either catalogue.** The surface was never built |
| Tests for the derived figures | **None.** No test in the repository contains the string `average`, `frequency` or `never_engaged` |

What **is** built is the engagement lifecycle: open, rate (range enforced), close, the project-mismatch
refusal of rule 6a, and the audit on all three. That is real and it works. It is roughly four of
eleven criteria.

**`BUILT` is not an honest trailer for this story.** It is not a partial build of one criterion; it is
a whole half of the story — §10's *"average day rate, frequency and rating"*, which is the reason
`KAFF-210` exists — with no endpoint, no screen, no key and no test. `Q76` holds the **day rate**, not
the frequency and not the rating count, so the hold does not cover the gap. Recorded as **`V-38-G`**.
Moving the trailer is the Board's, not mine.

### 4.2 The retention rate crosses as a number. Is a rate covered by D-135? — **flagged to the Architect**

**What I measured**, against the live endpoints:

| Field | Request type | What the server does with it | Response |
|---|---|---|---|
| `Subcontractor.retentionRate` | `decimal?` | **treats it as a percent** — `5` → `0.05`, `2.5` → `0.025`, `12.75` → `0.1275` | **a fraction, as a string**: `"0.050000"` |
| `Bab.defaultMarkup` | `decimal?` | **treats it as a fraction** — `0.1275` → `0.1275` | a fraction, as a string: `"0.1275"` |

Three separate things fall out, and I am flagging all three rather than ruling any of them:

1. **The transport half of D-135 is already satisfied.** `KaffJson`'s decimal converter writes every
   decimal as a JSON string, and the model binder accepts a JSON string on the way in — I sent
   `retentionRate: "2.5"` and it stored `0.025` exactly as the number `2.5` did. So a rate does
   already cross as a string outbound and may cross as one inbound. **Whether it must** — whether
   D-135 and D-139 §8 reach a `Percentage` at all, or stop at `Money` — is the Architect's to say.
   The live risk is the frontend, not the wire: `subcontractor-form-page.ts` calls `Number()` on
   submit, which converts the operator's text to an IEEE-754 double before it is ever serialised.
2. **Two rate fields, two unit conventions.** One takes a percent and one takes a fraction, on the
   same API, with no distinguishing name. Nothing in either request type says which.
3. **The subcontractor rate's read shape is not its write shape.** `GET` gives a fraction, `PUT`
   consumes a percent. **Measured:** a firm stored at the 5% default, read back as `"0.050000"` and
   written back unchanged, became **0.05%** — a hundredfold silent loss on a round trip through the
   public API. The Angular form does not hit this because it converts both ways
   (`fractionToPercent` on read, ×100 on submit); anything that is not that form does.

Also measured, and separate: **`101` is accepted** and stored as `1.01`, rendering `101%` on the
list. Retention above 100% has no refusal. `Bab.defaultMarkup` took `12.75` the same way — a 1275%
markup. Recorded as **`V-38-H`**. The ruling is the Architect's; the round trip is a defect either
way.

### 4.3 D-146 live against the endpoints — **all four confirmed**

| Case | No acknowledgement | With `AcknowledgedDuplicatePhone: true` |
|---|---|---|
| Salaried, active salaried match | `409 errors.master.employee_phone_taken` | **still `409`** — the refusal is D-146 §1's partial unique index `ux_employees_salaried_phone`, not an application check, and an acknowledgement cannot talk it round |
| Day labour | `409 errors.master.duplicate_phone_not_acknowledged` | `201`, both records exist, one `DuplicatePhoneAcknowledged` audit row |
| Subcontractor | `409 …duplicate_phone_not_acknowledged` | `201`, one acknowledgement row |
| Supplier | `409 …duplicate_phone_not_acknowledged` | `201`, one acknowledgement row |
| Site register (`KAFF-209`) | `409 …duplicate_phone_not_acknowledged` | `201`, one acknowledgement row |
| Site phone-check against a **salaried** record | — | `{"matches":[{"restricted":true}]}` — no id, no name, no code. D-140 point 6 exactly |

The schema backs it: `ux_employees_phone`, `ux_subcontractors_phone` and `ux_suppliers_phone` are
gone, replaced by non-unique `ix_*`, and `ux_employees_salaried_phone` is a partial index carrying
`WHERE … 'Salaried'` — `SchemaInvariantTests` asserts each of those by name.

**One thing the ruling does not cover and the product decides anyway** — see **`V-38-F`**. `AC-208-B`
holds the **active** cross-population case on `Q80`. Live, an active day labourer's phone submitted
on a salaried create is refused without an acknowledgement and **accepted with one** — identical to
the archived case D-146 §4(a) actually ruled. The hold is visibly held in the test suite (the one
skipped Api test) and silently resolved in the running system.

### 4.4 D-148 / D-149 — **both halves proven, from the audit table**

| Act | Route | `grant_path` | `project_id` |
|---|---|---|---|
| Site Engineer registers a worker (`Employee`, project-scoped) | `/api/projects/{A}/day-labour` | `Assignment` | **`A`** ✅ |
| HR creates an employee (`Employee`, company-wide) | `/api/employees` | *(none)* | **none** ✅ |
| HR edits an employee | `/api/employees/{id}` | *(none)* | **none** ✅ |
| Engagement open / rate / close | `/api/projects/{A}/day-labour/engagements…` | `Assignment` | **`A`** ✅ |
| Technical Office creates or edits a subcontractor | `/api/subcontractors` | *(none)* | **none** ✅ |
| Finance creates a supplier | `/api/suppliers` | *(none)* | **none** ✅ |

The same entity type, `Employee`, records `A` on the project-scoped route and nothing on the
company-wide one — which is D-148 §4 and D-149's opt-in working, and it is the strongest form of the
proof because the difference cannot be the entity. D-149's narrowing is real in the code too:
`Employee` is the **only** type implementing `IAuditScopedByGrant`, and
`AuditSaveChangesInterceptor` falls back to `_auditContext.GrantProjectId` only for that marker.

⚠️ **One inconsistency inside the proof — `V-38-J`.** On the project-scoped register route, the
`Employee Created` row carries `grantPath: Assignment` and `projectId: A`, while the
`DuplicatePhoneAcknowledged` row **written by the same request** carries neither. Two rows from one
project-scoped act, disagreeing about which project it happened on.

### 4.5 D-142 / D-145 — **eight أبواب, idempotent, never overwriting an edit. Proven live.**

Seeded on a fresh database: `CON, MAS, PLU, ELE, HVA, FIN, CAR, MET` — **exactly eight, the codes
D-145 §1 names, in that sort order**, with Karim's markups (`0.15, 0.15, 0.20, 0.20, 0.20, 0.30,
0.25, 0.25`).

Then the real test, run against the running stack:

1. I renamed `CON` to `اسم معدّل من المدقق` and set its markup to `0.4242` **directly in the
   database**, and **deleted `MET` outright**. Seven rows, one of them hand-edited.
2. Restarted the API, which runs `BabSeeder` on startup.
3. Read the table back: **eight rows.** `MET` was re-inserted at its own markup and sort order.
   **`CON` still carries `اسم معدّل من المدقق` and `0.424200`** — untouched.

Idempotent, additive, and it does not repair a row it did not write. The code says the same: it keys
on `Code`, has no update path, and contains neither `SetDefaultMarkup` nor `Rename` — D-142 point 3's
own test for whether it is wrong.

### 4.6 `KAFF-200` — the three money and template questions

**No `double` anywhere near a price in the import path.** Searched every file of
`src/Api/Features/Catalogue`, `src/Api/Common/XlsxSheetReader.cs` and
`src/Domain/MasterData/CatalogueTemplate.cs`: the tokens `double`, `float`, `ToDouble`, `GetDouble`
and `Convert.To` appear **only inside documentation comments**, never in an expression. The one
numeric path is `SheetCell.TryReadDecimal`, which is
`decimal.TryParse(Text, NumberStyles.Float, InvariantCulture, out value)` over the cell's stored
text. `NumberStyles.Float` there is a parse *style* (it admits Excel's `1E-4` exponent form), not a
binary float — reading it as one would be the wrong alarm.

**A 17-digit price survives.** Imported `1234567890123.4567` as both `costPrice` and `baseSellRate`;
read back from the API **exactly**, and the audit `after` snapshot carries the same string. Alongside
it, `987.6543` / `1234.5678` survived to the fourth decimal, and a five-decimal `1.00005` stored as
`1.0001` — rounded through `Money`, not refused, which is D-144 §6.

**The template columns and the header check come from one list.**
`CatalogueTemplate.Columns` — `code, descriptionAr, descriptionEn, unit, bab, costPrice,
baseSellRate`, no `status`. `XlsxTemplateWriter` writes the header with
`foreach (string header in CatalogueTemplate.Columns)`; `CatalogueTemplate.TryMatch` checks the
uploaded header against `Columns.Count` and then `foreach (string column in Columns)`. **One list,
two readers, no second copy.** The downloaded template's own sheet XML carries exactly those seven
headers, which I extracted and read.

What that list does **not** do is carry into the refusal message — **`V-38-E`**.

### 4.7 Permission tests hit endpoints directly, one per role, for every new route

**Yes, and I re-ran the matrix myself** rather than trusting the suite. Eight signed-in principals
(Owner, Finance, Technical Office, a Site Engineer assigned to project A, a Site Engineer assigned
only to project B, Head of Design, Marketing/Sales, HR) × 27 routes, called with `fetch` and a
session cookie, **no browser involved**. Every cell matched the story that owns the route:

- Catalogue import and template: Owner + Technical Office. Six roles `403`.
- Employees (list, get, babs, phone-check, create, edit, archive): Owner + HR. **Site Engineer and Technical Office `403`.**
- Day labour under `/api/projects/{A}/…` (pool, babs, phone-check, register, engagements): Owner + the engineer **assigned to A**. **HR `403`** — D-140 point 1's own reason for keeping HR off the row. The engineer assigned only to B is `403` on all four of A's routes.
- Subcontractors: Owner + Technical Office. **Finance `403`.**
- Subcontractor tax registration (both routes): Owner + Finance. **Technical Office `403`.**
- Suppliers: Owner + Finance. **Technical Office `403`.**

The structural control is stronger than the per-route tests: `EndpointPermissionCoverageTests`
requires **every** mapped endpoint to declare `RequirePermission(...)` or be a named member of an
allow-list, and separately fails a dead allow-list entry. A new route cannot be added ungated without
a deliberate edit to that test.

### 4.8 The E2E suite — **still unwritten for this batch**

`git log 7117704..HEAD -- tests/E2E.Tests` returns **nothing**. The suite that exists is slice 1's
(`SmokeTests`, `ClientScreenTests`, `UserScreenTests`, `AuditScreenTests`, `BidiGeometryTests`), and
no file in it mentions an employee, a worker, a subcontractor, a supplier or the catalogue import.

**Under D-138's board policy this caps all eight stories at `CONDITIONAL`**, which is why no story
above reads better than that regardless of its own evidence. Recorded as **`V-38-L`**.

### 4.9 Tests that cannot fail

I found **no** test in this batch that passes by having searched nothing, and the two shapes most at
risk both carry controls:

- `CatalogueImportHasNoSyncTests` asserts `suspectRoutes.Should().BeEmpty()` — an absence — and then
  asserts `allRoutes.Should().Contain("/api/catalogue-items/import")` and the two re-import routes.
  **That is the positive control**: the route table it searched is provably populated.
- `SchemaInvariantTests` does it properly and repeatedly — it drops a constraint, asserts the guard
  check **goes red** naming it, restores it, and asserts green again. A real red is watched, three
  times over.
- The bid/RFQ absence tests in `SupplierTests` and `SubcontractorTests` iterate a literal array of
  route constants, so the collection cannot be empty and a moved constant fails the compile.

**One weak one — `V-38-K`.** `EngagementLifecycleTests.No_money_member_appears_on_the_open_request_or_response`
loops `type.GetProperties()` and asserts inside the loop. If either record ever had no properties the
loop body would never run and the test would pass having checked nothing. It is a small hole — the
records are in the same solution and a property-less record is a visible edit — but it is the one
assertion here with no control that it enumerated anything.

**The larger fact is not a weak test but an absent one:** nothing asserts `AC-210-B`, `AC-210-D` or
`AC-210-G`, because there is nothing to assert against. See `V-38-G`.

---

*(pass in progress — sections 5 and 6 not yet written)*
