# KAFF-500 · The extract chain — prepare, gate, deduct, approve, issue

<!-- kaff id=KAFF-500 slice=5 points=21 state=READY -->

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
| The daily log, `KAFF-600` | An extract is assembled from **executed** quantities | **not built** |
| The three billing calculators | Every one returns `CalculatorNotImplemented` today | **in progress** |
| The ruling on when an extract posts | Changes what the Owner's screen previews and whether a rejection needs reversing postings | `RulingSet.ExtractPostingMoment`, default `OwnerApproval`, **unruled** |

**Do not start this story before `KAFF-600` and the calculators land.** The study's refusal list is
explicit: no extract UI before the posting builder, and the chain driven from typed quantities is the
defect this ordering exists to prevent.

## Story

The Technical Office assembles a client extract for a period from the executed quantities the site
recorded. It passes a blocking quantity gate, Accounts applies the deductions, the Owner approves every
one of them, and it issues. At some ruled moment in that sequence it becomes six postings in the ledger.

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
