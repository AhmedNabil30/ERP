# KAFF-201 · Re-importing is not a sync — a second import is a deliberate, reviewed act

<!-- kaff id=KAFF-201 slice=2 points=2 state=NOT-BUILT verdict=none at=- on=2026-09-08 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 2 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-08 by the BA. **This story is blocked at its centre, not at its edges — `Q62` decides what it does at all.**
**Spec:** **§4.1** (*"Excel import is not an ongoing sync"*), §4.4 · **Decisions:** —
**Register:** `stories/questions-for-karim.md` → **`Q62`** (new, raised by this story, **and it is the whole story**), `Q12`
**Screens:** `ux/screen-inventory.md` → **`S-019`**
**Owner:** Backend, then Frontend
**Depends on:** KAFF-200

## Story
As the Technical Office, when I import a second spreadsheet over a catalogue that is already loaded, I
want the system to treat it as the deliberate act it is — not as a silent refresh — because a price
list that quietly reloaded itself is indistinguishable from one that was edited by hand, and only one
of those two is auditable.

## ⛔ What `spec.md` says, and the exact shape of what it does not

`spec.md` §4.1 is one sentence on this subject and it is a **prohibition**:

> *"Loaded from Excel at setup. Edited manually after. **Excel import is not an ongoing sync.**"*

**That rules out one behaviour and names no replacement.** It says the catalogue must not track a
spreadsheet. It does not say what happens when somebody imports a second file, and there are at least
three answers, each defensible and each producing a different system:

| Reading | What a second import does | What it costs if it is the wrong one |
|---|---|---|
| **Refuse** | The import screen is available only while the catalogue is empty, the same shape as S-002's one-time Owner setup | Kaff cannot load a corrected sheet after a bad first import, and must fix hundreds of rows through `KAFF-202` |
| **Add only** | New codes are created; a code that already exists is left exactly as it is | A price correction in the sheet is silently ignored, which is the failure mode that reads as a working import |
| **Add and re-price** | New codes created, existing codes re-priced through `Reprice` [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Reprice`] | This is a sync in everything but name, and §4.1 forbids exactly that |

**The board's own title — *"a deliberate, reviewed act"* — is not a fourth answer.** It describes a
gate in front of whichever of the three is chosen: a confirmation that says what is about to happen
before it happens. **What is about to happen is `Q62`, and it is Karim's.**

**A previous session could have closed this from the title alone, and would have been wrong to.** The
title is the board's phrasing of the intent; it is not a ruling, and *"deliberate and reviewed"* is
satisfied by all three readings.

## What is safe to say today, whichever way `Q62` is answered

These hold under all three readings and are written as rules because they are sourced, not because
they fill the gap:

| # | Rule | Source |
|---|---|---|
| 1 | The catalogue never tracks a spreadsheet. There is no scheduled import, no watched folder, no *"re-sync"* control, and no background job that reads a file | §4.1 |
| 2 | A second import is an act a person performs and confirms, and the confirmation states what is about to change **before** it changes — count of items to be created, and count to be affected | §4.1 · `ux/components.md` §11 (`kaff-confirm-dialog`) |
| 3 | **A signed BOQ cannot be reached by any import, ever.** The BOQ holds copies of catalogue values with no foreign key to follow, so a re-priced item cannot change a signed contract | **§4.4** — MUST |
| 4 | **Open, unsigned estimates re-price only through an explicit review**, never automatically: *"the system alerts 'you have X open offers on old pricing' and a human decides per estimate"*. **That surface is `S-049` and it is slice 4** — this story raises no alert, and must not be built to | §4.4 · `ux/screen-inventory.md` -> `S-049` |
| 5 | Whatever the import changes, **it writes an audit record** naming who, when, the file, and what changed. If existing items are re-priced, the record carries the before and after of each price it moved | **CLAUDE.md** — *"Every state change writes an audit record … what changed (before and after)"* |
| 6 | `CatalogueManage`, `CompanyWide`, no assignment. Technical Office settled by §2; the Owner grant stands on **`Q12`** | §2 · D-044 ruling 4 · **`Q12`** |
| 7 | Every string on S-019's confirmation is an i18n key | CLAUDE.md |

## Permissions, money, audit, i18n
- **Permissions:** `CatalogueManage`, `CompanyWide`, no assignment required. Same row as `KAFF-200`.
- **Money:** may change `costPrice` and `baseSellRate` under two of the three readings, at
  `decimal(18,4)` [Verified: 2026-09-08 @ `src/Infrastructure/Persistence/KaffDbContext.cs` -> `ConfigureConventions`]. **Moves no money and writes no `Posting`.**
- **Audit:** rule 5, with before-and-after on every price it moves.
- **i18n:** `catalogue.import.second_import_title`, `catalogue.import.second_import_body`,
  `catalogue.import.will_create`, `catalogue.import.will_affect`, `action.confirm`, `action.cancel`.

## Acceptance criteria

**AC-201-A — nothing imports itself** *(fails if the rule is broken)*
Given a catalogue loaded from a file, and that file changed afterwards on disk or anywhere else
When the system runs for any length of time with nobody importing anything
Then no catalogue item changes, and no background job, schedule or watcher reads a spreadsheet

**AC-201-B — a second import states what it will do before it does it** *(fails if the rule is broken)*
Given a catalogue that already holds items, and a second file chosen on S-019
When the operator confirms nothing yet
Then a confirmation names the counts — how many items would be created and how many existing items the file touches — and **no item has changed at the moment that confirmation is shown**
And cancelling leaves the catalogue byte-for-byte as it was

**AC-201-C — a signed BOQ is untouched by any import** *(fails if the rule is broken)*
Given a signed BOQ carrying an item's values at signature time
When any import runs against that item's catalogue row, changing its price
Then every value on the signed BOQ line is unchanged, and no query path exists from the BOQ line back to the catalogue row

**AC-201-D — the import raises no re-pricing alert** *(fails if the rule is broken)*
Given open, unsigned estimates referencing items the import touches
When the import completes
Then nothing on any estimate changes and no alert is raised by this story — §4.4's review is `S-049`, slice 4

**AC-201-E — the second import is audited with before and after** *(fails if the rule is broken)*
Given a second import that changes any stored value
When the audit trail is read
Then it names the actor, the time and the file, and carries the before and the after of every value it moved

**AC-201-F — ⏳ HELD on `Q62`: what the second import actually does**
**Not written, deliberately.** Whether a second import is refused, adds only, or adds and re-prices is the question this story exists to implement and `spec.md` answers none of it. Every other criterion here is true under all three readings; this one cannot be. **`Q62`.**

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-201-A` … `AC-201-E` |
| Stable `AC-201-<LETTER>` ids | ✅ — `F` allocated and held; the next takes `G` |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–7, all cited |
| No uncited rule | ✅ — **and that is exactly why the story is not Ready**: the rule that would need citing has not been written, because it has not been ruled |
| Permissions named explicitly | ✅ — `CatalogueManage`, CompanyWide, no assignment; the Owner half flagged to `Q12` |
| Money behaviour named explicitly | ✅ |
| Arabic UI strings as i18n keys | ✅ — six keys |
| The audit record it writes is stated | ✅ — rule 5, `AC-201-E` |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist; no `TC-` range. **QA's to write** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ⛔ **Not met — `Q62`, and it is not at the edge of this story but at its centre.** Two of the three readings make it a 2-point change and one makes it a one-line gate |

**Flip the trailer to `READY` when `Q62` is ruled, `AC-201-F` is written against the D-number that
rules it, and QA's cases land.**

## Not in this story
- **The first import.** `KAFF-200`.
- **§4.4's open-offer review.** `S-049`, slice 4, and rule 4 exists to stop it being built here.
- **Any BOQ or estimate behaviour at all.**
- **Editing one item by hand.** `KAFF-202` — which is §4.1's *"edited manually after"* and is the path
  that exists whatever `Q62` decides.

## Questions

| # | Question, as Nabil should ask it | Owner |
|---|---|---|
| **`Q62`** | **"You load the price list from a spreadsheet once, at the start. If somebody imports a second spreadsheet later — say the prices were updated, or the first file was wrong — what should the system do? Refuse it, and make them fix the items one at a time on the screen? Add only the items that are new and leave the existing ones alone? Or update the prices of the ones that are already there as well?"** Say why it is being asked: you told us the import is not an ongoing sync, which tells us what it must **not** be and not what it should do instead. **New, raised by this story. Blocking** | **Karim** |
| **`Q12`** | Open, slice-2-wide. Whether the Owner keeps `CatalogueManage` | **Karim** |
