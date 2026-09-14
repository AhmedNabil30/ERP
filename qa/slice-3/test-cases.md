# Slice 3 — test cases

**Range allocated: `TC-3-001` … `TC-3-063`.**

Checked before writing a single case: `grep -rn "TC-3-" qa/ stories/ decisions.md process/` returned
nothing. `qa/README.md`'s ID scheme is `TC-<slice>-<nnn>` — the slice number is part of the
identifier — so `TC-3-nnn` cannot collide with `TC-1-nnn` or `TC-2-nnn` under the scheme as written.
The range is taken as `001`–`022`.

⛔ **Extended 2026-09-14, `TC-3-023` … `TC-3-063`, for `KAFF-301`, `302`, `303`, `304`, `305`, `319`.**
Checked again before writing: `grep -rn "TC-3-" qa/ stories/ decisions.md process/` returned only the
existing `001`…`022` (in this file, in `tests/Domain.Tests/Section15WorkedExampleTests.cs`, and in
`KAFF-300`'s own story) — `022` is confirmed the highest id in use, so the extension starts at `023`.
These six stories cut the Definition-of-Ready item this range closes: none of them had a QA scenario
before this pass.

Covers `KAFF-300` — the `spec.md` §15 worked-example fixture — in full: all nine acceptance criteria,
`AC-300-A` through `AC-300-I`. Twenty-two cases against nine criteria because two criteria are written
in the story itself to require more than one case to be fully discharged:

- `AC-300-B` says *"every row of §15's table is asserted, including the 'Client pays' column"* and
  warns explicitly that *"a fixture that checks only the totals passes on two compensating errors."*
  One monolithic case cannot catch a single wrong cell reconciling against another; it is cased per
  event row (six cases: five events plus the totals row) so a failure names which row broke.
- `AC-300-C` says *"each of §15's five [invariants] is its own named assertion, so a failure says
  which invariant broke"* and calls out `Section_15_holds` by name as the anti-pattern. It is cased
  per invariant (five cases) for the same reason — one case per named assertion, mirroring what the
  fixture itself is required to do.

Every other criterion is one case, because the AC's own wording does not ask for row- or
column-level granularity.

**Layer is `Domain` for `TC-3-001`…`022` (`KAFF-300`) and for `KAFF-304`'s query-object cases
(`TC-3-046`…`051`); `Api` for everything else added in the 2026-09-14 extension.** `tests/Domain.Tests`
exists in this repository [Verified: 2026-09-14 — `tests/Domain.Tests` is a real project with prior
content, e.g. `PostingRuleTests.cs`, `TestAccounts.cs`]. `KAFF-300`'s own text places its fixture there:
it is written against `Posting`/`Account` derivation directly, supplies §15's figures as given data
rather than driving a calculator, and adds no endpoint — `AC-300-D`'s "no figure is read from a stored
balance, a running total kept by the fixture, or a column" is a Domain-level guarantee about how the
fixture computes its own expectations, not an HTTP contract. `KAFF-301`, `302`, `303`, `305` and `319`
are real HTTP endpoints exercising permission checks and database guards end to end, so their cases run
against `tests/Api.Tests` (real PostgreSQL). `KAFF-304` is split: the reusable balance query object
(`AC-304-A`, `B`, `C`, `D`, `E`) is Domain-testable apart from any endpoint and is cased there; the one
criterion that needs an HTTP-facing permission gate is held on `Q90` and cased at `Api`.

## How to read a case

```
**TC-3-000 · what it checks**
`AC-300-X` · P1 · Domain · spec.md §15 · D-034
Given … When … Then …
*Fails if:* the one defect this case catches.
```

Same conventions as `qa/slice-1/test-cases.md` and `qa/slice-2/test-cases.md`: **Priority** P1
blocker · P2 major · P3 minor (`qa/README.md`). The citation is the source of the expected result —
`spec.md` or a `decisions.md` D-number, never the implementation. **`Fails if:`** is the mutation from
`qa/strategy.md` §5, written as the defect the case catches; a case with no `Fails if:` line does not
get written.

**Priority.** Every `KAFF-300` case in this file is **P1**. `qa/README.md`'s own scale puts "Money"
first among what a P1 gates, and `CLAUDE.md`'s testing priority order puts "the `spec.md` §15 worked
example and its invariants" first of all, calling them "the acceptance criteria for the whole system."
Nothing in `KAFF-300` is a P2 or P3 concern. The 2026-09-14 extension (`TC-3-023`…`063`) mixes P1
(money, permission, database-guard rules) and P2 (structural/architecture rules — idempotency,
reusability, exhaustiveness discipline — where breaking the rule is a defect but not an immediate money
or permission leak); each case states its own priority.

## Story coverage index

| Story | Criterion | Cases | Note |
|---|---|---|---|
| `KAFF-300` | `AC-300-A` | `TC-3-001` | fixture present and red/skipped-with-reason, not the invariants it will hold |
| `KAFF-300` | `AC-300-B` | `TC-3-002`…`007` | one case per event row, plus the totals row |
| `KAFF-300` | `AC-300-C` | `TC-3-008`…`012` | one case per named invariant |
| `KAFF-300` | `AC-300-D` | `TC-3-013` | — |
| `KAFF-300` | `AC-300-E` | `TC-3-014` | — |
| `KAFF-300` | `AC-300-F` | `TC-3-015`…`017` | growth, release, and the two refusals split so a failure names which |
| `KAFF-300` | `AC-300-G` | `TC-3-018`…`020` | the sequence, the refusal, and the Safe-never-negative sweep |
| `KAFF-300` | `AC-300-H` | `TC-3-021` | — |
| `KAFF-300` | `AC-300-I` | `TC-3-022` | carries `Q14`/`Q58` by name — see the case itself |
| `KAFF-301` | `AC-301-A`…`I` | `TC-3-023`…`031` | one case per criterion |
| `KAFF-301` | `AC-301-J` | `TC-3-032` | **HELD — cross-cutting with `KAFF-319`, per the story's own text** |
| `KAFF-302` | `AC-302-A`…`G` | `TC-3-033`…`039` | one case per criterion; `Q88` narrows nothing cased here — the BA already scoped `AC-302-A` to Lump Sum only |
| `KAFF-303` | `AC-303-A`, `B`, `D`, `E`, `F` | `TC-3-040`, `041`, `043`, `044`, `045` | one case per criterion |
| `KAFF-303` | `AC-303-C` | `TC-3-042` | **HELD — cross-cutting with `KAFF-319`, no domain `Error` exists yet for `KAFF_REVERSAL_OF_REVERSAL`** |
| `KAFF-304` | `AC-304-A`, `B`, `C`, `D`, `E` | `TC-3-046`…`050` | Domain-layer, the query object apart from any endpoint |
| `KAFF-304` | endpoint/permission gate | `TC-3-051` | **HELD `Q90`** |
| `KAFF-305` | `AC-305-A`, `B`, `C`, `D`, `E`, `G` | `TC-3-052`…`057` | one case per criterion, ruled pairs only |
| `KAFF-305` | `AC-305-F` (ruled types) | `TC-3-058` | exhaustiveness mechanism, run against the pairs `spec.md` already states |
| `KAFF-305` | `AC-305-F` (unruled types) | `TC-3-059` | **HELD `Q91`** — names the five posting types `Q91` leaves open |
| `KAFF-319` | `AC-319-A`…`D` | `TC-3-060`…`063` | one case per criterion |

No `KAFF-300` criterion is HELD. `Q14` narrows `AC-300-I`'s figures if Karim contradicts D-034, and
`Q58` gates a slice-5 calculator this story does not build — neither blocks writing this fixture's
case, per the story's own "What `Q14` gates" section.

**DoR-clean on the QA item after this pass: `KAFF-301`, `302`, `303`, `304`, `305`, `319` — all six.**
Every criterion in every one of them now has either a cased scenario or a `HELD Qnn`/`HELD` (cross-cutting)
line citing the exact open question or dependency, which the DoR item itself treats as satisfied — the
same convention `KAFF-300`'s own `Q14`/`Q58` handling on `AC-300-I` already established for this file.
No acceptance criterion across the six stories was left uncased.

---

**TC-3-001 · the §15 fixture exists and is not silently green, absent, or ignored**
`AC-300-A` · P1 · Domain · spec.md §15 (first line) · D-034
Given the repository at the moment slice 3 opens, before `KAFF-301` is pulled
When the Domain test suite runs
Then a §15 worked-example fixture is discovered by the test runner, and it either **fails** with an
assertion message that names one of §15's own figures (e.g. "240,000", "Extract 1", "20%") — not a
generic assertion-library message — or is **explicitly skipped** with a skip reason string naming what
Treasury code is missing
And it is not the case that the fixture is absent from the test run, passes with no assertions
executed, or is skipped with an empty or generic reason
*Fails if:* the fixture does not exist yet the story is marked as having discharged this criterion; or
the fixture exists but its skip attribute (or equivalent) carries no reason string, so `dotnet test`'s
output cannot distinguish "not yet buildable" from "silently disabled" — the exact D-046 failure mode
(`Zero tests ran` reported as success) this criterion exists to close off.

**TC-3-002 · row "Advance at signing" — Client pays 250,000, all other columns nil**
`AC-300-B` · P1 · Domain · spec.md §15, header line
Given the contract (1,000,000), advance rate (25%), hold rate (20%) and تشوينات rate (75% of 100,000)
When the advance-at-signing posting is applied and no other event has run
Then Work, Hold, تشوينات all derive to 0, Advance derives to 250,000, and Client pays derives to
250,000 — each read from summed postings, not asserted as one combined figure
*Fails if:* the advance posting is booked against the wrong account (e.g. straight to Advance without
a matching Client pays movement), so the row balances in total but the individual columns are wrong —
exactly the "two compensating errors" `AC-300-B`'s own text warns a totals-only case would miss.

**TC-3-003 · row "Extract 1" — Work 300,000, Hold −60,000, Advance −75,000, تشوينات +75,000, Client pays 240,000**
`AC-300-B` · P1 · Domain · spec.md §15, table row 2
Given the state after the advance-at-signing event
When Extract 1's five postings are applied
Then Work derives to 300,000, Hold derives to −60,000 for the period (running hold 60,000), Advance
derives to −75,000 for the period (running advance 175,000), تشوينات derives to +75,000, and Client
pays derives to exactly 240,000
*Fails if:* Client pays is computed as a net of the other four columns rather than summed from its own
postings, and any one column is wrong by an amount the net absorbs — e.g. تشوينات booked as +70,000
and hold as −65,000 still nets to 240,000 while both individual figures are false.

**TC-3-004 · row "Extract 2" — Work 300,000, Hold −60,000, Advance −75,000, تشوينات −45,000, Client pays 120,000**
`AC-300-B` · P1 · Domain · spec.md §15, table row 3
Given the state after Extract 1
When Extract 2's five postings are applied
Then Work derives to 300,000, running Hold to 120,000, running Advance to 100,000, تشوينات for the
period derives to −45,000, and Client pays derives to exactly 120,000
*Fails if:* the تشوينات recovery of 45,000 (`AC-300-I`'s transcribed literal) is silently replaced by a
derived figure — e.g. 25% of period work value, which is the *advance* recovery rule misapplied here —
producing a different, coincidentally plausible Client pays figure.

**TC-3-005 · row "Extract 3" — Work 400,000, Hold −80,000, Advance −100,000, تشوينات −30,000, Client pays 190,000**
`AC-300-B` · P1 · Domain · spec.md §15, table row 4
Given the state after Extract 2
When Extract 3's five postings are applied
Then Work derives to 400,000, running Hold to 200,000, running Advance to exactly 0, تشوينات for the
period derives to −30,000, and Client pays derives to exactly 190,000
*Fails if:* the Advance ledger is left at a nonzero residual (e.g. 0.0001 from an unconfigured decimal
precision) instead of exactly 0, which `AC-300-C`'s second invariant also checks but this row-level
case catches at the point of the specific posting that caused it.

**TC-3-006 · row "Handover — hold release" — Hold +200,000, Client pays 200,000, all else nil**
`AC-300-B` · P1 · Domain · spec.md §15, table row 5
Given the state after Extract 3, with running Hold at 200,000
When the handover hold-release posting is applied
Then Work, Advance and تشوينات derive to 0 for this event, Hold derives to +200,000 leaving the
running Hold balance at exactly 0, and Client pays derives to exactly 200,000
*Fails if:* the release is modelled as two or more postings (a partial release followed by a second
movement) rather than one, so the row still totals 200,000 but `AC-300-F`'s "once, in full" property is
already broken at the point this row would need to catch it.

**TC-3-007 · totals row — Work 1,000,000, Hold 200,000, Advance 250,000, تشوينات 0, Client cash 1,000,000**
`AC-300-B` · P1 · Domain · spec.md §15, table totals row
Given all five events applied in order — advance, Extract 1, Extract 2, Extract 3, handover
When each column is summed across all events
Then Work totals 1,000,000, Hold totals 200,000, Advance totals 250,000, تشوينات totals 0, and Client
pays totals exactly 1,000,000 — each to four decimal places, matching §15 exactly
*Fails if:* the totals row is asserted as the *only* check in the fixture (the anti-pattern `AC-300-B`
names) so that this case, run alone with `TC-3-002`…`006` absent, would let two offsetting per-row
errors pass undetected — recorded here as the reason this case must never stand alone.

**TC-3-008 · invariant 1 — accumulated hold equals exactly 20% of contract value**
`AC-300-C` · P1 · Domain · spec.md §15, invariant list item 1
Given the fixture's postings applied through handover
When the named assertion `Hold_equals_twenty_percent_of_contract` (or equivalent) runs
Then it independently checks accumulated Hold (200,000) against 20% of the contract value (1,000,000),
as its own assertion, distinct from any other invariant's pass/fail
*Fails if:* this invariant is folded into one combined boolean with another (e.g. `AllInvariantsHold`)
so that a hold-percentage regression and an unrelated regression both report as one failure with no
way to tell which broke — the `Section_15_holds` anti-pattern `AC-300-C` names by name.

**TC-3-009 · invariant 2 — advance ledger reaches exactly zero and never goes negative**
`AC-300-C` · P1 · Domain · spec.md §15, invariant list item 2
Given the running Advance balance sampled after each of the five events (250,000 → 250,000 → 175,000 →
100,000 → 0 → 0)
When the named assertion for this invariant runs
Then it checks, as its own assertion, that the sequence never goes negative at any sampled point and
lands at exactly 0 after Extract 3
*Fails if:* only the final value (0) is checked and an intermediate negative excursion — e.g. a
posting order bug that recovers 275,000 before Extract 3 — passes undetected because the ledger
recovers back to zero by the end.

**TC-3-010 · invariant 3 — تشوينات in equals تشوينات recovered**
`AC-300-C` · P1 · Domain · spec.md §15, invariant list item 3 · D-034
Given the تشوينات postings: +75,000 at Extract 1, −45,000 at Extract 2, −30,000 at Extract 3
When the named assertion for this invariant runs
Then it checks, as its own assertion, that the sum of تشوينات-in postings (75,000) equals the sum of
تشوينات-recovered postings (45,000 + 30,000 = 75,000), netting to exactly 0
*Fails if:* this check is satisfied by the totals row's تشوينات column reading 0 without separately
confirming the in-figure and the recovered-figure are each 75,000 — a compensating error (e.g. 80,000
in, 80,000 recovered by a wrong schedule) would still net to 0 and pass.

**TC-3-011 · invariant 4 — total client cash equals contract value exactly**
`AC-300-C` · P1 · Domain · spec.md §15, invariant list item 4
Given the five Client-pays postings: 250,000, 240,000, 120,000, 190,000, 200,000
When the named assertion for this invariant runs
Then it checks, as its own assertion, that their sum equals the contract value (1,000,000) exactly, to
four decimal places
*Fails if:* the check compares against a hard-coded literal `1000000m` copied from the fixture's own
setup rather than against the contract-value variable the fixture derives everything else from, so a
future change to the contract figure at the top of the fixture would silently stop testing anything.

**TC-3-012 · invariant 5 — no posting sequence in the fixture produces a negative Safe balance**
`AC-300-C` · P1 · Domain · spec.md §15, invariant list item 5 · D-044 §8
Given the Safe account's balance sampled after every individual posting in the fixture, not only after
each event
When the named assertion for this invariant runs
Then it checks, as its own assertion, that the Safe balance is non-negative at every sampled point
across the full posting sequence
*Fails if:* the Safe balance is sampled only at event boundaries (after all of an event's postings are
applied) rather than after each posting within an event, so a same-event ordering that dips negative
mid-event and recovers before the event's last posting passes undetected.

**TC-3-013 · every fixture balance is derived by summing postings, never read from a stored figure**
`AC-300-D` · P1 · Domain · spec.md §6.1 · CLAUDE.md ("Never store a balance")
Given the fixture's expected figures for every row and every invariant
When the fixture computes a balance to assert against
Then it does so by summing `Posting` rows for the relevant account and period, and the fixture itself
declares no running-total field, no cached balance variable, and no `Balance` column read from an
entity
And introducing a stored `Balance` column on `Account` that is allowed to drift from summed postings
would make this case fail, because the fixture is written to sum postings regardless of any such
column's presence
*Fails if:* the fixture keeps its own accumulator variable (e.g. `runningHold += 60000m;`) and asserts
against that accumulator instead of re-summing `Posting` rows each time — a second implementation of
the thing the fixture exists to grade, which could disagree with the real derivation and never notice.

**TC-3-014 · no ledger is netted against another anywhere in the fixture, including "Client pays"**
`AC-300-E` · P1 · Domain · spec.md §6.4 · CLAUDE.md ("The five ledgers never net against each other")
Given the client advance, hold, firm advance, عهدة (تشوينات) and owner current account ledgers as they
stand after Extract 1
When the fixture computes Extract 1's Client pays figure (240,000)
Then it is computed as the sum of Extract 1's own separately-derived movements — the Hold posting, the
Advance posting, the تشوينات posting and the direct cash-collection posting — and at no point is one
ledger's balance subtracted from another's balance to produce a third figure
*Fails if:* Client pays is computed as `PeriodWorkValue − CurrentHoldBalance − CurrentAdvanceBalance +
CurrentTashwinatBalance` (a net of running balances) rather than as a sum of the period's own posted
movements — arithmetically equal on §15's numbers today, and silently wrong the day any ledger carries
a balance from outside this fixture's five events.

**TC-3-015 · the hold only grows before handover — 60,000, then 120,000, then 200,000, no debit**
`AC-300-F` · P1 · Domain · spec.md §6.4 and its 2026-08-20 amendment · CLAUDE.md ("The hold only grows")
Given Extracts 1, 2 and 3 applied in order, before handover
When the running Hold balance is sampled after each extract
Then it reads exactly 60,000, then 120,000, then 200,000 — monotonically increasing — and no posting
in any of the three extracts debits the Hold account
*Fails if:* a snag, deduction or adjustment posting debits the Hold account at any point before
handover and the fixture's totals still reconcile because a later posting compensates — the running
Hold figure must be checked at each extract, not only compared to 200,000 at the end.

**TC-3-016 · handover releases the hold once, in full, to exactly zero**
`AC-300-F` · P1 · Domain · spec.md §6.4 amendment · CLAUDE.md
Given the running Hold balance at exactly 200,000 after Extract 3
When the handover event is applied
Then exactly one Hold-release posting is recorded, for the full 200,000, and the resulting Hold
balance is exactly 0 — not approximately 0, not a small positive or negative residual
*Fails if:* the handover event is modelled as more than one posting against Hold (e.g. two partial
releases summing to 200,000), which would satisfy "reaches zero" while violating "once" — this case
counts postings, not only the resulting balance.

**TC-3-017 · a partial hold release, or any hold debit before handover, is refused**
`AC-300-F` · P1 · Domain · spec.md §6.4 amendment · CLAUDE.md ("If you write code that debits the hold ledger before handover, you have misread the spec") · D-033
Given the running Hold balance at any point before the handover event (e.g. 60,000, after Extract 1)
When the fixture attempts a Hold debit of any amount, or a partial release smaller than the full
accumulated balance, before the handover event fires
Then the attempt is refused, and refused by the database guard named in `KAFF-300`'s evidence table
(`trg_postings_hold_release_in_full`) — not merely absent from the fixture's happy path
*Fails if:* the fixture only demonstrates the happy path (hold grows, then releases once) and never
exercises the refusal — a regression that silently permits a pre-handover Hold debit would leave every
other assertion in this file green.

**TC-3-018 · the advance ledger runs 250,000 → 175,000 → 100,000 → 0 in that exact sequence**
`AC-300-G` · P1 · Domain · spec.md §15 · D-044 §8
Given advance recoveries of 75,000 (Extract 1), 75,000 (Extract 2) and 100,000 (Extract 3) against an
advance of 250,000
When each recovery posting is applied in order
Then the running Advance balance is sampled after each and reads exactly 250,000, 175,000, 100,000,
then 0 — in that order, not merely reaching 0 as a final state
*Fails if:* the three recovery amounts are applied out of order (e.g. 100,000 first) and the fixture
only checks the final balance of 0, which is order-independent and would not catch the transposition.

**TC-3-019 · a fourth advance recovery is refused by the database, not by application code**
`AC-300-G` · P1 · Domain · spec.md §6.1 ("Enforce in the database, not only in application code") · D-044 §8
Given the running Advance balance at exactly 0 after Extract 3's recovery
When a fourth recovery posting of any positive amount is attempted against the Advance account
Then the attempt is refused by the database's non-negative-floor constraint on the Advance account,
and the fixture asserts the refusal comes from that constraint (e.g. a caught constraint-violation
exception or the guard's own error), not from an in-fixture or in-handler pre-check
*Fails if:* the fixture prevents the fourth recovery only with an `if` guard in the test helper or in
application code, so the case would still pass green even if the database constraint on the Advance
account were dropped — proving nothing about the floor D-044 §8 requires.

**TC-3-020 · no ordering of §15's postings produces a negative Safe balance**
`AC-300-G` · P1 · Domain · spec.md §6.1 · D-044 §8
Given the full set of postings §15's five events generate
When they are replayed in at least one alternative legal ordering that the fixture's own event sequence
does not use (e.g. all of Extract 2's postings before all of Extract 1's, where the domain does not
otherwise forbid the reordering)
Then the Safe account's balance, sampled after every posting in the alternative ordering, never goes
negative
*Fails if:* the fixture asserts non-negativity only along its one canonical event ordering — a Safe
floor bug that surfaces only under a different, still-legal ordering (e.g. a cash-collection posting
that should precede a disbursement but is not enforced to) would never be exercised.

**TC-3-021 · every figure in the fixture is `decimal`, exact to four places, and no `float`/`double` appears anywhere in it**
`AC-300-H` · P1 · Domain · spec.md §6.1 amendment · D-044 §6 · CLAUDE.md ("Never use `float` or `double` anywhere near money")
Given the fixture's own source file
When it is inspected for numeric literal types, intermediate variable types, and assertion tolerances
Then every monetary literal, every intermediate value and every expectation is `decimal`, and no
assertion is written with a floating-point tolerance (`Should().BeApproximately`, an epsilon compare,
or equivalent) anywhere in the file
*Fails if:* an assertion is written as `actual.Should().BeApproximately(240000m, 0.01m)` or similar —
a tolerance that would let a `double`-truncation bug (CLAUDE.md's own warning) through silently on
numbers that are, as the story notes, whole thousands and would otherwise expose the bug exactly.

**TC-3-022 · تشوينات's three figures are transcribed literals citing `Q14`/`Q58`, not a derived formula**
`AC-300-I` · P1 · Domain · spec.md §15 تشوينات column · D-034 · `stories/questions-for-karim.md` → `Q14`, `Q58`
Given §15's تشوينات column: **+75,000** at Extract 1, **−45,000** at Extract 2, **−30,000** at Extract
3, netting to zero
When the fixture's source is read at the point these three figures are declared
Then all three appear as literal `decimal` values (`75000.0000m`, `-45000.0000m`, `-30000.0000m` or
equivalent) assigned directly, not computed from any expression, and a code comment or identifier at
that point in the source names both `Q14` and `Q58` by number
And no function, formula, or calculation anywhere in the fixture derives 45,000 or 30,000 from period
work value, advance recovery rate, or any other §15 figure — including a proportional split, a 25%
share, or any other rule not stated in `spec.md`
*Fails if:* a future edit replaces the literal `-45000.0000m` with an expression such as
`0.25m * periodWorkValue` (the *advance* recovery rule from §16 assumption 2, misapplied here) — the
fixture would still pass today, because §15's own numbers are exactly what such a formula would be
fitted to and would then be silently wrong on any project whose work values differ from 300,000 /
400,000. This is the specific defect `Q58`'s own text warns an invented rule would produce, and this
case exists to make that substitution visible even though it cannot be caught by any assertion on the
current numbers alone — the citation-in-source check is the only thing that catches it, which is why
this case requires it as pass/fail criteria in its own right, not as documentation.

---

# KAFF-301 · Post a movement between two accounts

**TC-3-023 · a valid posting between two matching, active accounts is created and retrievable**
`AC-301-A` · P1 · Api, real PostgreSQL · spec.md §6.1
Given two active, postable accounts of the same currency, same ledger scope, matching project tags
When Finance `POST`s a movement between them with a positive amount, a `PostingType` and a `SourceDocument`
Then the posting is created, `IsReversal` is `false`, and the response returns its id, date, accounts,
amount and type
*Fails if:* the endpoint returns a recomputed or rounded amount instead of the stored value, or omits
any of id/date/accounts/amount/type from the response, so a caller cannot confirm what was actually
persisted.

**TC-3-024 · the amount round-trips at four decimal places with no floating-point type in the contract**
`AC-301-B` · P1 · Api, real PostgreSQL · D-044 §6
Given an amount of `1234.5678`
When the posting is created and then read back through the same endpoint
Then the returned amount equals `1234.5678` exactly, and the request/response DTOs carry no `float` or
`double` field anywhere in their contract
*Fails if:* the amount is stored or serialised through a `double`-typed field at any point, silently
truncating the fourth decimal on values that do not round cleanly in binary floating point.

**TC-3-025 · a zero or negative amount is refused, and no row is written**
`AC-301-C` · P1 · Api, real PostgreSQL · spec.md §6.1
Given an amount of `0` and, separately, an amount of `-100`
When each posting is attempted
Then both are refused with `errors.treasury.amount_must_be_positive`, and a subsequent count of
postings for the target accounts is unchanged
*Fails if:* the endpoint accepts a zero amount as a no-op success, or accepts a negative amount and
relies on the from/to accounts to carry the correct direction — exactly the sign-in-amount mistake rule
3 forbids.

**TC-3-026 · a posting that would net the client advance ledger against the hold ledger is refused**
`AC-301-D` · P1 · Api, real PostgreSQL · spec.md §6.4
Given a from-account in the client advance ledger and a to-account in the hold ledger
When the posting is attempted
Then it is refused with `errors.treasury.ledgers_must_not_net`
*Fails if:* the endpoint allows the posting because both accounts individually pass their own floor and
scope checks, missing that the pair itself crosses two of the five ledgers that must never be netted.

**TC-3-027 · a non-`HoldRelease` posting cannot move value out of Hold before handover**
`AC-301-E` · P1 · Api, real PostgreSQL · spec.md §5.1 · CLAUDE.md
Given a Hold account with an accrued balance on a project not yet at handover
When a posting of any type other than `HoldRelease` attempts to debit it
Then it is refused with `errors.treasury.hold_only_grows`
*Fails if:* any posting type other than `HoldRelease` is allowed to draw down the Hold balance before
handover, even by one unit — the exact mistake `CLAUDE.md` names as "you have misread the spec."

**TC-3-028 · a payment that would breach the Safe's floor is refused, not clamped**
`AC-301-F` · P1 · Api, real PostgreSQL · spec.md §6.1, §15 · D-044 §8
Given the Safe's derived balance is less than the posting amount
When the posting is attempted
Then it is refused with `errors.treasury.negative_balance`, and the response names an owner injection as
the next step
*Fails if:* the posting silently succeeds and clamps the Safe to zero, or succeeds and leaves the Safe
negative, instead of refusing outright — either would breach the hard floor D-044 §8 requires.

**TC-3-029 · a posting whose project tag mismatches its accounts is refused**
`AC-301-G` · P1 · Api, real PostgreSQL · spec.md §6.10
Given one account belonging to project A and a `projectId` naming project B, and, separately, a
`projectId` of `null` against two project-scoped accounts
When each posting is attempted
Then the first is refused with `errors.treasury.project_tag_forbidden` (or `_required`, as appropriate)
and the second with `errors.treasury.project_tag_required`
*Fails if:* the endpoint trusts the caller-supplied `projectId` without cross-checking it against the
accounts actually named, letting a project-A posting land tagged as project B.

**TC-3-030 · a posting dated inside a closed accounting period is refused**
`AC-301-H` · P1 · Api, real PostgreSQL · spec.md §6.6
Given an `AccountingPeriod` closed for the posting's date
When the posting is attempted
Then it is refused with `errors.treasury.closed_period`
*Fails if:* the endpoint checks the period against today's date rather than the posting's own dated
value, letting a back-dated posting land inside a period Finance already closed.

**TC-3-031 · role without project assignment is refused with 403 before any domain check runs**
`AC-301-I` · P1 · Api, real PostgreSQL · CLAUDE.md ("role and assignment... server-side, always")
Given a Finance user holding `TreasuryPostProject` but with no assignment row on the target project
When they attempt a project-tagged posting
Then the request is refused with `403`, and no domain validation or database write is attempted first
*Fails if:* the endpoint runs the domain/database checks before the assignment check, so a request that
should be refused purely on assignment instead surfaces a different error (or, worse, succeeds because
the domain and database have no assignment concept to refuse on).

**TC-3-032 · HELD — every domain-checked rule is also enforced independently by the database, cross-cutting `KAFF-319`**
`AC-301-J` · P1 · Api, real PostgreSQL · spec.md §6.1 (rule 10)
⛔ **HELD, cross-cutting `KAFF-319`.** Given a direct database write that bypasses `Posting.Create` (a
future caller, a test harness writing the row directly)
When the row is attempted
Then — held: the story itself marks this criterion HELD, satisfied by the guards already in
`001_guards.sql` and not by new SQL this story would add; asserting it as a passing case here would
require `KAFF-319`'s exception-to-`ProblemDetails` mapping to exist first, so the guard fires
observably rather than as a raw `PostgresException`. *Not a passing case.* Cased again, as a passing
scenario, once `KAFF-319` ships its mapping.

---

# KAFF-302 · Create a project's account set on creation

**TC-3-033 · a Lump Sum project gets the full five-account set**
`AC-302-A` · P1 · Api, real PostgreSQL · spec.md §5.1, §6.3
Given a Lump Sum project is created for a client
When creation succeeds
Then accounts of type `ClientAdvance`, `Hold`, `FirmAdvance`, `MaterialAdvance` and `ClientReceivable`
all exist, scoped to that project and that client, each carrying the class/normal-balance/floor
`AccountTypes` dictates
*Fails if:* any one of the five account types is missing, or one is created with a class, normal
balance or floor that does not match `AccountTypes`' own metadata for it — a silent copy of D-034's
mistake for a different account.

**TC-3-034 · a Cost Plus project gets no Hold and no تشوينات account**
`AC-302-B` · P1 · Api, real PostgreSQL · spec.md §5.2 ("No hold. No تشوينات.")
Given a Cost Plus project is created
When creation succeeds
Then no `Hold` account and no `MaterialAdvance` account exist for that project
*Fails if:* the account-set creation step is not contract-type-aware and opens a `Hold` or
`MaterialAdvance` account for every project regardless of type, giving a Cost Plus project a ledger
`spec.md` says it must never have.

**TC-3-035 · a Design project gets no Hold and no تشوينات account**
`AC-302-C` · P1 · Api, real PostgreSQL · spec.md §5.3 ("no hold, no تشوينات")
Given a Design project is created
When creation succeeds
Then no `Hold` account and no `MaterialAdvance` account exist for that project
*Fails if:* Design is folded into the same code path as Lump Sum and inherits its Hold/تشوينات
accounts.

**TC-3-036 · عهدة is never opened as part of the automatic account set**
`AC-302-D` · P1 · Api, real PostgreSQL · spec.md §6.3
Given a project of any contract type is created
When creation succeeds
Then no `PettyCashAdvance` account is created as part of the set
*Fails if:* a `PettyCashAdvance` account is created with no employee attached, violating its
`PartyType.Employee` requirement and pre-empting `KAFF-311`'s per-employee opening flow.

**TC-3-037 · account-set creation is all-or-nothing with project creation**
`AC-302-E` · P2 · Api, real PostgreSQL · spec.md §6.1 (consistency), CLAUDE.md
Given a project creation request where account creation would fail (a forced metadata defect or
database error injected in the account-creation step)
When the request completes
Then the project does not exist afterward either — no project row is left with zero accounts to post
against
*Fails if:* the project row commits before the account-set step runs and stays committed when that
step fails, leaving a project `KAFF-301`'s endpoint cannot post against.

**TC-3-038 · creating the account set twice for the same project is a no-op the second time**
`AC-302-F` · P1 · Api, real PostgreSQL · CLAUDE.md ("never update or delete a posting" pattern extended to accounts)
Given a project whose account set already exists
When the creation step runs again (a retried request, a re-run migration path)
Then no duplicate account is created and no existing account's row is edited
*Fails if:* re-running the step creates a second `ClientAdvance` account for the same project (breaking
every later query that assumes one account per type per project) or rewrites an existing account's
fields.

**TC-3-039 · every created account passes the same validation a hand-built `Account.Create` call would**
`AC-302-G` · P1 · Api, real PostgreSQL · spec.md §6.3
Given the account set for any contract type
When each account is created
Then scope, party and metadata validation all run and pass exactly as `Account.Create` already
enforces — no direct database insert bypasses it
*Fails if:* the seeding path inserts account rows directly against the database, bypassing
`Account.Create`'s `ValidateScope`/`ValidateParty` checks, so a future change to those rules silently
stops applying to project-creation-time accounts.

---

# KAFF-303 · Correct a mistake with a reversing posting

**TC-3-040 · reversing a posting creates a full mirror with accounts swapped**
`AC-303-A` · P1 · Api, real PostgreSQL · spec.md §6.1
Given an existing posting from account X to account Y for amount N
When it is reversed
Then a new posting is created from Y to X for amount N, same type, same source document, with
`ReversesId` set to the original's id
*Fails if:* the reversal is created with a different amount, a different type, or with `ReversesId`
left unset — any of which breaks the "full mirror through a reference" shape rule 2 requires.

**TC-3-041 · a posting already reversed cannot be reversed a second time**
`AC-303-B` · P1 · Api, real PostgreSQL · spec.md §6.1, unique index on `reverses_id`
Given a posting that already has a reversal
When a second reversal of the same original is attempted
Then it is refused with `errors.treasury.posting_already_reversed`
*Fails if:* the endpoint's own pre-check misses this and it falls through to a raw unique-constraint
violation instead of the translated message, or — worse — a second reversal row is written because no
check runs before the insert.

**TC-3-042 · HELD — a reversal of a reversal is refused, but its exact i18n key is not yet assigned**
`AC-303-C` · P1 · Api, real PostgreSQL · spec.md §6.1
⛔ **HELD, cross-cutting `KAFF-319`.** Given a posting that is itself a reversal (`ReversesId` is set)
When a reversal of it is attempted
Then — held: `KAFF_REVERSAL_OF_REVERSAL` fires at the database but has no matching domain `TreasuryErrors`
member or `errors.treasury.*` key yet, per the story's own note. *Not a passing case* until `KAFF-319`
assigns the mapping — asserting a specific `messageKey` here would invent it. Cased again, naming the
real key, once `KAFF-319` ships.

**TC-3-043 · a reversal of a Hold-ledger accrual succeeds even though a normal posting would be refused**
`AC-303-D` · P1 · Api, real PostgreSQL · spec.md §5.1
Given a Hold account with value accrued by a `HoldAccrual` posting made in error
When that posting is reversed
Then the reversal succeeds and debits the Hold account, even though the project has not reached
handover
*Fails if:* the reversal path runs through the same "hold only grows" check a forward posting does and
is refused, leaving Finance with no way to correct a wrongly-accrued Hold entry before handover.

**TC-3-044 · a reversal dated into a closed accounting period is refused**
`AC-303-E` · P1 · Api, real PostgreSQL · spec.md §6.6
Given a closed `AccountingPeriod` covering the reversal's intended date
When the reversal is attempted
Then it is refused with `errors.treasury.closed_period`
*Fails if:* the reversal endpoint dates the new posting differently from the original (e.g. always
"today") to dodge this check, or skips the closed-period guard because it only runs on the forward
posting endpoint.

**TC-3-045 · role without project assignment cannot reverse a project-tagged posting**
`AC-303-F` · P1 · Api, real PostgreSQL · CLAUDE.md ("role and assignment... server-side, always")
Given a Finance user with `TreasuryPostProject` but no assignment on the project
When they attempt to reverse a posting on that project
Then the request is refused with `403`
*Fails if:* the reversal endpoint checks the reversing user's assignment against some default/company
scope instead of the specific project the original posting is tagged with.

---

# KAFF-304 · Every balance is derived by summing postings

**TC-3-046 · a single account's balance matches a hand-summed total to four decimal places**
`AC-304-A` · P1 · Domain · spec.md §6.1
Given an account with a known posting history
When its balance is requested through the reusable query object
Then `Inflow`, `Outflow`, `RawBalance` and `SignedBalance` all match a hand-summed total over that
account's postings, to four decimal places
*Fails if:* the query object's `SignedBalance` fails to apply the account's normal-balance direction,
so a liability account with money owed on it reads negative instead of positive.

**TC-3-047 · a project's five ledgers are returned as five independent figures, never netted**
`AC-304-B` · P1 · Domain · spec.md §6.4
Given a project with postings against several of its five ledgers
When `LedgerBalances` is requested for that project
Then the response carries `ClientAdvance`, `Hold`, `FirmAdvance`, `PettyCashAdvance` and
`OwnerCurrentAccount` as five independent figures, plus `MaterialAdvance` alongside them, and no field
sums or nets any two of the five
*Fails if:* a `Total` property (or any field computed by subtracting one ledger's figure from another's)
is added to the response shape, which `AccountBalance.cs`'s own remarks say there must never be.

**TC-3-048 · two different callers of the balance query object go through the same mechanism**
`AC-304-C` · P2 · Domain · this story's own reason for existing
Given two call sites that both need a project's hold balance (a hold-release check and a dashboard
read, simulated as two independent test callers)
When each calls the query object this story builds
Then both invoke the same type/method, not two independently written LINQ queries against
`account_balances`
*Fails if:* a second, parallel query is written against the `account_balances` view for a new caller
instead of reusing this story's mechanism — the exact duplication this story exists to prevent.

**TC-3-049 · a balance read executes as a single aggregate query, never a fetch-then-sum in C#**
`AC-304-D` · P1 · Domain · this story's own performance requirement
Given a project with a large posting history
When its balance is read through the query object
Then exactly one aggregate query reaches PostgreSQL (verified by query-count assertion or generated SQL
inspection) and no `Posting` rows are materialised into a C# collection to be summed in memory
*Fails if:* the query object fetches all matching `Posting` rows into memory and sums them in C#
instead of letting the `account_balances` view (or an equivalent aggregate) do it in the database — the
exact anti-pattern rule 5 forbids, and the one that stops scaling once a project accumulates postings.

**TC-3-050 · an account with no postings returns a zero balance, not an error or a null**
`AC-304-E` · P1 · Domain · this story's own consistency requirement
Given a newly created account with no postings yet
When its balance is requested through the query object
Then `Inflow`, `Outflow`, `RawBalance` and `SignedBalance` are all exactly zero, and no exception is
thrown
*Fails if:* the query object throws or returns `null` for an account with no rows in
`account_balances`, instead of relying on the view's own `COALESCE(..., 0)` columns.

**TC-3-051 · HELD — the balance-reading endpoint's permission gate is not yet named**
`AC-304-C`/endpoint surface · — · Api, real PostgreSQL · this story's Definition of Ready
⛔ **HELD `Q90`.** *"Who is allowed to see a project's ledger balances — the client advance, the hold,
the firm advance — day to day? Finance and the Owner, obviously. Does Technical Office or a Site
Engineer assigned to the project see any of it, or is it Finance-and-Owner only?"* Given the reusable
query object from `TC-3-046`…`050`, when an HTTP endpoint is built to expose it to a caller other than
another backend feature, then — held: no `Permission.TreasuryRead`/`BalanceRead` (or equivalent) exists
in the catalogue, and the story's own Definition of Ready blocks the endpoint shape on this question.
*Not a passing case.* The query object itself is not blocked — see `TC-3-046`…`050`, which are cased
and runnable today.

---

# KAFF-305 · The posting-type × account-pair legality table

**TC-3-052 · a legal pair is accepted**
`AC-305-A` · P1 · Api, real PostgreSQL · spec.md §15
Given `PostingType.ClientAdvanceReceipt` between a `Bank`/`Safe` account and a `ClientAdvance` account
When the posting is attempted
Then it succeeds
*Fails if:* the legality table is built inverted or over-restrictively and refuses a pair `spec.md`
itself names as legal, blocking every legitimate client-advance receipt.

**TC-3-053 · an illegal pair is refused by both the domain and, if bypassed, the database**
`AC-305-B` · P1 · Api, real PostgreSQL · spec.md §6.2, §6.3 · D-034
Given `PostingType.SupplierPayment` attempted between a `Hold` account and a `ClientReceivable` account
When the posting is attempted through the domain, and separately via a direct database insert that
bypasses the domain
Then both are refused — the domain with a translated message, the database independently
*Fails if:* the domain refuses the pair but the database has no matching guard, so a future caller that
skips `Posting.Create` could still write the row `spec.md` says must never exist — the exact D-034
failure mode this story generalises against.

**TC-3-054 · only `HoldRelease` may debit the Hold ledger, enforced by the type-aware table, not merely the ledger check**
`AC-305-C` · P1 · Api, real PostgreSQL · spec.md §5.1
Given a posting attempting to move value out of a `Hold` account with a type other than `HoldRelease`
When it is attempted
Then it is refused by the legality table
*Fails if:* the table's Hold rule is coded as "any type may debit Hold as long as the ledger-level
`KAFF_HOLD_DEBIT` guard allows it," collapsing the type-aware check into the ledger-aware one `KAFF-301`
already has, which duplicates rather than generalises `AC-301-E`.

**TC-3-055 · تشوينات moves only via its two named posting types**
`AC-305-D` · P1 · Api, real PostgreSQL · spec.md §5.1, §15 · D-034
Given a posting of any type other than `MaterialAdvanceIssue` or `MaterialAdvanceRecovery` attempting to
touch a `MaterialAdvance` account
When it is attempted
Then it is refused
*Fails if:* a posting type unrelated to تشوينات (e.g. `SupplierPayment`) is allowed to touch a
`MaterialAdvance` account because the table only checks the *from* side and not the *to* side, or vice
versa.

**TC-3-056 · a non-cash posting type is refused if either side is `Safe` or `Bank`**
`AC-305-E` · P1 · Api, real PostgreSQL · spec.md §6.2
Given `PostingType.Depreciation` (or any other `PostingNature.NonCash` type) attempted with `Safe` or
`Bank` as either side
When it is attempted
Then it is refused
*Fails if:* the check only inspects the `from` account and not the `to` account, letting a non-cash
type land against a cash instrument on the receiving side.

**TC-3-057 · a reversal is checked against the table using its own inherited type, not exempted**
`AC-305-G` · P1 · Api, real PostgreSQL · spec.md §6.2 (rule 9)
Given a reversal of a legal original posting
When the reversal is validated
Then it passes the same legality check its type would need to pass forward, using the swapped account
pair — and a reversal of an illegal-if-forward pair (constructed by first allowing the original, then
attempting a hypothetical reversal that would create a new illegal forward pair) is still checked, not
waved through
*Fails if:* the legality check is skipped entirely for `IsReversal == true` postings, on the mistaken
assumption that "it mirrors something already legal" is itself a sufficient pair-legality guarantee for
every possible original.

**TC-3-058 · every ruled `PostingType` has at least one legal pair — the exhaustiveness mechanism, run against stated pairs**
`AC-305-F` · P2 · Api, real PostgreSQL · spec.md §6.3 ("not an open-ended chart of accounts")
Given the subset of the 41-member `PostingType` enumeration whose legal pair `spec.md` or
`PostingType.cs`'s own remarks state explicitly (i.e. every type this story's rules 2–8 name)
When the legality table is built and the exhaustiveness test runs
Then the test asserts each of these named types has at least one row in the table
*Fails if:* a stated type (e.g. `HoldAccrual`, `MaterialAdvanceIssue`, `ClientAdvanceReceipt`) is
missing a row and the exhaustiveness test does not fail loudly enough to be noticed — the same
discipline `PostingTypes.NatureOf`'s own remark requires for cash/non-cash classification.

**TC-3-059 · HELD — five posting types have no account pair stated anywhere, and `Q91` has not ruled them**
`AC-305-F` (unruled types) · — · Api, real PostgreSQL · this story's Definition of Ready
⛔ **HELD `Q91`.** *"For several posting types — `SiteExpensePayment`, `AssetPurchase`, `LoanDrawdown`,
`PeriodCloseTransfer`, `YearEndProfitTransfer` among them — spec.md names the business event but not the
exact pair of accounts it moves between. Is the account pair for each of these obvious enough to infer
from the account tree, or does each need its own ruling the way تشوينات did?"* Given the same
exhaustiveness test from `TC-3-058`, when it is run against these five named types, then — held: no
legal pair can be asserted for any of them without inventing one, which is the exact D-034 failure mode
this story exists to prevent. *Not a passing case.* The exhaustiveness test itself will name these five
as gaps once built — this case exists so that fact is expected, not a surprise, and so the five names
are recorded rather than discovered fresh when `Q91` comes back.

---

# KAFF-319 · A refused posting reads as a translated message, not a 500

**TC-3-060 · a database guard firing without a prior domain refusal returns a translated `ProblemDetails`, never a 500**
`AC-319-A` · P1 · Api, real PostgreSQL · CLAUDE.md (i18n), spec.md §1
Given a posting that the domain check would have allowed but the database guard refuses (simulated by
bypassing the domain pre-check directly against the database)
When the request completes
Then the response is a `ProblemDetails` carrying `code` and `messageKey` matching the guard that fired,
with a 400 or 409 status — not `Status500InternalServerError`
*Fails if:* the `PostgresException` propagates unmapped to `UseExceptionHandler` and the caller receives
a bare 500 with no `messageKey`, exactly the gap this story exists to close.

**TC-3-061 · every one of the twelve named guard exceptions has a mapping**
`AC-319-B` · P1 · Api, real PostgreSQL · `001_guards.sql`'s twelve `RAISE EXCEPTION` sites
Given the twelve `KAFF_*` exception prefixes the guards can raise (`KAFF_NEGATIVE_BALANCE`,
`KAFF_LEDGER_NETTING`, `KAFF_HOLD_DEBIT`, `KAFF_CLOSED_PERIOD`, `KAFF_REVERSAL_MISMATCH`,
`KAFF_REVERSAL_OF_REVERSAL`, `KAFF_REVERSAL_TARGET_MISSING`, `KAFF_CROSS_PROJECT`, `KAFF_PROJECT_TAG`,
`KAFF_ACCOUNT_NOT_POSTABLE`, `KAFF_ACCOUNT_INACTIVE`, `KAFF_CURRENCY_MISMATCH`, `KAFF_ACCOUNT_MISSING`)
When an exhaustiveness test enumerates the twelve prefixes against the mapping table
Then every one resolves to an existing or new `TreasuryErrors` member with an `errors.treasury.*` key
*Fails if:* one prefix (most likely `KAFF_REVERSAL_OF_REVERSAL`, which `KAFF-303` already notes has no
domain `Error` today) is left unmapped and falls through to the generic 500 path this story exists to
close off.

**TC-3-062 · a mapped refusal resolves through the existing i18n catalogue, no second mechanism**
`AC-319-C` · P2 · Api, real PostgreSQL · CLAUDE.md ("no hardcoded user-facing strings")
Given a mapped database-guard refusal
When it reaches the Angular client
Then it resolves through the existing `errors.treasury.*` catalogue exactly as a domain-level refusal
would, with no separate translation table or string introduced by this story
*Fails if:* this story adds a second, parallel message catalogue for database-originated refusals
instead of reusing the existing `errors.treasury.*` keys and `ResultExtensions` pipeline.

**TC-3-063 · an unrecognised database exception still fails safely as a 500, the fallback is not removed**
`AC-319-D` · P2 · Api, real PostgreSQL · CLAUDE.md ("exceptions are for genuinely exceptional cases")
Given a database exception that is not one of the twelve named guard prefixes (a genuinely unexpected
failure, simulated with an injected fault)
When it propagates
Then it still reaches the generic exception handler and returns 500
*Fails if:* the new catch-and-map logic swallows an unrecognised exception silently (e.g. returning a
generic 400 with no detail) instead of letting it fall through to the existing 500 fallback, hiding a
genuine unexpected failure behind a misleadingly specific-looking response.
