# KAFF-322 · Treasury guard tests against real PostgreSQL

<!-- kaff id=KAFF-322 slice=3 points=2 state=BUILT verdict=none at=- on=2026-09-14 -->

**Slice:** 3 (Treasury) · **Epic:** Treasury · **Points:** 2 · **Status:** BUILT, 2026-09-14.
**Spec:** §5.1 ("the hold releases once, in full, at handover"), §6.1 ("the safe balance MUST NOT go
negative... enforce in the database, not only in application code"), §15 ("advance ledger reaches
exactly zero, never negative") · **Decisions:** D-044 §8 (which accounts carry the non-negative floor)
**Owner:** Backend
**Depends on:** `KAFF-300` (the §15 worked example fixture, `tests/Domain.Tests/Section15WorkedExampleTests.cs`)
and `KAFF-301` (`tests/Api.Tests/TreasuryGuardTests.cs` and its `PostgresDatabase`/`DatabaseGuard`
test infrastructure, already built and reused here unchanged).

## Why this story exists

`Section15WorkedExampleTests` carried two `[Fact(Skip = ...)]` placeholders,
`A_partial_hold_release_is_refused_by_the_database` and
`A_fourth_advance_recovery_is_refused_by_the_database`. Both document, in their own skip reason, that
the rule they name is enforced by a PostgreSQL trigger — `trg_postings_hold_release_in_full` and the
non-negative-balance constraint raising `KAFF_NEGATIVE_BALANCE` — and that a `Domain.Tests` fixture,
which never touches a real database, cannot exercise either. They existed only so the gap was visible
in the build output rather than silently absent (`decisions.md` D-034's rule that a required fixture
must be "failing or skipped, but present"). This story closes the gap: two real cases in
`Kaff.Api.Tests`, against the same PostgreSQL the other `TreasuryGuardTests` cases already use, proving
both refusals actually fire.

## What was built

Two new `[Fact]` cases in `tests/Api.Tests/TreasuryGuardTests.cs`, using the exact scenario numbers
from `Section15WorkedExampleTests` so each proves the same fact the skipped Domain test named, not a
different one:

- **`A_partial_hold_release_is_refused_by_the_database`** — accrues the hold to 200,000 across three
  postings (60,000 + 60,000 + 80,000, §15's own Extract 1–3 figures), then attempts a `HoldRelease` of
  150,000 — less than the full balance. Asserts the database refuses it and the refusal carries
  `KAFF_HOLD_PARTIAL_RELEASE` (`src/Infrastructure/Persistence/Sql/001_guards.sql`,
  `trg_postings_hold_release_in_full`).
- **`A_fourth_advance_recovery_past_zero_is_refused_by_the_database`** — receipts 250,000 into
  `ClientAdvance`, recovers it to exactly zero across three postings (75,000 + 75,000 + 100,000, §15's
  own figures), confirms the balance reads zero, then attempts a fourth recovery of 10,000. Asserts the
  database refuses it and the refusal carries `KAFF_NEGATIVE_BALANCE`
  (`001_guards.sql`, `kaff_check_non_negative_balance`).

Both reuse the existing `PostgresDatabase`/`DatabaseGuard` fixture pattern already in
`tests/Api.Tests/Infrastructure/` — no new way of reaching a database was introduced. `DatabaseGuard.cs`
gained one new marker constant, `HoldPartialRelease = "KAFF_HOLD_PARTIAL_RELEASE"`, alongside the
existing `NegativeBalance`, `HoldDebit`, etc.

## Domain.Tests disposition — deleted, not left skipped

The two skipped Domain.Tests cases were **deleted**, replaced with a short comment pointing at their
`Kaff.Api.Tests` replacement by name. Reasoning: a test that can never pass and will never run is not
documentation, it is a card index — a permanently red-or-skipped test earns its place in the suite only
if it can eventually go green or if its prose carries information the passing suite doesn't. Here the
`XML` doc remarks on both skipped methods (why Domain.Tests structurally cannot prove the rule, which
trigger enforces it, which file the real proof belongs in) were already duplicated almost verbatim into
the corresponding `[Fact(Skip = ...)]` reason string; that same information now lives as citations in
the two new `TreasuryGuardTests` cases and in this story. Keeping the two skipped methods around after
their replacement exists would have meant two ways to answer "is the partial hold release / fourth
advance recovery refusal proven" — one always-skipped and one real — which is the drift `CLAUDE.md`'s
`stories/backlog.md` state-column warning (D-119, D-122) already exists to prevent for story state; the
same logic applies to test state. `Section15WorkedExampleTests.This_fixture_is_present_and_runs_real_assertions_not_a_vacuous_pass`
still passes — it asserts at least 20 `[Fact]` methods exist; the file now has 21.

## Acceptance criteria

**AC-322-A — a partial hold release is refused by the database, not by the domain**
Given a hold account accrued to 200,000 across three postings
When a `HoldRelease` posting for 150,000 (less than the full balance) is saved
Then `SaveChangesAsync` throws a `PostgresException` whose message contains `KAFF_HOLD_PARTIAL_RELEASE`

**AC-322-B — a fourth advance recovery past zero is refused by the database, not by the domain**
Given a `ClientAdvance` account receipted to 250,000 and recovered to exactly zero across three postings
When a fourth `ClientAdvanceRecovery` posting for any positive amount is saved
Then `SaveChangesAsync` throws a `PostgresException` whose message contains `KAFF_NEGATIVE_BALANCE`

## Definition of Ready / Done

| Item | |
|---|---|
| Builds clean, warnings as errors | ✅ |
| `dotnet format` clean on changed files | ✅ |
| `dotnet test tests/Domain.Tests` passes | ✅ |
| `dotnet test tests/Api.Tests` passes | ✅ |
| No new dependency, no new way of reaching a database | ✅ — reuses `PostgresDatabase`/`DatabaseGuard` |
| `decisions.md` updated | not needed — no structural decision, a test-coverage gap closed per an existing decision (D-034) |

## Not in this story

- No change to any guard SQL, domain rule, or endpoint behaviour. This is test coverage only.
- No new `DatabaseGuard` marker beyond `HoldPartialRelease` — the negative-balance case reuses the
  existing `NegativeBalance` marker (`The_safe_balance_cannot_go_negative` already covers the Safe
  account; this story's case is the same guard function, `AccountType.ClientAdvance` instead).
