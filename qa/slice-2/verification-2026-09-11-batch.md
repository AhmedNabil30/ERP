# Slice 2 — batch verification (D-134), 2026-09-11

Verifier, fresh session, model `opus` (claude-opus-5). HEAD at start: `7117704`. `grep V-37-` over
the repo returned nothing before this file was created; findings are numbered from `V-37-A`.

## Progress

| # | Block | State |
|---|---|---|
| 1 | Gates, re-measured | **done** — all seven exit 0, §1 |
| 2 | KAFF-202, KAFF-206 (REJECTED last pass) | **done** — §2: `V-36-H` and `V-36-C` repaired, re-driven live; every other `V-36` fix holds |
| 3 | Money (D-135), permissions, audit across the batch | **done** — §3: D-135 exact end to end; refusals `403` live; audit clean; `V-37-B` |
| 4 | KAFF-204, 205, 213, 207, 208 | **done** — §4: `V-37-D` HIGH on 207/208; `V-37-C`, `E`, `F`, `K`, `M` |
| 5 | KAFF-203, KAFF-214 conditions | **done** — §5: `V-36-A` repaired but untested; `V-36-D` not repaired |
| 6 | Verdicts | **done** — §6 |
| 7 | Not reached · findings index · brief corrections | **done** — §7 |

## 1. Gates — re-measured by me at `7117704`

Stranded `Kaff.Api` / `Kaff.Api.Tests` / `Kaff.Domain.Tests` hosts and the 5080 listener killed first (none found), then build, exit code read, then tests. Logs were redirected to files and grepped, never piped.

| Gate | Figure | Exit |
|---|---|---|
| Build, Release, `-warnaserror` | `Build succeeded. 0 Warning(s) 0 Error(s)` | 0 |
| `dotnet format KaffErp.sln --verify-no-changes --no-restore` | no output | 0 |
| Domain.Tests (full) | `total: 197 · failed: 0 · succeeded: 197 · skipped: 0` | 0 |
| Api.Tests (full, unfiltered) | `total: 435 · failed: 0 · succeeded: 435 · skipped: 0` | 0 |
| `scripts/check-citations.ps1` | `1383 checked · 0 broken · 0 legacy` | 0 (`$LASTEXITCODE`) |
| SPA `npm run build` | `Application bundle generation complete`; **one budget warning** (below) | 0 |
| SPA `npm test` | `Test Files 8 passed (8) · Tests 59 passed (59)` | 0 |

Deltas against the 2026-09-10 re-pass: Domain 154 → 197, Api 365 → 435, SPA 29 → 59, citations 1351 → 1383.

`npm run build` prints `▲ [WARNING] src/app/features/catalogue/catalogue-list/catalogue-list-page.css exceeded maximum budget` (4.30 kB). A budget warning does not fail the build; recorded as `V-37-A`.

## 1a. How the stack was driven

**Database.**
- **The scratch database `kaff_verifier_v37`, never `kaff`.** I created it in the `kaff-db` container.
- The API ran on 5080 with `ConnectionStrings__KaffDatabase` pointed at it. Health returned `200`, with `guardsInstalled: true` and `missingGuards: []`.
- `POST /api/setup` answered **`201`**, which is only possible on a database with no Owner. That proves the API was not on `kaff`.
- `npm start` ran on 4200.

**Fixtures.** Everything is labelled `VERIFIER-FIXTURE` / `VRF37-*`, and all of it was created through the API:
- **Seven role users:** Finance, Technical Office, Site Engineer, Head of Design, Marketing/Sales, HR, and Client, each on a temporary password changed at first sign-in.
- **One client:** `VERIFIER-FIXTURE client`.
- **Six أبواب,** at markup `"0"`, except that `VRF37-P` and `VRF37-D` carry `"0.1275"`. That is `AC-204-B`'s own 12.75%, chosen to exercise the round trip. It is not a trade rate, and no 15% or 30% was used.
- **Catalogue items and three employees.**

The database was dropped at the end of the pass (§7).

**Tools.**
- **API probes:** a Node `fetch` script in my scratchpad that replays the session cookie. The cookie is `SameSite=Strict`, and there is no CSRF header to fake.
- **UI:** only through `run-kaff-erp`'s `driver.mjs eval`, run from Git Bash so the script reaches the driver as one argument. The in-page script signs in with `fetch('/api/auth/sign-in')` and loads each route in a same-origin `<iframe>` pinned on-screen at exactly **390px**.
- **Palettes:** each palette is forced by copying the stylesheet's own `:root` custom properties (28 light) or the `prefers-color-scheme: dark` block's (11 dark) onto the frame's root. The canvas converts any colour format, so contrast is computed after ancestor opacity is composited.
- **Nothing was screenshotted.** Geometry, text and contrast stand in for looking, which is weaker for visual crowding.
- **No test was written or committed.**

**Audit.** Audit rows were read straight from `kaff_verifier_v37.audit_records`.

## 2. `KAFF-202` and `KAFF-206` — the two REJECTED stories

### 2.1 `V-36-H` — a blank price stored as 0: **repaired, both halves**

**API** (`86f0188`):

| Body | Result |
|---|---|
| both prices omitted | `400 errors.master.cost_price_required` |
| sell rate omitted | `400 errors.master.sell_rate_required` |
| both `null` | `400` |
| both `""` | `400` |
| edit with both omitted | `400 cost_price_required`; the row read back unchanged at `12345678901234.5678` |
| explicit `"0"` | `201`, stored `0` — legal per `AC-202-D` (only negatives are refused) |

**Form** (`c5b2547`). On `/catalogue/new` at 390px I filled code, description, unit and باب and left both prices blank. **Submit was disabled**, and no row `VRF37-UI-2` existed until the prices were filled. A blank price can no longer become `Number("")` = 0: `toWireDecimal` returns the text unchanged, and no `Number(` / `parseFloat` touches money anywhere under `src/Web/src/app` (§3.1).

### 2.2 `V-36-C` — badge contrast: **repaired**

| «مؤرشف» on an archived row, 390px | light | dark |
|---|---:|---:|
| `S-017` catalogue (was 2.89 / 3.59) | **5.25** | **6.55** |

Both clear 4.5:1. **`AC-206-I` is met on its badge half.** The filter chips have no low-contrast text in either palette. The rest of the archived row is still faded below the floor, and the new lists copied the fade: `V-37-C`.

### 2.3 The other `V-36` repairs

| Finding | State at `7117704` | Evidence |
|---|---|---|
| `V-36-I` (JS double) | **repaired** | 17-digit create through the form stored `12345678901234.5678 / 99999999999999.9999` exactly; the edit form loaded those exact strings; a description-only edit through the form left both prices exact, and its audit row names `DescriptionAr` only |
| `V-36-J` (discard-changes confirm after create) | **repaired** | a stubbed `confirm` was called **0** times on a successful create, and 0 times on an edit save; the page navigated to `/catalogue/{id}` |
| `V-36-K` hex / exponent | **repaired** | form: `1e3`, `0x10` disable submit and show «أدخل رقمًا بالأرقام الإنجليزية (٠-٩ غير مقبولة)، بدون فواصل أو صيغة أسية.»; API refuses both `400` |
| `V-36-K` Arabic-Indic | **open for UX** — `V-37-G` | `١٢٣` and `12٫5` refused with the same price-specific message, no longer the generic error |
| `V-36-B` (unlabelled figures) | **repaired** | `row-price-label`s render «سعر التكلفة» / «سعر البيع الأساسي» (at 3.17:1 on archived rows — `V-37-C`) |
| `V-36-E` (archive keeps every §4.1 field) | **repaired** | `ArchiveCatalogueItemTests` now asserts `DescriptionAr`, `Unit`, `BabId`, `CostPrice`, `BaseSellRate` unchanged |
| `V-36-F` (both fields' old/new audited) | **repaired** | `EditCatalogueItemTests` reads `DescriptionAr` from both `BeforeJson` and `AfterJson`; live, the audit row carried both |
| `V-36-G` (`AC-206-F` credited to the default) | **repaired** | the only `AC-206-F` left in `ListCatalogueItemsTests` is the new *"held to slice 4"* remark |

### 2.4 The rest of their criteria

- **Re-driven live.**
  - `AC-202-A`: create `201`, response fields match §4.1.
  - `AC-202-C`: four decimals kept, and nothing passes through a double on either side of the wire.
  - `AC-202-D`: `"0"` is accepted.
  - `AC-202-H`: every non-holder gets `403` (§3.2).
  - `AC-202-I`: live.
  - `AC-202-J`: `/catalogue/new` and the edit form at 390px, `rtl`/`ar`, `scrollWidth 390 = clientWidth`, no raw key, no low contrast.
  - `AC-206-A` and `AC-206-E`: `409 already_archived` on a second archive, with no audit row.
  - `AC-206-G` and `AC-206-H`: live.
- **Held, visibly and dated, not failures:** `AC-202-E/F` and `AC-206-C/D/F`.
- **Not re-driven:** `AC-202-B`'s concurrent pair, `AC-202-G`'s portal surfaces, and `AC-206-B`'s route allow-list. The re-pass passed all three, and nothing in this batch touched them.
- **E2E `TC-2-027` and `TC-2-065` are unwritten** (§6).

## 3. Money, permissions and audit across the batch

### 3.1 D-135 end to end

| Check | Result |
|---|---|
| `POST` then `GET /api/catalogue-items`, `"12345678901234.5678"` / `"99999999999999.9999"` | both come back as **JSON strings, byte-identical** |
| the same through `S-018` (form → wire → DB → list → edit form) | exact at every hop (§2.3) |
| `GET /api/babs` markup | a string: `"0.127500"` (stored scale), `"0.000000"` |
| server grammar | refuses `1e3`, `0x10`, `١٢٣`, `12٫5`, `1,000`, `+5`, `" 5"` with `400`; accepts a JSON number `12.5` (D-135 keeps that for .NET callers) |
| `Number(`/`parseFloat`/`parseInt` under `src/Web/src/app` | **none on money**. The one hit is `Number.parseInt(sortOrder)` in `bab-form-page.ts`, an integer sort key. `percent-wire.ts` moves the decimal point by string manipulation |
| rounding above 4 decimals | `"1.23456"` stored `1.2346`, silently — **D-008, Nabil's, observed not ruled** |

### 3.2 Permissions — live matrix, refusals only

I called 15 routes on the scratch stack as each role that lacks the permission:
- `POST /api/babs`, `GET /api/babs`, `PUT /api/babs/{id}`, `PUT …/parent`, `POST …/archive`
- `PUT /api/catalogue-items/{id}/bab`, `POST /api/catalogue-items`, `PUT /api/catalogue-items/{id}`, `POST …/archive`
- `GET /api/employees/babs`, `GET /api/employees`, `GET /api/employees/{id}`, `POST /api/employees`, `PUT /api/employees/{id}`, `POST …/archive`

| Role | Result |
|---|---|
| Finance, Site Engineer, Head of Design, Marketing/Sales | **`403` on all 15** |
| HR | **`403` on all 9 باب/catalogue routes**, including **`GET /api/babs`** |
| Technical Office | **`403` on all 6 employee routes**, including `GET /api/employees/babs` |
| Anonymous | `401` on all 15 |
| **Client** | **could not be executed** — a Client user is `401` at `POST /api/auth/sign-in`, and no portal sign-in exists. See `V-37-B` |

**Nothing changed after the matrix.** باب `VRF37-E` still had its name and `"0.000000"` markup, `VRF37-P` was still active, and employee `E-10001` was unchanged.

**D-137, HR's باب list.** `GET /api/employees/babs` as HR returned `200` with 6 items. Their key set is exactly **`{id, code, nameAr, nameEn, parentBabId, isActive}`**, and the word "markup" appears nowhere in the body. HR's `S-024` picker offered the active أبواب and was **not disabled**, and `employee-babs-unavailable` did not render. HR on `/catalogue` is redirected to `/forbidden`.

### 3.3 Audit

**Every state change the probe made wrote exactly one record, with before and after.** Read from `audit_records`:
- باب markup: `{"DefaultMarkup":"0.000000"}` → `{"DefaultMarkup":"0.1275"}`.
- Two re-parents: `ParentBabId` old → new, including → `null`.
- Item move: `BabId` old → new.
- Item archive: `Status` Active → Archived.
- باب archive: `IsActive` true → false.
- Employee edit: `FullName` + `Specialty`, both old/new.
- Employee archive: `IsActive`.
- Creates carry the full `after`.
- Money in new rows is a string: `"CostPrice":"12345678901234.5678"`.

**Every refusal wrote nothing.** No `Bab`, `CatalogueItem` or `Employee` row exists for any of these:
- the duplicate code, the omitted markup, the three cycle attempts;
- the omitted, null or empty prices, and the seven grammar refusals;
- the باب archive with active items, and the re-archive;
- the day labourer without a باب, the duplicate phone and the kind change;
- all 99 matrix refusals.

The only security-event rows are `SignedIn` / `SignInFailed`, as designed.

*Cosmetic, not a finding:* the markup audit row's `before` carries the stored scale (`"0.000000"`) and its `after` the submitted scale (`"0.1275"`).

## 4. `KAFF-204`, `205`, `213`, `207`, `208` — criterion by criterion

"Test" means the test the builder mapped in the commit message. "Mutation" means the builder's commit records what was mutated, which test reddened on an assertion, and that it was reverted: D-133 §2's bar. I did not re-run any mutation, because that would mean changing `src/`.

### `KAFF-204` — the باب tree

| AC | Result | Witness |
|---|---|---|
| A | **met** | live: 5 أبواب created, B under A, C under B, the rest roots; tree renders B indented under D after the move. `CreateBabTests` ×2 |
| B | **met** | live: `"0.1275"` stored and listed as `"0.127500"`, rendered «12.75%» in a `<bdi>`. `PercentageTests` (Domain) and `percent-wire.spec.ts`; mutation recorded (`4f7f591`: 4 of 50 failed) |
| C | **held half** — own-markup half met (`CreateBabTests`), BOQ half held to slice 4 as recorded | — |
| D | **held half** — `A_markup_change_writes_no_row_beyond_the_babs_own`; BOQ half held | — |
| E | **met** | live: A under C (depth 3), A under B (2), A under A (self) all `400 errors.master.bab_cannot_be_its_own_ancestor`; the SPA renders «لا يمكن أن يكون الباب أصلاً لنفسه.» |
| F | **met** | live: `vrf37-a` against `VRF37-A` → `409 errors.master.bab_code_taken` |
| G | **met for 5 of 7 roles** — Client and Subcontractor unexecuted, `V-37-B` | live matrix §3.2 |
| H | **met** | live audit old/new rate; `EditBabTests` |
| I | **met** | 390px, both palettes: `rtl`/`ar`, `scrollWidth 390`, no raw key, no low-contrast text on active rows; the child row's `margin-inline-start: 20px` computes to `margin-right: 20px` in RTL, `margin-left: 0`; codes and percentages in `<bdi>` `isolate` |
| J | **read, not rendered** — `bab-tree-empty` with `bab-create` exists in the template; my scratch tree was never empty when rendered | — |

### `KAFF-205` — re-parent and move

| AC | Result | Witness |
|---|---|---|
| A | **met** | live: B (with child C) re-parented under D `200`; C's parent unchanged by it; `MoveBabTests` |
| B | **met** | live: C cleared to root `200`, renders at top level |
| C | **met** | live, three depths, one key, both catalogues resolve it (rendered in Arabic) |
| D | **met** | live `200`; audit `BabId` old → new; `MoveCatalogueItemTests`; mutation recorded |
| E, F | **held** to slice 4, visibly | — |
| G | **half** — new-default half only, as `TC-2-053` itself scopes it | — |
| H | **met for 5 of 7 roles** — `V-37-B` | live matrix |
| I | **met** | live audit, both moves |
| J | **met, one LOW** | باب move panel at 390, both palettes: no overflow, `scrollWidth 390`; item move panel: no overflow; its «إلغاء» is **3.27:1** light — `V-37-K` |

### `KAFF-213` — archive a باب

| AC | Result | Witness |
|---|---|---|
| A | **met** | live `204`; default tree excludes it, `?status=archived` includes it, `?status=bogus` → `400` |
| B | **met** | live `409 errors.master.bab_has_active_items` with `"count":2`; UI renders «يحتوي هذا الباب على ⁨7⁩ عنصر نشط…» for a باب with 7; test asserts 12 (mutation recorded: expected 12, found 15) |
| C | **met (test)** | `The_active_item_count_is_the_babs_own_not_its_subtrees` — not driven live |
| D | **half** — no item or child changes (test); **the route allow-list half has no test**, `V-37-M` | — |
| E | **met** | live, both routes: an item moved away and the other archived, then the archive succeeded `204` |
| F | **met** | live: the archived باب's archived item is still returned by `?status=archived` |
| G | **met** | live `409 already_archived`, no audit row |
| H | **met for 5 of 7 roles** — `V-37-B` | live matrix |
| I | **met** | live audit `IsActive` true → false; both refusals wrote none |
| J | **met** | count isolated by U+2068/U+2069 (FSI/PDI) inside the Arabic sentence; refusal text 6.90 / 5.18; badge 5.25 / 6.55; no overflow. Action chips on archived rows at 2.89 light — `V-37-C` |

### `KAFF-207` — employee register

| AC | Result | Witness |
|---|---|---|
| A | **met** | live as HR: `201`, `E-10001`, `Salaried`, active |
| B | **met** | live `400 day_labour_requires_trade`; raw-SQL constraint test |
| C | **not witnessed** — `V-37-E` | — |
| D | **met** (mechanism: the phone index, `Q70`) | live `409 employee_phone_taken` for `+20 100 037 0101` against `01000370101` |
| E | **met** | allow-list test; `GET /api/employees/{id}` keys contain no money member |
| F | **met** | live: archived excluded by default, found with `status=all`; route allow-list test |
| G | **met** | live: a body `code:"E-100"` ignored, `E-10001` generated; the form renders exactly full name, phone, kind, باب, specialty, national id, job title, hired-on |
| H | **met for 5 of 7 roles** — `V-37-B` | live matrix (TO, Finance, SE, HoD, MS) |
| I | **met** | live audit `FullName` + `Specialty` old/new; the refused kind change wrote none |
| J | **FAILED on `S-024` edit** — the kind line is a raw key, `V-37-D`. List and create form: `rtl`, no overflow, code and phone in `<bdi>`, name in `dir="auto"` | — |

### `KAFF-208` — nobody in both populations

| AC | Result | Witness |
|---|---|---|
| A | **met (test)** | `EmployeeKindInvariantTests.Every_stored_employee_carries_exactly_one_defined_kind` |
| B | **met** (mechanism named: phone index, `Q70`) | live `409` |
| C | **met** | live `409 errors.master.employee_kind_immutable`; mutation recorded; `EmployeeKindIsImmutable` is now returned |
| D | **met (test)** | named allow-list; mutation not recorded for this one |
| E | **blocked on `Q70`** — `V-37-F` | live `409` on re-registration |
| F | **met for 5 of 7 roles** — `V-37-B` | live matrix |
| G | **FAILED on `S-024` edit** — `V-37-D`. The create form reads «يومية» / «موظف بالراتب» verbatim | — |

## 5. `KAFF-203` and `KAFF-214` — their conditions only

| Condition | State |
|---|---|
| `V-36-A` (203) | **Repaired, live.** With every item archived (7 archived through the API, `status=all` still 8), the default `/catalogue` at 390 in both palettes rendered `catalogue-list-empty-filtered` «لا توجد نتائج مطابقة.» with «مسح الفلاتر». **Two residues:** no test pins it (`V-37-J`), and the clear action is a dead end here (`V-37-L`) |
| E2E `TC-2-035` (203) | **Not written.** `tests/E2E.Tests/` holds `ClientScreenTests`, `UserScreenTests`, `AuditScreenTests`, `BidiGeometryTests`, `SmokeTests` and nothing for the catalogue, أبواب or employees |
| `V-36-D` (214) | **Not repaired** — one of three parts; `V-37-I` |
| E2E `TC-2-103` (214) | **Not written** |

## 6. Verdicts

**On the missing E2E suite: yes, it makes every story in this batch at best `CONDITIONAL`, and on its own it rejects none.**

**Why at best `CONDITIONAL`:**
- Every one of the nine stories has an Arabic/RTL/390px criterion, and QA cased each one as E2E: `TC-2-027`, `035`, `045`, `046`, `056`, `065`, `075`, `082`, `092`, `103`.
- Not one of them exists. `CLAUDE.md`'s Definition of Done asks for the demo script to run end to end, and that cannot be met without them.
- My 390px measurements witness those criteria **once, today**. Nothing catches a regression tomorrow. `V-37-D` is exactly that failure: a component nothing renders in a test.

**Why not `REJECTED`:** where I measured the criterion and it held, the behaviour exists. A missing regression witness is a condition, not a defect in the story.

| Story | Verdict | One-line reason |
|---|---|---|
| `KAFF-202` | **CONDITIONAL** | `V-36-H` repaired on both halves, and every other `V-36` repair holds; condition: E2E `TC-2-027` |
| `KAFF-206` | **CONDITIONAL** | `V-36-C` repaired (5.25 / 6.55); conditions: E2E `TC-2-065`; `V-37-C`'s faded row text (3.17 / 4.31) on the same screen |
| `KAFF-203` | **CONDITIONAL** | `V-36-A` repaired live; conditions: `V-37-J` (no test) and E2E `TC-2-035`; `V-37-L` to UX |
| `KAFF-214` | **CONDITIONAL** | behaviour unchanged; conditions: `V-37-I` (`V-36-D` still two-thirds stale) and E2E `TC-2-103` |
| `KAFF-204` | **CONDITIONAL** | every live-drivable criterion met; conditions: `V-37-B` (Client/Subcontractor unexecuted for `AC-204-G`), E2E `TC-2-045`/`046` (`AC-204-J` read, not rendered) |
| `KAFF-205` | **CONDITIONAL** | every live-drivable criterion met; conditions: `V-37-B` for `AC-205-H`, E2E `TC-2-056`; `V-37-K` LOW |
| `KAFF-213` | **CONDITIONAL** | every live-drivable criterion met; conditions: `V-37-M` (`AC-213-D`'s route half), `V-37-B` for `AC-213-H`, `V-37-C` on its rows, E2E `TC-2-092` |
| `KAFF-207` | **REJECTED** | `AC-207-J` fails on `S-024` edit (`V-37-D`, raw key); `AC-207-C` has no witness (`V-37-E`) |
| `KAFF-208` | **REJECTED** | `AC-208-G` fails on `S-024` edit (`V-37-D`); `AC-208-E` blocked on `Q70` (`V-37-F`, Karim's) |

The line-3 trailers are not mine to move. **None of these verdicts is acceptance.**

## Findings (numbered as formed)

### `V-37-A` — LOW · the catalogue list stylesheet exceeds its Angular budget

`npm run build` warns `catalogue-list-page.css exceeded maximum budget … total of 4.30 kB`. Exit 0, so it is not a gate failure. The re-pass recorded the build with "no warning or error line", so this warning is new since `d5e6548` (probably the V-36-B/C repairs in `c5b2547`). Owner: **Frontend**, either trim the file or raise the budget with a reason.

### `V-37-B` — MEDIUM · no test executes a `Role.Client` or `Role.Subcontractor` call against the باب, move, archive or employee write endpoints

**The cases name them.** `TC-2-043`, `TC-2-054`, `TC-2-063`, `TC-2-090`, `TC-2-073` and `TC-2-081` each list Client and Subcontractor among the roles that must be refused.

**The tests do not call them.** A search of `CreateBabTests`, `EditBabTests`, `MoveBabTests`, `ArchiveBabTests`, `MoveCatalogueItemTests`, `CreateEmployeeTests`, `EditEmployeeTests`, `ArchiveEmployeeTests` and `ListBabOptionsTests` finds **no `Role.Client` and no `Role.Subcontractor`**. `Role.HeadOfDesign` appears only in `CreateBabTests` and `CreateEmployeeTests`.

**Where the executed Client call does exist.** Among the new endpoints, only `GetEmployeeTests` executes a `Role.Client` call (with a portal-client stamp). The catalogue test files from before this batch also carry one each. `PermissionMechanismTests` has no route sweep that would cover the gap.

**Behaviour, measured separately on the scratch stack.**
- A `HeadOfDesign` session was refused `403` on all 15 new or changed routes.
- **A live `Role.Client` call could not be made.** A Client user gets `401` at `POST /api/auth/sign-in`, and there is no portal sign-in route today. So the live matrix's Client column, all `401`, measures an unauthenticated caller, not a Client session.
- Server-side, the gate is one `RequirePermission` line per endpoint (verified on all 17 Endpoint.cs files under `Babs`, `Catalogue`, `Employees`). The authorization handler refuses Subcontractor outright (per `Role.cs`).

So the behaviour is very probably right. **The criterion's witness is missing for two of its roles on nine endpoints.** Owner: **Backend/QA**, test-only.

### `V-37-C` — MEDIUM · `V-36-C`'s cause was copied to two new screens: archived rows fade text below 4.5:1

**Method.** I measured at 390px in Arabic, with each palette forced through the stylesheet's own `:root` tokens: 28 light and 11 dark custom properties, read off `document.styleSheets`. I composited computed colours with ancestor opacity, the same method as the re-pass.

**`V-36-C` itself is repaired.** The catalogue's «مؤرشف» now measures **5.25 light / 6.55 dark**. The same badge on the باب tree and the employee list measures 5.25 / 6.55 too.

**The rest of each archived row is still faded below the floor** (`ux/components.md`: 4.5:1 for text, both palettes):

| Screen | Element on an archived row | light | dark |
|---|---|---:|---:|
| `S-017` catalogue | `.row-unit`, both `.row-price-label`s (the labels `V-36-B` made visible) | 3.17 | 4.31 |
| `S-021` باب tree | `.row-markup-label`; the «تعديل» and «نقل إلى أب آخر» action chips | 3.17 / **2.89** | 4.31 / 3.59 |
| `S-023` employees | the phone `bdi.row-phone`, the kind chip «يومية», the specialty, the «تعديل» chip | **2.89** | 3.59 |

**The cause** is the same row-level `opacity` that `V-36-C` named. `c5b2547` fixed the badge by changing its colour token, and left the fade. `4f7f591` and `31b049f` copied the fade into the two new lists.

**Criteria.**
- The badges pass `AC-206-I` and `AC-213-J`, and nothing here fails a criterion's literal wording.
- But the archived employee's phone number, which `AC-207-J` names, is at 2.89:1 in the light palette.
- So are two live action controls on an archived باب.

Owner: **Frontend** (one fix in three stylesheets), **UX** to say whether fading an archived row is wanted at all.

### `V-37-D` — HIGH · `S-024`'s edit screen shows the population as a raw key, `enum.EmployeeKind.[object Object]`

**Rendered.** On `/employees/{id}` (edit), in Arabic at 390px and in both palettes, the kind line `employee-kind-display` reads `enum.EmployeeKind.[objec…` inside its `<bdi>`, where it should read «يومية» or «موظف بالراتب». The raw-key scan found `enum.EmployeeKind` on screen. Every other route I rendered came back with no raw key.

**Cause.** The template concatenates the field, not its value: `i18n.t('enum.EmployeeKind.' + employeeForm.kind())` [@ `src/Web/src/app/features/employees/employee-form/employee-form-page.html` -> `employee-kind-display`]. `employeeForm` is a signal form, `form(this.model, draft)` [@ `employee-form-page.ts` -> `employeeForm`]. So `employeeForm.kind()` returns the field's state object, and string concatenation turns that into `[object Object]`.

**Why HIGH.** This is the one place a user reads which population a person is in after creation, and the one place `hr.employee.kind_is_permanent` explains it. Two criteria fail:
- **`AC-208-G`**: *"it reads **يومية** and the salaried term verbatim from the catalogue … no string is a literal"*. On `S-024`'s edit half it reads neither.
- **`AC-207-J`**: *"no string is a literal in either language"* on `S-024`.

The create half is correct: the `<select>` offers `Salaried=موظف بالراتب` and `DayLabour=يومية`, measured as HR. None of `npm test`'s 59 cases renders the edit display, which is how it passed.

- **Owner:** Frontend (a one-token fix, `.value()`, plus a component test that renders the edit half).

### `V-37-E` — MEDIUM · `AC-207-C` and `TC-2-068` still describe typing a duplicate code, and nothing witnesses code uniqueness at all (confirms the brief's known item 1)

**The record is still stale.** `TC-2-068` still reads *"a second is submitted with `E-100`, again with `e-100`"* (re-read this pass). `CreateEmployee.Request` has no `Code` member.
- **Measured:** a body carrying `"code":"E-100"` was answered `201` with the generated `E-10001`, so the submitted code was ignored.
- **Consequence:** the case as written cannot be executed.

**And no replacement witness exists.** The builder's own commit (`fe571d7`) states that no test asserts `ux_employees_code`, and no test in the files above does. So `AC-207-C` has **no passing test**. The mechanism is sound on reading: a database sequence plus a unique index. But the criterion is unwitnessed, not met.
- **Owner:** BA to restate `AC-207-C` for a generated code (as `KAFF-119` did for clients). QA to recase `TC-2-068`. Backend for one test that the index refuses a duplicate code inserted below the API.

### `V-37-F` — for Karim · `AC-208-E` is blocked by the phone index, confirmed live (confirms the brief's known item 2)

**Driven on the scratch stack as HR:**
1. Archived the day labourer `E-10003`: `204`.
2. Registered the same phone `01000370101` as salaried: **`409 errors.master.employee_phone_taken`**.

D-130 §7's archive-and-re-register therefore cannot be carried out for the same real person today. The test `EmployeeKindInvariantTests.Re_registering_the_same_person_by_phone_after_archiving_is_currently_blocked_by_the_phone_index` asserts the block and says so.

**Whether a phone must be unique only among active records is `Q70`. It is Karim's, and I do not rule it.** Until it is ruled, `AC-208-E` is not met.

The other half is live and correct: `AC-208-C`'s kind change is refused `409 errors.master.employee_kind_immutable`, and the refusal wrote no audit row.

### `V-37-G` — for UX · a price typed in Arabic-Indic digits is refused (confirms `V-36-K`'s open half, the brief's known item 4)

**The API's grammar, measured:** `١٢٣`, `12٫5`, `1e3`, `0x10`, `1,000`, `+5` and `" 5"` are each refused `400`. The SPA's grammar is the same (§3 below). Hex and exponent are now refused, so **`V-36-K`'s first half is repaired.**

Whether a price field accepts `٠`–`٩` and `٫` is `ux/rtl-and-i18n.md`'s to say, and it is silent. D-135 already fixes the mechanism if UX says yes: convert text to text before validating. Owner: **UX**.

### `V-37-H` — LOW · four story headers still say `NOT-BUILT` beside a `BUILT` trailer

`KAFF-205`, `KAFF-207`, `KAFF-208` and `KAFF-213` each open their Status line with **`NOT-BUILT.`**, and their line-3 trailers say `state=BUILT`. `CLAUDE.md` makes the trailer the only state. `KAFF-202`/`206` show the right form: *"state and verdict live only in this file's line-3 trailer"*. A reader of the header is told the opposite of the truth. Owner: **BA**.

### `V-37-I` — LOW-MEDIUM · `V-36-D` is repaired in one of its three parts: `KAFF-214`'s story still reads under-tested and still cites §4.5

Re-read at `7117704`:
- **Repaired:** the DoR row now reads *"✅ Met … `TC-2-099`"* (line 118).
- **Still stale, line 52:** *"No test drives a portal `Role.Client` against this endpoint"*. `UnarchiveCatalogueItemTests` carries two `Role.Client` references (`d5e6548` closed it, per the re-pass).
- **Still stale, line 53:** *"No test asserts the un-archived item's other §4.1 fields"*.
- **Still stale, line 44:** the evidence row still says *"one permission case"*.
- **Still wrong, line 20:** *"code is unique (§4.5)"*. §4.5 is the BOQ builder, and D-133 §6 routed this citation to the BA on 2026-09-10.

Owner: **BA**. It fails no `AC-214-*`, but it is the named condition on `KAFF-214`'s last verdict, and it is not met.

### `V-37-J` — LOW-MEDIUM · `V-36-A`'s repair has no test

**The code is repaired.** `isFiltered` is now `searchTerm().trim().length > 0 || statusFilter() !== 'all'` [@ `src/Web/src/app/features/catalogue/catalogue-list/catalogue-list-page.ts` -> `isFiltered`], so the default `active` counts as a filter.

**No test pins it.** A search of every `*.spec.ts` under `src/Web/src/app` finds no `isFiltered`, no `empty-filtered` and no `empty_filtered`. The E2E case that would catch it, `TC-2-035`, is unwritten. So a later edit back to `!== 'active'` passes all 59 SPA tests. The behaviour is re-driven live in §5. Owner: **Frontend** (one component test).

### `V-37-K` — LOW · the item move panel's «إلغاء» is 3.27:1 in the light palette

**Measured.** On `S-018` (edit) at 390px, after `catalogue-move-start`, the cancel control `button.back-link[catalogue-move-cancel]` measures **3.27:1** light. Everything else in the panel clears the floor, and nothing overflows. The dark palette was not measured for this panel.

`AC-205-J` does not name contrast. `ux/components.md`'s 4.5:1 floor does. Owner: **Frontend**.

### `V-37-L` — LOW-MEDIUM · with every item archived, «مسح الفلاتر» leads back to the same empty list

**Driven.** After `V-36-A`'s repair, the all-archived default correctly says «لا توجد نتائج مطابقة.» and offers «مسح الفلاتر». Clicking it left the URL at `/catalogue` with **0 rows**. Clearing returns to the default, `active`, which is the filter that was already empty.

**Consequence.** The operator the re-pass described, who needs to find the archived item, is offered one action, and it does nothing. The action that helps is the «مؤرشف» / «الكل» chip.

Which action this state should offer is a wording and flow question, not a business rule: `KAFF-203` rule 7 already says the two empty states have *different actions*. Owner: **UX**, then **Frontend**.

### `V-37-M` — LOW-MEDIUM · `AC-213-D`'s route-enumeration half has no test

**What the criterion asks.** `AC-213-D` wants *"the mapped routes this story adds are enumerated as an allow-list, and none of them touches a `CatalogueItem` or another `Bab` row"*.

**What the test does.** `ArchiveBabTests`' `AC-213-D` section tests the data half, `Archiving_a_bab_touches_no_item_and_no_child_bab`. The file contains no route enumeration: there is no match for `Route`, `EndpointDataSource` or `allow`. By comparison, `AC-206-B` and `AC-207-F` both have a real route allow-list.

The data half is met. Owner: **Backend/QA**, test-only.

### Observed, not ruled — D-008

`POST /api/catalogue-items` with `costPrice: "1.23456"` answered `201` and stored **`1.2346`**, silently. The audit row's `after` says `"CostPrice": "1.2346"`. This is D-008's open question (Nabil's). It is recorded as observed behaviour, and **it is not a finding of this pass**. The SPA form behaves the same (§3).

---

## 7. What I did not reach · findings index · corrections to the brief

### 7.1 Not reached

- **No live `Role.Client` call on any endpoint.** There is no portal sign-in (see `V-37-B`). **No Subcontractor call either:** that role holds no credential.
- **The E2E suite.** It is unwritten, and I wrote none, because a Verifier does not add tests.
- **No mutation re-run.** It would mean changing `src/`. I relied on the mutations the builders' commit messages record, under D-133 §2's bar.
  - Recorded for: `DecimalText`, CreateCatalogueItem's price guard, `Bab.Rename`, MoveCatalogueItem, ArchiveBab's count, ListBabOptions, `Employee.Create`, EditEmployee's kind guard, `percent-wire`, and the employee form's باب predicate.
  - **None recorded for:** `AC-207-E`'s and `AC-208-D`'s allow-lists, and `GetEmployee` (whose commit says so).
- **Not rendered:**
  - `AC-204-J`'s empty tree: my tree was never empty when rendered.
  - `S-025`, which `AC-208-G` names: it does not exist yet (it is `KAFF-209`'s screen).
  - Any screen at 1280px.
  - Any screenshot: measurement only.
- **Not re-driven:** `AC-202-B`'s concurrent pair, `AC-202-G`'s portal surfaces, `AC-206-B`, `AC-213-C` (test only), and `AC-208-A`/`D` (tests only).
- **Palettes forced by tokens**, not by `prefers-color-scheme` emulation, so UA-drawn widget colours (a native `<select>` popup) stay in the host's scheme.
- **Leftover test databases.** The Api run left its `kaff_test_*` database, as every run does (`run-kaff-erp`). I did not sweep them.

### 7.2 Findings index

| # | Severity | Finding | Owner | Story |
|---|---|---|---|---|
| `V-37-D` | **HIGH** | `S-024` edit renders the population as `enum.EmployeeKind.[object Object]` | Frontend | 207, 208 |
| `V-37-B` | MEDIUM | No test executes Client/Subcontractor against the باب, move, archive or employee write endpoints | Backend/QA | 204, 205, 213, 207, 208 |
| `V-37-C` | MEDIUM | Archived rows fade phone, chips and labels to 2.89–3.17:1 on three lists | Frontend · UX | 206, 213, 207 |
| `V-37-E` | MEDIUM | `AC-207-C`/`TC-2-068` still describe a typed code; no test witnesses code uniqueness | BA · QA · Backend | 207 |
| `V-37-F` | — | `AC-208-E` blocked by the table-wide phone index | **Karim** (`Q70`) | 208 |
| `V-37-G` | — | Arabic-Indic digits refused in price fields | **UX** | 202 |
| `V-37-I` | LOW-MEDIUM | `KAFF-214`'s story still stale in 2 of `V-36-D`'s 3 parts | BA | 214 |
| `V-37-J` | LOW-MEDIUM | `V-36-A`'s repair has no test | Frontend | 203 |
| `V-37-L` | LOW-MEDIUM | All-archived: «مسح الفلاتر» returns to the same empty list | UX · Frontend | 203 |
| `V-37-M` | LOW-MEDIUM | `AC-213-D`'s route allow-list half has no test | Backend/QA | 213 |
| `V-37-A` | LOW | `catalogue-list-page.css` over its Angular budget (4.30 kB) | Frontend | 203/206 |
| `V-37-H` | LOW | Four story headers say `NOT-BUILT` beside a `BUILT` trailer | BA | 205, 207, 208, 213 |
| `V-37-K` | LOW | Item move panel's «إلغاء» at 3.27:1 light | Frontend | 205 |
| — | — | Silent rounding `1.23456` → `1.2346`: D-008, observed only | **Nabil** | 202 |

### 7.3 Corrections to the brief

1. **"Including an executed `Role.Client` call" cannot be done on a running stack.**
   - A Client user is refused `401` at `POST /api/auth/sign-in`, and no portal sign-in route exists.
   - A Client call can only be executed in the integration suite, which mints a portal-client stamp.
   - The brief's check therefore reduces to *"is there a test that does it"*, and for nine of the ten new endpoints there is not (`V-37-B`).
2. **"The driver cannot … set a width for `shot`" is wrong.** `shot <url> <out.png> [width]` takes a width (`driver.mjs` → `screenshot`, and the `shot` case). What it cannot do is set a width for `eval`, which is what the re-pass recorded.
3. **The commit list omits three records-only commits in the range:** `bbcb96a`, `f04e177`, `8a4b81b`. They touch no `src/`/`tests/`, so nothing is lost. The list is not the whole range.
4. **Known item 3 understates the E2E gap.** No E2E file exists for any of these screens (`tests/E2E.Tests/` holds Client, User, Audit, Bidi and Smoke only). So the missing cases include the 204 (`045`/`046`), 205 (`056`), 207 (`075`), 208 (`082`) and 213 (`092`) cases as well as those listed.
5. **The known items miss one defect that sits beside them:** `V-37-D`. Every builder report and gate was green, and it is HIGH. It passed because no test renders `S-024`'s edit half.
6. **Otherwise confirmed, not contradicted:**
   - HEAD `7117704`.
   - Every figure the builders' last commit states: Domain 197, Api 435, SPA 59, citations 1383.
   - D-135 and D-137 as briefed.
   - The four known items (`V-37-E`, `V-37-F`, §5 and `V-37-G`).
   - The held criteria are visibly held in their stories and cases.
