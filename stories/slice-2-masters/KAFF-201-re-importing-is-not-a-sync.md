# KAFF-201 · Re-importing is not a sync — a second import is a deliberate, reviewed act

<!-- kaff id=KAFF-201 slice=2 points=2 state=NOT-BUILT verdict=none at=- on=2026-09-08 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 2 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-08 by the BA. **⚠️ Amended 2026-09-09 — `Q62` and `Q12` are both answered (D-129 §4, D-129 §1) and `AC-201-F` is written. One Definition-of-Ready box is still unticked — see *Definition of Ready* below.**
**Spec:** **§4.1** (*"Excel import is not an ongoing sync"*), §4.4 · **Decisions:** **D-129 §4, D-129 §1**
**Register:** `stories/questions-for-karim.md` → **`Q62`** (✅ answered, D-129 §4), **`Q12`** (✅ answered, D-129 §1)
**Screens:** `ux/screen-inventory.md` → **`S-019`**
**Owner:** Backend, then Frontend
**Depends on:** KAFF-200

## Story
As the Technical Office, when I import a second spreadsheet over a catalogue that is already loaded, I
want the system to treat it as the deliberate act it is — not as a silent refresh — because a price
list that quietly reloaded itself is indistinguishable from one that was edited by hand, and only one
of those two is auditable.

## ✅ What `spec.md` said, what it left open, and how `Q62` closed it

`spec.md` §4.1 is one sentence on this subject and it is a **prohibition**:

> *"Loaded from Excel at setup. Edited manually after. **Excel import is not an ongoing sync.**"*

**That ruled out one behaviour and named no replacement.** It said the catalogue must not track a
spreadsheet. It did not say what happens when somebody imports a second file, and there were at least
three defensible readings:

| Reading | What a second import does | What it costs if it is the wrong one |
|---|---|---|
| **Refuse** | The import screen is available only while the catalogue is empty, the same shape as S-002's one-time Owner setup | Kaff cannot load a corrected sheet after a bad first import, and must fix hundreds of rows through `KAFF-202` |
| **Add only** | New codes are created; a code that already exists is left exactly as it is | A price correction in the sheet is silently ignored, which is the failure mode that reads as a working import |
| **Add and re-price** | New codes created, existing codes re-priced through `Reprice` [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Reprice`] | This is a sync in everything but name, and §4.1 forbids exactly that |

**Nabil ruled it — D-129 §4.** *"A second import is accepted, its effect shown, and confirmed by a
human"* — which is the third reading, gated: new codes are created **and** existing codes are re-priced,
but nothing changes until the operator has seen exactly what will change and confirmed it. This is
`KAFF-201`'s own board title, *"a deliberate, reviewed act"*, honoured as the gate in front of the
chosen reading rather than as a fourth answer, and it satisfies §4.1's prohibition precisely because a
human reviews and confirms every time — that is what keeps it from being a sync.

**`AC-201-F` is written below against this ruling.**

## The rules, now that `Q62` is answered

| # | Rule | Source |
|---|---|---|
| 1 | The catalogue never tracks a spreadsheet. There is no scheduled import, no watched folder, no *"re-sync"* control, and no background job that reads a file | §4.1 |
| 2 | A second import **creates new codes and re-prices existing ones**, and does neither until a person confirms: the confirmation states what is about to change **before** it changes — count of items to be created, and count of items to be re-priced, naming each one's old and new price | **D-129 §4** · §4.1 · `ux/components.md` §11 (`kaff-confirm-dialog`) |
| 3 | **A signed BOQ cannot be reached by any import, ever.** The BOQ holds copies of catalogue values with no foreign key to follow, so a re-priced item cannot change a signed contract | **§4.4** — MUST |
| 4 | **Open, unsigned estimates re-price only through an explicit review**, never automatically: *"the system alerts 'you have X open offers on old pricing' and a human decides per estimate"*. **That surface is `S-049` and it is slice 4** — this story raises no alert, and must not be built to | §4.4 · `ux/screen-inventory.md` -> `S-049` |
| 5 | Whatever the import changes, **it writes an audit record** naming who, when, the file, and what changed. If existing items are re-priced, the record carries the before and after of each price it moved | **CLAUDE.md** — *"Every state change writes an audit record … what changed (before and after)"* |
| 6 | `CatalogueManage`, `CompanyWide`, no assignment. Technical Office settled by §2; **the Owner keeps it too** | §2 · D-044 ruling 4 · **D-129 §1** |
| 7 | Every string on S-019's confirmation is an i18n key | CLAUDE.md |

## Permissions, money, audit, i18n
- **Permissions:** `CatalogueManage`, `CompanyWide`, no assignment required. Same row as `KAFF-200`.
- **Money:** may change `costPrice` and `baseSellRate` — a second import both creates and re-prices
  (D-129 §4) — at `decimal(18,4)` [Verified: 2026-09-08 @ `src/Infrastructure/Persistence/KaffDbContext.cs` -> `ConfigureConventions`]. **Moves no money and writes no `Posting`.**
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

**AC-201-F — a second import creates, re-prices, and only after confirmation** *(fails if the rule is broken)*
Given a catalogue already holding items, and a second file that adds three new codes and re-prices two existing ones
When the operator confirms
Then the three new items are created, the two existing items carry the file's new prices, and every other item is untouched
And every one of the five changes is named in the confirmation shown **before** the operator confirmed it — this criterion is what `Q62` (D-129 §4) makes writable

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-201-A` … `AC-201-F` |
| Stable `AC-201-<LETTER>` ids | ✅ — `F` is now written; the next takes `G` |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–7, all cited |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `CatalogueManage`, CompanyWide, no assignment; the Owner grant confirmed by **D-129 §1** |
| Money behaviour named explicitly | ✅ |
| Arabic UI strings as i18n keys | ✅ — six keys |
| The audit record it writes is stated | ✅ — rule 5, `AC-201-E` |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist; no `TC-` range. **QA's to write** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ — `Q62` (D-129 §4) and `Q12` (D-129 §1) are both answered |

**Flip the trailer to `READY` when QA's cases land.** Everything else is ruled.

## Not in this story
- **The first import.** `KAFF-200`.
- **§4.4's open-offer review.** `S-049`, slice 4, and rule 4 exists to stop it being built here.
- **Any BOQ or estimate behaviour at all.**
- **Editing one item by hand.** `KAFF-202` — which is §4.1's *"edited manually after"* and is the path
  that exists alongside this one.

## Questions

| # | Question, as Nabil should ask it | Owner |
|---|---|---|
| **`Q62`** | ✅ **ANSWERED — D-129 §4.** A second import is accepted, its effect is shown, and it is confirmed by a human. `AC-201-F` is written against it | **Closed** |
| **`Q12`** | ✅ **ANSWERED — D-129 §1.** The Owner keeps `CatalogueManage` | **Closed** |
