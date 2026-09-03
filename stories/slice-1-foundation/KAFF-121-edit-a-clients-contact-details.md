# KAFF-121 · Edit a client's name and contact details

**Slice:** 1 · **Epic:** Foundation · **Points:** 3 · **Status:** Ready
**Spec:** §2 (**amended**), §3, §12 · **Decisions:** D-044 (ruling 4), **D-049 (ruling 8)**
**Depends on:** KAFF-119

## Story
As Marketing, I correct a client's name, primary phone, alternate phone, email, address and notes, so
that one file per client stays worth having.

## Two things this story had wrong, both now fixed
**Finding F-09 — the headline behaviour had no domain path, and it still has none.** `Client` exposes
`SetContactDetails(alternatePhone, email, address, notes)`, `SetTaxRegistration` and `Archive`, and
that is the whole of its mutation surface. There is **no setter for `Name` and no setter for the
primary phone** [Verified: 2026-09-04 @ `src/Domain/MasterData/Client.cs` -> `SetContactDetails`, `SetTaxRegistration`, `Archive` — the three
public mutators; `Name` and the `PhoneEntered`/`PhoneNormalised` pair have private setters and are
written only by `Create`].

That is missing capability, not a missing rule (`meetings/2026-08-20-sprint-1-refinement.md`, action
SM-3), so it is in-scope build work rather than a reason to block — but it is the *first* work in this
story, not an assumption underneath it. **A mistyped client name is currently permanent** on a master
record §2 requires to hold *"full history"*. QA has `TC-1-174` and `TC-1-168` written and expected to
fail until it exists.

**Finding F-19 — AC2 asserted a refusal, and the refusal is now wrong anyway.** The old AC2 said a
phone edited into a collision *"is refused as a duplicate"*. `ux/questions.md` Q-UX-4 sub-question 2
listed exactly that as unanswered — and then Karim answered the question underneath it in the other
direction:

> **"Deduplicated by phone" is a warning, not a refusal.** A repeated number shows the operator which
> client already holds it and asks whether to proceed. **It does not block the save.**
> — `spec.md` §2, amended · D-049 ruling 8

**The amendment is written as a property of the record, not of the create path**, and the unique index
is gone — so refusing on edit would need new application code contradicting the ruling. The rule here
is therefore the same rule as KAFF-119's: **warn, name the client, proceed.** *(That reading is stated
explicitly so a later reader can challenge it: Karim was asked about registering a client, and this
story applies his answer to editing one.)*

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | `ClientManage`, `CompanyWide`, Marketing and Owner | §2 · D-044 ruling 4 |
| 2 | The **name** is editable, and every change is in the trail with its before-state | §2 (*full history*) · F-09 |
| 3 | The **primary phone** is editable | §2 · F-09 |
| 4 | Changing the primary phone re-runs the duplicate check on the normalised number, and **warns without blocking**, naming the client that already holds it — the same interaction as registration | §2 amendment · D-049 ruling 8 · KAFF-119 rule 4 |
| 5 | The **code cannot be edited.** Manual editing is forbidden and `Client` has no setter for it [Verified: 2026-08-22 @ `src/Domain/MasterData/Client.cs` -> `Code` — private setter, written only by `Create`] | D-049 ruling 7 · §2 amendment |
| 6 | Changing `ClientKind` from `Corporate` to `Individual` re-applies §6.7: an individual carrying a tax registration number is refused. **Note there is no `Kind` setter either** [Verified: 2026-08-22 @ `src/Domain/MasterData/Client.cs` -> `Kind`], so this rule is build work on the same missing surface as rules 2 and 3 — and the guard must live with the setter, not in a validator (KAFF-120 rule 5) | §6.7 amendment · KAFF-120 |
| 7 | There is no withholding category on the client to edit. It moved to the contract [Verified: 2026-08-22 @ `src/Domain/MasterData/Client.cs` -> `class Client` — no such member; it is @ `src/Domain/Projects/Project.cs` -> `WithholdingCategory`] | D-049 ruling 9 · §6.7 amendment |
| 8 | Notes are internal. `spec.md` §12: the client MUST NEVER see internal notes | §12 |
| 9 | Editing does not archive, and archiving is not an edit — `Archive` is its own method and touches only `IsActive` [Verified: 2026-08-22 @ `src/Domain/MasterData/Client.cs` -> `Archive`] | slice 0 `Client.Archive` |
| 10 | The client record carries no money, so no edit here can change one | §6.1 · CLAUDE.md |

## Permissions, money, audit, i18n
- **Permissions:** `ClientManage`, `CompanyWide`, Marketing and Owner. `Role.Client` must not hold it
  (§12).
- **Money:** moves no money.
- **Audit:** `Modified` on `Client`, before and after, `ChangedProperties` naming the fields. The
  before-state matters here specifically: *"the phone number on file when we sent that invoice"* is a
  question that gets asked. **Where a phone edit proceeded past a duplicate warning, the record says
  so and names the match** — same as KAFF-119, and for the same reason.
- **i18n:** reuses KAFF-119's `clients.field.*` and `clients.duplicate.*` keys, plus
  `clients.edit.title`, `clients.field.notes`, `clients.notes.internal_only`, and
  `clients.field.code.not_editable`.

## Acceptance criteria
**AC-121-A — a name can be corrected at all** *(fails if the rule is broken)*
Given a client whose name was mistyped at registration
When Marketing corrects it
Then the stored name changes, and the audit record carries both the old and the new value

**AC-121-B — a correction is recorded with its before-state**
Given a client with an address
When Marketing changes it
Then the new address is stored, and the audit record carries both values

**AC-121-C — changing the phone re-runs the duplicate check, and warns** *(fails if the rule is broken)*
Given clients A and B with different phones
When A's phone is edited to B's phone
Then a warning is returned naming **B**
And the edit is **not** refused
And on proceeding, A's phone is changed and the audit record records that a duplicate was matched

**AC-121-D — the check runs on the normalised number** *(fails if the rule is broken)*
Given client B holds `01001234567`
When A's phone is edited to `+20 100 123 4567`
Then the warning fires — a format difference must not slip past the only control that is left

**AC-121-E — the code cannot be edited** *(fails if the rule is broken)*
Given a client with code `C-10001`
When an edit supplies a different code
Then the stored code is unchanged, by any route through the API

**AC-121-F — kind changes cannot smuggle a tax registration past §6.7** *(fails if the rule is broken)*
Given a corporate client with a tax registration number
When its kind is changed to `Individual` without clearing the number
Then it is refused with `errors.master.individual_does_not_withhold`

**AC-121-G — nobody outside Marketing and the Owner may edit**
Given I am Finance, then Technical Office, then HR, then a Site Engineer, then a portal Client
When each attempts an edit
Then each is refused with 403

**AC-121-H — internal notes stay internal** *(fails if the rule is broken)*
Given a client with notes
When any endpoint reachable by `Role.Client` is called
Then the notes appear in no response

**AC-121-I — Arabic, RTL, at mobile width**
Given the edit form and the duplicate warning at 390px in Arabic
When they render
Then direction is RTL, no literal strings, phone numbers and emails are bidi-isolated inside Arabic labels, no horizontal overflow

## Not in this story
The contract's withholding rate — it left the client record entirely (D-049 ruling 9) and is
**KAFF-416**, slice 4. Archiving (KAFF-123). Merging duplicates — no merge exists; see KAFF-119.

## Questions for Karim
None that block. **Q39** — what is offered when the matched client is archived — touches rule 4 and is
the same open edge KAFF-119 carries.
