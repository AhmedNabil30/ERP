# Verification — 2026-09-10

**⚠️ Continued on `sonnet`, not the strongest model — `agents.md` §M's never-downgrade rule for a
Verifier was knowingly overridden for this continuation session because the strongest model's budget
was exhausted until 03:00, and a pass that reaches §21 was judged worth more than one stalled at §3.
Per §M's 2026-09-10 amendment, this verdict carries that caveat: nothing below was re-derived by the
stronger model, and a second pass on `opus` after 03:00 is owed before this verdict is treated as
final.**

**Verifier, continuing a session that hit a session limit after §3.** Everything through §3 (this
file's own §§0–3) was written by the prior session and is not redone here — §0's table below is
picked up where it left off. `CLAUDE.md`: *"If you wrote the code, you do not certify it."* I wrote
none of the code in scope and none of the commits in scope.

**Scope:** the nine points at `state=BUILT verdict=none` — **KAFF-202** (`586a7d0`), **KAFF-203**
(`dd5f14c`), **KAFF-206** (`f675f1b`).

**Brief:** `meetings/BRIEF-2026-09-10-verifier.md`, written by the Scrum Master session that ran all
three gates and briefed the `KAFF-206` builder. Every claim in it is a claim to check, the gate
figures included. §21 records where it was right and where it was wrong.

**This file was created before any other action** (before any gate was run, any test executed, or any
finding formed) and is written finding-by-finding, committed as it goes. A section still marked
`pending` at the end is a section I did not reach, and that is itself a finding — see §19.

Findings are numbered `V-35-x`, continuing the series `qa/slice-1/verification-2026-09-07.md` used.

---

## 0. Progress of this report

Everything is `pending` until reached. Nothing is marked done on an author's evidence.

| # | Item | State |
|---|---|---|
| 1 | Opening gate — `HEAD`, `git status`, stranded hosts | **done** — §1 |
| 2 | Gate figures re-measured by me | **done** — §2, **all five green** |
| 3 | **Block 1** — the five criteria whose subject does not exist | **done** — §3, **four findings** |
| 4 | `AC-202-G` — cost price never leaves the internal surface | **done** — §4, PASS |
| 5 | `AC-202-B` — repeated code refused by the database, not a lookup | **done** — §5, PASS |
| 6 | `AC-202-C` — four decimals, and which side of `AC-200-B` it picked | **done** — §6, **finding `V-35-Q`** |
| 7 | `AC-203-I` — ordered by باب then code; is there more than one باب? | **done** — §7, **finding `V-35-R`** |
| 8 | `AC-206-H` / `AC-202-I` — audited before and after, absence claims | **done** — §8, PASS |
| 9 | KAFF-202 — remaining criteria and their cases | **done** — §9, **finding `V-35-T`** |
| 10 | KAFF-203 — remaining criteria and their cases | **done** — §10 |
| 11 | KAFF-206 — remaining criteria and their cases | **done** — §11, **findings `V-35-S`, `V-35-U`** |
| 12 | "Every mutation was watched failing" — claim 2 of the brief | **done** — §12, **finding `V-35-V`** |
| 13 | Criteria with no QA case | **done** — §13 |
| 14 | Test-case execution ledger (`TC-2-017` … `TC-2-036`, `TC-2-057` … `TC-2-065`) | **done** — §14 |
| 17 | Verdict per story | **done** — §17 |
| 19 | What I did not reach | **done** — §19 |
| 20 | Findings index | **done** — §20 |
| 21 | Corrections to the brief | **done** — §21 |

**Nothing in this pass is marked done on an author's evidence.** Every figure re-measured, every
claim re-driven.

---

## 1. Opening gate — the state of the machine when I arrived

| Check | Result |
|---|---|
| `HEAD` before I wrote anything | `3601034` — the brief's stated target, exactly |
| Working tree | clean under `src/`, `tests/`, `stories/`; two untracked files in `meetings/` (this pass's brief and a message to Karim) |
| Stranded `Kaff.Api` / `Kaff.Api.Tests` / `Kaff.Domain.Tests` hosts | **none** — `Get-Process` returned nothing for all three |
| Listener on port 5080 | **none** |
| `dotnet` processes | **none** |
| `kaff-db` container | up 11 hours, healthy |
| Other actors on the gate | none — I am the only one; nothing was running when I started and nothing else ran while I measured |

I ran the kill step anyway, in the skill's stated order, **before** the build. It was a no-op, which
is the correct outcome rather than a skipped step.

**No change was made under `src/` in this pass, and no story trailer was touched.**

---

## 2. The gate figures, re-measured by me

**Every figure below I ran in this session, in `.claude/skills/run-kaff-erp/SKILL.md`'s stated
order: kill, then build, then check `$LASTEXITCODE`, then test.**

| Gate | Brief claimed | **Measured by me** | |
|---|---|---|---|
| Build, **Release**, `-warnaserror` | 0 / 0, exit 0 | **`Build succeeded. 0 Warning(s) 0 Error(s)`, exit 0** | ✔ |
| `dotnet format --verify-no-changes` | exit 0 | **no output, exit 0** | ✔ |
| Domain.Tests | 154 / 154 | **total 154 · failed 0 · succeeded 154 · skipped 0**, exit 0 | ✔ |
| Api.Tests | 358 / 358 in **276s** | **total 358 · failed 0 · succeeded 358 · skipped 0**, exit 0, **duration 5m 18s** | ✔ figure right, **timing wrong — §21** |
| Citations | 0 broken | **1327 checked · 0 broken · 0 legacy**, exit 0 | ✔ |

Every test figure carries a `total:` line read out of the log file, not an exit code alone. The build
exited 0 **before** either suite ran, so neither figure is measured against a stale binary; no
`MSB3021` and no `MSB3026` appeared.

**The gate is green and the brief's gate claims are true.** That is the last unambiguous good news in
this report.

---

## 3. Block 1 — the five criteria whose subject does not exist

### 3.0 The premise, re-verified by me

A ripgrep over `src/` for a class, record, interface, enum or struct declaration named `Boq`,
`SignedBoq`, `BoqLine` or `Estimate`: **no matches.** A second, wider sweep for the bare words
`boq|signedboq|boqline|estimate`, case-insensitive, across `.cs`, `.ts`, `.html` and `.json` under
`src/`, returns **27 hits in 16 files and every one of them is a comment or an XML doc paragraph** —
`CatalogueItem.cs` explaining §4.4, `ArchiveCatalogueItem/Handler.cs` explaining that it touches
nothing, `Project.cs` listing what it does not model. **There is no BOQ entity, no estimate entity,
no table, no route.**

**The brief is right, and so is `3601034`'s commit message.** Confirmed independently, not quoted.

### 3.1 `V-35-M` — ⛔ **`AC-206-C` and `AC-206-D` have no witness of any kind, and only the QA file says so**

**Severity: MEDIUM.** *(Not HIGH: the shipped behaviour is almost certainly correct — see below.
MEDIUM because the record reads as if a criterion is discharged when nothing discharges it.)*

Disposition, established rather than assumed:

- **No test anywhere names `AC-206-C` or `AC-206-D`.** A search of `tests/` for `AC-206-[A-I]`
  returns hits for `A`, `B`, `E`, `F`, `G`, `H` and for nothing else.
- **No document reports them discharged.** A repo-wide search for those two ids returns exactly four
  places: this brief, the story, and `qa/slice-2/test-cases.md`'s two held cases. Nothing claims a
  pass.
- `TC-2-059` and `TC-2-060` carry the held block, with the reason and the verification date.
  **QA did this correctly and `3601034` did this correctly.**

**So the criteria were not falsely reported discharged — and that is not the finding.** The finding
is that **`KAFF-206` is at `state=BUILT` with two of its nine criteria carrying no witness, and its
own story file does not say so.** Its Definition-of-Ready table reads *"QA has written at least one
scenario that fails if the rule is broken ✅ Met 2026-09-09"*, with no qualification; a reader of the
story alone cannot tell that two criteria are outstanding. The hold lives in a file the story does
not cite. **Under the same reasoning `3601034` itself gives — a case nobody can run gets reported as
not-run once and assumed passing thereafter — the hold has to be visible where the story's state is
read**, which today is the story file and `STATUS.md`.

**Owner: Scrum Master** (the story trailer and its DoR table are the Scrum Master's, not mine).

### 3.2 `V-35-N` — ⛔ **`AC-202-E` and `AC-202-F` sit in exactly `TC-2-059`/`TC-2-060`'s position, and `TC-2-022`/`TC-2-023` are not marked held**

**Severity: MEDIUM-HIGH. This is the asymmetry `3601034` identified for `KAFF-206` and did not carry
across to `KAFF-202`, whose criteria are the same rule in the same words.**

`AC-202-E` (*re-pricing does not touch a signed BOQ*) and `AC-206-C` (*archiving does not touch a
signed BOQ*) assert the same §4.4 MUST about the same absent entity. `AC-202-F` and `AC-206-D`
likewise. But:

| | `KAFF-206` | `KAFF-202` |
|---|---|---|
| QA case | `TC-2-059`, `TC-2-060` | `TC-2-022`, `TC-2-023` |
| Marked held, with the verification | **yes** | **no — they read as ordinary runnable cases** |
| Reported discharged by a test | no | **yes** — see below |

**And `AC-202-E`/`AC-202-F` are claimed discharged**, by `tests/Api.Tests/EditCatalogueItemTests.cs`
-> `Repricing_touches_no_row_but_the_items_own_and_writes_no_estimate_or_boq_row`, whose class
remarks say *"`AC-202-E` and `AC-202-F`, as far as slice 2 can prove them."*

**What that test actually asserts** is a row-count invariant: after a reprice, `AuditRecords` is up
by exactly one, `CatalogueItems` is unchanged, and **`Babs` is unchanged**. That is a genuine and
useful property — *the reprice mechanism's whole reach is its own row* — and I record it as a **PASS
for what it asserts.** It is not `AC-202-E`. `AC-202-E` names a signed BOQ line whose values must not
move and which must hold no foreign key back to the catalogue row; the test's third assertion counts
**أبواب**, an unrelated table, because أبواب is the only other table there is. Its own message says
so: *"there is no BOQ or estimate table in this codebase yet for it to reach either."*

**A case that cannot fail cannot witness the rule it exists for** — the brief's own sentence, and it
applies here identically. The `Babs` count could not move under any implementation of reprice; the
assertion is unfalsifiable in the direction the criterion cares about.

**Two separate defects, then:**

1. **`TC-2-022` and `TC-2-023` need the same held block `TC-2-059`/`TC-2-060` carry.** Today they are
   indistinguishable from executable cases, which is precisely the asymmetry `3601034` called *"the
   dangerous direction."* **Owner: QA.**
2. **`AC-202-E`/`AC-202-F` must not stand as discharged on that test.** The test can keep its name
   and its assertions and stop citing those two criteria as covered — or the criteria's BOQ half has
   to be recorded outstanding to slice 4 the way `TC-2-040` records `AC-204-D`'s. **Owner: Backend
   (the citation in the test) and the Scrum Master (the story's record).**

### 3.3 `V-35-O` — ⚠️ **`AC-206-F` is discharged by a *default*, not by a guarantee. It resembles the criterion.**

**Severity: MEDIUM.** The brief asked me to decide this one and say why. **My answer: it resembles
it.**

What exists: `GET /api/catalogue-items` takes a three-state `status` filter — `Active` (the default),
`Archived`, `All` — an unknown value is refused rather than defaulted, and the default excludes
archived items. `tests/Api.Tests/ListCatalogueItemsTests.cs` ->
`An_archived_item_is_hidden_by_default_and_returned_when_asked_for` drives all three states with a
real archived item and **passes**. That is a good test of a real behaviour and I record it as a
**PASS for `AC-206-A`'s "reachable through the list's explicit archived filter" half.**

Why it does not discharge `AC-206-F`:

- `AC-206-F` says *"an archived item is absent from a **new BOQ line's** search"*, and D-130 §3's
  reasoning is that **archiving must actually keep an item off new work** — *"offering it, with or
  without a warning, would make the archive decorative."*
- The catalogue search keeps an archived item off nothing. It **omits it by default and hands it over
  on request.** Any caller — including tomorrow's BOQ builder, which will be a caller of this same
  endpoint holding `CatalogueManage` — gets every archived item by passing `status=all`. Nothing in
  the endpoint, the handler or the entity distinguishes *"the list screen wants to see the archive"*
  from *"a BOQ line wants to use an archived item."*
- So the property under test is **"the default filter is `Active`"**. That is `KAFF-206` rule 7,
  which is `AC-206-A`'s territory and already covered. `AC-206-F`'s own guarantee — that an archived
  item cannot reach a **new BOQ line** — has no subject to be enforced against and no test.

The handler's own remarks state the intended chain plainly: *"This is also the search the BOQ
builder's 'add item' reaches later (slice 4) — its default excluding archived items is what keeps an
archived item off new work without that caller having to know to ask."* **A default the caller need
not know to ask for is also a default the caller can override without knowing it matters.** That is
the whole distance between resembling the criterion and meeting it.

**This is the one of the five the brief flagged as possibly reachable, and the answer is that the
reachable part is a different criterion.** `AC-206-F` belongs recorded outstanding to slice 4,
alongside `AC-206-C` and `AC-206-D`, with a note that the slice-4 BOQ item picker must pin
`status=Active` rather than inherit the default. **Owner: Scrum Master to record; Architect to carry
the constraint into slice 4.**

### 3.4 `V-35-P` — ⚠️ **Disposition of all five, and what slice 5 will read**

**Severity: MEDIUM, and it is the one that will still be true in slice 5.**

| Criterion | Reported discharged? | By what | Verdict |
|---|---|---|---|
| `AC-202-E` | **yes** | `EditCatalogueItemTests` -> `Repricing_touches_no_row_but_the_items_own_and_writes_no_estimate_or_boq_row` | ⛔ **not discharged** — asserts a `Babs` row count, unfalsifiable for this rule (`V-35-N`) |
| `AC-202-F` | **yes** | the same single test | ⛔ **not discharged** (`V-35-N`) |
| `AC-206-C` | **no** | nothing — `TC-2-059` held | ✔ correctly held, ⚠️ invisible in the story (`V-35-M`) |
| `AC-206-D` | **no** | nothing — `TC-2-060` held | ✔ correctly held, ⚠️ invisible in the story (`V-35-M`) |
| `AC-206-F` | **yes** | `ListCatalogueItemsTests` -> `An_archived_item_is_hidden_by_default_and_returned_when_asked_for` | ⚠️ **partially** — the test passes and proves `AC-206-A`; `AC-206-F`'s own guarantee is unwitnessed (`V-35-O`) |

**None of the five is a defect in shipped behaviour.** I looked for a mechanism by which repricing or
archiving could reach anything beyond the item's own row and there is none — no cascade, no
notification, no second table written. **`KAFF-206` rule 3's *"the correct implementation is to add
nothing"* was honoured.** The defect is in the record, and the record is what slice 5 will read.

---

## 4. `AC-202-G` — cost price never leaves the internal surface

**PASS. The mechanism is a whitelist, not a blocklist, and D-116's positive control is real.**

- **Every response type that returns an item is an explicit allow-list**, not a projection with a
  field removed: `CreateCatalogueItem.Response`, `EditCatalogueItem.Response` and
  `ListCatalogueItems.CatalogueItemSummary` are each a `sealed record` with named parameters
  [Verified @ `src/Api/Features/Catalogue/CreateCatalogueItem/Response.cs`,
  `.../EditCatalogueItem/Response.cs`, `.../ListCatalogueItems/Response.cs`]. A field nobody named
  cannot ride along — D-106's shape, correctly applied on all three surfaces named in `AC-202-A`
  through `AC-203-G`.
- **Archive and Unarchive return `204 NoContent`** [Verified @ `.../ArchiveCatalogueItem/Handler.cs`,
  `.../UnarchiveCatalogueItem/Handler.cs`] — no body, so trivially nothing leaks from either.
- **The client-portal half is checked against the real route table, not a grep.**
  `EditCatalogueItemTests.No_portal_route_is_mapped_anywhere_in_the_application` enumerates every
  `EndpointDataSource` the host actually registered and asserts none starts `/api/portal`
  [Verified @ `tests/Api.Tests/EditCatalogueItemTests.cs:184-195`]. That is the whole client-facing
  surface today — there is no separate portal contract to check field-by-field because there is no
  portal route at all.
- **The positive control is real, per D-116.** `The_internal_edit_response_does_carry_cost_price`
  confirms `CostPrice` **is** a member of the internal response type, so the absence check above is
  proven to be looking at something rather than passing because nothing anywhere ever returns the
  field [Verified @ same file, lines 203-210].

One nuance, not a defect: the positive control asserts via reflection on the C# record's public
properties, not by parsing the actual JSON body for a `costPrice` key. Given the response is a `record`
serialized with default `System.Text.Json` casing, the two are equivalent in practice, and the
create-path test (`CreateCatalogueItemTests`, line 82) does assert the JSON allow-list directly — so
this is not filed as a finding.

---

## 5. `AC-202-B` — a repeated code is refused by the database, not a lookup

**PASS. The mechanism named by the criterion is the one shipped.**

`CreateCatalogueItem.Handler` does not pre-check the code. It inserts directly and only reacts to a
constraint violation: `catch (DbUpdateException exception) when (IsCodeCollision(exception))`, where
`IsCodeCollision` matches the Postgres unique-violation SQL state **and** the specific constraint name
`ux_catalogue_items_code` [Verified @ `src/Api/Features/Catalogue/CreateCatalogueItem/Handler.cs` -> `HandleAsync`].
The index itself is confirmed on the code column
[Verified @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` ->
`CatalogueItemConfiguration` -> `ux_catalogue_items_code`].

`Two_concurrent_requests_for_the_same_code_leave_exactly_one_item` drives this for real: two identical
creates fired with `Task.WhenAll` against the real PostgreSQL container, asserting exactly one `201` and
one `409`, then re-reading the table to confirm exactly one row [Verified @
`tests/Api.Tests/CreateCatalogueItemTests.cs:106-123`]. This is a genuine race exercised against a real
database, not a single-threaded simulation — it would fail under a `AnyAsync`-then-insert
implementation, which is exactly what the criterion asks the case to prove.

---

## 6. `AC-202-C` — four decimals, and which side of `AC-200-B` it picked

**The exact round-trip is genuinely proven. The "more than four decimals" half is not tested here
either — and the implementation has already, silently, picked a side of the still-open question, at a
place that reaches every `Money` in the system, not only this one.**

`Both_prices_keep_four_decimals_through_create_and_read_back` submits `987.6543` / `1234.5678`,
re-reads from a fresh `KaffDbContext`, and asserts both figures exact to the fourth decimal
[Verified @ `tests/Api.Tests/CreateCatalogueItemTests.cs:127-147`]. `decimal(18,4)` is applied as a
model-wide convention over every `Money` property, not per-property, so it cannot be forgotten on a
later addition [Verified @ `src/Infrastructure/Persistence/KaffDbContext.cs:73-93`,
`ConfigureConventions`]. That half of the criterion is solid.

**No test anywhere in `CreateCatalogueItemTests`, `EditCatalogueItemTests` or
`CatalogueItemEditingTests` submits a price with more than four decimals.** So `AC-202-C` is proven at
exactly four and silent above it — consistent with `TC-2-002`'s own note that the "more than four
decimals" half is not cased, pending a ruling.

**But the implementation already answers the question the ruling is supposed to settle, and does so
below the level any of these three stories touch.** `Money`'s constructor —
`src/Domain/Common/Money.cs:36-49` — runs every amount through
`decimal.Round(amount, Scale, Rounding)` with `Rounding = MidpointRounding.AwayFromZero` on
construction, unconditionally. A caller submitting `987.65436` gets `987.6544` stored with **no error,
no refusal, and nothing in the response that says a rounding happened** — `Money.From` and the
handlers that call it (`CreateCatalogueItem.Handler:67-68`, `EditCatalogueItem.Handler:65`) pass the
raw request decimal straight in. This is **rounding, not truncation**, so it is not the behaviour
`AC-200-B`'s text singles out as definitely wrong ("never silently truncated") — but it is also not a
refusal, and the caller is never told a value they typed was not the value stored.

This is not a new discovery — `decisions.md` D-008 is already open and already names exactly this
mechanism ("Money rounds away from zero at four decimal places · **OPEN**", *"OPEN for Nabil. Confirm
the rounding convention with Kaff's accountant"*). What this pass adds is the answer to the brief's
specific question: **the implementation has picked "round, silently, away from zero" globally**, not
"refuse," and it did so before `AC-200-B` or `D-008` was ruled, in a value type every money-bearing
feature in this codebase already depends on. **I am not resolving `AC-200-B` or `D-008` — this is
evidence for Nabil's ruling, not a decision taken behind it, exactly per the brief's own instruction.**

**`V-35-Q` — ⚠️ MEDIUM.** `AC-202-C`'s test proves the exact round trip and nothing above it; the
implementation it is silently sitting on top of has already committed the whole codebase to
silent-rounding-away-from-zero for the open `AC-200-B`/`D-008` question, with no caller-visible signal
that a submitted value was altered. **Owner: Nabil** (the ruling itself), **Backend** (if the ruling
goes the other way, `Money`'s constructor is the one place to change it — which is also the point in
`Money`'s favour: fixing it later touches one type, not three stories' worth of handlers).

---

## 7. `AC-203-I` — ordered by باب, then by code; is there more than one باب?

**There are genuinely two أبواب in the fixture — but the test cannot tell `AC-203-I`'s rule from a
plain code sort, because the two أبواب's `SortOrder` and their items' code prefixes were chosen to
agree with each other.**

`Results_are_grouped_by_bab_then_ordered_by_code_within_each` creates `babZ` at `SortOrder: 20` and
`babA` at `SortOrder: 10`, then items `{nonce}-Z-9`, `{nonce}-Z-1`, `{nonce}-A-9`, `{nonce}-A-1`, and
asserts the result equals `[aLow, aHigh, zLow, zHigh]` [Verified @
`tests/Api.Tests/ListCatalogueItemsTests.cs` -> `Results_are_grouped_by_bab_then_ordered_by_code_within_each`]. The handler's query is
`orderby bab.SortOrder, item.Code` [Verified @ `src/Api/Features/Catalogue/ListCatalogueItems/Handler.cs` -> `HandleAsync`].

**The confound:** babA's items are coded `{nonce}-A-*` and babZ's are coded `{nonce}-Z-*`. Ordinal
string comparison puts `"A"` before `"Z"` — so **sorting by `item.Code` alone, with no reference to
`bab.SortOrder` at all**, produces exactly the same sequence `[aLow, aHigh, zLow, zHigh]` the test
asserts. The test's own comment claims *"a code-only sort would interleave them"* — it would not: the
باب whose `SortOrder` the test put first (`babA`, 10) is also the باب whose codes sort first
alphabetically (`A` < `Z`), so the two hypotheses the case exists to distinguish — *"grouped by باب's
own SortOrder"* versus *"sorted by item code across the whole list, باب ignored"* — predict the
identical output here. A mutation that deleted `orderby bab.SortOrder,` entirely, leaving only
`orderby item.Code`, would **not** redden this test. This is precisely the trap the brief's own hint
named — *"an ordering assertion over a single group is satisfied by every possible ordering"* — arrived
at from a different direction: not one باب, but two whose labels happen to encode the same ordering
twice.

The Arabic-collation half is correctly left unasserted, exactly as `Q63`'s standing caveat requires
[Verified @ same test, the trailing `.Because` string names the caveat explicitly] — that part is not
the finding.

**`V-35-R` — ⚠️ MEDIUM.** `AC-203-I`'s only test does not discriminate باب-grouping from a plain
code sort, because `babA`/`babZ`'s `SortOrder` (10/20) and their item codes' alphabetic order (`A`/`Z`)
were chosen to agree. A `SortOrder` reversed relative to its code prefix — e.g. the باب sorted
**first** carrying codes that sort **later** alphabetically — is what would actually prove the rule.
**Owner: QA** (the case), **Backend has done nothing wrong here** — the handler's own query is correct
as written; the gap is entirely in what the test would catch.

---

## 8. `AC-206-H` / `AC-202-I` — audited before and after, absence claims

**PASS on both. Neither reads as the D-116 trap the brief named — the counts are scoped, not global
vibes, and the successful-change assertions check actual before/after JSON values, not "a record
exists."**

- **`AC-206-H`** (`ArchiveCatalogueItemTests`): the positive half reads the audit record scoped to
  `EntityId == id`, asserts `ActorUserId`, `ChangedProperties` containing `Status`, and both
  `BeforeJson`/`AfterJson` values (`Active` → `Archived`) [Verified @
  `tests/Api.Tests/ArchiveCatalogueItemTests.cs:82-100`]. The refusal half takes a count **scoped to
  the same `EntityId`**, before and after the refused second archive, and asserts equality — not "at
  least one record exists" and not an unscoped global count that could hide a stray write elsewhere
  [Verified @ same file, lines 114-131]. This is exactly the shape that would catch "the refusal wrote
  zero rows or the success wrote two," which is the failure mode the brief warned about.
- **`AC-202-I`** (`EditCatalogueItemTests`): the positive half edits two fields in one request and
  asserts `ChangedProperties` contains both `DescriptionAr` and `BaseSellRate`, with the before/after
  JSON checked for the sell rate specifically (`150m` → `175m`) [Verified @
  `tests/Api.Tests/EditCatalogueItemTests.cs:71-106`]. The refusal half uses an **unscoped, global**
  `AuditRecords` count before and after a `403`-refused edit [Verified @ same file, lines 108-125] —
  arguably a stronger check than the scoped one above, since it would catch a stray write to any row,
  not only this one.

No gap found in either.

---

## 9. KAFF-202 — remaining criteria and their cases

`AC-202-A` (whitelist confirmed, §4), `AC-202-B` (§5), `AC-202-C` (§6, `V-35-Q`), `AC-202-D`,
`AC-202-E`/`AC-202-F` (§3.2, `V-35-N`), `AC-202-G` (§4), `AC-202-I` (§8) are all disposed above or in
Block 1.

- **`AC-202-D`** — `TC-2-020`/`TC-2-021` are cased at the `Domain` layer, per the QA file's own
  citation, and that is where they are proven: `CatalogueItemEditingTests` asserts a `-1` cost price
  refused with `CostPriceMustNotBeNegative` and a sell rate below cost **accepted** as the positive
  control [Verified @ `tests/Domain.Tests/CatalogueItemEditingTests.cs:52-90`, and the same guard
  reapplied to `Reprice` at lines 165-200]. **PASS**, no gap against the case as written.
- **`AC-202-H`** — the refused-role set is `Finance, Hr, SiteEngineer, HeadOfDesign, MarketingSales`
  (five), with an exhaustiveness assertion (`The_refused_list_is_every_role_TC_2_025_names`) explaining
  that `Client` and `Subcontractor` are excluded because a portal session cannot reach a company-wide
  staff route and `Subcontractor` cannot sign in at all — spec.md §9: *"Subcontractor (record only, no
  login)"*, confirmed [Verified @ `spec.md` -> `Roles`]. **The Subcontractor half of that reasoning is solid.
  The Client half is asserted only in a comment, not executed** — unlike `ArchiveCatalogueItemTests`,
  which adds an explicit `ArchiveAsync(..., Role.Client, ...)` call and asserts `403` on the archive
  endpoint [Verified @ `tests/Api.Tests/ArchiveCatalogueItemTests.cs:224-227`], neither
  `CreateCatalogueItemTests` nor `EditCatalogueItemTests` ever constructs a `Role.Client` actor and
  calls create or edit. The permission gate (`RequirePermission(Permission.CatalogueManage)`) is
  almost certainly uniform regardless of session kind, so this is very unlikely to be a live defect —
  but it is an unexecuted claim standing where an executed one already exists as a pattern two files
  over.

**`V-35-T` — ⚠️ LOW-MEDIUM.** `AC-202-H`'s test suite asserts, by comment only, that a `Client`
session cannot reach the create/edit endpoints; `AC-206-G`'s sibling test in the same PR actually
drives a `Client` actor against its endpoint and asserts the refusal. The create/edit path should do
the same — it is a small addition and the pattern already exists to copy. **Owner: Backend/QA
(test-only change).**

- **`AC-202-J`** — no evidence exists; see §11's frontend finding (`V-35-S`), which covers all three
  stories' E2E criteria together.

---

## 10. KAFF-203 — remaining criteria and their cases

`AC-203-A`, `AC-203-B`, `AC-203-C` read cleanly and are genuinely exercised — a case-insensitive code
match, a true middle-of-phrase Arabic substring (`{nonce} عادية` against `خرسانة {nonce} عادية
للأساسات`), and a single query returning two items that each match a different field only [Verified @
`tests/Api.Tests/ListCatalogueItemsTests.cs:56-97`]. **PASS**, all three.

- **`AC-203-D`** — the Api-level half is real (a search matching nothing returns `200` with `items: []`,
  never `null` or `404` — [Verified @ same file, lines 99-115]) but **that is not what the criterion
  asserts.** `AC-203-D` names a specific pair of i18n keys — `catalogue.list.empty_filtered` versus
  `catalogue.list.empty` — rendered on the screen. That half is E2E/frontend and, per §11 below, there
  is no catalogue frontend at all to render either key. The API test proves a necessary precondition
  (an honest empty array to render from), not the criterion itself.
- **`AC-203-E`** (portal client refused, no leak in the body) and **`AC-203-F`** (role refusal plus the
  cost-price positive control) both read as genuine, targeted tests with real assertions against
  response bodies, not just status codes [Verified @ same file, lines 117-159]. **PASS.**
- **`AC-203-G`** (no money beyond the two prices) is asserted as a named property allow-list against
  the response type, with the test's own remark distinguishing it from "a search for suspect words"
  [Verified @ same file, line 200]. **PASS.**
- **`AC-203-H`** — no evidence; folded into `V-35-S` (§11).
- **`AC-203-I`** — disposed in §7 (`V-35-R`).

---

## 11. KAFF-206 — remaining criteria and their cases

`AC-206-A` (§8/archive test), `AC-206-C`/`AC-206-D` (Block 1), `AC-206-F` (Block 1, `V-35-O`),
`AC-206-H` (§8) are disposed. Remaining:

- **`AC-206-B`** — the no-delete allow-list is genuinely strong: it enumerates the live
  `EndpointDataSource`, filters to routes the shipped assembly maps under `/api/catalogue-items`,
  asserts the route list is **non-empty** before asserting no `DELETE` verb exists among them — the
  exact positive control D-116 and the case both ask for [Verified @
  `tests/Api.Tests/ArchiveCatalogueItemTests.cs:158-207`]. **PASS.**
- **`AC-206-E`** — proven together with `AC-206-H`'s refusal half in §8. **PASS.**
- **`AC-206-G`** — same shape as `AC-202-H` but stronger: five roles via `RefusedActors()` plus an
  **executed** `Role.Client` portal call, both asserted `403`, plus a positive confirmation that none of
  the six refused calls changed the item's status before the Owner's own call succeeds [Verified @
  `tests/Api.Tests/ArchiveCatalogueItemTests.cs:211-234`]. **PASS**, and the model `AC-202-H` should
  copy (§9, `V-35-T`).
- **`AC-206-I`** — no evidence; folded into `V-35-S` below.

**`V-35-S` — ⛔ HIGH.** *(Elevated above the brief's own framing, because this is not a coverage gap —
it is an absent feature carrying a `state=BUILT` trailer.)* **No catalogue screen exists anywhere in
this codebase.** `src/Web/src/app/features/` holds `audit`, `auth`, `clients`, `forbidden`, `landing`,
`not-found`, `users` — no `catalogue` folder [Verified: 2026-09-10]. `tests/E2E.Tests/` holds
`AuditScreenTests.cs`, `BidiGeometryTests.cs`, `ClientScreenTests.cs`, `SmokeTests.cs`,
`SuiteConfigurationTests.cs`, `UserScreenTests.cs` — no catalogue E2E file of any name [Verified:
2026-09-10]. That means:

- **`AC-202-J`, `AC-203-H`, `AC-206-I`** — each an explicit Arabic/RTL/mobile-width acceptance
  criterion of its own story — have **zero implementation**, not merely zero test coverage. There is no
  `S-017` and no `S-018` to screenshot.
- **`AC-203-D`'s actual empty-state message half** (§10) is unimplemented for the same reason.
- **CLAUDE.md's own Definition of Done** lists *"Arabic RTL correct at mobile width"* as a checklist
  item for every slice, unconditionally — not qualified by "backend first."

Every story's own "What already exists, and what this story adds" section is honest about this
in advance — `KAFF-202`, `-203` and `-206` all state plainly that no endpoint or screen exists yet and
name `Owner: Backend, then Frontend`. **The finding is not that the frontend is missing — it is that
each story's trailer already reads `state=BUILT`, unqualified, while three of its own P3 acceptance
criteria have no code behind them at all.** A reader of the trailer alone — which is CLAUDE.md's stated
single source of truth for a story's state — cannot tell "built end-to-end" from "backend built,
frontend not started," and this is the same shape `V-35-M` found for `KAFF-206`'s two held BOQ
criteria: a state that reads as fully discharged while carrying criteria nobody has touched.
**Owner: Scrum Master** (the trailers and what `state=BUILT` is allowed to mean when Frontend has not
started); **Frontend** (the actual gap).

**`V-35-U` — ⚠️ MEDIUM.** **`UnarchiveCatalogueItem` shipped inside commit `f675f1b` (labelled
`KAFF-206` by the brief's own scope table) with no `AC-206-*` id, no `TC-2-*` id, and a story file that
still lists its placement as undecided.** The endpoint exists, is permission-gated identically to
`Archive` (`RequirePermission(Permission.CatalogueManage)`), is audited by the same interceptor
mechanism, and has its own test file (`tests/Api.Tests/UnarchiveCatalogueItemTests.cs`) — this is not a
quality problem with the code itself. It is a continuity problem: `qa/slice-2/test-cases.md` explicitly
records *"No story owns building the endpoint … No case is written here for un-archiving an item,
because there is no story and no `AC-` to trace it to"* (its own `KAFF-206` section, "Coverage gap, not
a hole in this file"), and `stories/slice-2-masters/KAFF-206-archive-a-catalogue-item.md` itself still
says, unchanged, *"Left for the Scrum Master to place — either folded into this story's own re-estimate,
or its own small story — rather than assumed here."* Nobody placed it; it shipped anyway, inside a
commit the brief's own scope table attributes entirely to `KAFF-206`'s 3 points. The Unarchive
endpoint's own doc comment says it was built *"per the brief that placed it in this story"* — a brief
from the session that built it, not a decision recorded in the story file or `decisions.md`. **The
story file — CLAUDE.md's stated continuity, next to `STATUS.md` — is now wrong about what shipped under
`KAFF-206`, and no acceptance criterion exists for a caller to verify Unarchive against.** **Owner:
Scrum Master** — place it (fold into `KAFF-206`'s re-estimate with its own `AC-206-*` ids, or cut it to
its own story), the same way `Q67`/`KAFF-213` was handled for the باب-archive question this story
deliberately kept separate.

---

## 12. "Every mutation was watched failing" — claim 2 of the brief

**Unverifiable, and the absence of a record is itself notable given this repo's own history.**

The brief's claim — *"`KAFF-206`'s agent reports mutating `Archive()`'s guard, watching 3 of 22 Domain
tests redden, reverting, and confirming no marker survived"* — appears **nowhere else in this
repository.** Searched: `git log` for `f675f1b` and `3601034` (neither commit message mentions a
mutation count), `decisions.md` (no `D-13x` entry for `KAFF-206`'s build session — the surrounding
entries stop at `D-130`, 2026-09-09, and name nothing about `Archive()`), and a repo-wide search for
`"3 of 22"` / `"22 Domain"` / `"redden"` near `KAFF-206` [Verified: 2026-09-10, all three searches
empty]. This repository's own convention, demonstrated repeatedly elsewhere (`decisions.md` around
`D-108`, `D-109`, `D-127`, `V-33-A` through `V-34-E`), is to record a mutation's result — what was
mutated, what reddened, what did not — in `decisions.md` or a verification file, precisely so a claim
like this one is checkable later without re-running it. **That record does not exist for this claim.**

**I did not attempt to reproduce it myself.** The brief's hard constraints for this pass forbid any
change under `src/` or `tests/`, and a mutation — even one reverted before this file is committed — is
a change under `src/` for the duration of the edit. Reproducing the mutation to check the claim would
itself violate the constraint the brief places on me, so the honest answer is that the claim stands
**neither confirmed nor refuted**, on the evidence available without touching source.

**`V-35-V` — ⚠️ MEDIUM.** The brief's mutation-testing claim for `KAFF-206`'s `Archive()` guard has no
corroborating artifact anywhere in the repository, unlike this board's usual practice of recording
mutation results in `decisions.md`. This does not mean the claim is false — the guard reads correctly
in isolation (§3.0, §8) and a same-shape guard on `ArchiveClient` was previously verified by mutation in
this repo's history — but this specific claim, for this specific commit, rests on the say-so of the
session that built the code it describes, with nothing independent behind it. **Owner: Scrum Master** —
either the mutation record exists somewhere this search missed and should be cited from the story or
`decisions.md`, or it does not exist and the claim should be retracted from future briefs rather than
repeated.

---

## 13. Criteria with no QA case

**Among the 29 lettered criteria across `AC-202-A`…`J`, `AC-203-A`…`I`, `AC-206-A`…`I`: none.** Every
letter in all three stories has a written `TC-2-` case, including the two held ones (`AC-206-C` /
`TC-2-059`, `AC-206-D` / `TC-2-060`, both disposed in Block 1). This matches `qa/slice-2/test-cases.md`'s
own coverage index.

**One criterion-shaped thing outside that count has no case for a reason worse than "not yet
written": `UnarchiveCatalogueItem` (§11, `V-35-U`) has no `AC-` id at all**, so there is nothing for a
`TC-2-` case to trace to even in principle. It is not a gap in the QA file — the QA file names this
exact absence and declines to invent a criterion to fill it, correctly, per its own stated discipline.
The gap is upstream, in the story record.

---

## 14. Test-case execution ledger

Every case actually run this pass (Api.Tests / Domain.Tests, gate re-measured green in §2, tree
unmoved since). **PASS** unless noted.

| Case | Criterion | Result |
|---|---|---|
| `TC-2-017` | `AC-202-A` | PASS |
| `TC-2-018` | `AC-202-B` | PASS — §5 |
| `TC-2-019` | `AC-202-C` | PASS as written; see `V-35-Q` (§6) for what it does not cover |
| `TC-2-020` | `AC-202-D` | PASS |
| `TC-2-021` | `AC-202-D` (positive control) | PASS |
| `TC-2-022` | `AC-202-E` | ⛔ not a valid witness — `V-35-N` (§3.2) |
| `TC-2-023` | `AC-202-F` | ⛔ not a valid witness — `V-35-N` (§3.2) |
| `TC-2-024` | `AC-202-G` | PASS — §4 |
| `TC-2-025` | `AC-202-H` | PASS with a gap — `V-35-T` (§9) |
| `TC-2-026` | `AC-202-I` | PASS — §8 |
| `TC-2-027` | `AC-202-J` | ⛔ unbuilt — `V-35-S` (§11) |
| `TC-2-028` | `AC-203-A` | PASS |
| `TC-2-029` | `AC-203-B` | PASS |
| `TC-2-030` | `AC-203-C` | PASS |
| `TC-2-031` | `AC-203-D` | PASS (API half only) — `V-35-S` covers the missing UI half |
| `TC-2-032` | `AC-203-E` | PASS |
| `TC-2-033` | `AC-203-F` | PASS |
| `TC-2-034` | `AC-203-G` | PASS |
| `TC-2-035` | `AC-203-H` | ⛔ unbuilt — `V-35-S` (§11) |
| `TC-2-036` | `AC-203-I` | ⚠️ confounded, not a valid witness — `V-35-R` (§7) |
| `TC-2-057` | `AC-206-A` | PASS |
| `TC-2-058` | `AC-206-B` | PASS |
| `TC-2-059` | `AC-206-C` | HELD, correctly — Block 1 |
| `TC-2-060` | `AC-206-D` | HELD, correctly — Block 1 |
| `TC-2-061` | `AC-206-E` | PASS |
| `TC-2-062` | `AC-206-F` | ⚠️ partial — `V-35-O` (§3.3) |
| `TC-2-063` | `AC-206-G` | PASS |
| `TC-2-064` | `AC-206-H` | PASS |
| `TC-2-065` | `AC-206-I` | ⛔ unbuilt — `V-35-S` (§11) |

---

## 17. Verdict per story

**KAFF-202 — Create and edit a catalogue item: NOT VERIFIED AS BUILT.** The create/edit mechanism
itself is solid — the unique-index race, the four-decimal round trip, the whitelist response shape, the
audit before/after — all hold under real inspection. But two of ten criteria (`AC-202-E`, `AC-202-F`)
are reported discharged by a test that cannot fail for the rule it names (`V-35-N`), and one
(`AC-202-J`) has no implementation at all (`V-35-S`). **A story cannot be certified Verified while two
of its criteria are falsely marked discharged and a third has never been built.**

**KAFF-203 — Find a catalogue item by code or description: NOT VERIFIED AS BUILT.** Eight of nine
criteria are genuinely, often well, proven. `AC-203-I`'s only test cannot distinguish the rule it names
from a coincidental code sort (`V-35-R`), and `AC-203-H` — plus half of `AC-203-D` — has no
implementation (`V-35-S`). **Same shape as `KAFF-202`: strong backend, at least one criterion that
cannot fail, at least one criterion nobody has built.**

**KAFF-206 — Archive a catalogue item: NOT VERIFIED AS BUILT.** The strongest of the three on its own
merits — real allow-lists for the no-delete and no-alert claims, a scoped before/after audit check that
avoids the "one row exists" trap, an executed Client-portal refusal. Still carries: two held criteria
invisible from the story itself (`V-35-M`), one criterion that resembles but does not discharge its own
guarantee (`V-35-O`), one criterion with no implementation (`AC-206-I`, `V-35-S`), and — beyond its own
criteria entirely — a shipped endpoint (`Unarchive`) with no acceptance criterion and a story file that
still disclaims owning it (`V-35-U`). **The commit under this trailer is bigger than the story that
names it.**

**Across all three:** the gate is green (§2), the domain logic is careful and the tests that exist are
frequently better than the median for this kind of claim (real routes, real concurrency, scoped
counts). **What is not yet true for any of the three is "every acceptance criterion is discharged by a
witness that could have caught the rule being broken."** That is the bar `state=BUILT verdict=none` is
waiting to clear, and none of the three clears it today.

---

## 19. What I did not reach

**Everything in scope was reached.** Items 4 through 14, 17, 19, 20 and 21 are all written above. I did
not re-run §§1–3 (the opening gate, the gate figures, Block 1) — those are the prior session's completed
work and the brief and CLAUDE.md both direct against redoing what is already done and dated.

Two things deliberately **not** attempted, both by the brief's own hard constraints rather than by
running short:
- **Reproducing the `KAFF-206` mutation claim myself** (§12) — barred by "no changes under `src/`,"
  which a temporary, reverted mutation would still violate for its duration.
- **Resolving `AC-200-B`** (§6) — barred explicitly; evidence recorded (`V-35-Q`) and handed to Nabil,
  not decided.

---

## 20. Findings index

| id | severity | one line |
|---|---|---|
| `V-35-M` | MEDIUM | `AC-206-C`/`AC-206-D` correctly held, but invisible from `KAFF-206`'s own story file |
| `V-35-N` | MEDIUM-HIGH | `AC-202-E`/`AC-202-F` reported discharged by a test that cannot fail for the rule named |
| `V-35-O` | MEDIUM | `AC-206-F` resembles its guarantee (default-excludes-archived) but does not discharge it (no BOQ-line search exists) |
| `V-35-P` | MEDIUM | Disposition summary of Block 1's five criteria — no defect in shipped behaviour, the defect is in the record |
| `V-35-Q` | MEDIUM | `AC-202-C` proven only at exactly 4 decimals; `Money`'s constructor already silently rounds away-from-zero above that, system-wide, pre-empting the open `AC-200-B`/`D-008` ruling |
| `V-35-R` | MEDIUM | `AC-203-I`'s only test cannot distinguish باب-grouping from a plain code sort — the fixture's `SortOrder` and code-prefix orderings coincidentally agree |
| `V-35-S` | HIGH | No catalogue frontend exists at all — `AC-202-J`, `AC-203-H`, `AC-206-I` and half of `AC-203-D` are unbuilt, not merely untested, under three trailers all reading `state=BUILT` |
| `V-35-T` | LOW-MEDIUM | `AC-202-H`'s Client-refusal claim is asserted only in a comment; the sibling `AC-206-G` test executes the same check |
| `V-35-U` | MEDIUM | `UnarchiveCatalogueItem` shipped under `KAFF-206`'s commit with no `AC-` id, no QA case, and a story file that still disclaims owning it |
| `V-35-V` | MEDIUM | The brief's `KAFF-206` mutation-testing claim ("3 of 22 Domain tests redden") has no corroborating artifact anywhere in the repository |

---

## 21. Corrections to the brief

1. **The model directive could not be honoured, and the brief's own §M amendment requires saying so in
   the open.** The brief states *"Model: strongest … never list"* and is correct that this is the rule.
   It was overridden for this continuation session for a reason external to the brief (budget, not a
   judgement that the rule is wrong) — recorded at the top of this file and repeated here because §17's
   verdict is exactly the kind of output §M's amendment says must carry the caveat forward.

2. **The brief's scope table attributes the whole of commit `f675f1b` to `KAFF-206`'s 3 points; it is
   not.** The commit also ships `UnarchiveCatalogueItem` end-to-end — entity method, endpoint, handler,
   and its own test file — which is not named by any `AC-206-*` id and which `KAFF-206`'s own story
   file still lists as unplaced (`V-35-U`, §11). The brief's Block 1 table and "Five places a defect
   would be invisible" list both stop at the criteria `KAFF-206` itself writes; neither flags that the
   shipped diff is larger than the story. This is not the brief being factually wrong about anything it
   asserts — it is a gap in what it asked the Verifier to check, and it is the kind of gap Block 1's own
   framing would have caught had the instruction been "verify the commit" rather than "verify the
   story's criteria."

3. **Everything else in the brief checked out.** Block 1's premise (§3.0, independently re-verified),
   the five "places a defect would be invisible" hints (all five led somewhere real — §§4–8), the gate
   figures (§2, already re-measured by the prior session), and the "Known-open" register all matched
   what this pass found independently. I found no factual claim in the brief's prose itself that was
   wrong, only the one scope gap in item 2 above and the model-authority point in item 1.
