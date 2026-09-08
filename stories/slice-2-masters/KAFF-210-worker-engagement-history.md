# KAFF-210 · Worker engagement history, day rate, frequency and rating

<!-- kaff id=KAFF-210 slice=2 points=3 state=NOT-BUILT verdict=none at=- on=2026-09-09 -->

**Slice:** 2 (Masters) · **Epic:** Masters · **Points:** 3 (`stories/backlog.md`'s slice-2 table) · **Status:** **NOT-BUILT.** Refined 2026-09-09 by the BA. **Two Definition-of-Ready boxes are unticked, and the unit this story counts is not defined anywhere.**
**Spec:** **§10** (*"Carries engagement history and per-engagement ratings, producing a searchable pool with average day rate, frequency and rating"*), §2 · **Decisions:** D-044 ruling 4, D-044 ruling 6 (four decimals stored, two displayed)
**Register:** `stories/questions-for-karim.md` → **`Q72`** (blocking), **`Q12`** (open, slice-2-wide)
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

## ⛔ What an engagement is, is not written down anywhere

§10 asks for *"engagement history and per-engagement ratings"* and never says what one engagement is.
**Three readings, all ordinary, and each produces a different number in the same column:**

| Reading | *"Frequency"* then means | And the average day rate is over |
|---|---|---|
| One day on one site | how many days he has worked | days |
| One stretch of work on one project | how many times he has been taken on | engagements |
| One project, however many stretches | how many projects he has been on | projects |

**A pool that says *"worked 40 times"* and a pool that says *"worked 4 times"* about the same man are
the same data under two definitions**, and an engineer choosing between two workers reads the number,
not the definition. **Registered as `Q72`, with the rating scale as its second half** — §10 asks for a
rating and never gives a scale; §10's *only* stated scale is *"a 1–5 quality score"*, and it is for
**Technical Office tasks**, not for workers. **Borrowing it would be inventing a rule from an adjacent
sentence**, which is the same move `KAFF-209` refuses on D-049 ruling 8.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | A worker carries a **history of engagements**, each with its own rating. The history is a list of events, not a summary | **§10** |
| 2 | ⛔ **The pool's three figures — average day rate, frequency and rating — are DERIVED by reading the engagements, never stored.** Not a column, not a cached total, not a nightly job that writes a number back. **A stored average is a stored balance under another name**, and the rule that forbids one forbids the other for the same reason: the day the two disagree, nobody can tell which is right | **CLAUDE.md** — *"Never store a balance … balances are derived by summing"* · §10 |
| 3 | **The day rate is money.** `decimal` only, never `float` or `double`, at `decimal(18,4)` — carried by the `Money` value object and its EF convention rather than by a bare decimal, so a rate added in a later session inherits the precision and cannot be forgotten [Verified: 2026-09-09 @ `src/Infrastructure/Persistence/KaffDbContext.cs` -> `ConfigureConventions`] | **CLAUDE.md** · slice 0 |
| 4 | ⛔ **A day rate on an engagement is a record of what was agreed, not an instruction to pay.** This story writes no `Posting` and creates no account. Day labour is costed from the daily log and paid as a treasury event (§10); **the five ledgers are not touched and nothing here nets against anything** | **§10** · CLAUDE.md |
| 5 | ⛔ **What one engagement is, and what the rating is out of, is `Q72`** — see the section above. **No unit and no scale is chosen here**, and the criteria that would assert either are held | **`Q72`** — uncited, therefore asked |
| 6 | **An engagement is an append-only record of something that happened.** It is not edited into a different past: a wrong rating is corrected by recording a correction, the shape the treasury uses for a posting. ⚠️ **This is a consistency argument, not a ruling** — `spec.md` makes no such demand of an engagement, and it is written as rule 6 rather than as a criterion for that reason. If Kaff wants a rating editable, that is a legitimate answer to `Q72`'s neighbourhood | CLAUDE.md's posting rule, **applied by analogy and marked as such** |
| 7 | **Who rates a worker is the site engineer who engaged him**, and `ux/screen-inventory.md` gives `S-027` to `HR, O, SE`. ⚠️ **Whether a site engineer may rate a worker he did not engage is not stated** anywhere and is not decided here; it rides with `Q71`, which is already asking who reaches a worker record at all | `ux/screen-inventory.md` -> `S-027` · **`Q71`** |
| 8 | **A worker with no engagements shows three explicit empty states**, never a zero. A rate of `0` and an unrated man are different facts and must not render the same | §10 · `spec.md` §4.5's own *"never phantom pre-filled rows"* applied to the same class of surface |
| 9 | `EmployeeManage`, `CompanyWide`, no assignment, for HR and the Owner today [Verified: 2026-09-09 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.EmployeeManage`]; **the site engineer's reach is `Q71`.** The Owner grant stands on **`Q12`** | §2, §10 · D-044 ruling 4 · **`Q12`**, **`Q71`** |
| 10 | Recording an engagement and recording a rating are state changes and **each writes an audit record**: who, when, and what was written. A rating is a judgement about a person that decides whether he is hired again, and it is the one field here somebody will later ask who entered | **CLAUDE.md** |
| 11 | Every string is an i18n key; Arabic RTL at 390px. **The rate is bidi-isolated** — `formatMoney` injects `U+200F` on the leading character, so an amount inside a `<bdi>` in Arabic text needs `dir="ltr"` for the reason the audit timestamp does | CLAUDE.md · `STATUS.md`'s money/bidi note, 2026-09-08 |

## Permissions, money, audit, i18n
- **Permissions:** `EmployeeManage`, `CompanyWide`, no assignment — **and `Q71` is open on whether the
  site engineer who does the engaging can record it.** Every other role refused `403` server-side.
- **Money:** ⛔ **This story stores a rate and moves no money.** The day rate is `Money` at
  `decimal(18,4)`, never `float`/`double`. **No `Posting` is written, no account is created, no
  balance is stored, and the three pool figures are derived on every read.** The five ledgers are not
  reachable from here and nothing nets against anything.
- **Audit:** rule 10, on both the engagement and the rating.
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

**AC-210-F — HELD on `Q72`: what one engagement is, and what the rating is out of**
Given the history screen and the pool
When frequency and rating render
Then — **held.** `Q72` decides the unit and the scale. **No unit is implemented and no scale is validated until it is ruled**, and a number rendered under the wrong definition is the failure this hold exists to prevent

**AC-210-G — a worker with no engagements reads as unengaged, not as zero** *(fails if the rule is broken)*
Given a worker registered and never engaged
When `S-025` and `S-027` render him
Then each of the three figures shows `hr.worker.pool.never_engaged` as an explicit empty state — never `0`, never `0.0000`, never a blank cell and never a placeholder row

**AC-210-H — a role without the permission reads and writes nothing** *(fails if the rule is broken)*
Given a signed-in user of each role that does not hold `EmployeeManage`
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

## Definition of Ready — where this story stands

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ — `AC-210-A` … `AC-210-J`. **`AC-210-F` is written and held** |
| Stable `AC-210-<LETTER>` ids, appended never inserted | ✅ |
| Every business rule cites a `spec.md` section or a D-number | ✅ — rules 1–11. **Rule 6 is marked as an analogy rather than sourced**, which is the honest label for it |
| No uncited rule | ✅ — the unit and the scale are registered as `Q72` instead of being written, and the one rule reasoned by analogy says so in its own source column |
| Permissions named explicitly | ✅ — `EmployeeManage`, CompanyWide, no assignment; the site engineer's reach flagged to `Q71`, the Owner's to `Q12` |
| Money behaviour named explicitly | ✅ — rules 2, 3, 4; `AC-210-B`, `AC-210-C`, `AC-210-E`. **Stores a rate, derives every summary, writes no posting** |
| Arabic UI strings as i18n keys | ✅ — nine keys, rule 11 |
| The audit record it writes is stated | ✅ — rule 10, `AC-210-I` |
| **QA has written at least one scenario that fails if the rule is broken** | ⛔ **Not met.** `qa/slice-2/` does not exist and no `TC-` range is allocated [Verified: 2026-09-09 — `qa/` holds `slice-1`, `questions.md`, `README.md`, `risk-register.md` and `strategy.md`]. Seven criteria are marked *(fails if the rule is broken)*. **QA's to write** |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ⛔ **Not met — `Q72`**, which defines the unit this story counts and the scale it rates on, plus **`Q12`** and **`Q71`** |

**Flip the trailer to `READY` when `Q72` and `Q12` are ruled and QA's cases land.** `Q71` shapes who
may write, not what is written.

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
| **`Q72`** | **New, raised here and blocking.** What counts as one engagement — a day, a stretch of work, or a whole project — and what is a worker rated out of? Asked as one, because the same conversation answers both and because *"frequency"* is meaningless until the first half is settled | **Karim** |
| **`Q12`** | Open, slice-2-wide. Whether the Owner keeps `EmployeeManage` | **Karim** |
| **`Q71`** | Raised by `KAFF-209`. Whether the site engineer who engages a worker can record and rate the engagement, and whether that is scoped to his project | **Karim** |
| 1 | **Nothing in this slice produces an engagement automatically**, and §10 says day labour is costed from the daily log. **Not a question for Karim** — it is a sequencing fact between slice 2 and slice 6, recorded so that the slice-6 story that raises engagements from the log finds this note rather than a second, parallel history | **Scrum Master**, at slice-6 refinement |
