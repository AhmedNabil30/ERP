# KAFF-209 · Register a worker from site, deduplicated by phone

<!-- kaff id=KAFF-209 slice=2 points=5 state=NOT-BUILT verdict=none at=- on=2026-09-09 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 5 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-09 by the BA. **⚠️ Amended 2026-09-09 — `Q12` clears (D-129 §1). Two Definition-of-Ready boxes remain unticked, and this story carries the one carry-note `stories/backlog.md` writes as an instruction.**
**Spec:** **§10** (*"engineers register workers from site … Deduplicated by phone"*), **§2** · **Decisions:** D-044 ruling 4, **D-049 ruling 8 — ⛔ NOT extended here**
**Register:** `stories/questions-for-karim.md` → **`Q70`** (blocking, and the reason this story is not Ready — ⚠️ this is this story's own worker-phone question, a **different** question from the trade-markup `Q70` that `KAFF-204`/`KAFF-211`/`KAFF-212` cite; the register has two open questions sharing one number and it is not this session's to renumber), **`Q71`** (blocking), **`Q12`** (✅ answered, D-129 §1)
**Screens:** `ux/screen-inventory.md` → **`S-026`** (register from site, **`M1`**, one hand at 390px), **`S-025`** (the pool)
**Owner:** Backend, then Frontend
**Depends on:** KAFF-204 (a worker's trade must name a باب that exists), KAFF-207 (the register)

## Story
As a site engineer, I put a worker on the system from the site itself, because the pool is only worth
having if it is filled at the moment somebody is hired — and a form that has to wait for the office is
a form that is filled from memory a week later or not at all.

## ⛔ The carry-note this story exists around, and it is an instruction

`stories/backlog.md`'s slice-2 section, verbatim:

> **Carry into KAFF-209 from D-049 ruling 8:** the worker master is *"deduplicated by phone"* in
> exactly the words §2 uses for the client, and Karim's ruling softened that to a warning **for the
> client**. It was not asked about workers, and the unique index on the worker phone is still there.
> **Do not extend the ruling; ask.** It is the same shape as Q29.

**The index is still there** [Verified: 2026-09-09 @
`src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_employees_phone`],
and it is `IsUnique` — a **refusal**, while the client's index on the same column shape is not
[Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ix_clients_phone`].

⛔ **A duplicate-phone rule for clients is not a duplicate-phone rule for workers, and the difference
is not a technicality.** Karim's reason for softening the client rule was *"a corporate client and its
CEO might be registered as two separate entities sharing the same contact number"* — **two records that
are genuinely two parties.** A worker is a person, and §2's requirement of him is *"every costed person,
exactly one record"*, which is a **costing** invariant: two rows for one man are two payees. The
opposite pull is just as ordinary — a family, a village, a foreman whose number goes on every card, a
labourer with no phone at all. **Both readings are defensible, which is exactly why neither may be
chosen here.** Registered as **`Q70`**.

**This is registered rather than reasoned to a conclusion, and the reasoning above is deliberately not
an answer.** An invented rule is always plausible; this one would be *especially* plausible, because
D-049 ruling 8 is sitting right there in the same shape and would look like a precedent.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | A worker is registered with **name, phone, trade/باب and specialty**. §10 lists exactly these four, and this story adds no fifth field | **§10** |
| 2 | A worker is a costed person in the day-labour population — the same `Employee` entity with `Kind = DayLabour`, not a second table [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `EmployeeKind`]. §10 calls it *"the worker registry"*; §2 requires *"exactly one record"* per costed person, and two tables cannot give that | **§2** · §10 |
| 3 | The trade/باب is **required** for a worker, enforced by the entity and by a database check constraint [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ck_employees_day_labour_has_trade`] | **§10** |
| 4 | ⛔ **Whether a repeated phone refuses the save or warns about it is `Q70`.** Today it refuses [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_employees_phone`]. **D-049 ruling 8 is not extended to workers, by instruction** | **`Q70`** — uncited, therefore asked |
| 5 | **Whichever way `Q70` is ruled, the match must be made on the normalised form**, so `+20 10 …`, `0020 10 …` and `010 …` all match — the same normalisation the client uses [Verified: 2026-09-09 @ `src/Domain/MasterData/Employee.cs` -> `PhoneNormalised`]. **A matcher that misses is worse under the warning reading than under the refusal**, because a missed match then means a warning nobody sees rather than a save that fails loudly | §10 · D-049 ruling 8's own reasoning about matching, which applies to the *mechanism* whatever the answer |
| 6 | ⛔ **Who may register a worker from site is `Q71`, and today the answer is nobody.** §10 says *"**engineers** register workers from site"* and `ux/screen-inventory.md` gives `S-026` to `SE`; **`EmployeeManage` is granted to the Owner and HR alone** [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.EmployeeManage`], and its own citation line reads *"§2, §10"*. **The catalogue row and the §10 it cites disagree**, and the fix is not obvious: handing a site engineer `EmployeeManage` hands him the whole salaried register, edit and archive included — the shape `Q42`'s warning exists to prevent | **§10** vs the catalogue · **`Q71`** |
| 7 | **If a narrow permission is the answer, whether it is project-scoped is part of the same question.** An engineer registering from site is on a site he is assigned to, and §9's *"role alone is insufficient"* leans project-scoped — **leaning is not a ruling**, and the `ProjectTeamRead` precedent (D-051 Q32) shows the shape without deciding this one. **No permission is invented here** | §9 · **`Q71`** |
| 8 | ⛔ **This story stores no day rate.** §10 asks for an *"average day rate"* on the pool and that average is **derived from engagements, never stored** — `KAFF-210` owns them. **A rate typed onto a worker's card at registration is a stored average with one sample**, and it is the mistake this rule exists to prevent | **CLAUDE.md** — *"Never store a balance"* · §10 |
| 9 | Registration is a state change and **writes an audit record**: who, when, and the record created. Where `Q70` is ruled as a warning, **the acknowledgement is itself audited** — the client precedent is `AuditEventKind.DuplicatePhoneAcknowledged` through the mechanism D-061 already built [Verified: 2026-09-09 @ `src/Domain/Auditing/IAuditContext.cs` -> `DuplicatePhoneAcknowledged`], and D-107 §3 rules that the unbackfillable part is *"already in the ground"* | **CLAUDE.md** · D-107 §3 |
| 10 | `S-026` is **`M1`** — the only mobile-first screen in this slice. RTL at 390px, one hand, and the phone field opens a numeric keypad. Every string is an i18n key | `ux/screen-inventory.md` -> `S-026` · CLAUDE.md |
| 11 | ⛔ **Registration is online.** CLAUDE.md permits offline **drafts** and slice 9 owns offline entirely; this story builds no offline path, no queue and no local store. **It moves no money, so nothing here is the money-never-moves-offline rule** — it is simply not this slice's work | CLAUDE.md · slice 9 |

## Permissions, money, audit, i18n
- **Permissions:** **HELD on `Q71`.** Today: `EmployeeManage`, `CompanyWide`, Owner and HR, no
  assignment [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.EmployeeManage`].
  §10 and `S-026` both say a site engineer performs this act and he holds nothing. **The story cannot
  be built until this is ruled, because there is no actor for its main scenario.**
- **Money:** ⛔ **Stores none, moves none, writes no `Posting`.** No day rate, no wage, no balance —
  rule 8. The pool's average day rate is `KAFF-210`'s derived figure.
- **Audit:** rule 9. Under the warning reading, the acknowledgement is audited too.
- **i18n:** `hr.worker.register_title`, `hr.worker.field.name`, `hr.worker.field.phone`,
  `hr.worker.field.bab`, `hr.worker.field.specialty`, `hr.worker.duplicate_phone_warning`,
  `hr.worker.duplicate_phone_confirm`, `hr.worker.pool_title`, plus the existing
  `errors.master.day_labour_requires_trade`. **The two duplicate keys are listed and are not written
  until `Q70` is ruled** — under the refusal reading they are never used.

## Acceptance criteria

**AC-209-A — a worker is registered with §10's four fields**
Given a site engineer on `S-026`
When a name, phone, باب and specialty are submitted
Then the worker exists as an `Employee` with `Kind = DayLabour` carrying those values, and appears in the pool on `S-025`

**AC-209-B — a worker without a باب is refused** *(fails if the rule is broken)*
Given a registration with no trade
When it is submitted
Then it is refused with `errors.master.day_labour_requires_trade`, and the same request driven straight at the database is refused by the check constraint

**AC-209-C — the phone is matched on its normalised form** *(fails if the rule is broken)*
Given a worker registered as `+20 100 123 4567`
When the same number is submitted as `0100 123 4567`, as `0020 100 123 4567`, and with spaces and dashes in different places
Then every one of them is recognised as the same number
And this criterion holds **under either answer to `Q70`** — it tests the matcher, not the verdict

**AC-209-D — HELD on `Q70`: what a repeated phone does**
Given a worker already registered with a number
When a second worker is submitted with the same number
Then — **held.** `Q70` decides refusal or warning. **Today it is refused by the unique index**; that behaviour is not asserted as correct here, and it is not removed either — the index is the reversible half, and dropping it before the ruling destroys the constraint that `KAFF-208`'s invariant currently rests on

**AC-209-E — HELD on `Q71`: who may register from site**
Given the roles §9 defines
When each calls the registration endpoint directly
Then — **held.** Exactly one thing is asserted meanwhile, and it is asserted as a **gap**: today no site-engineer role reaches this endpoint, and the test says so, so that it reddens the day somebody grants it. That is the shape `V-35-G` used for HR's landing and it is deliberate

**AC-209-F — a role holding nothing reaches nothing** *(fails if the rule is broken)*
Given a signed-in user of each role that holds neither `EmployeeManage` nor whatever `Q71` produces
When each calls the registration endpoint directly, with no browser involved
Then every call is refused `403`, and no worker is created by any of them

**AC-209-G — no rate is captured at registration** *(fails if the rule is broken)*
Given the registration request and its response
When their members are enumerated as an allow-list
Then no day rate, wage, salary or money-typed member appears in either, under that name or any other

**AC-209-H — registration is audited** *(fails if the rule is broken)*
Given a worker registered from site
When the audit trail is read
Then a record names the actor, the time, and the record created
And under the warning reading of `Q70`, an acknowledged duplicate writes its own record naming the worker already holding the number

**AC-209-I — `S-026` works one-handed in Arabic at 390px**
Given the register-from-site screen at 390px in Arabic
When it renders
Then direction is RTL, the phone field opens a numeric keypad, every control is reachable with one thumb, the name and the number are bidi-isolated, no string is a literal in either language, and the page body does not scroll horizontally

**AC-209-J — the pool shows a worker with no engagements as having none** *(fails if the rule is broken)*
Given a worker registered today and never engaged
When `S-025` renders him
Then his average day rate, frequency and rating each read as an explicit empty state — never `0`, never a blank, and never a placeholder row

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-209-A` … `AC-209-J`. **`AC-209-D` and `AC-209-E` are written and held** |
| Stable `AC-209-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–11; rules 4, 6 and 7 cite `Q70` / `Q71` and are marked as questions rather than sourced |
| No uncited rule | ✅ — ⛔ **and the one rule that could have been written from a precedent was not.** D-049 ruling 8 is cited only as *what is not being extended* |
| Permissions named explicitly | ⛔ **Named, and named as broken.** Today's grant is cited; §10's actor holds nothing; **`Q71` is the question and no permission is invented** |
| Money behaviour named explicitly | ✅ — rule 8, `AC-209-G`. Stores none, moves none |
| Arabic UI strings as i18n keys | ✅ — eight keys plus one existing, two of them conditional on `Q70` |
| The audit record it writes is stated | ✅ — rule 9, `AC-209-H`, including the acknowledgement branch |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated [Verified: 2026-09-09 — `qa/` holds `slice-1`, `questions.md`, `README.md`, `risk-register.md` and `strategy.md`]. Six criteria are marked *(fails if the rule is broken)*. **QA's to write** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ⛔ **Not met, twice over, and both are at the centre.** **`Q70`** decides the story's title — *"deduplicated by phone"* — and **`Q71`** decides whether its main actor exists. **`Q12` is answered (D-129 §1)** |

⛔ **This is the least Ready of the seven.** Two of its questions do not shape an edge; they shape the
story. **Flip the trailer to `READY` when `Q70` and `Q71` are ruled and QA's cases land.**

## Not in this story
- **Engagement history, the day rate, frequency and the rating.** `KAFF-210`. This story registers the
  person; that one records what he did.
- **The salaried register.** `KAFF-207`.
- **The two-populations invariant.** `KAFF-208` — ⛔ **and it currently rests on the very index `Q70`
  asks about**, which is stated in that story rather than left to be discovered.
- **Costing him.** The daily log is slice 6; payroll is a treasury event.
- **Offline registration.** Slice 9, rule 11.
- **Any change to the client's duplicate-phone behaviour.** D-049 ruling 8 governs the client and is
  untouched here in either direction.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q70`** | **New, raised here and blocking. The backlog's own carry-note is the instruction to ask it.** Is a repeated worker phone a refusal or a warning? Asked with the two neighbouring populations named separately — **subcontractors and suppliers also carry unique phone indexes today** [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/Configurations/MasterDataConfigurations.cs` -> `ux_subcontractors_phone`, `ux_suppliers_phone`] — **and the answer may legitimately differ for each**, which is why they are named apart rather than rolled into one rule | **Karim** |
| **`Q71`** | **New, raised here and blocking.** §10 says engineers register workers from site and no engineer holds a permission that reaches it. Who may, and is it scoped to a project he is assigned to? ⛔ **Not answerable by granting `EmployeeManage` to the site engineer** — that hands him the salaried register, which is `Q42`'s warning in a second place | **Karim** |
| **`Q12`** | ✅ **ANSWERED — D-129 §1.** The Owner keeps `EmployeeManage` | **Closed** |
| **`Q36`** | Already open, and adjacent: *"can two people who use the system share a phone number?"* — the `User` half of the same shape. **It is not this question and does not answer it**: a `User` is a login, a worker is a costed person | **Karim** |
