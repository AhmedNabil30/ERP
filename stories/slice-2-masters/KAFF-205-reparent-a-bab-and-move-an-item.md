# KAFF-205 · Re-parent a باب, and move an item between أبواب

<!-- kaff id=KAFF-205 slice=2 points=3 state=NOT-BUILT verdict=none at=- on=2026-09-08 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 3 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-08 by the BA; **amended 2026-09-09** — the cycle defect it carried is **repaired** (D-127 §1) and its refusal key is **settled** (D-128). **Two Definition-of-Ready boxes are unticked.**
**Spec:** **§2** (*"tree"*), **§4.2**, **§4.4**, §4.5 · **Decisions:** D-044 ruling 4
**Register:** `stories/questions-for-karim.md` → **`Q12`** (open, slice-2-wide)
**Screens:** `ux/screen-inventory.md` → **`S-021`**, **`S-022`**, **`S-018`**
**Owner:** Backend, then Frontend
**Depends on:** KAFF-204 (the tree), KAFF-202 (the item)

## Story
As the Technical Office, I move a باب under a different parent and move an item from one باب to
another, because a trade tree set up once at setup is wrong within a year and the alternative to
moving a node is retyping every item beneath it.

## ✅ The finding this story existed around — **REPAIRED 2026-09-08, D-127**

> ### ⚠️ This section read as a live defect until 2026-09-09 and was stale. Corrected under SM-29, not rewritten.
>
> **What it said, and it was true when written on 2026-09-08:** `Bab.SetParent` prevented a باب being
> its own parent and nothing else, so `A.SetParent(B)` then `B.SetParent(A)` was accepted in full and
> neither باب was reachable from a root. **`spec.md` §2 says the أبواب are a *tree*.** It was routed
> to Backend as a defect rather than raised as a question, because nothing needs asking of Karim to
> know a tree has no cycles.

**Backend fixed it the same day.** `SetParent` now walks the candidate parent's ancestors and refuses
a cycle **at any depth**, taking every باب's parent pointer so the walk can see a tree an entity
cannot [Verified: 2026-09-09 @ `src/Domain/MasterData/Bab.cs` -> `SetParent`]. The walk is bounded by
the size of the tree, so a cycle written before the guard existed **fails rather than hangs**. Watched
red first at 3 of 5. `decisions.md` **D-127** §1.

⛔ **What is still owed is this story's, and it is `AC-205-C`'s key — see rule 3a.** The guard is
right; the sentence it returns is not.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | A باب may be moved to a different parent, or to no parent, becoming a root | §2 — a tree with a mutable shape; the behaviour exists [Verified: 2026-09-08 @ `src/Domain/MasterData/Bab.cs` -> `SetParent`] |
| 2 | **A move takes the whole subtree.** Children stay children of the باب that moved; nothing is orphaned and nothing is re-parented to a grandparent behind the operator's back | §2 |
| 3 | **A باب may not become its own ancestor, at any depth.** ✅ **Built 2026-09-08** — the guard walks the candidate parent's ancestors and refuses at any depth, bounded by the size of the tree [Verified: 2026-09-09 @ `src/Domain/MasterData/Bab.cs` -> `SetParent`]. **This story no longer adds the check; it calls it** — and `SetParent` now takes every باب's parent pointer, which is **one query for the handler to read** | **§2** · D-127 §1 |
| 3a | ⛔ **The refusal must say what it refused.** The guard returns `MasterDataErrors.BabCannotBeItsOwnParent` [Verified: 2026-09-09 @ `src/Domain/MasterData/Bab.cs` -> `SetParent`], whose text in both catalogues is *"cannot be its own parent"* — **true of the two-step case and false of the deeper one**, where the باب is being made its own grandparent and no parent relationship is what is refused. **The identifier and the key are renamed rather than a second error added** — see *The refusal key* below | **SM-33** (a name a change makes **false** is renamed) · D-127 §1's own routing |
| 4 | An item may be moved from one باب to another. The item's باب is a required field and is not nullable [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `BabId`], so a move is a change of value, never a removal | §4.1 · §2 |
| 5 | **Moving an item changes what a *future* BOQ line defaults to, and moves no existing line.** §4.2's markup *"defaults from the item's باب"* at the moment a line is created; §4.4 makes a signed BOQ a set of copies with no foreign key to follow, and re-prices an open estimate only through the explicit human review of `S-049`, slice 4. **The correct implementation of this rule is to add nothing** — no cascade, no recalculation, no notification | **§4.4** · §4.2 |
| 6 | **Moving a باب changes no markup.** The markup belongs to the باب that moved, travels with it, and is not replaced by the new parent's — there is no inheritance (`KAFF-204` rule 2) | §4.2 |
| 7 | Both moves are state changes and **each writes an audit record**: who, when, and the before and after of the parent or the باب. **This is the audit record somebody will actually need**, because a moved item silently changes what every future line under it starts at | **CLAUDE.md** |
| 8 | `BabManage` for the باب move; `CatalogueManage` for the item move. Both `CompanyWide`, **no assignment**. Technical Office settled by §2; the Owner grants stand on **`Q12`** | §2 · D-044 ruling 4 · **`Q12`** |
| 9 | Every string is an i18n key; Arabic RTL at 390px, indentation by logical property | CLAUDE.md · `ux/rtl-and-i18n.md` |

## Permissions, money, audit, i18n
- **Permissions:** `BabManage`, `CompanyWide`, no assignment — the باب move.
  `CatalogueManage`, `CompanyWide`, no assignment — the item move. Every other role `403`.
- **Money:** moves none, shows none, writes no `Posting`. It changes which default a **future** line
  starts from and no stored amount anywhere.
- **Audit:** rule 7 — before and after on the parent, and on the item's باب.
- **i18n:** `bab.move.title`, `bab.move.confirm`, `bab.move.new_parent`,
  `catalogue.item.move_title`, `catalogue.item.move_confirm`, `catalogue.item.new_bab`,
  **`errors.master.bab_cannot_be_its_own_ancestor`** — see below.

## ⛔ The refusal key — settled 2026-09-09 by the BA, and what it costs to implement

**This story named `errors.bab.cannot_be_own_ancestor`, and it named it twice** — in the i18n list and
in `AC-205-C`. **Two things were wrong with it and Backend correctly declined to decide either**
(D-127 §1, *"Reported, not decided"*):

1. ⛔ **The key exists in neither catalogue** [Verified: 2026-09-09 @ `src/Web/public/locales/en.json` -> `errors.master.bab_cannot_be_its_own_parent`
   — that is the only باب refusal in the file, and there is no `errors.bab.` entry in either catalogue].
2. ⛔ **`errors.bab.*` is a namespace this codebase does not use.** Every shipped master-data refusal
   is `errors.master.*`, one namespace per domain error catalogue class
   [Verified: 2026-09-09 @ `src/Domain/MasterData/MasterDataErrors.cs` -> `BabCannotBeItsOwnParent`].

**This is a naming question and a criterion question, not a business one. It is the BA's, and it is
settled here rather than sent to Karim.** `decisions.md` **D-128**.

**The ruling: `errors.master.bab_cannot_be_its_own_ancestor`, by renaming the existing error rather
than adding a second one.**

| | |
|---|---|
| **Namespace** | `errors.master.*`. It is what ships, and a second namespace would put two باب refusals in two places for one rule |
| **One error, not two** | *"Own ancestor"* is true of depth one as well — a parent **is** an ancestor — so a separate self-parent error would be two messages for one guard, and the handler would have to choose between them at a depth the entity does not report |
| **Renamed, not added** | An added key leaves the false one in place. `TranslationCatalogueTests` would stay green: `Every_screen_key_in_the_catalogues_is_read_by_a_template_or_a_component` is **scoped to screen prefixes and excludes `errors.*` deliberately** [Verified: 2026-09-09 @ `tests/Domain.Tests/TranslationCatalogueTests.cs` -> `Every_screen_key_in_the_catalogues_is_read_by_a_template_or_a_component`], so an orphaned `errors.*` key is invisible to every gate this repository has. **The rename is what removes it** |
| **SM-33 applies and says rename** | The law distinguishes a name a change makes **narrow** (which stays) from one it makes **false** (which is renamed in the same change). *"Cannot be its own parent"* is **false** of the refusal it now returns for `A → B → C → A`. D-127 §2 made the same call on a test name the same day |

**What is owed — three files plus the tests, and none of it is the BA's to write:**

| # | Owed | Where |
|---|---|---|
| 1 | Rename `BabCannotBeItsOwnParent` → **`BabCannotBeItsOwnAncestor`**, with code `master.bab_cannot_be_its_own_ancestor` and message key `errors.master.bab_cannot_be_its_own_ancestor` | `src/Domain/MasterData/MasterDataErrors.cs` |
| 2 | The two call sites in the guard | `src/Domain/MasterData/Bab.cs` -> `SetParent` |
| 3 | The four references, **plus an SM-33 breadcrumb naming the old identifier in the renamed test's `<summary>`**, so D-127's prose still lands a reader | `tests/Domain.Tests/BabTreeTests.cs` |
| 4 | ⛔ **The entry in BOTH catalogues, renamed in the same commit or `TranslationCatalogueTests` goes red** — `Every_domain_error_key_has_an_arabic_and_an_english_translation` and `The_two_catalogues_describe_the_same_set_of_keys` both fail on a one-sided edit | `src/Web/public/locales/en.json`, `src/Web/public/locales/ar.json` |

**Proposed text.** English: *"A باب cannot be its own ancestor."* Arabic: *"لا يمكن أن يكون الباب
أصلاً لنفسه."* — replacing *"الباب لا يكون أباً لنفسه."* ⚠️ **Arabic is the product language and the
BA is not ruling its wording**: the sentence must say *ancestor*, not *father*, and the exact phrasing
is the Frontend's or Nabil's to confirm.

⛔ **Nothing under `src/` was changed by this session** — the BA may not write there. This table is the
route, with the exact key already decided so the implementer chooses nothing.

## Acceptance criteria

**AC-205-A — a باب moves, and its children move with it**
Given باب A with children B and C, and a second root D
When A is re-parented to D
Then A is a child of D, B and C are still children of A, and every one of the four is still reachable from a root

**AC-205-B — a باب becomes a root**
Given a child باب
When its parent is cleared
Then it is a root, its own children are unchanged, and it renders at the top level of S-021

**AC-205-C — a باب cannot become its own ancestor, at any depth** *(fails if the rule is broken)*
Given the chain A → B → C, where C's parent is B and B's parent is A
When A is re-parented to C
Then it is refused with **`errors.master.bab_cannot_be_its_own_ancestor`** — the one key, in the namespace every shipped master-data refusal uses, and it is a **rename** of `errors.master.bab_cannot_be_its_own_parent` rather than a second key beside it
And the two-step case is refused the same way: with B's parent set to A, setting A's parent to B is refused
And the self-parent case is refused with **that same key**, because a parent is an ancestor and one guard returns one message
And after every refusal, every باب is still reachable from a root
And the key resolves to a sentence in **both** catalogues, so the refusal reaches the screen as text rather than as its own name

**AC-205-D — an item moves between أبواب**
Given an item in باب A
When it is moved to باب B
Then the item's باب is B, and it appears under B and no longer under A in the catalogue list

**AC-205-E — a move does not touch a signed BOQ** *(fails if the rule is broken)*
Given a signed BOQ line for an item, created while that item was in a باب at 15%
When the item is moved to a باب at 30%, and separately when its old باب is re-parented
Then every value on the signed BOQ line is unchanged, including its markup
And no field on the line holds a foreign key to the catalogue row or to either باب

**AC-205-F — a move re-prices no open estimate and raises no alert** *(fails if the rule is broken)*
Given open, unsigned estimate lines for the item
When the item is moved to a باب with a different markup
Then no estimate line changes, and this story raises no alert — §4.4's review is `S-049`, slice 4

**AC-205-G — the new default reaches the next line only**
Given an item moved from a باب at 15% to a باب at 30%
When a **new** BOQ line is started from that item
Then it defaults to 30%, while every line created before the move still carries 15%

**AC-205-H — a role without the permission moves nothing** *(fails if the rule is broken)*
Given a signed-in user of each role holding neither `BabManage` nor `CatalogueManage`
When each calls the باب-move endpoint and then the item-move endpoint directly, with no browser involved
Then every call is refused `403`, and no باب and no item has moved

**AC-205-I — both moves are audited before and after** *(fails if the rule is broken)*
Given a باب re-parented and an item moved
When the audit trail is read
Then each has a record naming the actor, the time, and the old and new value of what moved

**AC-205-J — Arabic, RTL, at mobile width**
Given the move surfaces at 390px in Arabic
When they render
Then direction is RTL, the tree indents from the inline-start, no string is a literal in either language, and the page body does not scroll horizontally

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-205-A` … `AC-205-J` |
| Stable `AC-205-<LETTER>` ids | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–9 |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — two permissions, both CompanyWide, no assignment; the Owner halves flagged to `Q12` |
| Money behaviour named explicitly | ✅ — moves none; rules 5 and 6 say precisely what a move does **not** reach |
| Arabic UI strings as i18n keys | ✅ — eight keys |
| The audit record it writes is stated | ✅ — rule 7, `AC-205-I` |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated. Six criteria are marked *(fails if the rule is broken)*. ⚠️ **This row said `AC-205-C` was red against the code — that was true on 2026-09-08 and is no longer.** The guard landed the same day (D-127 §1) and five domain tests hold it [Verified: 2026-09-09 @ `tests/Domain.Tests/BabTreeTests.cs` -> `BabTreeTests`]. **What is red today is the key it names**, and the repair is routed above, not asked. **QA's cases are still to write** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ⛔ **Not met — `Q12`**, the slice-wide grant. **No question of this story's own is open** — the cycle finding is a defect, not a question, and is routed to Backend |

**Flip the trailer to `READY` when `Q12` is ruled and QA's cases land.**

## Not in this story
- **Creating a باب or an item.** `KAFF-204` and `KAFF-202`.
- **Archiving either.** `KAFF-206` for the item; **the باب has no archive story** — recorded in
  `KAFF-204`'s questions, not absorbed here.
- **Merging two أبواب, or deleting one.** Neither is in `stories/backlog.md`'s slice-2 table and
  neither is in `spec.md`. **Not invented here.**
- **Re-pricing anything.** `KAFF-202` re-prices an item; §4.4's open-offer review is `S-049`, slice 4.
- **The BOQ's باب sections.** §4.5, `S-054`, slice 4.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q12`** | Open, slice-2-wide. Whether the Owner keeps `BabManage` and `CatalogueManage` | **Karim** |
| 1 | ~~**The missing ancestor check is a defect, not a question**~~ — ✅ **CLOSED 2026-09-08, D-127 §1.** Routed as a defect and repaired the same day, at any depth, bounded so a pre-existing cycle fails rather than hangs [Verified: 2026-09-09 @ `src/Domain/MasterData/Bab.cs` -> `SetParent`]. **Kept struck rather than deleted** — what the board claimed and when is the record | ~~Backend~~ **done** |
| 2 | **The refusal key is settled, 2026-09-09, and it is the BA's own call rather than Karim's** — `errors.master.bab_cannot_be_its_own_ancestor`, by rename. See *The refusal key* above and `decisions.md` **D-128**. **What remains is implementation and it is routed with the exact name**, so nobody chooses one | **Backend**, then **Frontend** for the two catalogue entries |
