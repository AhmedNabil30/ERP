# KAFF-206 · Archive a catalogue item without breaking what already references it

<!-- kaff id=KAFF-206 slice=2 points=3 state=NOT-BUILT verdict=none at=- on=2026-09-09 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 3 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-09 by the BA. **Two Definition-of-Ready boxes are unticked, and this story raises the question that decides its own centre.**
**Spec:** **§4.1** (`status`), **§4.4**, **§4.5**, §2 · **Decisions:** D-018 (the `status` values, 🟡), D-044 ruling 4
**Register:** `stories/questions-for-karim.md` → **`Q12`** (open, slice-2-wide), **`Q65`** (blocking), **`Q66`**
**Screens:** `ux/screen-inventory.md` → **`S-017`** (the list, and its status filter), **`S-018`**
**Owner:** Backend, then Frontend
**Depends on:** KAFF-202 (the item), KAFF-203 (the list this is reached from)

## Story
As the Technical Office, I take an item out of use without deleting it, because a price list that can
only grow becomes unusable and a price list that can be deleted from breaks every document that has
already quoted the deleted row.

## What already exists, and what this story adds

| Already built | Evidence |
|---|---|
| `Archive`, and its refusal when the item is already archived | [Verified: 2026-09-09 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Archive`] |
| The two statuses, flagged 🟡 because §4.1 names the field and enumerates nothing | [Verified: 2026-09-09 @ `src/Domain/MasterData/CatalogueItem.cs` -> `CatalogueItemStatus`] |
| The refusal itself, shared by every master record that archives | [Verified: 2026-09-09 @ `src/Domain/MasterData/MasterDataErrors.cs` -> `AlreadyArchived`] |
| The permission row | [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.CatalogueManage`] |

**What does not exist is any endpoint or screen** [Verified: 2026-09-09 — `src/Api/Features/` holds
`Assignments`, `Audit`, `Auth`, `Clients`, `Health`, `Setup` and `Users`, and no catalogue folder].
**And there is no un-archive path anywhere in this codebase** — `MasterDataErrors.NotArchived` is
declared and returned by nothing [Verified: 2026-09-09 @ `src/Domain/MasterData/MasterDataErrors.cs` -> `NotArchived`],
which is evidence that the shape was anticipated and never ruled. See `Q66`.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | Archiving sets `status` to `Archived`. It is a state change on the row, not a removal of it [Verified: 2026-09-09 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Archive`] | §4.1 |
| 2 | **There is no delete path, and this story must not create one.** The precedent is `AC-123-D` on the client, which was made to fail on purpose so the absence is asserted rather than assumed | **CLAUDE.md** · KAFF-123 |
| 3 | **A signed BOQ is untouched, and there is no mechanism by which it could be touched.** §4.4 makes the signed line a set of copies with no foreign key back to the catalogue row, so archiving has nothing to follow. **The correct implementation of this rule is to add nothing** | **§4.4** — MUST |
| 4 | **An open, unsigned estimate that already carries the item is untouched too.** §4.4's second paragraph gives exactly one mechanism for reaching an open offer — the *"you have X open offers on old pricing"* review, `S-049`, slice 4, where a human decides per estimate. This story raises no alert and re-prices nothing | **§4.4** · `ux/screen-inventory.md` -> `S-049` |
| 5 | Archiving an item that is already archived is refused with `errors.master.already_archived`, not silently accepted [Verified: 2026-09-09 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Archive`] | slice 0, built · **the refuse-or-no-op family is `Q51`**, and this row is not a new answer to it |
| 6 | ⛔ **Whether an archived item may still be put on a *new* BOQ line is `Q65`, and it is this story's centre.** §4.5 says *"Add item is always visible and searches the catalogue by code or description"* and says nothing about status. The title's *"without breaking what already references it"* is about **existing** references — rules 3 and 4 settle those completely. **What is unruled is whether it can take new ones**, and both readings are ordinary | **`Q65`** — uncited, therefore asked, not decided |
| 7 | **The default list excludes archived items; they stay findable through an explicit filter.** The shape is already settled on the client list and is copied rather than re-invented: **three states, not a boolean** — `Active` (the default), `Archived` alone, `All` — because a boolean cannot express the third chip, and an unknown value is **refused rather than defaulted**, since a silently-defaulted wrong filter is indistinguishable from an empty archive [Verified: 2026-09-09 @ `src/Api/Features/Clients/ListClients/ClientListFilter.cs` -> `ClientListFilter`] | D-111 §3 · KAFF-124 rule 2 · §4.5 |
| 8 | `CatalogueManage`, `CompanyWide`, **no assignment** — the catalogue belongs to no project. Technical Office settled by §2; the Owner grant stands on **`Q12`** | §2, §4.1 · D-044 ruling 4 · **`Q12`** |
| 9 | Archiving is a state change and **writes an audit record**: who, when, and the old and new status | **CLAUDE.md** |
| 10 | Every string is an i18n key; Arabic RTL at 390px | CLAUDE.md · `ux/rtl-and-i18n.md` |

## Permissions, money, audit, i18n
- **Permissions:** `CatalogueManage`, `CompanyWide`, no assignment required. Every other role refused
  `403` server-side; hiding the archive control is not the control.
- **Money:** **moves none and writes no `Posting`.** It changes no price: `CostPrice` and
  `BaseSellRate` are untouched by `Archive` [Verified: 2026-09-09 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Archive`],
  and no stored total anywhere is recomputed, because no total is stored.
- **Audit:** rule 9, before and after on `status`.
- **i18n:** `catalogue.item.archive_action`, `catalogue.item.archive_confirm`,
  `catalogue.item.archived_badge`, `catalogue.filter.include_archived`, plus the existing
  `errors.master.already_archived`.

## Acceptance criteria

**AC-206-A — an item is archived and is still there**
Given an active catalogue item
When it is archived
Then its status is `Archived`, the row still exists with every one of its §4.1 fields unchanged, and it is reachable through the list's explicit archived filter

**AC-206-B — there is no delete path** *(fails if the rule is broken)*
Given the catalogue endpoints the API maps
When the mapped routes are enumerated — the routes the host actually registered, not a search of the source for the word "delete"
Then no route deletes a catalogue item, under any verb, and the enumeration is written as an allow-list of what exists rather than an absence test for a word nobody typed

**AC-206-C — archiving does not touch a signed BOQ** *(fails if the rule is broken)*
Given a signed BOQ line carrying an item's values at signature time
When that catalogue item is archived
Then every value on the signed line is unchanged, the line still renders, and no field on it holds a foreign key to the catalogue row

**AC-206-D — archiving does not touch an open estimate and raises no alert** *(fails if the rule is broken)*
Given open, unsigned estimate lines carrying the item
When the item is archived
Then no estimate line changes and no alert is raised — §4.4's review is `S-049`, slice 4

**AC-206-E — archiving twice is refused**
Given an already archived item
When it is archived again
Then it is refused with `errors.master.already_archived`, and nothing is written to the audit trail for the refusal

**AC-206-F — HELD on `Q65`: an archived item and a new BOQ line**
Given an archived item
When somebody searches the catalogue from the BOQ builder's "add item"
Then — **held.** `Q65` decides whether the item is offered, offered with a warning, or absent. **No behaviour is asserted here, and none is implemented until it is ruled.** The two readings differ in one place only, and this criterion is that place

**AC-206-G — a role without `CatalogueManage` archives nothing** *(fails if the rule is broken)*
Given a signed-in user of each role that does not hold `CatalogueManage`
When each calls the archive endpoint directly, with no browser involved
Then every call is refused `403`, and no item's status has changed

**AC-206-H — archiving is audited before and after** *(fails if the rule is broken)*
Given an archived item
When the audit trail is read
Then a record names the actor, the time, and the old and new status
And the refused second archive of `AC-206-E` produced no record at all

**AC-206-I — Arabic, RTL, at mobile width**
Given `S-017` and its archive control at 390px in Arabic
When they render
Then direction is RTL, the archived badge and the filter are readable, no string is a literal in either language, and the page body does not scroll horizontally

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-206-A` … `AC-206-I`. **`AC-206-F` is written and held**, which is the shape a criterion takes when the rule behind it is a question |
| Stable `AC-206-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–10; **rule 6 cites `Q65` and is marked as a question rather than sourced** |
| No uncited rule | ✅ — the one rule with no source is registered as `Q65` instead of being written |
| Permissions named explicitly | ✅ — `CatalogueManage`, CompanyWide, no assignment; the Owner half flagged to `Q12` |
| Money behaviour named explicitly | ✅ — moves none, changes no price, writes no `Posting` |
| Arabic UI strings as i18n keys | ✅ — four new keys plus one existing |
| The audit record it writes is stated | ✅ — rule 9, `AC-206-H` |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated [Verified: 2026-09-09 — `qa/` holds `slice-1`, `questions.md`, `README.md`, `risk-register.md` and `strategy.md`]. Five criteria are marked *(fails if the rule is broken)*. **QA's to write** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ⛔ **Not met — and unlike `KAFF-202`…`205` this one is blocked on a question of its own.** `Q65` decides `AC-206-F` and rule 6; `Q12` is the slice-wide grant. `Q66` blocks nothing |

**Flip the trailer to `READY` when `Q65` and `Q12` are ruled and QA's cases land.**

## Not in this story
- **Archiving a باب.** `Bab.Archive` exists [Verified: 2026-09-09 @ `src/Domain/MasterData/Bab.cs` -> `Archive`]
  and **no story on the board owns it.** `stories/backlog.md`'s slice-2 table names this row *"archive
  a catalogue **item**"* at 3 points and has no باب row at all. ⛔ **It is deliberately not absorbed
  here** — it is a different entity with a different question behind it (`Q67`: may a باب holding items
  be archived, and what happens to those items), and a story that quietly grows to cover an unlisted
  one is how an estimate stops meaning anything. **The BA's recommendation is a new story at 3 points,
  and cutting one is scope — the Scrum Master's to propose and Nabil's to lock.** See `decisions.md`
  D-128.
- **Un-archiving.** `Q66`. There is no un-archive path for any master record in this system today.
- **Creating and editing an item.** `KAFF-202`. **Re-pricing.** `KAFF-202`.
- **The list and its search.** `KAFF-203`, which owns the filter's default order (`Q63`).
- **Moving an item between أبواب.** `KAFF-205`.
- **Anything a BOQ does with an archived item.** Slice 4 builds the BOQ; this story only asks `Q65`.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q65`** | **New, raised here and blocking.** May an archived item still be put on a **new** BOQ line? §4.5's *"add item is always visible and searches the catalogue"* is silent on status, and both readings are ordinary. `AC-206-F` is held on it rather than answered | **Karim** |
| **`Q66`** | **New, raised here. Blocks nothing.** Does anything archived ever come back? No master record in this system can be un-archived and `MasterDataErrors.NotArchived` is returned by nothing [Verified: 2026-09-09 @ `src/Domain/MasterData/MasterDataErrors.cs` -> `NotArchived`]. **Ask it in the same breath as `Q39`**, which is the identical question about a client | **Karim** |
| **`Q67`** | **New, raised here and owned by no story.** Archiving a باب. Registered so it is visible while the story that would carry it does not exist | **Karim**, after the Scrum Master decides where the story goes |
| **`Q12`** | Open, slice-2-wide. Whether the Owner keeps `CatalogueManage` | **Karim** |
| 1 | **What `status` values an item may hold.** §4.1 names the field and enumerates nothing; the code carries `Active` and `Archived` as *"the minimum the freeze rule needs"* and flags itself 🟡 [Verified: 2026-09-09 @ `src/Domain/MasterData/CatalogueItem.cs` -> `CatalogueItemStatus`]. **`KAFF-202` recorded that this story is the one that turns on the answer, and it does:** if a third status exists, `Archived` is one of several and this story's single control is wrong | **Nabil** · D-018 |
