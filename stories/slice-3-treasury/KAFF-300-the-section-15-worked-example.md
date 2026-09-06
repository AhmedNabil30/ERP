# KAFF-300 · The §15 worked example as a fixture — present and failing before anything else is built

<!-- kaff id=KAFF-300 slice=3 points=5 state=NOT-BUILT verdict=none at=- on=2026-09-07 -->

**Slice:** 3 (Treasury) · **Epic:** Treasury · **Points:** 5 (`stories/backlog.md`'s slice-3 table) · **Status:** **NOT-BUILT.** Cut 2026-09-07 by the BA against `STATUS.md`'s *"What is actually next"* item 3, which had no story file. **One Definition-of-Ready box is unticked, and it is not `Q14` — see *Definition of Ready* below.**
**Spec:** **§15** (the whole of it), §6.1, §6.4 · **Decisions:** **D-034** (which mandates this story by name), **D-044 §6** and **§8**, **D-033**, D-096 §1
**Register:** `stories/questions-for-karim.md` → **`Q14`** (open, and what it does and does not gate is set out below) and **`Q58`** (new, raised by this story)
**Owner:** Backend
**Depends on:** nothing. Everything it needs shipped in slice 0.

## Why this story exists, and why it is first

**`decisions.md` D-034 mandates it in as many words**, as the closing paragraph of a defect entry:

> *"**The wider lesson, and the real gap.** Slice 0 has no test of the `spec.md` §15 worked example,
> even though `CLAUDE.md` puts it first in the testing priority order. A structural test of the account
> catalogue passed while the catalogue said something economically false. **The §15 fixture must exist
> before slice 3 opens — failing or skipped, but present, so the gate is a build outcome.**"*

D-034 is worth reading in full before building this, because it is the case study: `MaterialAdvance`
was modelled as an asset with a floor, **the Architect's own domain test posted it in the wrong
direction so the test agreed with the defect**, and the error was found only by walking §15's Extract 1
by hand. A fixture that hard-codes §15's five rows cannot agree with a defect, because it did not learn
its numbers from the code.

`spec.md` §15's own first line: ***"These numbers are a test, not an illustration. Any change that
breaks them fails the build."*** `CLAUDE.md` → *Testing* puts *"the `spec.md` §15 worked example and its
invariants"* first in the priority order and calls them *"the acceptance criteria for the whole
system."* `agents.md` → *Slice sequence* makes them slice 3's gate: **"the worked example reconciles."**

## ⚠️ Two corrections to how this story has been described, made in writing rather than assumed

**1. It is not true that "no Treasury code exists."** Slice 0 shipped a substantial amount of it, and
building the fixture against a blank page would duplicate all of it:

| Already built | Evidence |
|---|---|
| The five ledgers as account types, plus `MaterialAdvance` | [Verified: 2026-09-07 @ `src/Domain/Treasury/AccountType.cs` -> `ClientAdvance`, `Hold`, `FirmAdvance`, `PettyCashAdvance`, `OwnerCurrentAccount`, `MaterialAdvance`] |
| `Posting`, `Account`, `AccountBalance`, `PostingTypes`, `TreasuryErrors`, `AccountingPeriod` | [Verified: 2026-09-07 @ `src/Domain/Treasury/` — the eleven files] |
| The database guards: append-only, no-truncate, posting validity, the non-negative floor, hold-release-in-full, account-configuration immutability | [Verified: 2026-09-07 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` -> `FindMissingGuardsAsync` — the eight trigger names it requires] |
| Two Treasury test files | [Verified: 2026-09-07 @ `tests/Domain.Tests/PostingRuleTests.cs`, `tests/Api.Tests/TreasuryGuardTests.cs`, `tests/Domain.Tests/TestAccounts.cs`] |

**What does not exist is the fixture.** §15 appears today only as a comment beside individual
assertions — *"spec.md §15, Extract 1: 300,000 − 60,000 − 75,000 + 75,000 = 240,000"*
[Verified: 2026-09-07 @ `tests/Domain.Tests/PostingRuleTests.cs`] — which is a citation, not the
worked example. **The table has never been walked end to end by anything.**

**2. §15 has two gates, not one, and this story is the first of them.** `agents.md` → *Slice sequence*
separates them by name: slice 3's gate is **"the worked example reconciles"**; slice 5's is
**"§15 passes end to end."** The three billing calculators exist and are deliberate stubs returning
`BillingErrors.CalculatorNotImplemented`, and their own header says why:
*"Slice 0 delivers the seam … **The arithmetic of `spec.md` §5 and the acceptance figures of `spec.md`
§15 are slice 5.**"* [Verified: 2026-09-07 @ `src/Domain/Contracts/Billing/Calculators.cs`].

**So this fixture is written against postings and derived balances, with §15's figures supplied as
given data — not against a calculator that computes them.** Slice 3 makes the ledgers reconcile to
those numbers; slice 5 makes the calculators produce them. **The same table, asserted twice, at two
different heights.** A fixture written to drive `IBillingCalculator` could not fail for a Treasury
reason and would sit red for two slices for the wrong cause.

## The table this story encodes — `spec.md` §15, verbatim

Contract **1,000,000** · advance **25% = 250,000** · hold **20%** · تشوينات **75% of 100,000 material = 75,000** · advance recovery **25% of period work value**.

| Event | Work | Hold | Advance | تشوينات | Client pays |
|---|---:|---:|---:|---:|---:|
| Advance at signing | — | — | — | — | 250,000 |
| Extract 1 | 300,000 | −60,000 | −75,000 | +75,000 | **240,000** |
| Extract 2 | 300,000 | −60,000 | −75,000 | −45,000 | **120,000** |
| Extract 3 | 400,000 | −80,000 | −100,000 | −30,000 | **190,000** |
| Handover — hold release | — | +200,000 | — | — | **200,000** |
| **Total** | **1,000,000** | **200,000** | **250,000** | **0** | **1,000,000** |

**The five invariants, verbatim, each of which becomes its own named assertion:**
- Accumulated hold = exactly 20% of contract value
- Advance ledger reaches exactly zero, never negative
- تشوينات in equals تشوينات recovered
- Total client cash equals contract value exactly
- No posting sequence can produce a negative safe balance

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | **The figures above are a test, not an illustration.** Any change that breaks them fails the build | `spec.md` §15, first line |
| 2 | **Every financial event is a `Posting`, and balances are always derived by summing postings — never stored** | `spec.md` §6.1 · `CLAUDE.md` |
| 3 | **Postings are append-only.** No update path, no delete path. Corrections are new reversing postings through `ReversesId` | `spec.md` §6.1 · `CLAUDE.md` · [Verified: 2026-09-07 @ `src/Infrastructure/Persistence/Sql/001_guards.sql` -> `trg_postings_append_only`] |
| 4 | **The five ledgers never net against each other** — client advance, hold, firm advance, عهدة, owner current account | `spec.md` §6.4 · `CLAUDE.md` |
| 5 | **The hold only grows.** Nothing leaves it mid-project; it releases **once, in full**, at handover, and the release must leave it at exactly zero | `spec.md` §6.4 and its 2026-08-20 amendment · `CLAUDE.md` · [Verified: 2026-09-07 @ `DatabaseInitializer.cs` -> `FindMissingGuardsAsync` — `trg_postings_hold_release_in_full`] |
| 6 | **Exactly three accounts carry a hard non-negative floor: the Safe, the client advance, and عهدة.** The hold, the firm advance and تشوينات are **not** floored — *"nothing stops تشوينات being recovered past what was issued … §15's 'تشوينات in equals تشوينات recovered' is still required, but is now caught at reconciliation rather than at the posting"* | **D-044 §8** · `spec.md` §6.4 amendment |
| 7 | **Four decimals in storage and in every calculation; two decimals only at display, at the last step.** No `float`, no `double`, anywhere near any of these figures | **D-044 §6** · `spec.md` §6.1 amendment · `CLAUDE.md` |
| 8 | **تشوينات is a liability, recovered as the material is installed — not an asset.** The client pays 75% of the value of material on site but not yet built into certified work: money received for work not yet done, structurally identical to `ClientAdvance` | **D-034** |
| 9 | **The application refuses to start when a database guard is missing.** The fixture's environment is a guarded database, not a bare one; a fixture that reconciles against an unguarded schema proves less than it appears to | **D-033** · [Verified: 2026-09-07 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` -> `FindMissingGuardsAsync`] |

## Permissions, money, audit, i18n

- **Permissions:** **none.** It is a fixture. It adds no endpoint and no catalogue row. Permission
  behaviour on the postings it exercises belongs to `KAFF-301` and after.
- **Money:** it moves none in production, and it is **entirely about money**. Rule 7 is the one that
  bites: every figure in the fixture is a `decimal`, and a `double` anywhere in the fixture — including
  in an assertion's tolerance — is a defect even if the test passes.
- **Audit:** writes none of its own.
- **i18n:** no user-facing string.

## Acceptance criteria

**AC-300-A — the fixture is present, and it is red, before `KAFF-301` is pulled** *(fails if the rule is broken)*
Given the repository at the moment slice 3 opens
When the test suites run
Then a §15 worked-example fixture exists and **reports a failure with §15's own figures in the message** — it is neither absent, nor passing vacuously, nor silently ignored
And if it is skipped rather than failed, the skip carries a reason naming what is missing
*Rule: D-034 — "failing or skipped, but present, so the gate is a build outcome." **A fixture that arrives after the code it grades is not a gate**, it is a description of what was built.*

**AC-300-B — every row of §15's table is asserted, including the "Client pays" column** *(fails if the rule is broken)*
Given the contract, advance, hold, تشوينات and recovery parameters of §15's header line
When the five events are applied in order — advance at signing, extracts 1, 2 and 3, handover
Then each row's **Work, Hold, Advance, تشوينات and Client pays** figures match §15 exactly, to four decimal places
And the totals row matches: work **1,000,000**, hold **200,000**, advance **250,000**, تشوينات **0**, client cash **1,000,000**
*A fixture that checks only the totals passes on two compensating errors. Every row, every column.*

**AC-300-C — the five invariants are five separately-named assertions, not one** *(fails if the rule is broken)*
Given the sequence of `AC-300-B`
When the invariants are checked
Then each of §15's five is its own named assertion, so a failure says **which** invariant broke
*`spec.md` §15's own list. One aggregate assertion named `Section_15_holds` tells the next reader nothing when it goes red, and `CLAUDE.md` calls these "the acceptance criteria for the whole system."*

**AC-300-D — every balance in the fixture is derived by summing postings** *(fails if the rule is broken)*
Given the fixture's expected figures
When a balance is needed
Then it is computed by summing `Posting` rows, and **no figure is read from a stored balance, a running total kept by the fixture, or a column**
And the fixture would go red if a stored-balance column were introduced and diverged
*Rule 2, `spec.md` §6.1, `CLAUDE.md`'s "Never store a balance … If you find yourself adding a `Balance` column, stop — that's the bug." **A fixture that keeps its own running total is a second implementation of the thing it is grading.***

**AC-300-E — nothing in the fixture nets one ledger against another** *(fails if the rule is broken)*
Given the client advance, hold, firm advance, عهدة and owner current account
When any of §15's figures is computed
Then no ledger's balance is offset against another's at any point, including inside the "Client pays" arithmetic
*Rule 4. §15's "Client pays" column is a **sum of separately-derived movements**, not a net of the five ledgers, and the difference is invisible in the result and decisive in the model.*

**AC-300-F — the hold takes nothing out before handover, then releases once, in full, to exactly zero** *(fails if the rule is broken)*
Given extracts 1, 2 and 3
When each is applied
Then the hold ledger only ever grows — **60,000, then 120,000, then 200,000** — and no snag, deduction or adjustment debits it
And at handover it releases **once**, for the whole **200,000**, leaving exactly zero
And an attempted partial release, or any debit before handover, is refused
*Rule 5. `CLAUDE.md`: "If you write code that debits the hold ledger before handover, you have misread the spec."*

**AC-300-G — the client advance reaches exactly zero and never goes negative, and the Safe never goes negative** *(fails if the rule is broken)*
Given advance recoveries of 75,000, 75,000 and 100,000 against an advance of 250,000
When they are applied in order
Then the advance ledger runs 250,000 → 175,000 → 100,000 → **0**, and a fourth recovery is **refused by the database, not by application code**
And no ordering of §15's postings produces a negative Safe balance
*Rule 6, D-044 §8, `spec.md` §6.1's "Enforce in the database, not only in application code."*

**AC-300-H — every figure is `decimal`, exact at four places, and no `float` or `double` appears anywhere in the fixture** *(fails if the rule is broken)*
Given the fixture's source
When it is examined
Then every monetary literal, intermediate and expectation is `decimal`
And no assertion uses a floating-point tolerance — the figures are exact, and a tolerance is what hides the truncation `CLAUDE.md` warns about
*Rule 7, D-044 §6. §15's numbers are whole thousands and **that is precisely why a `double` bug would survive this fixture unless the type itself is asserted.***

**AC-300-I — ⛔ the تشوينات rows are taken verbatim from §15 and carry `Q14` by name; no recovery *rule* is written** *(fails if the rule is broken)*
Given §15's تشوينات column — **+75,000** at extract 1, **−45,000** at extract 2, **−30,000** at extract 3, netting to zero
When the fixture encodes them
Then the three figures are **transcribed from §15's table as given data**, and `Q14` and `Q58` are cited beside them in the fixture itself
And **no formula is written that derives 45,000 and 30,000 from anything** — see *What `Q14` gates* below
*Rules 8, D-034, and `stories/questions-for-karim.md` -> `Q14`, `Q58`.*

## ⛔ What `Q14` gates, precisely — and what it does not

**`Q14` is open and it is Karim's.** Its text in the register, verbatim: *"At extract 1 the client pays
an extra 75,000 for material delivered to site, and it comes off later extracts as that material is
installed — correct?"* — *"One sentence, and **it confirms the D-034 fix**."*

**⚠️ Correction to how `Q14` has been described in briefs: it does not ask which ledger.** The ledger is
decided. D-034 ruled تشوينات a **liability** — *"money received for work not yet done — structurally
identical to `ClientAdvance`"* — and `AccountType.MaterialAdvance` exists in the catalogue today
[Verified: 2026-09-07 @ `src/Domain/Treasury/AccountType.cs` -> `MaterialAdvance`]. **`Q14`
is a confirmation of a mechanic that was inferred from §15's own arithmetic, not a choice between
ledgers.** Nothing here decides it either way, and the confirmation is still owed.

**What it gates:** if Karim contradicts D-034 — if the 75,000 is *not* an extra payment at extract 1, or
does *not* come back as material is installed — then extract 1's client payment of **240,000**, the
recovery rows, and two of the totals all move. **`AC-300-I` is the criterion that would go red**, and it
is marked so that the blast radius is one criterion rather than a rewrite.

**What it does not gate: whether this story is built.** D-034's instruction is that the fixture must
exist *"failing or skipped, but present"* **before slice 3 opens**, and `spec.md` is the business truth
under `CLAUDE.md` whether or not a confirmation has come back. **Holding the fixture for `Q14` would
invert D-034's own ruling** and leave slice 3 with no gate for the sake of a sentence that is expected
to confirm what is already written. So this story is **not** marked `BLOCKED` — the exposure is named on
one criterion instead.

**And a second question, which `Q14` does not cover — `Q58`, raised by this story.** §15 gives the
recovery amounts **45,000** and **30,000** and **no rule that produces them.** They are not 25% of period
work value (that is the *advance* recovery, §16 assumption 2), they are not proportional to work value
(45,000 : 30,000 against 300,000 : 400,000 runs the wrong way), and *"as the material is installed"* is a
description of an event, not an arithmetic. **The fixture does not need the rule — it has the numbers.
The slice-5 calculator does.** Registered as `Q58` rather than inferred; **it is not answered here.**

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ |
| Stable `AC-300-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–9 |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — none |
| Money behaviour named explicitly | ✅ — rules 6, 7, `AC-300-H` |
| Arabic UI strings as i18n keys | ✅ — n/a |
| The audit record it writes is stated | ✅ — none |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** No `TC-` case exists; `qa/slice-3/` does not exist. Eight of the nine criteria are marked *(fails if the rule is broken)*, but the case is **QA's to write, not the BA's**. **Routed to QA.** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ — **`Q14` gates one criterion's figures, not the story**, for the reason set out above. `Q58` gates a slice-5 calculator this story does not build |

**Trailer reads `NOT-BUILT`, and the QA case is the only thing outstanding.** Flip it to `READY` when
QA's cases land.

## Not in this story

- **Any Treasury endpoint.** Posting a movement is `KAFF-301`; the account set is `KAFF-302`;
  reversals are `KAFF-303`. This story writes a fixture and nothing that serves a request.
- **The billing calculators.** They stay stubs. `agents.md`'s slice-5 gate — *"§15 passes end to
  end"* — is the second reading of this same table and belongs to slice 5, per the calculators' own
  header.
- **The bank account §15's collections land in.** `spec.md` §6.5 defaults client collections to bank,
  and **`Q15` (which banks) is open** — so the fixture asserts the *ledger* movements, and which
  concrete bank account holds the cash is `KAFF-316`/`317`'s and Karim's.
- **Withholding.** §15's worked example carries none; a corporate client's withholding is `KAFF-317`
  and `Q29`.
- **The retention, partitioning and audit concerns of `KAFF-129`.** Different table, different story.

## Questions for Karim

| # | |
|---|---|
| **`Q14`** | **Open, unchanged, and not narrowed here.** Confirms D-034's reading of the تشوينات mechanic. Gates `AC-300-I`'s figures, not this story |
| **`Q58`** | **New, raised by this story.** §15 gives the تشوينات recovery amounts and no rule that produces them. Needed by slice 5's calculator, not by this fixture. **Not inferred here** |
