# KAFF-129 · Partition `audit_records` by month, from the start

<!-- kaff id=KAFF-129 slice=1 points=8 state=NOT-BUILT verdict=none at=- on=2026-09-07 -->

**Slice:** 1 (schema; the deadline is **before slice 3**) · **Epic:** Foundation · **Points:** 8 · **Status:** **NOT-BUILT.** Cut 2026-09-07 by the BA against `STATUS.md`'s *"What is actually next"* item 1, which had no story file and therefore could not be pulled. **One Definition-of-Ready box is unticked and it is not the BA's to tick — see *Definition of Ready* below.**
**Spec:** §9 (the audit obligation), `CLAUDE.md` → *Audit* · **Decisions:** **D-072 §3** (the mechanism), D-063 §2 (why the column exists), D-033 (the guards gate startup), D-096 §1
**Register:** `stories/questions-for-karim.md` → **N11** (Architect) and **Q54** (Karim, **open**)
**Owner:** Architect
**Depends on:** nothing. It is a schema change against a table that already exists.

## Why this story exists

**`STATUS.md` → *What is actually next*, item 1, and it has been item 1 since the board was rebuilt:**

> *"**N11 — partition `audit_records` by month.** Overdue and blocks slice 3. Mechanism ruled:
> `decisions.md` **D-072 §3** — PostgreSQL monthly partitioning, drop expired partitions. Converting a
> populated, trigger-protected, append-only table later is a new table + a migration + a swap. **The
> cost only goes up.**"*

It had **no story file and no trailer**, so it appeared in no generated table, carried no points, and
nothing on the board could be pulled against it. `STATUS.md` itself says why that is the failure mode
worth spending a file on: *"A story with **no** trailer appears as `❓ UNKNOWN` rather than vanishing,
because a missing row reads as *nothing to do*."* A story that does not exist at all does not even get
that.

**The deadline is not slice 9, and this is the whole of the argument.** D-072 §3 filed the mechanism
under slice 9 and then flagged its own consequence:

> *"**Converting a populated table to a partitioned one is materially harder than creating it
> partitioned** — in PostgreSQL it is a new table plus a data migration plus a swap, on a table that is
> **append-only and trigger-protected**, which is precisely the kind of table you least want to
> rewrite. **If `audit_records` should be partitioned from the start, that decision is due now, not at
> slice 9.** … The deadline is not slice 9 — it is **before the first production rows exist**, and the
> cheapest moment is before slice 3 starts writing real money history."*

Slice 3 is the next objective. This is the last cheap moment.

## ⛔ What this story does **not** decide — `Q54` is Karim's and it is open

**`Q54` asks how long an audited IP address is kept. The mechanism is ruled; the number is not.**
`stories/questions-for-karim.md` → `Q54`, verbatim: *"**D-072 §3 never states the retention period**,
and Q54's own question asked for one in as many words. **The number is what remains open, and it is
Karim's.** *(A previous BA session was instructed to close this row against D-072 §3 in full and
correctly declined — doing so would have overstated what was decided. Do not close it now either.)*"

**This story therefore ships the partitioning and no retention period at all.** Not a default, not a
placeholder, not a configuration value with a value in it. `AC-129-H` is the criterion that fails if
one appears. **The partitioning is what is expensive to do late; the number is one line of
configuration that is cheap to add the day Karim answers.** Splitting them is what lets the expensive
half land now without anybody inventing the cheap half.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | **Every state change writes an audit record**, and the record is evidence. *"Evidence that can be edited is not evidence"* | `CLAUDE.md` → *Audit* · [Verified: 2026-09-07 @ `src/Infrastructure/Persistence/Sql/001_guards.sql` — the comment heading section 1] |
| 2 | **`audit_records` accepts no `UPDATE`, no `DELETE` and no `TRUNCATE`**, refused by the database rather than by application code, raising `KAFF_APPEND_ONLY` | [Verified: 2026-09-07 @ `src/Infrastructure/Persistence/Sql/001_guards.sql` -> `trg_audit_records_append_only` (`BEFORE UPDATE OR DELETE … FOR EACH ROW`) and `trg_audit_records_no_truncate` (`BEFORE TRUNCATE … FOR EACH STATEMENT`), both calling `kaff_reject_mutation()`] |
| 3 | **The application refuses to start when a guard is missing**, and the check is a fixed list of trigger names compared against the live database | **D-033** · [Verified: 2026-09-07 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` -> `FindMissingGuardsAsync` — the list names `trg_audit_records_append_only` and `trg_audit_records_no_truncate`] |
| 4 | **A failed sign-in records the caller's IP address**, which is personal data sitting in a table with no delete path. That is the problem Q54 was raised against | **D-063 §2** · [Verified: 2026-09-07 @ `src/Infrastructure/Persistence/Configurations/AuditConfiguration.cs` -> `builder.Property(record => record.IpAddress)`] |
| 5 | **The mechanism is ruled and is not open for re-selection.** Nabil, D-072 §3, verbatim: *"we will implement **PostgreSQL table partitioning by month** on `audit_records`. This allows us to drop entire historical partitions once the legal retention period expires, effectively deleting the PII without violating the append-only/no-truncate triggers on the active partitions."* The keyed hash was the alternative and was not chosen | **D-072 §3** |
| 6 | **Dropping a partition is DDL. Deleting a row is DML.** That distinction is the entire reason this mechanism is the compatible one: `DROP TABLE`/`DETACH PARTITION` never runs a row trigger, so the append-only guarantee is untouched by retention, and no code path anywhere gains the ability to delete one audit row | **D-072 §3**, read together with rule 2 |
| 7 | **The retention period is `Q54`, it is open, and this story invents no value for it** | `stories/questions-for-karim.md` -> `Q54` · **D-096 §5**, which lists *"Q54/N11's retention consequence"* among four things that *"still stand with Nabil, none has moved, and **none may be answered by any agent**"* |
| 8 | **`GET /api/audit` keeps working, unchanged, Owner-only.** A schema change that quietly narrows or widens who reads the trail is a permission change wearing a migration's clothes | **KAFF-117** · `ux/screen-inventory.md` -> `S-015` |

## Permissions, money, audit, i18n

- **Permissions:** **none of its own.** It adds no endpoint and no catalogue row. `GET /api/audit`'s
  gate is KAFF-117's and must be provably unchanged (`AC-129-G`).
- **Money:** moves none, reads none. It touches `audit_records` and nothing else — **`postings` is not
  partitioned by this story** (see *Not in this story*).
- **Audit:** writes no audit record. It *is* the audit table; a migration is not a state change a user
  made. The obligation this story carries is the opposite one — that every record already written
  survives it, byte for byte (`AC-129-F`).
- **i18n:** no user-facing string. `KAFF_APPEND_ONLY` is a database error code and
  `guardsInstalled` is a diagnostic field on `/api/health`; neither is shown to a user.

## Acceptance criteria

**AC-129-A — `audit_records` is range-partitioned by month on `occurred_at`**
Given a database created from the migrations, with no rows
When the schema is inspected
Then `audit_records` is a **partitioned** table, `RANGE` on `occurred_at`, with one partition per calendar month
And the month boundaries are computed in **one stated timezone**, written down where the migration lives, because `occurred_at` is `timestamp with time zone` [Verified: 2026-09-07 @ `src/Infrastructure/Persistence/Migrations/20260819213508_Initial.cs` -> the `audit_records` table, `occurred_at`] and *"which month a record is in"* is otherwise a question with two answers in Egypt

**AC-129-B — every partition refuses `UPDATE`, `DELETE` and `TRUNCATE`, including one that did not exist when the migration ran** *(fails if the rule is broken)*
Given a partition created **after** the migration — next month's, or one created by whatever creates them
When an `UPDATE`, a `DELETE` and a `TRUNCATE` are each attempted against a row in it, and again against the parent table
Then all six are refused with `KAFF_APPEND_ONLY`
*Rule 2. This is the criterion the whole story turns on: a partitioned table where the guard sits only on the parent, or only on the partitions that existed on migration day, is an append-only table with a hole in it that opens by itself every month.*

**AC-129-C — an unguarded partition makes the application refuse to start** *(fails if the rule is broken)*
Given a partition from which the append-only trigger has been dropped by hand
When the application starts and `/api/health` is read
Then startup is refused and `guardsInstalled` is **false**, naming that partition
*Rule 3, D-033. `FindMissingGuardsAsync` compares against a **fixed list of trigger names** [Verified: 2026-09-07 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` -> `FindMissingGuardsAsync`]. A fixed list cannot name a partition that will exist next year, so **the check as written today would report a fully-guarded database while an unguarded partition accepted deletes.* **This criterion fails against the current check by construction** — it is the half of the work that is easy to miss, because the missing check and a passing check produce identical output (D-096 §3, the same shape, recorded on this project once already).

**AC-129-D — a record never fails to insert because its month has no partition** *(fails if the rule is broken)*
Given the clock moves into a month whose partition nobody created
When an audit record is written
Then it lands
*An audit write that fails takes the state change with it — the interceptor writes the record in the same `SaveChanges` as the change [Verified: 2026-09-07 @ `src/Infrastructure/Persistence/Interceptors/AuditSaveChangesInterceptor.cs`]. A missing partition is therefore an outage, not a warning, and "somebody runs a script each month" is not an answer.*

**AC-129-E — dropping a whole partition deletes no row, and trips no trigger** *(fails if the rule is broken)*
Given a partition holding audit records, on a scratch database
When the partition is dropped as DDL
Then it goes, no row-level trigger fires, no `DELETE` is issued against `audit_records`, and the remaining partitions are unaffected
And attempting to delete a **single row** from any remaining partition is still refused with `KAFF_APPEND_ONLY`
*Rule 6. This is the criterion that proves the ruled mechanism actually is compatible with the append-only guarantee, rather than asserting that it is. **It exercises the drop; it does not schedule one** — see `AC-129-H`.*

**AC-129-F — every record already written survives the conversion, unchanged**
Given a database holding audit records written before this migration
When the migration runs
Then the row count is identical, and every record's `id`, `occurred_at`, `before_json`, `after_json` and `ip_address` are identical
And each row sits in the partition its own `occurred_at` selects
*The dev database already holds real history and staging holds more. D-072 §3's *"a new table plus a data migration plus a swap"* is exactly this criterion; it is the cost the story exists to pay once, cheaply.*

**AC-129-G — `GET /api/audit` returns the same trail, across a partition boundary, to exactly the same people** *(fails if the rule is broken)*
Given records either side of a month boundary
When the Owner reads `GET /api/audit`
Then the results, their order and their paging are what KAFF-117's criteria already require, spanning partitions transparently
And every role that KAFF-117 refuses is still refused, asserted against `Role` rather than a hand-written list (D-122 §4)
*Rule 8.*

**AC-129-H — ⛔ no retention period ships, in any form** *(fails if the rule is broken)*
Given the whole of this story's delivered work — migrations, SQL, configuration, scheduled jobs, documentation and defaults
When it is searched for a retention period
Then there is **no number, no default, no placeholder and no scheduled job that drops anything on a timer**
And the retention period exists only as a **named, unset parameter** with `Q54` cited beside it, so that answering Q54 is a value change and not a design change
*Rule 7. **A `90` or a `7 years` in a config file is an invented business rule, and it is the exact failure mode `CLAUDE.md` and `agents.md` both call this project's most expensive** — plausible, unreviewable, and discovered during a compliance audit. This criterion is written to fail loudly if a future session "finishes" the story by picking one.*

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ |
| Stable `AC-129-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–8 above |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — none of its own; KAFF-117's gate unchanged |
| Money behaviour named | ✅ — moves none |
| Arabic UI strings as i18n keys | ✅ — n/a, no user-facing string |
| The audit record it writes is stated | ✅ — none; it is the audit table |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** No `TC-` case exists for this story. Six of the eight criteria are marked *(fails if the rule is broken)* and `AC-129-B`, `C` and `E` are fault-injectable as written, but **`qa/slice-1/test-cases.md` is QA's file and the case is QA's to write** — the BA does not tick this box. **Routed to QA.** |
| Story-currency citations dated with a stable identifier | ✅ — every claim above carries `[Verified: 2026-09-07 @ … -> …]` |
| Not `BLOCKED` on an open question | ✅ — **`Q54` blocks the retention *period*, which this story deliberately does not build.** Nothing in `AC-129-A`…`H` needs an answer from Karim |

**So the trailer reads `NOT-BUILT`, not `READY`, and one box is why.** `READY` is the Scrum Master's
declaration made in refinement, and the board's own precedent is that a `READY` story has QA cases —
KAFF-104 carries `TC-1-030…036, 233, 234` and KAFF-115 carries `TC-1-121…128`
[Verified: 2026-09-07 @ `qa/slice-1/test-cases.md` — the story index rows]. **Flip the trailer to
`READY` when QA's cases land; nothing else is outstanding.**

## Not in this story

- **The retention period, and any job that drops a partition on a schedule.** `Q54`. `AC-129-H`.
- **Partitioning `postings`.** It is the other append-only, trigger-protected table and it will be
  larger than this one — but **D-072 §3 ruled `audit_records` and only `audit_records`**, and
  postings are business records with no expiry anybody has asked for. Extending the ruling to a
  second table would be inventing a decision. **Raised for the Architect, not taken here.**
- **Slice 9's compliance surface** — an export, a redaction report, a data-subject request. None of it
  is ruled and none of it is needed to land the partitioning.
- **The composite-key consequence, which is a design note rather than an exclusion.** PostgreSQL
  requires the partition key to be part of every unique key, and `audit_records`' primary key is
  `id` alone today [Verified: 2026-09-07 @ `src/Infrastructure/Persistence/Migrations/20260819213508_Initial.cs` -> `table.PrimaryKey("PK_audit_records", x => x.id)`; @ `src/Infrastructure/Persistence/Configurations/AuditConfiguration.cs` -> `builder.HasKey(record => record.Id)`]. **The Architect owns what that becomes** — a composite `(id, occurred_at)` key is the ordinary answer and it changes the EF configuration. Named so it is not discovered halfway through the migration.

## Questions for Karim

**`Q54`, unchanged and still open** — the retention period for audited IP addresses. **It is not
answered here, it is not narrowed here, and this story is built so that it does not need to be.** No
new question is raised by this story: the mechanism, the granularity and the compatibility argument
are all ruled by D-072 §3.

## Questions for the Architect — not Karim's, and not answered here

| # | Question |
|---|---|
| 1 | **Which timezone defines a month boundary** (`AC-129-A`). UTC is the ordinary answer for a `timestamptz` partition key and Egypt is not on it. Whichever is chosen must be written down where the migration lives, because it silently decides which month a record near midnight belongs to — and therefore which month's drop takes it. |
| 2 | **What creates next month's partition** (`AC-129-D`), and how far ahead. `pg_partman`, a startup step beside `DatabaseInitializer`, or a migration that pre-creates a fixed horizon. `CLAUDE.md` forbids a dependency without a `decisions.md` entry, so an extension is a decision to record rather than a default to take. |
| 3 | **How `FindMissingGuardsAsync` checks a set of tables that grows on its own** (`AC-129-C`). Its list is fixed names today. Deriving the expected set from `pg_inherits` is the shape the rest of this repository already prefers — *a hand-written list is a claim; the catalogue is the fact* (D-122 §4) — but it is the Architect's call, not the BA's. |
| 4 | **Whether `postings` follows**, when Treasury lands. Out of scope above; asked once so it is not rediscovered in slice 3. |
