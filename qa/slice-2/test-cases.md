# Slice 2 — test cases

**Range allocated: `TC-2-001` … `TC-2-098`, plus `TC-2-000` as the non-case format template.**
Checked against `qa/slice-1/test-cases.md` before writing a single case: slice 1's identifiers run
`TC-1-000` … `TC-1-306` [Verified: 2026-09-09 @ `qa/slice-1/test-cases.md` -> `TC-1-306`, the highest
id in that file]. `qa/README.md`'s own ID scheme is `TC-<slice>-<nnn>` — the slice number is part of
the identifier, not a suffix on a shared counter — so `TC-2-nnn` cannot collide with `TC-1-nnn` under
the scheme as written; a `grep -r "TC-2-" qa/ stories/ decisions.md process/` run before this file
existed returned nothing [Verified: 2026-09-09]. **The range is taken anyway, and stated, because the
brief asked for it named and because a second slice-2 QA session should not have to re-derive that the
prefix itself is the collision guard.**

Covers the nine stories named Ready-but-for-QA in this brief — `KAFF-200`, `201`, `202`, `203`, `205`,
`206`, `207`, `208`, `213` — plus `KAFF-204`, whose *behaviour* is cased here and whose *data* is not.
`KAFF-209`, `210`, `211`, `212` get a held-open note each, no `TC-` ids, per the brief: a case written
against an unruled question would be worse than none.

## How to read a case

```
**TC-2-000 · what it checks**
`AC-2nn-X` · P1 · Api · spec.md §9 · D-044 ruling 1
Given … When … Then …
*Fails if:* the one defect this case catches.
```

Same conventions as `qa/slice-1/test-cases.md`: **Priority** P1 blocker · P2 major · P3 minor
(`qa/README.md`). **Layer** `Domain` (`tests/Domain.Tests`) · `Api` (`tests/Api.Tests`, real
PostgreSQL) · `E2E` (Playwright). The citation is the source of the expected result — `spec.md` or a
`decisions.md` D-number, never the implementation. **`Fails if:`** is the mutation from
`qa/strategy.md` §5, written as the defect the case catches; a case with no `Fails if:` line does not
get written. **`HELD Qnn`** marks a criterion the story itself writes as held on an open question —
the scenario is stated, nothing is asserted, and it is not a passing case.

## Story coverage index

| Story | Cases | Held / not written, and why |
|---|---|---|
| KAFF-200 import from Excel | TC-2-001…010 | — |
| KAFF-201 re-import is not a sync | TC-2-011…016 | — |
| KAFF-202 create/edit an item | TC-2-017…027 | — |
| KAFF-203 find an item | TC-2-028…036 | — |
| KAFF-204 باب tree + markup | TC-2-037…046 | data (Kaff's real trades/rates) — `Q75` |
| KAFF-205 re-parent / move item | TC-2-047…056 | — |
| KAFF-206 archive an item | TC-2-057…065 | un-archive endpoint — no story owns it yet |
| KAFF-207 employee register | TC-2-066…075 | — |
| KAFF-208 nobody in both populations | TC-2-076…082 | `AC-208-C` — no edit endpoint exists to call |
| KAFF-213 archive a باب | TC-2-083…092 | — |
| KAFF-209 register a worker from site | none | `Q70`, `Q71` — no ruled actor, no ruled dedup rule |
| KAFF-210 worker engagement history | none | `Q72` — the unit and scale are undefined |
| KAFF-211 subcontractor master | none | `Q29`, `Q73`, `Q70` |
| KAFF-212 supplier master | none | `Q29`, `Q70` |

**98 live cases.** Roles cited throughout are the nine in `Role.cs`: `Owner`, `Finance`,
`TechnicalOffice`, `SiteEngineer`, `HeadOfDesign`, `MarketingSales`, `Client`, `Subcontractor`, `Hr`
[Verified: 2026-09-09 @ `src/Domain/Identity/Role.cs` -> `enum Role`]. Grants read from the catalogue
before any negative case was written, not assumed from a story's prose
[Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.CatalogueManage`,
`Permission.BabManage`, `Permission.EmployeeManage`, `Permission.SubcontractorManage`,
`Permission.SupplierManage`]:

| Permission | Holders | Refused (the other seven) |
|---|---|---|
| `CatalogueManage`, `BabManage`, `SubcontractorManage` | Owner, TechnicalOffice | Finance, SiteEngineer, HeadOfDesign, MarketingSales, Client, Subcontractor, Hr |
| `EmployeeManage` | Owner, Hr | Finance, TechnicalOffice, SiteEngineer, HeadOfDesign, MarketingSales, Client, Subcontractor |
| `SupplierManage` | Owner, Finance | TechnicalOffice, SiteEngineer, HeadOfDesign, MarketingSales, Client, Subcontractor, Hr |

**`Q12` (D-129 §1) is why every "who may" case below reads Owner as a holder, not an exception.** The
Owner cases assert access; every refusal case belongs to the other roles named in the table above.

---

# KAFF-200 · Import the catalogue from Excel — masters ready for QA

**TC-2-001 · a clean file becomes a catalogue**
`AC-200-A` · P1 · Api, real PostgreSQL · §4.1
Given a spreadsheet of N valid rows in the template's shape, with every row's باب already present, and
the caller in turn the Technical Office and separately the Owner (`Q12`, D-129 §1), when each imports
it, then N catalogue items exist, each carrying the code, description, unit, باب, cost price and base
sell rate its row named.
*Fails if:* fewer or more than N items result, or a field is copied from the wrong column.

**TC-2-002 · a rate keeps its fourth decimal on import**
`AC-200-B` · P1 · Domain + Api, real PostgreSQL · CLAUDE.md · §4.1
Given a row whose base sell rate is `1234.5678` and whose cost price is `987.6543`, when the file is
imported and the two values are read back, then they are exactly `1234.5678` and `987.6543`, and
neither value has passed through a `float` or a `double` between the cell and the database.
*Fails if:* either value differs in the fourth decimal, or the reader's double accessor is used at any
point in the parse path.

⚠️ **Finding — `AC-200-B`'s second sentence cannot be executed as written.** *"The same holds for a rate
with more than four decimals … refused or rounded by a stated rule, never silently truncated"* names
two legal behaviours and **no rule saying which**. Nothing in `spec.md`, `decisions.md`, or `KAFF-200`
states whether a five-decimal cell is refused or rounded, or (if rounded) by what method. This case
therefore asserts only the exact round-trip at four decimals; the "more than four decimals" half is
**not cased** and is reported here as a criterion needing a ruling, the `AC-125-C` shape — not silently
dropped, not guessed.

**TC-2-003 · an unknown باب is not invented**
`AC-200-C` · P1 · Api, real PostgreSQL · §2 · §4.1 · D-129 §2
Given a row naming a باب that does not exist, when the file is imported, then no catalogue item is
created for that row, no باب is created for it either, and the row-level report names the row and the
باب it could not find.
*Fails if:* an unknown باب is silently created to let the row through, or the row is dropped with no
name attached to the failure.

**TC-2-004 · cost price never leaves the internal surface — import path**
`AC-200-D` · P1 · Api · §4.2
Given an imported catalogue, when every `/api/portal/*` response and every client-facing print or
export is inspected, then `costPrice` appears in none of them, under that name or any other.
*Fails if:* `costPrice` is projected onto any portal or client-facing contract, under any name.
**Positive control (D-116):** `TC-2-033` asserts `costPrice` **is** present on the internal
(`TO`/`Owner`) search response for the same imported items — proving this absence test would catch the
field if it leaked, rather than passing because nothing on either surface is ever checked.

**TC-2-005 · the screen states it is not a sync**
`AC-200-E` · P2 · E2E · §4.1 · `ux/screen-inventory.md` -> `S-019`
Given `S-019` before a file is chosen, when it renders, then `catalogue.import.not_a_sync` is displayed
as part of the screen body, not behind a tooltip or a help link.
*Fails if:* the sentence is present only in a tooltip, a help panel, or documentation.

**TC-2-006 · a role without `CatalogueManage` cannot import**
`AC-200-F` · P1 · Api · §2, §4.1 · D-129 §1
Given a signed-in user of each of Finance, SiteEngineer, HeadOfDesign, MarketingSales, Client,
Subcontractor and Hr, when each posts a valid file to the import endpoint directly, with no browser
involved, then every one is refused `403`, and no catalogue item is created by any of them.
*Fails if:* any one of the seven succeeds, or an item is created before the refusal is returned.

**TC-2-007 · the import is audited, and a refused import writes nothing**
`AC-200-G` · P1 · Api, real PostgreSQL · CLAUDE.md
Given a successful import, when the audit trail is read, then one record names the actor, the time,
the file, and the number of items created; and given a refused import (a role without the permission),
the catalogue count is unchanged and no audit record for it exists.
*Fails if:* a refused import still creates an audit record, or a successful one creates none.

**TC-2-008 · a bad row does not sink the good ones, and the report names it**
`AC-200-H` · P1 · Api, real PostgreSQL · D-129 §3 · D-130 §1
Given a file of 200 rows of which exactly one is invalid — a missing price, an unknown باب, a code
repeated within the file, tried as three separate sub-cases — when it is imported, then the other 199
rows become catalogue items, the one bad row does not, the response names that row's number and the
reason it was refused, and the catalogue afterward holds exactly 199 items.
*Fails if:* the whole file is refused because of the one bad row (all-or-nothing, the behaviour `Q61`
overturned), or the bad row's item is created anyway, or the report is silent on which row failed.

**TC-2-009 · the file's shape must be the template's shape**
`AC-200-I` · P2 · Api · D-129 §2
Given a file whose columns are `code, description, unit, bab, costPrice, baseSellRate, status` (the
template) tried once as-is, once with an extra column, and once with a column missing, when each is
imported, then the first is accepted and the other two are refused, each refusal naming what the
template requires and what the file carried instead; and the template download is reachable from
`S-019` before a file is ever chosen.
*Fails if:* a file with an extra or a missing column is silently accepted, mapping columns by position
rather than by name.

**TC-2-010 · a bad row's fix goes in on the next import** *(cross-story: KAFF-200 + KAFF-201)*
`AC-200-H`, `AC-201-F` · P1 · Api, real PostgreSQL · D-129 §3, §4 · D-130 §1
Given the refused row from `TC-2-008`, corrected in a second file and re-imported with the operator
confirming the preview, when the second import completes, then the previously-refused row's item now
exists, carrying the corrected values, and the 199 items from the first import are untouched.
*Fails if:* the corrected row cannot be added without re-importing the whole 200-row file, or the
second import silently re-prices any of the 199 untouched items.

---

# KAFF-201 · Re-importing is not a sync — masters ready for QA

**TC-2-011 · nothing imports itself between human-triggered imports**
`AC-201-A` · P1 · Api · §4.1
Given a catalogue loaded from a file, and that file changed on disk afterwards, when the system runs
for any length of time with nobody importing anything, then no catalogue item changes, and no
scheduled job, watched folder or background process reads a spreadsheet.
*Fails if:* any catalogue value changes with no human-triggered import in between.
**Positive control (D-116):** `TC-2-001` and `TC-2-016` show a human-triggered import **does** change
the catalogue — proving this test would catch a background sync if one existed, rather than passing
because the catalogue never changes at all.

**TC-2-012 · a second import states what it will do before it does it**
`AC-201-B` · P1 · Api · D-129 §4
Given a catalogue that already holds items and a second file chosen on `S-019`, when the operator has
not yet confirmed, then the preview names the count of items to be created and the count to be
re-priced, and no item has changed at that moment; and when the operator cancels instead, the catalogue
is byte-for-byte as it was.
*Fails if:* any item changes before confirmation, or cancelling still leaves a changed row.

**TC-2-013 · a signed BOQ is untouched by any import**
`AC-201-C` · P1 · Api, real PostgreSQL · §4.4
Given a signed BOQ line carrying an item's values at signature time, when a second import re-prices
that item's catalogue row, then every value on the signed line is unchanged, and no column on the line
holds a foreign key back to the catalogue row.
*Fails if:* any value on the signed line moves, or a foreign key from the line to the catalogue exists
at all.

**TC-2-014 · the import raises no re-pricing alert**
`AC-201-D` · P2 · Api · §4.4
Given open, unsigned estimates referencing items the import touches, when the import completes, then
nothing on any estimate changes and no alert is raised — `S-049`'s review is slice 4.
*Fails if:* an estimate line is silently re-priced, or an alert of any kind is raised by this story.

**TC-2-015 · the second import is audited with before and after**
`AC-201-E` · P1 · Api, real PostgreSQL · CLAUDE.md
Given a second import that changes any stored value, when the audit trail is read, then it names the
actor, the time and the file, and carries the before and the after of every value it moved.
*Fails if:* the record omits the actor, the file, or either side of a changed value.

**TC-2-016 · a second import creates, re-prices, and only after confirmation**
`AC-201-F` · P1 · Api, real PostgreSQL · D-129 §4
Given a catalogue already holding items and a second file that adds three new codes and re-prices two
existing ones, when the operator confirms, then the three new items are created, the two existing items
carry the file's new prices, every other item is untouched, and all five changes were named in the
preview shown before confirmation.
*Fails if:* fewer or more than five items change, or a change is applied that the preview did not name.

---

# KAFF-202 · Create and edit a catalogue item — masters ready for QA

**TC-2-017 · an item is created with exactly §4.1's fields**
`AC-202-A` · P1 · Api · §4.1
Given the Technical Office on `S-018`, when a code, Arabic description, unit, باب, cost price and base
sell rate are submitted, then the item exists carrying exactly those values, its status is `Active`,
and the response carries no field §4.1 does not name.
*Fails if:* the response or the stored row carries an extra field, or drops one of the six.

**TC-2-018 · a repeated code is refused by the unique index, not a lookup**
`AC-202-B` · P1 · Api, real PostgreSQL · §4.5 · slice 0
Given an item with code `CONC-100`, when a second item is submitted with `CONC-100`, and again as
`conc-100`, and again as two requests arriving simultaneously with `CONC-100`, then all three are
refused.
*Fails if:* a concurrent pair both succeed — the guarantee under test is the database's unique index,
not an application read-then-write that a race defeats.

**TC-2-019 · both prices keep four decimals through create and read-back**
`AC-202-C` · P1 · Domain + Api · CLAUDE.md
Given a cost price of `987.6543` and a base sell rate of `1234.5678`, when the item is saved and read
back, then both are exact to the fourth decimal, and neither has passed through a `float` or a `double`
between the request body and the database.
*Fails if:* either value differs at the fourth decimal.

**TC-2-020 · a negative price is refused**
`AC-202-D` · P1 · Domain · §4.1
Given a cost price of `-1`, when it is submitted, then it is refused with
`errors.master.cost_price_negative` (`CostPriceMustNotBeNegative`).
*Fails if:* the negative value is accepted.

**TC-2-021 · a loss-making sell rate is accepted** *(positive control for TC-2-020)*
`AC-202-D` · P2 · Domain · §4.1 · §4.2
Given a base sell rate below the item's cost price, when it is submitted, then the save succeeds.
*Fails if:* the save is refused — nothing in `spec.md` forbids selling at a loss, and a rule that
over-refuses here is exactly what pairs with `TC-2-020` to prove the negative-price guard is scoped to
negatives and not to "unprofitable."

**TC-2-022 · re-pricing does not touch a signed BOQ**
`AC-202-E` · P1 · Api, real PostgreSQL · §4.4
Given a signed BOQ line carrying an item's values at signature time, when that item's cost price and
sell rate are both changed, then every value on the signed line is unchanged and no column on the line
holds a foreign key to the catalogue row.
*Fails if:* any value on the signed line moves.

**TC-2-023 · re-pricing raises no alert and re-prices no estimate**
`AC-202-F` · P2 · Api · §4.4
Given open, unsigned estimates referencing the item, when the item is re-priced, then nothing on any
estimate changes and no alert is raised.
*Fails if:* an estimate line changes, or an alert is raised.

**TC-2-024 · cost price and margin never leave the internal surface — hand-edit path**
`AC-202-G` · P1 · Api · §4.2
Given an item with a cost price, when every `/api/portal/*` response and every client-facing print or
export is inspected, then `costPrice` appears in none of them, and no blended margin figure appears
anywhere.
*Fails if:* `costPrice` or a blended margin figure is projected onto any client-facing contract.
**Positive control (D-116):** `TC-2-033` shows `costPrice` **is** present on the internal search
response for the same item.

**TC-2-025 · a role without `CatalogueManage` reaches neither endpoint**
`AC-202-H` · P1 · Api · §2, §4.1 · D-129 §1
Given a signed-in user of each of Finance, SiteEngineer, HeadOfDesign, MarketingSales, Client,
Subcontractor and Hr, when each calls the create endpoint and then the edit endpoint directly, with no
browser involved, then every call is refused `403`, and no item is created or changed by any of them.
*Fails if:* any one of the seven succeeds at either endpoint.

**TC-2-026 · every change is audited before and after, a refusal writes nothing**
`AC-202-I` · P1 · Api, real PostgreSQL · CLAUDE.md
Given an item whose description and base sell rate are both edited in one request, when the audit
trail is read, then one record names the actor, the time, and both fields with their old and new
values; and given a refused edit (from `TC-2-025`), no audit record exists for it and nothing changed.
*Fails if:* the refused edit still writes a record, or the successful edit's record omits either field.

**TC-2-027 · Arabic, RTL, at mobile width**
`AC-202-J` · P3 · E2E · CLAUDE.md
Given `S-018` at 390px in Arabic, when it renders, then direction is RTL, prices and codes are
bidi-isolated inside Arabic text, no string is a literal in either language, and the page body does not
scroll horizontally.
*Fails if:* the body scrolls horizontally, or a price/code inside an Arabic label renders unisolated.

---

# KAFF-203 · Find a catalogue item by code or description — masters ready for QA

**TC-2-028 · a code finds the item, in either case**
`AC-203-A` · P2 · Api · §4.5
Given an item with code `CONC-100`, when `CONC-100` is searched, and again as `conc-100`, then both
return that item.
*Fails if:* the lower-case search returns nothing.

**TC-2-029 · a partial Arabic description finds the item**
`AC-203-B` · P2 · Api · §4.1 · §4.5
Given an item whose Arabic description is a phrase of several words, when a substring from the middle
of that phrase is searched, then the item is returned.
*Fails if:* only a prefix match is implemented and the middle-of-phrase substring returns nothing.

**TC-2-030 · one query reaches both fields**
`AC-203-C` · P2 · Api · §4.5
Given one item matching only by code and another matching only by description for the same search
term, when that term is searched once, then both items are returned.
*Fails if:* the caller must choose which field to search, or only one of the two items is returned.

**TC-2-031 · an empty result names which empty state it is**
`AC-203-D` · P3 · Api + E2E · §4.5 · `ux/components.md` §9
Given a search matching nothing against a catalogue that holds items, when the results render, then
`catalogue.list.empty_filtered` is displayed with a clear-search action, not `catalogue.list.empty`;
and given a catalogue holding no items at all (a separate, unfiltered case), `catalogue.list.empty` is
displayed instead.
*Fails if:* the two states are swapped, or either renders as a blank area.

**TC-2-032 · a portal client cannot search the catalogue**
`AC-203-E` · P1 · Api · §12 · D-035
Given a `Role.Client` user with a valid portal session, when the catalogue search endpoint is called
with any term, then it is refused, and no item code, description or price appears in the response
body.
*Fails if:* the request succeeds, or a refusal body still leaks a code, description or price.

**TC-2-033 · a role without `CatalogueManage` is refused, and the holder sees cost price**
`AC-203-F` · P1 · Api · §2, §4.1 · D-129 §1
Given a signed-in user of each of Finance, SiteEngineer, HeadOfDesign, MarketingSales, Client,
Subcontractor and Hr, when each calls the search endpoint directly, then every one is refused `403`;
and given the Technical Office or the Owner, the same call succeeds and the response carries the
item's `costPrice`.
*Fails if:* any of the seven refused roles succeeds, or the holder's response omits `costPrice` — the
second half is `TC-2-004`'s and `TC-2-024`'s positive control, proving the absence tests would notice
a leak rather than passing because nothing anywhere ever returns the field.

**TC-2-034 · no money beyond the item's own two prices**
`AC-203-G` · P2 · Api · §6.1 · CLAUDE.md
Given the list response contract, when it is inspected, then it carries the item's cost price and base
sell rate and no other money-shaped field — no total, no margin, no project or BOQ value.
*Fails if:* any additional money-shaped field is projected, joined in from another table.

**TC-2-035 · Arabic, RTL, at mobile width**
`AC-203-H` · P3 · E2E · CLAUDE.md · `ux/components.md` §8
Given `S-017` at 390px in Arabic, when it renders, then direction is RTL, the identity column is at
the inline-start, codes and figures are bidi-isolated, and the table scrolls inside its own container
rather than the page body.
*Fails if:* the page body scrolls horizontally, or the table's own container does not.

**TC-2-036 · ordered by باب, then by code**
`AC-203-I` · P2 · Api · D-129 §5
Given an unfiltered list spanning several أبواب, each containing items whose codes are not already in
order, when the results render, then items are grouped by باب, and within each باب ordered by item
code.
*Fails if:* items are ordered by code alone across the whole list, ignoring باب grouping. The
Arabic-collation question (ا/أ/إ ordering) is out of this case's scope — it is the Architect's per
`Q63`'s standing caveat, not asserted here either way.

---

# KAFF-204 · The باب tree with default markup — behaviour cased, data held on `Q75`

**All test fixtures below are arbitrary باب names invented for the test** (e.g. "Test Trade A" /
"باب اختبار أ"), with arbitrary markup values chosen to exercise the arithmetic — **never
`spec.md` §4.2's own examples of *concrete* 15% or *finishes* 30%, and never Kaff's real trades.**
`Q75` (D-130 §8) is refused as a decision and stays Karim's alone; no case here seeds or asserts a real
trade name or a real markup, and no case asserts the tree's real ~40-trade content or size — rule 6's
*"roughly 40, a size not a limit"* is not something a case can drive without inventing what the 40 are.

**TC-2-037 · a باب is created with a name, a parent and a markup**
`AC-204-A` · P1 · Api · §2
Given the Technical Office, and separately the Owner (`Q12`, D-129 §1), on `S-022`, when a code,
Arabic name, English name, optional parent and default markup are submitted, then the باب exists
carrying those values and appears in the tree under its parent, or as a root when none was given.
*Fails if:* the باب is not created, or appears under the wrong parent.

**TC-2-038 · a markup entered as a whole number is stored as its fraction**
`AC-204-B` · P1 · Domain · D-044 ruling 6
Given a default markup entered as `10`, meaning ten percent, and separately as `12.75`, when each is
stored and read back, then the first is the fraction `0.10` and applying it to a rate of `100` gives
`110` (not `1100`), and the second survives its round trip exactly at both the storage precision
(4 decimals of a percent) and the display precision (2) D-044 ruling 6 sets.
*Fails if:* `10` is stored or applied as `10` rather than `0.10`, or `12.75%` loses precision on the
round trip.

**TC-2-039 · every باب carries its own markup, and no child inherits one**
`AC-204-C` · P1 · Domain + Api · §4.2 · §2
Given a parent باب at one arbitrary rate and a child باب at a different arbitrary rate, when an item in
the child باب starts a new BOQ line, then the line's markup defaults to the child's own rate — the
parent's rate reaches nothing.
*Fails if:* the line defaults to the parent's rate, or to any value other than the child's own.

**TC-2-040 · a markup change does not move any existing reference to it**
`AC-204-D` · P1 · Api, real PostgreSQL · §4.4
Given a باب whose markup is changed, when every table this codebase persists is inspected for a row
written by the markup-change handler, then the only row written is the باب's own — no other table
gains, loses or changes a row as a side effect of the markup change.
*Fails if:* the handler writes to any table beyond the `Bab` row itself. **Note:** a full end-to-end
proof that a *signed BOQ line* or an *open estimate line* keeps its old markup needs those entities,
which do not exist before slice 4; this case proves the slice-2 half — the mechanism propagates to
nothing — and `AC-204-D`'s BOQ-line half is re-driven when `S-054` ships.

**TC-2-041 · a باب cannot be its own ancestor, at any depth**
`AC-204-E` · P1 · Domain · D-127 §1
Given باب A and باب B, when A is given B as its parent and then B is given A as its parent, then the
second is refused with `errors.master.bab_cannot_be_its_own_ancestor`; and given the longer chain
A → B → C, closing it as C → A is refused the same way, at the closing edge.
*Fails if:* either the two-step or the three-step cycle is accepted, or the tree loses its root
afterward.

**TC-2-042 · a repeated باب code is refused**
`AC-204-F` · P2 · Api, real PostgreSQL · slice 0
Given a باب with code `CONC`, when a second باب is submitted with code `CONC`, and again as `conc`,
then both are refused.
*Fails if:* either succeeds.

**TC-2-043 · a role without `BabManage` reaches nothing**
`AC-204-G` · P1 · Api · §2 · D-129 §1
Given a signed-in user of each of Finance, SiteEngineer, HeadOfDesign, MarketingSales, Client,
Subcontractor and Hr, when each calls the create endpoint and then the edit endpoint directly, then
every call is refused `403`, and no باب is created or changed.
*Fails if:* any of the seven succeeds at either endpoint.

**TC-2-044 · a markup change is audited before and after**
`AC-204-H` · P1 · Api, real PostgreSQL · CLAUDE.md
Given a باب whose markup moves from one arbitrary rate to another, when the audit trail is read, then
a record names the actor, the time, and both the old and the new rate.
*Fails if:* either rate is missing from the record.

**TC-2-045 · Arabic, RTL, indentation from the inline-start**
`AC-204-I` · P3 · E2E · CLAUDE.md · `ux/rtl-and-i18n.md`
Given `S-021` at 390px in Arabic, when the tree renders, then direction is RTL, each level indents from
the inline-start (not the left), percentages are bidi-isolated, and the page body does not scroll
horizontally.
*Fails if:* indentation is a hardcoded `margin-left` rather than a logical property — the failure this
case exists to catch is invisible in LTR and only shows up once the direction flips.

**TC-2-046 · an empty tree says so**
`AC-204-J` · P3 · E2E · `ux/components.md` §9
Given no باب exists, when `S-021` renders, then `bab.tree.empty` is displayed as an explicit empty
state with the create action.
*Fails if:* the screen renders a blank area or a placeholder row instead.

**Held open — the tree's data, not its behaviour.** No case above seeds or asserts Kaff's real trades
or their real markups, and none is written to. `Q75` (D-130 §8) is data only Karim holds; a demo or a
seed script populated with `spec.md` §4.2's own 15%/30% examples would be exactly the "plausible
fiction mistaken for a decision" D-130 §8 warns against, and this file does not produce one.

---

# KAFF-205 · Re-parent a باب, and move an item between أبواب — masters ready for QA

**TC-2-047 · a باب moves, and its children move with it**
`AC-205-A` · P1 · Api · §2
Given باب A with children B and C, and a second root D, when A is re-parented to D, then A is a child
of D, B and C are still children of A, and every one of the four is still reachable from a root.
*Fails if:* B or C is orphaned, or re-parented to D directly instead of staying under A.

**TC-2-048 · a باب becomes a root**
`AC-205-B` · P2 · Api · §2
Given a child باب, when its parent is cleared, then it is a root and its own children are unchanged.
*Fails if:* its children are also detached, or it is not reachable at the top level.

**TC-2-049 · a باب cannot become its own ancestor, at any depth, and the refusal names it**
`AC-205-C` · P1 · Domain + Api · D-127 §1 · D-128
Given the chain A → B → C (C's parent is B, B's parent is A), when A is re-parented to C, then it is
refused with `errors.master.bab_cannot_be_its_own_ancestor`; and given the two-step case (B's parent
set to A, then A's parent set to B) and the self-parent case (A's parent set to A), both are refused
with that same key; and after every refusal every باب is still reachable from a root, and the key
resolves to a sentence in both `en.json` and `ar.json`.
*Fails if:* any of the three depths is accepted, or the key is anything other than
`errors.master.bab_cannot_be_its_own_ancestor` — including the retired
`errors.master.bab_cannot_be_its_own_parent`, which SM-33 requires renamed rather than left beside it.

**TC-2-050 · an item moves between أبواب**
`AC-205-D` · P1 · Api · §4.1 · §2
Given an item in باب A, when it is moved to باب B, then the item's باب is B, and it appears under B and
no longer under A in the catalogue list.
*Fails if:* it appears under both, or under neither.

**TC-2-051 · a move does not touch a signed BOQ**
`AC-205-E` · P1 · Api, real PostgreSQL · §4.4
Given a signed BOQ line for an item created while that item was in a باب at one rate, when the item is
moved to a باب at a different rate, and separately when its old باب is re-parented, then every value on
the signed line is unchanged, including its markup, and no field on the line holds a foreign key to the
catalogue row or to either باب.
*Fails if:* the line's markup or any other value changes under either move.

**TC-2-052 · a move re-prices no open estimate and raises no alert**
`AC-205-F` · P2 · Api · §4.4
Given open, unsigned estimate lines for the item, when the item is moved to a باب with a different
markup, then no estimate line changes and no alert is raised.
*Fails if:* an estimate line changes, or an alert is raised.

**TC-2-053 · the new default reaches only the next line**
`AC-205-G` · P1 · Api, real PostgreSQL · §4.2
Given an item moved from a باب at one rate to a باب at a different rate, when a **new** BOQ line is
started from that item, then it defaults to the new باب's rate; a line created before the move keeps
its own. **Note:** as with `TC-2-040`, the "line created before the move keeps its rate" half is fully
provable only once `S-054` (slice 4) exists; this case proves the new-default half now and the
mechanism writes nothing to any pre-existing row (allow-list, as `TC-2-040`).
*Fails if:* the new line defaults to the old باب's rate.

**TC-2-054 · a role without the permission moves nothing**
`AC-205-H` · P1 · Api · §2 · D-129 §1
Given a signed-in user of each of Finance, SiteEngineer, HeadOfDesign, MarketingSales, Client,
Subcontractor and Hr — holding neither `BabManage` nor `CatalogueManage` — when each calls the باب-move
endpoint and then the item-move endpoint directly, then every call is refused `403`, and no باب and no
item has moved.
*Fails if:* any of the seven succeeds at either endpoint.

**TC-2-055 · both moves are audited before and after**
`AC-205-I` · P1 · Api, real PostgreSQL · CLAUDE.md
Given a باب re-parented and an item moved, when the audit trail is read, then each has a record naming
the actor, the time, and the old and new value of what moved.
*Fails if:* either record omits the old or the new value.

**TC-2-056 · Arabic, RTL, at mobile width**
`AC-205-J` · P3 · E2E · CLAUDE.md
Given the move surfaces at 390px in Arabic, when they render, then direction is RTL, the tree indents
from the inline-start, and the page body does not scroll horizontally.
*Fails if:* the body scrolls horizontally.

---

# KAFF-206 · Archive a catalogue item — masters ready for QA

**TC-2-057 · an item is archived and is still there**
`AC-206-A` · P1 · Api · §4.1
Given an active catalogue item, when it is archived, then its status is `Archived`, the row still
exists with every §4.1 field unchanged, and it is reachable through the list's explicit archived
filter.
*Fails if:* the row is removed, or a field other than `status` changes.

**TC-2-058 · there is no delete path**
`AC-206-B` · P1 · Api · CLAUDE.md · KAFF-123
Given the catalogue endpoints the API host actually maps, when the mapped routes are enumerated as an
allow-list, then no route deletes a catalogue item, under any verb.
*Fails if:* a delete route exists under any verb or path.
**Positive control (D-116):** `TC-2-057` shows the *archive* route **does** exist and does change
`status` — proving the allow-list enumeration in this case is exercised against a real, populated route
table, not against an empty one that would pass regardless of what exists.

**TC-2-059 · archiving does not touch a signed BOQ**
`AC-206-C` · P1 · Api, real PostgreSQL · §4.4
Given a signed BOQ line carrying an item's values at signature time, when that item is archived, then
every value on the signed line is unchanged and the line still renders.
*Fails if:* any value on the signed line changes, or the line fails to render.

**TC-2-060 · archiving does not touch an open estimate and raises no alert**
`AC-206-D` · P2 · Api · §4.4
Given open, unsigned estimate lines carrying the item, when the item is archived, then no estimate
line changes and no alert is raised.
*Fails if:* an estimate line changes, or an alert is raised.

**TC-2-061 · archiving twice is refused**
`AC-206-E` · P1 · Api, real PostgreSQL · slice 0
Given an already-archived item, when it is archived again, then it is refused with
`errors.master.already_archived`, and no audit record is written for the refusal.
*Fails if:* the second archive succeeds silently, or writes a record.

**TC-2-062 · an archived item is absent from a new BOQ line's search**
`AC-206-F` · P1 · Api · D-130 §3
Given an archived item, when the catalogue search used by the BOQ builder's "add item" is called, then
the archived item does not appear among the results.
*Fails if:* the archived item still appears, with or without a warning badge.
**Positive control (D-116):** `TC-2-028`/`TC-2-036` show that same item, while active, **is** returned
by the identical search — proving this case would notice an archived item that leaked through, rather
than passing because the search never returns anything.

**TC-2-063 · a role without `CatalogueManage` archives nothing**
`AC-206-G` · P1 · Api · §2 · D-129 §1
Given a signed-in user of each of Finance, SiteEngineer, HeadOfDesign, MarketingSales, Client,
Subcontractor and Hr, when each calls the archive endpoint directly, then every call is refused `403`,
and no item's status has changed.
*Fails if:* any of the seven succeeds.

**TC-2-064 · archiving is audited before and after; a refusal writes nothing**
`AC-206-H` · P1 · Api, real PostgreSQL · CLAUDE.md
Given an archived item, when the audit trail is read, then a record names the actor, the time, and the
old and new status; and the refused second archive from `TC-2-061` produced no record at all.
*Fails if:* the refusal writes a record, or the successful archive's record omits either status.

**TC-2-065 · Arabic, RTL, at mobile width**
`AC-206-I` · P3 · E2E · CLAUDE.md
Given `S-017` and its archive control at 390px in Arabic, when they render, then direction is RTL, the
archived badge and the filter are readable, and the page body does not scroll horizontally.
*Fails if:* the body scrolls horizontally.

**Coverage gap, not a hole in this file — un-archiving.** `Q66` (D-130 §4) rules that an archived
catalogue item **can** be un-archived, and `MasterDataErrors.NotArchived` already exists as the shape
of that refusal-when-not-archived [Verified: 2026-09-09 @
`src/Domain/MasterData/MasterDataErrors.cs` -> `NotArchived`]. **No story owns building the endpoint** —
`KAFF-206`'s own *Not in this story* section leaves the placement to the Scrum Master. No case is
written here for un-archiving an item, because there is no story and no `AC-` to trace it to; writing
one now would be inventing the criterion this file exists to avoid inventing. **NO STORY**, the
`qa/slice-1` convention for "the ruling exists, the story does not."

---

# KAFF-207 · Employee register — masters ready for QA

**TC-2-066 · a salaried employee is created**
`AC-207-A` · P1 · Api · §2 · §10
Given HR, and separately the Owner (`Q12`, D-129 §1), on `S-024`, when a code, full name, phone and a
kind of salaried are submitted, then the record exists carrying those values, its kind is `Salaried`,
and it is active.
*Fails if:* the record is not created, or its kind is anything other than `Salaried`.

**TC-2-067 · day labour without a باب is refused, in the entity and in the database**
`AC-207-B` · P1 · Domain + Api, real PostgreSQL (raw SQL) · §10
Given a create request with a kind of day labour and no باب, when it is submitted through the handler,
then it is refused with `errors.master.day_labour_requires_trade`; and given the same row inserted by
raw SQL that bypasses the entity entirely, the database's check constraint refuses the insert too.
*Fails if:* either path accepts a day-labour row with no باب — the entity guard alone is not this
rule's enforcement; `spec.md`'s requirement is checked at the table, per `qa/strategy.md` §3.

**TC-2-068 · two people cannot share an employee code**
`AC-207-C` · P1 · Api, real PostgreSQL · slice 0
Given an employee with code `E-100`, when a second is submitted with `E-100`, again with `e-100`, and
again as two requests arriving simultaneously with `E-100`, then all three are refused.
*Fails if:* a concurrent pair both succeed.

**TC-2-069 · one person, one record — the mechanism is named**
`AC-207-D` · P1 · Api, real PostgreSQL · §2
Given an existing employee, when the same person (same normalised phone) is submitted a second time,
then a second row is not created, and the refusal is traced to the unique index on the normalised
phone.
*Fails if:* a second row is created. **Caveat carried from the story itself:** this mechanism is `Q70`
and may not survive it — if `Q70` is ruled toward a warning, this case's expected result reverses and
must be re-read the day that ruling lands; it is not silently re-asserted as still correct.

**TC-2-070 · the register stores no pay figure**
`AC-207-E` · P1 · Domain · §10 · D-055 §2 · CLAUDE.md
Given the `Employee` entity's stored properties and the create/edit endpoints' request and response
bodies, when each is enumerated as an allow-list, then no salary, day rate, wage or other money-typed
member appears among them.
*Fails if:* any money-typed member is added to the stored properties or either contract.

**TC-2-071 · an employee is archived, not deleted**
`AC-207-F` · P1 · Api · D-049 ruling 5
Given an active employee who has left, when they are archived, then the row still exists with every
field intact, is excluded from the default list and findable through the explicit filter; and when the
mapped routes are enumerated as an allow-list, none deletes an employee under any verb.
*Fails if:* the row is removed, or a delete route exists.

**TC-2-072 · the employee code is generated, and no field is added beyond what exists**
`AC-207-G` · P1 · Api · D-130 §6, §8
Given the create form, when a salaried employee is created, then its reference number is generated by
the system, never typed and never editable; and the form renders exactly `Employee`'s existing fields
(§10's four worker fields plus `NationalId`, `JobTitle`, `HiredOn`) with none added on top.
*Fails if:* the code is caller-supplied, or a field beyond the ones named is rendered or stored.
**Expected to fail on first run.** `Employee.Create` takes a caller-supplied code today
[Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `Create`]; the story itself records that
switching it to a generated sequence is owed by Backend and has not shipped. This case is written
against the ruling (D-130 §6), not against the code as it stands, per `qa/README.md`'s instruction that
the Verifier records a failing case and why, rather than rewriting it to pass.

**TC-2-073 · a role without `EmployeeManage` reaches nothing**
`AC-207-H` · P1 · Api · §2 · §10 · D-129 §1
Given a signed-in user of each of Finance, TechnicalOffice, SiteEngineer, HeadOfDesign, MarketingSales,
Client and Subcontractor — explicitly including the Site Engineer and the Technical Office — when each
calls the create, edit and archive endpoints directly, then every call is refused `403`, and no record
is created or changed by any of them.
*Fails if:* any of the seven succeeds at any of the three endpoints.

**TC-2-074 · every change is audited before and after; a refusal writes nothing**
`AC-207-I` · P1 · Api, real PostgreSQL · CLAUDE.md
Given an employee whose name and specialty are both edited in one request, when the audit trail is
read, then one record names the actor, the time, and both fields with their old and new values; and a
refused edit (from `TC-2-073`) writes no record and changes nothing.
*Fails if:* the refused edit writes a record, or the successful edit's record omits either field.

**TC-2-075 · Arabic, RTL, at mobile width**
`AC-207-J` · P3 · E2E · CLAUDE.md
Given `S-023` and `S-024` at 390px in Arabic, when they render, then direction is RTL, names and phone
numbers are bidi-isolated inside Arabic text, and the page body does not scroll horizontally.
*Fails if:* the body scrolls horizontally, or a phone number inside an Arabic row renders unisolated.

---

# KAFF-208 · Nobody appears in both populations — masters ready for QA

**TC-2-076 · every costed person is in exactly one population**
`AC-208-A` · P1 · Domain · §10
Given the `Employee` store seeded with records of both kinds, when every record is read, then each
carries exactly one `Kind`, that kind is a defined enum member — not the zero value, not a string the
binder produced — and no record carries both or neither.
*Fails if:* any record's `Kind` is the enum's default/zero value rather than an explicitly set member.

**TC-2-077 · one person cannot be created into both populations**
`AC-208-B` · P1 · Api, real PostgreSQL · §10
Given a registered day labourer, when the same person (same normalised phone) is submitted again as
salaried staff, then the second create is refused, and the refusal is traced to the unique index on
the normalised phone.
*Fails if:* the second create succeeds. **Same `Q70` caveat as `TC-2-069`**: this mechanism may not
survive `Q70`'s ruling, and `KAFF-208` itself records that a warning-reading of `Q70` removes this
case's only enforcement, at which point this story needs a different one.

**TC-2-078 · a population cannot be edited from one to the other**
`AC-208-C` · P1 · Domain + Api · §10
Given an existing employee of either kind, when an edit request carries a different `kind`, then the
population does not change and the refusal `errors.master.employee_kind_immutable`
(`EmployeeKindIsImmutable`) is returned.
⛔ **Unexecutable as written, today — a finding, the `AC-125-C` shape.** There is **no employee edit
endpoint of any kind** yet [Verified: 2026-09-09 — `src/Api/Features/` holds no HR/Employee folder],
so there is no request to submit a different `kind` to. `MasterDataErrors.EmployeeKindIsImmutable` is
declared and translated but returned by nothing [Verified: 2026-09-09 @
`src/Domain/MasterData/MasterDataErrors.cs` -> `EmployeeKindIsImmutable`], exactly as `KAFF-208`'s own
Definition-of-Ready row records. This case is written against the target behaviour and is **not run**
until an edit endpoint exists to call — it is not a passing case in the meantime, and reporting it as
one would be the D-046 shape (asserting on a value that cannot yet be produced). **Held**, not deleted.

**TC-2-079 · immutability is not merely an absent setter**
`AC-208-D` · P1 · Domain · CLAUDE.md
Given the `Employee` entity, when its public members are enumerated as a named allow-list, then no
public member sets `Kind` after construction.
*Fails if:* a `SetKind` or equivalent public setter exists and is not on the allow-list — the
allow-list is written out by name so that adding one is a deliberate edit to this test.

**TC-2-080 · a day labourer joining the payroll is archived and re-registered, never moved**
`AC-208-E` · P1 · Api, real PostgreSQL · D-130 §7
Given a day labourer with an engagement history, when HR records that he has gone onto the payroll as
salaried staff, then his day-labour record is archived — unchanged, still carrying every engagement it
earned — and a **new** employee record is created for him as salaried staff; and reading the full
history of both records afterward shows no single record ever carried both `Kind`s at any point in
time, and no existing engagement's owning record was edited.
*Fails if:* the day-labour record's `Kind` is changed in place (a move), or its engagement history is
migrated onto the new record rather than staying with the one that earned it — either would make the
invariant false at some point in the past, which is exactly what D-130 §7 forced this shape to prevent.
This is the case the brief calls for asserting the invariant **over history**, not only at the moment
of the change.

**TC-2-081 · a role without `EmployeeManage` cannot reach either population**
`AC-208-F` · P1 · Api · §2 · §10 · D-129 §1
Given a signed-in user of each of Finance, TechnicalOffice, SiteEngineer, HeadOfDesign, MarketingSales,
Client and Subcontractor, when each calls the employee endpoints directly, then every call is refused
`403`.
*Fails if:* any of the seven succeeds.

**TC-2-082 · Arabic, RTL, يومية verbatim**
`AC-208-G` · P3 · E2E · §14
Given `S-023`, `S-024` and `S-025` at 390px in Arabic, when the population is displayed and chosen,
then it reads **يومية** for day labour, verbatim from the catalogue, and direction is RTL.
*Fails if:* the term is translated to a synonym (*casual*, *temporary*, *labourer*) rather than the
catalogue's own key.

---

# KAFF-213 · Archive a باب — masters ready for QA

**TC-2-083 · a باب with no active items is archived**
`AC-213-A` · P1 · Api · §2
Given a باب with no catalogue items, or only archived ones, when it is archived, then its `IsActive` is
`false`, the row still exists, and it is reachable through the tree's explicit archived filter.
*Fails if:* the row is removed, or archiving is refused with no active items present.

**TC-2-084 · a باب holding active items cannot be archived, and the refusal names the count**
`AC-213-B` · P1 · Api, real PostgreSQL · D-130 §5
Given a باب with 12 active catalogue items filed directly under it, when it is archived, then the
archive is refused with `errors.master.bab_has_active_items`, the refusal names **12**, and the باب's
`IsActive` remains `true`.
*Fails if:* the archive succeeds, or the refusal does not name the count, or names the wrong count.

**TC-2-085 · the count is the باب's own items, not its subtree's**
`AC-213-C` · P1 · Api, real PostgreSQL · D-130 §5
Given a parent باب with no items of its own and a child باب with 3 active items, when the parent is
archived, then the parent archives successfully and the child باب and its 3 items are unaffected.
*Fails if:* the parent's archive is refused on the child's item count, or the child باب's status
changes.

**TC-2-086 · archiving a باب is not a cascade**
`AC-213-D` · P1 · Api, real PostgreSQL · D-130 §5
Given a باب with no active items of its own, a child باب, and an archived item filed directly under the
parent, when the parent باب is archived, then no catalogue item's status changes, no child باب's status
changes, no child باب is re-parented, and the mapped routes this story adds — enumerated as an
allow-list — touch no `CatalogueItem` row and no other `Bab` row.
*Fails if:* any item or child باب changes as a side effect, or a route beyond the باب's own row is
reachable from this story's endpoint.

**TC-2-087 · moving or archiving the items first clears the refusal** *(cross-story: KAFF-213 + KAFF-205/206)*
`AC-213-E` · P1 · Api, real PostgreSQL · D-130 §5
Given a باب refused for archiving under `TC-2-084`, when its 12 active items are archived (`KAFF-206`)
in one sub-case and moved to another باب (`KAFF-205`) in a second sub-case until none remain active
under it, then the باب can now be archived successfully in both sub-cases.
*Fails if:* the refusal persists after the active-item count reaches zero by either route.

**TC-2-088 · an archived باب's already-archived items stay findable**
`AC-213-F` · P2 · Api · §4.4 · D-130 §3
Given a باب archived while it held items that were themselves already archived, when the catalogue's
explicit archived filter is used, then those items still appear, still carrying their باب, and no field
on any signed BOQ line referencing them has changed.
*Fails if:* the items disappear, or a signed BOQ line's value changes.

**TC-2-089 · archiving twice is refused**
`AC-213-G` · P1 · Api, real PostgreSQL · slice 0
Given an already-archived باب, when it is archived again, then it is refused with
`errors.master.already_archived`, and no audit record is written for the refusal.
*Fails if:* the second archive succeeds, or writes a record.

**TC-2-090 · a role without `BabManage` archives nothing**
`AC-213-H` · P1 · Api · §2 · D-129 §1
Given a signed-in user of each of Finance, SiteEngineer, HeadOfDesign, MarketingSales, Client,
Subcontractor and Hr, when each calls the archive endpoint directly, then every call is refused `403`,
and no باب's status has changed.
*Fails if:* any of the seven succeeds.

**TC-2-091 · archiving is audited before and after; refusals write nothing**
`AC-213-I` · P1 · Api, real PostgreSQL · CLAUDE.md
Given an archived باب, when the audit trail is read, then a record names the actor, the time, and the
old and new status; and neither the refused archive of `TC-2-084` nor the refused re-archive of
`TC-2-089` produced any record.
*Fails if:* either refusal writes a record, or the successful archive's record omits either status.

**TC-2-092 · Arabic, RTL, at mobile width, count bidi-isolated**
`AC-213-J` · P3 · E2E · CLAUDE.md
Given `S-021` and its archive control at 390px in Arabic, when they render, then direction is RTL, the
archived badge and the refusal's item count are readable, and the count is bidi-isolated.
*Fails if:* the numeral inside the Arabic refusal sentence renders unisolated or reordered.

---

# Held open — not Ready, no case written

Per the brief: a case driven against a question nobody has answered would report a coverage that does
not exist. Each story below gets the question that blocks it and nothing else.

**KAFF-209 · Register a worker from site.** Held on **`Q70`** (is a repeated worker phone a refusal or
a warning) and **`Q71`** (which role, if any, may call the registration endpoint — today none does).
`Q71` alone makes every permission case for this story unwritable: there is no actor to assert *can*
reach the endpoint, and asserting only who *cannot* would certify a gap as a feature. The story's own
`AC-209-D` and `AC-209-E` are already written and held for the same reason, inside the story; this file
adds no case beyond them.

**KAFF-210 · Worker engagement history.** Held on **`Q72`** (what one engagement is — a day, a stretch
of work, or a project — and what a rating is out of). Every figure this story renders (average day
rate, frequency, rating) is defined in terms of "engagement," and no unit means no case can assert a
specific number without inventing the unit. The story's own `AC-210-F` is already written and held for
the same reason.

**KAFF-211 · Subcontractor master with rates.** Held on **`Q29`** (whether the withholding rate belongs
to the firm or the job — named in `stories/backlog.md` as a slice-2 blocker before this story was
written), **`Q73`** (whether the master carries a rate card at all, or rates live only on the job's
sub-BOQ), and **`Q70`**'s third population (repeated subcontractor phone). `AC-211-G`, `AC-211-H` and
`AC-211-I` are already written and held in the story for these three.

**KAFF-212 · Supplier master.** Held on **`Q29`** and **`Q70`**'s fourth population (repeated supplier
phone). `AC-212-F` and `AC-212-G` are already written and held in the story.

---

# Findings, stated here rather than filed to `qa/questions.md`

This QA session did not edit `qa/questions.md`, `decisions.md`, any story file or any trailer — ticking
a Definition-of-Ready box is the Scrum Master's, after this file lands. Two things found while writing
cases are recorded here for that handoff:

1. **`AC-200-B`'s "more than four decimals" clause names two legal behaviours and no rule choosing
   between them.** See the note under `TC-2-002`. Not cased; needs a ruling (BA or Nabil, not Karim —
   it is a storage/parse policy, not a business fact).
2. **`AC-208-C` cannot be executed against the code as it stands, because no employee-edit endpoint
   exists to send the request to** — the same shape as `AC-125-C`. See the note under `TC-2-078`. The
   case is written and held, not dropped, so the day an edit endpoint ships this file already has its
   test.

No contradiction was found between `D-129`/`D-130` and any of the ten stories cased here beyond what
the stories themselves already flag (the `AC-200-B` ambiguity, `AC-207-G`'s expected-red state pending
Backend's generated-code switch, and `AC-208-C`'s missing endpoint) — all three were found by the
stories' own Definition-of-Ready sections and are confirmed here, not newly discovered.
