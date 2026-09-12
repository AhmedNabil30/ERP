# STATUS — Kaff ERP, the single source of truth

**Read this file first. Nothing else in the repository states the current position.**

Everything above the generated block is maintained by hand and dated. Everything inside it is
derived. If another file disagrees with this one, **this one is right and the other file is
history** — see *The map* at the bottom for which is which.

**2026-09-07 · Slice 1 is CLOSED. Slice 2 (Masters) opens.**

**2026-09-10 · Slice 2's first three stories are `REJECTED` and being repaired finding by finding
(D-132, D-133). Slice 2 is now fifteen stories and 53 points: `KAFF-214` was cut for the un-archive
code that shipped without a story.**

---

## Slice 2 — D-132's findings, and where each one is (2026-09-10)

✅ **Re-verified 2026-09-10 at `d5e6548`, on `opus` (`1088889`). Every row below held.** The re-pass
found **`V-36-A`…`K`**. The verdicts, now on the board: `KAFF-202` **REJECTED** (`V-36-H`, HIGH, a
blank price is stored as 0), `KAFF-206` **REJECTED** (`V-36-C`, badge contrast), `KAFF-203` and
`KAFF-214` **CONDITIONAL** (D-133 §8).

⛔ **2026-09-11 — Nabil: build slice 2 as one batch, then verify it once (D-134).** ✅ The Architect
ruled `a169f62`: **D-135**, money crosses the wire as a JSON string, and **D-136**, `.xlsx` is read
with `ZipArchive` + `XmlReader`, no package. ⛔ **`KAFF-200`/`201` wait for Nabil.** D-136 raised
three questions that decide the template: one description column or two, whether a باب is named by
code or by name, and what the `status` column does. D-008's rounding question comes with them.
**Order now:** BA `V-36-D` → Backend: D-135 plus the `V-36` backend repairs → Backend: `204`, `205`,
`213` → Frontend: D-135, the `V-36` repairs, the `204`/`205`/`213` screens and the catalogue E2E →
Backend and then Frontend: `207`, `208` → one fresh Verifier. `KAFF-209`–`212` wait on Karim.

⚠️ **2026-09-11, state after the first Backend session.** It wrote D-135's server half, the `V-36`
backend repairs, and the `204`/`205`/`213` APIs (`CreateBab`, `EditBab`, `MoveBab`, `ArchiveBab`,
`MoveCatalogueItem`, `DecimalText`), then **ended without committing any of it**, waiting on a
background test run that never finished. Measured by the Scrum Master: build 0/0 and Domain.Tests
178/178, both from that session's own logs. **No Api.Tests result exists, and no mutation was
recorded.** A finisher Backend session was dispatched to run the gates and commit per item.
✅ **Finished and committed**: `f501f32` (D-135 server) · `86f0188` (`V-36-H/E/F/G`) · `bb7b8c9`
(`KAFF-204` API) · `8beaf5d` (`KAFF-205` API) · `a69de5d` (`KAFF-213` API). Reported by the
finisher: build 0/0, Domain 186/186, **Api 405/405**, one recorded mutation per item. These are its
figures; the batch Verifier re-measures them. `AC-205-E`/`F` were found to be `V-35-N`'s shape a
third time and are now **held to slice 4**; `qa/slice-2` counts **93 live cases of 103**. **Backend
lane still to build: `207`, `208`.** Frontend `c5b2547`: D-135's client half and the `V-36-A/B/C/H/J/K`
UI repairs, `npm test` 35 passing (the Frontend's figures). **The `204`/`205`/`213` screens and the
catalogue E2E suite are not started**, so they go to a second Frontend session. `V-36-K` raised a UX
question: `ux/rtl-and-i18n.md` does not say whether a price field accepts Arabic-Indic digits, so for
now they are refused, not converted.
⚠️ **The second Frontend session died on a session limit.** It left the `204`/`205` screens
**uncommitted** (`features/babs/`, `bab-tree.ts`, `percent-wire.ts`, `bab-manage.guard.ts`, the move
panel in the catalogue form). A Frontend finisher was dispatched to gate and commit that work, then
finish `213` and the E2E suite. Trailers for `204`/`205`/`213` now read `COMMITTED`: backend on
`main`, frontend not yet (D-133 §7).
✅ **Finished.** `4f7f591` commits the باب screens for `204`/`205`/`213`, including 33 Arabic keys the
dead session had left only in `en.json`. `npm test` is 50/50 (the Frontend's figure). **The trailers
move to `BUILT`.** ⚠️ **The catalogue and باب E2E suite is still unwritten.**
✅ **Backend `fe571d7`: `KAFF-207` + `208`**, four `/api/employees` endpoints. Api 427/427 is the
Backend's figure. Both trailers read `COMMITTED`; the frontend (`S-023`/`S-024`) is dispatched
next. ⛔ **Found: `AC-208-E` is blocked by `Q70`.** The phone index is unique across the whole table,
so an archived day labourer cannot be re-registered as salaried under the same phone (D-130 §7).
Whether phones must be unique only among active records is Karim's to answer (`Q70`). **Owed by
BA and QA:** `AC-207-C`/`TC-2-068` describe typing a duplicate employee code, which D-130 §6 made
impossible.
✅ **Frontend `31b049f`: `S-023`/`S-024`**, `npm test` 56/56 (the Frontend's figure). It found two
API gaps and did not paper over them. **(1) A silent data loss.** `PUT /api/employees/{id}` nulls
`NationalId`/`JobTitle`/`HiredOn` because no endpoint returns them. **(2) HR cannot read the باب
list** the day-labour field needs. **D-137** (`8a4b81b`) rules (2): `GET /api/employees/babs` under
`EmployeeManage`, carrying no markup. **Backend was dispatched for D-137 and for
`GET /api/employees/{id}`, then Frontend to wire both.** Only after that does the batch go to the
Verifier. ✅ Backend `1717a9e` (D-137, `GET /api/employees/babs`) and `ab224b6`
(`GET /api/employees/{id}`, data-loss regression test watched red first). Api 435/435 is the
Backend's figure. ✅ Frontend `79e01f4` wires both endpoints: `npm test` 59/59 and Domain 197/197
are the Frontend's figures. **`207`/`208` move to `BUILT`.** **The batch build is complete.** Every
buildable slice-2 story now has both lanes on `main`. **One fresh Verifier covers all of it next.**
**Not built:** the E2E suite for catalogue, باب and employees, and `200`/`201`/`209`–`212`.
⛔ **`200`/`201` were asked for again on 2026-09-11 and were refused.** The refusal is on the
business questions, not on budget. D-136's three template questions (one description column or
two, باب by code or name, what the `status` column does) and D-008's rounding question are
**Nabil's**, and a builder would have to guess all four. **The slice gate, *"Excel import works"*,
stays open until he answers.** ⚠️ **`AC-207-C`/`TC-2-068` are still stale:** the bookkeeping pass
that was to mark them superseded by D-130 §6 died on a session limit and edited nothing. The
Verifier is told this by name. **Owed by BA and QA.**

✅ **Batch verdict `57f0d78` (D-138):** 202, 203, 204, 205, 206, 213 and 214 are **CONDITIONAL**,
each capped by the unwritten E2E suite. **207 and 208 are REJECTED** on `V-37-D`, HIGH: the edit
screen renders `enum.EmployeeKind.[object Object]`.
⛔ **2026-09-11 — Nabil ruled Q70, Q71, Q72, Q73, Q29, Q13, Q68 and Q75 (D-139).** Next, in order:
1. Architect: the `Q71` permission, the `Q70` warn mechanism, the `Q75` seed mechanism.
2. BA: `207`, `209`–`212` and the register under D-139; cut the banks story; the `KAFF-318` impact;
   `V-37-E`/`H`/`I`.
3. QA: cases in one pass.
4. Backend and Frontend, serially: `V-37-D` first, then the other `V-37` repairs, the `Q70`
   migration, `207`'s fields, `209`–`212`, and the E2E suite.
5. One fresh Verifier.
**Waiting on Nabil:** the Arabic names and codes for the eight trades (the seed data waits, the
mechanism does not); warn-only for salaried staff; `HiredOn`; D-136's template questions; D-008.
✅ **Step 1 done — Architect `bd371b8`:**
- **D-140** — `DayLabourSiteManage`: project-scoped, for the Owner and assigned Site Engineers, on
  routes under `/api/projects/{projectId}/day-labour`.
- **D-141** — the three phone indexes become non-unique, and the client's D-049 warn-and-acknowledge
  flow is reused.
- **D-142** — a startup seeder keyed on trade code: insert-only, list empty until Nabil answers.

New questions for Nabil from D-140 to D-142:
- May a Site Engineer see or record a worker's day rate?
- May they close an engagement another engineer opened?
- Does the seed run beside trades the client already built?

**Step 2, the BA, dispatched next.**

⛔ **2026-09-12 — Karim's rulings via Nabil recorded as D-144 (`d13ef4a`); caveman rule as D-143.**
Salaried phones refuse, others warn; `HiredOn` kept; template: two description columns, باب by code,
no status column; D-008 closed in part (per-line vs total rounding open for slice 4). **Trade list
conflicts with D-139 §8 — asked of Nabil; seed mechanism built, rows held.**
Run plan (serial): (1) Frontend `V-37-D` → (2) Backend+Frontend `207` fields → (3) BA `200`/`201`/
`209`–`212`, then QA → (4) Architect amends D-141 for salaried-refuse, then Backend `Q70` + D-142
mechanism → (5) build `200`, `201`, `209`–`212` → (6) one `opus` Verifier over `7117704..HEAD`.
- Step 1 dispatched: Frontend (`sonnet`), `V-37-D`. ✅ `641b3cc`: `employeeForm.kind().value()`;
  two component tests watched red then green; `npm test` 61/61 (the Frontend's figure). BUILT, unverified.
- ⛔ **D-145 (Nabil):** the trade conflict is resolved — Karim's eight (`CON`…`MET`) with Arabic names
  and markups replace D-139 §8; **seed rows released**. Extract rounding per line then sum (slice 4).
  Slice 2 no longer waits on Nabil except D-140/D-142's three questions.
- Step 2 dispatched: Backend (`sonnet`), `207` fields. ⚠️ It ended waiting on a background test run
  and **committed nothing**: `Department` on entity, create/edit/get, migration `EmployeeDepartment`,
  tests — all uncommitted. A Backend finisher (`sonnet`) was dispatched to gate and commit.
  ✅ `c5b3a24`: `department: string?`, optional, on create/edit/get. Api 438/438, Domain 197/197 (the
  finisher's figures). Flag: required-vs-optional not ruled; built optional like `nationalId`/`jobTitle`.
- Step 2b dispatched: Frontend (`sonnet`), `207` Department on `S-023`/`S-024`. ✅ `2723fd8`: form
  field + ar/en keys; `npm test` 62/62 (the Frontend's figure). The asked-for edit round-trip test
  may not exist (report names only a label test) — the Verifier checks.
- Step 3 dispatched: BA (`sonnet`), stories `200`/`201`/`207`/`208`/`209`–`212`, banks story, register.
  ✅ `279b24a`, `053ab38`, `e7ed371`, `4e9524d`, `abc1a36`. New `KAFF-320` (banks, slice 3, not
  Ready). Register `Q76`–`Q82` added. `AC-208-B`/`E` HELD on cross-population phone (`Q80`).
  `KAFF-318` has no story file; impact recorded in the register. `211`'s Finance-only tax number
  needs an Architect mechanism.
- Step 4a (moved ahead of QA, so QA cases the final rule): Architect (`opus`). ✅ `e640ea9` **D-146**:
  `ux_employees_salaried_phone` partial unique + non-unique `ix_employees_phone`; `EmployeePhoneTaken`
  kept for salaried-on-salaried; archived cross-population warns; `AC-208-E` unheld, `AC-208-B` held
  (`Q80` narrowed to active records). ✅ `cd35dab` **D-147**: `SubcontractorTaxRegistrationEdit = 63`,
  Owner + Finance, own endpoint; suppliers need no split.
  New for Nabil: `Q80` (active cross-population: refuse or warn); rehiring a salaried leaver on the
  same phone (index refuses, no unarchive); whether "Finance only" excludes the Owner.
- Step 3b dispatched: BA (`sonnet`) follow-up — `211` `AC-211-K`, `Q80` narrowed, new register rows,
  `204` note for D-145. ✅ `212e694`, `1455f9f`: `AC-211-K` restated, `AC-211-N` added, `AC-211-O`
  HELD (no UX S-number); `AC-208-B` narrowed to active, HELD; `AC-204-K` (seed eight) added; `201`
  no change. Register `Q83` (rehire salaried leaver), `Q84` (Owner in "Finance only").
- Step 3c dispatched: QA (`sonnet`), cases for every changed criterion in one pass. ✅ `3742094`,
  `df39b40`: `TC-2-104` (seed), `105`–`115` (209), `116`–`126` (210), `127`–`141` (211), `142`–`153`
  (212); `068` rewritten (`V-37-E`), `001`/`002`/`009`/`080` rewritten, `077` HELD. **142 live / 12
  held** (QA's count, not re-derived). QA flags `AC-210-C`'s agreed day rate as money; who records it
  from site is `Q76`, so `210` builds that field HR-side only until Nabil answers.
- Step 4 dispatched: Backend (`sonnet`), D-141+D-146 phone indexes and warn-ack, D-142 seeder with
  D-145's eight rows. ✅ `63a57e7`: `Q70PhoneIndexes`, `POST /api/employees/phone-check`, salaried
  refuse then warn-ack. ✅ `f746119`: `BabSeeder` with the eight trades. Api 450 (449 pass, 1 skipped
  on `Q80`), Domain 197 — the Backend's figures.
- Step 4b dispatched: Frontend (`sonnet`), employee form phone warn-ack (D-141 frontend half).
  ✅ `3e9335d`: the client warning lifted to `shared/duplicate-phone-warning/` and reused; `npm test`
  65/65 (the Frontend's figure). The salaried refusal shows at submit only (D-146 point 3 leaves a
  `kind` on the match open).
- Step 5 dispatched: Backend (`sonnet`), `KAFF-200` import API. ✅ `3867454`, `3e46ab5`: hand-read
  xlsx per D-136, template from one column list (D-144 columns), both endpoints `CatalogueManage`.
  Api 478 (477 pass, 1 skipped), Domain 197 — the Backend's figures. Flags: audit granularity still
  unruled (built per-entity plus one `CatalogueImported` event; Architect's); a concurrent import on
  one code fails the whole batch (no AC names it).
- Step 5b dispatched: Frontend (`sonnet`), `KAFF-200` `S-019`. ✅ `bbaff0d`: `/catalogue/import`,
  upload as-is, row report; `npm test` 67/67 (the Frontend's figure). Not rendered live at 390px.
- Step 5c dispatched: Backend (`sonnet`), `KAFF-201`. ✅ `35ced3c`: stateless preview and confirm
  (confirm re-parses); reader moved to `Api/Common/`, column list to `Domain/MasterData/` per D-136;
  absence tests watched red with a dummy sync route and hosted service. `AC-201-C`/`D` untestable until
  slice 4 (no BOQ/Estimate entity). Api 484 (483 pass, 1 skipped) — the Backend's figure.
- Step 5d dispatched: Frontend (`sonnet`), `KAFF-201` confirm dialog on `S-019`. ✅ `d034ba1`:
  re-import as its own action beside `200`'s (story silent on one flow or two — flagged); prices as
  strings through `i18n.formatMoney`; `npm test` 70/70 (the Frontend's figure). ⚠️ It did **not** watch
  its tests fail first — the Verifier is told.
- Step 5e dispatched: Backend (`sonnet`), `KAFF-209` Site Engineer register (D-140).

| Finding | What | Owner | State |
|---|---|---|---|
| `V-35-S` HIGH | No catalogue frontend | Frontend | Screens built in `934bfb9`, **unverified**. E2E `TC-2-027`/`035`/`065` **not written, and cannot be**: no endpoint creates a باب, so a seeded stack holds no catalogue row (D-133 §4). **Waits on `KAFF-204`** |
| `V-35-N` | `AC-202-E`/`F` "discharged" by a test that cannot fail | BA · QA · Backend | BA ✅ story record · QA ✅ `TC-2-022`/`023` held · Backend ✅ `d5e6548`, the test re-cited to what it proves. **Unverified** |
| `V-35-M` | `AC-206-C`/`D` hold invisible from the story | BA | ✅ 2026-09-10 |
| `V-35-O` | `AC-206-F` is a default, not a guarantee | BA · QA · Architect | ✅ recorded and ✅ `TC-2-062` re-scoped, outstanding to slice 4. **Architect** rules the mechanism at slice-4 refinement |
| `V-35-U` | Un-archive shipped with no story | Scrum Master · QA · Backend | ✅ placed as **`KAFF-214`** (D-133 §1) · QA ✅ `TC-2-099`…`103` · Backend ✅ `d5e6548`: six-field, executed-Client and scoped-audit tests. **Unverified** |
| `V-35-R` | `AC-203-I`'s test cannot tell باب order from code order | QA → Backend | QA ✅ `TC-2-036` rewritten · Backend ✅ `d5e6548`: fixture inverted, and the code-only mutation **went red on the ordering assertion**, recorded in the commit. **Unverified** |
| `V-35-T` | Client refusal asserted in a comment | Backend | ✅ `d5e6548`: `Role.Client` now executed against create, edit and unarchive, all `403`. **Unverified** |
| `V-35-V` | Mutation claim with no artifact | Scrum Master | ✅ **retracted** (D-133 §2). It was the Scrum Master's brief |
| `V-35-Q` | `Money` silently rounds above four decimals | **Nabil** | **Open. It blocks `KAFF-200`, the slice gate** |

**`KAFF-204` is `READY` as of 2026-09-10** (D-133 §7). QA held `TC-2-039`'s BOQ-line half.
⛔ **It is the pull that makes the catalogue demonstrable at all**, and whether it enters a sprint
is Nabil's. **It moves to `BUILT` only when both its lanes have shipped** (D-133 §7).

---

## ⛔ What "slice 1 closed" does and does not mean

**Closed is not done, and the difference is 33 points.**

Nabil closed slice 1 on 2026-09-07. That is a legitimate scope decision and it is his to make. What
it changes is that **slice 1 takes no new build work**. What it does **not** change is the state of
the code, and the board says so rather than rounding it up:

| | pts | |
|---|---:|---|
| 🔵 **Verified** | **103** | a Verifier gave a verdict and it still stands |
| ⛔ **Lapsed** | **3** | KAFF-125 — confirmed on real diffs 2026-09-07, **and it could not be lifted** |
| 🟡 **Built, nobody independent has looked** | **0** | cleared 2026-09-07 by the `V-35` pass |
| 🔻 **Deferred, never built** | **21** | KAFF-104 · KAFF-115 · KAFF-129 → **slice 1b** |
| ✅ **ACCEPTED** | **84** | **2026-09-09 — Nabil ran the demo script.** 21 stories, `deploy/DEMO.md` §9 |

**The `V-35` pass, 2026-09-07 — the first independent eye on any of it.** KAFF-118 **PASS** (every
mutating endpoint driven individually; each wrote exactly one record, every read and refusal none) ·
KAFF-101b **PASS** (all eight criteria driven for the first time) · KAFF-128 **CONDITIONAL** ·
KAFF-125 **LAPSED stands**.

✅ **2026-09-09 — the first acceptance this project has had.** `process/agile.md` §4 is *"Nabil runs
the demo script"*, and it happened: Nabil recreated the database, ran `deploy/DEMO.md`, and accepted
**84 points across 21 stories**, explicitly including the UI enhancements. **Only §9's "demonstrable
today" table moved.** Nothing was accepted by silence — the 22 points §9 lists as unreachable and the
21 `DEFERRED` are untouched, and no agent performed §4.

⚠️ **3 points still carry no standing verdict, and were NOT accepted on 2026-09-09** — KAFF-125, and the `V-35` pass **could not lift it**:
the rewritten `AC-125-C` cannot be executed by anyone. That is sprint 6 item 3, and it is a criterion
problem, not a code problem.

### The deferred 21 points are carried, not cancelled

**`slice 1b`** is where they live. Nothing was deleted; each story keeps its file, its criteria and
its trailer, and `state=DEFERRED` says exactly what happened to it.

| Story | Pts | Why it was not built |
|---|---:|---|
| **KAFF-104** — reset a forgotten password | 5 | Committed to sprint 5; the agent died on its first tool call at a session limit |
| **KAFF-115** — project team panel | 8 | Held back at sprint-5 planning for capacity, never pulled |
| **KAFF-129** — partition `audit_records` monthly | 8 | Cut 2026-09-07. **Its retention period is `Q54`, still Karim's** |

⛔ **KAFF-129 is the one with a deadline attached.** D-072 §3 rules monthly partitioning of
`audit_records`, and the cost rises the moment Treasury writes its first posting: converting a
populated, trigger-protected, append-only table is a new table plus a migration plus a swap.
**It must land before slice 3 opens, not before slice 3 finishes.**

---

## Slice 2 — Masters. What has to happen before anyone builds

**Gate:** *Excel import works.* **Epic:** catalogue, أبواب, employees, workers, subcontractors,
suppliers.

⛔ **Two corrections to what this file said on 2026-09-07, both found by the BA on 2026-09-08 and
both mine:**

**1. Slice 2 is thirteen stories and 48 points — `KAFF-200`…`KAFF-212`.** This file listed six and
called it the slice. It was a **prefix**, taken from a partial read of `stories/backlog.md`. *(Superseded 2026-09-09: **fourteen stories, 51 points** — `KAFF-213` was cut by D-128 §2. The 48 stands as what was true on 2026-09-08 and is kept so the correction reads in order; **do not quote it as the current figure** — the generated block below is.)*

**2. ⛔ "Slice 2 needs none of Karim's questions" was FALSE.** `stories/backlog.md` has said
*"Blocked by: `Q12` and `Q13` — due before this slice opens"* since the slice was first estimated,
and **`Q12` is open.** It decides whether the Owner keeps `CatalogueManage` / `BabManage`. I checked
only this file's own seven-row question table and reported the absence as a fact about the slice.
**Slice 2 was opened on a claim that was not true**, and the BA raised it rather than working around
it.

### Refined in full 2026-09-09 — **fourteen stories, 51 points, every one `NOT-BUILT`**

`READY` is a claim that the Definition of Ready is met, not a default. **None of the fourteen meets
it, and they all fail on the same box.**

**`qa/slice-2/` does not exist and no `TC-` range is allocated**, so *"QA has written a failing
scenario"* is unmet everywhere. That is QA's box and it is the only thing standing between nine of
these stories and Ready.

| | Stories | Pts | What is left |
|---|---|---:|---|
| **Waiting only on QA** | `KAFF-200` `201` `202` `203` `205` `206` `207` `208` `213` | 27 | Every business question answered. Write the `TC-` cases and they are Ready |
| **Buildable, not seedable** | `KAFF-204` | 5 | Behaviour fully specified. **Its data is `Q75`** — Kaff's real trades and their markups, which nobody here can invent |
| **Still blocked on Karim** | `KAFF-209` `210` `211` `212` | 16 | `Q70` `Q71` `Q72` `Q73`, plus `Q29` on `211`/`212`. `KAFF-209` additionally has **no role that reaches its endpoint** |
| **New** | `KAFF-213` archive a باب | 3 | Cut by D-128 §2, ruled by D-130 §5 |

**What changed on 2026-09-09.** Nabil answered `Q12` — the Owner holds every permission (D-129 §1) —
and passed Karim's ball back to the team, which produced D-130's seven rulings. Between them they
closed **`Q12` `Q39` `Q60` `Q61` `Q62` `Q63` `Q65` `Q66` `Q67` `Q68` `Q69`**. `Q64` was explicitly
**not** closed: it is a data-exposure boundary, D-055 §2 already rules name-and-role, and nothing is
blocked by leaving it open.

⚠️ **`KAFF-200`'s title changed and the board's did with it.** It said *"all-or-nothing"*; `Q61` is
now ruled the other way — the import **loads the good rows and returns a row-level report**. The old
title described the behaviour the ruling calls gridlock.

⛔ **`Q75` is refused, not pending.** It is Kaff's ~40 trades and each one's markup, and it is **data,
not a decision** — no authority makes anyone here know it. `spec.md` §4.2's *concrete 15%* and
*finishes 30%* are examples, and an agent building the tree against silence will use them and they
will look like data. It goes to Karim with `Q70`–`Q73` and `Q29`.

✅ **The `Bab` cycle defect is repaired.** This file described it as live until 2026-09-09.
`SetParent` now walks the candidate parent's ancestors and refuses at any depth, bounded by the tree
so a pre-existing cycle fails rather than hangs (D-127 §1). `AC-205-C` is green.

✅ **The `Q70` numbering collision is resolved.** Two live questions shared one number — the trade
markup and the worker/subcontractor/supplier phone. The trade question moved to **`Q75`** on citation
count; `Q70` means the phone question and nothing else. Correcting it found `KAFF-209`'s header
naming two stories on the wrong side of the collision it was warning about.

✅ **`Q70`–`Q73` were registered 2026-09-09.** They had been cited across five slice-2 stories with no row
here at all — `Q65`–`Q69` were the same failure one wave earlier. **They now block four stories
visibly instead of invisibly**, which is the only thing registration changes.

*(`stories/backlog.md` remains the authority on epics, the slice sequence and estimates, and on
nothing else. Its state column is dead — D-119, D-122.)*

---

## Sprint 6 — the order, and why verification is first this time

**Sprint 5 put the Verifier last and the budget never reached it.** *Sequencing is not a plan when
the last item is the one that always gets cut* — and the cut item was that sprint's whole purpose.
It went first this time and **reached every story**.

**The list below is what the `V-35` pass produced, and it is worse than what it replaced.** Verifying
four stories did not shorten the sprint; it turned an unknown into eight known things, two of which
are now closed. **That is what a verification pass is for**, and it is the argument for never
scheduling one last again.

| # | Work | Owner |
|---|---|---|
| 1 | ✅ **DONE 2026-09-08 — `V-35-K` closed.** `ci.yml` now seeds between the API health check and the SPA server. Watched both ways in CI's own shape: empty database **7/25, exit 2**; seeded **25/25, exit 0**. The seed script runs unmodified on pwsh 7 / Linux, and `POST /api/setup` is **not** gated to `Development`, so Owner bootstrap works under `Staging`. **No test was weakened.** D-125 | Backend / CI |
| 2 | ✅ **DONE 2026-09-08.** The survivor that was green for the wrong reason now carries a positive control. `E2ESession.AssertPortalAccountExistsAsync` was **private to `UserScreenTests`** — that is why the later suite went without it; **moved, not copied**. A/B on the same empty database: pre-repair **PASSED having tested nothing**; repaired **FAILED** in the control | Backend / CI |
| 3 | **`V-35-F` — the rewritten `AC-125-C` cannot be executed by anyone.** This is why KAFF-125's lapse could not be lifted, and it is not the code's fault | BA |
| 4 | ✅ **DONE 2026-09-08 — `V-35-G` closed.** `landingFor` takes the `Session` and looks the landing up in a **permission → landing table**; `navLabelKeyFor`/`navPathFor` follow it. The nine-case `switch (role)` is gone. Mutation watched: swapping the table for the equivalent role switch reddens **3 of 7** — the tests pin the *mechanism*, using sessions the two designs answer differently, not the nine outputs | Frontend |
| 5 | ✅ **DONE 2026-09-08 — `V-35-I` closed**, and it found a defect in `V-35-I` itself. See the money note below | Frontend |
| 4a | ⛔ **HR's landing cannot be derived, and the gap is structural.** `ux/navigation.md` rules S-009a's permission as `ProjectTeamRead` and says *"the guard reads whatever `GET /api/auth/me` returns"* — **but the endpoint returns it to nobody.** It is `ProjectScoped`, so `CompanyWidePermissionsHeld` excludes it by construction (D-035); and HR's projects arrive as `TeamProjectEntry`, which carries **no `permissions` field at all** (D-103). **The file the ruling points the guard at is empty of the fact it is told to read.** HR's branch stays a named role check, with a test asserting the *gap* so it reddens the day somebody maps a permission onto it. **Two changes close it, neither the frontend's:** `TeamProjectEntry` carries the caller's project-scoped permissions (Backend, KAFF-105b), **or** the ruling is amended (UX/BA) | Backend **or** UX/BA |
| 6 | **Refine slice 2** — story files, criteria, Definition of Ready | BA |
| 7 | **Reconcile the two `V-34-A` passes** — a 701-line `proposals/` document and `AC-127-J`…`N` in the story, written independently, neither having read the other | BA |
| 8 | **`AC-128-B`'s assignment half.** `V-35-B`: the builder's *"cannot be built"* was **half wrong** — the Technical Office account **can** be created (`201`, one request). Only the **assignment** cannot, because `POST /api/projects` does not exist. Attach the *"even for their own projects"* clause to whatever story ships it | Backend |
| 9 | ⛔ **`V-31-A`/`V-33-F` diagnosed at last — and D-101 records a repair that never happened.** The `kaff` dev database still holds `PROBE-UNFLOORED`, D-101's own manual probe of 2026-09-02, which D-101 says was *"Row deleted; 200 healthy restored."* **It was not deleted.** Verified 2026-09-08: the account is present and carries **the only two postings in the entire database** — the −4,000 overdraw that proved the exposure. **It cannot be cleaned up:** postings are append-only and trigger-protected so the account cannot be deleted, and `trg_accounts_configuration_immutable` is `BEFORE UPDATE` so the flag cannot be repaired. **The database has to be recreated.** The API has refused to start against `kaff` since 2026-09-02; CI never sees it because CI builds fresh | Architect / Backend |
| 10 | ✅ **DONE 2026-09-08 — all three.** `Bab.SetParent` now walks the candidate parent's ancestors and refuses a cycle **at any depth**, bounded by the size of the tree so a pre-existing cycle fails rather than hangs; watched red first at **3 of 5**. The false HR test name renamed under SM-33, citations moved. `/api/users` added to the audit-absence loop, and its name's count dropped — **the count was already false**: ten iterations over three routes is thirty reads. D-127 | Backend |

### ⛔ Money is pre-exposed to bidi today, and `V-35-I` said it was not

**Found 2026-09-08 by writing the check down rather than reading the report.** `V-35-I` concluded
*"money is not pre-exposed"* from `Intl.NumberFormat('ar-EG')` — **a different call from the one this
codebase ships.** `formatMoney` passes `style: 'currency'`, and **the currency style is what injects
the marks.** Measured all four:

| call | result | `U+200F` |
|---|---|---|
| `Intl.NumberFormat('ar-EG')` — what the report checked | `١٬٢٣٤٫٥` | no |
| `formatNumber` (`ar-EG-u-nu-latn`, plain) | `1,234.5` | no |
| **`formatMoney` (`ar-EG-u-nu-latn` + currency EGP)** | `‏1,234.50 ج.م.‏` | **yes** |
| `ar-EG` + currency EGP | `‏١٬٢٣٤٫٥٠ ج.م.‏` | **yes** |

**The mark lands on the leading character — the one first-strong reads.** Nothing shipped is wrong:
`formatMoney` has no call site yet. ⛔ **But slice 3's first amount inside a `<bdi>` will need
`dir="ltr"` for exactly the reason the audit timestamp does.** A Verifier finding was wrong, and the
agent that found it said so instead of building on it.

⛔ **Struck from this sprint: the first-strong exposure on the client and user lists.** `V-35-H`
disproved it. A registered `0100-123-4567` **does not reorder** — every `<bdi>` on both lists resolves
`ltr`. **`dir="auto"` with no strong character falls back to `ltr`, not to the parent**, so `<bdi>`
alone already handles `::1`, `/api/auth/sign-in` and a slash-separated date. What reorders is an
*unisolated* run, which is what `<bdi>` exists to prevent. **It was scheduled work that should not be
done, on a mechanism that was wrong** — and the wrong mechanism is written into shipped source, which
is item 5.

⚠️ **The SPA must be up on 4200 and the database seeded before any E2E run**, and the suite
**skips silently** when nothing answers — a green run that proves nothing. That is the local half of
item 1.

---

## ⛔ Blocked on Karim — seven questions, and they gate slice 3, not slice 2

Full text in [stories/questions-for-karim.md](stories/questions-for-karim.md).

⛔ **This heading used to say slice 2 needed none of them. That was wrong** — see the slice-2 section
above. **`Q12` blocks five of the six refined stories**, and `stories/backlog.md` had said so since
the slice was estimated. **Five more questions were raised by the refinement itself** — `Q60` the
spreadsheet's shape · `Q61` what one bad row does to the good ones · `Q62` what a second import does ·
`Q63` the catalogue list's order (ask with `Q59`) · `Q64` HR's field list — plus **`N12`** for the
Architect.

| Q | In one line | Blocks |
|---|---|---|
| **Q14** | At extract 1 the client pays an extra 75,000 for material on site. Confirms a mechanic §15's own arithmetic implies — it does **not** choose a ledger; D-034 already ruled تشوينات a liability | §15's fixture |
| **Q15** | Which banks — QNB, CIB, الأهلي, others? | `KAFF-316`/`317` |
| **Q16** | Does any bank account have an overdraft? | the non-negative floor's shape |
| **Q29** | Subcontractor/supplier withholding — per job or per firm? | `KAFF-318` |
| **Q54** | Retention period for audited IP addresses. **Mechanism ruled (D-072 §3); the number is not** | **KAFF-129** |
| **Q58** | §15 recovers تشوينات at 45,000 then 30,000 and **nothing in `spec.md` produces those numbers** — the only column in §15 with no stated rule | slice 5's calculator |
| **Q59** | What order the user and client lists are in — including whether alif with and without hamza collate together | a QA case, no story |

Also open: **Q12**, **Q13**, **Q30**, **Q57**, `Q-N10-1`, `Q-N10-2b`, `Q-N10-3`.

---

## Still Nabil's alone

- **The slice-order decision.** Treasury's first two story files now exist and are pullable.
  **Starting slice 3 before slice 2 has not been decided**, and no agent may decide it.
- **The seven questions above.** They travel through Nabil to Karim and come back as D-numbers.
- **`AC-125-C` — ratify or reject the rewrite.** `V-35-E`: the ruling that it was a *criterion
  defect, not a second lapse* is **right on D-096** — only `7461332`, KAFF-125's own build, ever added
  that rendering, so no commit moved behaviour under the verdict. **But the 2026-09-04 Verifier had
  reserved this call to Nabil in writing** — *"only Nabil can write it… It should not close by a
  Verifier"* — and a BA closed it three days later. **Keep the text; it needs Nabil's ratification.**
- **Acceptance.** §4. **84 of 127 points, 2026-09-09.** It moves when Nabil moves it, and it did.

---

## Gates, as last measured

At `a21892e`, 2026-09-07. **Re-measure rather than quote** — every one of these has been wrong once.

| Gate | | Measured by |
|---|---|---|
| Build, Debug **and** Release, `-warnaserror` | **0 / 0** | Scrum Master |
| `dotnet format --verify-no-changes` | **clean** | Scrum Master |
| Domain.Tests | **132 / 132, exit 0** — 127 + 5 for the cycle guard | Backend, 2026-09-08 |
| Api.Tests | **317 / 317** | Scrum Master |
| Citations | **1181 / 0 / 0, exit 0** | Scrum Master, after the `V-35-L` repair |
| SPA production build | clean | Verifier |
| vitest | **18 / 18, exit 0** — 8 baseline, +7 landing, +3 i18n. ⚠️ **Still no component test anywhere** | Frontend, 2026-09-08 |
| E2E (Playwright) | **25 / 25 seeded, exit 0** · 7 / 25 unseeded, exit 2 | CI agent, 2026-09-08 |

✅ **The E2E row means something again.** `ci.yml` seeds as of 2026-09-08, so CI measures the same
thing a local run does. The unseeded figure is kept **as the control** — it is what the gate scores
when the data is absent, and it is the number that was hidden for four days.

⚠️ **Nobody has confirmed the `e2e` job is a *required* check.** Branch protection is not in the
repository. **A seeded job that nothing blocks on is still advisory** — Nabil's to confirm.

⚠️ **Always read the citation checker's EXIT CODE, never its summary line.** On 2026-09-07 the Scrum
Master reported `1183 / 0 / 0` and pushed while the gate was **red with 2 broken, exiting 1** — the
output was piped through `Select-Object`, which discarded the detail lines, and the exit code was
never checked. `V-35-L` caught it, and it caught it on both sources at once: the Scrum Master's figure
and the sprint-5 close note agreed with each other and were both wrong.
`powershell -NoProfile -File scripts/check-citations.ps1; $LASTEXITCODE`
| `Kaff:ForwardedProxyHops = 2` | ⚠️ **unwitnessed** against real staging | — |

⚠️ **A Release build reporting `MSB3021` / `MSB3027` is a file lock from a running `Kaff.Api`, not a
code error.** Identify the exact PID and stop only that one — a broad process match has killed an
unrelated editor process twice on this board.

⚠️ **Test projects are `Kaff.Domain.Tests.csproj` and `Kaff.Api.Tests.csproj` and need `-c Release`.**
`dotnet test` on the folder path runs **zero tests and exits 5**, which reads as a broken suite and is
not. Use `/run-kaff-erp`. Also: `--filter` matches nothing here — use `--filter-class` /
`--filter-method`.

---

<!-- BEGIN GENERATED - tools/status.ps1 - do not edit by hand -->
*Generated by `tools/status.ps1` from the `<!-- kaff -->` trailers. **Edit a story file, then re-run** — hand edits here are overwritten.*

## Points by slice

| | slice 1 | slice 2 | slice 3 |
|---|---:|---:|---:|
| ✅ **ACCEPTED** — Nabil ran the demo script (`process/agile.md` §4) | 84 | 0 | 0 |
| 🔵 VERIFIED — a Verifier gave a verdict, and it still stands | 19 | 22 | 0 |
| ⛔ LAPSED — had a verdict; later code moved under it (D-096) | 3 | 0 | 0 |
| 🔴 REJECTED — a Verifier looked, and it did not pass | 0 | 8 | 0 |
| ⚫ NOT-BUILT — cut, and not yet Ready | 0 | 23 | 5 |
| 🔻 DEFERRED — carried out of this slice, to a named place | 21 | 0 | 0 |
| **total** | **127** | **53** | **5** |

## Every story

| Story | Slice | Pts | State | Verdict | At | On | |
|---|---:|---:|---|---|---|---|---|
| [KAFF-100](stories/slice-1-foundation/KAFF-100-bootstrap-the-first-owner.md) | 1 | 5 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Bootstrap the first Owner through a one-time setup screen |
| [KAFF-101a](stories/slice-1-foundation/KAFF-101a-sign-in-api.md) | 1 | 5 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Sign in, and the server sets an `HttpOnly` session cookie |
| [KAFF-101b](stories/slice-1-foundation/KAFF-101b-sign-in-screen.md) | 1 | 3 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | The staff sign-in screen, and where each role lands after it |
| [KAFF-102](stories/slice-1-foundation/KAFF-102-sign-out.md) | 1 | 2 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Sign out |
| [KAFF-103](stories/slice-1-foundation/KAFF-103-set-first-password.md) | 1 | 5 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Change the temporary password on first sign-in |
| [KAFF-104](stories/slice-1-foundation/KAFF-104-reset-forgotten-password.md) | 1 | 5 | 🔻 DEFERRED | none | `-` | 2026-09-07 | Reset a forgotten password with an Owner-generated link |
| [KAFF-105a](stories/slice-1-foundation/KAFF-105a-api-me-identity.md) | 1 | 2 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | GET /api/auth/me` returns who I am and what I may do |
| [KAFF-105b](stories/slice-1-foundation/KAFF-105b-api-me-project-list.md) | 1 | 5 | 🔵 VERIFIED | PASS | `-` | 2026-08-30 | GET /api/auth/me` returns the projects I reach, and how I reach them |
| [KAFF-106](stories/slice-1-foundation/KAFF-106-owner-creates-a-user.md) | 1 | 5 | ✅ ACCEPTED | CONDITIONAL | `1d04bde` | 2026-09-09 | The Owner creates a user with a role and a department |
| [KAFF-107](stories/slice-1-foundation/KAFF-107-hr-role-is-bound-to-the-hr-department.md) | 1 | 2 | ⚪ FOLDED | none | `-` | 2026-08-22 | An HR user cannot be created or moved outside the HR department |
| [KAFF-108](stories/slice-1-foundation/KAFF-108-move-a-user-between-departments.md) | 1 | 3 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Move a user between departments |
| [KAFF-109](stories/slice-1-foundation/KAFF-109-change-a-users-role.md) | 1 | 5 | ✅ ACCEPTED | CONDITIONAL | `1d04bde` | 2026-09-09 | Change a user's role |
| [KAFF-110](stories/slice-1-foundation/KAFF-110-deactivate-a-user.md) | 1 | 5 | ✅ ACCEPTED | CONDITIONAL | `1d04bde` | 2026-09-09 | Deactivate a user, and their access ends on the next request |
| [KAFF-111](stories/slice-1-foundation/KAFF-111-a-deactivated-users-assignments.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Deactivating a user revokes their project assignments |
| [KAFF-112](stories/slice-1-foundation/KAFF-112-reactivate-a-user.md) | 1 | 3 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Reactivate a user, who comes back with nothing |
| [KAFF-113](stories/slice-1-foundation/KAFF-113-assign-a-user-to-a-project.md) | 1 | 5 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Assign a user to a project, with seniority for site engineers |
| [KAFF-114](stories/slice-1-foundation/KAFF-114-revoke-a-project-assignment.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Revoke a project assignment without losing who could act when |
| [KAFF-115](stories/slice-1-foundation/KAFF-115-project-team-panel.md) | 1 | 8 | 🔻 DEFERRED | none | `-` | 2026-09-07 | The project team panel is built from assignment rows, not from the access check |
| [KAFF-116](stories/slice-1-foundation/KAFF-116-audit-records-how-access-was-granted.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Every audit record says how the actor reached the project |
| [KAFF-117](stories/slice-1-foundation/KAFF-117-read-the-audit-trail.md) | 1 | 5 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | The Owner reads the audit trail, and nobody else does |
| [KAFF-118](stories/slice-1-foundation/KAFF-118-every-slice-1-change-is-audited.md) | 1 | 3 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Every state change in slice 1 writes an audit record |
| [KAFF-119](stories/slice-1-foundation/KAFF-119-register-a-client.md) | 1 | 5 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Register a client, with a generated code and a duplicate-phone warning |
| [KAFF-120](stories/slice-1-foundation/KAFF-120-individual-clients-do-not-withhold.md) | 1 | 2 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | An individual's contract cannot carry a withholding rate, and nor can the individual |
| [KAFF-121](stories/slice-1-foundation/KAFF-121-edit-a-clients-contact-details.md) | 1 | 3 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Edit a client's name and contact details |
| [KAFF-122](stories/slice-1-foundation/KAFF-122-corporate-client-withholding.md) | 1 | 0 | ⚪ SUPERSEDED | none | `-` | 2026-08-21 | Set a corporate client's withholding category and tax registration number |
| [KAFF-123](stories/slice-1-foundation/KAFF-123-archive-a-client.md) | 1 | 2 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Archive a client |
| [KAFF-124](stories/slice-1-foundation/KAFF-124-list-and-search-clients.md) | 1 | 2 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Find a client by name or phone |
| [KAFF-125](stories/slice-1-foundation/KAFF-125-staff-shell.md) | 1 | 3 | ⛔ VERIFIED | LAPSED | `8ea9258` | 2026-09-06 | The staff shell: session resolution, chrome, and role-based landing |
| [KAFF-126](stories/slice-1-foundation/KAFF-126-client-screens.md) | 1 | 8 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | The client screens |
| [KAFF-127](stories/slice-1-foundation/KAFF-127-user-management-screens.md) | 1 | 8 | ✅ ACCEPTED | CONDITIONAL | `1d04bde` | 2026-09-09 | The user-management screens |
| [KAFF-128](stories/slice-1-foundation/KAFF-128-audit-trail-screen.md) | 1 | 3 | ✅ ACCEPTED | CONDITIONAL | `1d04bde` | 2026-09-09 | The audit trail screen |
| [KAFF-129](stories/slice-1-foundation/KAFF-129-partition-audit-records-by-month.md) | 1 | 8 | 🔻 DEFERRED | none | `-` | 2026-09-07 | Partition `audit_records` by month, from the start |
| [KAFF-200](stories/slice-2-masters/KAFF-200-import-the-catalogue-from-excel.md) | 2 | 5 | ⚫ NOT-BUILT | none | `-` | 2026-09-08 | Import the catalogue from Excel at setup, loading the good rows and reporting the rest |
| [KAFF-201](stories/slice-2-masters/KAFF-201-re-importing-is-not-a-sync.md) | 2 | 2 | ⚫ NOT-BUILT | none | `-` | 2026-09-08 | Re-importing is not a sync — a second import is a deliberate, reviewed act |
| [KAFF-202](stories/slice-2-masters/KAFF-202-create-and-edit-a-catalogue-item.md) | 2 | 3 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | Create and edit a catalogue item |
| [KAFF-203](stories/slice-2-masters/KAFF-203-find-a-catalogue-item-by-code-or-description.md) | 2 | 3 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | Find a catalogue item by code or description |
| [KAFF-204](stories/slice-2-masters/KAFF-204-the-bab-tree-with-default-markup.md) | 2 | 5 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | The باب tree, carrying each trade's default markup |
| [KAFF-205](stories/slice-2-masters/KAFF-205-reparent-a-bab-and-move-an-item.md) | 2 | 3 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | Re-parent a باب, and move an item between أبواب |
| [KAFF-206](stories/slice-2-masters/KAFF-206-archive-a-catalogue-item.md) | 2 | 3 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | Archive a catalogue item without breaking what already references it |
| [KAFF-207](stories/slice-2-masters/KAFF-207-employee-register.md) | 2 | 5 | 🔴 BUILT | REJECTED | `7117704` | 2026-09-11 | Employee register — exactly one record per costed person |
| [KAFF-208](stories/slice-2-masters/KAFF-208-nobody-appears-in-both-populations.md) | 2 | 3 | 🔴 BUILT | REJECTED | `7117704` | 2026-09-11 | Nobody appears in both populations: day labour and salaried |
| [KAFF-209](stories/slice-2-masters/KAFF-209-register-a-worker-from-site.md) | 2 | 5 | ⚫ NOT-BUILT | none | `-` | 2026-09-09 | Register a worker from site, deduplicated by phone |
| [KAFF-210](stories/slice-2-masters/KAFF-210-worker-engagement-history.md) | 2 | 3 | ⚫ NOT-BUILT | none | `-` | 2026-09-09 | Worker engagement history, day rate, frequency and rating |
| [KAFF-211](stories/slice-2-masters/KAFF-211-subcontractor-master-with-rates.md) | 2 | 5 | ⚫ NOT-BUILT | none | `-` | 2026-09-09 | Subcontractor master with rates |
| [KAFF-212](stories/slice-2-masters/KAFF-212-supplier-master.md) | 2 | 3 | ⚫ NOT-BUILT | none | `-` | 2026-09-09 | Supplier master — one account serving many projects |
| [KAFF-213](stories/slice-2-masters/KAFF-213-archive-a-bab.md) | 2 | 3 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | Archive a باب |
| [KAFF-214](stories/slice-2-masters/KAFF-214-unarchive-a-catalogue-item.md) | 2 | 2 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | Un-archive a catalogue item |
| [KAFF-300](stories/slice-3-treasury/KAFF-300-the-section-15-worked-example.md) | 3 | 5 | ⚫ NOT-BUILT | none | `-` | 2026-09-07 | The §15 worked example as a fixture — present and failing before anything else is built |

<!-- END GENERATED -->

---

## The map — which file is truth, which is history

The rule that stops the drift coming back: **only one file per fact, and history never claims a
present tense.**

### TRUTH — read these, they are current

| File | Is the only authority on |
|---|---|
| **`STATUS.md`** | Where the project stands today. This file. |
| **`spec.md`** | The business. `CLAUDE.md`: if code and spec disagree, **spec wins**. |
| **`CLAUDE.md`** | How to work here. Prohibitions. |
| **`decisions.md`** | Why things are the way they are. Append-only, `D-nnn`, never rewritten. |
| **`stories/slice-*/KAFF-*.md`** | One story's criteria **and its state** — the `<!-- kaff -->` trailer on line 3 is what `STATUS.md` is generated from. |
| **`stories/questions-for-karim.md`** | Open questions for the client. The **one** register — `qa/questions.md` and `ux/questions.md` merged into it (SM-31). |
| **`qa/risk-register.md`** | `RSK-nn` exposures. |
| **`process/agile.md`** · **`agents.md`** | The ceremonies, and who is who. |
| **`qa/slice-1/test-cases.md`** | `TC-` cases. |

### HISTORY — dated records. Never read these for current state.

`meetings/**` · `qa/slice-1/verification-*.md` · `qa/slice-1/story-review-*.md` ·
`proposals/**` · every superseded block inside a story file.

Each is true **as of its date** and was never updated afterwards. That is correct behaviour for a
record and wrong behaviour for a status.

### ⚠️ DEMOTED — was treated as truth, is not any more

| File | Now |
|---|---|
| **`stories/backlog.md`** | **State column is dead.** It drifted on KAFF-116, KAFF-108 and KAFF-113 inside one week (D-119), and a figure read off it was reported to Nabil and was wrong (D-122). Keep it for **epics, slice sequence and estimates**; take state from `STATUS.md`. |
| **`qa/questions.md`** · **`ux/questions.md`** | Superseded by `stories/questions-for-karim.md`. |
| **`stories/ac-id-map.md`** | Snapshot of 2026-08-24. Criteria live in the story files. |

---

## How to keep this file honest

```powershell
powershell -NoProfile -File tools/status.ps1          # regenerate after editing any story trailer
powershell -NoProfile -File tools/status.ps1 -Check   # exit 1 if stale
```

**The trailer is the fact; this file is a view of it.** To change a story's state, edit its
`<!-- kaff ... -->` line and re-run. Do not hand-edit the generated block — it is overwritten.

A story with **no** trailer appears as `❓ UNKNOWN` rather than vanishing, because a missing row
reads as *nothing to do* and that is the failure this whole file exists to prevent.

**States:** `NOT-BUILT` · `READY` · `COMMITTED` · `BUILT` · `DELIVERED` · `VERIFIED` · `ACCEPTED` ·
`DEFERRED` · `FOLDED` · `SUPERSEDED`
**Verdicts:** `none` · `PASS` · `CONDITIONAL` · `LAPSED` · `REJECTED`

**`DEFERRED`** was added 2026-09-07, when slice 1 closed with 21 points never built. It means
*carried out of this slice to a named place, with its file and criteria intact* — **not** done and
**not** dropped. It exists because the alternative was marking unbuilt work `done`, and a board that
reports a safety it does not have is the one defect this project keeps paying for.

⚠️ **Edit this file with a text editor or the file tools, never by piping through PowerShell.**
The tail of this file carried mojibake (`Â·`, `â€”`) from 2026-09-06 to 2026-09-07 because a
`Get-Content` / `WriteAllText` round-trip in Windows PowerShell 5.1 reads ANSI and writes UTF-8.
Repaired 2026-09-07.
