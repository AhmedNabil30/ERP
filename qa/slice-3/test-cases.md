# Slice 3 — test cases

**Range allocated: `TC-3-001` … `TC-3-022`.**

Checked before writing a single case: `grep -rn "TC-3-" qa/ stories/ decisions.md process/` returned
nothing. `qa/README.md`'s ID scheme is `TC-<slice>-<nnn>` — the slice number is part of the
identifier — so `TC-3-nnn` cannot collide with `TC-1-nnn` or `TC-2-nnn` under the scheme as written.
The range is taken as `001`–`022`.

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

**Layer is `Domain` for every case in this file.** `tests/Domain.Tests` exists in this repository
[Verified: 2026-09-14 — `tests/Domain.Tests` is a real project with prior content, e.g.
`PostingRuleTests.cs`, `TestAccounts.cs`]. `KAFF-300`'s own text places this fixture there: it is
written against `Posting`/`Account` derivation directly, supplies §15's figures as given data rather
than driving a calculator, and adds no endpoint — `AC-300-D`'s "no figure is read from a stored
balance, a running total kept by the fixture, or a column" is a Domain-level guarantee about how the
fixture computes its own expectations, not an HTTP contract. No case in this file belongs at `Api` or
`E2E`.

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

**Priority.** Every case in this file is **P1**. `qa/README.md`'s own scale puts "Money" first among
what a P1 gates, and `CLAUDE.md`'s testing priority order puts "the `spec.md` §15 worked example and
its invariants" first of all, calling them "the acceptance criteria for the whole system." Nothing in
`KAFF-300` is a P2 or P3 concern.

## Story coverage index

| Criterion | Cases | Note |
|---|---|---|
| `AC-300-A` | `TC-3-001` | fixture present and red/skipped-with-reason, not the invariants it will hold |
| `AC-300-B` | `TC-3-002`…`007` | one case per event row, plus the totals row |
| `AC-300-C` | `TC-3-008`…`012` | one case per named invariant |
| `AC-300-D` | `TC-3-013` | — |
| `AC-300-E` | `TC-3-014` | — |
| `AC-300-F` | `TC-3-015`…`017` | growth, release, and the two refusals split so a failure names which |
| `AC-300-G` | `TC-3-018`…`020` | the sequence, the refusal, and the Safe-never-negative sweep |
| `AC-300-H` | `TC-3-021` | — |
| `AC-300-I` | `TC-3-022` | **carries `Q14`/`Q58` by name — see the case itself** |

No criterion is HELD. `Q14` narrows `AC-300-I`'s figures if Karim contradicts D-034, and `Q58` gates a
slice-5 calculator this story does not build — neither blocks writing this fixture's case, per the
story's own "What `Q14` gates" section.

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
