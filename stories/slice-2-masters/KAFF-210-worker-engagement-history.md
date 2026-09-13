# KAFF-210 · Worker engagement history, day rate, frequency and rating

<!-- kaff id=KAFF-210 slice=2 points=3 state=VERIFIED verdict=CONDITIONAL at=54ff044 on=2026-09-13 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 3 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-09 by the BA. **⚠️ Amended 2026-09-12 — `Q72` is answered (D-139 §3, mechanism by D-140): an engagement is one continuous stretch of work on one project, closed only by an explicit manual close, no timeout; ratings are out of 5. Rating's finer shape (whole number, decimal, criteria breakdown) is not ruled — see Questions. One Definition-of-Ready box remains unticked — QA's cases.**
**Spec:** **§10** (*"Carries engagement history and per-engagement ratings, producing a searchable pool with average day rate, frequency and rating"*), §2 · **Decisions:** D-044 ruling 4, D-044 ruling 6 (four decimals stored, two displayed), **D-129 §1, D-139 §3, D-140**
**Register:** `stories/questions-for-karim.md` → **`Q72`** (✅ answered — D-139 §3, D-140), **`Q71`** (✅ answered — D-139 §2, D-140, engagement open/close share `KAFF-209`'s permission), **`Q12`** (✅ answered, D-129 §1)
**Screens:** `ux/screen-inventory.md` → **`S-027`** (history and rating), **`S-025`** (the pool it feeds)
**Owner:** Backend, then Frontend
**Depends on:** KAFF-209 (the worker), KAFF-204 (his باب)

## Story
As HR and as a site engineer, I can see what a worker has done for Kaff before and what he was worth,
because §10's whole purpose for the registry is *"a searchable pool"* — a list of names with no history
behind them tells the next engineer nothing he did not already know.

## What already exists

**Nothing.** There is no engagement, no rating and no day rate anywhere in this codebase
[Verified: 2026-09-09 — a repository-wide search of `src/` for `Engagement`, `DayRate` and `Rating`
returns one file, `src/Domain/MasterData/Employee.cs`, and it is the doc comment quoted below].

The entity says so itself, in as many words: *"Engagement history and per-engagement ratings (spec.md
§10) are not modelled here; they belong to the HR slice"* [Verified: 2026-09-09 @
`src/Domain/MasterData/Employee.cs` -> `Employee`]. **This story is that slice's share of it.**

## ✅ What an engagement is — ruled, D-139 §3 / D-140

§10 asked for *"engagement history and per-engagement ratings"* without saying what one engagement is.
Three readings were registered as `Q72`:

| Reading | *"Frequency"* then means | And the average day rate is over |
|---|---|---|
| One day on one site | how many days he has worked | days |
| **One stretch of work on one project** ✅ | how many times he has been taken on | engagements |
| One project, however many stretches | how many projects he has been on | projects |

**Nabil ruled the middle reading.** *"An engagement is one continuous stretch of work on one project:
not a single day, and not a whole career."* Frequency is a count of engagements, and the average day
rate is over engagements, not days or projects.

**The engagement closes only by an explicit manual close by the Site Engineer** — when the worker
leaves the site or the work scope ends — **and there is no automatic day-count timeout.** Nabil rejected
a timeout because weather halts, holidays and stand-downs would close active engagements by mistake.
The close is an audited state change, checked on role and assignment, under **`Permission.
DayLabourSiteManage`** (`KAFF-209`'s row, D-140) scoped to the project the engagement is on.

**Ratings are out of 5** (D-139 §3). §10's *only other* stated scale — *"a 1–5 quality score"* — is for
**Technical Office tasks**, a different subject, and is not what this number reuses; the two happen to
share a range by coincidence, not by borrowing. **The finer shape of the rating — a whole number, a
decimal, or several weighted criteria — is not ruled** and is registered below rather than assumed.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | A worker carries a **history of engagements**, each with its own rating. The history is a list of events, not a summary | **§10** |
| 2 | ⛔ **The pool's three figures — average day rate, frequency and rating — are DERIVED by reading the engagements, never stored.** Not a column, not a cached total, not a nightly job that writes a number back. **A stored average is a stored balance under another name**, and the rule that forbids one forbids the other for the same reason: the day the two disagree, nobody can tell which is right | **CLAUDE.md** — *"Never store a balance … balances are derived by summing"* · §10 |
| 3 | **The day rate is money.** `decimal` only, never `float` or `double`, at `decimal(18,4)` — carried by the `Money` value object and its EF convention rather than by a bare decimal, so a rate added in a later session inherits the precision and cannot be forgotten [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/KaffDbContext.cs` -> `ConfigureConventions`] | **CLAUDE.md** · slice 0 |
| 4 | ⛔ **A day rate on an engagement is a record of what was agreed, not an instruction to pay.** This story writes no `Posting` and creates no account. Day labour is costed from the daily log and paid as a treasury event (§10); **the five ledgers are not touched and nothing here nets against anything** | **§10** · CLAUDE.md |
| 5 | ✅ **What one engagement is, and what the rating is out of, is ruled — D-139 §3.** An engagement is one continuous stretch of work on one project; ratings are out of 5. **The rating's finer shape** (whole number, decimal, or several weighted criteria) **is not chosen here** and stays open — see Questions | **D-139 §3** |
| 6 | ✅ **The engagement closes only by an explicit manual close, never a timeout.** The Site Engineer closes it when the worker leaves the site or the scope ends; there is no automatic day-count expiry — Nabil rejected one because weather halts, holidays and stand-downs would close active engagements by mistake. **The close is a state change and is audited**, not a silent transition | **D-139 §3** |
| 6a | ⛔ **The close handler must check `engagement.ProjectId == route projectId`.** The permission is project-scoped to the route (D-140), and without this check an engineer assigned to project A could close an engagement on project B by pairing A's route with B's engagement id — D-140's own SM-30 test 14 exists for exactly this | **D-140** |
| 6b | **A rating, once recorded, is an append-only record of something that happened.** It is not edited into a different past: a wrong rating is corrected by recording a correction, the shape the treasury uses for a posting. ⚠️ **This is a consistency argument, not a ruling** — `spec.md` and D-139 §3 make no such demand of a rating, and it is written here rather than as a criterion for that reason. If Kaff wants a rating editable, that is a legitimate later answer | CLAUDE.md's posting rule, **applied by analogy and marked as such** |
| 7 | ✅ **Who opens and closes an engagement is ruled — D-139 §2, mechanism by D-140.** The same `Permission.DayLabourSiteManage` row `KAFF-209` builds, `ProjectScoped`, under the same route prefix, for the Owner and any assigned Site Engineer. ⛔ **Whether a Site Engineer may close an engagement another engineer opened is NOT ruled** — D-140 states the row permits it (both engineers are assigned) and does not refuse it, and this is registered as a question rather than decided | `ux/screen-inventory.md` -> `S-027` · **D-139 §2 · D-140** |
| 8 | **A worker with no engagements shows three explicit empty states**, never a zero. A rate of `0` and an unrated man are different facts and must not render the same | §10 · `spec.md` §4.5's own *"never phantom pre-filled rows"* applied to the same class of surface |
| 9 | ✅ **`Permission.DayLabourSiteManage`, `ProjectScoped`**, Owner and assigned Site Engineer [D-140], for opening, closing and rating. `EmployeeManage`'s own read-only reach over the worker record is unaffected — the Owner and HR keep it. ⛔ **Whether the Site Engineer sees or records the agreed day rate on this route is NOT ruled** — D-140 explicitly leaves it to Nabil, and no money member is projected until it is answered | §2, §10 · D-044 ruling 4 · **D-129 §1, D-139 §2, D-140** |
| 10 | Recording an engagement, closing it and recording a rating are each a state change and **each writes an audit record**: who, when, and what was written. A rating is a judgement about a person that decides whether he is hired again, and it is the one field here somebody will later ask who entered | **CLAUDE.md** |
| 11 | Every string is an i18n key; Arabic RTL at 390px. **The rate is bidi-isolated** — `formatMoney` injects `U+200F` on the leading character, so an amount inside a `<bdi>` in Arabic text needs `dir="ltr"` for the reason the audit timestamp does | CLAUDE.md · `STATUS.md`'s money/bidi note, 2026-09-08 |

## Permissions, money, audit, i18n
- **Permissions:** `Permission.DayLabourSiteManage`, `ProjectScoped`, Owner and assigned Site Engineer
  (D-139 §2, D-140), for opening, closing and rating an engagement — under
  `/api/projects/{projectId:guid}/day-labour`, with the close handler checking `engagement.ProjectId ==
  route projectId` (rule 6a). Every other role refused `403` server-side, including on the read
  (D-110 §2 — the permission test *is* the control on a read).
- **Money:** ⛔ **This story stores a rate and moves no money.** The day rate is `Money` at
  `decimal(18,4)`, never `float`/`double`. **No `Posting` is written, no account is created, no
  balance is stored, and the three pool figures are derived on every read.** The five ledgers are not
  reachable from here and nothing nets against anything. **Whether the Site Engineer's own read/write
  surface carries the day rate at all is still open** (rule 9) — until it is ruled, no money member is
  projected to that role.
- **Audit:** rule 10, on the engagement, its close, and the rating.
- **i18n:** `hr.worker.history_title`, `hr.worker.engagement.project`, `hr.worker.engagement.dates`,
  `hr.worker.engagement.day_rate`, `hr.worker.engagement.rating`, `hr.worker.pool.average_day_rate`,
  `hr.worker.pool.frequency`, `hr.worker.pool.rating`, `hr.worker.pool.never_engaged`.

## Acceptance criteria

**AC-210-A — an engagement is recorded against a worker**
Given a registered worker
When an engagement is recorded with its project, its dates and its agreed day rate
Then it appears in his history on `S-027` and the pool figures on `S-025` change to account for it

**AC-210-B — the three pool figures are derived, not stored** *(fails if the rule is broken)*
Given a worker with three engagements at different rates
When the stored columns of the worker and of the engagement are enumerated as an allow-list
Then no average, no count, no total and no cached rating appears among them
And deleting nothing and recomputing from the engagements alone reproduces every figure the pool shows

**AC-210-C — the day rate keeps four decimals and never passes through a float** *(fails if the rule is broken)*
Given an agreed day rate of `487.6543`
When the engagement is saved and read back
Then the value is exact to the fourth decimal, and it has not passed through a `float` or a `double` at any point between the request body and the database

**AC-210-D — the average is the average of the rates actually paid** *(fails if the rule is broken)*
Given engagements at `300`, `400` and `500`
When the pool renders his average day rate
Then it is `400` computed from those three rows at the moment of the read
And adding a fourth engagement changes it on the next read with nothing else written anywhere

**AC-210-E — this story writes no posting and creates no account** *(fails if the rule is broken)*
Given an engagement recorded at a day rate
When the treasury is inspected
Then no `Posting` and no account was created by it, under any type
And the routes this story maps are enumerated as an allow-list, none of which reaches the treasury

**AC-210-F — an engagement is one continuous stretch on one project, and a rating is out of 5** *(fails if the rule is broken, restated 2026-09-12 — `Q72` answered: D-139 §3)*
Given the history screen and the pool
When frequency and rating render
Then frequency counts **engagements**, not days and not projects, and a rating is refused outside `1`–`5`
And the rating's finer shape (whole number vs. decimal vs. weighted criteria) is not asserted here — it is a separate open question and this criterion validates only the range

**AC-210-G — a worker with no engagements reads as unengaged, not as zero** *(fails if the rule is broken)*
Given a worker registered and never engaged
When `S-025` and `S-027` render him
Then each of the three figures shows `hr.worker.pool.never_engaged` as an explicit empty state — never `0`, never `0.0000`, never a blank cell and never a placeholder row

**AC-210-H — a role without the permission reads and writes nothing** *(fails if the rule is broken)*
Given a signed-in user of each role that does not hold `DayLabourSiteManage`, and an assigned Site Engineer with no assignment to the engagement's project
When each calls the engagement and rating endpoints, and then the pool read, directly with no browser involved
Then every call is refused `403` — **including the read**, because on a read the permission test is the entire control (D-110 §2)

**AC-210-I — the engagement and the rating are audited** *(fails if the rule is broken)*
Given an engagement recorded and then rated
When the audit trail is read
Then each has a record naming the actor, the time, and what was written

**AC-210-J — Arabic, RTL, at mobile width**
Given `S-025` and `S-027` at 390px in Arabic
When they render
Then direction is RTL, the day rate is bidi-isolated with an explicit direction rather than left to first-strong, dates and ratings do not reorder, no string is a literal in either language, and the page body does not scroll horizontally

**AC-210-K — an engagement closes only by explicit manual close, and only within its own project** *(new, appended — D-139 §3, D-140)*
Given an open engagement on project A
When a Site Engineer assigned to A closes it, and separately an engineer assigned only to project B attempts to close it by pairing B's route with A's engagement id
Then the assigned engineer's close succeeds and is audited, the mismatched close is refused `403`, and no automatic process ever closes an engagement on its own

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-210-A` … `AC-210-K` |
| Stable `AC-210-<LETTER>` ids, appended never inserted | ✅ — `K` is new, appended |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–11, 6a, 6b. **Rule 6b is marked as an analogy rather than sourced**, which is the honest label for it |
| No uncited rule | ✅ |
| Permissions named explicitly | ✅ — `Permission.DayLabourSiteManage`, `ProjectScoped`, Owner and assigned Site Engineer (D-139 §2, D-140) |
| Money behaviour named explicitly | ✅ — rules 2, 3, 4; `AC-210-B`, `AC-210-C`, `AC-210-E`. **Stores a rate, derives every summary, writes no posting.** The Site Engineer's own money visibility stays open (rule 9) |
| Arabic UI strings as i18n keys | ✅ — nine keys, rule 11 |
| The audit record it writes is stated | ✅ — rule 10, `AC-210-I`, and the close (rule 6, `AC-210-K`) |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated [Verified: 2026-09-09 — `qa/` holds `slice-1`, `questions.md`, `README.md`, `risk-register.md` and `strategy.md`]. Nine criteria are marked *(fails if the rule is broken)*. **QA's to write, against D-140's SM-30 test 14** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ — `Q72` (D-139 §3) and `Q71` (D-139 §2 / D-140) are both answered. `Q12` is answered (D-129 §1). **New, does not block:** the rating's finer shape, the Site Engineer's day-rate visibility, and closing another engineer's engagement (see Questions) |

**Flip the trailer to `READY` when QA's cases land.**

## Not in this story
- **Registering the worker.** `KAFF-209`.
- **Paying him.** Day labour is costed from the daily log (slice 6) and paid as a treasury event
  (slice 3). ⛔ **No `Posting`, no account, no ledger.**
- **Where an engagement comes from.** ⚠️ **Nothing in slice 2 creates one automatically.** §10 costs
  day labour *"from the daily log"*, and the daily log is slice 6 — so an engagement is entered by hand
  here and may later be raised by the log. **That is an ordering fact worth stating and it is not a
  defect**: the pool has to exist before there is anything to fill it from.
- **Performance review, the eleven weighted KPIs, and the monthly report per engineer.** §10, 🟡, and
  no slice-2 story. **A worker's per-engagement rating is not a performance review** and the two must
  not be merged.
- **The Technical Office's 1–5 quality score.** §10's last bullet, a different subject, and explicitly
  **not** borrowed as this story's scale.

## Questions

| # | Question | Owner |
|---|---|---|
| **`Q72`** | ✅ **ANSWERED — D-139 §3.** An engagement is one continuous stretch of work on one project; ratings are out of 5 | **Closed** |
| **`Q12`** | ✅ **ANSWERED — D-129 §1.** The Owner keeps `EmployeeManage` | **Closed** |
| **`Q71`** | ✅ **ANSWERED — D-139 §2, mechanism by D-140.** The Site Engineer opens, closes and rates under `DayLabourSiteManage`, scoped to his assigned project | **Closed** |
| **`Q76`** | **New, registered.** Whether the Site Engineer sees or records the agreed day rate. D-140 raises this and explicitly leaves it to Nabil — it is money, and D-139 §2 isolates payroll from the site register | **Nabil** |
| **`Q77`** | **New, registered.** Whether a Site Engineer may close an engagement another engineer opened, on the same project. The permission row permits it (both engineers are assigned) and D-140 does not refuse it — Nabil's call | **Nabil** |
| **`Q78`** | **New, registered.** The rating's finer shape — a whole number 1–5, a decimal, or several weighted criteria. D-139 §3 gives the range and not the shape | **Karim** |
| 1 | **Nothing in this slice produces an engagement automatically**, and §10 says day labour is costed from the daily log. **Not a question for Karim** — it is a sequencing fact between slice 2 and slice 6, recorded so that the slice-6 story that raises engagements from the log finds this note rather than a second, parallel history | **Scrum Master**, at slice-6 refinement |
