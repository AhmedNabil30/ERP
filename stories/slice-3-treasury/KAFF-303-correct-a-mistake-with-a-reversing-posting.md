# KAFF-303 · Correct a mistake with a reversing posting, never an edit

<!-- kaff id=KAFF-303 slice=3 points=5 state=BUILT verdict=none at=- on=2026-09-15 -->

**Slice:** 3 (Treasury) · **Epic:** Treasury · **Points:** 5 (`stories/backlog.md` slice-3 table) · **Status:** NOT-BUILT.
**Spec:** §6.1 ("Corrections are new reversing postings referencing the original") · **Decisions:** D-007 (cited by `Posting.Reverse`'s own remarks — partial reversals are a spec change, not a loosening)
**Register:** none of `Q14`/`Q15`/`Q16`/`Q29` gate this story. New: `Q89`
**Owner:** Backend
**Depends on:** `Posting.Reverse` already exists and is fully built in the domain
[Verified: 2026-09-14 @ `src/Domain/Treasury/Posting.cs` -> `Reverse`]. `KAFF-301` (the posting endpoint)
should land first so both endpoints share one request/response shape, but nothing in `Reverse` itself
requires it.

## Why this story exists

`Posting.Reverse` and the database's mirror-check (`kaff_postings_validate`'s `reverses_id` block) are
already written and tested at the domain level
[Verified: 2026-09-14 @ `tests/Domain.Tests/PostingRuleTests.cs`]. No endpoint exposes it. This story is
the `POST` that lets Finance correct a posting by reversing it — never by editing or deleting the row,
which `CLAUDE.md` and `spec.md` §6.1 both forbid outright and which the database has no path for at all.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | Postings are never updated or deleted. A correction is a new reversing posting referencing the original through `ReversesId` | `spec.md` §6.1 · `CLAUDE.md` |
| 2 | A reversal is a full mirror of its original — same amount, same type, same source document, accounts swapped. There is no partial reversal and no free-form correction with its own reference field | [Verified: 2026-09-14 @ `Posting.cs` -> `Reverse` remarks, citing D-007] |
| 3 | A posting can be reversed once only, enforced by a unique index on `reverses_id` | [Verified: 2026-09-14 @ `src/Infrastructure/Persistence/Configurations/TreasuryConfigurations.cs` -> `ux_postings_reverses`] |
| 4 | A reversal cannot itself be reversed — one correction per posting, and the correction is final | [Verified: 2026-09-14 @ `001_guards.sql` -> `kaff_postings_validate`, error `KAFF_REVERSAL_OF_REVERSAL`] |
| 5 | A reversal is exempt from the "hold only grows" rule, because it corrects an accrual that should never have existed rather than withdrawing from the hold | `spec.md` §5.1 · [Verified: 2026-09-14 @ `Posting.cs` -> `Validate`, the `!isReversal` branch; `001_guards.sql` `reverses_id IS NULL` clause on `KAFF_HOLD_DEBIT`] |
| 6 | A reversal must date into an open accounting period like any other posting | `spec.md` §6.6 · [Verified: 2026-09-14 @ `001_guards.sql` `KAFF_CLOSED_PERIOD` check runs on every insert, reversals included] |
| 7 | Every state change writes an audit record — who, when, what changed | `CLAUDE.md` |
| 8 | Rejections return to origin with a reason — a reversal that fails validation is refused, not silently dropped | `CLAUDE.md` |

## Permissions, money, audit, i18n

- **Permissions.** `Posting.Reverse` is a posting like any other, so the same permission that gates
  creating one gates correcting it: `Permission.TreasuryPostProject` / `Permission.TreasuryPostCompany`
  (Finance) — same citation as `KAFF-301`. **⛔ Open question, not answered by `spec.md` §9 or any
  decision:** whether the person reversing a posting may be the same person who created the original.
  `spec.md` §9's separation-of-duties rule is written for *"creates and approves"*, a create/approve
  pair — a reversal is a second **create**, not an approval, so the rule as stated does not plainly
  cover it either way. `CLAUDE.md`'s "Nobody creates and approves the same movement" is the same
  wording and the same gap.
- **Money.** No new money rule beyond `KAFF-301`'s — a reversal is exact, unrounded, and carries the
  original's amount verbatim (rule 2).
- **Audit.** The reversal's own row already carries who/when/what via `CreatedByUserId`/`CreatedAt`
  and `ReversesId` (rule 7 is satisfied structurally by the posting itself, same as `KAFF-301`'s note).
- **i18n.** `errors.treasury.reversal_must_mirror_original` and `errors.treasury.posting_already_reversed`
  already exist [Verified: 2026-09-14 @ `en.json` lines 249-250]. No new string needed for the rules
  named above; `KAFF_REVERSAL_OF_REVERSAL`'s database message has no matching domain `Error` today —
  see *Not in this story* / `KAFF-319`.

## Acceptance criteria

**AC-303-A — reversing a posting creates a mirror with accounts swapped**
Given an existing posting from account X to account Y for amount N
When it is reversed
Then a new posting is created from Y to X for amount N, same type, same source document, with `ReversesId` set to the original's id
*Rule: 1, 2.*

**AC-303-B — a posting already reversed cannot be reversed again**
Given a posting that already has a reversal
When a second reversal of the same original is attempted
Then it is refused with `errors.treasury.posting_already_reversed`
*Rule: 3.*

**AC-303-C — a reversal cannot itself be reversed**
Given a posting that is itself a reversal (`ReversesId` is set)
When a reversal of it is attempted
Then it is refused — HELD on the exact i18n key, since `KAFF_REVERSAL_OF_REVERSAL` has no domain-level `Error` yet
*Rule: 4. See `KAFF-319`'s scope note.*

**AC-303-D — the hold ledger accepts a reversal that debits it, where a normal posting would be refused**
Given a Hold account that has accrued value from a `HoldAccrual` posting made in error
When that posting is reversed
Then the reversal succeeds even though it moves value out of the hold before handover
*Rule: 5.*

**AC-303-E — a reversal dated into a closed period is refused**
Given a closed `AccountingPeriod` covering the reversal's intended date
When the reversal is attempted
Then it is refused with `errors.treasury.closed_period`
*Rule: 6.*

**AC-303-F — role without project assignment cannot reverse a project-tagged posting**
Given a Finance user with `TreasuryPostProject` but no assignment on the project
When they attempt to reverse a posting on that project
Then the request is refused server-side with a 403
*`CLAUDE.md` — role and assignment, both, server-side, always.*

## Definition of Ready

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ |
| Stable `AC-303-<LETTER>` ids | ✅ |
| Every business rule cites a `spec.md` section, a D-number, or a `[Verified: ...]` citation | ✅ |
| No uncited rule | ✅ |
| Permissions named explicitly | ⚠️ Base permission named; whether the same user may reverse their own posting is **open** |
| Money behaviour named explicitly | ✅ |
| Arabic UI strings as i18n keys | ⚠️ One key gap named (`KAFF_REVERSAL_OF_REVERSAL`), routed to `KAFF-319` |
| The audit record it writes is stated | ✅ |
| QA has written at least one scenario that fails if the rule is broken | ✅ **Met, 2026-09-14.** `qa/slice-3/test-cases.md` `TC-3-040`…`045` — C HELD cross-cutting `KAFF-319` |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ — the open question narrows one permission detail on one criterion (`AC-303-A`'s actor), not the story |

## Not in this story

- **The forward posting endpoint.** `KAFF-301`.
- **Mapping the database's `KAFF_REVERSAL_OF_REVERSAL` exception text to a domain `Error`/i18n key.** `KAFF-319`.
- **Any ledger-specific correction workflow** (a specific "undo this عهدة settlement" screen, for
  instance) — this story is the generic reversal primitive every such screen would call.
- **A second-approver requirement for reversals**, since it is not ruled either way — see Questions for Karim.

## Questions for Karim

| # | |
|---|---|
| **`Q89`** | **"When Finance reverses a posting they made in error, can they do it themselves, or does someone else — a supervisor, the Owner — need to approve the reversal, the way the Owner approves the original movement?"** Say why it is being asked: `spec.md` §9's "nobody creates and approves the same movement" is written for a create/approve pair on one movement; a reversal is a second creation of a mirrored movement, and nothing says whether it inherits the same-actor prohibition, needs its own approval step, or is free of both. **Not inferred here; affects `AC-303-A` and `AC-303-F`'s actor rule.** |
