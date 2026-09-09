# KAFF-207 · Employee register — exactly one record per costed person

<!-- kaff id=KAFF-207 slice=2 points=5 state=NOT-BUILT verdict=none at=- on=2026-09-09 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 5 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-09 by the BA. **⚠️ Amended 2026-09-09 — `Q68` is answered in part (D-130 §6): the reference number's origin is ruled, the field list beyond §10's four is refused (D-130 §8) and stays open. `Q12` clears (D-129 §1). One Definition-of-Ready box is still unticked — QA's cases.**
**Spec:** **§2** (*"every costed person, exactly one record"*), **§10** · **Decisions:** D-044 ruling 4, D-055 §2 (HR sees names and roles, no salary), D-016 (Worker vs Employee, 🟡), **D-130 §§6, 8, D-129 §1**
**Register:** `stories/questions-for-karim.md` → **`Q12`** (✅ answered, D-129 §1), **`Q68`** (✅ answered in part — D-130 §6/§8: reference number ruled, field list refused and still open)
**Screens:** `ux/screen-inventory.md` → **`S-023`** (list), **`S-024`** (create / edit)
**Owner:** Backend, then Frontend
**Depends on:** KAFF-204 — day labour must name a باب that exists

## Story
As HR, I keep one record for every person Kaff pays, because §2 says *"every costed person, exactly one
record"* and a second copy of a person is what makes a payroll run pay somebody twice.

## What already exists, and what this story adds

**The entity, its validation and its persistence are built.** This story adds endpoints and two screens
and rebuilds none of it:

| Already built | Evidence |
|---|---|
| One entity for both populations, carrying a `Kind`, so the same person cannot exist as two rows in two tables | [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `Employee`, `EmployeeKind`] |
| Creation with code, full name, phone, kind, باب and specialty, and its guards | [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `Create`] |
| The staff-only fields, set through behaviour rather than public setters | [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `SetStaffDetails`, `NationalId`, `JobTitle`, `HiredOn`] |
| Archiving, and its refusal when already archived | [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `Archive`] |
| A unique index on the employee code | [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_employees_code`] |
| ⛔ A **unique** index on the normalised phone — a refusal, not a warning | [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_employees_phone`] |
| A database check constraint that day labour carries a trade | [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ck_employees_day_labour_has_trade`] |
| The permission row — `CompanyWide`, granted to the Owner and HR | [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.EmployeeManage`] |

**What does not exist is any endpoint or screen** [Verified: 2026-09-09 — `src/Api/Features/` holds
`Assignments`, `Audit`, `Auth`, `Clients`, `Health`, `Setup` and `Users`, and no HR folder].

⛔ **One thing owed, now that `Q68`'s reference-number half is ruled (D-130 §6).** `Employee.Create`
takes a code from its caller today [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `Create`],
i.e. typed. **It must generate one instead**, the same shape as `KAFF-119`'s client-code sequence —
this is **Backend's**, not the BA's; nothing under `src/` was changed by this session.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | **One record per costed person, and HR is the single source.** Not one per employment, not one per project, not one per department | **§2** · **§10** |
| 2 | **One entity, two populations, distinguished by a `Kind`.** Salaried staff and day labour are the same table with the same identity rules, which is what makes *"nobody appears in both"* enforceable at all [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `EmployeeKind`]. 🟡 D-016 records that whether Kaff wants two visibly separate registers is a `spec.md` clarification and not a schema preference; **the screens are already two (`S-023`, `S-025`) over one store** | §2 · §10 · D-016 |
| 3 | **Day labour must carry a باب**, enforced by a database check constraint and by the entity [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `DayLabourRequiresTrade`]. Salaried staff need not | **§10** — *"name, phone, trade/باب, specialty"* |
| 4 | ⛔ **What a salaried staff record carries beyond §10's four worker fields is refused, not ruled — D-130 §8.** §10 lists the fields for a **worker** and lists none for salaried staff. `NationalId`, `JobTitle` and `HiredOn` exist in the entity today and are traceable to no §, no ruling and no D-number [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `SetStaffDetails`]. *"What Karim's paper staff file carries is a fact about his office, not a design choice."* **This story renders and stores exactly §10's fields and adds none**; a field beyond them is a later change to a built screen, not a guess now | **§10 · D-130 §8** |
| 5 | ✅ **Where an employee's code comes from is ruled — D-130 §6.** **Generated, not typed.** The precedent is decided and shipped: `spec.md` §2's amendment and `KAFF-119`'s client code, *"the code is generated; nobody typed it and nobody can edit it"* (`C-10001`). Consistency here is the answer, not a preference — `Employee.Create` moves from a typed code to a generated one | **D-130 §6** |
| 6 | ⛔ **This story stores no pay figure of any kind.** The entity carries no salary and no day rate today [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `Employee`], D-055 §2 rules HR's user list carries *"no visibility into salary if one is ever added"*, and §10 makes payroll **a treasury event** rather than a field on a person. **A rate on a master record is the shape that turns a register into a ledger**, and this story does not take it | **§10** · D-055 §2 · CLAUDE.md |
| 7 | **The searchable pool's average day rate is derived, never stored.** §10 asks for *"average day rate, frequency and rating"* — all three are computed by summing and counting engagements, and none is a column. **A stored average is a stored balance under a different name** | **CLAUDE.md** — *"Never store a balance"* · §10 · **`KAFF-210`** owns the engagements |
| 8 | Archiving replaces deletion. A leaver is deactivated, never deleted, and stays on historical project teams — the same ruling that governs a user [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `Archive`] | **D-049 ruling 5** |
| 9 | `EmployeeManage`, `CompanyWide`, **no assignment** — a person belongs to no project. HR settled by §2 and §10; **the Owner keeps it too** [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.EmployeeManage`] | §2, §10 · D-044 ruling 4 · **D-129 §1** |
| 10 | ⛔ **A user account and an employee record are two different things and this story joins them by nothing.** `User` is the login (slice 1); `Employee` is the costed person. **No foreign key between them is added here**, because nothing in `spec.md` says every employee has a login or every user is an employee — `Q55` is open on exactly that shape, and a join written now would answer it silently | **§9** · §10 · **`Q55`** |
| 11 | Creating, editing and archiving are state changes and **each writes an audit record**: who, when, what changed before and after | **CLAUDE.md** |
| 12 | Every string is an i18n key; Arabic RTL at 390px. **Personal names and phone numbers are bidi-isolated**, for the reason the audit timestamp is | CLAUDE.md · `ux/rtl-and-i18n.md` |

## Permissions, money, audit, i18n
- **Permissions:** `EmployeeManage`, `CompanyWide`, no assignment required. Every other role refused
  `403` server-side. ⚠️ **A Site Engineer holds it not at all** — that is `Q71` and `KAFF-209`'s
  problem, not this story's: `S-023` and `S-024` are `HR, O` in the screen inventory.
- **Money:** ⛔ **This story stores no money and moves none.** No salary, no day rate, no balance, no
  `Posting`. Rules 6 and 7 are the whole of it, and they are prohibitions rather than descriptions.
- **Audit:** rule 11, before and after on every changed field.
- **i18n:** `hr.employee.list_title`, `hr.employee.create_title`, `hr.employee.edit_title`,
  `hr.employee.field.code`, `hr.employee.field.full_name`, `hr.employee.field.phone`,
  `hr.employee.field.kind`, `hr.employee.field.bab`, `hr.employee.field.specialty`,
  `hr.employee.archive_action`, `enum.employee_kind.salaried`, `enum.employee_kind.day_labour`,
  plus the existing `errors.master.*` keys for the domain guards.

## Acceptance criteria

**AC-207-A — a salaried employee is created**
Given HR on `S-024`
When a code, full name, phone and a kind of salaried are submitted
Then the record exists carrying those values, its kind is `Salaried`, and it is active

**AC-207-B — day labour without a باب is refused** *(fails if the rule is broken)*
Given a create request with a kind of day labour and no باب
When it is submitted
Then it is refused with `errors.master.day_labour_requires_trade`
And the same request submitted straight at the database, bypassing the entity, is refused by the check constraint — the guarantee is in two places on purpose

**AC-207-C — two people cannot share an employee code** *(fails if the rule is broken)*
Given an employee with code `E-100`
When a second is submitted with `E-100`, and again with `e-100`
Then both are refused, and the refusal survives two requests arriving at the same instant — the guarantee is the unique index, not a read-then-write

**AC-207-D — one person, one record** *(fails if the rule is broken)*
Given an existing employee
When the same person is submitted a second time
Then a second row is not created
And the mechanism that prevents it is named in the test rather than assumed — today it is the unique index on the normalised phone, **which is `Q70` and may not survive it**

**AC-207-E — the register stores no pay figure** *(fails if the rule is broken)*
Given the employee create and edit endpoints and their responses
When the stored properties of `Employee` are enumerated as an allow-list
Then no salary, day rate, wage or any other money-typed member appears among them
And no response body carries one, under that name or any other

**AC-207-F — an employee is archived, not deleted** *(fails if the rule is broken)*
Given an active employee who has left
When they are archived
Then the row still exists with every field intact, it is excluded from the default list and findable through the explicit filter
And no mapped route on the API deletes an employee, under any verb — enumerated as an allow-list of the routes the host registered

**AC-207-G — the code is generated, and no field is added beyond what the entity already holds**
Given the create form
When a salaried employee is created
Then its reference number is generated by the system, never typed and never editable — the same shape as a client code (`KAFF-119`, D-049 ruling 7)
And the form renders exactly the fields `Employee` already carries — §10's four worker fields, plus the staff fields already built (`NationalId`, `JobTitle`, `HiredOn`) — and no field is added on top of them, because D-130 §8 refuses to guess what Karim's paper file carries beyond what is already there

**AC-207-H — a role without `EmployeeManage` reaches nothing** *(fails if the rule is broken)*
Given a signed-in user of each role that does not hold `EmployeeManage` — including the Site Engineer and the Technical Office
When each calls the create, edit and archive endpoints directly, with no browser involved
Then every call is refused `403`, and no record is created or changed by any of them

**AC-207-I — every change is audited before and after** *(fails if the rule is broken)*
Given an employee whose name and specialty are both edited in one request
When the audit trail is read
Then one record names the actor, the time, and both fields with their old and new values
And a refused edit writes no record and changes nothing

**AC-207-J — Arabic, RTL, at mobile width**
Given `S-023` and `S-024` at 390px in Arabic
When they render
Then direction is RTL, names and phone numbers are bidi-isolated inside Arabic text, no string is a literal in either language, and the page body does not scroll horizontally

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-207-A` … `AC-207-J` |
| Stable `AC-207-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–12; rule 5 is re-cited to `D-130 §6`, rule 4 to `D-130 §8`'s refusal |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `EmployeeManage`, CompanyWide, no assignment; the Owner grant confirmed by **D-129 §1** |
| Money behaviour named explicitly | ✅ — rules 6 and 7, `AC-207-E`. **Stores none, moves none, derives the one average §10 asks for** |
| Arabic UI strings as i18n keys | ✅ — twelve keys, rule 12 |
| The audit record it writes is stated | ✅ — rule 11, `AC-207-I` |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated [Verified: 2026-09-09 — `qa/` holds `slice-1`, `questions.md`, `README.md`, `risk-register.md` and `strategy.md`]. Seven criteria are marked *(fails if the rule is broken)*. **QA's to write** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ — `Q68`'s reference-number half (D-130 §6) and `Q12` (D-129 §1) are both answered. **`Q68`'s field-list half is refused, not open** (D-130 §8) — this story renders exactly what the entity already holds and does not wait on it. `Q70` reaches `AC-207-D`'s mechanism through `KAFF-209` and is unrelated to this box |

**Flip the trailer to `READY` when QA's cases land.** Backend still owes the switch from a typed to a
generated employee code (see above) before `AC-207-G` is true of the running system.

## Not in this story
- **The two-populations invariant.** `KAFF-208` — *"nobody appears in both"* is its own story and its
  own question.
- **Registering a worker from site.** `KAFF-209`, which is a different actor on a different screen
  (`S-026`, `M1`, one hand at 390px) and carries the phone-dedup question.
- **Engagement history, day rate, frequency and rating.** `KAFF-210`.
- **Payroll, timesheets and the daily log.** §10 makes payroll a treasury event: slice 3 for the
  postings, slice 6 for the log. **No pay is computed, stored or displayed anywhere in slice 2.**
- **Performance review and the eleven KPIs.** §10, 🟡, and no slice-2 story.
- **Linking an employee to a `User` login.** Rule 10, and `Q55`.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q68`** | ✅ **ANSWERED IN PART — D-130 §6, and refused in the other half by D-130 §8.** The reference number is **generated**, not typed (`AC-207-G`). **What a staff file carries beyond §10's four worker fields is NOT ruled** — it is a fact about Kaff's office, not a design choice, and stays open for whenever Karim can show the file | **Closed** — reference number; **open** — field list |
| **`Q12`** | ✅ **ANSWERED — D-129 §1.** The Owner keeps `EmployeeManage` | **Closed** |
| **`Q70`** | Raised by `KAFF-209`. The unique phone index is this story's *"exactly one record"* mechanism too, so a ruling that softens it reaches `AC-207-D` | **Karim** |
| 1 | **D-016's 🟡 is still 🟡.** Whether Kaff wants Worker and Employee as visibly separate registers is recorded in the entity as an open question and has never been asked [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `Employee`]. **It does not block this story** — the screen inventory already answers it at the surface (`S-023` staff, `S-025` the worker pool, one store beneath), which is a UX shape rather than a business ruling | **Karim**, when `Q68` is asked — the same conversation |
