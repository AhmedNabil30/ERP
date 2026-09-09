# Verification — 2026-09-10

**Verifier, fresh session.** `CLAUDE.md`: *"If you wrote the code, you do not certify it."* I wrote
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
| 4 | `AC-202-G` — cost price never leaves the internal surface | pending |
| 5 | `AC-202-B` — repeated code refused by the database, not a lookup | pending |
| 6 | `AC-202-C` — four decimals, and which side of `AC-200-B` it picked | pending |
| 7 | `AC-203-I` — ordered by باب then code; is there more than one باب? | pending |
| 8 | `AC-206-H` / `AC-202-I` — audited before and after, absence claims | pending |
| 9 | KAFF-202 — remaining criteria and their cases | pending |
| 10 | KAFF-203 — remaining criteria and their cases | pending |
| 11 | KAFF-206 — remaining criteria and their cases | pending |
| 12 | "Every mutation was watched failing" — claim 2 of the brief | pending |
| 13 | Criteria with no QA case | pending |
| 14 | Test-case execution ledger (`TC-2-017` … `TC-2-036`, `TC-2-057` … `TC-2-065`) | pending |
| 17 | Verdict per story | pending |
| 19 | What I did not reach | pending |
| 20 | Findings index | pending |
| 21 | Corrections to the brief | pending |

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
