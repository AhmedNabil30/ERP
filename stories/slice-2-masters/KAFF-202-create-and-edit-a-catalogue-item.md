# KAFF-202 · Create and edit a catalogue item

<!-- kaff id=KAFF-202 slice=2 points=3 state=NOT-BUILT verdict=none at=- on=2026-09-08 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 3 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-08 by the BA. **Two Definition-of-Ready boxes are unticked — and neither is a business question this story raises.**
**Spec:** **§4.1**, **§4.2**, **§4.4**, §2 · **Decisions:** D-018 (the `status` values, 🟡), D-044 ruling 4
**Register:** `stories/questions-for-karim.md` → **`Q12`** (open, slice-2-wide)
**Screens:** `ux/screen-inventory.md` → **`S-018`** (create / edit), reached from **`S-017`** (list)
**Owner:** Backend, then Frontend
**Depends on:** KAFF-204 — an item must name a باب that exists

## Story
As the Technical Office, I add and correct catalogue items by hand, because `spec.md` §4.1 loads the
catalogue from Excel once and then says *"edited manually after"* — the spreadsheet is how the
catalogue arrives, not how it lives.

## What already exists, and what this story adds

**The entity, its validation and its persistence are built.** This story adds two endpoints and one
screen and rebuilds none of it:

| Already built | Evidence |
|---|---|
| Creation with every §4.1 field, and its guards — code, Arabic description and unit all required, neither price negative | [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Create`, `MaxCodeLength`, `MaxDescriptionLength`, `MaxUnitLength`] |
| Re-pricing as its own behaviour, not a public setter | [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Reprice`] |
| The errors those guards return | [Verified: 2026-09-08 @ `src/Domain/MasterData/MasterDataErrors.cs` -> `CodeRequired`, `DescriptionRequired`, `UnitRequired`, `CostPriceMustNotBeNegative`, `SellRateMustNotBeNegative`] |
| `decimal(18,4)` on both prices, by convention rather than per property | [Verified: 2026-09-08 @ `src/Infrastructure/Persistence/KaffDbContext.cs` -> `ConfigureConventions`] |
| A unique index on the code | [Verified: 2026-09-08 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_catalogue_items_code`] |
| The permission row | [Verified: 2026-09-08 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.CatalogueManage`] |

**What does not exist is any endpoint or screen** — there is no `Catalogue` feature folder in
`src/Api/Features/` [Verified: 2026-09-08 — the folder holds `Assignments`, `Audit`, `Auth`,
`Clients`, `Health`, `Setup` and `Users`].

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | An item carries exactly §4.1's fields: `code · description · unit · bab · costPrice · baseSellRate · status`. No field is added to the entity by this story | §4.1 |
| 2 | **The code is typed, not generated** — it comes from Kaff's own spreadsheet, and §4.1 makes the item's code one of its imported fields. ⚠️ **This is the opposite of a client code**, which D-049 ruling 7 makes generated (`C-10001`) with manual entry forbidden. The contrast is deliberate and is stated here so a later session does not "correct" one to match the other | §4.1 · contrast with D-049 ruling 7 |
| 3 | Two items cannot share a code. Enforced by the database, not by a read-then-write in the handler [Verified: 2026-09-08 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_catalogue_items_code`] | §4.5 (*"searches the catalogue by code"* — a code that identifies two items identifies neither) · slice 0 |
| 4 | **Editing a price cannot reach a signed BOQ.** §4.4 is a MUST: the BOQ holds copies and no foreign key back to the catalogue, so there is no path to follow. **The correct implementation of this rule in this story is to add nothing** — no cascade, no notification to a BOQ, no "update signed lines" option, however helpful it would look | **§4.4** — MUST |
| 5 | **Editing a price does not silently re-price an open estimate either.** §4.4's second paragraph gives one mechanism — the *"you have X open offers on old pricing"* review, `S-049`, where **a human decides per estimate**. That is slice 4. This story raises no alert and re-prices nothing | §4.4 · `ux/screen-inventory.md` -> `S-049` |
| 6 | `costPrice` is captured on this screen and **must never appear in any client-facing output** — screen, print view, PDF, export or photo caption. S-018 is `TO, O` and internal | **§4.2** |
| 7 | The screen shows cost and sell **as two figures**. It shows no single blended margin number; §4.2 forbids that on the margin surfaces and Kaff's owner *"found it unreadable"*. If this screen shows a margin at all it shows cost, sell and profit % separately | §4.2 |
| 8 | Neither price may be negative [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Create`]. **A sell rate below the cost price is not refused** — nothing in `spec.md` forbids selling at a loss, and refusing it would be inventing a rule | §4.1 · §4.2 |
| 9 | An item's باب is required and must exist [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `BabId`]. **Moving an item to a different باب is `KAFF-205`, not this story** | §2 · §4.1 |
| 10 | `CatalogueManage`, `CompanyWide`, **no assignment** — the catalogue belongs to no project. Technical Office settled by §2; the Owner grant stands on **`Q12`** | §2, §4.1 · D-044 ruling 4 · **`Q12`** |
| 11 | Create and edit are state changes and **each writes an audit record**: who, when, and what changed before and after — including both prices when either moves | **CLAUDE.md** — *"Every state change writes an audit record"* |
| 12 | Every string is an i18n key. Arabic, RTL, correct and scrollable at 390px — S-018 is `M3`, desktop-primary, which is a design priority and not an exemption from the Definition of Done | CLAUDE.md · `ux/screen-inventory.md` -> `S-018` |

## Permissions, money, audit, i18n
- **Permissions:** `CatalogueManage`, `CompanyWide`, no assignment required. Every other role refused
  `403` server-side; hiding the menu item is not the control.
- **Money:** `costPrice` and `baseSellRate` are `Money` at `decimal(18,4)`. **This story moves no
  money and writes no `Posting`** — a catalogue is a price list, not a ledger.
- **Audit:** rule 11, before and after on every changed field.
- **i18n:** `catalogue.item.create_title`, `catalogue.item.edit_title`, `catalogue.field.code`,
  `catalogue.field.description`, `catalogue.field.unit`, `catalogue.field.bab`,
  `catalogue.field.cost_price`, `catalogue.field.base_sell_rate`, `catalogue.field.status`,
  `errors.catalogue.code_taken`, plus the existing `errors.*` keys for the domain guards.

## Acceptance criteria

**AC-202-A — an item is created with §4.1's fields and nothing else**
Given the Technical Office on S-018
When a code, Arabic description, unit, باب, cost price and base sell rate are submitted
Then the item exists carrying exactly those values, its status is `Active`, and the response carries no field §4.1 does not name

**AC-202-B — a repeated code is refused by the database, not by a lookup** *(fails if the rule is broken)*
Given an item with code `CONC-100`
When a second item is submitted with code `CONC-100`, and again with `conc-100`
Then both are refused, and the refusal survives two requests arriving at the same instant — the guarantee is the unique index, not a read-then-write

**AC-202-C — both prices keep four decimals** *(fails if the rule is broken)*
Given a cost price of `987.6543` and a base sell rate of `1234.5678`
When the item is saved and read back
Then both values are exact to the fourth decimal, and neither has passed through a `float` or a `double` at any point between the request body and the database

**AC-202-D — a negative price is refused, a loss-making one is not**
Given a cost price of `-1`
When it is submitted
Then it is refused with `CostPriceMustNotBeNegative`
And given a base sell rate below the cost price, the save **succeeds** — no rule forbids it

**AC-202-E — re-pricing an item does not touch a signed BOQ** *(fails if the rule is broken)*
Given a signed BOQ line carrying an item's values at signature time
When that catalogue item's cost price and sell rate are both changed
Then every value on the signed BOQ line is unchanged
And no field on the BOQ line holds a foreign key to the catalogue row, so there is no path a future change could take

**AC-202-F — re-pricing raises no alert and re-prices no estimate** *(fails if the rule is broken)*
Given open, unsigned estimates that reference the item
When the item is re-priced
Then nothing on any estimate changes, and this story raises no alert — §4.4's review is `S-049`, slice 4

**AC-202-G — cost price never leaves the internal surface** *(fails if the rule is broken)*
Given an item with a cost price
When every client-reachable surface is inspected — each `/api/portal/*` response and every client-facing print or export
Then `costPrice` appears in none of them, under that name or any other, and no blended margin figure appears anywhere

**AC-202-H — a role without `CatalogueManage` reaches neither endpoint** *(fails if the rule is broken)*
Given a signed-in user of each role that does not hold `CatalogueManage`
When each calls the create endpoint and then the edit endpoint directly, with no browser involved
Then every call is refused `403`, and no item is created or changed by any of them

**AC-202-I — every change is audited before and after** *(fails if the rule is broken)*
Given an item whose description and base sell rate are both edited in one request
When the audit trail is read
Then one record names the actor, the time, and both fields with their old and new values
And a refused edit writes no audit record and changes nothing

**AC-202-J — Arabic, RTL, at mobile width**
Given S-018 at 390px in Arabic
When it renders
Then direction is RTL, prices and codes are bidi-isolated inside Arabic text, no string is a literal in either language, and the page body does not scroll horizontally

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-202-A` … `AC-202-J` |
| Stable `AC-202-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–12 |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `CatalogueManage`, CompanyWide, **no assignment**; the Owner half flagged to `Q12` rather than assumed |
| Money behaviour named explicitly | ✅ — rules 6, 7, 8; `AC-202-C`, `AC-202-D`, `AC-202-G`. Moves none |
| Arabic UI strings as i18n keys | ✅ — ten keys, rule 12 |
| The audit record it writes is stated | ✅ — rule 11, `AC-202-I` |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated. Seven criteria are marked *(fails if the rule is broken)*; **the case is QA's to write, not the BA's.** Routed to QA |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ⛔ **Not met — `Q12`**, and it is the slice-wide one rather than this story's own. `stories/backlog.md` lists slice 2 as *"Blocked by: Q12 and Q13"*, due **before** the slice opens. It removes or keeps one permission grant; it does not reshape a single criterion above |

**This is the closest of the six to Ready.** Flip the trailer when `Q12` is ruled and QA's cases land.
**Nothing else is outstanding**, and no question this story raises is unanswered — because it raises
none.

## Not in this story
- **Importing.** `KAFF-200` and `KAFF-201`.
- **Search and the list.** `KAFF-203` builds S-017's search; this story's screen is reached from it.
- **Archiving.** `KAFF-206` — *"without breaking what already references it"* is its own problem.
- **Moving an item to another باب.** `KAFF-205`.
- **Creating a باب.** `KAFF-204`.
- **Custom items flagged `pendingCatalogueReview`.** §4.5, a BOQ surface, slice 4 — and they **MUST NOT
  write to the catalogue automatically**, which is a prohibition this story must not quietly relax by
  exposing its create endpoint to the BOQ builder.
- **Any margin panel.** `S-056`, slice 4.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q12`** | Open, slice-2-wide. Whether the Owner keeps `CatalogueManage`, or comes off it because his *"all master data"* was the literal list of three. **Not raised here — it is already in the register and already named as slice 2's blocker** | **Karim** |
| 1 | **What `status` values an item may hold.** §4.1 lists `status` and never enumerates it; the code carries `Active` and `Archived` as *"the minimum the freeze rule needs"* and flags itself 🟡 [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `CatalogueItemStatus`]. **This story creates items `Active` and offers no control that sets any other value**, so it does not turn on the answer. `KAFF-206` does | **Nabil** · D-018 |
