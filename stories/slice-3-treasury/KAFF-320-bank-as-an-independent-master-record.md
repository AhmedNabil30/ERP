# KAFF-320 · Bank — an independent master record, not folded into the ledger

<!-- kaff id=KAFF-320 slice=3 points=0 state=NOT-BUILT verdict=none at=- on=2026-09-12 -->

**Slice:** 3 (Treasury) · **Epic:** Treasury · **Points:** not estimated — see *Definition of Ready*. **Status:** **NOT-BUILT.** Cut 2026-09-12 by the BA against D-139 §6, closing `Q13`.
**Spec:** §6.3, §6.5 (client collections default to a bank account) · **Decisions:** **D-139 §6** (which mandates this story by name), D-045 #1 (the original `Q13`)
**Register:** `stories/questions-for-karim.md` → **`Q13`** (✅ answered in part — D-139 §6: banks are independent records; **which banks, and whether any carries an overdraft, stay open as `Q15`/`Q16`**)
**Owner:** Backend, then Frontend
**Depends on:** nothing this story itself needs from slice 2. `KAFF-316`/`317` (Collections, client-side withholding) will depend on this once built — the ordering runs the other way from what `KAFF-212`'s file said before this session corrected it.

## Why this story exists, and why almost nothing in it is decided yet

**`Q13` asked one question**, in `stories/questions-for-karim.md`, D-045 #1: *"When you said bank
accounts — do you mean a list of your banks as records in their own right, or just the accounts
themselves in the ledger?"* **Nabil answered the shape, not the content — D-139 §6:**

> *"Banks are independent master records. No story exists for this. The BA cuts one and places it
> where it belongs, probably slice 3, Treasury. It is not built inside `KAFF-212`."*

That is the whole of what is ruled. **Everything else about a bank — which banks Kaff actually uses,
what fields a bank record carries beyond a name, whether an account can carry an overdraft, who owns
the record, how it relates to the `AccountType` catalogue already in `src/Domain/Treasury/`** — is not
answered anywhere in `spec.md` or `decisions.md`, and none of it is invented here. This story exists so
the shape has a home; its content waits on Karim.

## What already exists

**Nothing bank-specific.** `spec.md` §6.3 and §6.5 refer to a bank account only as the destination of a
client collection or a treasury movement; no bank entity, no bank field and no bank screen exists in
`src/` today [Verified: 2026-09-12 — a repository-wide search for `Bank` under `src/Domain/` and
`src/Api/` returns nothing]. The five ledgers and `AccountType` are unaffected by this story — a bank is
a **new, separate master record**, not a sixth ledger and not a rename of anything that exists.

## Business rules — only what D-139 §6 states

| # | Rule | Source |
|---|---|---|
| 1 | **A bank is an independent master record**, not merely an account row inside the ledger. It has its own identity, separate from any `AccountType` or `Posting` | **D-139 §6** |
| 2 | **This record is not built inside `KAFF-211` or `KAFF-212`.** Neither the subcontractor master nor the supplier master gets a bank field or a bank foreign key because of this story | **D-139 §6** |

⛔ **No rule beyond these two is written.** The field list a bank record carries (name, branch,
account number, IBAN, whether it holds an overdraft limit), who may create or edit one, whether it is
company-wide or has any scoping at all, and how a payment or collection references it are all **open
questions**, listed below rather than guessed.

## Permissions, money, audit, i18n

- **Permissions:** ⛔ **Not named.** No permission row exists for a bank record today, and inventing one
  (`BankManage`, company-wide, Owner and Finance, by analogy with every other master-data row this
  slice built) would be exactly the kind of plausible invention `CLAUDE.md` forbids. **Open question,
  below.**
- **Money:** this record is a master record, not a ledger. **It stores no balance** — whatever cash Kaff
  holds at a bank is a fact the treasury derives from postings against the account the bank record
  identifies, the same rule as every other master record in this slice. Whether a bank record carries a
  reference to one `AccountType` row, several, or none at all is not decided.
- **Audit:** creating, editing and archiving a bank record are state changes; **each would write an
  audit record**, by the same standing rule as every other master record — this is stated as a
  consequence of `CLAUDE.md`, not as a new decision.
- **i18n:** none yet — no field list, no screen.

## Acceptance criteria

**None are written.** A Given/When/Then criterion needs a field list, a permission and a screen, and
none of the three exists yet. Writing one now would be inventing the content D-139 §6 explicitly left
for Karim. **This section is empty on purpose**, not omitted by oversight.

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ⛔ n/a — no criterion is written yet |
| Stable `AC-320-<LETTER>` ids, appended never inserted | ⛔ n/a — none exist to be stable |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–2, both `D-139 §6` |
| No uncited rule | ✅ |
| Permissions named explicitly | ⛔ **Not met.** Open question below |
| Money behaviour named explicitly | ✅ — stores no balance, by the standing rule; what it references is open |
| Arabic UI strings as i18n keys | ⛔ n/a — no screen yet |
| The audit record it writes is stated | ✅ — as a consequence of the standing rule, not a new one |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** Nothing to case yet |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ⛔ **Blocked.** Every open question below stops this story from being estimated, let alone built |

**This story is not Ready and is not estimated.** It exists so `Q13`'s shape has a place to land; its
points, its criteria and its screens all wait on Karim's answers below.

## Not in this story

- **Collections.** `KAFF-316` records the method, date and reference of a client collection, and a bank
  reference is one of those fields — but `KAFF-316` consumes a bank record this story defines, it does
  not define one itself.
- **Withholding at source.** `KAFF-317`, unrelated to where the cash lands.
- **Any change to `AccountType` or the five ledgers.** A bank is a new master record, not a ledger.
- **Any subcontractor or supplier field.** D-139 §6's second sentence exists specifically to keep this
  out of `KAFF-211`/`212`.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q13`** | ✅ **Shape answered — D-139 §6.** Banks are independent master records. **The content is not answered and stays open under `Q15`/`Q16` below** | **Closed** (shape only) |
| **`Q15`** | Already open (Kickoff Q9): *"Which banks — QNB, CIB, الأهلي, others?"* This story cannot be estimated or cased without the list, because the field list a bank record needs may depend on what Kaff's actual banks require (an IBAN, a SWIFT code, a branch code) | **Karim** |
| **`Q16`** | Already open (Kickoff Q10, half answered by D-044 ruling 8 for the ledgers): *"Do any of your bank accounts have an overdraft?"* Bears directly on whether a bank record carries a limit field | **Karim** |
| new | Who owns a bank record — Finance alone, or Finance and the Owner as with every other master row this slice built — and is it company-wide? Not answerable from `spec.md` or any decision; §9's *"role alone is insufficient"* does not obviously apply to a record that touches no project | **Karim** |
| new | Does a bank record reference the `AccountType` catalogue directly (one bank, one or more ledger accounts), or is the relationship built when `KAFF-316` needs it? A modelling question the Architect should take once the field list exists, not before | **Architect**, after `Q15`/`Q16` |
