# The project journey — a three-expert study, 2026-10-08

Nabil asked for the project journey to be studied carefully, called it the main selling point of the
product, and said to spend what it takes. A BA, an architect and a UX designer studied the same
journey independently, then corrected each other, then the Scrum Master reconciled them.

**They converged on one finding, from three directions, without conferring.** That is the point of
this document; everything else is detail.

---

## The finding

**The money core has never produced `spec.md` §15 through production code.**

§15 is the worked example, and `CLAUDE.md` calls it the acceptance criterion for the whole system.
It is satisfied today by `tests/Domain.Tests/Section15WorkedExampleTests.cs`, which **builds its
postings by hand** and says so in its own header. Every `IBillingCalculator` returns
`CalculatorNotImplemented`. Nothing resolves `IContractTypeDispatcher`. So the arithmetic of the
ledger is proven and the behaviour of the product is not. A wrong calculator cannot fail that test,
because the figures it checks are typed into it.

Each expert arrived at the same crack by a different road:

- **The BA** found that §15's تشوينات recovery — 45,000 then 30,000 — **has no formula anywhere in
  the spec**, is not 25%, and runs opposite to the work value. It then found that
  `LumpSumBillingInput` already carries `MaterialInstalledThisPeriod`, that installed value was
  60,000 then 40,000, and that 75% of those is exactly 45,000 and 30,000. **It refused to adopt the
  formula**, because `Q58` exists to stop someone fitting a rule to two numbers. That refusal is
  correct and it is why this is a question for Karim rather than a decision already made.
- **The architect** found that `CreateProject/Request.cs:63` accepts `decimal? ContractValue` as a
  typed input, that no BOQ entity exists in `src/Domain` at all, and that `Project.Sign` stamps a date
  with nothing behind it. Hold, advance and تشوينات are all percentages of that typed number. Build
  the extract chain on it and every §15 figure derives from a value nobody can prove matches what the
  client signed — posting cleanly, balancing, and append-only, so nearly impossible to unwind.
- **The UX designer** concluded that the first money screen is therefore not a feature but an
  experiment, and that the product's credibility rests on whether it passes. Its answer is below.

**Verified independently by the Scrum Master:** `CreateProject` takes `ContractValue` and `Status`;
a search for a BOQ entity returns zero.

---

## What to build first: the proof screen

The UX designer's answer to the finding, and the Scrum Master adopts it.

Before any workflow screen — before the extract decision, the project hub or the journey line —
build **إثبات الأرقام**, a read-only reconciliation screen for Finance and the Owner with one job:
show a project's actual postings beside the figures the contract rules expect, and say line by line
whether they match.

- Left: the expected sequence. For the acceptance project that is literally §15's table.
- Right: what the posting engine actually produced.
- Every row carries a match mark with a word, never colour alone, and every difference shows both
  numbers and both postings.
- The totals row states §15's invariants as live checks: accumulated hold is 20% of contract value,
  the advance ledger is exactly zero, تشوينات in equals تشوينات recovered, client cash equals
  contract value, the safe never went negative.

Three reasons it goes first:

1. It is built from exactly the components every later money screen needs — the postings list, the
   waterfall, the five ledger tiles with their derivation, the reconciliation line. Building it *is*
   building the money half of the kit, against real data, with a pass or fail answer.
2. A gap between the fixture and the production path becomes visible **on day one, in front of the
   team**, instead of in front of the client.
3. It does not stop being useful. Finance opens it at month close; the Owner opens it the first time
   he does not believe a number.

---

## Reconciled build order

The three experts proposed three orders. Reconciled, with the reason each step precedes the next:

| # | Step | Why it must come first |
|---|---|---|
| 0 | **Verify slice 3 independently**, and re-verify `KAFF-204`, `205`, `213` | Everything stands on the posting model and nobody independent has signed it off. The three باب stories carry verdicts anchored at `7117704`, certifying code the 2026-10-07 rebuild replaced. |
| 1 | **Get the five rulings** (below). No code first | Each one changes the shape of what follows. Cheap to ask, expensive to guess — and this project has paid for guessing three times. |
| 2 | **Contract value becomes derived, never typed**: the BOQ entity, lines copied not referenced, a freeze guard in the style of `kaff_reject_mutation`, then `Sign` reading the BOQ total | Signature currently freezes nothing. Slice 4's own gate is "prices provably frozen", and the freeze test must edit the catalogue after signing and assert the BOQ is unchanged. |
| 3 | **The multi-posting builder and a reusable balance query** | An extract approval is about six postings that must commit together; `PostMovement` posts one. The builder is also what makes an honest consequence statement possible, and the guard-violation mapping must move out of `PostMovement` before a second caller needs it. |
| 4 | **The proof screen** | See above. |
| 5 | **The daily log, with period deltas** | An extract is assembled from executed quantities. The backlog has billing (slice 5) before execution (slice 6); built in that order, extracts would have nothing real to bill. |
| 6 | **The extract chain and the three calculators**, driven from §15 end to end, replacing the hand-built fixture | Needs the ruling on when an extract posts, the تشوينات formula, reshaped billing inputs carrying ledger balances, and a stored gate history for separation of duties. |
| 7 | Collections and withholding · change orders · closure, hold release, warranty | Each needs the step above it. |

**Refuse until the prior step exists:** any extract UI before the posting builder; any hold-release
endpoint before the handover state check; any BOQ edit endpoint before the freeze guard; any `Sign`
endpoint before the BOQ.

---

## Decisions needed

### Karim or an accountant

1. **When does an extract post to the ledgers — at Technical Office approval, at Owner approval, at
   Issued, or at Paid?** This is the question nobody had asked and it is not in
   `stories/questions-for-karim.md`. Unanswered, everything downstream is a guess: when revenue is
   recognised, when the hold accrues, when the advance is recovered, what the Owner's screen previews,
   whether a rejection needs reversing postings (append-only, so a rejection after posting is a very
   different act from one before), what `CanIssueExtracts` guards, and when withholding applies. One
   line unlocks the calculator, the state machine and the Owner's screen together.
2. **`Q58` — how is تشوينات recovery derived?** 75% of installed material value reproduces §15 exactly.
   Confirm or correct it; do not let anyone infer it.
3. **The withholding base** — net payable, period work value, or certified value less hold — and
   whether the hold release carries withholding.
4. **`Q82` / `Q21` rounding**: whether deductions are computed on the rounded total or per line, and
   whether the printed contractual figure is 2 or 4 decimals. `D-145 §2` settles line items only. The
   six-figure extract display is derived rows, not lines, so nothing yet guarantees the visible column
   foots, and hold-to-date may drift off exactly 20% of contract value after per-extract rounding.

### Nabil

5. **Who converts a won opportunity into a project?** Marketing holds `OpportunityManage` and closes
   the deal; `ProjectCreate` is Owner and Technical Office. Marketing can win work and cannot open the
   project. Recommended: Marketing marks it won and stops; the Technical Office opens it from a
   "won, awaiting survey" queue, because the survey must precede the contract value.
6. **May Finance post to a Stopped, Closed or Terminated project?** Nothing checks project state on a
   posting today. The honest answer is a per-posting-type allowed-status table enforced in the domain,
   accepting that raw SQL could bypass it.
7. **Thirteen journey moments have no permission row at all** — quotation build, Closed Won, حصر
   survey, contract signature, QC report, the Accounts deductions step, assembling the client extract
   document, stop/restart/terminate, practical completion, snag, handover, hold release, warranty,
   close. About six can reuse an existing row; about eight need their own.
8. **Terminated is entirely unruled.** `§13` names the state and `§6.9` lists "termination settlement"
   as an adjustment case. Nothing says what settles — what happens to the hold, the advance, تشوينات.

---

## Design principles this study established

These came out of the cross-correction and now apply to every screen, not only the journey.

**Every figure on a money screen declares its kind.** Calculated, with its derivation available;
entered, with who and when; or carried, from a named document. تشوينات recovery is "entered, with a
name on it" until a formula is ruled. A number with no provenance does not belong on a money screen.

**A consequence statement, never a promise.** What the document will do is arithmetic and can be
stated exactly. What the database may refuse — the safe floor, the advance floor — cannot be known
before commit, so the screen shows the current figure against the requirement and a timestamp, never
"this will succeed". The refusal at commit is a designed state: nothing half-done, the reason, and the
balance as it now stands.

**The five-ledger strip is presentation, not a guard.** The no-net rule only trips when both accounts
carry a `LedgerKind`, and §15 deliberately routes hold, advance and تشوينات through `ClientReceivable`,
which carries none. So the strip gains a drill-down to the postings behind each tile, a visible
reconciliation line, and an **unclassified bucket** that must stay empty — the cheapest visible guard
against netting nobody can see.

**Field screens are built from a money-free read model.** The site engineer sees quantities, hours,
counts, photos and materials by type — never rates, amounts, costs or balances. Not a desk screen with
columns hidden; a separate read.

**A control the user may not use is absent, never disabled.** With gate actors stored on the record,
the screen can say *in advance* that the person who created a movement may not approve it, instead of
letting them find out by being refused.

**Occurred-at belongs on every field entity.** `CreatedAt` is the sync time. Without the entry's own
time, a log entered offline across midnight lands on the wrong day, and the offline design is not
honest.

**Contents are declared by contract type; an unused group is absent, not empty.** The project
workspace holds for all three types because its five groups are intentions — understand, quantify,
execute, pay, organise — not tables. It stops holding the moment someone renders all three types with
the same widgets and hides the unused fields.

---

## Corrections this study made to itself

Worth recording, because each was caught by one expert reading another's work:

- The **safe floor does not bite on client extract approval.** Approving raises a receivable; it takes
  no cash out. The floor matters on payments out and subcontractor extracts. The Scrum Master had
  repeated the original claim to Nabil before the BA corrected it.
- **§15 "agrees with §6"** was downgraded: it agrees as ledger arithmetic, and says nothing about
  product behaviour.
- **Four dispatch points for contract type, not two** — billing calculator, progress metric, the
  Design-specific transition table, and the account set factory — plus a creation branch and three
  terms setters that will grow into the real fork. The interfaces are the vestigial pair; the
  `switch` statements are the live ones.
- The **health tag has no data behind it.** `متعثرة` and `تم تأجيلها` are tags, not states (`D-044
  §7`), and exist nowhere in `Project`. The header ships with the state alone until the domain has the
  tag. `KAFF-400`'s own text contradicts itself here and the BA must fix it.
