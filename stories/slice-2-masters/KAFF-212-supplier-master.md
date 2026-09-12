# KAFF-212 · Supplier master — one account serving many projects

<!-- kaff id=KAFF-212 slice=2 points=3 state=BUILT verdict=none at=35b0550 on=2026-09-12 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 3 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-09 by the BA. **⚠️ Amended 2026-09-12 — `Q29` and `Q70` are answered (D-139 §5, D-139 §1/D-141). `Q13` is answered too, but not for this story: banks are independent master records (D-139 §6), cut as a new slice-3 story, and NOT built inside this one. One Definition-of-Ready box remains unticked — QA's cases.**
**Spec:** **§2** (*"one account, serves many projects"*), **§6.3**, **§6.7**, §1 (out of scope) · **Decisions:** D-044 ruling 4, D-049 ruling 9 (**the client only**), **D-129 §1, D-139 §§1, 5, 6, D-141**
**Register:** `stories/questions-for-karim.md` → **`Q29`** (✅ answered — D-139 §5: per contract/job), **`Q13`** (✅ answered — D-139 §6: banks are independent records, new slice-3 story, not this one), **`Q12`** (✅ answered, D-129 §1), **`Q70`** (✅ answered — D-139 §1/D-141: warn-and-acknowledge)
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
| A withholding category and a tax registration number on the party record — **the category moves off per D-139 §5; the tax registration number stays** | [Verified: 2026-09-09 @ `src/Domain/MasterData/Supplier.cs` -> `WithholdingCategory`, `SetTaxDetails`] |
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
| 4 | ✅ **The withholding rate moves off this record — D-139 §5**, exactly as for the subcontractor. It is on the party record today [Verified: 2026-09-09 @ `src/Domain/MasterData/Supplier.cs` -> `WithholdingCategory`]; the rate is now set per contract/job, and `WithholdingCategory` is removed from `Supplier` — `KAFF-318`'s ground, not this story's | **D-139 §5** |
| 5 | **Withholding on a supplier payment is a liability Kaff carries to remit, never a recoverable asset.** §6.7 runs in two directions; **this record does not hold the rate (rule 4) and must never present the concept as recoverable or netted against the client side either** | **§6.7** — MUST · CLAUDE.md |
| 6 | **The tax registration number identifies the legal entity and does not vary by job**, so it stays on the record even though the rate does not (rule 4). ✅ **Who may set it is ruled — D-139 §5: entered and managed by Finance only.** `SupplierManage` is already Finance and the Owner (rule 9), so unlike `KAFF-211` **no split is needed here** — Finance already owns the whole record | **§6.7 amendment · D-139 §5** |
| 7 | ✅ **A repeated supplier phone warns and is acknowledged, never refuses — D-139 §1, D-141.** `ux_suppliers_phone` is dropped for a non-unique `ix_suppliers_phone`, the client's shape | **D-139 §1 · D-141** |
| 8 | Archiving replaces deletion; there is no delete path and this story adds none [Verified: 2026-09-09 @ `src/Domain/MasterData/Supplier.cs` -> `Archive`]. ⚠️ **A supplier with postings against him must never be removable**, and in slice 3 that is enforced by the postings being append-only rather than by anything here | CLAUDE.md · KAFF-123's precedent |
| 9 | `SupplierManage`, `CompanyWide`, **no assignment** — a supplier belongs to no project, which is the same fact as rule 2 seen from the permission side. Finance settled by §2; **the Owner keeps it too** [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.SupplierManage`] | §2 · D-044 ruling 4 · **D-129 §1** |
| 10 | Create, edit and archive are state changes and **each writes an audit record**, before and after — the tax details included | **CLAUDE.md** |
| 11 | Every string is an i18n key; Arabic RTL at 390px, names and numbers bidi-isolated | CLAUDE.md · `ux/rtl-and-i18n.md` |
| 12 | ⛔ **Bidding, RFQ and quote comparison are out of scope by name**, and the entity says so itself [Verified: 2026-09-09 @ `src/Domain/MasterData/Supplier.cs` -> `Supplier`]. **This is the screen where somebody will want to add "compare two quotes", and the answer is no** | **§1** · CLAUDE.md |
| 13 | ✅ **A bank is not a supplier — `Q13` answered, D-139 §6.** *"Banks are independent master records. No story exists for this. The BA cuts one and places it where it belongs, probably slice 3, Treasury."* **This story creates no bank record and no bank field, and does not model a bank as a supplier to save a table** | **D-139 §6** · §6.3 |

## Permissions, money, audit, i18n
- **Permissions:** `SupplierManage`, `CompanyWide`, no assignment. Finance and the Owner today. Every
  other role refused `403` server-side — **including the Technical Office**, which owns the
  subcontractor and not this.
- **Money:** ⛔ **This story writes no `Posting`, opens no account and stores no balance.** It holds one
  identifying field — the tax registration number — and **no withholding rate at all** (rule 4, D-139
  §5). What Kaff owes is derived by summing postings on one company-scoped sub-ledger in slice 3, and
  **the one account per supplier is the whole point of the §2 row**.
- **Audit:** rule 10, before and after on every changed field.
- **i18n:** `supplier.list_title`, `supplier.create_title`, `supplier.edit_title`,
  `supplier.field.code`, `supplier.field.name`, `supplier.field.phone`, `supplier.field.address`,
  `supplier.field.tax_registration_number`, `supplier.archive_action`, plus the existing
  `errors.master.*` keys. **`supplier.field.withholding_category` is dropped** — the field it named no
  longer lives here.

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

**AC-212-C — no balance and no withholding rate is stored anywhere on this record** *(fails if the rule is broken)*
Given a supplier
When his stored properties are enumerated as an allow-list
Then no balance, outstanding, total, purchases-to-date, withholding rate or any other amount-typed member appears among them, under that name or any other
And the allow-list is written out by name, so adding one is a deliberate edit to this test rather than a silent widening

**AC-212-D — two suppliers cannot share a code** *(fails if the rule is broken)*
Given a supplier with code `S-100`
When a second is submitted with `S-100`, and again with `s-100`
Then both are refused, and the refusal survives two requests arriving at the same instant — the guarantee is the unique index, not a read-then-write

**AC-212-E — nothing here presents withholding as recoverable or nets it against the client side** *(fails if the rule is broken)*
Given a supplier record and every screen showing it
When they are inspected
Then none of them carries a withholding rate — it lives on the contract/job (D-139 §5) — and none presents the concept as recoverable, as an asset, or as a figure netted with a corporate client's tax withheld at source

**AC-212-F — the withholding rate is not on this record** *(fails if the rule is broken, restated 2026-09-12 — `Q29` answered: D-139 §5)*
Given a supplier delivering materials on one job and a service on another
When the entity's stored properties are enumerated as an allow-list
Then no withholding rate or category appears among them — it is set per contract/job, on `KAFF-318`'s ground

**AC-212-G — a repeated supplier phone warns and is acknowledged, never refused** *(fails if the rule is broken, restated 2026-09-12 — `Q70` answered: D-139 §1, D-141)*
Given a supplier already registered with a number
When a second is submitted with the same number and no acknowledgement
Then the save is refused `409 errors.master.duplicate_phone_not_acknowledged`, naming the existing supplier
And with `AcknowledgedDuplicatePhone: true` the save succeeds and one `DuplicatePhoneAcknowledged` audit record is written

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
| Every criterion is Given / When / Then | ✅ — `AC-212-A` … `AC-212-L` |
| Stable `AC-212-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–13; rules 4, 6 and 7 re-cited to `D-139 §§1, 5, 6` / `D-141` |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `SupplierManage`, CompanyWide, no assignment; the Technical Office's exclusion stated; the Owner grant confirmed by **D-129 §1** |
| Money behaviour named explicitly | ✅ — rules 2, 3, 5; `AC-212-B`, `AC-212-C`, `AC-212-E`. **Stores no balance, holds no withholding rate, opens no account, writes no posting, nets nothing** |
| Arabic UI strings as i18n keys | ✅ — nine keys, rule 11 |
| The audit record it writes is stated | ✅ — rule 10, `AC-212-J` |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated [Verified: 2026-09-09 — `qa/` holds `slice-1`, `questions.md`, `README.md`, `risk-register.md` and `strategy.md`]. Nine criteria are marked *(fails if the rule is broken)*. **QA's to write** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ — `Q29` (D-139 §5), `Q70` (D-139 §1 / D-141) and `Q13` (D-139 §6, and it never blocked this story's own behaviour) are all answered. `Q12` is answered (D-129 §1) |

**Flip the trailer to `READY` when QA's cases land.** Backend still owes removing `WithholdingCategory`
from `Supplier` before `AC-212-F` is true of the running system.

## Not in this story
- **The subcontractor.** `KAFF-211` — a different §2 row with a different owner. ⛔ **Not a synonym,
  and the two records must not be merged into one "vendor" table**, however similar their fields look.
- **Any account, posting or balance.** Slice 3. `SupplierPayable` exists as a type and this story
  opens none.
- **Paying a supplier, and the withholding rate and Kaff's remittance of it.** Slice 3's `KAFF-318` and
  the contract/job mechanism `Q29`'s answer now points to.
- **Purchase orders, deliveries, material requests and stock.** Not in `stories/backlog.md`'s slice-2
  table and not invented here.
- **Bidding, RFQ, quote comparison.** §1, out of scope by name, `AC-212-K`.
- **Banks.** `Q13` is answered (D-139 §6): independent master records, cut as a new story,
  `KAFF-320`, in `stories/slice-3-treasury/`. **Not `KAFF-316`/`317`** — those are Collections and
  client-side withholding, a pre-existing mis-citation in this file corrected here.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q29`** | ✅ **ANSWERED — D-139 §5.** Per contract/job, as for the client | **Closed** |
| **`Q13`** | ✅ **ANSWERED — D-139 §6.** Banks are independent master records, cut as `KAFF-320` (slice 3). **Not built inside this story** | **Closed** |
| **`Q70`** | ✅ **ANSWERED — D-139 §1, D-141.** Warn-and-acknowledge | **Closed** |
| **`Q12`** | ✅ **ANSWERED — D-129 §1.** The Owner keeps `SupplierManage`, as D-044 ruling 4's own example list had already named suppliers explicitly | **Closed** |
