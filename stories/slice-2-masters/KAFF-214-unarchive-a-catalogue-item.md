# KAFF-214 · Un-archive a catalogue item

<!-- kaff id=KAFF-214 slice=2 points=2 state=VERIFIED verdict=CONDITIONAL at=d5e6548 on=2026-09-10 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 2 — the Scrum Master's retrospective estimate
(`decisions.md`), not a pre-build estimate: the code shipped before this story existed. · **Status:**
state and verdict live only in this file's line-3 trailer and in `STATUS.md` — not restated here
(`CLAUDE.md`, D-119). Cut 2026-09-10 by the BA, from `qa/slice-2/verification-2026-09-10.md`'s
`V-35-U` — placement ruled by the Scrum Master.
**Spec:** **Decisions:** **D-130 §4** (`Q66`), D-044 ruling 4, D-129 §1
**Register:** `stories/questions-for-karim.md` → **`Q66`** (✅ answered, D-130 §4), **`Q39`** (✅
answered in the same shape, D-130 §4)
**Screens:** `ux/screen-inventory.md` → **`S-017`** (the list, and its per-row un-archive control)
**Owner:** already built — Backend and Frontend both shipped; this story exists to give the shipped
code an `AC-` id and a QA case, per `V-35-U`
**Depends on:** KAFF-206 (archiving the same entity), KAFF-202 (the item)

## Story
As the Technical Office, I bring a mistakenly archived catalogue item back into active use, because a
code is unique (§4.5) and an archive that cannot be reversed turns a misclick into permanent data — the
item can only return under a new code, and the catalogue then carries two codes for one real thing
forever.

## Why this story exists after the code, not before it

**This is the defect `V-35-U` names, not a normal refinement.** `UnarchiveCatalogueItem` shipped
inside commit `f675f1b` — the same commit `KAFF-206`'s trailer attributes entirely to its own 3
points — with no `AC-206-*` id, no `TC-2-*` id, and `KAFF-206`'s own story file still read, until
today, *"Left for the Scrum Master to place."* The row control shipped later, inside `934bfb9`
("KAFF-202/203/206: the catalogue frontend"), again under nobody's criterion. **Nobody placed it; it
shipped anyway.** This story is that placement, written after the fact so the shipped code has
something to be verified against — the same shape `Q67`/`KAFF-213` gave the باب-archive question
`KAFF-206` deliberately kept separate, except that placement happened *before* the code, and this one
happens after.

## What already exists, and what this story adds

| Already built | Evidence |
|---|---|
| The endpoint — `POST /api/catalogue-items/{catalogueItemId}/unarchive`, gated `CatalogueManage`, no body, no delete | [Verified: 2026-09-10 @ `src/Api/Features/Catalogue/UnarchiveCatalogueItem/Endpoint.cs` -> `Endpoint`] |
| The handler — defers entirely to the entity's own refusal, no `Status` pre-check of its own, one `SaveChangesAsync` | [Verified: 2026-09-10 @ `src/Api/Features/Catalogue/UnarchiveCatalogueItem/Handler.cs` -> `HandleAsync`] |
| `CatalogueItem.Unarchive` — refuses a row that is not archived rather than a silent no-op, the mirror of `Archive` | [Verified: 2026-09-10 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Unarchive`] |
| The refusal — declared before this endpoint existed and returned by nothing until now | [Verified: 2026-09-10 @ `src/Domain/MasterData/MasterDataErrors.cs` -> `NotArchived`] |
| A test file covering the round trip, the refusal, and one permission case | [Verified: 2026-09-10 @ `tests/Api.Tests/UnarchiveCatalogueItemTests.cs` -> `An_archived_item_is_unarchived_and_reappears_in_the_default_search`, `Unarchiving_an_active_item_is_refused_and_writes_no_audit_record`, `A_role_without_CatalogueManage_cannot_unarchive_an_item`] |
| The row control on `S-017` — a chip rendered only when `item.status !== 'Active'`, calling the API and reloading the list | [Verified: 2026-09-10 @ `src/Web/src/app/features/catalogue/catalogue-list/catalogue-list-page.html` -> the `catalogue-unarchive-` button; `src/Web/src/app/features/catalogue/catalogue-list/catalogue-list-page.ts` -> `onUnarchive`] |
| The frontend API call | [Verified: 2026-09-10 @ `src/Web/src/app/core/catalogue/catalogue.api.ts` -> `unarchive`] |

**What this story adds is not code.** It adds the `AC-214-*` ids the shipped code has none of today,
a QA case for each, and the record that ties the shipped commits to a story that owns them. Two gaps
in what already exists, found while writing this table and left for the owner named:

- **No test drives a portal `Role.Client` against this endpoint.** `A_role_without_CatalogueManage_cannot_unarchive_an_item` [Verified: 2026-09-10] exercises `Finance` only, not `Client` — unlike `ArchiveCatalogueItemTests`'s equivalent, which does. `AC-214-C` below states the rule for both; the `Client` half is not yet executed. **Owner: Backend/QA**, the same shape as `V-35-T`.
- **No test asserts the un-archived item's other §4.1 fields are unchanged** — the existing round-trip test checks `Status` and `Code` only. `AC-214-A` below states the fuller rule.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | An archived item can be un-archived, returning it to `Active` with every other field unchanged. Codes are unique, so without this an archived-in-error item can only return under a new code and the catalogue then carries two codes for one real thing, permanently | **D-130 §4** (`Q66`) |
| 2 | Un-archiving an item that is not archived is refused with `errors.master.not_archived`, not silently accepted — the mirror of `KAFF-206` rule 5's refusal of a second archive [Verified: 2026-09-10 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Unarchive`] | **D-130 §4** · KAFF-206 rule 5 |
| 3 | The mechanism is built once in `Domain/`, on the entity itself, not duplicated per master record. **This answers `Q39` for clients in the same shape** — the rule is one rule for archived records generally, this story exercises it for the catalogue | **CLAUDE.md** · **D-130 §4** |
| 4 | `CatalogueManage`, `CompanyWide`, **no assignment** — same gate as every other catalogue endpoint. Technical Office settled by §2; the Owner keeps it too | §2 · D-044 ruling 4 · **D-129 §1** |
| 5 | Un-archiving is a state change and **writes an audit record**: who, when, and the old and new status. A refused un-archive writes none | **CLAUDE.md** |
| 6 | Every string is an i18n key. Arabic, RTL, correct at 390px — the control lives on `S-017`, already built to that standard for the archive action it sits beside | CLAUDE.md · `ux/rtl-and-i18n.md` |

## Permissions, money, audit, i18n
- **Permissions:** `CatalogueManage`, `CompanyWide`, no assignment required. Every other role refused
  `403` server-side, including a portal `Role.Client` — hiding the row control is not the control.
- **Money:** moves none and writes no `Posting`. `CostPrice` and `BaseSellRate` are untouched
  [Verified: 2026-09-10 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Unarchive`].
- **Audit:** rule 5, before and after on `status`.
- **i18n:** `catalogue.item.unarchive_action` [Verified: 2026-09-10 @
  `src/Web/src/app/features/catalogue/catalogue-list/catalogue-list-page.html` -> the
  `catalogue.item.unarchive_action` key], plus the existing `errors.master.not_archived`.

## Acceptance criteria

**AC-214-A — un-archiving returns the item to `Active`, with every other field unchanged**
Given an archived catalogue item
When it is un-archived
Then its status is `Active`, and its code, description, unit, باب, cost price and base sell rate are
all exactly what they were before archiving

**AC-214-B — un-archiving an item that is not archived is refused** *(fails if the rule is broken)*
Given an active catalogue item
When it is un-archived
Then it is refused with `errors.master.not_archived`, and its status does not change

**AC-214-C — a role without `CatalogueManage` reaches nothing, and a portal client cannot reach it either** *(fails if the rule is broken)*
Given an archived item, and a signed-in user of each role that does not hold `CatalogueManage`,
including a `Role.Client` portal session
When each calls the un-archive endpoint directly, with no browser involved
Then every call is refused `403`, and the item's status does not change

**AC-214-D — every change is audited before and after, and a refusal writes nothing** *(fails if the rule is broken)*
Given an archived item that is un-archived
When the audit trail is read
Then one record names the actor, the time, and both the old (`Archived`) and new (`Active`) status
And a refused un-archive of `AC-214-B` writes no audit record at all

**AC-214-E — Arabic, RTL, at mobile width**
Given `S-017` and its un-archive control at 390px in Arabic
When it renders
Then direction is RTL, the control's label is readable, no string is a literal in either language,
and the page body does not scroll horizontally

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-214-A` … `AC-214-E` |
| Stable `AC-214-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–6 |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `CatalogueManage`, CompanyWide, no assignment; portal `Role.Client` named explicitly in `AC-214-C` |
| Money behaviour named explicitly | ✅ — moves none, writes no `Posting` |
| Arabic UI strings as i18n keys | ✅ — one key, already shipped, plus the existing refusal key |
| The audit record it writes is stated | ✅ — rule 5, `AC-214-D` |
| **QA has written at least one scenario that fails if the rule is broken** | ✅ Met, with [Verified: 2026-09-11 @ `qa/slice-2/test-cases.md` → `TC-2-099`]. `TC-2-103` (the E2E case for `AC-214-E`) held until a باب can exist on a seeded stack (D-133 §4). |
| Story-currency citations dated with a stable identifier | ✅ — all dated 2026-09-10, re-read today |
| Not `BLOCKED` on an open question | ✅ — `Q66` (D-130 §4) is answered; no new question is raised |

QA has now cased this story as `TC-2-099`…`TC-2-103` in `qa/slice-2/test-cases.md`, and its state lives in the line-3 trailer.

## Not in this story
- **Archiving.** `KAFF-206`.
- **Creating and editing an item, or re-pricing.** `KAFF-202`.
- **The list and its search, including the default filter that excludes archived items.** `KAFF-203`,
  `KAFF-206` rule 7 — this story does not touch the list's default; it only brings one row back to
  `Active`.
- **Anything a BOQ does with an archived or un-archived item.** Slice 4 builds the BOQ; `KAFF-206`'s
  `AC-206-F` and its held slice-4 note are that story's, not this one's.
- **Un-archiving a client or a باب.** `Q39` is answered in the same shape for a client by D-130 §4, but
  building that endpoint is not this story — this story is the catalogue item only, matching the
  commit it is placing.

## Questions
None. `Q66` already answers the one business question this story turns on (D-130 §4), and no new one
is raised by writing criteria against shipped code.
