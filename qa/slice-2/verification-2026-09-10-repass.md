# Slice 2 — Verifier re-pass, 2026-09-10 (`opus`)

Re-runs `qa/slice-2/verification-2026-09-10.md` (finished on `sonnet`) on `opus`, per `agents.md` §M.
Target: `d5e6548` (`HEAD`). Written as it goes.

## Progress

| # | Block | State |
|---|---|---|
| 1 | Opening gate and figures | **done** — all seven green, §1 |
| 2 | `V-35-S` — catalogue frontend vs its criteria | **done** — §2: four met, `AC-206-I` fails on contrast (`V-36-C`); plus `V-36-A`, `V-36-B`; the money-path probe in §2.4 |
| 3 | Repaired findings re-driven | **done** — §3: all five hold; one residue, `V-36-G` |
| 4 | `KAFF-214` first verification | **done** — §4: A–D witnessed, E observed; record stale, `V-36-D` |
| 5 | Spot check of previously passed criteria | **done** — §5: `AC-206-A`, `AC-202-I`; `V-36-E`, `V-36-F` |
| 6 | Verdict per story | **done** — §6: `KAFF-202` REJECTED · `KAFF-203` CONDITIONAL · `KAFF-206` REJECTED · `KAFF-214` CONDITIONAL |
| 7 | Not reached · findings index · brief corrections | **done** — §7 |

## 0. Starting state

| Check | Result |
|---|---|
| `HEAD` | `d5e65481d3e615ba15f30a6500a39156c015bff6` — the brief's target |
| `git diff HEAD -- src/ tests/` | empty |
| Uncommitted records (verified **as they stand, uncommitted**) | `STATUS.md`, `decisions.md` (D-133), `qa/slice-2/test-cases.md`, `KAFF-202`, `KAFF-203`, **`KAFF-204`**, `KAFF-206` modified; `KAFF-214` untracked |
| Prefix | `V-36-` searched repo-wide before use: **no match**. Used as briefed |
| Model | `opus` (Claude Opus 5) |

⚠️ The brief lists the uncommitted records and omits `KAFF-204`, whose trailer moved `NOT-BUILT` → `READY` in the working tree. Out of this pass's scope; recorded so nobody reads the list as complete.

## 1. Gate and figures — re-measured by me

Order per `run-kaff-erp`: stranded hosts killed (none found), then build, exit code read, then tests.

| Gate | Figure | Exit |
|---|---|---|
| Build, Release, `-warnaserror` | `Build succeeded. 0 Warning(s) 0 Error(s)` | 0 |
| `dotnet format --verify-no-changes --no-restore` | no output | 0 |
| Domain.Tests (full) | `total: 154 · failed: 0 · succeeded: 154 · skipped: 0` | 0 |
| Citations | `1351 checked · 0 broken · 0 legacy` | 0 |
| Api.Tests (full project, unfiltered) | `total: 365 · failed: 0 · succeeded: 365 · skipped: 0 · duration: 5m 08s` | 0 |
| SPA `npm run build` | `Application bundle generation complete`, no warning or error line in the log | 0 |
| SPA `npm test` (`ng test`, vitest 4.1.11) | `Test Files 5 passed (5) · Tests 29 passed (29)` | 0 |

## 2. `V-35-S` — the catalogue frontend, rendered (in progress)

### 2.0 How it was rendered, and the scratch database

- **A separate scratch database, never `kaff`.** I ran `CREATE DATABASE kaff_verifier_v36` in the `kaff-db` container. The API ran on 5080 with `ConnectionStrings__KaffDatabase` pointed at it (Development, migrations on boot; health `200`, `guardsInstalled: true`), and `npm start` ran on 4200. `POST /api/setup` answered **201** and created the Owner `verifier-v36` ("VERIFIER V36 OWNER - scratch db"). That proves the API was not on `kaff`, which already has an Owner and would have refused the call.
- **Driven only through `run-kaff-erp`'s `driver.mjs eval`.** The driver cannot do three things this block needs:
  - authenticate a browser: there is no cookie support, and each command starts a fresh profile;
  - set a width for `eval`: only `shot` takes one;
  - emulate `prefers-color-scheme`.
  So the eval expression works inside the page. It signs in with `fetch('/api/auth/sign-in')`, then loads each route in a same-origin `<iframe>` at exactly 390px or 1280px. Media queries evaluate at the iframe's width. It then measures `documentElement.scrollWidth` against `clientWidth`, lists every element whose box crosses the viewport edge, takes `<bdi>` computed direction, looks for raw i18n keys on screen, and computes WCAG contrast of the rendered colours. The probe script is in my scratchpad, not the repo.
- ⚠️ **The headless browser defaults to `prefers-color-scheme: dark`** because it follows the host OS. My first stage's "light" and "dark" frames returned identical contrast (15.07 = `#ececec` on `#16181a`). **Every frame of the first stage was therefore the dark palette.** From then on, each frame forces its palette explicitly: the base `:root` tokens for light, the `@media (prefers-color-scheme: dark)` `:root` tokens for dark, set inline on the root. That reproduces exactly what the media query swaps (tokens only), apart from UA widget colours such as the native `<select>` popup.
- **No screenshots of the catalogue screens exist from this pass.** See the correction to the brief in §7. What stands in for "look at them" is measured geometry, text and contrast, which is weaker for visual defects like clipping or crowding. It says so here rather than implying a look that did not happen.

### 2.1 Stage A — no catalogue rows, dark palette (as the browser defaulted)

| Route | 390px | 1280px |
|---|---|---|
| `/catalogue` | `dir=rtl lang=ar`; `scrollWidth 390 = clientWidth 390`; renders `catalogue-list-empty` = «لا توجد أصناف بعد.»; no raw key on screen | `scrollWidth 1280 = clientWidth`; same state |
| `/catalogue?search=ZZZ-NO-MATCH` | renders `catalogue-list-empty-filtered` = «لا توجد نتائج مطابقة.» plus the clear action «مسح الفلاتر». **Clicking it** navigated to `/catalogue` and rendered `catalogue-list-empty`, so the two states are distinct and the action works | — |
| `/catalogue?status=archived` | `catalogue-list-empty-filtered` plus the clear action | — |
| `/catalogue/new` | every field full width (`x=28..362`), `scrollWidth 390`; the باب `<select>` holds only its placeholder «اختر الباب», because no باب exists | `scrollWidth 1280` |

Dark-palette contrast: text on ground **15.07**, the inactive chip **6.57**, the cost-price hint **8.87**, and the disabled submit **4.62**, which is below 4.5 only when opacity is counted and so is not a failure.

**One off-canvas element crosses the start edge at 390px:** the shell's `nav.side-nav` at `x=406..662`, beyond the inline-start (right) edge. Overflow past the start edge in an RTL document is not scrollable, which is why `scrollWidth` stays 390. It belongs to the shell (`KAFF-125`), not to these stories, and I record it without counting it.

### 2.2 Stage B/C — with verifier fixture rows (scratch DB only)

**What I inserted, and how.** Everything went into `kaff_verifier_v36`, never `kaff`:
- **Two أبواب, by SQL**, because no endpoint creates one (D-133 §4): `VERIFIER-FIXTURE-1` (`sort_order 10`) and `VERIFIER-FIXTURE-2` (`sort_order 20`). Both are named `VERIFIER FIXTURE n - not real data` in `name_ar` and `name_en`, with `default_markup = 0` so that no trade markup was invented.
- **Four items, through `POST /api/catalogue-items` as the scratch Owner**, `VRF-FIXTURE-001`…`004`. The descriptions are «بند اختبار المتحقق n»; `002`'s description is six times longer, to force wrapping; the prices run `0.5` … `123456789.9999` to stress figure width.
- `004` archived through the API; then (stage C) all four archived; then (stage D) all un-archived and `004` re-archived.

The whole database is dropped at the end of this pass (§7).

⚠️ **Probe artefact, corrected.** Stage B's iframe sat below the outer page's viewport. The rows carry `content-visibility: auto`, so their contents were render-skipped. `innerText` of every row came back empty, and the in-row geometry was inconsistent (row 2's archive button at y=772, inside its own description's 728–847). **Stage B's in-row geometry is discarded.** Stage D re-measures with the iframe pinned on-screen. Stage B's computed-style results (contrast, `<bdi>` direction, test ids, `scrollWidth`) do not depend on painting and stand.

**What stage B/C established:**
- Every code renders in a `<bdi>` with no `dir`, which computes `ltr`. Every figure renders in `<bdi dir="ltr">`, which computes `ltr`, e.g. `‏1,234.57 ج.م.‏` and `‏123,456,790.00 ج.م.‏`. Money displays at two decimals with the `U+200F` marks `STATUS.md` predicted, contained by the `dir="ltr"`.
- `scrollWidth = clientWidth` at 390 and at 1280 on the default, archived and all views, and on the edit form reached from a row. **The page body never scrolls horizontally.**
- At 1280 the code sits at the inline-start (right) edge, `x=848..975`, with description, unit and the two figures in columns toward the inline-end.
- The archive control opens its confirm body plus confirm and cancel, all within the row at 390; cancel dismisses it.
- The un-archive chip renders only on the archived row. **Clicking it** emptied the archived view to `catalogue-list-empty-filtered` and returned `VRF-FIXTURE-004` to the default list with an archive control, so the control was driven end to end.
- Clicking a row opens `S-018` in edit mode: «بيانات الصنف», the code as isolated read-only text (`<bdi>`, `ltr`), the باب picker absent, every input full width (`x=28..362` at 390, `208..816` at 1280).

### 2.3 Stage D — re-measured with the iframe on-screen, and the criteria

**Per-row geometry at 390, light and dark: 0 overlapping boxes, 0 boxes escaping their row**, on every rendered row. That includes `002`'s six-times description, which wraps (row height 315px), and the archived row: code `y=477`, badge «مؤرشف» `y=619`, un-archive chip «إلغاء الأرشفة» `y=655..699`, full row width `x=45..345`. At 1280, 0 overlaps on all four rows. `scrollWidth = clientWidth` everywhere.

**Other things observed:**
- **English.** Clicking the shell's «English» flips the document to `dir=ltr lang=en`. Every catalogue string translates («Item catalogue», «Archive item», «Cost price», `EGP 1,234.57`…), and no raw key appears in either language.
- **Deep link.** `/catalogue/{id}`, opened directly, renders `catalogue-form-no-source` («افتح هذا الصنف من قائمة الكتالوج لتعديله.») with a back link. This is the honest degradation the component documents, since the API has no `GET /api/catalogue-items/{id}`.
- **Filtered-empty with rows present.** `?search=ZZZ-NO-MATCH` against three active rows renders `catalogue-list-empty-filtered` with «مسح الفلاتر», and clicking that returns the rows (stage B).

**Static checks:**
- **i18n.** Every `catalogue.*`, `action.*` and `errors.*` key the catalogue screens, guard and APIs reference (36 keys) is present in **both** `ar.json` and `en.json`. The `catalogue.*` key sets of the two files are identical; there is no key in one only.
- **No literals.** Both templates render every string through `i18n.t`; the `*` required-field marker is the only literal.
- **Logical properties only, zero colour literals.** Over `features/catalogue/**` I searched for hex, `rgb(`, `hsl(`, `margin|padding|border-left|right`, `left:`/`right:`, `text-align: left|right` and `float`: **no match**.

| Criterion | Result on the scratch stack | Basis |
|---|---|---|
| `AC-202-J` (`S-018` at 390, Arabic) | **met.** RTL. The code is `<bdi>` (`ltr`) and the price inputs are `dir="ltr"`. No literal in either language. No horizontal scroll on the create form (stage A) or the edit form (stages B/D) | verifier-observed; `TC-2-027` E2E **not written** |
| `AC-203-H` (`S-017` at 390) | **met in substance.** RTL, identity column at the inline-start at 390 and 1280, codes and figures isolated, no body scroll. ⚠️ The criterion's *"the table scrolls inside its own container"* presumes a table: the shipped list is a `<ul>` that reflows to a stacked row under 48rem, so there is no wide content to contain. It is recorded, not failed | verifier-observed; `TC-2-035` held |
| `AC-203-D` (the UI half) | **met for both Givens.** `catalogue.list.empty_filtered` with a working clear action, against rows and against an empty catalogue. `catalogue.list.empty` on an empty catalogue. The two keys are distinct in both locales. **Plus `V-36-A`**, a third state no criterion names | verifier-observed |
| `AC-206-I` (`S-017` and the archive control at 390) | **not met on the badge half: `V-36-C`.** RTL, the filter readable (6.23 / 6.57), archive and confirm within the row, no literal, no body scroll | verifier-observed; `TC-2-065` held |
| `AC-214-E` (the un-archive control at 390) | **met.** RTL. «إلغاء الأرشفة» is readable: it sits outside the faded link, with the same colours as the filter chips (6.23 / 6.57). No literal, no body scroll, and the control was driven end to end | verifier-observed; `TC-2-103` held |

**Where this leaves `V-35-S`.** The layer it found missing exists now, and its five criteria render. **Four are met and one fails on contrast.** No committed test witnesses any of them: every catalogue E2E case is still unwritten or held (D-133 §4). What stands is one Verifier's observation on a scratch database, which a later commit can silently undo. **`V-35-S` is closed as "absent layer"; it is not closed as "witnessed".**

### 2.4 The money path through the new form (stages E–F)

This goes beyond block 2's brief. I drove `S-018`'s create form with unusual price input, and posted to the API directly. The fixture rows are `VRF-FIXTURE-005`…`012`, every value read back from `catalogue_items` with `psql`. The results are `V-36-H` (a blank price stored as zero), `V-36-I` (prices carried through a double), `V-36-J` (a successful save asks to discard changes) and `V-36-K` (hex and exponent accepted, Arabic-Indic digits refused generically).

The first attempt froze the headless page on the `V-36-J` dialog for more than 300s. I killed that driver process and its chromium by exact command-line markers (`driver.mjs eval`; `ms-playwright` + `--remote-debugging-port=0` + `--lang=ar-EG`), nothing broader. The re-run stubbed `window.confirm` inside the iframe to record calls.

## 3. The repaired findings — re-driven, not quoted

| Finding | Re-driven today | Holds? |
|---|---|---|
| `V-35-N` | **Test.** `EditCatalogueItemTests`' class remarks now say *"It does not discharge `AC-202-E` or `AC-202-F`"*, and the third assertion's reason string calls the أبواب count *"not a witness for AC-202-E/F, which are held to slice 4"*. The test name never claimed them. **QA file.** `TC-2-022` and `TC-2-023` each carry a dated held block naming that test as not a witness. **Story.** `KAFF-202` carries the note directly under `AC-202-F`. **Search.** A search of `src/` and `tests/` for `AC-202-E`/`F` finds only those disclaimers and `EditCatalogueItem.Handler`'s remarks, which cite the criteria as the rule honoured and state that no BOQ entity exists: honest, not a claim of witness | **yes** |
| `V-35-R` | **The fixture now discriminates.** `Early` (`SortOrder 10`) holds `{nonce}-Z-9` and `{nonce}-Z-1`; `Late` (`20`) holds `{nonce}-A-9` and `{nonce}-A-1`. Asserted: `[Z-1, Z-9, A-1, A-9]`. **A code-only sort gives `[A-1, A-9, Z-1, Z-9]`, which is different**, so the mutation `orderby item.Code` must redden it. I derived this by reading only: `src/` is not mine to mutate. **The commit's record is consistent with that derivation.** It reports *"13 total, 1 failed"*, and I count exactly 13 `[Fact]`s in `ListCatalogueItemsTests`; the one failure was this test's `.Should().Equal` assertion, which is the only assertion that prediction reaches. **Other mutations, by reading.** `orderby bab.Code` reddens (the أبواب's codes put `Late` first). Dropping the secondary key reddens in practice (items are inserted high-before-low). ⚠️ `orderby item.BabId` would pass about half the time (random GUIDs). That is a residual weakness, too small to file | **yes** |
| `V-35-T` | `Role.Client` is **executed** with a portal session (`ClientIdHeader`) in all three places, each asserting `403`. In create, a follow-up read also asserts that no item with the Client's attempted code exists. In edit, it asserts the sell rate is still 150. In unarchive, it asserts the status is still `Archived`. All three live in the 365/365 run, with 0 skipped | **yes** |
| `V-35-M` / `V-35-O` | `KAFF-206` carries dated held notes directly under `AC-206-D` (`C`/`D`, to slice 4) and under `AC-206-F` (to slice 4, mechanism the Architect's), and its DoR QA row is now qualified, not an unqualified ✅. `TC-2-062` is re-attributed to `AC-206-A`, with `AC-206-F` held in the Verifier's words. **Residue: `V-36-G`.** A test and two source remarks still credit the default with `AC-206-F` | **yes, with `V-36-G`** |
| `V-35-V` | *"3 of 22"* occurs in four places. It is the original claim only in `meetings/BRIEF-2026-09-10-verifier.md`, which is history per `STATUS.md`'s map. In the other three it is quoted as the finding or as its retraction: D-132, D-133 §2, and the prior verification. **Nothing repeats it as evidence.** D-133 §2's new rule was honoured by the next commit: `d5e6548` records both of its mutations, with what was changed, what reddened and that it was reverted | **yes** |

## 4. `KAFF-214` — first verification

| Criterion | Case | Test in `UnarchiveCatalogueItemTests` | Could it fail? |
|---|---|---|---|
| `AC-214-A` | `TC-2-099` | `An_archived_item_is_unarchived_and_reappears_in_the_default_search`: all six named fields read **before archiving** and compared after un-archiving | **yes**: any field `Unarchive` touched would redden it |
| `AC-214-B` | `TC-2-100` | `Unarchiving_an_active_item_is_refused_and_writes_no_audit_record`: `409`, `errors.master.not_archived`, status still `Active` | **yes**. The commit's recorded mutation (guard removed → `204`, failing on the `409` assertion) is consistent with the test's first assertion. Not re-run by me |
| `AC-214-C` | `TC-2-101` | `A_role_without_CatalogueManage_cannot_unarchive_an_item`: five staff roles and an executed portal `Client`, all `403`; status unchanged; the Owner's `204` as the positive control. `The_refused_list_is_every_reachable_role_TC_2_101_names` pins the set. `Subcontractor` is excluded structurally (§9, no login), the same split its siblings use | **yes** |
| `AC-214-D` | `TC-2-102` | **Positive half**, in the round-trip test: actor, `Status` in `ChangedProperties`, before `Archived` / after `Active`. **Refusal half**: an `AuditRecords` count scoped to the item's `EntityId`, unchanged | **yes**: if un-archive wrote nothing, the latest `Modified` record would be the archive's (`Active`→`Archived`) and the before-assertion reddens. ⚠️ *"one record"* is not asserted (a double write passes), and nor is *"the time"*. That is minor and filed under `V-36-F`'s shape, not separately |
| `AC-214-E` | `TC-2-103` held (D-133 §4) | none | **met by observation**, §2.3 |

**Placement (D-133 §1) against D-130 §4: consistent.** D-130 §4 rules the *what*: an archived record can come back, *"built once in `Domain/`"*. It names no story. `KAFF-206` recorded the placement as the Scrum Master's. Cutting a separate story follows the `KAFF-213` precedent that D-128 §2 and D-130 §5 set, and it keeps `KAFF-206`'s verdict-bearing scope unchanged.

⚠️ **One claim to watch, not a finding.** `KAFF-214` rule 3 says the mechanism is *"not duplicated per master record"*. What shipped is `CatalogueItem.Unarchive`, which is entity-local. Whether `Q39`'s client un-archive becomes a second copy or a shared mechanism is a future the rule describes before it exists: the Architect's, when `Q39` is built.

## 5. Spot check of criteria the `sonnet` pass passed

**Chosen, and why.** Both are universals, *"every … field"* and *"both fields"*. In both, the prior pass's own text shows it accepted a witness for one member of the set:
- **`AC-206-A`.** It was passed by cross-reference (*"§8/archive test"*). And `TC-2-099` exists **because** this exact gap was found on un-archive, so the archive twin was the likeliest miss.
- **`AC-202-I`.** The prior pass wrote *"the before/after JSON checked for the sell rate specifically"*.

Both are confirmed as partial: `V-36-E`, `V-36-F`. **Not rechecked:** every other criterion the prior pass passed.

## 6. Verdict per story

**The rule I applied, stated so it can be argued with:**
- **`REJECTED`:** a stated criterion is not met, or a HIGH defect sits on the story's own surface.
- **`CONDITIONAL`:** every criterion is met, subject to named conditions.
- **Not counted as failures:** criteria held to slice 4 by an explicit, visible, dated record (`AC-202-E`/`F`, `AC-206-C`/`D`/`F`).

| Story | Verdict | Reason, one line |
|---|---|---|
| **`KAFF-202`** | **`REJECTED`** | Its own create form **and** endpoint store an unpriced item at 0.00 (`V-36-H`, HIGH, a money value invented by the system). A successful save then asks to discard the changes (`V-36-J`). Criteria A–D, G and H pass; I is partial (`V-36-F`); J is met by observation |
| **`KAFF-203`** | **`CONDITIONAL`** | Every criterion is met: `AC-203-I` now discriminates, and H and D's UI half render correctly. **Conditions:** (1) `V-36-A`: the list must not say «لا توجد أصناف بعد» while archived items exist; (2) `TC-2-035` written as an E2E case. `V-36-B` is routed, not a condition |
| **`KAFF-206`** | **`REJECTED`** | `AC-206-I` fails on its badge half: «مؤرشف» measures 2.89:1 light and 3.59:1 dark against the project's own 4.5:1 in both palettes (`V-36-C`). Also: `AC-206-A`'s witness covers the code only (`V-36-E`), and stale `AC-206-F` citations remain (`V-36-G`) |
| **`KAFF-214`** | **`CONDITIONAL`** | `AC-214-A`…`D` are each witnessed by a test that can fail, and E is met by observation. **Conditions:** (1) `V-36-D`: the story file corrected, since it still reads uncased and under-tested and still cites §4.5; (2) `TC-2-103` written as an E2E case |

**The criteria that need rows, answered plainly.** **None was unreached in this pass.** I reached all five on a scratch database, with rows I inserted and then dropped. **But none of them is protected by a committed test**, and the rows I rendered them with no longer exist.

**I do not count the missing E2E cases against these four stories as failures.** Their cause is `KAFF-204` and `Q75` (no باب can be created or seeded), recorded in D-133 §4, which is outside all four stories' scope. **I do make writing them a condition**, because today every one of `AC-202-J`, `AC-203-H`, `AC-206-I` and `AC-214-E` rests on a single observation that the next frontend commit can undo without any gate noticing. And see §7's correction: they need not wait for `KAFF-204`.

## Findings (numbered as formed)

### `V-36-A` — MEDIUM · when every item is archived, the list says the catalogue is empty

**Stage C.** I archived all four fixture items through the API; `status=All` still returned four rows, all `Archived`. The default `/catalogue` at 390 then rendered **`catalogue-list-empty` — «لا توجد أصناف بعد.»**, which says *no catalogue items yet*, with no clear action.

That is false, and it is the exact conflation `KAFF-203` rule 7 forbids: *"the 'nothing matches the filter' empty state, not the 'nothing exists yet' one, which are different messages with different actions."* The default list is itself a filter: `KAFF-206` rule 7, `Active`. The component's own doc comment says the same, and its `isFiltered` then treats `active` as unfiltered [@ `src/Web/src/app/features/catalogue/catalogue-list/catalogue-list-page.ts` -> `isFiltered`].

**Consequence.** An operator told there are no items retypes one. Codes are unique, so the create is refused `409` `errors.master.catalogue_item_code_taken` against an item they cannot see. That is the trap `KAFF-214` exists to get them out of, and this screen hides the way out.

**Not a failure of `AC-203-D` as written.** Its two Givens (a search against a catalogue holding items, and a catalogue holding nothing at all) both render the right key; see §2.1 and stage D. The all-archived state is a third state that no criterion names.
- **Owner:** Frontend for the behaviour; BA/UX for the wording and action. Neither needs a new business rule: rule 7 already decides it.

### `V-36-B` — LOW-MEDIUM · cost price and base sell rate render as two unlabelled figures

- **The labels are hidden at every width.** Each figure's label (`catalogue.column.cost_price`, `catalogue.column.base_sell_rate`) sits in a `.sr-only` span [@ `catalogue-list-page.html` -> `row-price`], so it is invisible at every width.
- **Nothing else names the columns.** At 1280 the row becomes five columns with no header row. At 390 the two figures stack with nothing between them.
- **Measured:** at 1280 the figures sit at `x=131..208` and `x=49..127` and nothing on screen says which is which.

**Why it matters.** The two are money figures of different sensitivity: §4.2 keeps cost price off every client surface. An operator reading the internal list has to *know* that the first figure is cost.

**What the story asked for.** `KAFF-203` names header keys `catalogue.column.code`/`description`/`unit`/`bab`/`status` and `catalogue.status.active`/`archived`. **None of them exists** in either locale file; the i18n cross-check in §2.3 lists the keys that do exist. Rule 4 lists *status* among what the list shows, and an active row shows no status at all; only the archived badge exists.

**Not a failed criterion.** No `AC-203-*` names the labels, and every figure is present and isolated.
- **Owner:** Frontend and UX.

### `V-36-C` — MEDIUM · `AC-206-I`'s archived badge is below the project's own contrast floor, in both palettes

`ux/components.md` sets *"Contrast: 4.5:1 for text … in **both** the light and dark palettes."* Measured on the rendered archived row at 390 (computed colours, with the ancestor opacity composited):

| Element | light | dark |
|---|---:|---:|
| `.row-archived-badge` «مؤرشف» | **2.89** | **3.59** |
| archived row `.row-unit` | **3.17** | **4.31** |
| archived row code / figures | 5.25 | 6.55 |

**Cause.** `.row--archived .row-link { opacity: 0.65 }` compounds with `color: var(--color-muted)` on a badge that sits *inside* `.row-link` [@ `catalogue-list-page.css` -> `.row--archived .row-link`, `.row-archived-badge`].

**Verdict on the criterion.** `AC-206-I`'s Then says *"the archived badge and the filter are readable"*. The filter is readable: the inactive chip measures 6.23 light and 6.57 dark. **The badge fails the project's stated floor in both palettes**, so I record `AC-206-I` as **not met on its badge half**.

**Not the same thing.** The un-archive chip (`AC-214-E`) sits in `.row-actions`, outside the faded link, so it is unaffected; see stage D.
- **Owner:** Frontend.

### `V-36-D` — LOW-MEDIUM · `KAFF-214`'s story file is stale against its own cases and tests, in the pessimistic direction

**Two gaps it lists as open are both closed.** *What already exists* still reads *"No test drives a portal `Role.Client` against this endpoint"* and *"No test asserts the un-archived item's other §4.1 fields"*. `d5e6548` closed both: see §4, `AC-214-A` and `AC-214-C`. Its evidence row still says *"one permission case"*.

**Its QA box is wrong.** The DoR row reads *"⛔ Not met. QA has not cased this story"*, but `TC-2-099`…`TC-2-103` exist.

**The §4.5 mis-citation D-133 §6 routed is still there.** The header's *"Spec: §4.5 (item codes are unique)"* and rule 1's §4.5 citation stand. §4.5 is the BOQ builder; the basis for unique codes is D-130 §4.

**Consequence.** A reader of the story alone concludes that the story is uncased and its code under-tested. It is `V-35-M`'s defect, the story disagreeing with the QA file, pointed the other way.
- **Owner:** BA. Not a failure of any `AC-214-*`.

### `V-36-E` — MEDIUM-LOW · `AC-206-A`'s *"every one of its §4.1 fields unchanged"* is witnessed for the code only

**What the test asserts.** In `ArchiveCatalogueItemTests`, the `AC-206-A` / `AC-206-H` test checks `Status` and `Code` after archiving, and nothing else of the row. A search of the file finds no assertion on `DescriptionAr`, `Unit`, `BabId`, `CostPrice` or `BaseSellRate`.

**What the case demands.** `TC-2-057`'s *Fails if* names *"a field other than `status` changes"*.

**Why it was missed.** `d5e6548` closed exactly this gap for un-archive (`TC-2-099`) and left it standing on archive. That is `V-35-N`'s "asymmetry one story over" again. The prior pass recorded `AC-206-A` as PASS.

**Behaviour, read separately.** `CatalogueItem.Archive` refuses an already-archived row and otherwise sets `Status` and nothing else [@ `src/Domain/MasterData/CatalogueItem.cs` -> `Archive`], so the behaviour is right today. The defect is the witness: a later change to `Archive` that touched a price would pass this test.
- **Owner:** Backend/QA, test-only.

### `V-36-F` — LOW · `AC-202-I` asserts old and new values for one of its two fields

**What the test asserts.** `A_description_and_a_sell_rate_edited_together_are_one_audited_change` checks that `ChangedProperties` contains both `DescriptionAr` and `BaseSellRate`. It reads old and new values from `BeforeJson`/`AfterJson` **only for `BaseSellRate`** (150 → 175).

**What the criterion says.** *"both fields with their old and new values."* `DescriptionAr`'s values are unasserted, so an audit that named the field but serialised it wrongly would pass.

**Across these files.** No audit test here asserts *"the time"* (`OccurredAt`). The audit mechanism is shared and was verified in slice 1 (`KAFF-118`), so that part is recorded, not counted.
- **Owner:** Backend/QA, test-only.

### `V-36-G` — LOW-MEDIUM · a test and two source remarks still credit the list's default with `AC-206-F`

**The test.** In `ListCatalogueItemsTests`, the section header over `An_archived_item_is_hidden_by_default_and_returned_when_asked_for` still reads **`AC-206-A / AC-206-F`**. Its reason string says the default *"is what keeps an archived item off new work (`Q65`, D-130 §3) without the caller having to know to ask"*.

**The source.** `ListCatalogueItems.Handler`'s remarks carry the same sentence, and `CatalogueItemListFilter`'s `Active` summary cites `AC-206-F`.

**Why it matters.** That is precisely the claim `V-35-O` refuted, and that `KAFF-206`'s held note and `TC-2-062`'s re-scope now disown. The record and the code's own account of itself disagree, and the next reader of the test is told the guarantee exists. `d5e6548` re-cited `V-35-N`'s test but not `V-35-O`'s twin. D-132/D-133 did not explicitly route the citation to Backend, so this is residue, not a failed repair.
- **Owner:** Backend (test text and remarks only). Not a behaviour defect.

### `V-36-H` — HIGH · a blank price is saved as zero, by the create form and by the API

**The form, driven.** On the scratch stack I filled `S-018`'s create form with code `VRF-FIXTURE-005`, a description, a unit and the first باب, **left both price fields empty**, and pressed «حفظ». The row stored is:

`VRF-FIXTURE-005 | cost_price 0.0000 | base_sell_rate 0.0000 | Active`

read straight from `kaff_verifier_v36.catalogue_items`. No refusal, no warning.

**Why the form does it.**
- **The form never requires a price.** Both price labels carry the `*` required marker [@ `catalogue-form-page.html`], but the form's schema requires only code, description, unit and باب [@ `catalogue-form-page.ts` -> `draft`], so submit is enabled.
- **An empty string becomes zero.** `toWireDecimal` is `Number(raw.trim())` [@ `src/Web/src/app/core/catalogue/money-wire.ts` -> `toWireDecimal`], and `Number("")` is `0`.

**The API does it too, without the SPA.** `POST /api/catalogue-items` with `costPrice` and `baseSellRate` **omitted** from the body answered `201` and stored `VRF-FIXTURE-008 | 0.0000 | 0.0000`. `CreateCatalogueItem.Request` declares both as non-nullable `decimal`, so a missing member binds to `0`, and nothing refuses it. An explicit `null` was refused (`VRF-FIXTURE-009` was not stored).

**Why HIGH.** It is a money value **invented by the system**. A catalogue row priced 0.00 that nobody priced flows through `spec.md` §4.2's `lineRate = baseSellRate × …` into every BOQ line built from it: zero sell rate, zero cost, zero margin, all plausible-looking. CLAUDE.md's premise for money is that a wrong value must fail rather than pass silently. No criterion covers it: `AC-202-A` submits all six values, and `AC-202-D` covers negative prices only.

**Whether a zero price is ever legitimate is a business question**, and `spec.md` does not answer it. **What is not a business question is that an empty field must not be read as one.**
- **Owner:** Frontend (`required` on both prices, and no `Number("")`); Backend (a missing price must be refused, not defaulted); BA (a criterion that fails if it is not). The zero question goes to Nabil only if someone proposes to allow it.

### `V-36-I` — LOW · the SPA carries prices through a JavaScript double

**The mechanism.** `toWireDecimal` returns a JS `number` [@ `money-wire.ts`], and the list/edit contract types prices as `number` [@ `catalogue.api.ts` -> `CatalogueItem`]. CLAUDE.md: *"Never use `float` or `double` anywhere near money."* `AC-202-C`'s *"not through a float or a double … between the request body and the database"* holds for the API; the double is upstream of the request body, in the browser.

**Measured.** A 17-significant-digit price, inside `decimal(18,4)`'s domain, sent as a JS number: `12345678901234.5678` was stored as **`12345678901234.5680`** (`VRF-FIXTURE-011`).

**The edit path.** It is `String(item.costPrice)` on load, then `Number` on save, so a stored price beyond about 15 significant digits is silently re-priced by **any** edit, including a description-only one.

**Sending a string is safe.** The API accepts a JSON string for a price (`"12.5"` stored exact, `VRF-FIXTURE-010`), so the fix needs no server change.

**Severity LOW** because it takes magnitudes above about 10¹¹ with four decimals.
- **Owner:** Frontend.

### `V-36-J` — MEDIUM · a successful create asks the operator to discard their changes

**Driven.** I created `VRF-FIXTURE-012` with valid values: stored `100.2500 / 150.5000`, and the page navigated to `/catalogue/{id}`. **During that navigation the leave-page guard called `confirm`** with «هناك تعديلات لم تُحفظ. مغادرة الصفحة ستفقدها. هل تريد المتابعة؟», recorded by a stub. The same happened on `VRF-FIXTURE-006`. Without the stub, the dialog froze stage E, which is how it was found.

**Cause.** `onSubmit`'s create branch navigates without re-baselining `pristine`; only the edit branch calls `applyLoaded`. So `hasUnsavedChanges()` is still true when `confirmUnsavedChangesGuard` runs [@ `catalogue-form-page.ts` -> `onSubmit`; `src/Web/src/app/core/navigation/unsaved-changes.guard.ts` -> `confirmUnsavedChangesGuard`].

**Consequence.** Every successful save tells the operator their changes will be lost. The cautious answer, «إلغاء», keeps them on `/catalogue/new` with the form still filled. Saving again is refused `409 catalogue_item_code_taken`, for the item they have just created.

**No test covers the form component.** None of `npm test`'s five spec files is a catalogue component test.
- **Owner:** Frontend.

### `V-36-K` — MEDIUM · a price field accepts hex and exponent, and refuses Arabic-Indic digits with a generic error

**Driven through `S-018`:**
- **Hex and exponent pass.** Cost `1e3` and sell `0x10` were stored as **`1000.0000` / `16.0000`** (`VRF-FIXTURE-006`), because `Number()` accepts both.
- **Arabic-Indic digits fail generically.** Cost `١٢٣٫٥` (Arabic-Indic digits with the Arabic decimal separator) becomes `NaN`, which is sent as JSON `null`. The API refuses it, and the form shows **«حدث خطأ غير متوقع. حاول مرة أخرى.»**, a generic *unexpected error* with no price message. Nothing was stored.

**Why it matters.** Arabic is the product's primary direction and `S-018` is checked at 390px, where a phone's Arabic keyboard can produce exactly those digits.

**What is a decision and what is not.** Which digit systems a price accepts is a UX decision, not a business rule. A typed `0x10` silently becoming 16 is not a decision anyone made.
- **Owner:** Frontend, UX.

---

## 7. What I did not reach · findings index · corrections to the brief

### 7.1 Not reached

- **Screenshots of the catalogue screens.** None were produced; see correction 1. What stands in for them is measured geometry, text, `<bdi>` direction and contrast. No pixel-level look happened: not at glyph clipping inside a box, not at font fallback, not at focus rings.
- **Dark palette of the native `<select>` popup.** It is UA-drawn and not token-driven, so forcing the tokens does not reach it.
- **Saving an edit through `S-018`.** I opened the edit form but never submitted a `PUT` from the UI. `V-36-I`'s edit-path consequence is derived from the code, not driven.
- **Other sessions in the UI.** A Technical Office session was not tried; only the Owner was used, so the Technical Office landing on `/catalogue` is unchecked. A refused role reaching `/forbidden` in the UI is also unchecked; the server-side refusals are covered by the Api suite.
- **Mutations.** None was re-run, because `src/` is out of bounds. `V-35-R`'s and `AC-214-B`'s recorded mutations were checked for consistency with the tests, not reproduced.
- **Previously passed criteria.** Every one except the two spot checks in §5.
- **The E2E suite.** Not run; it is not in block 1's list.
- **The shell's single nav item.** It carries a static `is-active` class, so it reads as active even on `/catalogue` [@ `src/Web/src/app/app.html` -> `nav-item is-active`]. Observed, but it is `KAFF-125`'s scope and not investigated.
- **Housekeeping.** My Api.Tests run leaves one more `kaff_test_<guid>` database behind, the known leak. I did not sweep it.

**Cleanup, confirmed.**
- The API host on 5080 and the dev server on 4200 were stopped by the PID of the process listening on each port.
- `DROP DATABASE IF EXISTS kaff_verifier_v36 WITH (FORCE)` answered `DROP DATABASE`, exit 0. A follow-up `pg_database` query returns no `kaff_verifier%` database.
- `kaff` was never connected to, and nothing was inserted anywhere but the scratch database.
- `git diff HEAD -- src/ tests/` is still empty.

### 7.2 Findings index

| id | severity | one line | owner | story |
|---|---|---|---|---|
| `V-36-H` | **HIGH** | A blank price in the create form, or an omitted price on the API, is stored as 0.0000 | Frontend · Backend · BA | `KAFF-202` |
| `V-36-C` | MEDIUM | «مؤرشف» at 2.89:1 / 3.59:1 against the project's 4.5:1; `AC-206-I` not met on its badge half | Frontend | `KAFF-206` |
| `V-36-A` | MEDIUM | With every item archived, the list says «لا توجد أصناف بعد» | Frontend · BA/UX | `KAFF-203` |
| `V-36-J` | MEDIUM | A successful create triggers the "unsaved changes, discard?" confirm | Frontend | `KAFF-202` |
| `V-36-K` | MEDIUM | Price input accepts `1e3`/`0x10` and refuses Arabic-Indic digits with a generic error | Frontend · UX | `KAFF-202` |
| `V-36-E` | MEDIUM-LOW | `AC-206-A`'s "every §4.1 field unchanged" is witnessed for the code only | Backend/QA | `KAFF-206` |
| `V-36-B` | LOW-MEDIUM | Cost and sell render unlabelled; the story's header keys do not exist | Frontend · UX | `KAFF-203` |
| `V-36-D` | LOW-MEDIUM | `KAFF-214`'s story reads uncased and under-tested, and still cites §4.5 | BA | `KAFF-214` |
| `V-36-G` | LOW-MEDIUM | A test and two source remarks still credit the list's default with `AC-206-F` | Backend | `KAFF-206` |
| `V-36-F` | LOW | `AC-202-I` asserts old/new values for one of its two fields | Backend/QA | `KAFF-202` |
| `V-36-I` | LOW | The SPA carries prices through a JS double (17 digits: `…4.5678` → `…4.5680`) | Frontend | `KAFF-202` |

Standing, not re-ruled: **`V-35-Q`** (`Money` rounds silently above four decimals) is Nabil's and is untouched by this pass. None of the above resolves `AC-200-B` or D-008.

### 7.3 Corrections to the brief

1. **Block 2 cannot be done through `run-kaff-erp` as written.** `driver.mjs` has no way to authenticate a browser: no cookie, and a fresh profile per command. It has no width for `eval`, since only `shot` takes one. And it cannot emulate `prefers-color-scheme`. So *"screenshots at 390px and at desktop … light and dark"* of an authenticated screen cannot be taken through the skill, and I did not hand-roll a browser script to take them. The pass stayed inside `driver.mjs eval` and measured through in-page iframes instead (§2.0).
   - ⚠️ **On this machine the headless chromium defaults to the dark palette**, because it follows the OS. `shot` uses the same launcher, so a `shot` taken here without forcing a palette should be assumed dark. I did not audit earlier screenshots against this.
   - **Proposed, to the skill's owner:** a palette flag (`Emulation.setEmulatedMedia`), a width for `eval`, and a sign-in step.
2. **The brief's list of uncommitted records omits `KAFF-204`**, whose trailer moved `NOT-BUILT` → `READY` in the working tree and whose body gained a held note under `AC-204-C`.
3. **D-133 §4's *"cannot be written as E2E cases"* (and `STATUS.md`'s *"not written, and cannot be"*) is too strong.** It is true for the seeded demo stack only. An E2E fixture can stand up its own disposable database and seed a clearly-labelled باب by SQL, exactly as §2.2 did, without any invented trade reaching `kaff` or a demo. `TC-2-027`/`035`/`065`/`103` need not wait for `KAFF-204`. **Owner:** QA, with the Scrum Master to amend the record.
4. **Block 2 listed five criteria; the HIGH finding was outside all of them.** `V-36-H` sits on `S-018`'s own save path. It is the prior pass's §21.2 again: a brief that says *verify the criteria* rather than *verify the surface* would not have found it. This is a gap in what was asked, not a false statement.
5. **Confirmed as briefed:**
   - `HEAD` = `d5e6548` and `src/`/`tests/` clean.
   - The frontend landed in `934bfb9` and `GET /api/babs` in `e9ed405`.
   - `d5e6548`'s gate figures: Api 365/365 (my duration 5m 08s against the commit's 4m 43s, immaterial), Domain 154/154, citations 1351/0/0.
   - `Role.Client` is executed on all three endpoints.
   - *"3 of 22"* is not repeated as evidence.
   - `V-36-` was unused.

**Model: this pass ran on `opus` (Claude Opus 5) end to end**, as §M's never-downgrade list requires. It discharges the debt recorded at the top of `qa/slice-2/verification-2026-09-10.md` for the criteria re-run here; §7.1 says which were not.
