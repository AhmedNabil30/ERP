# KAFF-200 · Import the catalogue from Excel at setup, loading the good rows and reporting the rest

<!-- kaff id=KAFF-200 slice=2 points=5 state=NOT-BUILT verdict=none at=- on=2026-09-08 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 5 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-08 by the BA against `STATUS.md`'s sprint-6 item 6. **⚠️ Amended 2026-09-09 — `Q60`'s schema half, `Q61` and `Q12` are all answered (D-129 §2, D-129 §3 / D-130 §1, D-129 §1), rule 7 is rewritten off the "all-or-nothing" title it used to cite, and `AC-200-H`/`AC-200-I` are written. One Definition-of-Ready box is still unticked — see *Definition of Ready* below.**
**Spec:** **§4.1** (the whole of it), §4.2, §4.4, §2 · **Decisions:** D-018 (the `status` values, 🟡), D-044 ruling 4, **D-129 §§1–2, D-129 §3 / D-130 §1**
**Register:** `stories/questions-for-karim.md` → **`Q60`** (schema half ✅ answered, D-129 §2; the data half — Kaff's real trades and markups — stays open as **`Q75`**), **`Q61`** (✅ answered, D-129 §3 / D-130 §1), and **`Q12`** (✅ answered, D-129 §1)
**Screens:** `ux/screen-inventory.md` → **`S-019`**
**Owner:** Backend, then Frontend
**Depends on:** KAFF-204 — an item names a باب, and `CatalogueItem.BabId` is not nullable [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `BabId`]. **The أبواب are set up first, through KAFF-204** — D-129 §2 rules the template carries exactly §4.1's item fields and no column that would create a باب, so an import row can only reference one that already exists.

## Story
As the Technical Office, I load Kaff's existing price catalogue from the spreadsheet we already keep,
once, at setup, because retyping several hundred priced items into a screen is how a catalogue arrives
wrong and nobody notices until a BOQ is signed on the wrong rate.

## What already exists, and what this story adds

**The entity is built and this story does not rebuild it.** Slice 0 shipped `CatalogueItem` with every
field `spec.md` §4.1 names, its validation, and its persistence:

| Already built | Evidence |
|---|---|
| The entity, its factory and its guards | [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `CatalogueItem`, `Create`, `Reprice`] |
| `code · description · unit · bab · costPrice · baseSellRate · status` — §4.1's field list, complete | [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `Code`, `DescriptionAr`, `Unit`, `BabId`, `CostPrice`, `BaseSellRate`, `Status`] |
| Money at `decimal(18,4)`, applied as a convention so a later session cannot forget it | [Verified: 2026-09-08 @ `src/Infrastructure/Persistence/KaffDbContext.cs` -> `ConfigureConventions`] |
| A unique index on the code | [Verified: 2026-09-08 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_catalogue_items_code`] |
| The permission row | [Verified: 2026-09-08 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.CatalogueManage`] |

**What does not exist is any of the import.** There is no `Catalogue` feature folder and no catalogue
endpoint of any kind [Verified: 2026-09-08 — `src/Api/Features/` holds `Assignments`, `Audit`, `Auth`,
`Clients`, `Health`, `Setup` and `Users`, and nothing else]. This story adds the upload endpoint, the
parse, the all-or-nothing write and S-019.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | The catalogue is **loaded from Excel at setup and edited manually afterwards**. The import is a setup act, not a maintenance one | §4.1 |
| 2 | **The import is not an ongoing sync**, and S-019 says so on the screen in words the operator reads before they choose a file — not in a tooltip and not in documentation | §4.1 · `ux/screen-inventory.md` -> `S-019` |
| 3 | An imported item carries exactly §4.1's fields: `code · description · unit · bab · costPrice · baseSellRate · status`. Nothing else is read from the sheet, and a column the sheet carries that this list does not name is **not** imported into a field invented for it | §4.1 |
| 4 | **Every money value is read as a decimal, never through `float` or `double`.** Excel stores numbers as IEEE-754 doubles, so a rate of `1234.5678` read as a double and cast arrives wrong at the fourth decimal — which is exactly the digit `decimal(18,4)` exists to keep. Read the cell's text, or the reader's decimal accessor; never its double accessor | **CLAUDE.md** — *"Never use `float` or `double` anywhere near money"* · §4.1 |
| 5 | `costPrice` is imported and **must never appear in any client-facing output** — not on a screen, a print view, a PDF or an export | §4.2 |
| 6 | An item's باب must already exist. The item's `BabId` is required and there is no unassigned باب [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `BabId`]. **The template carries exactly §4.1's item fields and no column that creates a باب** (D-129 §2), so the أبواب are created first, through `KAFF-204`, and an import row only references one that already exists | §2 · §4.1 · **D-129 §2** |
| 7 | **The import loads every good row and refuses only the bad ones**, returning a row-level report that names each row it could not take and why. A row that fails does not stop any other row in the file from being imported, and the catalogue after the import holds exactly the valid rows' items. **This reverses the story's own former title and rule** — *"all-or-nothing"* was cited to a backlog title and nothing else, which is what `Q61` was raised against. Nabil's ruling reads against refusing the whole file: *"a clear validation report that surfaces any row-level errors … giving users actionable feedback rather than failing silently or **causing total gridlock**"* | **D-129 §3 · D-130 §1** |
| 8 | `CatalogueManage`, `CompanyWide`. **Technical Office** — settled, `CatalogueItem` is owned by the Technical Office in §2. **The Owner also holds it** [Verified: 2026-09-08 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.CatalogueManage`], and **the Owner keeps it** — Karim's *"the owner have all the prevliges on all the system"* names every master-data grant, `CatalogueManage` included | §2, §4.1 · D-044 ruling 4 · **D-129 §1** |
| 9 | An import is a state change and **writes an audit record**: who, when, the file's name, and how many items it created | **CLAUDE.md** — *"Every state change writes an audit record"* |
| 10 | Every string on S-019 is an i18n key. The screen is Arabic and RTL at 390px, per the Definition of Done, though S-019 is an `M3` desktop-primary screen | CLAUDE.md · `ux/screen-inventory.md` -> `S-019` |
| 11 | A signed BOQ cannot be reached by anything this story writes, because a signed BOQ holds **copies** and no foreign key back to a catalogue row | §4.4 |

## Permissions, money, audit, i18n
- **Permissions:** `CatalogueManage`, `CompanyWide`, **no assignment required** — the catalogue is
  company-wide and belongs to no project. Technical Office and the Owner both — **D-129 §1** confirms
  the Owner keeps `CatalogueManage`. Every other role is refused server-side, `403`.
- **Money:** imports `costPrice` and `baseSellRate` as `Money`, stored `decimal(18,4)`. **Moves none** —
  the catalogue is a price list, not a ledger, and this story writes no `Posting`.
- **Audit:** one record per import act — rule 9. ⚠️ **One record per import, or one per row?** Not a
  business question; **routed to the Architect** in *Open questions* below rather than chosen here.
- **i18n:** `catalogue.import.title`, `catalogue.import.choose_file`, `catalogue.import.not_a_sync`
  (rule 2's sentence), `catalogue.import.confirm`, `catalogue.import.succeeded`,
  `catalogue.import.rejected`, `catalogue.import.row_error`, **`errors.master.catalogue_import_failed`** ⚠️ *(was `errors.catalogue.import_failed` until 2026-09-09 — corrected under **D-128 §1** before it shipped. Catalogue errors live in `MasterDataErrors`, so the namespace is `errors.master.*`; `KAFF-202` shipped the same mistake and had to be renamed after the fact.)*.

## Acceptance criteria

**AC-200-A — a clean file becomes a catalogue**
Given a spreadsheet of N valid rows in the agreed shape, and the أبواب they name already present
When the Technical Office imports it
Then N catalogue items exist, each carrying the code, description, unit, باب, cost price and base sell rate of its row

**AC-200-B — a rate keeps its fourth decimal** *(fails if the rule is broken)*
Given a row whose base sell rate is `1234.5678` and whose cost price is `987.6543`
When the file is imported and the two values are read back from the database
Then they are exactly `1234.5678` and `987.6543` — not `1234.5677`, not `1234.568`, and not any value that differs in the fourth decimal
And the same holds for a rate with more than four decimals in the sheet: it is refused or rounded by a stated rule, never silently truncated by the storage

**AC-200-C — an unknown باب is not invented** *(fails if the rule is broken)*
Given a row naming a باب that does not exist
When the file is imported
Then no catalogue item is created for that row, and **no باب is created for it either**
And the failure names the row and the باب it could not find

**AC-200-D — cost price does not leave the internal surface** *(fails if the rule is broken)*
Given an imported catalogue
When any client-reachable surface is inspected — every `/api/portal/*` response and every client-facing print or export
Then `costPrice` appears in none of them, under that name or any other

**AC-200-E — the screen says it is not a sync**
Given S-019 before a file is chosen
When it renders
Then `catalogue.import.not_a_sync` is displayed as part of the screen, not behind a tooltip or a help link

**AC-200-F — a role without `CatalogueManage` cannot import** *(fails if the rule is broken)*
Given a signed-in user of each role that does not hold `CatalogueManage`
When each posts a valid file to the import endpoint directly, with no browser involved
Then every one is refused `403`, and no catalogue item is created by any of them

**AC-200-G — the import is audited** *(fails if the rule is broken)*
Given a successful import
When the audit trail is read
Then it carries a record naming the actor, the time, the file and the number of items created
And a **refused** import writes no item and leaves the catalogue count unchanged

**AC-200-H — a bad row does not sink the good ones** *(fails if the rule is broken)*
Given a file of 200 rows of which one is invalid — a missing price, an unknown باب, a code repeated within the file
When it is imported
Then the other 199 rows become catalogue items, the one bad row does not, and the response carries a row-level report naming that row's number and the reason it was refused
And the catalogue afterward holds exactly those 199 items — nothing fewer, and nothing invented to paper over the gap

**AC-200-I — the file's shape is the template's shape**
Given the standardized template Kaff downloads before filling in its price list (D-129 §2)
When a file is imported
Then it is accepted only if its columns match the template's — exactly §4.1's field list: code, description, unit, باب, cost price, base sell rate, status — and a file with an extra or a missing column is refused, naming what the template requires and what the file carried instead
And the template is available for download from S-019 before a file is ever chosen

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-200-A` … `AC-200-I` |
| Stable `AC-200-<LETTER>` ids, appended never inserted | ✅ — `H` and `I` are now written; the next criterion takes `J` |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rule 7 is re-cited to **D-129 §3 / D-130 §1** in place of the backlog title |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `CatalogueManage`, CompanyWide, no assignment. The Owner grant is confirmed by **D-129 §1** |
| Money behaviour named explicitly | ✅ — rules 4, 5, `AC-200-B`, `AC-200-D` |
| Arabic UI strings as i18n keys | ✅ — eight keys, rule 10 |
| The audit record it writes is stated | ✅ — rule 9, `AC-200-G`. Its granularity is an Architect question, not a missing statement |
| **QA has written at least one scenario that fails if the rule is broken** | ✅ **Met 2026-09-09** — `qa/slice-2/test-cases.md`, `TC-2-001`…`TC-2-098`. ⚠️ **One criterion is NOT cased: `AC-200-B`** — its *"more than four decimals"* clause names two legal behaviours and no rule choosing between them. **A ruling is owed (BA or Nabil, not Karim — it is a storage/parse policy, not a business fact), and until it lands this story is not Ready.** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ — `Q60`'s schema half, `Q61` and `Q12` are all answered. `Q60`'s data half (`Q75`) is not this story's data and does not block it |

**Flip the trailer to `READY` when QA's cases land.** Everything else this story was waiting on is
ruled: `Q60`'s schema half (D-129 §2), `Q61` (D-129 §3 / D-130 §1) and `Q12` (D-129 §1).

## Not in this story
- **Re-importing.** A second import is `KAFF-201`, and it is a different act with a different question behind it.
- **Creating or editing one item by hand.** `KAFF-202`.
- **Searching the catalogue.** `KAFF-203`.
- **The أبواب themselves.** `KAFF-204` builds the tree; this story consumes it.
- **Archiving an item.** `KAFF-206`.
- **The custom-item review queue (S-020).** §4.5, and it is a BOQ surface — slice 4.
- **Re-pricing open estimates.** §4.4's *"you have X open offers on old pricing"* is `S-049`, slice 4.
- **Any BOQ.** The freeze rule (§4.4) is enforced where the BOQ is built, not here.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q60`** | ✅ **Schema half ANSWERED — D-129 §2.** A standardized downloadable template defines the columns; `AC-200-I` is written against it. **The data half — Kaff's real ~40 trades and their markups — is still open, re-registered as `Q75`**, and this story seeds none of it | **Karim** — `Q75` only |
| **`Q61`** | ✅ **ANSWERED — D-129 §3 / D-130 §1.** The import loads the good rows and reports the bad ones; rule 7 and `AC-200-H` are rewritten against it | **Closed** |
| **`Q12`** | ✅ **ANSWERED — D-129 §1.** The Owner keeps `CatalogueManage` | **Closed** |
| 1 | **One audit record per import, or one per row?** A 600-row file writes 600 records under the second reading, and the audit table is append-only and partitioned monthly (D-072 §3). **Not a business question** — the record's *content* is stated in rule 9; its granularity is a design call | **Architect** |
| 2 | **What `status` values a catalogue item may hold.** `spec.md` §4.1 lists `status` as a field and never enumerates it; the code carries `Active` and `Archived` as *"the minimum the freeze rule needs"* and flags itself 🟡 as a question for Nabil [Verified: 2026-09-08 @ `src/Domain/MasterData/CatalogueItem.cs` -> `CatalogueItemStatus`]. **This story imports items as `Active` and never sets any other value**, so it does not turn on the answer — recorded so `KAFF-206` does not meet it cold | **Nabil** · D-018 |
