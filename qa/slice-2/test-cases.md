# Slice 2 — test cases

**Range allocated: `TC-2-001` … `TC-2-153`, plus `TC-2-000` as the non-case format template.**
⛔ **Extended again, 2026-09-12 — `TC-2-104` … `TC-2-153`, for `AC-204-K` and for `KAFF-209`/`210`/`211`/`212`,
whose blocking questions (`Q29`, `Q70`–`Q73`) are now ruled — D-139, D-140, D-141, D-145 §1, D-146,
D-147. `093`…`098` stays an unused gap inside the old range, unrelated to this extension.**
Checked against `qa/slice-1/test-cases.md` before writing a single case: slice 1's identifiers run
`TC-1-000` … `TC-1-306` [Verified: 2026-09-09 @ `qa/slice-1/test-cases.md` -> `TC-1-306`, the highest
id in that file]. `qa/README.md`'s own ID scheme is `TC-<slice>-<nnn>` — the slice number is part of
the identifier, not a suffix on a shared counter — so `TC-2-nnn` cannot collide with `TC-1-nnn` under
the scheme as written; a `grep -r "TC-2-" qa/ stories/ decisions.md process/` run before this file
existed returned nothing [Verified: 2026-09-09]. **The range is taken anyway, and stated, because the
brief asked for it named and because a second slice-2 QA session should not have to re-derive that the
prefix itself is the collision guard.**

⛔ **Extended 2026-09-10 to `TC-2-001` … `TC-2-103`, for `KAFF-214`.** `KAFF-214` did not exist when the
range above was allocated — it is the retrospective story `D-133 §1` cut from `V-35-U`, after
`UnarchiveCatalogueItem` shipped with no `AC-` id inside `f675f1b`/`934bfb9`. `099`…`103` were unused in
this file and in `qa/slice-1/test-cases.md`'s own range, so no collision.

Covers the nine stories named Ready-but-for-QA in this brief — `KAFF-200`, `201`, `202`, `203`, `205`,
`206`, `207`, `208`, `213` — plus `KAFF-204`, cased in full including its data (`AC-204-K`, D-145 §1) —
plus **`KAFF-214`**, added 2026-09-10 — plus, added 2026-09-12 now that `Q29`/`Q70`–`Q73` are ruled,
**`KAFF-209`, `210`, `211`, `212`** in full. Two criteria stay HELD with a `Q`-id rather than cased:
`AC-208-B` (`Q80`, narrowed to active records) and `AC-211-O` (no `S`-number yet for Finance's screen).

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
| KAFF-202 create/edit an item | TC-2-017…027 | `TC-2-022`/`023` (`AC-202-E`/`F`) held to slice 4 — no BOQ/estimate entity exists (`V-35-N`, 2026-09-10) |
| KAFF-203 find an item | TC-2-028…036 | `TC-2-036` rewritten 2026-09-10 so باب-order and code-order predict different sequences (`V-35-R`) — not held, just no longer confounded. `TC-2-035` (`AC-203-H`, E2E) cannot run on a seeded stack — no rows (D-133 §4) |
| KAFF-204 باب tree + markup | TC-2-037…046, **104** | data (Kaff's real trades/rates) — was `Q75`, ✅ **now cased, `TC-2-104`, D-145 §1.** `TC-2-039`'s (`AC-204-C`) BOQ-line half also held to slice 4, the `TC-2-040` shape (`V-35-N`, 2026-09-10) |
| KAFF-205 re-parent / move item | TC-2-047…056 | `TC-2-051`/`052` (`AC-205-E`/`F`) held to slice 4 — no BOQ/estimate entity exists (`V-35-N`, 2026-09-11) |
| KAFF-206 archive an item | TC-2-057…065 | `TC-2-059`/`060` (`AC-206-C`/`D`) held to slice 4. `TC-2-062` re-scoped 2026-09-10 to `AC-206-A`; `AC-206-F`'s own guarantee held to slice 4, mechanism the Architect's (`V-35-O`). `TC-2-065` (`AC-206-I`, E2E) cannot run on a seeded stack — no rows (D-133 §4). Un-archive → **`KAFF-214`** |
| KAFF-207 employee register | TC-2-066…075 | `TC-2-068` **retired and rewritten 2026-09-12** (`V-37-E`): `CreateEmployee.Request` carries no `Code` member, so the old typed-duplicate scenario cannot execute — recast around the generated code's uniqueness |
| KAFF-208 nobody in both populations | TC-2-076…082 | `AC-208-C` — no edit endpoint exists to call. `TC-2-077` (`AC-208-B`) **now HELD**, narrowed to an active-record cross-population match (`Q80`, D-146 §4(b)). `TC-2-080` (`AC-208-E`) **rewritten** — released by D-146 §4(a), warn-and-acknowledge |
| KAFF-213 archive a باب | TC-2-083…092 | — |
| **KAFF-214 unarchive an item** | **TC-2-099…103** | **new, 2026-09-10.** `TC-2-103` (`AC-214-E`, E2E) held — no seeded باب until `KAFF-204` ships (D-133 §4) |
| **KAFF-209 register a worker from site** | **TC-2-105…115** | **new, 2026-09-12** — `Q70`/`Q71` ruled (D-139 §§1–2, D-140, D-141) |
| **KAFF-210 worker engagement history** | **TC-2-116…126** | **new, 2026-09-12** — `Q72` ruled (D-139 §3, D-140). Rating's finer shape (`Q78`) and Site Engineer day-rate visibility (`Q76`) stay open but block no criterion here |
| **KAFF-211 subcontractor master** | **TC-2-127…141** | **new, 2026-09-12** — `Q29`/`Q73`/`Q70` ruled (D-139 §§1, 4–5, D-141, D-147). `TC-2-141` (`AC-211-O`) **HELD** — UX owes an `S`-number for Finance's tax-registration screen |
| **KAFF-212 supplier master** | **TC-2-142…153** | **new, 2026-09-12** — `Q29`/`Q70`/`Q13` ruled (D-139 §§1, 5–6, D-141) |

**Recounted 2026-09-12, after this pass's additions.** Before this pass: 93 live, 10 not-live, 103
written (2026-09-11 count, unchanged reasoning below). **This pass adds 51 new cases**, `TC-2-104`
through `TC-2-153`: `TC-2-104` (live, `AC-204-K`) plus the full `KAFF-209`/`210`/`211`/`212` sets
(`TC-2-105`…`153`, 50 cases). Of those 50, **49 are live** and **1 is not** — `TC-2-141` (`AC-211-O`),
HELD because UX has not named an `S`-number for Finance's tax-registration screen. **This pass also
moves one existing case from live to held**: `TC-2-077` (`AC-208-B`) is now explicitly HELD, narrowed
to the active-record cross-population phone match (`Q80`, D-146 §4(b)) — it was counted live in the
2026-09-11 recount below because the cross-population case had not yet been split into an archived
half (ruled) and an active half (still open). **Running total: 154 cases written (`TC-2-001`…`153`,
minus the pre-existing unused `093`…`098` gap = 148 defined), of which 93 − 1 + 50 = 142 are live and
10 + 1 + 1 = 12 are not.**

**93 live cases, recounted 2026-09-11 — subtracting 2 newly held for KAFF-205.** `TC-2-001`
… `TC-2-103` is **103** cases written. **10 cannot be run and are not live**, per this file's own
header — *"it is not a passing case"*: `TC-2-059`, `TC-2-060` (`AC-206-C`/`D`, pre-existing, no
BOQ/estimate entity), `TC-2-078` (`AC-208-C`, pre-existing, "Held, not deleted", no edit endpoint),
`TC-2-022`, `TC-2-023` (`AC-202-E`/`F`, newly held 2026-09-10, no BOQ/estimate entity, `V-35-N`), `TC-2-051`, `TC-2-052` (`AC-205-E`/`F`, newly held today, no BOQ/estimate entity, `V-35-N`), and, newly
dated today (D-133 §4, item 6) — **not `HELD Qnn`'s shape (no open business question), the same
"cannot produce a value to assert against" shape as the BOQ-absence cases above** — `TC-2-035`
(`AC-203-H`), `TC-2-065` (`AC-206-I`) and `TC-2-103` (`AC-214-E`), none of which can exercise its
assertion against a seeded stack that holds no catalogue row, because no endpoint creates a باب and
none is seeded. `103 − 10 = 93`. `TC-2-027` (`AC-202-J`) is **not** added to this list: it can render
and drive `S-018`'s empty create form today, only its edit half is blocked, so it counts live with that
caveat recorded inline. Roles cited throughout are the nine in `Role.cs`: `Owner`, `Finance`,
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
| `DayLabourSiteManage` (D-140), `ProjectScoped` | Owner, and any Site Engineer **assigned to the route's project** | Finance, TechnicalOffice, HeadOfDesign, MarketingSales, Client, Subcontractor, Hr, and an **unassigned** Site Engineer |
| `SubcontractorTaxRegistrationEdit` (D-147), `CompanyWide` | Owner, Finance | TechnicalOffice (including a holder of `SubcontractorManage`), SiteEngineer, HeadOfDesign, MarketingSales, Client, Subcontractor, Hr |

**`Q12` (D-129 §1) is why every "who may" case below reads Owner as a holder, not an exception.** The
Owner cases assert access; every refusal case belongs to the other roles named in the table above.

---

# KAFF-200 · Import the catalogue from Excel — masters ready for QA

**TC-2-001 · a clean file becomes a catalogue, both descriptions and Active — rewritten 2026-09-12 against D-136/D-144 §§3–5**
`AC-200-A` · P1 · Api, real PostgreSQL · §4.1 · D-144 §§3–5
Given a spreadsheet of N valid rows in the template's shape — columns `code, descriptionAr,
descriptionEn, unit, bab (by Code), costPrice, baseSellRate`, no `status` column — with every row's باب
already present by Code, and the caller in turn the Technical Office and separately the Owner (`Q12`,
D-129 §1), when each imports it, then N catalogue items exist, each carrying the code, **Arabic
description, English description**, unit, باب resolved by its Code, cost price and base sell rate, and
each is `Active`.
*Fails if:* fewer or more than N items result, either description is missing or copied into the wrong
language field, the باب is resolved by name instead of Code, or any item's status is anything other
than `Active`.

**TC-2-002 · a rate keeps its fourth decimal on import, and a longer one is stored through `Money`, not refused — rewritten 2026-09-12 against D-144 §6**
`AC-200-B` · P1 · Domain + Api, real PostgreSQL · CLAUDE.md · §4.1 · D-144 §6
Given a row whose base sell rate is `1234.5678` and whose cost price is `987.6543`, when the file is
imported and the two values are read back, then they are exactly `1234.5678` and `987.6543`, and
neither value has passed through a `float` or a `double` between the cell and the database.
And given a second row whose base sell rate carries five decimals in the sheet, when it is imported,
then the import is **not refused** for that reason, and the stored value is the rate rounded to four
decimals through `Money`, the same as any other caller — no import-specific refusal rule exists.
*Fails if:* either four-decimal value differs in the fourth decimal, the reader's double accessor is
used at any point in the parse path, or the five-decimal row is refused instead of stored at four
decimals.

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

**TC-2-009 · the file's shape must be the template's shape — rewritten 2026-09-12 against D-144 §§3–5, two description columns, no `status`**
`AC-200-I` · P2 · Api · D-129 §2 · D-144 §§3–5
Given a file whose columns are `code, descriptionAr, descriptionEn, unit, bab, costPrice, baseSellRate`
(the template — **no `status` column, and two description columns, not one**) tried once as-is, once
with an extra column, once with a column missing, and once with a single `description` column standing
in for the two, when each is imported, then the first is accepted and the other three are refused, each
refusal naming what the template requires and what the file carried instead; and the template download
is reachable from `S-019` before a file is ever chosen.
*Fails if:* a file with an extra or a missing column is silently accepted, a single-description file is
accepted by guessing a language, a `status` column is accepted and its values honoured, or columns are
mapped by position rather than by name.

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

⛔ **Unexecutable as written, today — held, outstanding to slice 4 (`V-35-N`).** There is **no `Boq`,
`SignedBoq`, `BoqLine` or `Estimate` entity anywhere in this codebase**
[Verified: 2026-09-10 — no such class under `src/`], so there is no signed line for this case to leave
untouched. **Held, not dropped**, the same `TC-2-059`/`TC-2-060` shape, so the day slice 4 ships those
entities this case is already written and waiting for them. **`tests/Api.Tests/EditCatalogueItemTests.cs`
-> `Repricing_touches_no_row_but_the_items_own_and_writes_no_estimate_or_boq_row` is not a witness for
this criterion** — its third assertion counts أبواب rows, an unrelated table that is the only other one
there is, and that count could not move under any implementation of `Reprice`. It proves a real,
different property (reprice touches nothing beyond its own row and one audit record); it does not prove
`AC-202-E`.

**TC-2-023 · re-pricing raises no alert and re-prices no estimate**
`AC-202-F` · P2 · Api · §4.4
Given open, unsigned estimates referencing the item, when the item is re-priced, then nothing on any
estimate changes and no alert is raised.
*Fails if:* an estimate line changes, or an alert is raised.

⛔ **Unexecutable as written, today — held, outstanding to slice 4 (`V-35-N`).** There is **no `Boq`,
`SignedBoq`, `BoqLine` or `Estimate` entity anywhere in this codebase**
[Verified: 2026-09-10 — no such class under `src/`], so there is no open estimate for this case to leave
untouched. **Held, not dropped**, the same `TC-2-059`/`TC-2-060` shape, so the day slice 4 ships those
entities this case is already written and waiting for them. **The same `EditCatalogueItemTests` test
named above is not a witness for this criterion either**, for the same reason.

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

⚠️ **Dated note, 2026-09-10 (D-133 §4) — what this case can and cannot prove on a seeded stack.** `S-018`
is the create/edit **form**, not a row of catalogue data — creating an item needs a `BabId`, and no
endpoint creates a باب and none is seeded [Verified: 2026-09-10 @
`tests/Api.Tests/DatabaseSeedingTests.cs` -> `A_freshly_initialised_database_seeds_no_babs`]. **This
case can render and drive the empty create form** — direction, i18n, no horizontal scroll — because
none of that needs a باب to exist. **It cannot drive the edit half** (an existing item's fields
populated and bidi-isolated), because a seeded stack holds no catalogue item to edit. Runnable in full
once `KAFF-204` ships a باب create endpoint.

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

⛔ **Dated note, 2026-09-10 (D-133 §4) — cannot run on a seeded stack.** This case needs at least one
catalogue row to render a populated table (the identity column, codes and figures, bidi-isolated). No
endpoint creates a باب and none is seeded [Verified: 2026-09-10 @
`tests/Api.Tests/DatabaseSeedingTests.cs` -> `A_freshly_initialised_database_seeds_no_babs`], and a
catalogue item cannot be created without a `BabId`, so a seeded stack holds no catalogue item for this
case to render. **Not written as an E2E test until `KAFF-204` ships a باب create endpoint.**
`scripts/seed-demo.ps1` is not the fix — seeding an invented باب is refused as data (`Q75`, D-130 §8).

**TC-2-036 · ordered by باب, then by code — rewritten 2026-09-10 so the rule and a plain code sort must disagree (`V-35-R`)**
`AC-203-I` · P2 · Api · D-129 §5
Given two أبواب whose `SortOrder` and item-code prefixes are **inverted on purpose**, so the two
hypotheses this case exists to tell apart predict different results: باب `Early` at `SortOrder: 10`
holding items coded `{nonce}-Z-1` and `{nonce}-Z-9`, and باب `Late` at `SortOrder: 20` holding items
coded `{nonce}-A-1` and `{nonce}-A-9`.
When the unfiltered list is requested
Then the result is `[{nonce}-Z-1, {nonce}-Z-9, {nonce}-A-1, {nonce}-A-9]` — باب `Early`'s items first
(its `SortOrder`, 10, precedes `Late`'s, 20), each باب's own items then ordered by code.
*Fails if:* the result is `[{nonce}-A-1, {nonce}-A-9, {nonce}-Z-1, {nonce}-Z-9]` — **ordering by item
code alone**, ignoring باب grouping entirely. That is the mutation this fixture is built to catch: with
`SortOrder` and code prefix inverted between the two أبواب, "grouped by باب's own order, then by code"
and "sorted by code alone across the whole list" now predict different sequences, unlike the prior
fixture (`babA`/`babZ` with `SortOrder` 10/20 and codes `A-*`/`Z-*`, where the two orderings coincided
and a mutation deleting `orderby bab.SortOrder,` entirely would not have reddened this case). The
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

**TC-2-039 · every باب carries its own required markup, and nothing reads a parent's — rewritten 2026-09-10, the `TC-2-040` shape (`V-35-N`)**
`AC-204-C` · P1 · Domain · §4.2 · §2
Given a parent باب created at one arbitrary rate and a child باب created under it at a different
arbitrary rate, when each باب's own `DefaultMarkup` is read back, and separately when the codebase is
searched for any query, handler or property that resolves a باب's markup by walking to an ancestor,
then the child's `DefaultMarkup` is its own value, distinct from the parent's and not derived from it,
`DefaultMarkup` is a required value on every باب (construction refuses a missing one), and no code path
anywhere reads a parent باب's markup for a child's item.
*Fails if:* the child's `DefaultMarkup` is ever absent, null, or equal to the parent's by inheritance
rather than by coincidence of the fixture, or a باب can be constructed with no markup of its own.

⛔ **`AC-204-C`'s own BOQ-line half is held, outstanding to slice 4 — the `TC-2-040` shape (`V-35-N`).**
The criterion's own *When* — *"an item in the child باب starts a new BOQ line"* — names a BOQ line, and
there is **no `Boq`, `BoqLine` or `Estimate` entity anywhere in this codebase**
[Verified: 2026-09-10 — no such class under `src/`], the same absence `AC-204-D`/`TC-2-040` already
holds explicitly. **This case proves what slice 2 can actually prove and no more:** each باب holds its
own required `DefaultMarkup`, with no inheritance from a parent [Verified: 2026-09-10 @
`src/Domain/MasterData/Bab.cs` -> `DefaultMarkup`] — nothing in this codebase reads a parent باب's
markup for anything, because nothing reads a parent's markup at all. No BOQ-line mechanism is invented
here to make the criterion pass early; the criterion's wording does not change, and the BOQ-line half
is re-driven when `S-054` ships, the same as `AC-204-D`'s.

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

✅ **`Q75` answered, D-145 §1 — the tree's data is now cased below**, replacing the held-open note this
section used to carry: no case here used to seed or assert Kaff's real trades, because the trades and
their markups were data only Karim held. They are named now.

**TC-2-104 · the seed is exactly Karim's eight trades, top-level, insert-if-code-absent, and never overwrites an edit**
`AC-204-K` · P1 · Api, real PostgreSQL · D-145 §1 · D-142
Given a fresh database, when `BabSeeder` runs, then exactly these eight أبواب exist as **root** أبواب
(no parent), one per row, no more and no fewer: `CON` أعمال خرسانة 15%, `MAS` أعمال مباني 15%, `PLU`
أعمال صحية 20%, `ELE` أعمال كهرباء 20%, `HVA` أعمال تكييف وتهوية 20%, `FIN` أعمال تشطيبات 30%, `CAR`
أعمال نجارة 25%, `MET` أعمال معدنية 25%.
And given the seeder runs a second time, when it completes, then it inserts nothing new and changes
nothing that already exists — insert-if-code-absent, never an overwrite.
And given one of the eight is edited by hand — a renamed markup, a different Arabic name — before the
seeder runs a third time, when it completes, then the hand-edited row is untouched.
And given a client has already created أبواب of its own by hand before the seeder ever runs, when it
runs, then the eight seeded أبواب are added beside them rather than the run being refused — `Q81`'s
open question, unaffected by this case.
*Fails if:* a ninth باب appears, any seeded code/name/markup differs from the table above, a second or
third run changes any existing row (seeded or hand-edited), or the seeder refuses to run because
أبواب already exist.

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

⛔ **Unexecutable as written, today — held, outstanding to slice 4 (`V-35-N`).** There is **no `Boq`, `BoqLine` or `Estimate` entity anywhere in this codebase** [Verified: 2026-09-11 — no such class under `src/`], so there is no signed BOQ line for this case to leave untouched. **Held, not dropped**, the same `TC-2-022`/`TC-2-023` shape, so the day slice 4 ships those entities this case is already written and waiting for them.

**TC-2-052 · a move re-prices no open estimate and raises no alert**
`AC-205-F` · P2 · Api · §4.4
Given open, unsigned estimate lines for the item, when the item is moved to a باب with a different
markup, then no estimate line changes and no alert is raised.
*Fails if:* an estimate line changes, or an alert is raised.

⛔ **Unexecutable as written, today — held, outstanding to slice 4 (`V-35-N`).** There is **no `Boq`, `BoqLine` or `Estimate` entity anywhere in this codebase** [Verified: 2026-09-11 — no such class under `src/`], so there is no open estimate for this case to leave untouched. **Held, not dropped**, the same `TC-2-022`/`TC-2-023` shape, so the day slice 4 ships those entities this case is already written and waiting for them.

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

⛔ **Unexecutable as written, today — a finding, the `AC-125-C` shape.** There is **no `Boq`, `SignedBoq`, `BoqLine` or `Estimate` entity anywhere in this codebase** [Verified: 2026-09-09 — no such class under `src/`], so there is no signed line and no open estimate to leave untouched. **Held, not dropped**, so the day slice 4 ships those entities this case is already written and waiting for them.

⚠️ **Passing it today would prove nothing**, which is why it is marked rather than run: `KAFF-206` rule 3's correct implementation is *to add nothing*, and a case that cannot fail cannot witness that.

**TC-2-060 · archiving does not touch an open estimate and raises no alert**
`AC-206-D` · P2 · Api · §4.4
Given open, unsigned estimate lines carrying the item, when the item is archived, then no estimate
line changes and no alert is raised.
*Fails if:* an estimate line changes, or an alert is raised.

⛔ **Unexecutable as written, today — a finding, the `AC-125-C` shape.** There is **no `Boq`, `SignedBoq`, `BoqLine` or `Estimate` entity anywhere in this codebase** [Verified: 2026-09-09 — no such class under `src/`], so there is no signed line and no open estimate to leave untouched. **Held, not dropped**, so the day slice 4 ships those entities this case is already written and waiting for them.

⚠️ **Passing it today would prove nothing**, which is why it is marked rather than run: `KAFF-206` rule 3's correct implementation is *to add nothing*, and a case that cannot fail cannot witness that.

**TC-2-061 · archiving twice is refused**
`AC-206-E` · P1 · Api, real PostgreSQL · slice 0
Given an already-archived item, when it is archived again, then it is refused with
`errors.master.already_archived`, and no audit record is written for the refusal.
*Fails if:* the second archive succeeds silently, or writes a record.

**TC-2-062 · the catalogue search's default excludes an archived item — re-scoped 2026-09-10 to what it actually proves (`V-35-O`)**
`AC-206-A` (re-attributed from `AC-206-F` — see the held note below) · P1 · Api · D-130 §3
Given an archived item, when the catalogue search is called with no explicit status filter, then the
archived item does not appear among the results, and it does appear when `status=Archived` or
`status=All` is passed explicitly.
*Fails if:* the archived item appears in the unfiltered default result, or an unknown `status` value is
silently defaulted rather than refused.
**Positive control (D-116):** `TC-2-028`/`TC-2-036` show that same item, while active, **is** returned
by the identical search — proving this case would notice an archived item that leaked through the
default, rather than passing because the search never returns anything.

⛔ **`AC-206-F`'s own guarantee is held, outstanding to slice 4 (`V-35-O`).** What this case proves is
`AC-206-A`'s guarantee — the list's **default** excludes archived items, and a caller who asks for
`status=all` gets them back. That is not `AC-206-F`: *"an archived item is absent from a **new BOQ
line's** search"* is a guarantee that the item cannot reach new work, and no BOQ line exists yet for
anything to be kept off of, and no caller-identity distinguishes "the list screen wants the archive"
from "a BOQ line wants to use an archived item." **A default the caller need not know to ask for is
also a default the caller can override without knowing it matters** — that is the whole distance
between resembling `AC-206-F` and discharging it. **Carried forward, in the words the Verifier left
it in:** the BOQ builder must not be able to put an archived item on a new line — *enforced by the
server, not by a picker default a caller can override with `status=all`* — and the mechanism is the
Architect's, ruled at slice-4 refinement. No case is written here for `AC-206-F` itself; writing one
against a search that cannot yet tell the two callers apart would be inventing the mechanism this file
exists to avoid inventing.

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

⛔ **Dated note, 2026-09-10 (D-133 §4) — cannot run on a seeded stack.** This case needs an existing,
archived catalogue row to show the archived badge against. No endpoint creates a باب and none is
seeded [Verified: 2026-09-10 @ `tests/Api.Tests/DatabaseSeedingTests.cs` ->
`A_freshly_initialised_database_seeds_no_babs`], and a catalogue item cannot be created without a
`BabId`, so a seeded stack holds no catalogue item to archive and no row for this case to render.
**Not written as an E2E test until `KAFF-204` ships a باب create endpoint.** `scripts/seed-demo.ps1` is
not the fix — seeding an invented باب is refused as data (`Q75`, D-130 §8).

**Coverage gap closed 2026-09-10 — un-archiving.** `Q66` (D-130 §4) rules that an archived catalogue
item **can** be un-archived. This section previously read "no story owns building the endpoint" and "no
case is written here … because there is no story" — both true when `AC-206-F` above was written, and
both stale now. **The endpoint shipped anyway** (`f675f1b`/`934bfb9`, `V-35-U`), the Scrum Master placed
it as its own story (D-133 §1), and it is cased in full below, in its own section — `TC-2-099`…`TC-2-103`
against `AC-214-A`…`AC-214-E`. See **`KAFF-214`**.

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

**TC-2-068 · the employee code is generated, unique, and a submitted code is ignored — retired and rewritten 2026-09-12 against `V-37-E`**
`AC-207-C` · P1 · Api, real PostgreSQL · D-130 §6
⛔ **Retired.** The case as it stood — *"a second is submitted with `E-100`, again with `e-100`"* —
described typing a duplicate code. `CreateEmployee.Request` carries no `Code` member
[Verified: 2026-09-12 @ `src/Domain/MasterData/Employee.cs` -> `Create`], so a code cannot be submitted
at all, and the old scenario cannot be executed. `V-37-E` found this stale during batch verification and
confirmed a body carrying `"code":"E-100"` is answered `201` with a server-generated code, the submitted
value ignored.
Given two create requests sent at the same instant, when both are handled, then each employee is given
a distinct, system-generated code — the same generated shape as `KAFF-119`'s client code (D-130 §6) —
and the guarantee that the two never collide is the database's unique index on the generated value, not
a read-then-write in the handler.
And given a create request whose body carries a `code` property regardless, when it is submitted, then
the property is ignored and the response's code is the one the server generated.
*Fails if:* two concurrent creates receive the same code, or a submitted `code` property reaches the
stored value. **Backend still owes one test that the index itself refuses a duplicate code inserted
below the API** — `V-37-E`'s own finding — which this case's first paragraph does not substitute for.

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

**TC-2-077 · HELD on the ACTIVE cross-population phone case — rewritten 2026-09-12, narrowed by D-146 §4(b), `Q80`**
`AC-208-B` · P1 · Api, real PostgreSQL · §10 · D-146 §4(b)
⛔ **HELD `Q80`.** Given an **active** registered day labourer, when the same person's phone is
submitted on a salaried create, or the reverse — an active salaried record's phone on a new day-labour
registration — then **held**: D-146 §4(a) has since derived and ruled the **archived** half of this
case as warn-and-acknowledge (`TC-2-080`, `AC-208-E`); only the active-to-active match stays open. D-146
§4(b) names this a business choice — refuse, or warn-and-acknowledge — that weighs §10's *"nobody
appears in both"* against D-139 §1's *"a shared phone is evidence, not proof"*, and rules that no agent
may choose between them. **Nothing here asserts either behaviour as correct until Nabil answers `Q80`.**
The skipped fact in the running code, `A_day_labourer_cannot_be_registered_again_as_salaried_with_the_same_phone`
[Verified: 2026-09-12 @ `tests/Api.Tests/CreateEmployeeTests.cs`], is D-146 test 9 — kept skipped with
its name and body, not deleted, until the ruling lands.
*Not a passing case.* When `Q80` is answered: **refuse** → this case asserts a `409` with the new error
key D-146 §4(b) names and the skipped test above is un-skipped; **warn** → this case asserts
warn-and-acknowledge exactly as `TC-2-080` does for the archived half, and the skipped test is rewritten
to that shape. Recased on `Q80`'s answer, not guessed now.

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

**TC-2-080 · a day labourer joining the payroll is archived and re-registered, never moved, and the same-phone salaried create warns and succeeds once acknowledged — rewritten 2026-09-12, released by D-146 §4(a)**
`AC-208-E` · P1 · Api, real PostgreSQL · D-130 §7 · D-146 §4(a)
Given a day labourer with an engagement history, when HR records that he has gone onto the payroll as
salaried staff **on the same phone number**, then his day-labour record is archived — unchanged, still
carrying every engagement it earned — and a **new** employee record is created for him as salaried
staff; and reading the full history of both records afterward shows no single record ever carried both
`Kind`s at any point in time, and no existing engagement's owning record was edited — `AC-208-C` still
refuses a direct edit of `Kind`, because this is a create-and-archive, never a move.
And, on the phone match itself — **released 2026-09-12 by D-146 §4(a)**, no longer HELD: given the
salaried create is submitted against the archived day labourer's phone with no acknowledgement, then it
is refused `409 errors.master.duplicate_phone_not_acknowledged`, naming the archived record; and with
`AcknowledgedDuplicatePhone: true` the save succeeds, and one `DuplicatePhoneAcknowledged` audit record
is written naming the archived record's real id.
And, the reverse direction of D-146 §4(a) — an archived salaried record, then a new day labourer on the
same phone — also warns and succeeds once acknowledged, per D-146 test 6,
`A_day_labourer_matching_an_archived_salaried_record_warns_and_succeeds_once_acknowledged`.
*Fails if:* the day-labour record's `Kind` is changed in place (a move), its engagement history is
migrated onto the new record rather than staying with the one that earned it, the salaried create
refuses outright rather than warning on the archived match, or no acknowledgement audit record is
written. This is the case the brief calls for asserting the invariant **over history**, not only at the
moment of the change. **Named test for the forward direction** — D-141's renamed
`Re_registering_the_same_person_by_phone_after_archiving_warns_and_succeeds_once_acknowledged`
[Verified: 2026-09-12 @ `tests/Api.Tests/EmployeeKindInvariantTests.cs`].

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

# KAFF-214 · Un-archive a catalogue item — cased 2026-09-10, retrospective to shipped code (`V-35-U`)

**Range extended.** This file's range was `TC-2-001` … `TC-2-098`; `KAFF-214` did not exist when that
range was allocated (it is a retrospective story, cut from `V-35-U` after commits `f675f1b` and
`934bfb9` shipped with no `AC-` id). **Extended today to `TC-2-001` … `TC-2-103`**, stated here and in
the header above, per `qa/README.md`'s `TC-<slice>-<nnn>` scheme — no collision, since `099`…`103` were
unused.

**TC-2-099 · un-archiving returns every §4.1 field unchanged, not only status and code**
`AC-214-A` · P1 · Api, real PostgreSQL · D-130 §4
Given an archived catalogue item carrying a code, Arabic description, unit, باب, cost price and base
sell rate, when it is un-archived, then its status is `Active`, and its code, description, unit, باب,
cost price and base sell rate are each exactly what they were before archiving.
*Fails if:* any one of those six §4.1 fields differs after un-archiving, or the assertion checks only
`status` and `code` — the shipped test's own scope today, per `KAFF-214`'s own story: *"No test asserts
the un-archived item's other §4.1 fields are unchanged — the existing round-trip test checks `Status`
and `Code` only."*

**TC-2-100 · un-archiving an item that is not archived is refused**
`AC-214-B` · P1 · Api, real PostgreSQL · D-130 §4
Given an active catalogue item, when it is un-archived, then it is refused with
`errors.master.not_archived`, and its status does not change.
*Fails if:* the refusal succeeds silently, or the item's status changes despite the refusal.

**TC-2-101 · a role without `CatalogueManage` cannot unarchive, and neither can a portal client — executed, not asserted in a comment**
`AC-214-C` · P1 · Api · §2 · D-129 §1 · D-130 §4
Given an archived item, and a signed-in user of each role that does not hold `CatalogueManage`,
**including a `Role.Client` portal session**, when each calls the un-archive endpoint directly, with no
browser involved, then every call — the `Client` call **driven as an actual request against the
endpoint**, not asserted in a code comment — is refused `403`, and the item's status does not change.
*Fails if:* any of the refused roles succeeds, or the `Client` refusal is only asserted in a comment
rather than executed — the `V-35-T` shape `KAFF-214`'s own story records: *"No test drives a portal
`Role.Client` against this endpoint … exercises `Finance` only, not `Client`."*

**TC-2-102 · un-archiving is audited before and after; a refusal writes nothing, scoped**
`AC-214-D` · P1 · Api, real PostgreSQL · CLAUDE.md
Given an archived item that is un-archived, when the audit trail is read, then a record names the
actor, the time, and the old (`Archived`) and new (`Active`) status; and given the refused un-archive
of `TC-2-100`, an `AuditRecords` count **scoped to that item's own `EntityId`**, taken before and after
the refusal, is unchanged.
*Fails if:* the refusal writes a record, the successful un-archive's record omits either status, or the
refusal check is an unscoped global count that could hide a stray write to a different row — the
`TC-2-064`/D-116 shape.

**TC-2-103 · Arabic, RTL, at mobile width**
`AC-214-E` · P3 · E2E · CLAUDE.md
Given `S-017` and its un-archive control at 390px in Arabic, when it renders, then direction is RTL,
the control's label is readable, no string is a literal in either language, and the page body does not
scroll horizontally.
*Fails if:* the body scrolls horizontally, or the control's label is a hardcoded literal.

⛔ **Held, dated 2026-09-10 — cannot run on a seeded stack (D-133 §4).** The un-archive control renders
only for a row whose status is not `Active`, and no endpoint creates a باب and none is seeded
[Verified: 2026-09-10 @ `tests/Api.Tests/DatabaseSeedingTests.cs` ->
`A_freshly_initialised_database_seeds_no_babs`], so a seeded stack holds no catalogue item — archived or
otherwise — to carry the control this case renders. **Not run until `KAFF-204` ships a باب create
endpoint and a stack can be seeded with at least one archived catalogue item.**
`scripts/seed-demo.ps1` is not the fix — seeding an invented باب is refused as data (`Q75`, D-130 §8).

---

Held open no longer applies to any of these four stories as a whole — `Q29`, `Q70`, `Q71`, `Q72` and
`Q73` are all ruled (D-139, D-140, D-141, D-145 §1). They are cased in full below. One criterion inside
`KAFF-208` (`AC-208-B`, above) and one inside `KAFF-211` (`AC-211-O`, below) still HELD on their own
narrower, still-open questions — `Q80` and UX's `S`-number respectively — and are marked so in place,
per the brief: a case driven against a question nobody has answered would report a coverage that does
not exist.

---

# KAFF-209 · Register a worker from site — masters ready for QA (new, 2026-09-12 — `Q70`/`Q71` ruled)

**TC-2-105 · a site engineer registers a worker with §10's four fields, and the request cannot carry `Kind`**
`AC-209-A` · P1 · Api, real PostgreSQL · §10 · D-140
Given a site engineer assigned to project A, and separately the Owner, on `POST /api/projects/{A}/day-labour`,
when a name, phone, باب and specialty are submitted, then the worker exists as an `Employee` with
`Kind = DayLabour` carrying those values and appears in the pool on `GET …/day-labour`; and the request
type is enumerated as an allow-list carrying no `Kind` member at all.
*Fails if:* the worker is created with any kind other than `DayLabour`, or the request type has a `Kind`
member that could be set to `Salaried` — D-140's own reasoning: leaving it out is stronger than
validating it. **Named test:** `An_assigned_site_engineer_registers_a_day_labourer_from_site` (D-140
SM-30 test 5).

**TC-2-106 · a worker without a باب is refused, in the handler and in the database**
`AC-209-B` · P1 · Domain + Api, real PostgreSQL (raw SQL) · §10
Given a registration with no trade, when it is submitted, then it is refused with
`errors.master.day_labour_requires_trade`; and the same row inserted by raw SQL that bypasses the
handler is refused by `ck_employees_day_labour_has_trade`.
*Fails if:* either path accepts a day-labour row with no باب.

**TC-2-107 · the phone is matched on its normalised form**
`AC-209-C` · P1 · Domain + Api · §10 · D-141
Given a worker registered as `+20 100 123 4567`, when the same number is submitted as `0100 123 4567`,
as `0020 100 123 4567`, and with spaces and dashes in different places, then every one of them is
recognised as the same number. **Named test:** `Employee_phone_match_is_on_the_normalised_form` (D-141).
*Fails if:* any one form is not recognised as a match.

**TC-2-108 · a repeated day-labour phone warns and is acknowledged, never refused**
`AC-209-D` · P1 · Api, real PostgreSQL · D-139 §1 · D-141
Given a worker already registered with a number, when a second worker is submitted with the same number
and no acknowledgement, then the save is refused `409 errors.master.duplicate_phone_not_acknowledged`,
naming the existing worker; and with `AcknowledgedDuplicatePhone: true` the save succeeds, both workers
exist, and one `DuplicatePhoneAcknowledged` audit record is written against the first.
*Fails if:* the unacknowledged save succeeds, the acknowledged save is refused, or no audit record is
written. **Named test:** `An_unacknowledged_duplicate_employee_phone_is_refused_and_writes_nothing`
(D-141), read for its day-labour form per D-146 test 8 (two day-labour records, not salaried).

**TC-2-109 · an assigned site engineer reaches the route; an unassigned one does not**
`AC-209-E` · P1 · Api · D-139 §2 · D-140
Given a site engineer assigned to project A and a site engineer with no assignment to project A, when
each calls `POST /api/projects/{A}/day-labour`, then the assigned one succeeds and the unassigned one is
refused `403`, and no worker is created by the refused call.
*Fails if:* the unassigned engineer's call succeeds. **Named tests:**
`An_unassigned_site_engineer_is_refused_registration` and
`An_unassigned_site_engineer_is_refused_DayLabourSiteManage` (D-140 SM-30 tests 3, 6).

**TC-2-110 · a role holding neither `DayLabourSiteManage` nor `EmployeeManage` reaches nothing, HR included**
`AC-209-F` · P1 · Api · D-139 §2 · D-140
Given a signed-in user of each of Finance, TechnicalOffice, HeadOfDesign, MarketingSales, Client,
Subcontractor and **Hr**, when each calls the registration endpoint directly, then every call is refused
`403`, and no worker is created by any of them.
*Fails if:* any of the seven succeeds — **HR included**, so a later grant to HR is caught, D-140's own
reasoning. **Named tests:** `A_site_engineer_is_refused_every_EmployeeManage_route` and
`Every_role_without_DayLabourSiteManage_is_refused` (D-140 SM-30 tests 8, 12).

**TC-2-111 · no rate is captured at registration**
`AC-209-G` · P1 · Domain · §10 · CLAUDE.md
Given the registration request and its response, when their members are enumerated as an allow-list,
then no day rate, wage, salary or money-typed member appears in either, under that name or any other.
*Fails if:* any money-typed member is added to either contract.

**TC-2-112 · registration is audited, and the acknowledgement is audited against the real id**
`AC-209-H` · P1 · Api, real PostgreSQL · CLAUDE.md · D-140 point 6
Given a worker registered from site, when the audit trail is read, then a record names the actor, the
time, and the record created; and given an acknowledged duplicate, its own `DuplicatePhoneAcknowledged`
record names the worker already holding the number.
*Fails if:* either record is missing, or the acknowledgement record's subject is not the real matched
id. **Named test:** `Registration_from_site_is_audited_with_its_project` (D-140 SM-30 test 13).

**TC-2-113 · `S-026` works one-handed in Arabic at 390px**
`AC-209-I` · P3 · E2E · CLAUDE.md
Given the register-from-site screen at 390px in Arabic, when it renders, then direction is RTL, the
phone field opens a numeric keypad, every control is reachable with one thumb, the name and the number
are bidi-isolated, no string is a literal in either language, and the page body does not scroll
horizontally.
*Fails if:* the body scrolls horizontally, or the phone field does not open a numeric keypad.

**TC-2-114 · the pool shows a never-engaged worker as having no figures, not zero**
`AC-209-J` · P2 · Api · §10
Given a worker registered today and never engaged, when `GET …/day-labour` renders him, then his
average day rate, frequency and rating each read as an explicit empty state — never `0`, never a blank,
and never a placeholder row.
*Fails if:* any of the three figures renders as `0` or a blank rather than the named empty state.

**TC-2-115 · a phone match against a salaried record is masked**
`AC-209-K` · P1 · Api, real PostgreSQL · D-140 point 6
Given a salaried employee registered with a number, when a site engineer's phone-check is run against
that number, then the response is `{ restricted: true }` with no id, name or code; and if the site
engineer proceeds and acknowledges, the save succeeds and the audit record names the real salaried id,
never exposed to the response.
*Fails if:* the response names the salaried record, or the audit record names anything other than the
real id. **Named test:** `A_salaried_phone_match_is_restricted_for_a_site_engineer` (D-140 SM-30 test
11).

---

# KAFF-210 · Worker engagement history, day rate, frequency and rating — masters ready for QA (new, 2026-09-12 — `Q72` ruled)

⚠️ **Money-touching story, flagged per this pass's brief.** `AC-210-C` carries a day rate at
`decimal(18,4)` through `Money`. It writes no `Posting` and derives every summary rather than storing
it (`AC-210-B`), but the rate itself is money and its precision is asserted below.

**TC-2-116 · an engagement is recorded against a worker**
`AC-210-A` · P1 · Api, real PostgreSQL · §10
Given a registered worker, when an engagement is recorded with its project, its dates and its agreed
day rate, then it appears in his history and the pool figures change to account for it.
*Fails if:* the engagement does not appear in the history, or the pool is unchanged by it.

**TC-2-117 · the three pool figures are derived, never stored**
`AC-210-B` · P1 · Domain, real PostgreSQL · CLAUDE.md · §10
Given a worker with three engagements at different rates, when the stored columns of the worker and of
the engagement are enumerated as an allow-list, then no average, no count, no total and no cached
rating appears among them; and recomputing from the engagements alone, with nothing deleted, reproduces
every figure the pool shows.
*Fails if:* a stored average, count or rating column exists, under any name.

**TC-2-118 · the day rate keeps four decimals and never passes through a float** *(money-touching)*
`AC-210-C` · P1 · Domain + Api, real PostgreSQL · CLAUDE.md
Given an agreed day rate of `487.6543`, when the engagement is saved and read back, then the value is
exact to the fourth decimal, and it has not passed through a `float` or a `double` at any point between
the request body and the database.
*Fails if:* the value differs in the fourth decimal, or a `float`/`double` accessor is used in the
parse or persistence path.

**TC-2-119 · the average is the average of the rates actually paid, recomputed on every read**
`AC-210-D` · P1 · Api, real PostgreSQL · §10
Given engagements at `300`, `400` and `500`, when the pool renders his average day rate, then it is
`400`, computed from those three rows at the moment of the read; and adding a fourth engagement changes
it on the next read with nothing else written anywhere.
*Fails if:* the average is wrong, or does not change after a fourth engagement is added with no other
write.

**TC-2-120 · this story writes no posting and creates no account**
`AC-210-E` · P1 · Api, real PostgreSQL · CLAUDE.md · §10
Given an engagement recorded at a day rate, when the treasury is inspected, then no `Posting` and no
account was created by it, under any type; and the routes this story maps are enumerated as an
allow-list, none of which reaches the treasury.
*Fails if:* any `Posting` or account exists that this story's routes created.

**TC-2-121 · an engagement is one continuous stretch on one project, and a rating is out of 5**
`AC-210-F` · P1 · Domain + Api · D-139 §3
Given the history and the pool, when frequency and rating render, then frequency counts
**engagements**, not days and not projects, and a rating outside `1`–`5` is refused.
*Fails if:* frequency counts days or projects instead of engagements, or a rating outside `1`–`5` is
accepted. The rating's finer shape (whole number vs. decimal vs. weighted criteria, `Q78`) is **not**
asserted here — this case validates only the range D-139 §3 gives.

**TC-2-122 · a worker with no engagements reads as unengaged, not as zero**
`AC-210-G` · P1 · Api · §10
Given a worker registered and never engaged, when the pool and the history render him, then each of the
three figures shows the explicit empty state — never `0`, never `0.0000`, never a blank cell and never
a placeholder row.
*Fails if:* any figure renders as `0`, `0.0000` or a blank rather than the named empty state.

**TC-2-123 · a role without the permission reads and writes nothing, including the read**
`AC-210-H` · P1 · Api · D-110 §2 · D-139 §2 · D-140
Given a signed-in user of each role that does not hold `DayLabourSiteManage`, and an assigned site
engineer with no assignment to the engagement's project, when each calls the engagement, rating and pool
read endpoints directly, then every call is refused `403` — **including the read**, because on a read
the permission test is the entire control.
*Fails if:* any refused role's read succeeds.

**TC-2-124 · the engagement and the rating are audited**
`AC-210-I` · P1 · Api, real PostgreSQL · CLAUDE.md
Given an engagement recorded and then rated, when the audit trail is read, then each has a record naming
the actor, the time, and what was written.
*Fails if:* either record is missing.

**TC-2-125 · Arabic, RTL, at mobile width, the rate bidi-isolated**
`AC-210-J` · P3 · E2E · CLAUDE.md
Given the pool and the history at 390px in Arabic, when they render, then direction is RTL, the day
rate is bidi-isolated with an explicit direction rather than left to first-strong, dates and ratings do
not reorder, no string is a literal in either language, and the page body does not scroll horizontally.
*Fails if:* the body scrolls horizontally, or the rate is left to first-strong rather than an explicit
`dir`.

**TC-2-126 · an engagement closes only by explicit manual close, and only within its own project**
`AC-210-K` · P1 · Api, real PostgreSQL · D-139 §3 · D-140
Given an open engagement on project A, when a site engineer assigned to A closes it, and separately an
engineer assigned only to project B attempts to close it by pairing B's route with A's engagement id,
then the assigned engineer's close succeeds and is audited, the mismatched close is refused `403`, and
no automatic process ever closes an engagement on its own.
*Fails if:* the mismatched close succeeds, or any process closes an engagement without an explicit
call. **Named test:** `An_engagement_cannot_be_closed_through_another_projects_route` (D-140 SM-30 test
14, rule 6a's own reason for existing).

---

# KAFF-211 · Subcontractor master, profile only — masters ready for QA (new, 2026-09-12 — `Q29`/`Q73`/`Q70` ruled)

**TC-2-127 · a subcontractor is created with a trade and the default 5% retention**
`AC-211-A` · P1 · Api · §5.1
Given the Technical Office, and separately the Owner (`Q12`, D-129 §1), when a code, name, phone and
trade باب are submitted with no retention given, then the firm exists carrying those values and a
retention of **5%**, and it is active.
*Fails if:* the default retention is anything other than 5%, or the firm is inactive on creation.

**TC-2-128 · retention is zeroed for one firm and no other**
`AC-211-B` · P1 · Api, real PostgreSQL · §5.1
Given two subcontractors, both at the 5% default, when one is set to 0%, then that one is 0% and the
other is still 5%, and no global or default value anywhere has changed.
*Fails if:* the second firm's retention changes, or a global default changes.

**TC-2-129 · the retention rate is a percentage and survives its round trip**
`AC-211-C` · P1 · Domain · D-044 ruling 6
Given a retention entered as `5`, meaning five percent, when it is stored and read back, then it is the
fraction `0.05` — not `5` — and `2.5%` survives exactly at storage and display precision.
*Fails if:* `5` is stored as the integer `5` rather than the fraction `0.05`, or `2.5%` loses precision.

**TC-2-130 · no subcontractor can sign in**
`AC-211-D` · P1 · Api · §9 · D-065
Given a subcontractor record, when the endpoints this story maps are enumerated as an allow-list, then
none of them creates a `User`, sets a credential or grants a role; and a sign-in attempt against a
subcontractor answers the same generic `401` as an unknown username.
*Fails if:* any endpoint creates a `User` or credential, or a sign-in attempt against a subcontractor's
identity is distinguishable from an unknown username.

**TC-2-131 · this story writes no posting and stores no balance**
`AC-211-E` · P1 · Domain, real PostgreSQL · CLAUDE.md
Given a subcontractor record, when its stored properties are enumerated as an allow-list and the
treasury is inspected, then no balance, outstanding, total, amount-typed or withholding-rate member
appears among them, and no `Posting` and no account was created.
*Fails if:* any such member exists, or a `Posting`/account was created.

**TC-2-132 · nothing here presents withholding as recoverable or nets it against the client side**
`AC-211-F` · P1 · Api · §6.7 · D-139 §5
Given a subcontractor record and every screen that shows it, when they are inspected, then none of them
carries a withholding rate — it lives on the contract/job — and none presents the concept as
recoverable, as an asset, or netted with a client's tax withheld at source.
*Fails if:* a withholding rate or a recoverable/netted framing appears on any surface.

**TC-2-133 · the withholding rate is not on this record**
`AC-211-G` · P1 · Domain · D-139 §5
Given a subcontractor working two jobs at two different rates, when the entity's stored properties are
enumerated as an allow-list, then no withholding rate or category appears among them.
*Fails if:* `WithholdingCategory` or an equivalent member still exists on `Subcontractor`.

**TC-2-134 · the master carries no rate card**
`AC-211-H` · P1 · Api · D-139 §4
Given the create and edit screens, when they render, then neither carries a price, a rate or a
rate-card field of any kind.
*Fails if:* a rate-card field appears on either screen.

**TC-2-135 · a repeated subcontractor phone warns and is acknowledged, never refused**
`AC-211-I` · P1 · Api, real PostgreSQL · D-139 §1 · D-141
Given a subcontractor already registered with a number, when a second firm is submitted with the same
number and no acknowledgement, then the save is refused `409 errors.master.duplicate_phone_not_acknowledged`,
naming the existing firm; and with `AcknowledgedDuplicatePhone: true` the save succeeds and one
`DuplicatePhoneAcknowledged` audit record is written.
*Fails if:* the unacknowledged save succeeds, or the acknowledged save is refused.

**TC-2-136 · a role without `SubcontractorManage` reaches nothing, Finance included**
`AC-211-J` · P1 · Api · §2 · D-129 §1
Given a signed-in user of each role that does not hold `SubcontractorManage` — **including Finance**,
which disburses but does not own the record — when each calls the create, edit, retention and archive
endpoints directly, then every call is refused `403`, and no record is created or changed by any of
them.
*Fails if:* any refused role, Finance included, succeeds at any endpoint.

**TC-2-137 · the retention edit and the tax-registration edit write two separate audit records, from two roles**
`AC-211-K` · P1 · Api, real PostgreSQL · D-147
Given a subcontractor whose retention rate is edited by the Technical Office through
`SubcontractorManage`, and whose tax registration number is separately edited by Finance through
`PUT /api/subcontractors/{id}/tax-registration`, when the audit trail is read, then **two** records
exist — one naming the Technical Office actor, the time, and the retention field's old and new values;
the other naming the Finance actor, the time, and the tax registration number's old and new values.
*Fails if:* one combined record spans both fields, or either actor is misattributed. **Named tests:**
`Finance_sets_a_subcontractors_tax_registration_and_it_is_audited_before_and_after` (D-147 test 3) for
the tax-registration half.

**TC-2-138 · nothing on this screen is a bid**
`AC-211-L` · P1 · Api · §1
Given `S-028` and `S-029`, when every field, control and route they carry is enumerated as an
allow-list, then none of them is a bid, a quotation, an RFQ or a comparison of two firms' prices.
*Fails if:* any such field, control or route exists.

**TC-2-139 · Arabic, RTL, at mobile width**
`AC-211-M` · P3 · E2E · CLAUDE.md
Given `S-028` and `S-029` at 390px in Arabic, when they render, then direction is RTL, the term reads
**مقاول باطن**, percentages and phone numbers are bidi-isolated, no string is a literal in either
language, and the page body does not scroll horizontally.
*Fails if:* the body scrolls horizontally, or the term is a synonym rather than the catalogue's own key.

**TC-2-140 · the Technical Office cannot set the tax registration number by any route**
`AC-211-N` · P1 · Domain + Api · D-147
Given a Technical Office user holding `SubcontractorManage` but not `SubcontractorTaxRegistrationEdit`,
when every endpoint is enumerated as an allow-list, including create, edit and the tax-registration
endpoint itself, then `SubcontractorManage`'s create and edit request shapes carry no
`TaxRegistrationNumber` member to send, and a direct call to
`PUT /api/subcontractors/{id}/tax-registration` by this user is refused `403`.
*Fails if:* the create/edit request type carries a `TaxRegistrationNumber` member, or the direct call
succeeds. **Named test:** `The_technical_office_cannot_set_a_subcontractors_tax_registration_by_any_route`
(D-147 test 4). **Also asserted:** `Only_the_owner_and_finance_hold_SubcontractorTaxRegistrationEdit_and_it_touches_no_money`
and `Finance_edits_a_subcontractors_tax_registration_but_not_the_subcontractor_record` (D-147 tests 1,
2), and `Every_role_without_SubcontractorTaxRegistrationEdit_is_refused_the_tax_registration_routes`
(D-147 test 5) for the remaining roles.

**TC-2-141 · HELD — Finance's tax-registration screen has no `S`-number**
`AC-211-O` · — · — · D-147
⛔ **HELD.** Given `GET /api/subcontractors/tax-registrations` and
`PUT /api/subcontractors/{id}/tax-registration`, both ruled by D-147, when Frontend is asked to build
the screen Finance uses to reach them, then — **held.** UX has not named an `S`-number for this screen.
*Not a passing case.* **No frontend criterion is cased here until UX assigns one** — inventing a screen
id would be inventing UX's answer. The endpoints' own backend behaviour is still cased above (`TC-2-137`,
`TC-2-140`) and D-147 test 6, `The_finance_subcontractor_list_carries_only_the_tax_registration_fields`,
covers the projection `GET` returns without needing a screen to exist.

---

# KAFF-212 · Supplier master — masters ready for QA (new, 2026-09-12 — `Q29`/`Q70`/`Q13` ruled)

**TC-2-142 · a supplier is created once and is not per project**
`AC-212-A` · P1 · Api · §2
Given Finance, when a code, name, phone and address are submitted, then the supplier exists carrying
those values, is active, and carries no project — the record has no project field to carry one.
*Fails if:* a project field exists or is populated.

**TC-2-143 · one supplier, one account, many projects**
`AC-212-B` · P1 · Domain · §2 · §6.3
Given a supplier delivering to three projects, when his record and the account type that will serve him
are inspected, then there is exactly one supplier row and his account type is company-scoped, not
project-scoped; and nothing in this story creates a second record, a per-project record, or an account
at all.
*Fails if:* a second or per-project record exists, or an account is created by this story.

**TC-2-144 · no balance and no withholding rate is stored anywhere on this record**
`AC-212-C` · P1 · Domain · CLAUDE.md · D-139 §5
Given a supplier, when his stored properties are enumerated as a named allow-list, then no balance,
outstanding, total, purchases-to-date, withholding rate or other amount-typed member appears among
them, under that name or any other.
*Fails if:* any such member exists.

**TC-2-145 · two suppliers can never carry the same code**
`AC-212-D` · P1 · Api, real PostgreSQL · slice 0 · HELD — `Q85`
Restated 2026-09-13 (`V-38-I`): the code is generated (`SupplierCodeSequence`), never typed — `Request`
carries no `Code` member, so a submitted `S-100`/`s-100` pair cannot collide by typing and the prior
case asserted a shape the endpoint does not have.
Given many supplier-create requests arriving concurrently, when they all resolve, then every generated
code is distinct, guaranteed by the sequence draw and the unique index on `Supplier.Code`, not a
read-then-write.
*Fails if:* two created suppliers ever carry the same code.

**TC-2-146 · nothing here presents withholding as recoverable or nets it against the client side**
`AC-212-E` · P1 · Api · §6.7 · D-139 §5
Given a supplier record and every screen showing it, when they are inspected, then none of them carries
a withholding rate, and none presents the concept as recoverable, as an asset, or netted with a
corporate client's tax withheld at source.
*Fails if:* a withholding rate or a recoverable/netted framing appears on any surface.

**TC-2-147 · the withholding rate is not on this record**
`AC-212-F` · P1 · Domain · D-139 §5
Given a supplier delivering materials on one job and a service on another, when the entity's stored
properties are enumerated as an allow-list, then no withholding rate or category appears among them.
*Fails if:* `WithholdingCategory` or an equivalent member still exists on `Supplier`.

**TC-2-148 · a repeated supplier phone warns and is acknowledged, never refused**
`AC-212-G` · P1 · Api, real PostgreSQL · D-139 §1 · D-141
Given a supplier already registered with a number, when a second is submitted with the same number and
no acknowledgement, then the save is refused `409 errors.master.duplicate_phone_not_acknowledged`,
naming the existing supplier; and with `AcknowledgedDuplicatePhone: true` the save succeeds and one
`DuplicatePhoneAcknowledged` audit record is written.
*Fails if:* the unacknowledged save succeeds, or the acknowledged save is refused.

**TC-2-149 · a role without `SupplierManage` reaches nothing, Technical Office included**
`AC-212-H` · P1 · Api · §2 · D-129 §1
Given a signed-in user of each role that does not hold `SupplierManage` — **including the Technical
Office** — when each calls the create, edit and archive endpoints directly, then every call is refused
`403`, and no record is created or changed by any of them.
*Fails if:* any refused role, Technical Office included, succeeds at any endpoint.

**TC-2-150 · a supplier is archived, not deleted**
`AC-212-I` · P1 · Api · CLAUDE.md
Given an active supplier, when he is archived, then the row still exists with every field intact and is
findable through the explicit filter; and no mapped route on the API deletes a supplier, under any verb.
*Fails if:* the row is removed, or a delete route exists.

**TC-2-151 · every change is audited before and after**
`AC-212-J` · P1 · Api, real PostgreSQL · CLAUDE.md
Given a supplier whose address and tax registration number are both edited in one request, when the
audit trail is read, then one record names the actor, the time, and both fields with their old and new
values.
*Fails if:* either field's old or new value is missing from the record.

**TC-2-152 · nothing on this screen is a quote comparison**
`AC-212-K` · P1 · Api · §1
Given `S-030`, when every field, control and route it carries is enumerated as an allow-list, then none
of them is a bid, an RFQ, a quotation or a comparison of two suppliers' prices.
*Fails if:* any such field, control or route exists.

**TC-2-153 · Arabic, RTL, at mobile width**
`AC-212-L` · P3 · E2E · CLAUDE.md
Given `S-030` at 390px in Arabic, when it renders, then direction is RTL, names, numbers and the tax
registration number are bidi-isolated, no string is a literal in either language, and the page body
does not scroll horizontally.
*Fails if:* the body scrolls horizontally.

---

# Findings, stated here rather than filed to `qa/questions.md`

This QA session did not edit `qa/questions.md`, `decisions.md`, any story file or any trailer — ticking
a Definition-of-Ready box is the Scrum Master's, after this file lands.

**Closed since the last pass, no longer findings:**
1. ~~`AC-200-B`'s "more than four decimals" ambiguity~~ — closed by D-144 §6, recased as `TC-2-002`.
2. ~~`AC-207-C`/`TC-2-068` describing a typed code~~ — closed by `V-37-E`'s own routing and D-130 §6,
   recased as `TC-2-068`.

**Still open, carried forward:**
1. **`AC-208-C` cannot be executed against the code as it stands, because no employee-edit endpoint
   exists to send the request to** — the same shape as `AC-125-C`. See the note under `TC-2-078`. The
   case is written and held, not dropped.
2. **`AC-207-G`'s generated-code switch and `Department` field are Backend's to ship** before
   `TC-2-072` and `TC-2-105`'s field-list assertions are true of the running system — both cases are
   written against the ruling, not the code as it stands.

**New gaps for the BA, found while casing this pass — none invented an expected behaviour a criterion
does not state:**
3. **`AC-208-B` (`TC-2-077`) and `AC-211-O` (`TC-2-141`) are HELD**, on `Q80` (narrowed to an active
   cross-population phone match, D-146 §4(b) — Nabil's) and on UX's still-unassigned `S`-number for
   Finance's tax-registration screen (D-147) respectively. Neither is cased as passing; both are ready
   to be un-held the day their answer lands, per the pattern D-146 test 9 already uses (kept skipped,
   not deleted).
4. **`Q79`** (is `Department` required on the staff file — `KAFF-207`), **`Q76`**/`Q77`**/`Q78`** (the
   Site Engineer's day-rate visibility, closing another engineer's engagement, and the rating's finer
   shape — `KAFF-210`), and **`Q83`**/`Q84`** (rehiring a salaried leaver on the same phone, and whether
   D-147's "Finance only" includes the Owner — `KAFF-208`/`KAFF-211`) are all registered and none blocks
   a criterion cased in this file — recorded here so the next QA session does not have to re-derive that
   fact from five separate stories.
5. **Money-touching flag, per this pass's instruction.** `KAFF-210` stores an agreed day rate as
   `Money` at `decimal(18,4)` (`AC-210-C`, `TC-2-118`). It writes no `Posting`, opens no account and
   derives every summary rather than storing it (`AC-210-B`), so it does not touch a ledger — but the
   rate itself is money and is flagged here rather than silently passed over. No other criterion cased
   in this pass stores or moves money.

No contradiction was found between `D-129`/`D-130`/`D-139`–`D-147` and any of the thirteen stories cased
here beyond what the stories themselves already flag.
