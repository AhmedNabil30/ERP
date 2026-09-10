# KAFF-203 · Find a catalogue item by code or description

<!-- kaff id=KAFF-203 slice=2 points=3 state=BUILT verdict=REJECTED at=dd5f14c on=2026-09-10 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 3 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-08 by the BA. **⚠️ Amended 2026-09-09 — `Q63` and `Q12` are both answered (D-129 §5, D-129 §1) and `AC-203-I` is written. One Definition-of-Ready box is still unticked — see *Definition of Ready* below.**
**Spec:** **§4.5** (*"searches the catalogue by code or description"*), §4.1, §4.2, §12 · **Decisions:** D-035, D-044 ruling 4, **D-129 §5, D-129 §1**
**Register:** `stories/questions-for-karim.md` → **`Q63`** (✅ answered, D-129 §5), **`Q12`** (✅ answered, D-129 §1)
**Screens:** `ux/screen-inventory.md` → **`S-017`** (catalogue list) · `ux/components.md` §12 (`kaff-search-input`), §9 (`kaff-empty-state`), §8 (`kaff-table`)
**Owner:** Backend, then Frontend
**Depends on:** KAFF-200 or KAFF-202 — something must put items in the catalogue first

## Story
As the Technical Office, I find an item by its code or by a piece of its description, because §4.5
makes this search the way every BOQ line is added and a catalogue nobody can search is a catalogue
nobody uses.

## Why this story is bigger than a list screen

**§4.5 makes this search a load-bearing component of slice 4, not a convenience:**

> *"'Add item' is always visible and **searches the catalogue by code or description**. Selecting an
> item auto-creates its باب section if absent."*

The BOQ builder (`S-054`) calls this same search. Building it narrowly here — code-prefix only, or
Latin-only matching — produces a BOQ builder that cannot find items whose description is the only
thing the engineer remembers, and that failure appears two slices later in someone else's story.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | Search matches on **code** and on **description**, and a partial description matches | **§4.5** |
| 2 | The description Kaff writes is Arabic [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `DescriptionAr`], so a substring search must work in Arabic — the same requirement `AC-124-D` carries for client names | §4.1 · §4.5 |
| 3 | A code search is case-insensitive. Codes are stored upper-cased [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Create`], so a lower-case search that returned nothing would be an artefact of storage, not a business rule | §4.5 · slice 0 |
| 4 | The list shows what `ux/screen-inventory.md` -> `S-017` names: *"Code, description, unit, باب, cost price, base sell rate, status"* | `ux/screen-inventory.md` -> `S-017` · §4.1 |
| 5 | **`costPrice` is on this list and this list is internal.** S-017 is `TO, O`. §4.2's prohibition is on client-facing output, and the boundary rule of `ux/screen-inventory.md` — *"Cost never crosses to a client-facing surface"* — names the catalogue (S-017) by number | **§4.2** · `ux/screen-inventory.md` -> `S-017` |
| 6 | **A `Role.Client` user cannot reach this endpoint under any circumstances.** It carries cost prices for every item Kaff sells; §12 is absolute and D-035's portal boundary means this endpoint is not on the portal surface at all | **§12** · D-035 |
| 7 | An empty result renders an **explicit empty state**, never a blank area and never a phantom row — and the *"nothing matches the filter"* empty state, not the *"nothing exists yet"* one, which are different messages with different actions | §4.5 (*"Empty BOQ shows an explicit empty state. Never phantom pre-filled rows"*) · `ux/components.md` §9 |
| 8 | The list carries no money beyond the item's own two prices — no BOQ total, no project value, no margin. There is none on the entity to project [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `CatalogueItem`], so the rule is about what the projection must not **join** in | §6.1 · CLAUDE.md |
| 9 | `CatalogueManage`, `CompanyWide`, no assignment. Technical Office settled by §2; **the Owner keeps it too** | §2 · D-044 ruling 4 · **D-129 §1** |
| 10 | This is a read. **It writes no audit record**, and the audit trail is not a search log | CLAUDE.md — audit is on *state changes* |
| 11 | Every string is an i18n key; Arabic RTL, correct and scrollable at 390px. Codes and prices inside Arabic rows are bidi-isolated with `<bdi>`, and the table scrolls inside its own container rather than the page body | CLAUDE.md · `ux/components.md` §8 |
| 12 | **The list is ordered by باب, then by item code within each باب.** It follows from `Q60`'s template (D-129 §2): the باب is one of the file's own columns, so grouping by trade is the shape the data already arrives in and the way a price list is read | **D-129 §5** |

## Permissions, money, audit, i18n
- **Permissions:** `CatalogueManage`, `CompanyWide`, no assignment required. `Role.Client` refused
  absolutely (rule 6); every other role without the permission refused `403`.
- **Money:** displays `costPrice` and `baseSellRate`, four decimals stored and two displayed
  (D-044 ruling 6). **Moves none.**
- **Audit:** none. It is a read.
- **i18n:** `catalogue.list.title`, `catalogue.search.placeholder`, `catalogue.list.empty`,
  `catalogue.list.empty_filtered`, `catalogue.column.code`, `catalogue.column.description`,
  `catalogue.column.unit`, `catalogue.column.bab`, `catalogue.column.cost_price`,
  `catalogue.column.base_sell_rate`, `catalogue.column.status`, `catalogue.status.active`,
  `catalogue.status.archived`.

## Acceptance criteria

**AC-203-A — a code finds the item, in either case**
Given an item with code `CONC-100`
When `CONC-100` is searched, and again as `conc-100`
Then both return that item

**AC-203-B — a partial Arabic description finds the item** *(fails if the rule is broken)*
Given an item whose Arabic description is a phrase of several words
When a substring from the middle of that phrase is searched
Then the item is returned

**AC-203-C — the search reaches both fields in one query**
Given one item matching only by code and another matching only by description, for the same search term
When that term is searched once
Then both items are returned — the caller does not choose which field to search

**AC-203-D — an empty result says so, and says which empty state it is**
Given a search matching nothing, against a catalogue that holds items
When the results render
Then `catalogue.list.empty_filtered` is displayed with a clear-search action, **not** the *"nothing exists yet"* message and **not** a blank area
And given a catalogue holding no items at all, `catalogue.list.empty` is displayed instead

**AC-203-E — a portal client cannot search the catalogue** *(fails if the rule is broken)*
Given a `Role.Client` user with a valid portal session
When the catalogue search endpoint is called with any term and any filter
Then it is refused, and no item code, description or price appears in the response body

**AC-203-F — a role without `CatalogueManage` is refused** *(fails if the rule is broken)*
Given a signed-in user of each role that does not hold `CatalogueManage`
When each calls the search endpoint directly, with no browser involved
Then every one is refused `403`

**AC-203-G — no money beyond the item's own two prices** *(fails if the rule is broken)*
Given the list response contract
When it is inspected
Then it carries the item's cost price and base sell rate and no other money-shaped field — no total, no margin, no project or BOQ value

**AC-203-H — Arabic, RTL, at mobile width**
Given S-017 at 390px in Arabic
When it renders
Then direction is RTL, the identity column is at the inline-start, codes and figures are bidi-isolated, the table scrolls inside its own container and the page body does not scroll horizontally

**AC-203-I — ordered by باب, then by code**
Given a search or an unfiltered list spanning several أبواب
When the results render
Then items are grouped by باب, and within each باب ordered by item code
And the Arabic collation that decides where ا / أ / إ fall relative to one another is **not** ruled by this criterion — that half is `Q63`'s standing caveat and is the Architect's, not Karim's (D-129 §5)

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-203-A` … `AC-203-I` |
| Stable `AC-203-<LETTER>` ids | ✅ — `I` is now written; the next takes `J` |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–12 |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `CatalogueManage`, CompanyWide, no assignment; `Role.Client` absolutely refused; the Owner grant confirmed by **D-129 §1** |
| Money behaviour named explicitly | ✅ — rules 5, 8; `AC-203-G`. Moves none |
| Arabic UI strings as i18n keys | ✅ — thirteen keys |
| The audit record it writes is stated | ✅ — **none**, rule 10, stated rather than omitted |
| **QA has written at least one scenario that fails if the rule is broken** | ✅ **Met 2026-09-09** — `qa/slice-2/test-cases.md`, `TC-2-001`…`TC-2-098`. |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ — `Q63` (D-129 §5) and `Q12` (D-129 §1) are both answered |

**Flip the trailer to `READY` when QA's cases land.** Everything else is ruled. The Arabic-collation
caveat is not this story's to close — it is the Architect's, per D-129 §5.

## Not in this story
- **Creating or editing an item.** `KAFF-202`, reached from this list.
- **Archived items and how they are filtered.** `KAFF-206` — including whether archived items appear
  in this search at all, which is that story's question and not this one's default.
- **The BOQ builder's "Add item".** `S-054`, slice 4. It calls this search; it does not live here.
- **The custom-item review queue.** `S-020`, §4.5, slice 4.
- **Any margin or profit figure.** `S-056`, slice 4, and §4.2 forbids a blended one anywhere.

## Questions

| # | Question, as Nabil should ask it | Owner |
|---|---|---|
| **`Q63`** | ✅ **ANSWERED — D-129 §5.** Ordered by باب, then by item code. `AC-203-I` is written. The Arabic-collation caveat (ا / أ / إ) is **not** answered and is the Architect's | **Closed** for the ordering; Architect for collation |
| **`Q12`** | ✅ **ANSWERED — D-129 §1.** The Owner keeps `CatalogueManage` | **Closed** |
