# KAFF-213 · Archive a باب

<!-- kaff id=KAFF-213 slice=2 points=3 state=NOT-BUILT verdict=none at=- on=2026-09-09 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 3 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Cut 2026-09-09 by the Scrum Master (`decisions.md` D-128 §2) and refined the same day by the BA against `decisions.md` D-130 §5, which rules the business half `KAFF-204`'s own refinement left open (`Q67`). **One Definition-of-Ready box is unticked — QA's cases.**
**Spec:** **§2** (*"~40 trades, tree"*), §4.4 · **Decisions:** D-128 §2 (the story cut), **D-130 §5** (the ruling), D-044 ruling 4
**Register:** `stories/questions-for-karim.md` → **`Q67`** (✅ answered, D-130 §5), **`Q12`** (✅ answered, D-129 §1)
**Screens:** `ux/screen-inventory.md` → **`S-021`** (the tree carries the archive control and the refusal; no new screen)
**Owner:** Backend, then Frontend
**Depends on:** KAFF-204 (the tree), KAFF-206 (the same shape, for the catalogue item)

## Story
As the Technical Office, I retire a باب Kaff no longer uses without breaking the items that still sit
under it, because a tree that can only grow becomes unusable, and a تB that vanished mid-edit while it
still held active work would leave those items pointing at nothing.

## Why this is its own story and not a line in `KAFF-204`, `205` or `206`

**`KAFF-204`'s own refinement found the gap and refused to absorb it.** `Bab.Archive` already exists in
the entity [Verified: 2026-09-09 @ `src/Domain/MasterData/Bab.cs` -> `Archive`] and no slice-2 story
covered calling it. `KAFF-206` is *"archive a catalogue **item**"* — a different entity, and three
points already bought for one; `KAFF-204`/`205` are create-and-markup and re-parent, not archive.
**D-128 §2 recommended a new 3-point story rather than growing an estimated one, and D-130 §5 closes
the business half that made it un-writable — `Q67`.**

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | Archiving sets a باب's `IsActive` to `false`. It is a state change on the row, not a removal of it [Verified: 2026-09-09 @ `src/Domain/MasterData/Bab.cs` -> `Archive`] | §2 |
| 2 | **There is no delete path, and this story must not create one.** The precedent is `AC-206-B` on the catalogue item and `AC-123-D` on the client, both made to fail on purpose so the absence is asserted rather than assumed | **CLAUDE.md** · KAFF-206, KAFF-123 |
| 3 | ✅ **A باب holding active items cannot be archived — D-130 §5.** The refusal **names the count** of active items still filed under it, so the operator knows what stands in the way rather than being told only that the archive failed. **The correct fix is to move the items to another باب (`KAFF-205`) or archive them first (`KAFF-206`) — this story adds neither behaviour, only the refusal that makes one of them necessary** | **D-130 §5** |
| 4 | **The count in rule 3 is of the باب's *own* items, not its subtree's.** أبواب are a tree (`KAFF-204` rule 1) and a باب may have children; this story checks only the items whose `BabId` equals the باب being archived, because a child باب's items are a different باب's problem and are counted when *that* باب is archived. **No cascade counts across the tree** | §2 · D-130 §5's own reasoning against a cascade |
| 5 | **No cascade, under any reading.** D-130 §5 rejects it explicitly: *"archiving forty items behind one click on a tree node is an action nobody can review and nobody can undo in one step."* Archiving a باب never archives an item, never re-parents one, and never touches a child باب | **D-130 §5** |
| 6 | **An archived باب's existing catalogue items stay exactly as they are and stay findable.** The same reasoning `KAFF-206` rule 6 and `Q65` (D-130 §3) give for an item: a signed BOQ holds copies with no foreign key to follow (§4.4), so nothing an archived باب does can reach one — and an item is only unreachable from a *new* line when the **item itself** is archived, not when its باب is. Archiving a باب with no active items leaves its (already-archived, or nonexistent) items exactly where rule 3 already required them to be | §4.4 · D-130 §5 |
| 7 | **Archiving a باب that already holds no items, or only archived ones, succeeds** — rule 3 refuses on *active* items, not on the باب's history of ever having held any | D-130 §5 |
| 8 | Archiving an already-archived باب is refused with `errors.master.already_archived`, not silently accepted — the same shared refusal every archiving master record returns [Verified: 2026-09-09 @ `src/Domain/MasterData/Bab.cs` -> `Archive`] | slice 0, built |
| 9 | **The default tree excludes archived أبواب; they stay findable through an explicit filter.** The same three-state shape `KAFF-206` rule 7 copies from the client list — `Active` (the default), `Archived` alone, `All` — rather than a boolean, and an unknown filter value is refused rather than silently defaulted | D-111 §3 · KAFF-124 rule 2 · KAFF-206 rule 7 |
| 10 | `BabManage`, `CompanyWide`, **no assignment** — أبواب belong to no project. Technical Office settled by §2; the Owner keeps it too | §2 · D-044 ruling 4 · **D-129 §1** |
| 11 | Archiving is a state change and **writes an audit record**: who, when, and the old and new status | **CLAUDE.md** |
| 12 | Every string is an i18n key; Arabic RTL at 390px, indentation by logical property, matching `KAFF-204`'s tree | CLAUDE.md · `ux/rtl-and-i18n.md` |

## Permissions, money, audit, i18n
- **Permissions:** `BabManage`, `CompanyWide`, no assignment required. Every other role refused `403`
  server-side; hiding the archive control on the tree is not the control.
- **Money:** carries no money and moves none. Archiving does not touch `DefaultMarkup`, and no stored
  total anywhere is recomputed, because none is stored.
- **Audit:** rule 11, before and after on `IsActive`.
- **i18n:** `bab.tree.archive_action`, `bab.tree.archive_confirm`, `bab.tree.archived_badge`,
  `bab.tree.filter.include_archived`, `errors.master.bab_has_active_items`, plus the existing
  `errors.master.already_archived`.

⛔ **`errors.master.bab_has_active_items` does not exist yet** — this is the one new key this story
needs, in the same `errors.master.*` namespace D-128 settled for the باب refusal it already renamed.
**Owed by Backend**, alongside the entry in both locale catalogues; nothing under `src/` was changed by
this session.

## Acceptance criteria

**AC-213-A — a باب with no active items is archived**
Given a باب with no catalogue items, or only archived ones
When it is archived
Then its `IsActive` is `false`, the row still exists with every field unchanged, and it is reachable through the tree's explicit archived filter

**AC-213-B — a باب holding active items cannot be archived, and the refusal names the count** *(fails if the rule is broken)*
Given a باب with 12 active catalogue items filed under it
When it is archived
Then the archive is refused with `errors.master.bab_has_active_items`, the refusal names **12** as the count of active items still filed under it, and the باب's `IsActive` remains `true`

**AC-213-C — the count is the باب's own items, not its subtree's** *(fails if the rule is broken)*
Given a parent باب with no items of its own and a child باب with 3 active items
When the parent is archived
Then the parent is archived successfully — its own item count is zero — and the child باب and its 3 items are completely unaffected

**AC-213-D — archiving a باب is not a cascade** *(fails if the rule is broken)*
Given a باب with no active items of its own, a child باب, and an archived item filed directly under the parent
When the parent باب is archived
Then no catalogue item's status changes, no child باب's status changes, and no child باب is re-parented — the mapped routes this story adds are enumerated as an allow-list, and none of them touches a `CatalogueItem` or another `Bab` row

**AC-213-E — moving or archiving the items first clears the refusal** *(fails if the rule is broken)*
Given a باب refused for archiving under `AC-213-B`
When its active items are archived (`KAFF-206`) or moved to another باب (`KAFF-205`) until none remain active under it
Then the باب can now be archived successfully

**AC-213-F — an archived باب's existing items stay findable** *(fails if the rule is broken)*
Given a باب archived while it held items that were themselves already archived
When the catalogue's explicit archived filter is used
Then those items still appear, still carrying their باب, and no field on any signed BOQ line referencing them has changed

**AC-213-G — archiving twice is refused**
Given an already-archived باب
When it is archived again
Then it is refused with `errors.master.already_archived`, and nothing is written to the audit trail for the refusal

**AC-213-H — a role without `BabManage` archives nothing** *(fails if the rule is broken)*
Given a signed-in user of each role that does not hold `BabManage`
When each calls the archive endpoint directly, with no browser involved
Then every call is refused `403`, and no باب's status has changed

**AC-213-I — archiving is audited before and after** *(fails if the rule is broken)*
Given an archived باب
When the audit trail is read
Then a record names the actor, the time, and the old and new status
And the refused archive of `AC-213-B` and the refused re-archive of `AC-213-G` each produced no record

**AC-213-J — Arabic, RTL, at mobile width**
Given `S-021` and its archive control at 390px in Arabic
When they render
Then direction is RTL, the archived badge and the refusal's item count are readable, the count is bidi-isolated, no string is a literal in either language, and the page body does not scroll horizontally

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-213-A` … `AC-213-J` |
| Stable `AC-213-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–12, all cited |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `BabManage`, CompanyWide, no assignment; the Owner grant confirmed by **D-129 §1** |
| Money behaviour named explicitly | ✅ — carries none, moves none |
| Arabic UI strings as i18n keys | ✅ — four new keys plus one existing |
| The audit record it writes is stated | ✅ — rule 11, `AC-213-I` |
| **QA has written at least one scenario that fails if the rule is broken** | ✅ **Met 2026-09-09** — `qa/slice-2/test-cases.md`, `TC-2-001`…`TC-2-098`. |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ — `Q67` (D-130 §5) and `Q12` (D-129 §1) are both answered |

**Flip the trailer to `READY` when QA's cases land.** Everything else is ruled.

## Not in this story
- **Creating a باب, its markup, or re-parenting it.** `KAFF-204`, `KAFF-205`.
- **Archiving a catalogue item.** `KAFF-206` — the same shape, a different entity, and the reason this
  story exists separately rather than folded into it.
- **Un-archiving a باب.** `Q66` (D-130 §4) rules that un-archiving is wanted for master records in
  general; which story builds it for a باب specifically is unassigned, the same open placement question
  `KAFF-206` records for the catalogue item.
- **Merging two أبواب, or deleting one.** Neither is in `stories/backlog.md`'s slice-2 table and neither
  is in `spec.md`. Not invented here.
- **Anything a BOQ does with an archived باب's section.** Slice 4 builds the BOQ; §4.5's *"auto-creates
  its باب section if absent"* is unaffected by this story either way.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q67`** | ✅ **ANSWERED — D-130 §5.** A باب holding active items cannot be archived; the refusal names the count and the operator moves or archives the items first. No cascade | **Closed** |
| **`Q12`** | ✅ **ANSWERED — D-129 §1.** The Owner keeps `BabManage` | **Closed** |
