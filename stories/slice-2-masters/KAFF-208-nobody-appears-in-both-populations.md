# KAFF-208 · Nobody appears in both populations: day labour and salaried

<!-- kaff id=KAFF-208 slice=2 points=3 state=NOT-BUILT verdict=none at=- on=2026-09-09 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 3 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-09 by the BA. **⚠️ Amended 2026-09-09 — `Q69` and `Q12` are both answered (D-130 §7, D-129 §1) and `AC-208-E` is written. One Definition-of-Ready box is still unticked — QA's cases.**
**Spec:** **§10** (*"Two populations, one source each … Nobody appears in both"*), **§2** · **Decisions:** D-016 (🟡), D-044 ruling 4, **D-130 §7, D-129 §1**
**Register:** `stories/questions-for-karim.md` → **`Q12`** (✅ answered, D-129 §1), **`Q69`** (✅ answered, D-130 §7)
**Screens:** `ux/screen-inventory.md` → **`S-024`** (*"costing type is immutable after creation"*), **`S-023`**, **`S-025`**
**Owner:** Backend, then Frontend
**Depends on:** KAFF-207 (the register)

## Story
As HR, I cannot have the same person costed twice, because day labour is costed from the daily log and
salaried staff from timesheets — so a person in both populations is paid from two sources for the same
day and neither total is wrong on its own.

## Why this is a story and not a line in `KAFF-207`

**It is the invariant, and an invariant needs a criterion that fails.** §10's sentence is four words
long — *"Nobody appears in both"* — and it is the only sentence in §10 written as a prohibition. The
backlog gives it its own row at 3 points, and it is kept as one because the three mechanisms that hold
it live in three different places and no single one of them is the rule:

| Mechanism | Where | What it actually prevents |
|---|---|---|
| One table, one `Kind` per row | [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `EmployeeKind`] | Two rows in two tables for one person — structurally impossible, because there is one table |
| `Kind` has **no setter and no behaviour that changes it** | [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `Kind`] | A silent edit from one population to the other |
| The unique index on the normalised phone | [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_employees_phone`] | The **same person entered twice**, once in each population — which is the only way the sentence can actually be broken today |

⛔ **The third is the whole of the enforcement, and it is `Q70`.** If a repeated worker phone becomes a
warning rather than a refusal, **the only mechanism that stops one person appearing in both populations
disappears with it**, and this story needs another. That is a dependency between two stories'
questions, and it is stated here rather than discovered during the build.

⛔ **A defect finding, routed to Backend, not a question:** `MasterDataErrors.EmployeeKindIsImmutable`
is declared, translated in both catalogues, and **returned by nothing anywhere in `src/`**
[Verified: 2026-09-09 @ `src/Domain/MasterData/MasterDataErrors.cs` -> `EmployeeKindIsImmutable`]. The
immutability is real but structural — it holds because no method sets `Kind`, not because anything
refuses. **An error nobody returns reads as a rule somebody enforces**, and the next session to add a
`SetKind` will find a ready-made error waiting and conclude the rule was meant to be a refusal. Either
this story returns it (`AC-208-C`) or it is deleted; **it must not stay declared and unreachable.**

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | **There are exactly two populations and every costed person is in exactly one.** No third kind, no "both", no null | **§10** |
| 2 | **The costing source follows the population, not the person's title**: day labour from the daily log, salaried staff from timesheets. **Neither source is built in slice 2** — this story records which one applies and computes nothing | **§10** |
| 3 | **A person's population is fixed at creation.** No edit path changes it today, and this story adds none [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `Kind`]. `ux/screen-inventory.md` says the same at `S-024`: *"costing type is immutable after creation"* | §10 · `ux/screen-inventory.md` -> `S-024` |
| 4 | ✅ **What happens when a day labourer goes onto the payroll is ruled — D-130 §7, forced by the invariant rather than chosen.** **Archive the day-labour record, register a new employee record — never a move.** Moving the one record would make it salaried *retroactively, over its whole history*, falsifying §10's *"nobody appears in both"* at every point in the past; refusing outright is not a rule, only an absence of one, and the man really did join the payroll. Archiving and re-registering holds the invariant at every point in time, and **the engagement history stays attached to the record that earned it** — `KAFF-210`. The two records are not a duplicate person; they are **two employment relationships** | **§10 · D-130 §7** |
| 5 | **The invariant is asserted, not merely implied.** A criterion that passes because no code path exists is a criterion that stops passing the day one does. `AC-208-A` and `AC-208-B` name the mechanism they rest on | **`process/agile.md`** — a scenario that fails if the rule is broken · D-106 |
| 6 | `EmployeeManage`, `CompanyWide`, **no assignment**. HR settled by §2 and §10; **the Owner keeps it too** [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.EmployeeManage`] | §2, §10 · D-044 ruling 4 · **D-129 §1** |
| 7 | Any refusal this story adds is a **state change that did not happen**, so it writes no audit record — and any population change that is ever permitted **must** write one, before and after, because it changes how the person is paid | **CLAUDE.md** |
| 8 | Every string is an i18n key. The two population names appear in Arabic in the UI — **يومية is the word §10 and §14 use** and `DayLabour` is the identifier; never *casual*, *temporary* or *labourer* | CLAUDE.md · **§14** |

## Permissions, money, audit, i18n
- **Permissions:** `EmployeeManage`, `CompanyWide`, no assignment. Every other role refused `403`.
- **Money:** ⛔ **This story is about money and moves none.** It decides which costing *source* a person
  is paid from; it computes no pay, stores no rate and writes no `Posting`. **It is in slice 2 and its
  consequence lands in slice 6**, which is why the invariant is worth a story before either source
  exists — a duplicated person found after the first payroll run is found by the payment.
- **Audit:** rule 7.
- **i18n:** `enum.employee_kind.salaried`, `enum.employee_kind.day_labour`,
  `hr.employee.kind_is_permanent`, plus the existing `errors.master.employee_kind_immutable`, **which
  has an entry in both catalogues already** [Verified: 2026-09-09 @ `src/Web/public/locales/en.json` -> `errors.master.employee_kind_immutable`].

## Acceptance criteria

**AC-208-A — every costed person is in exactly one population** *(fails if the rule is broken)*
Given the `Employee` store with records of both kinds
When every record is read
Then each carries exactly one `Kind`, that kind is a defined enum member — not the zero value and not a string the binder produced — and no record carries both or neither

**AC-208-B — one person cannot be created into both populations** *(fails if the rule is broken)*
Given a registered day labourer
When the same person is submitted again as salaried staff
Then the second create is refused
And the test names the mechanism that refused it rather than asserting only the status code — today that is the unique index on the normalised phone, and `Q70` may replace it

**AC-208-C — a population cannot be edited from one to the other** *(fails if the rule is broken)*
Given an existing employee of either kind
When an edit request carries a different `kind`
Then the population does not change
And the refusal is reachable and translated — `MasterDataErrors.EmployeeKindIsImmutable` is returned, or it is deleted from the catalogue; it does not remain declared and unreachable

**AC-208-D — the immutability is not merely an absent setter** *(fails if the rule is broken)*
Given the `Employee` entity
When its members are enumerated as an allow-list
Then no public member sets `Kind` after construction, and the allow-list is written out by name so that adding one is a deliberate edit to this test rather than a silent widening

**AC-208-E — a day labourer joining the payroll is archived and re-registered, never moved** *(fails if the rule is broken)*
Given a day labourer with an engagement history
When HR records that he has gone onto the payroll as salaried staff
Then his day-labour record is archived, unchanged and still carrying every engagement it earned, and a **new** employee record is created for him as salaried staff
And no single record ever carries both `Kind`s, and no existing engagement's record is edited to point at the new record — `AC-208-C` still refuses a direct edit of `Kind`, because this is a create-and-archive, never a move (D-130 §7)

**AC-208-F — a role without `EmployeeManage` cannot reach either population** *(fails if the rule is broken)*
Given a signed-in user of each role that does not hold `EmployeeManage`
When each calls the employee endpoints directly, with no browser involved
Then every call is refused `403`

**AC-208-G — Arabic, RTL, at mobile width**
Given `S-023`, `S-024` and `S-025` at 390px in Arabic
When the population is displayed and chosen
Then it reads **يومية** and the salaried term verbatim from the catalogue, direction is RTL, no string is a literal in either language, and the page body does not scroll horizontally

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-208-A` … `AC-208-G` |
| Stable `AC-208-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–8; rule 4 is re-cited to `D-130 §7` |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `EmployeeManage`, CompanyWide, no assignment; the Owner grant confirmed by **D-129 §1** |
| Money behaviour named explicitly | ✅ — moves none, stores none, computes none; and the paragraph says why a money-free story is in the money section |
| Arabic UI strings as i18n keys | ✅ — three keys plus one existing, and §14's term is pinned |
| The audit record it writes is stated | ✅ — rule 7: a refusal writes none, and the new create-and-archive pair each write their own (creation, archiving) |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated [Verified: 2026-09-09 — `qa/` holds `slice-1`, `questions.md`, `README.md`, `risk-register.md` and `strategy.md`]. Six criteria are marked *(fails if the rule is broken)*, and **`AC-208-C` is red against the code as it stands** — there is no edit endpoint to refuse, and the error it names is returned by nothing. **QA's to write** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ — `Q69` (D-130 §7) and `Q12` (D-129 §1) are both answered. `Q70` still reaches `AC-208-B`'s mechanism through `KAFF-209` and is unrelated to this box |

**Flip the trailer to `READY` when QA's cases land.** `Q70` need not be ruled first, but `AC-208-B`
must be re-read the day it is.

## Not in this story
- **The register itself.** `KAFF-207`.
- **The worker phone rule.** `KAFF-209` and `Q70` — this story consumes that decision and does not
  take it.
- **Costing anybody.** The daily log is slice 6, timesheets are slice 6, payroll is a treasury event.
  **Nothing here computes or stores a rate or a total.**
- **Adding a `SetKind`.** ✅ **Settled by `Q69` (D-130 §7): never a move, so no `SetKind` is added.**
  `MasterDataErrors.EmployeeKindIsImmutable` stays and is returned — `AC-208-C` requires it reachable,
  which is Backend's to wire up, not the BA's.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q69`** | ✅ **ANSWERED — D-130 §7, forced by the invariant.** Archive the day-labour record, register a new employee record — never a move. `AC-208-E` is written | **Closed** |
| **`Q12`** | ✅ **ANSWERED — D-129 §1.** The Owner keeps `EmployeeManage` | **Closed** |
| **`Q70`** | Raised by `KAFF-209`. If the worker phone becomes a warning, **the last mechanism enforcing this story's invariant is gone** and this story needs another one | **Karim** |
| 1 | **`EmployeeKindIsImmutable` is declared, translated, and returned by nothing** [Verified: 2026-09-09 @ `src/Domain/MasterData/MasterDataErrors.cs` -> `EmployeeKindIsImmutable`]. **A defect finding, not a question** — an unreachable error reads as an enforced rule. Routed to **Backend** through `AC-208-C`, and named here so it is not read as this story inventing one | **Backend** |
