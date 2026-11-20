# KAFF-500 · The extract chain — prepare, gate, deduct, approve, issue

<!-- kaff id=KAFF-500 slice=5 points=21 state=VERIFIED verdict=CONDITIONAL at=432b3d3 on=2026-11-20 -->

**Slice:** 5 (Billing) · **Epic:** مستخلص · **Points:** 21 (absorbs `KAFF-501`, `KAFF-502`, `KAFF-503`, `KAFF-504`).
**Owner:** Backend, then Frontend.
**Spec:** `spec.md` §7 (the chain), §6 (the money it moves), §15 (the worked example it must reproduce).
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, step 6.

## Depends on, and will be refused without

| Needs | Why | State |
|---|---|---|
| The BOQ, frozen at signature | An extract bills against contract lines, not typed numbers | **done** |
| `PostingSetBuilder` | An extract approval is about six postings that must commit together; `PostMovement` posts one | **done** |
| `PostingProjectStatePolicy` | `spec.md` §7: "a stopped project MUST NOT issue extracts" | **done** |
| The daily log, `KAFF-600` | An extract is assembled from **executed** quantities | **done** — `acdc2f0`, `D-188` |
| The three billing calculators | An extract's figures must be derived, not typed | **done** — `4c79016`, `D-186`. `spec.md` §15 is now produced by them |
| The ruling on when an extract posts | Changes what the Owner's screen previews and whether a rejection needs reversing postings | `RulingSet.ExtractPostingMoment`, default `OwnerApproval`, **unruled** |

**Every blocker above has landed, so this story is ready to start.** It was held back until they did,
because the study's refusal list is explicit: no extract UI before the posting builder, and the chain
driven from typed quantities is the defect this ordering exists to prevent.

One dependency is still open and is **not** a blocker, by Nabil's standing instruction to build without
waiting: the ruling on when an extract posts. It runs on `RulingSet.ExtractPostingMoment` with
`OwnerApproval` as the default, so Karim's answer stays a one-line change.

*Dependency states verified against the repository 2026-11-01. This table said "not built" and "in
progress" for a day after both had landed — a story's own dependency table is exactly the kind of line
somebody reads instead of looking, so it is dated now.*

## Story

The Technical Office assembles a client extract for a period from the executed quantities the site
recorded. It passes a blocking quantity gate, Accounts applies the deductions, the Owner approves every
one of them, and it issues. At some ruled moment in that sequence it becomes six postings in the ledger.

## The blocking defect found before this story started — **FIXED 2026-11-01**

**Resolved by `e48de3a`, `49522a9` and `910561a`, and recorded in `D-190`.** `ExtractPostings.Build` is
replaced by two methods: `BuildAccrual` posts revenue recognition, hold accrual, client-advance recovery
and both تشوينات legs, touches **no cash account**, and leaves the net payable standing as a debit in
`ClientReceivable`; `BuildCollection` carries the `ClientCollection` and `TaxWithheldAtSource` legs and is
gated on `ExtractStage.Collected`. `tests/Domain.Tests/ExtractPostingTimingTests.cs` asserts the timing
the totals could not see. Gates at the fix: build 0 warnings, Domain.Tests 402 of 402, Api.Tests 732 of 732.

**So the endpoint slices may now be built.** What follows is kept as the record of the defect and of why
the test has the shape it does — read it before writing a posting test, not as a live warning.

---

### The defect as it was

**`ExtractPostings.Build` posts the client's cash into the safe at the moment the extract is approved.**
`src/Domain/Contracts/Billing/ExtractPostings.cs:72`:

```csharp
.AddIfPositive(accounts.ClientReceivable, accounts.Safe, cash, PostingType.ClientCollection);
```

That leg, and the `TaxWithheldAtSource` leg below it, are inside the single posting set the extract writes
when it reaches `ExtractPostingMoment` — `OwnerApproval` by default. **`spec.md` §7 makes collection a
separate, later step:** `→ Owner approval → Issued → Collected`. Raised by the BA while cutting
`KAFF-316`, and verified against the source.

§15 does not license this. Its table constrains **amounts** — one row per extract with a "Client pays"
column — and says nothing about **when** the cash arrives relative to approval. §7 is what constrains the
timing, and the two are not in conflict; only the code is.

Three consequences, in order of how much they cost:

1. **The safe is credited with money the client has not paid.** Every approved-but-uncollected extract
   inflates it. The safe-floor constraint — the one rule `CLAUDE.md` puts in the database rather than in
   application code — is then evaluated against a balance that includes cash nobody has received, so **a
   payment out can pass that should have been refused.** The journey study already corrected one error in
   this exact area: approving a client extract takes no cash out. It does not bring cash in either.
2. **`ClientReceivable` nets to zero at approval**, so the receivable ledger never shows what the client
   owes. A receivable that is always zero is not a receivable, and the five-ledger strip would show it as
   settled while the client has paid nothing.
3. **Postings are append-only.** Once this has run against real data it can only be reversed, never
   edited, which is what makes a wrong posting rule expensive rather than merely wrong.

§15's invariants still pass today, because the figures net out correctly by the end. **That is the point:
a totals-only test cannot see a timing error.** Whatever fixes this needs a test that asserts the safe is
*not* credited at approval and *is* credited at collection.

The fix belonged with `KAFF-316` (collections), which owns the later step. **`AC-500-A` has moved
accordingly**: §15's "Client pays" column is now produced by `BuildCollection` at the `Collected` stage,
not by the approval set, so a test driving §15 end to end must carry the extract to `Collected` to see the
cash. `KAFF-316` still owns the collection endpoint that calls `BuildCollection`.

**Four questions `D-190` named and did not answer**, all of which this story inherits: at which stage an
extract accrues (still `OwnerApproval` by ruling); the account pair for `TaxWithheldAtSource` (`Q91` — the
code posts `ClientReceivable → TaxWithheldAtSource` and nothing confirms it); whether a client may pay an
extract in parts, since `BuildCollection` posts the whole net payable in one set; and whether reaching
`Collected` requires the money actually received or is a clerical state, which diverges the moment a
cheque is involved.

A second, smaller one from the same read: `D-186` item 5 says the `TaxWithheldAtSource` account pair is
"pending `Q91`", but `PostingAccountLegality.PendingQ91` does not list it. One of the two is wrong, and
`KAFF-317` says to read the file when building.

---

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | The chain is: prepare → weekly QC report → Technical Office quantity gate **[blocking]** → Accounts → Owner approval → Issued → Collected | `spec.md` §7 |
| 2 | **Owner approval is required on every extract. There is no threshold** | `spec.md` §7 |
| 3 | The Technical Office gate is **splittable**: approve a percentage for payment, hold the remainder until an NCR closes. The extract enters `PartiallyApproved` and the held portion releases when the NCR closes | `spec.md` §7 |
| 4 | **Any rejection at any gate returns the extract to Draft**, with a written reason and a full audit trail. Never a silent step-back | `spec.md` §7 · `CLAUDE.md` |
| 5 | **Nobody creates and approves the same movement.** The gate actor is stored on the record, so the screen can say in advance that the creator may not approve | `CLAUDE.md` · the journey study |
| 6 | A stopped project MUST NOT issue extracts | `spec.md` §7 |
| 7 | The extract shows work value, hold this period, hold to date, advance recovered, تشوينات, and net payable | `KAFF-504` · `spec.md` §6 |
| 8 | The Technical Office gates quantities, **never money**. Site engineers approve nothing financial | `spec.md` §9 |
| 9 | Postings are append-only. If the ruled posting moment precedes a rejection, the rejection writes **reversing** postings referencing `ReversesId` — it does not delete | `CLAUDE.md` |
| 10 | The hold only grows. Nothing debits the hold ledger before handover, and an extract never does | `CLAUDE.md` |
| 11 | The five ledgers never net against each other | `CLAUDE.md` |
| 12 | Deductions are computed on the basis `RulingSet.DeductionRounding` names, at its decimal places, and **the visible column must foot** | `Q82` / `Q21` · the journey study |
| 13 | تشوينات recovery comes from `RulingSet.MaterialAdvanceRecoveryRule`, never from a formula fitted on site | `Q58` |
| 14 | Withholding uses `RulingSet.WithholdingBase`. Withholding tax is **two fields and two accounts**, and no tax module | `spec.md` §6.7 · `CLAUDE.md` |

## Acceptance criteria

**AC-500-A — §15 end to end, through the chain**
Given the §15 acceptance project, its signed BOQ, and its executed quantities recorded through daily logs
When both of §15's extracts run the full chain to Issued
Then every figure in §15's table is reproduced by the chain, and `Section15WorkedExampleTests` no longer
needs to build any posting by hand
*This is the acceptance criterion for the whole system. `CLAUDE.md`, "Testing", priority 1.*

**AC-500-B — the §15 invariants hold after the chain runs**
Given both extracts issued
When the ledgers are read
Then accumulated hold is 20% of contract value; the advance ledger is exactly zero; تشوينات in equals
تشوينات recovered; client cash equals contract value; and the safe never went negative
*Rules: 10, 11.*

**AC-500-C — the Owner gate cannot be skipped**
Given an extract past Accounts
When anyone attempts to issue it without the Owner's approval
Then it is refused, whatever the amount
*Rule: 2.*

**AC-500-D — nobody approves their own**
Given a user who created or prepared an extract
When that same user attempts any gate on it
Then it is refused server-side, and the screen had already shown the control as **absent, not disabled**
*Rule: 5 · the journey study, "Design principles".*

**AC-500-E — the Technical Office gate is splittable**
Given an extract at the Technical Office gate with an open NCR
When a percentage is approved for payment and the remainder held
Then the extract is `PartiallyApproved`, only the approved portion proceeds, and closing the NCR
releases the remainder
*Rule: 3.*

**AC-500-F — a rejection at every gate returns to Draft with a reason**
Given an extract at each gate in turn
When it is rejected at that gate
Then it returns to Draft, the stored reason is required and non-blank, an audit record carries who,
when and why, and no gate is silently stepped back
*Rule: 4.*

**AC-500-G — a rejection after posting reverses, never deletes**
Given the ruled posting moment has passed and the extract carries postings
When it is rejected
Then reversing postings are written referencing the originals through `ReversesId`, and no posting row
is updated or deleted
*Rule: 9.*

**AC-500-H — a stopped project issues nothing**
Given a project in `Stopped`, `Closed` or `Terminated`
When an extract is issued on it
Then it is refused by `PostingProjectStatePolicy`, in the domain
*Rule: 6.*

**AC-500-I — the Technical Office cannot move money**
Given a Technical Office user
When they attempt the Accounts deductions step or any posting
Then it is refused
*Rule: 8.*

**AC-500-J — the six figures, and the column foots**
Given an issued extract
When its document is read
Then it shows work value, hold this period, hold to date, advance recovered, تشوينات and net payable;
every figure declares its kind — calculated with its derivation, entered with who and when, or carried
from a named document; and the printed column adds up to the printed total exactly
*Rules: 7, 12 · the journey study, "Design principles".*

**AC-500-K — the commit is atomic**
Given an extract approval that produces about six postings
When one of them would breach a database guard
Then **none** of them is written, the refusal names the reason and the balance as it then stands, and
nothing is left half-done
*`PostingSetBuilder` · the journey study, "A consequence statement, never a promise".*

## Not in this story

Subcontractor extracts (`KAFF-510`) — same shape, own sub-ledger, and the safe floor genuinely bites
there. Change orders (`KAFF-508`, `KAFF-509`). A disputed issued extract resolving as a revised extract
or a credit note (`KAFF-511`). Collections and the withholding posting itself beyond rule 14's two
fields. Closure, hold release and warranty. The client portal's view of an extract.

## Questions for Karim

1. **When does an extract post to the ledgers — at Technical Office approval, at Owner approval, at
   Issued, or at Paid?** The study found nobody had asked. Unanswered, everything downstream is a
   guess: when revenue is recognised, when the hold accrues, when the advance is recovered, what the
   Owner's screen previews, and whether a rejection needs reversing postings at all — a rejection
   after posting is a very different act from one before. Running default:
   `RulingSet.ExtractPostingMoment = OwnerApproval`. One line unlocks the calculator, the state machine
   and the Owner's screen together.
2. **`Q58` — how is تشوينات recovery derived?** 75% of installed material value reproduces §15 exactly
   (45,000 then 30,000). Confirm or correct. Running default: `MaterialAdvanceRecoveryRule`.
3. **The withholding base** — net payable, period work value, or certified value less hold — and
   **whether the hold release carries withholding.** Running default: `WithholdingBase`, net payable,
   release carries it.
4. **`Q82` / `Q21` rounding** — on the rounded total or per line, and is the printed contractual figure
   2 or 4 decimals? `D-145 §2` settles line items only, and the six-figure display is derived rows, not
   lines. Hold-to-date may drift off exactly 20% of contract value after per-extract rounding. Running
   default: `DeductionRounding`, rounded extract total basis, 2dp, AwayFromZero.
5. **Thirteen journey moments have no permission row at all**, and this story needs four of them: the
   Accounts deductions step, assembling the client extract document, the QC report, and the Owner's
   extract approval.

## Build notes (2026-11-01, stage 1 of 3: the domain)

Built in `src/Domain/Extracts/` and `ExtractConfigurations.cs`, recorded in `D-193`. The `Extract` aggregate,
its stored gate history, the frozen state machine, the EF mapping and migration `ExtractChain`.
`AC-500-A` and `AC-500-B` are proven in `Section15ThroughExtractChainTests`, carrying each extract to `Collected`.
`AC-500-C`, `D`, `E`, `F`, `G` and `H` are proven at domain level in `ExtractChainTests`. `AC-500-I`, `J` and `K`
need the endpoint and document slices and are not done. This story's trailer stays `READY`: stages 2 and 3 remain.

## Build notes (2026-11-01, stage 2 of 3: the endpoints)

Built in `src/Api/Features/Extracts/`, recorded in `D-194`. Tests in `tests/Api.Tests/Features/Extracts/ExtractEndpointTests.cs`.
Every route is `/api/projects/{projectId}/extracts...` and is gated with `ProjectScope.FromRoute()`, so role and assignment are both required.

| Route | Permission |
|---|---|
| `POST /extracts` (prepare) | `ExtractPrepare` |
| `POST /extracts/{id}/qc-report` | `ExtractQcReportSubmit` (Supervisor level; unresolved holder) |
| `POST /extracts/{id}/technical-approval` | `QuantityGateApprove` |
| `POST /extracts/{id}/deductions` | `ExtractDeductionsApply` (Finance alone) |
| `POST /extracts/{id}/owner-approval` | `FinancialMovementApprove` |
| `POST /extracts/{id}/issue` | `FinancialMovementApprove` (assumed: no row exists for issuing) |
| `POST /extracts/{id}/technical-rejection`, `accounts-rejection`, `owner-rejection` | the permission of the gate rejected at |
| `GET /extracts`, `GET /extracts/{id}` | `ProjectRead`; money in the body only for holders of 73, 74 or 33 |

`AC-500-I`, `J` and `K` are proven over HTTP, and `AC-500-A`, `C`, `D`, `F`, `G` and `H` are exercised through the routes.
Not done: `Collect` (`KAFF-316`), `ReleaseHeldPortion`, revising a rejected extract's evidence, and all Angular. This story's
trailer stays `READY`: stage 3 remains.

## Build notes (2026-11-01, revise-evidence gap)

Recorded in `D-195`. `AC-500-F`'s rejection path was a dead end: a rejected extract returned to Draft with the same
quantities and nothing could change them. `Extract.ReviseEvidence` now corrects them, legal only in Draft after a rejection,
re-deriving certified work from the frozen BOQ's executed quantities. Route: `POST /extracts/{id}/evidence-revision`, gated
`ExtractPrepare` with role and assignment. The read model's `actions.reviseEvidence` tells a screen to offer the control, or
leave it out. The revision is a gate record (`EvidenceRevised`) with its actor, extends the cycle's prior actors, and writes no
posting. Tests: `ExtractRevisionTests` (domain) and four in `ExtractEndpointTests`. Open questions are registered as
`ExtractRevisionScope`. Not done: Angular; this story's trailer stays `READY`.
