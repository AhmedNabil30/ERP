# KAFF-209 · Register a worker from site, warned on a duplicate phone

<!-- kaff id=KAFF-209 slice=2 points=5 state=VERIFIED verdict=CONDITIONAL at=54ff044 on=2026-09-13 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 5 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-09 by the BA. **⚠️ Amended 2026-09-12 — `Q70` and `Q71` are both answered (D-139 §1/D-141, D-139 §2/D-140). The permission is `DayLabourSiteManage`, project-scoped, under `/api/projects/{projectId}/day-labour`. A duplicate phone warns and is acknowledged, never refuses. Two Definition-of-Ready boxes remain unticked — QA's cases.**
**Spec:** **§10** (*"engineers register workers from site … Deduplicated by phone"*), **§2**, §9 · **Decisions:** D-044 ruling 4, D-049 ruling 8 (the pattern this story now reuses, per D-141), **D-139 §§1–2, D-140, D-141**
**Register:** `stories/questions-for-karim.md` → **`Q70`** (✅ answered — D-139 §1, D-141: warn-and-acknowledge, matched on the normalised phone), **`Q71`** (✅ answered — D-139 §2, mechanism ruled by D-140: `Permission.DayLabourSiteManage`, `ProjectScoped`, Owner and any assigned Site Engineer), **`Q12`** (✅ answered, D-129 §1)
**Screens:** `ux/screen-inventory.md` → **`S-026`** (register from site, **`M1`**, one hand at 390px), **`S-025`** (the pool)
**Owner:** Backend, then Frontend
**Depends on:** KAFF-204 (a worker's trade must name a باب that exists), KAFF-207 (the register)

## Story
As a site engineer, I put a worker on the system from the site itself, because the pool is only worth
having if it is filled at the moment somebody is hired — and a form that has to wait for the office is
a form that is filled from memory a week later or not at all.

## ✅ The carry-note this story existed around, now closed

`stories/backlog.md`'s slice-2 section carried this instruction, verbatim:

> **Carry into KAFF-209 from D-049 ruling 8:** the worker master is *"deduplicated by phone"* in
> exactly the words §2 uses for the client, and Karim's ruling softened that to a warning **for the
> client**. It was not asked about workers, and the unique index on the worker phone is still there.
> **Do not extend the ruling; ask.** It is the same shape as Q29.

**It was asked, and answered — Nabil, D-139 §1: warn, do not block.** The worker (day labour) index
follows the client's shape after all, exactly as one of the two readings below anticipated. **`ux_employees_phone` is dropped and replaced by a non-unique `ix_employees_phone`** on the same
normalised column [Verified: 2026-09-12 @ `decisions.md` -> `D-141`]. **This differs from the client
case in one respect D-144 §1 adds**: a *salaried* record's phone still refuses on collision with another
salaried record (a partial unique index scoped to `Kind = Salaried`) — only the **day-labour**
population, which is this story's population, is warn-only.

The reasoning that made this worth asking, kept for the record now that it is answered: Karim's reason
for softening the client rule was *"a corporate client and its CEO might be registered as two separate
entities sharing the same contact number"* — two records that are genuinely two parties. A worker is a
person, and §2's requirement of him is *"every costed person, exactly one record"*, a costing invariant
where two rows for one man are two payees. The opposite pull was just as ordinary — a family, a
village, a foreman whose number goes on every card, a labourer with no phone at all. **Nabil ruled
warn-and-acknowledge for workers too**, so the costing-invariant reading did not carry the day; `KAFF-208`
now holds the invariant a different way (see that story's D-141/D-144 §1 amendment).

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | A worker is registered with **name, phone, trade/باب and specialty**. §10 lists exactly these four, and this story adds no fifth field | **§10** |
| 2 | A worker is a costed person in the day-labour population — the same `Employee` entity with `Kind = DayLabour`, not a second table [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `EmployeeKind`]. §10 calls it *"the worker registry"*; §2 requires *"exactly one record"* per costed person, and two tables cannot give that | **§2** · §10 |
| 3 | The trade/باب is **required** for a worker, enforced by the entity and by a database check constraint [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ck_employees_day_labour_has_trade`] | **§10** |
| 4 | ✅ **A repeated phone warns and is acknowledged; it never refuses the save — D-139 §1, D-141.** `ux_employees_phone` is dropped for a non-unique `ix_employees_phone`; the wire shape is the client's, exactly: `POST …/day-labour/phone-check`, and `AcknowledgedDuplicatePhone` on the register request. **One addition for this route only (D-140 point 6):** a match against a *salaried* record is returned masked, `{ restricted: true }`, with no id, name or code — the Site Engineer learns a match exists, never whose | **D-139 §1 · D-141 · D-140** |
| 5 | **The match is made on the normalised form**, so `+20 10 …`, `0020 10 …` and `010 …` all match — the same normalisation the client uses [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `PhoneNormalised`]. **A matcher that misses is worse under a warning than under a refusal**, because a missed match then means a warning nobody sees | §10 · D-049 ruling 8's own reasoning about matching · **D-141** |
| 6 | ✅ **Who may register a worker from site is ruled — D-139 §2, mechanism by D-140.** One new row, `Permission.DayLabourSiteManage`, `ProjectScoped`, granted to the Owner and any assigned Site Engineer (Junior or Supervisor). **`EmployeeManage` is NOT granted** — the salaried register and payroll stay isolated, and HR is deliberately not on this row (D-140 point 1), so HR keeps its one existing route into the register | **D-139 §2 · D-140** |
| 7 | ✅ **The permission is project-scoped, and the project comes from the route, never the body — D-140 point 2.** Every endpoint is under `POST /api/projects/{projectId:guid}/day-labour`, gated `DayLabourSiteManage` + `ProjectScope.FromRoute()`. **The pool itself stays company-wide** (D-140 point 3): the project authorises the act, it does not own the worker, and no `RegisteredOnProjectId` column is added | §9 · **D-139 §2 · D-140** |
| 8 | ⛔ **This story stores no day rate.** §10 asks for an *"average day rate"* on the pool and that average is **derived from engagements, never stored** — `KAFF-210` owns them. **A rate typed onto a worker's card at registration is a stored average with one sample**, and it is the mistake this rule exists to prevent | **CLAUDE.md** — *"Never store a balance"* · §10 |
| 9 | Registration is a state change and **writes an audit record**: who, when, and the record created. **The acknowledgement is itself audited**, one `DuplicatePhoneAcknowledged` per match — the client precedent, through the mechanism D-061 already built [Verified: 2026-09-09 @ `src/Domain/Auditing/IAuditContext.cs` -> `DuplicatePhoneAcknowledged`]. Under D-140 point 6's masked match, **the audit row still names the real matched id**, even though the response the Site Engineer saw did not | **CLAUDE.md** · D-141 · D-140 point 6 |
| 10 | `S-026` is **`M1`** — the only mobile-first screen in this slice. RTL at 390px, one hand, and the phone field opens a numeric keypad. Every string is an i18n key | `ux/screen-inventory.md` -> `S-026` · CLAUDE.md |
| 11 | ⛔ **Registration is online.** CLAUDE.md permits offline **drafts** and slice 9 owns offline entirely; this story builds no offline path, no queue and no local store. **It moves no money, so nothing here is the money-never-moves-offline rule** — it is simply not this slice's work | CLAUDE.md · slice 9 |

## Permissions, money, audit, i18n
- **Permissions:** `Permission.DayLabourSiteManage`, `ProjectScoped`, granted to `[owner,
  engineerJunior]` (D-140 point 1). The route is `/api/projects/{projectId:guid}/day-labour`, gated
  `FromRoute()`. HR and every other role are refused server-side, `403` — **including via
  `EmployeeManage`**, which does not reach this route at all.
- **Money:** ⛔ **Stores none, moves none, writes no `Posting`.** No day rate, no wage, no balance —
  rule 8. The pool's average day rate is `KAFF-210`'s derived figure.
- **Audit:** rule 9. The acknowledgement is audited, and D-140 point 6's masked salaried match is
  audited against the real id.
- **i18n:** `hr.worker.register_title`, `hr.worker.field.name`, `hr.worker.field.phone`,
  `hr.worker.field.bab`, `hr.worker.field.specialty`, `hr.worker.duplicate_phone_warning`,
  `hr.worker.duplicate_phone_confirm`, `hr.worker.duplicate_phone_restricted` (D-140 point 6's masked
  match), `hr.worker.pool_title`, plus the existing `errors.master.day_labour_requires_trade`.

## Acceptance criteria

**AC-209-A — an assigned site engineer registers a worker with §10's four fields**
Given a site engineer assigned to the project, on `S-026` under `/api/projects/{projectId}/day-labour`
When a name, phone, باب and specialty are submitted
Then the worker exists as an `Employee` with `Kind = DayLabour` carrying those values, appears in the pool on `S-025`, and the request carries no `Kind` member — it cannot be sent as anything other than day labour

**AC-209-B — a worker without a باب is refused** *(fails if the rule is broken)*
Given a registration with no trade
When it is submitted
Then it is refused with `errors.master.day_labour_requires_trade`, and the same request driven straight at the database is refused by the check constraint

**AC-209-C — the phone is matched on its normalised form** *(fails if the rule is broken)*
Given a worker registered as `+20 100 123 4567`
When the same number is submitted as `0100 123 4567`, as `0020 100 123 4567`, and with spaces and dashes in different places
Then every one of them is recognised as the same number

**AC-209-D — a repeated day-labour phone warns and is acknowledged, never refused** *(fails if the rule is broken, restated 2026-09-12 — `Q70` answered: D-139 §1, D-141)*
Given a worker already registered with a number
When a second worker is submitted with the same number and no acknowledgement
Then the save is refused `409 errors.master.duplicate_phone_not_acknowledged`, naming the existing worker
And with `AcknowledgedDuplicatePhone: true` the save succeeds and both workers exist, and one `DuplicatePhoneAcknowledged` audit record is written against the first

**AC-209-E — an assigned site engineer reaches the route; an unassigned one does not** *(fails if the rule is broken — `Q71` answered: D-139 §2, D-140)*
Given a site engineer assigned to project A and a site engineer with no assignment to project A
When each calls `POST /api/projects/{A}/day-labour` directly
Then the assigned one succeeds and the unassigned one is refused `403`, and no worker is created by the refused call

**AC-209-F — a role holding neither `DayLabourSiteManage` nor `EmployeeManage` reaches nothing** *(fails if the rule is broken)*
Given a signed-in user of each role without either permission, including Finance, Marketing/Sales, Technical Office and HR
When each calls the registration endpoint directly, with no browser involved
Then every call is refused `403`, and no worker is created by any of them
And **HR is included in this list, so a later grant to HR is caught** — D-140's own reasoning for keeping HR off this row

**AC-209-G — no rate is captured at registration** *(fails if the rule is broken)*
Given the registration request and its response
When their members are enumerated as an allow-list
Then no day rate, wage, salary or money-typed member appears in either, under that name or any other

**AC-209-H — registration is audited** *(fails if the rule is broken)*
Given a worker registered from site
When the audit trail is read
Then a record names the actor, the time, and the record created
And an acknowledged duplicate writes its own `DuplicatePhoneAcknowledged` record naming the worker already holding the number

**AC-209-I — `S-026` works one-handed in Arabic at 390px**
Given the register-from-site screen at 390px in Arabic
When it renders
Then direction is RTL, the phone field opens a numeric keypad, every control is reachable with one thumb, the name and the number are bidi-isolated, no string is a literal in either language, and the page body does not scroll horizontally

**AC-209-J — the pool shows a worker with no engagements as having none** *(fails if the rule is broken)*
Given a worker registered today and never engaged
When `S-025` renders him
Then his average day rate, frequency and rating each read as an explicit empty state — never `0`, never a blank, and never a placeholder row

**AC-209-K — a phone match against a salaried record is masked** *(new, appended — D-140 point 6)*
Given a salaried employee registered with a number
When a site engineer's phone-check is run against that number
Then the response is `{ restricted: true }` with no id, name or code
And if the site engineer proceeds and acknowledges, the save succeeds and the audit record names the real salaried id, never exposed to the response

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-209-A` … `AC-209-K` |
| Stable `AC-209-<LETTER>` ids, appended never inserted | ✅ — `K` is new, appended |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–11; rules 4, 6 and 7 are re-cited to `D-139 §§1–2`, `D-140`, `D-141` |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `Permission.DayLabourSiteManage`, `ProjectScoped`, Owner and assigned Site Engineer (D-140) |
| Money behaviour named explicitly | ✅ — rule 8, `AC-209-G`. Stores none, moves none |
| Arabic UI strings as i18n keys | ✅ — nine keys plus one existing |
| The audit record it writes is stated | ✅ — rule 9, `AC-209-H`, including the acknowledgement and masked-match branches |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated [Verified: 2026-09-09 — `qa/` holds `slice-1`, `questions.md`, `README.md`, `risk-register.md` and `strategy.md`]. Ten criteria are marked *(fails if the rule is broken)*. **QA's to write, against SM-30's test list in D-140** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ — `Q70` (D-139 §1 / D-141) and `Q71` (D-139 §2 / D-140) are both ruled. `Q12` is answered (D-129 §1) |

**Flip the trailer to `READY` when QA's cases land**, against D-140's SM-30 test list.

## Not in this story
- **Engagement history, the day rate, frequency and the rating.** `KAFF-210`. This story registers the
  person; that one records what he did.
- **The salaried register.** `KAFF-207`.
- **The two-populations invariant.** `KAFF-208` — its enforcement now rests on D-141/D-144 §1's
  partial unique index and warn-mechanism, restated there, not here.
- **Costing him.** The daily log is slice 6; payroll is a treasury event.
- **Offline registration.** Slice 9, rule 11.
- **Any change to the client's duplicate-phone behaviour.** D-049 ruling 8 governs the client and is
  untouched here in either direction.
- **Editing or archiving a worker.** Neither act is in D-139 §2, so both stay with `EmployeeManage`
  (D-140 point 4) — this story only registers.
- **Engagement open and close.** `KAFF-210`, under the same `DayLabourSiteManage` row (D-140 point 4).

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q70`** | ✅ **ANSWERED — D-139 §1, D-141.** Warn-and-acknowledge for day labour (and subcontractors, suppliers, each their own population); refuse only for a salaried-to-salaried match (D-144 §1) | **Closed** |
| **`Q71`** | ✅ **ANSWERED — D-139 §2, mechanism by D-140.** `Permission.DayLabourSiteManage`, `ProjectScoped`, Owner and any assigned Site Engineer; not `EmployeeManage` | **Closed** |
| **`Q12`** | ✅ **ANSWERED — D-129 §1.** The Owner keeps `EmployeeManage` | **Closed** |
| **`Q36`** | Already open, and adjacent: *"can two people who use the system share a phone number?"* — the `User` half of the same shape. **It is not this question and does not answer it**: a `User` is a login, a worker is a costed person | **Karim** |
| **`Q76`**, **`Q77`** | Raised by `KAFF-210`: whether the Site Engineer sees or records the agreed day rate on this same route, and whether one Site Engineer may close another's engagement — **not ruled here, D-140's own "what this does not decide"** | **Nabil** |
