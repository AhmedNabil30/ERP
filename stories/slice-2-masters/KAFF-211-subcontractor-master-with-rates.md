# KAFF-211 · Subcontractor master with rates

<!-- kaff id=KAFF-211 slice=2 points=5 state=NOT-BUILT verdict=none at=- on=2026-09-09 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 5 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-09 by the BA. **⚠️ Amended 2026-09-09 — `Q12` clears (D-129 §1). Two Definition-of-Ready boxes remain unticked, and `stories/backlog.md` names this story as blocked before it was written.**
**Spec:** **§2** (*"rates and BOQ; Finance only disburses"*), **§5.1** (5% retention, zeroable 🟡), **§6.7**, §9 (*"record only, no login"*) · **Decisions:** D-044 ruling 4, D-049 ruling 9 (the rate moved to the contract, **for the client only**), **D-129 §1**
**Register:** `stories/questions-for-karim.md` → **`Q29`** (blocking, and named in the backlog since the slice was estimated), **`Q73`** (blocking), **`Q12`** (✅ answered, D-129 §1), **`Q70`** (⚠️ this is a **different** `Q70` — a repeated subcontractor phone — than the trade-markup `Q70` `KAFF-204` cites; a register numbering collision that is not this session's to resolve)
**Screens:** `ux/screen-inventory.md` → **`S-028`** (list), **`S-029`** (create / edit)
**Owner:** Backend, then Frontend
**Depends on:** KAFF-204 — a subcontractor's trade names a باب

## Story
As the Technical Office, I keep a record for every مقاول باطن Kaff works with, because §2 puts their
rates and their BOQ in my department and puts the disbursement in Finance's — and neither half works
if the firm itself is typed fresh onto every job.

## What already exists, and what this story adds

| Already built | Evidence |
|---|---|
| The entity, with code, name, phone, trade باب and active flag | [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `Subcontractor`, `TradeBabId`] |
| §5.1's **5% retention, held per subcontractor so it can be zeroed without touching anything global** | [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `DefaultRetentionRate`, `SetRetentionRate`] |
| A withholding category and a tax registration number **on the party record** | [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `WithholdingCategory`, `SetTaxDetails`] |
| Archiving, and its refusal when already archived | [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `Archive`] |
| Unique indexes on the code and on the normalised phone | [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_subcontractors_code`, `ux_subcontractors_phone`] |
| The permission row — `CompanyWide`, Owner and Technical Office | [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.SubcontractorManage`] |
| The sub-ledger account type this master will one day be paid through | [Verified: 2026-09-09 @ `src/Domain/Treasury/AccountType.cs` -> `SubcontractorPayable`] |

**What does not exist is any endpoint or screen** [Verified: 2026-09-09 — `src/Api/Features/` holds
`Assignments`, `Audit`, `Auth`, `Clients`, `Health`, `Setup` and `Users`].

## ⛔ Two blockers, and both are at the centre

**1. `Q29` — the withholding rate: per job or per firm.** `stories/backlog.md` has named it as slice
2's third blocker since the slice was estimated, and §6.7's own amendment marks it 🟡 in as many words:
*"Not ruled on: subcontractors and suppliers … those rates are still held on the party record. Karim's
ruling named the client only, so nothing was changed there."* **The rate is on the entity today**
[Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `WithholdingCategory`]. D-049
ruling 9 moved the **client's** rate onto the contract for a stated reason — one client can hold a
design contract and an execution contract at two rates — **and that reason transfers to a
subcontractor exactly as well as it transfers to a client, which is precisely why it may not be
transferred without asking.** ⛔ **Extending the ruling would be inventing it.**

**2. `Q73` — what *"with rates"* in this story's own title means.** §2 gives the Technical Office
*"rates and BOQ"*; §5.1 gives the retention percentage; **nothing in `spec.md` describes a rate card
on the firm.** Two readings: the master carries agreed prices per باب or per item, which a job's
sub-BOQ then defaults from — or rates live only on each job's sub-BOQ and *"rates"* in §2 names the
**ownership** of that data rather than a field here. **The board's title is not a ruling** —
`process/agile.md`'s Definition of Ready is explicit that an uncited rule is a question, and `Q61` is
this board's own precedent for refusing to read a story title as an answer.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | One record per subcontracting firm, owned by the Technical Office. **Finance only disburses** and does not create or edit the record | **§2** |
| 2 | **A subcontractor never logs in.** §9 is *"record only, no login"*; the entity records that a `User` row with this role cannot hold a password [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `Subcontractor`], and `Role.Subcontractor` holds no row in the permission catalogue at all [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Role.Subcontractor`]. **This story creates no `User` and no credential** | **§9** · D-035 |
| 3 | **Retention is 5% by default and is zeroable per subcontractor**, held on the firm rather than globally, so zeroing one changes nothing for the others [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `DefaultRetentionRate`]. 🟡 §5.1 and §16 assumption 19 both flag it, and **`Q26` is the open confirmation** | **§5.1** · `Q26` |
| 4 | ⛔ **Retention is a `Percentage`, not a `Money`, and this story neither holds nor releases anything.** §5.1 releases sub-retention when the warranty ends — that is slice 8. **Nothing here debits, credits or nets any ledger**, and the Kaff-side hold of §6.4 is a different thing entirely and must not be confused with it | **§5.1** · CLAUDE.md — *"The five ledgers never net against each other"* |
| 5 | ⛔ **Where the withholding rate lives is `Q29`.** It is on the party record today [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `WithholdingCategory`]; D-049 ruling 9 moved the client's onto the contract and **named the client only** | **`Q29`** · §6.7 amendment, 🟡 |
| 6 | ⛔ **Whether the master carries a rate card is `Q73`** — see above. **No rate-card field is added until it is ruled**, and the criterion that would assert one is held | **`Q73`** — uncited, therefore asked |
| 7 | **Withholding here is a liability Kaff carries, never an asset.** §6.7: when Kaff pays subcontractors, **Kaff withholds and carries a liability to remit** — the opposite direction from a corporate client's collection, which is a recoverable **asset**. **The two must never net against each other**, and this story's only obligation is to not let a single field or screen suggest they are one number | **§6.7** — MUST · CLAUDE.md |
| 8 | **The tax registration number identifies the legal entity and does not vary by job**, so it stays on the record whatever `Q29` decides about the rate — that is D-049's own reasoning, applied to the field it explicitly kept on the client | **§6.7 amendment** — the client precedent, on the field it kept rather than the one it moved |
| 9 | ⛔ **The subcontractor's phone is unique today — a refusal** [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_subcontractors_phone`] — **and D-049 ruling 8 is not extended here either.** A firm sharing a number with its owner is Karim's own example from the client ruling, which makes it the population where the answer is most likely to be *warn* and **least defensible to assume**. `Q70`'s second half | **`Q70`** |
| 10 | Archiving replaces deletion; there is no delete path and this story adds none [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `Archive`] | CLAUDE.md · KAFF-123's precedent |
| 11 | `SubcontractorManage`, `CompanyWide`, **no assignment** — a firm belongs to no project. Technical Office settled by §2; **the Owner keeps it too** [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.SubcontractorManage`] | §2 · D-044 ruling 4 · **D-129 §1** |
| 12 | Create, edit, retention change and archive are state changes and **each writes an audit record**, before and after. **The retention rate is the one somebody will later ask who changed**, because it decides how much of a firm's money Kaff keeps | **CLAUDE.md** |
| 13 | Every string is an i18n key. **مقاول باطن is the term in the UI and `Subcontractor` is the identifier** — never *vendor*, *contractor* or *supplier*, and never interchangeable with `Supplier`, which is a different §2 row with a different owner | CLAUDE.md · **§14** |
| 14 | ⛔ **Bidding, RFQ and quote comparison are out of scope by name** and must not appear on this screen in any form | **§1** · CLAUDE.md |

## Permissions, money, audit, i18n
- **Permissions:** `SubcontractorManage`, `CompanyWide`, no assignment. Owner and Technical Office
  today. **Finance is deliberately absent** — §2 gives Finance the disbursement, not the record, and
  the D-055 §1 precedent is explicit that a grant written to reach one field hands over the whole
  record.
- **Money:** ⛔ **This story writes no `Posting`, opens no account and stores no balance.** It holds two
  **rates** — a retention `Percentage` and a withholding category — and a rate is not an amount. What
  Kaff owes a subcontractor is derived by summing postings on his `SubcontractorPayable` sub-ledger in
  slice 3, and **no outstanding, no total and no balance column is added here.** The withholding is a
  **liability** (§6.7) and never nets against the client-side asset.
- **Audit:** rule 12, before and after on every changed field, retention and tax details included.
- **i18n:** `subcontractor.list_title`, `subcontractor.create_title`, `subcontractor.edit_title`,
  `subcontractor.field.code`, `subcontractor.field.name`, `subcontractor.field.phone`,
  `subcontractor.field.trade`, `subcontractor.field.retention_rate`,
  `subcontractor.field.withholding_category`, `subcontractor.field.tax_registration_number`,
  `subcontractor.archive_action`, plus the existing `errors.master.*` keys.

## Acceptance criteria

**AC-211-A — a subcontractor is created with a trade and the default retention**
Given the Technical Office on `S-029`
When a code, name, phone and trade باب are submitted with no retention given
Then the firm exists carrying those values and a retention of **5%**, and it is active

**AC-211-B — retention is zeroed for one firm and no other** *(fails if the rule is broken)*
Given two subcontractors, both at the 5% default
When one is set to 0%
Then that one is 0% and the other is still 5%, and no global or default value anywhere has changed

**AC-211-C — the retention rate is a percentage and survives its round trip** *(fails if the rule is broken)*
Given a retention entered as `5`, meaning five percent
When it is stored and read back
Then it is the fraction `0.05` — not `5` — and `2.5%` survives exactly, at the storage and display precision D-044 ruling 6 sets

**AC-211-D — no subcontractor can sign in** *(fails if the rule is broken)*
Given a subcontractor record
When the endpoints this story maps are enumerated as an allow-list
Then none of them creates a `User`, sets a credential or grants a role
And a sign-in attempt against a subcontractor answers the same generic `401` as an unknown username — D-065's indistinguishable set, unchanged by this story

**AC-211-E — this story writes no posting and stores no balance** *(fails if the rule is broken)*
Given a subcontractor record
When its stored properties are enumerated as an allow-list, and the treasury is inspected
Then no balance, outstanding, total or amount-typed member appears among them, and no `Posting` and no account was created

**AC-211-F — withholding here is a liability and never nets against the client side** *(fails if the rule is broken)*
Given a subcontractor carrying a withholding category
When the record and every screen that shows it are inspected
Then nothing presents it as recoverable, as an asset, or as a figure netted with a client's tax withheld at source — §6.7 runs in two directions and this record is only ever on the liability side

**AC-211-G — HELD on `Q29`: where the withholding rate lives**
Given a subcontractor working two jobs at two different supplies
When the rate is set
Then — **held.** `Q29` decides whether it is a property of the firm or of the job. **The field stays where it is until it is ruled and is not moved by inference from D-049 ruling 9**; a rate moved to the job and a rate left on the firm are different databases, and the second is the reversible one

**AC-211-H — HELD on `Q73`: whether the master carries a rate card**
Given the create and edit screens
When they render
Then — **held.** `Q73` decides whether agreed rates live on the firm or only on each job's sub-BOQ. **No rate-card field is added until it is ruled**, and the story's own title is not read as the answer

**AC-211-I — HELD on `Q70`: what a repeated phone does**
Given a subcontractor already registered with a number
When a second firm is submitted with the same number — the firm and its owner, which is Karim's own example
Then — **held.** Today it is refused by the unique index and that behaviour is not asserted as correct

**AC-211-J — a role without `SubcontractorManage` reaches nothing** *(fails if the rule is broken)*
Given a signed-in user of each role that does not hold it — **including Finance**, which disburses but does not own the record
When each calls the create, edit, retention and archive endpoints directly, with no browser involved
Then every call is refused `403`, and no record is created or changed by any of them

**AC-211-K — every change is audited before and after** *(fails if the rule is broken)*
Given a subcontractor whose retention rate and tax registration number are both edited
When the audit trail is read
Then a record names the actor, the time, and both fields with their old and new values

**AC-211-L — nothing on this screen is a bid** *(fails if the rule is broken)*
Given `S-028` and `S-029`
When every field, control and route they carry is enumerated as an allow-list
Then none of them is a bid, a quotation, an RFQ or a comparison of two firms' prices — §1 puts all four out of scope by name

**AC-211-M — Arabic, RTL, at mobile width**
Given `S-028` and `S-029` at 390px in Arabic
When they render
Then direction is RTL, the term reads **مقاول باطن**, percentages and phone numbers are bidi-isolated, no string is a literal in either language, and the page body does not scroll horizontally

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-211-A` … `AC-211-M`. **Three are written and held**: `AC-211-G`, `AC-211-H`, `AC-211-I` |
| Stable `AC-211-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–14; rules 5, 6 and 9 cite `Q29` / `Q73` / `Q70` and are marked as questions rather than sourced |
| No uncited rule | ✅ — ⛔ **and D-049 ruling 9 is cited only as the ruling that is NOT being extended** |
| Permissions named explicitly | ✅ — `SubcontractorManage`, CompanyWide, no assignment; **Finance's absence stated as deliberate**; the Owner grant confirmed by **D-129 §1** |
| Money behaviour named explicitly | ✅ — rules 4, 7; `AC-211-E`, `AC-211-F`. **Holds rates, stores no amount, writes no posting, nets nothing** |
| Arabic UI strings as i18n keys | ✅ — eleven keys, rule 13, and §14's term pinned |
| The audit record it writes is stated | ✅ — rule 12, `AC-211-K` |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated [Verified: 2026-09-09 — `qa/` holds `slice-1`, `questions.md`, `README.md`, `risk-register.md` and `strategy.md`]. Eight criteria are marked *(fails if the rule is broken)*. **QA's to write** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ⛔ **Not met — `Q29`**, which `stories/backlog.md` named before this story existed, **`Q73`**, which decides what the title means, plus **`Q70`**. **`Q12` is answered (D-129 §1)** |

**Flip the trailer to `READY` when `Q29`, `Q73` and `Q70` are ruled and QA's cases land.**

## Not in this story
- **The supplier.** `KAFF-212` — a different §2 row, a different owner (Finance), and **not a synonym**.
- **Paying anybody.** Slice 3 opens the `SubcontractorPayable` sub-ledger; **slice 3's `KAFF-318` is
  the withholding Kaff carries as a liability**, and it is where `Q29`'s answer is spent.
- **The subcontractor's own BOQ and his extracts.** §2 gives the Technical Office *"rates and BOQ"*;
  the BOQ itself is slice 4 and the sub extract is slice 5.
- **Releasing the 5% at warranty end.** §5.1, slice 8.
- **Snags, debit notes and absorbing a sub's fault.** §11, slice 8. ⛔ **And nothing may debit Kaff's
  own hold** — a debit note against a subcontractor is not a movement on the `Hold` ledger.
- **Bidding, RFQ, quote comparison.** §1, out of scope by name, `AC-211-L`.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q29`** | Already open, **and named in `stories/backlog.md` as a slice-2 blocker since the slice was estimated.** Is a subcontractor's or supplier's withholding rate a property of the job or of the firm? §6.7's amendment flags it 🟡 itself | **Karim** |
| **`Q73`** | **New, raised here and blocking.** Does a subcontractor's record carry agreed rates, or do rates live only on each job's sub-BOQ? *"Rates and BOQ"* in §2 may be naming what the Technical Office **owns** rather than a field on this screen | **Karim** |
| **`Q70`** | Raised by `KAFF-209`, second half. Is a repeated subcontractor phone a refusal or a warning? **This is the population where Karim's own client example — a firm and its owner sharing a number — applies most directly, which is why it is asked rather than assumed** | **Karim** |
| **`Q12`** | ✅ **ANSWERED — D-129 §1.** The Owner keeps `SubcontractorManage` | **Closed** |
| **`Q26`** | Already open, slice 8: *"you keep 5% from every subcontractor … is that right for all of them, or do some have nothing held?"* **It does not block this story** — the rate is zeroable per firm today, which satisfies either answer | **Karim** |
