# KAFF-212 · Supplier master — one account serving many projects

<!-- kaff id=KAFF-212 slice=2 points=3 state=NOT-BUILT verdict=none at=- on=2026-09-09 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 3 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-09 by the BA. **⚠️ Amended 2026-09-09 — `Q12` clears (D-129 §1). Two Definition-of-Ready boxes remain unticked.**
**Spec:** **§2** (*"one account, serves many projects"*), **§6.3**, **§6.7**, §1 (out of scope) · **Decisions:** D-044 ruling 4, D-049 ruling 9 (**the client only**), **D-129 §1**
**Register:** `stories/questions-for-karim.md` → **`Q29`** (blocking), **`Q13`** (open, named in the backlog as a slice-2 blocker), **`Q12`** (✅ answered, D-129 §1), **`Q70`** (a repeated supplier phone — the question `KAFF-209` raised, cited here for this population. ✅ **The collision is resolved:** the trade-markup question is now **`Q75`**)
**Screens:** `ux/screen-inventory.md` → **`S-030`** (list and create / edit, one screen)
**Owner:** Backend, then Frontend
**Depends on:** nothing. `Supplier` shipped in slice 0.

## Story
As Finance, I keep one record and one account per supplier however many of Kaff's sites he delivers to,
because §2 says *"one account, serves many projects"* — and a supplier duplicated per project is a
supplier whose total exposure nobody in Kaff can see.

## What already exists, and what this story adds

| Already built | Evidence |
|---|---|
| The entity, with code, name, phone, address and active flag | [Verified: 2026-09-09 @ `src/Domain/MasterData/Supplier.cs` -> `Supplier`, `SetAddress`] |
| A withholding category and a tax registration number **on the party record** | [Verified: 2026-09-09 @ `src/Domain/MasterData/Supplier.cs` -> `WithholdingCategory`, `SetTaxDetails`] |
| Archiving, and its refusal when already archived | [Verified: 2026-09-09 @ `src/Domain/MasterData/Supplier.cs` -> `Archive`] |
| Unique indexes on the code and on the normalised phone | [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_suppliers_code`, `ux_suppliers_phone`] |
| The permission row — `CompanyWide`, Owner and Finance | [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.SupplierManage`] |
| ⛔ **The structural half of *"one account"* is already in the ground:** the supplier account type is **company-scoped rather than project-scoped**, which is what makes one account per supplier possible at all | [Verified: 2026-09-09 @ `src/Domain/Treasury/AccountType.cs` -> `SupplierPayable`] |

**What does not exist is any endpoint or screen** [Verified: 2026-09-09 — `src/Api/Features/` holds
`Assignments`, `Audit`, `Auth`, `Clients`, `Health`, `Setup` and `Users`].

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | One record per supplier, owned by **Finance** — not by the Technical Office, which owns the subcontractor. **The two are different §2 rows with different owners and are not interchangeable** | **§2** |
| 2 | ⛔ **One account, serving many projects.** A supplier is **not** duplicated per project and his account is **not** project-scoped [Verified: 2026-09-09 @ `src/Domain/Treasury/AccountType.cs` -> `SupplierPayable`]. **Project attribution happens on the cost side of the posting, not on the supplier's sub-ledger** — which is a slice-3 mechanism this story must not pre-empt by adding a project to the master record | **§2** · §6.3 |
| 3 | ⛔ **This story stores no balance and opens no account.** What Kaff owes a supplier is **derived by summing postings** on his one sub-ledger, in slice 3. **An `outstanding`, a `total_purchases` or a `balance` column here is the bug**, and it is the most tempting single field on this screen | **CLAUDE.md** — *"Never store a balance"* |
| 4 | ⛔ **Where the withholding rate lives is `Q29`**, exactly as for the subcontractor. It is on the party record today [Verified: 2026-09-09 @ `src/Domain/MasterData/Supplier.cs` -> `WithholdingCategory`]; D-049 ruling 9 moved the **client's** onto the contract and named the client only. §6.7's amendment flags this 🟡 itself | **`Q29`** · §6.7 amendment |
| 5 | **Withholding on a supplier payment is a liability Kaff carries to remit, never a recoverable asset.** §6.7 runs in two directions and this record is only ever on the liability side; **the two directions never net** | **§6.7** — MUST · CLAUDE.md |
| 6 | **The tax registration number identifies the legal entity and does not vary by job**, so it stays on the record whatever `Q29` decides about the rate — D-049's own reasoning about the field it kept on the client | **§6.7 amendment** |
| 7 | ⛔ **The supplier's phone is unique today — a refusal** [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_suppliers_phone`] — **and D-049 ruling 8 is not extended here.** `Q70`'s third half | **`Q70`** |
| 8 | Archiving replaces deletion; there is no delete path and this story adds none [Verified: 2026-09-09 @ `src/Domain/MasterData/Supplier.cs` -> `Archive`]. ⚠️ **A supplier with postings against him must never be removable**, and in slice 3 that is enforced by the postings being append-only rather than by anything here | CLAUDE.md · KAFF-123's precedent |
| 9 | `SupplierManage`, `CompanyWide`, **no assignment** — a supplier belongs to no project, which is the same fact as rule 2 seen from the permission side. Finance settled by §2; **the Owner keeps it too** [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.SupplierManage`] | §2 · D-044 ruling 4 · **D-129 §1** |
| 10 | Create, edit and archive are state changes and **each writes an audit record**, before and after — the tax details included | **CLAUDE.md** |
| 11 | Every string is an i18n key; Arabic RTL at 390px, names and numbers bidi-isolated | CLAUDE.md · `ux/rtl-and-i18n.md` |
| 12 | ⛔ **Bidding, RFQ and quote comparison are out of scope by name**, and the entity says so itself [Verified: 2026-09-09 @ `src/Domain/MasterData/Supplier.cs` -> `Supplier`]. **This is the screen where somebody will want to add "compare two quotes", and the answer is no** | **§1** · CLAUDE.md |
| 13 | ⚠️ **A bank is not a supplier.** `Q13` asks whether Kaff's banks are records in their own right or only accounts in the ledger, and **it is named in `stories/backlog.md` as one of slice 2's blockers.** It does not block this story's behaviour — **it blocks anyone deciding to model banks as suppliers to save a table**, which is the shortcut this rule exists to forbid | **`Q13`** · §6.3 |

## Permissions, money, audit, i18n
- **Permissions:** `SupplierManage`, `CompanyWide`, no assignment. Finance and the Owner today. Every
  other role refused `403` server-side — **including the Technical Office**, which owns the
  subcontractor and not this.
- **Money:** ⛔ **This story writes no `Posting`, opens no account and stores no balance.** It holds a
  withholding **category** — a rate, not an amount. What Kaff owes is derived by summing postings on
  one company-scoped sub-ledger in slice 3, and **the one account per supplier is the whole point of
  the §2 row**. The withholding is a **liability** and never nets against the client-side asset, nor
  against any of the five ledgers.
- **Audit:** rule 10, before and after on every changed field.
- **i18n:** `supplier.list_title`, `supplier.create_title`, `supplier.edit_title`,
  `supplier.field.code`, `supplier.field.name`, `supplier.field.phone`, `supplier.field.address`,
  `supplier.field.withholding_category`, `supplier.field.tax_registration_number`,
  `supplier.archive_action`, plus the existing `errors.master.*` keys.

## Acceptance criteria

**AC-212-A — a supplier is created once and is not per project**
Given Finance on `S-030`
When a code, name, phone and address are submitted
Then the supplier exists carrying those values, is active, and carries **no project** — the record has no project field to carry one

**AC-212-B — one supplier, one account, many projects** *(fails if the rule is broken)*
Given a supplier delivering to three projects
When his record and the account type that will serve him are inspected
Then there is exactly one supplier row and his account type is company-scoped, not project-scoped
And nothing in this story creates a second record, a per-project record, or an account at all

**AC-212-C — no balance is stored anywhere on this record** *(fails if the rule is broken)*
Given a supplier
When his stored properties are enumerated as an allow-list
Then no balance, outstanding, total, purchases-to-date or any other amount-typed member appears among them, under that name or any other
And the allow-list is written out by name, so adding one is a deliberate edit to this test rather than a silent widening

**AC-212-D — two suppliers cannot share a code** *(fails if the rule is broken)*
Given a supplier with code `S-100`
When a second is submitted with `S-100`, and again with `s-100`
Then both are refused, and the refusal survives two requests arriving at the same instant — the guarantee is the unique index, not a read-then-write

**AC-212-E — withholding here is a liability and never nets against the client side** *(fails if the rule is broken)*
Given a supplier carrying a withholding category
When the record and every screen showing it are inspected
Then nothing presents it as recoverable, as an asset, or as a figure netted with a corporate client's tax withheld at source

**AC-212-F — HELD on `Q29`: where the withholding rate lives**
Given a supplier delivering materials on one job and a service on another
When the rate is set
Then — **held.** `Q29` decides whether it belongs to the firm or to the job. **The field stays where it is until it is ruled and is not moved by inference from D-049 ruling 9**

**AC-212-G — HELD on `Q70`: what a repeated phone does**
Given a supplier already registered with a number
When a second is submitted with the same number
Then — **held.** Today it is refused by the unique index and that behaviour is not asserted as correct

**AC-212-H — a role without `SupplierManage` reaches nothing** *(fails if the rule is broken)*
Given a signed-in user of each role that does not hold it — **including the Technical Office**
When each calls the create, edit and archive endpoints directly, with no browser involved
Then every call is refused `403`, and no record is created or changed by any of them

**AC-212-I — a supplier is archived, not deleted** *(fails if the rule is broken)*
Given an active supplier
When he is archived
Then the row still exists with every field intact and is findable through the explicit filter
And no mapped route on the API deletes a supplier, under any verb — enumerated as an allow-list of the routes the host registered

**AC-212-J — every change is audited before and after** *(fails if the rule is broken)*
Given a supplier whose address and tax registration number are both edited in one request
When the audit trail is read
Then one record names the actor, the time, and both fields with their old and new values

**AC-212-K — nothing on this screen is a quote comparison** *(fails if the rule is broken)*
Given `S-030`
When every field, control and route it carries is enumerated as an allow-list
Then none of them is a bid, an RFQ, a quotation or a comparison of two suppliers' prices — §1 puts all four out of scope by name

**AC-212-L — Arabic, RTL, at mobile width**
Given `S-030` at 390px in Arabic
When it renders
Then direction is RTL, names, numbers and the tax registration number are bidi-isolated, no string is a literal in either language, and the page body does not scroll horizontally

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-212-A` … `AC-212-L`. **`AC-212-F` and `AC-212-G` are written and held** |
| Stable `AC-212-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–13; rules 4 and 7 cite `Q29` / `Q70` and are marked as questions rather than sourced |
| No uncited rule | ✅ — ⛔ **and D-049 ruling 9 is cited only as the ruling that is NOT being extended** |
| Permissions named explicitly | ✅ — `SupplierManage`, CompanyWide, no assignment; the Technical Office's exclusion stated; the Owner grant confirmed by **D-129 §1** |
| Money behaviour named explicitly | ✅ — rules 2, 3, 5; `AC-212-B`, `AC-212-C`, `AC-212-E`. **Stores no balance, opens no account, writes no posting, nets nothing** |
| Arabic UI strings as i18n keys | ✅ — ten keys, rule 11 |
| The audit record it writes is stated | ✅ — rule 10, `AC-212-J` |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated [Verified: 2026-09-09 — `qa/` holds `slice-1`, `questions.md`, `README.md`, `risk-register.md` and `strategy.md`]. Nine criteria are marked *(fails if the rule is broken)*. **QA's to write** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ⛔ **Not met — `Q29`**, named in `stories/backlog.md` as a slice-2 blocker, plus **`Q70`**. **`Q13` blocks no criterion here** and is named in rule 13 so that nobody models a bank as a supplier while it is open. **`Q12` is answered (D-129 §1)** |

**Flip the trailer to `READY` when `Q29` and `Q70` are ruled and QA's cases land.**

## Not in this story
- **The subcontractor.** `KAFF-211` — a different §2 row with a different owner. ⛔ **Not a synonym,
  and the two records must not be merged into one "vendor" table**, however similar their fields look.
- **Any account, posting or balance.** Slice 3. `SupplierPayable` exists as a type and this story
  opens none.
- **Paying a supplier, and the withholding Kaff remits.** Slice 3's `KAFF-318`, where `Q29`'s answer is
  spent.
- **Purchase orders, deliveries, material requests and stock.** Not in `stories/backlog.md`'s slice-2
  table and not invented here.
- **Bidding, RFQ, quote comparison.** §1, out of scope by name, `AC-212-K`.
- **Banks.** `Q13`, and `Q15` names the banks themselves. **Slice 3's `KAFF-316`/`317`, not this.**

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q29`** | Already open, **and named in `stories/backlog.md` as a slice-2 blocker since the slice was estimated.** Is a supplier's withholding rate a property of the job or of the firm? | **Karim** |
| **`Q13`** | Already open, **and the backlog's other slice-2 blocker**: are Kaff's banks records in their own right, or only accounts in the ledger? It blocks no criterion here; **it blocks the shortcut of modelling a bank as a supplier**, which this story forbids in rule 13 | **Karim** |
| **`Q70`** | Raised by `KAFF-209`, third half. Is a repeated supplier phone a refusal or a warning? | **Karim** |
| **`Q12`** | ✅ **ANSWERED — D-129 §1.** The Owner keeps `SupplierManage`, as D-044 ruling 4's own example list had already named suppliers explicitly | **Closed** |
