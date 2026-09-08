# KAFF-204 · The باب tree, carrying each trade's default markup

<!-- kaff id=KAFF-204 slice=2 points=5 state=NOT-BUILT verdict=none at=- on=2026-09-08 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 5 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-08 by the BA; **amended 2026-09-09** — rule 5's defect is **repaired** (D-127 §1) and both `errors.bab.*` keys are **renamed to the namespace that ships** (D-128). **Two Definition-of-Ready boxes are unticked.**
**Spec:** **§2** (*"~40 trades, tree, carries default markup %"*), **§4.2**, §4.5 · **Decisions:** D-044 ruling 4, D-044 ruling 6 (four decimals stored, two displayed)
**Register:** `stories/questions-for-karim.md` → **`Q12`** (open, slice-2-wide), **`Q60`** (the tree's own data, raised by `KAFF-200`)
**Screens:** `ux/screen-inventory.md` → **`S-021`** (the tree) and **`S-022`** (create / edit)
**Owner:** Backend, then Frontend
**Depends on:** nothing. `Bab` shipped in slice 0.

## Story
As the Technical Office, I set up Kaff's trades as a tree and give each one its default markup,
because §4.2 makes every new BOQ line inherit its markup from the item's باب — so the markup is set
once per trade instead of being retyped, and mistyped, on every line of every project.

## What already exists, and what this story adds

| Already built | Evidence |
|---|---|
| The entity, with parent, Arabic and English names, sort order and active flag | [Verified: 2026-09-08 @ `src/Domain/MasterData/Bab.cs` -> `Bab`, `ParentBabId`, `NameAr`, `NameEn`, `SortOrder`, `IsActive`] |
| The default markup, held as a `Percentage` rather than a bare decimal **so 15% cannot be stored as 15 in one place and 0.15 in another** | [Verified: 2026-09-08 @ `src/Domain/MasterData/Bab.cs` -> `DefaultMarkup`; @ `src/Domain/Common/Percentage.cs` -> `FromPercent`, `FromFraction`] |
| A negative rate is impossible — the value object throws | [Verified: 2026-09-08 @ `src/Domain/Common/Percentage.cs` -> `Percentage`] |
| `decimal(18,6)` on every `Percentage`, by convention | [Verified: 2026-09-08 @ `src/Infrastructure/Persistence/KaffDbContext.cs` -> `ConfigureConventions`; @ `src/Domain/Common/Percentage.cs` -> `Scale`] |
| A unique index on the باب code | [Verified: 2026-09-08 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_babs_code`] |
| The permission row | [Verified: 2026-09-08 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.BabManage`] |

**What does not exist is any endpoint or screen.** ⚠️ **This sentence also said *"and no cycle check"*
until 2026-09-09 and was stale — corrected under SM-29.** The check landed on 2026-09-08 (D-127 §1):
`SetParent` walks the candidate parent's ancestors and refuses at any depth
[Verified: 2026-09-09 @ `src/Domain/MasterData/Bab.cs` -> `SetParent`]. See rule 5.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | أبواب form a **tree**: every باب has at most one parent, and a باب with no parent is a root | **§2** — *"~40 trades, tree"* |
| 2 | **Every باب carries its own default markup.** There is no inheritance from a parent باب and no null markup: `DefaultMarkup` is required [Verified: 2026-09-08 @ `src/Domain/MasterData/Bab.cs` -> `DefaultMarkup`]. §4.2 says the line markup *"defaults from the item's باب"* — the item's own باب, not the nearest ancestor that has one | **§4.2** · §2 |
| 3 | The markup is a **default**, and it is **overridable per line** [Verified: 2026-09-08 — §4.2's own words]. A باب's markup is copied onto a new BOQ line and does not govern it afterwards | **§4.2** |
| 4 | **Changing a باب's markup does not change any existing BOQ line, signed or open.** A signed BOQ holds copies with no foreign key to follow (§4.4, MUST); an open estimate re-prices only through §4.4's explicit human review (`S-049`, slice 4). **The correct implementation here is to add nothing** | **§4.4** |
| 5 | **A باب may not be its own ancestor, at any depth.** ✅ **Built 2026-09-08** — the guard walks the candidate parent's ancestors, bounded by the size of the tree so a cycle already in the data fails rather than hangs [Verified: 2026-09-09 @ `src/Domain/MasterData/Bab.cs` -> `SetParent`]. ⚠️ **This rule said *"nothing checks a longer cycle"* until 2026-09-09 and was stale** — it was routed as a defect on 2026-09-08 and repaired the same day (D-127 §1). **This story must not create a cycle either**, and creating with a parent runs through the same walk. The refusal's key is settled in `KAFF-205` and in D-128 | **§2** · D-127 §1 |
| 6 | The tree carries roughly **40** trades — a size, not a limit. No rule caps the depth or the breadth, and none is invented here | §2 |
| 7 | `spec.md`'s own examples of a markup are **concrete 15%** and **finishes 30%** — examples, not defaults to seed. **What Kaff's real trades and rates are is `Q60`'s data half** and is not guessed here | §4.2 · **`Q60`** |
| 8 | Markup is **stored at four decimals of a percent and displayed at two** — D-044 ruling 6 sets storage at 4 and display at 2, and `Percentage` stores the *fraction* at six decimal places so that 0.01% survives [Verified: 2026-09-08 @ `src/Domain/Common/Percentage.cs` -> `Scale`] | D-044 ruling 6 |
| 9 | `BabManage`, `CompanyWide`, **no assignment** — أبواب belong to no project. Technical Office settled by §2; the Owner grant stands on **`Q12`** | §2 · D-044 ruling 4 · **`Q12`** |
| 10 | Creating a باب and changing its markup are state changes and **each writes an audit record**: who, when, before and after. A markup change is the one most worth reading later, because it silently changes what every future BOQ line starts at | **CLAUDE.md** |
| 11 | The Arabic name is the product name. **`باب` is the term in the UI and `Bab` is the identifier in code** — never `Section`, `Trade`, `Chapter` or `Category` | **CLAUDE.md** · `spec.md` §14 |
| 12 | Every string is an i18n key; Arabic RTL at 390px, and **the tree's indentation is a logical property** — a child indents from the inline-start, which is the right in RTL, never from the left | CLAUDE.md · `ux/rtl-and-i18n.md` |

## Permissions, money, audit, i18n
- **Permissions:** `BabManage`, `CompanyWide`, no assignment required. Every other role refused `403`
  server-side.
- **Money:** carries no money. A markup is a `Percentage`, not a `Money` — it is a rate applied to a
  price elsewhere. **Moves nothing and writes no `Posting`.**
- **Audit:** rule 10, before and after, on both the markup and the parent.
- **i18n:** `bab.tree.title`, `bab.tree.empty`, `bab.field.code`, `bab.field.name_ar`,
  `bab.field.name_en`, `bab.field.parent`, `bab.field.default_markup`, `bab.field.sort_order`,
  `bab.create_title`, `bab.edit_title`, **`errors.master.bab_code_taken`**,
  **`errors.master.bab_cannot_be_its_own_ancestor`**.

⚠️ **Both error keys were `errors.bab.*` until 2026-09-09 and neither existed.** The namespace is
settled — every shipped master-data refusal is `errors.master.*`, one namespace per domain error
catalogue class [Verified: 2026-09-09 @ `src/Domain/MasterData/MasterDataErrors.cs` -> `BabCannotBeItsOwnParent`].
`decisions.md` **D-128**; the full route and what it owes are written once, in `KAFF-205`.

⛔ **And `errors.bab.cycle` was a second spelling of a refusal that already exists.** `AC-204-E` and
`AC-205-C` are two criteria over **one** guard, so they name **one** key. Two spellings of one refusal
is `F-08`'s exact shape — the reason `TranslationCatalogueTests` carries a whitelist over the
withholding key at all [Verified: 2026-09-09 @ `tests/Domain.Tests/TranslationCatalogueTests.cs` -> `The_withholding_refusal_has_exactly_one_key_in_the_code_and_in_both_catalogues`].
**Caught in refinement this time, before either key was written.**

## Acceptance criteria

**AC-204-A — a باب is created with a name, a parent and a markup**
Given the Technical Office on S-022
When a code, Arabic name, English name, optional parent and default markup are submitted
Then the باب exists carrying those values, and appears in the tree under its parent — or as a root when none was given

**AC-204-B — 15% is 15%, not 15** *(fails if the rule is broken)*
Given a default markup entered as `15`, meaning fifteen percent
When it is stored and read back
Then it is the fraction `0.15` and applying it to a rate of `100` gives `115`, not `1600`
And a markup of `12.75%` survives the round trip exactly, at both the storage and the display precision D-044 ruling 6 sets

**AC-204-C — every باب carries its own markup, and no child inherits one** *(fails if the rule is broken)*
Given a parent باب at 15% and a child باب at 30%
When an item in the child باب is used to start a new BOQ line
Then the line's markup defaults to **30%**, the child's own — the parent's rate reaches nothing

**AC-204-D — a markup change does not move an existing line** *(fails if the rule is broken)*
Given a signed BOQ line and an open estimate line, both created while a باب's markup was 15%
When that باب's markup is changed to 30%
Then both lines still carry 15%, and no alert or re-price is raised by this story — §4.4's review is `S-049`, slice 4

**AC-204-E — a باب cannot be its own ancestor** *(fails if the rule is broken)*
Given باب A and باب B
When A is given B as its parent, and then B is given A as its parent
Then the second is refused with **`errors.master.bab_cannot_be_its_own_ancestor`** — the same one key `AC-205-C` names, because it is the same guard — and the tree still has a root
And the same holds for a longer chain — A → B → C → A is refused at the closing edge

**AC-204-F — a repeated code is refused**
Given a باب with code `CONC`
When a second باب is submitted with code `CONC`, and again as `conc`
Then both are refused

**AC-204-G — a role without `BabManage` reaches nothing** *(fails if the rule is broken)*
Given a signed-in user of each role that does not hold `BabManage`
When each calls the create endpoint and then the edit endpoint directly, with no browser involved
Then every call is refused `403`, and no باب is created or changed

**AC-204-H — a markup change is audited before and after** *(fails if the rule is broken)*
Given a باب whose markup moves from 15% to 30%
When the audit trail is read
Then a record names the actor, the time, and both the old and the new rate

**AC-204-I — Arabic, RTL, at mobile width**
Given S-021 at 390px in Arabic
When the tree renders
Then direction is RTL, each level indents from the **inline-start** and not from the left, percentages are bidi-isolated, no string is a literal in either language, and the page body does not scroll horizontally

**AC-204-J — an empty tree says so**
Given no باب exists
When S-021 renders
Then `bab.tree.empty` is displayed as an explicit empty state with the create action, never a blank area and never a placeholder row

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-204-A` … `AC-204-J` |
| Stable `AC-204-<LETTER>` ids | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–12 |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `BabManage`, CompanyWide, no assignment; the Owner half flagged to `Q12` |
| Money behaviour named explicitly | ✅ — carries none, moves none; the markup is a `Percentage` and rule 8 states its precision |
| Arabic UI strings as i18n keys | ✅ — twelve keys, rule 12 |
| The audit record it writes is stated | ✅ — rule 10, `AC-204-H` |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated. Six criteria are marked *(fails if the rule is broken)*; **QA's to write** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ⛔ **Not met — `Q12`**, the slice-wide grant. ⚠️ **`Q60` does not block the story, only its data**: the tree can be built, demonstrated and tested with any أبواب; **it cannot be *seeded* with Kaff's real trades and rates until `Q60` is answered**, and a demo seeded with invented trades at invented markups is the kind of plausible fiction that gets mistaken for a decision |

**Flip the trailer to `READY` when `Q12` is ruled and QA's cases land.**

## Not in this story
- **Re-parenting an existing باب, and moving an item between أبواب.** `KAFF-205` — rule 5's cycle
  check is stated here because this story must not create a cycle either, but the re-parent path is
  that story's.
- **Archiving a باب.** `Bab.Archive` exists [Verified: 2026-09-08 @ `src/Domain/MasterData/Bab.cs` -> `Archive`]; **no story in slice 2 covers it** — see *Questions*, and `KAFF-206` covers the item, not the باب.
- **Seeding Kaff's real 40 trades.** `Q60`.
- **The BOQ section a باب creates.** §4.5's *"selecting an item auto-creates its باب section if absent"*
  is `S-054`, slice 4.
- **Any margin or profit figure.** `S-056`, slice 4.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q12`** | Open, slice-2-wide. Whether the Owner keeps `BabManage` | **Karim** |
| **`Q60`** | Raised by `KAFF-200`. Whether the أبواب and their markups arrive in the setup spreadsheet with the items, or are set up first through this story. **It decides this story's data, not its behaviour** | **Karim** |
| 1 | **Archiving a باب is unassigned.** The behaviour exists in the entity and **no slice-2 story owns it** — `stories/backlog.md`'s slice-2 table has `KAFF-206` for the catalogue item and nothing for the باب. Whether an archived باب's items are still findable, and whether a باب holding items can be archived at all, is the same *"without breaking what already references it"* problem `KAFF-206` is named for. **Named rather than absorbed: a story that quietly grows to cover an unlisted one is how an estimate stops meaning anything** | **Scrum Master**, then Nabil |
| 1a | ✅ **Answered as far as the BA may answer it, 2026-09-09 — `decisions.md` D-128 §2.** It goes in **neither** `KAFF-206` (a different entity, and 3 points bought for one) **nor** `KAFF-204`/`205` (create-and-markup, and re-parent). **The recommendation is a new story at 3 points**, and cutting one is scope: the Scrum Master proposes, Nabil locks. **The business half is registered as `Q67`** and is Karim's — it was not answered here | **Scrum Master** → **Nabil**; `Q67` → **Karim** |
