# KAFF-600 · The daily log core — one log per engineer per day, recording deltas

<!-- kaff id=KAFF-600 slice=6 points=13 state=VERIFIED verdict=PASS at=9fb1c83 on=2026-11-16 -->

**Slice:** 6 (Execution) · **Epic:** Site execution · **Points:** 13 (absorbs `KAFF-601`, `KAFF-602`, `KAFF-603`, `KAFF-611`).
**Owner:** Backend, then Frontend.
**Spec:** `spec.md` §8.
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, step 5.

## Why this precedes billing

The backlog puts billing (slice 5) before execution (slice 6). The three-expert study reversed that
and the Scrum Master adopted the reversal: **an extract is assembled from executed quantities.** Built
in backlog order, the extract chain would have nothing real to bill and would be driven by typed
numbers — the same defect the BOQ work just removed from contract value.

`spec.md` §4 already names the three quantity states, and the third is "**Executed** — captured from
the site daily log" (`spec.md:124`). `Boq` and `BoqLine` ship with `quantity_executed` as the one
column that still moves on a frozen line. Nothing writes to it. This story is what writes to it.

## Story

A site engineer opens one log per project per day, on projects he is assigned to, and records what
happened on site that period: executed quantities, day labour, materials and تشوينات, photos, and
check-in and check-out times.

The log is the highest-volume screen in this product and the one served by nothing today. It is
designed mobile-first and offline-first.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | One daily log per engineer per project per day. A second one for the same triple is the same log, not a new one | `spec.md` §8 |
| 2 | Only an engineer assigned to the project may open or write a log on it. Role alone is insufficient, and the check is server-side | `spec.md` §8, §9 |
| 3 | **Every quantity field is a period delta, never a cumulative total** | `spec.md` §8 · `CLAUDE.md` |
| 4 | Two different engineers' entries for the same day **sum** | `spec.md` §8 |
| 5 | The same engineer, same day, same field: **latest timestamp wins** | `spec.md` §8 |
| 6 | Executed quantities from the log accumulate onto `BoqLine.QuantityExecuted`, which is additive by rule 3 and legal on a frozen line | `spec.md:124` · `003_boq_freeze.sql` |
| 7 | A stopped project still accepts a log recording the stoppage and its reason | `spec.md` §8 |
| 8 | Photos are client-visible by default and are **published deliberately, not mirrored automatically** | `spec.md` §8 |
| 9 | An engineer's expense entry is a **draft** that Accounts confirms and posts. The engineer never posts money | `spec.md` §8 |
| 10 | **Money never moves offline.** Offline produces drafts only; approval and disbursement happen online against a live balance | `spec.md` §8 · `CLAUDE.md` |
| 11 | **`OccurredAt` belongs on every field entity, separate from `CreatedAt`.** `CreatedAt` is the sync time; without the entry's own time a log entered offline across midnight lands on the wrong day | the journey study, "Design principles" |
| 12 | Every state change writes an audit record through the existing interceptor, not per-feature code | `CLAUDE.md` |

## Acceptance criteria

**AC-600-A — one log per engineer per project per day**
Given an engineer assigned to a project who already opened today's log
When he opens the log again for the same project and day
Then he reaches the same log, and no second log exists
*Rule: 1.*

**AC-600-B — assignment gates the log, server-side**
Given an engineer with the Site Engineer role but no assignment to the project
When he calls the log endpoints directly, bypassing the UI
Then every call is refused
*Rule: 2.*

**AC-600-C — quantities are deltas**
Given a log recording 40 units executed on a line, and a later log recording 25 on the same line
When the executed quantity for that line is read
Then it is 65, because each entry added its own period's amount and neither replaced the other
*Rules: 3, 6.*

**AC-600-D — two engineers on the same day sum**
Given two engineers both assigned to one project, each recording work on the same line on the same day
When the day's executed quantity is read
Then it is the sum of both entries
*Rule: 4.*

**AC-600-E — the same engineer's correction replaces, by timestamp**
Given one engineer who records a single-valued field, then records that same field again on the same day
When the value is read
Then the later timestamp's value stands and the earlier does not also count
*Rule: 5. See question 1 below for which fields this governs.*

**AC-600-F — the executed quantity reaches a frozen BOQ line**
Given a signed project whose BOQ is frozen
When a log records an executed quantity against one of its lines
Then `quantity_executed` moves and the database does not refuse it, while every other column on that line stays refused
*Rule: 6 · the freeze guard in `003_boq_freeze.sql`.*

**AC-600-G — a stopped project still takes a log**
Given a project in `Stopped`
When an assigned engineer records a log entry naming the stoppage and its reason
Then the entry is accepted
*Rule: 7.*

**AC-600-H — an engineer's expense is a draft and posts nothing**
Given an assigned engineer
When he enters a site expense
Then it is stored as a draft and no posting is written

> **Changed 2026-11-15 (D-225 item 3, Nabil).** The second half of this criterion, Accounts confirming and posting the draft, leaves this story. It is built in release 2 with the site money flows. Until then a draft stays a draft.
*Rules: 9, 10.*

**AC-600-I — occurred-at is the engineer's time, not the sync time**
Given an entry made at 23:50 local and synced at 00:20 the next day
When the entry's day is determined
Then it belongs to the day it occurred on, not the day it arrived
*Rule: 11.*

**AC-600-J — the engineer's screen carries no money**
Given the daily log screen at mobile width
When an engineer uses it
Then it shows quantities, hours, counts, photos and materials by type, and **no rate, amount, cost or
balance anywhere** — built from a money-free read model, not a desk screen with columns hidden
*The journey study, "Design principles". This is a separate read, not a filtered one.*

**~~AC-600-K~~ — RETIRED 2026-11-01** · superseded by `AC-600-K1` and `AC-600-K2` below. The original text is kept exactly as it was when `KAFF-600` was built, so a Verifier reads the history in order:

> **AC-600-K — Arabic RTL at mobile width**
> Given the log on a phone in Arabic
> When it is used one-handed
> Then the layout is correct RTL with logical properties, and the primary action sits in the shell's
> bottom bar per the phone primary-action contract
> *`CLAUDE.md` · `stories/open-decisions-2026-10-08.md` closed item 9.*

**Why it is retired, stated plainly.** `AC-600-K` was never satisfiable as written. It conflated two things: a layout check that can be run on the screen, and a placement that depends on a contract that did not exist. Closed item 9 of `stories/open-decisions-2026-10-08.md` rules that a screen declares one `primaryAction` and the shell places it in the bottom bar on phone, but no code declared or placed anything: the shell's `abar` carries only the menu button, and nothing joins it. The screen as built (`2f1879b`, `b78489e`) therefore uses a private `.footer` in `daily-log-page.html`, copied from `worker-register-page`. That is not a defect in the build, because the criterion asked for something no builder could deliver. The story that builds the contract is `KAFF-936` (`stories/design-system/KAFF-936-the-primary-action-slot.md`), which is `NOT-BUILT` and blocked on a Definition-of-Ready box. The same shape of unexecutable criterion is why `KAFF-125` has no standing verdict. This record is here so that it is not repeated.

**Dependency.** `KAFF-936` now exists on the board with an identifier, which is what `process/agile.md` section 2a rule 6 (D-117) requires before a criterion can legitimately depend on a story. `AC-936-J` is the criterion in `KAFF-936` that discharges the move of this screen onto the contract.

**AC-600-K1 — Arabic RTL at mobile width, as the screen is today**
Given the daily log in Arabic at 390px
When an engineer uses it
Then the layout is correct RTL, there is no horizontal overflow, the screen's CSS uses logical properties only (`margin-inline-start`, `inset-block-end`; no `margin-left`, `left` or `right`), and the save action is visible and reachable without horizontal scrolling in its current pinned footer
*`CLAUDE.md`, "RTL is the primary direction, not a mirror" · `KAFF-936`, "What the code looks like today" (the daily log's private `.footer` is the current placement). Executable now; it holds nothing back on `KAFF-936`. No touch-target height is asserted for the interim footer: the 56 to 60px figure is a rule for the shell bar (`KAFF-936` rule 9), not for this footer.*

**AC-600-K2 — the primary action sits in the shell's bottom bar — HELD on `KAFF-936`**
Given the daily log at 390px, with the primary-action contract built
When the page renders
Then the save action is declared to the shell and appears in the bottom bar beside the menu button, the screen's private `.footer` markup and CSS are gone, and the `data-testid` `daily-log-save` still reaches the rendered button
*Closed item 9 of `stories/open-decisions-2026-10-08.md` · `KAFF-936` `AC-936-C` and `AC-936-J`. **HELD on `KAFF-936`**: not executable, and not to be reported as failed or passed, until that story is built. When it lands, `AC-936-J` and this criterion are verified together; the daily log declares exactly one action, so the two-or-more case (`Q117`, `AC-936-F`) does not apply to it. Closed item 9 also makes zero actions legal (the menu expands to fill the bar), but the daily log declares one.*


## Not in this story

The extract chain and the billing calculators (slice 5, and they consume this). عهدة from site
(`KAFF-609`, `KAFF-610`). Check-in and check-out as a worked time record beyond storing the two
timestamps (`KAFF-607`). The weekly QC report (`KAFF-612`). The offline sync transport itself — this
story makes the data model additive so that sync *can* be conflict-free, and stops there. Photo
publication workflow beyond rule 8's default (`KAFF-606`).

## Questions for Karim

None blocking — `spec.md` §8 is unusually complete for this area. Two to confirm when convenient, both
recorded rather than decided:

1. Rule 5 says latest timestamp wins for the same engineer, same day, same field. **Is a quantity a
   "field" for this purpose, or does a second entry on the same line mean he did more work?** Rule 3
   says deltas, which implies it adds; rule 5 implies it replaces. The two rules collide on exactly
   this case. Running assumption: a quantity on a BOQ line **adds** (it is a delta by rule 3), and
   "latest wins" governs single-valued fields such as check-in time, weather, or a note. This goes in
   `RulingSet` so it is a one-line change.
2. May a junior engineer write a log directly, or does he draft it for his supervisor to submit, as he
   does for requests? `spec.md` §9 makes the junior a drafter for *requests*; a log is not obviously a
   request. Running assumption: the junior writes his own log directly.

## Build note (Backend, 2026-11-01)

Domain in `src/Domain/DailyLogs/` (`DailyLog`, `LogEntry`, `DailyLogErrors`). Seven slices under
`src/Api/Features/DailyLogs/` — `OpenDailyLog`, `GetMyLog`, `RecordQuantity`, `RecordField`,
`RecordDayLabour`, `RecordMaterial`, `RecordPhoto`, `DraftExpense` — sharing `DailyLogAccess.cs` for the
role-and-assignment check. EF configuration plus migration `20261101093937_DailyLogs`. Two new
permissions: `DailyLogWrite`, `SiteExpenseDraft`.

Question 1 is **registered, not answered**: `Rulings/DailyLogQuantityEntry.cs`, `Mode = Adds`, status
`Assumed`. `Effective()` implements `Adds` and `LatestWins` both, so Karim's ruling is one line in
`RulingSet.Default` with no handler change. Ties break on entry id so the result never depends on
arrival order — which offline sync does not preserve. A second assumption the story had not foreseen
came out of the build and is registered the same way: `Rulings/DailyLogProjectStates.cs`, because
`spec.md` §8 fixes only the Stopped cell and is silent on the other five states.

`OccurredAt` sits beside `CreatedAt` on every entry (rule 11, `AC-600-I`). `GetMyLog` is money-free by
construction (`AC-600-J`'s API half): no member for a rate, amount, cost or balance, and the handler
projects only those columns. `DraftExpense` writes no posting.

**Evidence:** `tests/Domain.Tests/DailyLogTests.cs` and `tests/Api.Tests/Features/DailyLogs/DailyLogTests.cs`.
Build clean, 0 warnings. Domain.Tests 395 of 395 (baseline was 389). Api.Tests 722 of 722 (baseline was
699, so 23 new). The daily-log subset runs 23 of 23.

**Not done:** `AC-600-J`'s screen and `AC-600-K` entirely — there is no Angular daily log, so none of
this is reachable by clicking, and RTL at mobile width is therefore untested. No verifier has looked at
any of it.

**Honest note on how this landed.** The building agent was terminated mid-run by an account session
limit before it wrote this note or the `decisions.md` entry. Both were written afterwards by the
coordinator, from the committed code rather than from the agent's report, and every gate figure above
was re-run rather than quoted. The last thing the agent said was "engineers need Junior level, fix test
and rerun" — by the time it stopped, it had already made that fix, and the 23 daily-log tests pass.
