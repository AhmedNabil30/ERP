# KAFF-203 · Find a catalogue item by code or description

<!-- kaff id=KAFF-203 slice=2 points=3 state=NOT-BUILT verdict=none at=- on=2026-09-08 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 3 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-08 by the BA. **Two Definition-of-Ready boxes are unticked, and one open question blocks a QA case rather than the story.**
**Spec:** **§4.5** (*"searches the catalogue by code or description"*), §4.1, §4.2, §12 · **Decisions:** D-035, D-044 ruling 4
**Register:** `stories/questions-for-karim.md` → **`Q63`** (new, and it blocks a test case, not this story), **`Q12`**
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
| 9 | `CatalogueManage`, `CompanyWide`, no assignment. Technical Office settled by §2; the Owner grant stands on **`Q12`** | §2 · D-044 ruling 4 · **`Q12`** |
| 10 | This is a read. **It writes no audit record**, and the audit trail is not a search log | CLAUDE.md — audit is on *state changes* |
| 11 | Every string is an i18n key; Arabic RTL, correct and scrollable at 390px. Codes and prices inside Arabic rows are bidi-isolated with `<bdi>`, and the table scrolls inside its own container rather than the page body | CLAUDE.md · `ux/components.md` §8 |

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

**AC-203-I — ⏳ HELD on `Q63`: the order the results come back in**
**Not written.** Nothing in `spec.md` or in `ux/` states an order for the catalogue list — by code, by باب then code, by description, or by relevance to the search term. This is the same gap `Q59` records for the user and client lists, one list across. **It blocks a test case, not the story**: the search works in any order, and a wrong order for Kaff would simply never be noticed. `Q63`.

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-203-A` … `AC-203-H` |
| Stable `AC-203-<LETTER>` ids | ✅ — `I` allocated and held; the next takes `J` |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–11 |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `CatalogueManage`, CompanyWide, no assignment; `Role.Client` absolutely refused; the Owner half flagged to `Q12` |
| Money behaviour named explicitly | ✅ — rules 5, 8; `AC-203-G`. Moves none |
| Arabic UI strings as i18n keys | ✅ — thirteen keys |
| The audit record it writes is stated | ✅ — **none**, rule 10, stated rather than omitted |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated. **QA's to write.** ⚠️ **And `Q63` means QA cannot write an ordering case even once the range exists** — the same shape `Q59` produced for `AC-127-A` and `AC-126-A` |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ⛔ **Not met — `Q12`**, the slice-wide grant. **`Q63` does not block it**: the story is buildable and demonstrable in any order, which is exactly why the order would go unchecked |

**Flip the trailer to `READY` when `Q12` is ruled and QA's cases land.** `Q63` is answered later or
not at all; if it is answered, `AC-203-I` is written then.

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
| **`Q63`** | **"When you open the price list, what order do you want the items in — by their code, grouped by باب, alphabetically by description, or something else?"** Say why it is being asked: **nothing in `spec.md` or in the screen designs states an order for this list**, and the same Arabic-collation caveat `Q59` carries applies here too. **Ask it in the same breath as `Q59`** — it is the same question about a third list, and asking them together is what makes the pattern visible. **New, raised by this story. Blocks a QA case, not the story** | **Karim** |
| **`Q12`** | Open, slice-2-wide. Whether the Owner keeps `CatalogueManage` | **Karim** |
