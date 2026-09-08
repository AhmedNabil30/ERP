# KAFF-205 · Re-parent a باب, and move an item between أبواب

<!-- kaff id=KAFF-205 slice=2 points=3 state=NOT-BUILT verdict=none at=- on=2026-09-08 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 3 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-08 by the BA. **Two Definition-of-Ready boxes are unticked, and this story carries a live defect finding — see rule 3.**
**Spec:** **§2** (*"tree"*), **§4.2**, **§4.4**, §4.5 · **Decisions:** D-044 ruling 4
**Register:** `stories/questions-for-karim.md` → **`Q12`** (open, slice-2-wide)
**Screens:** `ux/screen-inventory.md` → **`S-021`**, **`S-022`**, **`S-018`**
**Owner:** Backend, then Frontend
**Depends on:** KAFF-204 (the tree), KAFF-202 (the item)

## Story
As the Technical Office, I move a باب under a different parent and move an item from one باب to
another, because a trade tree set up once at setup is wrong within a year and the alternative to
moving a node is retyping every item beneath it.

## ⛔ The finding this story exists around

**`Bab.SetParent` prevents a باب being its own parent and nothing else** [Verified: 2026-09-08 @
`src/Domain/MasterData/Bab.cs` -> `SetParent`]. A two-step cycle satisfies that guard completely:

```
A.SetParent(B)   → allowed, B is not A
B.SetParent(A)   → allowed, A is not B      ← and now neither is reachable from a root
```

**`spec.md` §2 says the أبواب are a *tree*.** A structure with a cycle is not one, and the failure is
not cosmetic: a tree walk over it does not terminate, and every باب in the cycle disappears from
`S-021` because no root reaches it.

**This is a defect finding, not a business question.** Nothing needs to be asked of Karim to know
that a tree has no cycles. It is stated here, cited, and **routed to Backend** — this story is where
the guard belongs, because this story is what makes a longer cycle reachable in the first place.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | A باب may be moved to a different parent, or to no parent, becoming a root | §2 — a tree with a mutable shape; the behaviour exists [Verified: 2026-09-08 @ `src/Domain/MasterData/Bab.cs` -> `SetParent`] |
| 2 | **A move takes the whole subtree.** Children stay children of the باب that moved; nothing is orphaned and nothing is re-parented to a grandparent behind the operator's back | §2 |
| 3 | ⛔ **A باب may not become its own ancestor, at any depth.** The current guard catches depth one only [Verified: 2026-09-08 @ `src/Domain/MasterData/Bab.cs` -> `SetParent`]. **This story adds the ancestor check** | **§2** |
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
  `errors.bab.cycle`, `errors.bab.cannot_be_own_ancestor`.

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
Then it is refused with `errors.bab.cannot_be_own_ancestor`
And the two-step case is refused the same way: with B's parent set to A, setting A's parent to B is refused
And after every refusal, every باب is still reachable from a root

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
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated. Six criteria are marked *(fails if the rule is broken)*, and **`AC-205-C` is the one that is red against the code as it stands today** — a case for it would fail on first run, which is what a case is for. **QA's to write** |
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
| 1 | **The missing ancestor check is a defect, not a question** — §2 says *tree* and a cycle is not one. `AC-205-C` is the criterion; the guard belongs in the entity beside the self-parent check it extends. **Routed to Backend**, and named here so it is not read as this story inventing a rule | **Backend** |
