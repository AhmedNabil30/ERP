# KAFF-211 · Subcontractor master — profile only, rates live on the sub-BOQ

<!-- kaff id=KAFF-211 slice=2 points=5 state=VERIFIED verdict=CONDITIONAL at=54ff044 on=2026-09-13 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 5 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-09 by the BA. **⚠️ Amended 2026-09-12 — `Q29`, `Q73` and `Q70` are all answered (D-139 §§4–5, D-141/D-144 §1). Retitled: this master carries no rate card and no rate card was ever built — "with rates" is no longer true and the file's own former title was never a ruling. The file name is left as-is; only the heading changes.**
**Spec:** **§2** (*"rates and BOQ; Finance only disburses"*), **§5.1** (5% retention, zeroable 🟡), **§6.7**, §9 (*"record only, no login"*) · **Decisions:** D-044 ruling 4, D-049 ruling 9 (the rate moved to the contract, **for the client only**), **D-129 §1, D-139 §§4–5, D-141**
**Register:** `stories/questions-for-karim.md` → **`Q29`** (✅ answered — D-139 §5: withholding is per contract/job, as for the client; the tax registration number is Finance's alone), **`Q73`** (✅ answered — D-139 §4: rates live on the sub-BOQ, slice 4/5, not on this record), **`Q12`** (✅ answered, D-129 §1), **`Q70`** (✅ answered — D-139 §1/D-141: warn-and-acknowledge for a subcontractor, same as day labour). **Mechanism question closed 2026-09-12 — D-147**: `SubcontractorTaxRegistrationEdit`, its own endpoints, `AC-211-K` restated, `AC-211-N` added. UX's `S`-number for Finance's screen is HELD, `AC-211-O`.
**Screens:** `ux/screen-inventory.md` → **`S-028`** (list), **`S-029`** (create / edit)
**Owner:** Backend, then Frontend
**Depends on:** KAFF-204 — a subcontractor's trade names a باب

## Story
As the Technical Office, I keep a record for every مقاول باطن Kaff works with, because §2 puts their
rates and their BOQ in my department and puts the disbursement in Finance's — and neither half works
if the firm itself is typed fresh onto every job. **This record is a profile.** Agreed rates live on
each job's sub-BOQ (D-139 §4), not here.

## What already exists, and what this story adds

| Already built | Evidence |
|---|---|
| The entity, with code, name, phone, trade باب and active flag | [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `Subcontractor`, `TradeBabId`] |
| §5.1's **5% retention, held per subcontractor so it can be zeroed without touching anything global** | [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `DefaultRetentionRate`, `SetRetentionRate`] |
| A withholding category and a tax registration number on the party record — **the category moves off per D-139 §5; the tax registration number stays** | [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `WithholdingCategory`, `SetTaxDetails`] |
| Archiving, and its refusal when already archived | [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `Archive`] |
| Unique indexes on the code and on the normalised phone | [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_subcontractors_code`, `ux_subcontractors_phone`] |
| The permission row — `CompanyWide`, Owner and Technical Office | [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.SubcontractorManage`] |
| The sub-ledger account type this master will one day be paid through | [Verified: 2026-09-09 @ `src/Domain/Treasury/AccountType.cs` -> `SubcontractorPayable`] |

**What does not exist is any endpoint or screen** [Verified: 2026-09-09 — `src/Api/Features/` holds
`Assignments`, `Audit`, `Auth`, `Clients`, `Health`, `Setup` and `Users`].

## ✅ Two blockers, both closed 2026-09-12

**1. `Q29` — the withholding rate: per job or per firm — ANSWERED, D-139 §5.** *"Withholding for
subcontractors and suppliers is set per contract/job, as for clients. It is not a firm-level field. The
tax registration number is entered and managed by Finance only."* **The rate moves off this entity**,
the same way D-049 ruling 9 moved the client's — the reasoning transfers exactly, and Nabil has now said
so explicitly rather than by inference. `WithholdingCategory` is removed from `Subcontractor`
[Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `WithholdingCategory`] and rebuilt
where `KAFF-318` needs it — Backend's, not this story's. **The tax registration number stays on this
record** (rule 8), but D-139 §5 adds a permission consequence this story must state rather than build:
whoever holds `SubcontractorManage` cannot be assumed to also manage the tax registration number, if
Finance is meant to hold that field alone the way `ProjectFinancialsEdit` split off from
`ProjectManage` (D-055 §1's precedent). **This mechanism is the Architect's, named in Questions below,
not invented here.**

**2. `Q73` — what *"with rates"* meant — ANSWERED, D-139 §4.** *"Subcontractor rates live on each
project's sub-BOQ, not on the subcontractor's profile."* `KAFF-211`'s *"with rates"* moves to the
sub-BOQ, which is slice 4/5. **No rate-card field is added to this entity, ever, by this story.** The
retention percentage (§5.1, rule 3 below) is unaffected — it is a different figure with a different
source, held per firm because it must be zeroable per firm.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | One record per subcontracting firm, owned by the Technical Office. **Finance only disburses** and does not create or edit the record | **§2** |
| 2 | **A subcontractor never logs in.** §9 is *"record only, no login"*; the entity records that a `User` row with this role cannot hold a password [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `Subcontractor`], and `Role.Subcontractor` holds no row in the permission catalogue at all [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Role.Subcontractor`]. **This story creates no `User` and no credential** | **§9** · D-035 |
| 3 | **Retention is 5% by default and is zeroable per subcontractor**, held on the firm rather than globally, so zeroing one changes nothing for the others [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `DefaultRetentionRate`]. 🟡 §5.1 and §16 assumption 19 both flag it, and **`Q26` is the open confirmation** | **§5.1** · `Q26` |
| 4 | ⛔ **Retention is a `Percentage`, not a `Money`, and this story neither holds nor releases anything.** §5.1 releases sub-retention when the warranty ends — that is slice 8. **Nothing here debits, credits or nets any ledger**, and the Kaff-side hold of §6.4 is a different thing entirely and must not be confused with it | **§5.1** · CLAUDE.md — *"The five ledgers never net against each other"* |
| 5 | ✅ **The withholding rate moves off this record — D-139 §5.** *"Withholding for subcontractors and suppliers is set per contract/job, as for clients. It is not a firm-level field."* `WithholdingCategory` is removed from `Subcontractor` [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `WithholdingCategory`] and rebuilt on the contract/job — `KAFF-318`'s and slice 4's concern, not this story's | **D-139 §5** |
| 6 | ✅ **The master carries no rate card — D-139 §4.** Subcontractor rates live on each project's sub-BOQ. **No rate-card field is added to this entity, ever, by this story** | **D-139 §4** |
| 7 | **Withholding is a liability Kaff carries, never an asset.** §6.7: when Kaff pays subcontractors, **Kaff withholds and carries a liability to remit** — the opposite direction from a corporate client's collection, which is a recoverable **asset**. **The two must never net against each other.** This record does not hold the rate (rule 5); it must still never present the concept as a recoverable figure on any screen that shows it | **§6.7** — MUST · CLAUDE.md |
| 8 | **The tax registration number identifies the legal entity and does not vary by job**, so it stays on this record even though the rate does not — D-049's reasoning about the field it kept on the client, applied here. ✅ **Who may set it, and how, is now ruled — D-139 §5 and D-147.** The number is split off `SubcontractorManage` into its own permission, `SubcontractorTaxRegistrationEdit` (`CompanyWide`, `[owner, finance]`), reached only through its own endpoint, `PUT /api/subcontractors/{id}/tax-registration`. **The Technical Office holds `SubcontractorManage` and does not hold `SubcontractorTaxRegistrationEdit`, so it can read the number on its own screen but has no route, at any layer, that lets it write one.** Finance has no `SubcontractorManage`, so it reaches the firm only through `GET /api/subcontractors/tax-registrations`, a projection of `Id, Code, Name, TaxRegistrationNumber, IsActive` and nothing else (D-055 §2's *"the projection is the control"*) | **§6.7 amendment · D-139 §5 · D-147** |
| 9 | ✅ **A repeated subcontractor phone warns and is acknowledged, never refuses — D-139 §1, D-141.** `ux_subcontractors_phone` is dropped for a non-unique `ix_subcontractors_phone`, the client's own shape. Karim's own example for softening the client rule — *"a corporate client and its CEO might share a number"* — applies here just as directly, and Nabil ruled the same way | **D-139 §1 · D-141** |
| 10 | Archiving replaces deletion; there is no delete path and this story adds none [Verified: 2026-09-09 @ `src/Domain/MasterData/Subcontractor.cs` -> `Archive`] | CLAUDE.md · KAFF-123's precedent |
| 11 | `SubcontractorManage`, `CompanyWide`, **no assignment** — a firm belongs to no project. Technical Office settled by §2; **the Owner keeps it too** [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.SubcontractorManage`] | §2 · D-044 ruling 4 · **D-129 §1** |
| 12 | Create, edit, retention change and archive are state changes and **each writes an audit record**, before and after. **The retention rate is the one somebody will later ask who changed**, because it decides how much of a firm's money Kaff keeps | **CLAUDE.md** |
| 13 | Every string is an i18n key. **مقاول باطن is the term in the UI and `Subcontractor` is the identifier** — never *vendor*, *contractor* or *supplier*, and never interchangeable with `Supplier`, which is a different §2 row with a different owner | CLAUDE.md · **§14** |
| 14 | ⛔ **Bidding, RFQ and quote comparison are out of scope by name** and must not appear on this screen in any form | **§1** · CLAUDE.md |

## Permissions, money, audit, i18n
- **Permissions:** `SubcontractorManage`, `CompanyWide`, no assignment. Owner and Technical Office
  today. **Finance is deliberately absent from `SubcontractorManage`** — §2 gives Finance the
  disbursement, not the record, and the D-055 §1 precedent is explicit that a grant written to reach
  one field hands over the whole record. ✅ **D-147 closes the mechanism question rule 8 raised**:
  the tax registration number is reached instead through its own row, `SubcontractorTaxRegistrationEdit`
  (`CompanyWide`, `[owner, finance]`, `TouchesMoney: false`), and its own endpoints —
  `PUT /api/subcontractors/{id:guid}/tax-registration` (sets or clears the number) and
  `GET /api/subcontractors/tax-registrations` (Finance's own list, projecting `Id, Code, Name,
  TaxRegistrationNumber, IsActive` and nothing else, so no retention rate, trade or phone reaches
  Finance through this route). `SubcontractorManage`'s create and edit requests carry no
  `TaxRegistrationNumber` member at all — a member that does not exist cannot be sent — though the
  Technical Office's read shape may still show the number **read-only**, since D-139 §5 restricts
  entering and managing it, not reading it.
- **Money:** ⛔ **This story writes no `Posting`, opens no account and stores no balance.** It holds one
  rate — retention, a `Percentage` — and one identifying field, the tax registration number; **the
  withholding rate is not held here** (rule 5, D-139 §5). What Kaff owes a subcontractor is derived by
  summing postings on his `SubcontractorPayable` sub-ledger in slice 3, and **no outstanding, no total
  and no balance column is added here.**
- **Audit:** rule 12, before and after on every changed field, retention and tax registration number
  included.
- **i18n:** `subcontractor.list_title`, `subcontractor.create_title`, `subcontractor.edit_title`,
  `subcontractor.field.code`, `subcontractor.field.name`, `subcontractor.field.phone`,
  `subcontractor.field.trade`, `subcontractor.field.retention_rate`,
  `subcontractor.field.tax_registration_number`, `subcontractor.archive_action`, plus the existing
  `errors.master.*` keys. **`subcontractor.field.withholding_category` is dropped** — the field it
  named no longer lives here.

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
Then no balance, outstanding, total, amount-typed or withholding-rate member appears among them, and no `Posting` and no account was created

**AC-211-F — nothing here presents withholding as recoverable or nets it against the client side** *(fails if the rule is broken)*
Given a subcontractor record and every screen that shows it
When they are inspected
Then none of them carries a withholding rate — it lives on the contract/job (D-139 §5) — and none presents the concept as recoverable, as an asset, or as a figure netted with a client's tax withheld at source

**AC-211-G — the withholding rate is not on this record** *(fails if the rule is broken, restated 2026-09-12 — `Q29` answered: D-139 §5)*
Given a subcontractor working two jobs at two different rates
When the entity's stored properties are enumerated as an allow-list
Then no withholding rate or category appears among them — it is set per contract/job, on `KAFF-318`'s ground, not here

**AC-211-H — the master carries no rate card** *(fails if the rule is broken, restated 2026-09-12 — `Q73` answered: D-139 §4)*
Given the create and edit screens
When they render
Then neither carries a price, a rate or a rate-card field of any kind — agreed rates live only on each job's sub-BOQ

**AC-211-I — a repeated subcontractor phone warns and is acknowledged, never refused** *(fails if the rule is broken, restated 2026-09-12 — `Q70` answered: D-139 §1, D-141)*
Given a subcontractor already registered with a number — the firm and its owner sharing a number, Karim's own example
When a second firm is submitted with the same number and no acknowledgement
Then the save is refused `409 errors.master.duplicate_phone_not_acknowledged`, naming the existing firm
And with `AcknowledgedDuplicatePhone: true` the save succeeds and one `DuplicatePhoneAcknowledged` audit record is written

**AC-211-J — a role without `SubcontractorManage` reaches nothing** *(fails if the rule is broken)*
Given a signed-in user of each role that does not hold it — **including Finance**, which disburses but does not own the record
When each calls the create, edit, retention and archive endpoints directly, with no browser involved
Then every call is refused `403`, and no record is created or changed by any of them

**AC-211-K — every change is audited before and after, and the tax registration number is a separate audit record from the rest of the profile** *(fails if the rule is broken, restated 2026-09-12 — D-147: two roles, two endpoints)*
Given a subcontractor whose retention rate is edited by the Technical Office through `SubcontractorManage`, and whose tax registration number is separately edited by Finance through `PUT /api/subcontractors/{id}/tax-registration`
When the audit trail is read
Then **two** records exist — one naming the Technical Office actor, the time, and the retention field's old and new values; the other naming the Finance actor, the time, and the tax registration number's old and new values — because D-147 makes these two requests on two endpoints, never one combined save, and a single audit record spanning both would misstate who did what

**AC-211-L — nothing on this screen is a bid** *(fails if the rule is broken)*
Given `S-028` and `S-029`
When every field, control and route they carry is enumerated as an allow-list
Then none of them is a bid, a quotation, an RFQ or a comparison of two firms' prices — §1 puts all four out of scope by name

**AC-211-M — Arabic, RTL, at mobile width**
Given `S-028` and `S-029` at 390px in Arabic
When they render
Then direction is RTL, the term reads **مقاول باطن**, percentages and phone numbers are bidi-isolated, no string is a literal in either language, and the page body does not scroll horizontally

**AC-211-N — the Technical Office cannot set the tax registration number by any route** *(fails if the rule is broken, new 2026-09-12 — D-147)*
Given a Technical Office user holding `SubcontractorManage` but not `SubcontractorTaxRegistrationEdit`
When every endpoint this story and `KAFF-211`'s mechanism map is enumerated as an allow-list, including create, edit and the tax-registration endpoint itself
Then `SubcontractorManage`'s create and edit request shapes carry no `TaxRegistrationNumber` member to send, and a direct call to `PUT /api/subcontractors/{id}/tax-registration` by this user is refused `403`
And this holds however the number is read on the Technical Office's own screen — reading it read-only is not the same permission as writing it

**AC-211-O — HELD: Finance's tax-registration screen has no `S`-number** *(HELD 2026-09-12 — D-147)*
Given `GET /api/subcontractors/tax-registrations` and `PUT /api/subcontractors/{id}/tax-registration`, both ruled by D-147
When Frontend is asked to build the screen Finance uses to reach them
Then — **held.** UX has not named an `S`-number for this screen, unlike `S-028`/`S-029` for the Technical Office's own. **No frontend criterion is written for it until UX does** — inventing a screen id here would be inventing UX's answer, not the BA's to give

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-211-A` … `AC-211-M` |
| Stable `AC-211-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–14; rules 5, 6 and 9 re-cited to `D-139 §§4–5` / `D-141` |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `SubcontractorManage`, CompanyWide, no assignment; **Finance's absence stated as deliberate**; the Owner grant confirmed by **D-129 §1**. ⚠️ **New mechanism question, routed to the Architect below**: whether the tax registration field needs its own Finance-only permission |
| Money behaviour named explicitly | ✅ — rules 4, 7; `AC-211-E`, `AC-211-F`. **Holds one rate (retention), stores no amount, writes no posting, nets nothing. No withholding rate is held here at all** |
| Arabic UI strings as i18n keys | ✅ — ten keys, rule 13, and §14's term pinned |
| The audit record it writes is stated | ✅ — rule 12, `AC-211-K` |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated [Verified: 2026-09-09 — `qa/` holds `slice-1`, `questions.md`, `README.md`, `risk-register.md` and `strategy.md`]. Eight criteria are marked *(fails if the rule is broken)*. **QA's to write** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ — `Q29` (D-139 §5), `Q73` (D-139 §4) and `Q70` (D-139 §1 / D-141) are all answered. `Q12` is answered (D-129 §1) |

**Flip the trailer to `READY` when QA's cases land.** Backend still owes removing `WithholdingCategory`
from `Subcontractor` before `AC-211-G` is true of the running system.

## Not in this story
- **The supplier.** `KAFF-212` — a different §2 row, a different owner (Finance), and **not a synonym**.
- **Paying anybody, and the withholding rate itself.** Slice 3 opens the `SubcontractorPayable`
  sub-ledger; **slice 3's `KAFF-318` and the contract/job mechanism `Q29` now points to** are where the
  rate is actually held and spent.
- **The subcontractor's own BOQ, sub-BOQ and his extracts, including his rates.** §2 gives the
  Technical Office *"rates and BOQ"*; the BOQ itself is slice 4 and the sub extract is slice 5 — **this
  is where `Q73`'s answer lands, not here.**
- **Releasing the 5% at warranty end.** §5.1, slice 8.
- **Snags, debit notes and absorbing a sub's fault.** §11, slice 8. ⛔ **And nothing may debit Kaff's
  own hold** — a debit note against a subcontractor is not a movement on the `Hold` ledger.
- **Bidding, RFQ, quote comparison.** §1, out of scope by name, `AC-211-L`.
- **Splitting a Finance-only permission for the tax registration number.** ✅ **Closed, D-147** —
  `SubcontractorTaxRegistrationEdit`, its two endpoints, `AC-211-K` (restated) and `AC-211-N` (new).
- **Naming an `S`-number for Finance's tax-registration screen.** UX's to give — `AC-211-O`, HELD.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q29`** | ✅ **ANSWERED — D-139 §5.** The withholding rate is per contract/job, as for the client; the tax registration number is Finance's alone | **Closed** |
| **`Q73`** | ✅ **ANSWERED — D-139 §4.** Rates live on each job's sub-BOQ, not on this record | **Closed** |
| **`Q70`** | ✅ **ANSWERED — D-139 §1, D-141.** Warn-and-acknowledge, same as the client and as day labour | **Closed** |
| **`Q12`** | ✅ **ANSWERED — D-129 §1.** The Owner keeps `SubcontractorManage` | **Closed** |
| **`Q26`** | Already open, slice 8: *"you keep 5% from every subcontractor … is that right for all of them, or do some have nothing held?"* **It does not block this story** — the rate is zeroable per firm today, which satisfies either answer | **Karim** |
| new | ✅ **ANSWERED — D-147.** The tax registration number gets its own permission, `SubcontractorTaxRegistrationEdit` (`CompanyWide`, `[owner, finance]`), and its own endpoints — `PUT /api/subcontractors/{id:guid}/tax-registration` and `GET /api/subcontractors/tax-registrations`. `AC-211-K` restated, `AC-211-N` added | **Closed** |
| new | **UX owes an `S`-number for Finance's tax-registration screen.** Not named yet, unlike `S-028`/`S-029`. `AC-211-O` is HELD until it is | **UX** |
