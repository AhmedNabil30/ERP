# Brief — Verifier, slice 2's first three stories

**Written 2026-09-10 by the Scrum Master.** Target: `3601034` or later on `main`.
Nine points are `state=BUILT verdict=none`. Nobody independent has looked at any of it.

---

## Who you are

`agents.md` §7 · `process/agile.md` §3. You read `spec.md` and the QA cases, **never the
implementation, to decide what to test.** Reading code to understand a failure is fine; reading code
to decide what *should* be true is not — that is how a test comes to assert what the code does
rather than what the business needs.

**You report. You do not fix.** No changes under `src/`. Findings go back to the authors.

**Model: strongest.** `agents.md` §M puts a downgraded model for a Verifier task on the never list.

**⚠️ You cannot be disqualified twice over the way the Scrum Master is.** The Scrum Master ran all
three of these gates and briefed the agent that built `KAFF-206`. Every gate figure in *Three claims
worth disbelieving* below is therefore a claim, not evidence.

---

## Write as you go — this is not a tidiness rule

**Create `qa/slice-2/verification-2026-09-10.md` as your FIRST action**, with a progress table listing
every section as `pending`. Copy the shape from `qa/slice-1/verification-2026-09-07.md`.

Write each finding into that file **the moment you have it**, before moving on. Commit every section
or two (`git commit -F <a message file>`). **Do not push.**

A previous agent on this board hit a session limit having said *"Build clean. Now running the new
tests"* and left thirteen unrun tests on a tree that looked finished (D-120 §1). **A section you could
not reach is a finding, not a silence.**

---

## Scope

| Story | What it is | Commit | Points |
|---|---|---|---|
| **KAFF-202** | Create and edit a catalogue item · `AC-202-A` … `AC-202-J` | `586a7d0` | 3 |
| **KAFF-203** | Find an item by code or description · `AC-203-A` … `AC-203-I` | `dd5f14c` | 3 |
| **KAFF-206** | Archive an item · `AC-206-A` … `AC-206-I` | `f675f1b` | 3 |

QA's cases are in `qa/slice-2/test-cases.md` — `TC-2-001` … `TC-2-098`. **Execute them.** Where a case
does not exist for a criterion, say so; do not write it yourself and then pass it. `agents.md` §175:
*"QA writes the cases; the Verifier executes them in a fresh session."* A Verifier who writes its own
cases writes cases it can pass.

---

## ⛔ Block 1 — five criteria whose subject does not exist. Start here.

**No `Boq`, `SignedBoq`, `BoqLine` or `Estimate` class exists anywhere in `src/`.** Verified
2026-09-09 by the Scrum Master; re-verify it yourself, it is one grep and the whole block turns on it.

These five criteria assert something about those absent things:

| Criterion | What it asserts |
|---|---|
| `AC-202-E` | re-pricing an item does not touch a signed BOQ |
| `AC-202-F` | re-pricing raises no alert and re-prices no estimate |
| `AC-206-C` | archiving does not touch a signed BOQ |
| `AC-206-D` | archiving does not touch an open estimate and raises no alert |
| `AC-206-F` | an archived item is absent from a **new BOQ line's** search |

`TC-2-059` and `TC-2-060` were marked **held** on 2026-09-09 for exactly this reason. **The other
criteria were not.** The question for each of the five is the same and it is not rhetorical:

> **Was this criterion reported discharged, and by what?**

The correct implementation of `KAFF-206` rule 3 is **to add nothing**. So a test that passes because
the subject is absent is indistinguishable from a test that passes because the rule is honoured —
**a case that cannot fail cannot witness the rule it exists for.** If any of the five is standing as
discharged on that basis, that is a finding, and its severity is not cosmetic: these are the
`spec.md` §4.4 price-freeze criteria, and they will be *assumed passing* when slice 5 builds billing
on top of them.

**`AC-206-F` is the one that may be genuinely reachable** — there is no BOQ-line search, but there
*is* a catalogue search with a three-state filter (`KAFF-203`, `CatalogueItemListFilter`). Decide
whether the archived item's absence from *that* surface discharges the criterion or merely resembles
it. Say which, and why.

---

## Five places a defect would be invisible

Hints, not a scope limit. **A Verifier that checks only what the author suggested is checking the
author's imagination.**

1. **`AC-202-G` — cost price never leaves the internal surface.** `spec.md` §4.2. D-106 is binding
   here: **whitelist over blocklist.** A response shaped by *removing* `costPrice` leaks on the next
   field somebody adds; a response shaped by *listing* what goes out does not. Check which shape it
   is, on **every** surface that returns an item — create, edit, list, search, archive, and the
   client portal. And D-116: an absence test needs a **positive control**. A test asserting
   `NotContain("costPrice")` passes just as happily against an empty response, a 404, or a typo'd
   property name. Prove the test can fail.

2. **`AC-202-B` — a repeated code is refused by the database, not by a lookup.** The criterion names
   the mechanism, not just the outcome. A handler that does `AnyAsync(...)` and then inserts is racy
   and passes every single-threaded test ever written. Look for the unique index and confirm the
   refusal path is the constraint violation, not a pre-check. If it is a pre-check, the criterion is
   not met however green the suite is.

3. **`AC-202-C` — both prices keep four decimals — and `AC-200-B` is still unruled.** `AC-200-B`'s
   *"more than four decimals"* clause names two legal behaviours and no rule choosing between them;
   it is with Nabil. **Do not resolve it.** But `AC-202-C` may have been discharged with a test that
   silently picks one of those two behaviours. If it did, say which one it picked — that becomes
   evidence for Nabil's ruling rather than a decision taken behind it. `HasPrecision(18, 4)` on
   **both** money properties, or EF truncates silently.

4. **`AC-203-I` — ordered by باب, then by code.** `Bab` exists (`src/Domain/MasterData/Bab.cs`), but
   **`KAFF-204`, the story that builds the باب tree, is NOT-BUILT.** So: is there more than one باب in
   any fixture the ordering test runs against? An ordering assertion over a single group is satisfied
   by every possible ordering. And D-129 §5 left the **Arabic collation caveat (ا / أ / إ) explicitly
   unanswered** and gave it to the Architect — check whether the implementation quietly answered it.

5. **`AC-206-H` and `AC-202-I` — audited before and after.** `AC-206-H` carries an unusual second
   half: *"the refused second archive of `AC-206-E` produced no record at all."* That is an absence
   claim inside an audit criterion — the exact shape D-116 warns about. A test that checks "one audit
   row exists" passes whether the refusal wrote zero rows or the success wrote two.

---

## Three claims worth disbelieving

1. **The gate figures.** Build Release 0/0 exit 0 · `dotnet format` exit 0 · Domain **154/154** · Api
   **358/358** in 276s · citations 0 broken. **All measured by the Scrum Master, who is disqualified.**
   Re-run them. `.claude/skills/run-kaff-erp/SKILL.md` has the gate order and it is an order, not a
   list: **kill stranded hosts first, then build, then check `$LASTEXITCODE` before you believe any
   test result.** A build run before the kill hits `MSB3021`, exits 1, leaves the *previous* exe on
   disk, and every figure after it is measured against a stale binary. That cost five hours on
   2026-09-08 and was misdiagnosed three times. **A run without a `total:` line is not a result.**
   **One actor per gate** — if you are running the Api suite, nobody else is.

2. **"Every mutation was watched failing."** `KAFF-206`'s agent reports mutating `Archive()`'s guard,
   watching 3 of 22 Domain tests redden, reverting, and confirming no marker survived. Three known
   false-negative shapes (D-109 §3): a mutation that does not compile runs the *previous* binaries; a
   revert can leave stale binaries; and a string replacement can silently miss on a CRLF/LF mismatch
   and report green. Also ⚠️ **`git checkout` cannot revert a mutation in an untracked file** — it
   silently does nothing (D-120 §3).

3. **This brief.** Written by the session that briefed the `KAFF-206` agent and ran all three gates.
   **Correct it in writing where it is wrong.** Previous passes corrected their predecessor's
   statements and one of those corrections is now settled evidence.

---

## Known-open, so you do not re-report them as new

`AC-200-B` (Nabil's — the decimals ruling; `KAFF-200` is not Ready until it lands) · `AC-125-C`
(Nabil's, an unperformed check, verdict LAPSED) · `Q70`–`Q73`, `Q75`, `Q29` (Karim's — they block
`KAFF-209`–`212`) · the Arabic collation caveat on `AC-203-I` (Architect's) · `F-127-1` — the user
list's search box and filter chips render with no server behaviour, recorded and not routed ·
**`KAFF-129`** — monthly `audit_records` partitioning, **must land before slice 3 opens** · ~22 leaked
`kaff_test_*` databases, investigated and cleared as a cause of any failure, fixture cleanup owed.

---

## Deliverable

`qa/slice-2/verification-2026-09-10.md` — findings numbered `V-35-x`, severity stated, evidence given,
**no fixes applied**, and a **per-story verdict** for all three. Commit as you go. **Do not push.**

**Report back:** findings by severity · the gate figures you measured yourself · the verdict per story
· the disposition of each of Block 1's five criteria · which criteria have no QA case · and anything
in this brief you could not confirm.
