# Verification — slice 1 re-verification: KAFF-109, 105a, 102, 101a, 103 · 2026-08-27

**Verifier Agent, fresh session.** Wrote none of this code and none of the fixes. Read `CLAUDE.md`,
`agents.md` §7 and §3c, `process/agile.md` (SM-29 / SM-30 / SM-31), `spec.md` §9,
`meetings/2026-08-27-sprint-1-close.md` §1–§2, `meetings/2026-08-27-sprint-1-retrospective.md` §2–§3,
and `qa/slice-1/verification-2026-08-26.md` — the pass whose verdicts are re-tested here.

**The prior verdicts carry no weight.** Five stories' shipped code moved after the session that judged
it, two of them *after acceptance*. Every verdict below is established against the files at HEAD
`559ac45`, never inherited.

**The Verifier reports. It does not fix.** Nothing under `src/`, `tests/`, `stories/` or `decisions.md`
was changed. Mutations were applied, watched, and reverted; the tree is verified clean after each.

Citations are `[Verified: 2026-08-27 @ `File` -> `Identifier`]` per SM-31 — identifier, never a line
number.

**Three verdicts, never two:** *accept* · *reject* · *not verifiable in this session, with the reason*.
A story that could not be verified is **not accepted**.

---

## 0. Progress of this report

Written incrementally and committed as each section closed, per the brief.

| Story | Reached | Verdict |
|---|---|---|
| KAFF-109 change a user's role | **in progress** | pending — `V-27-A` open against it |
| KAFF-105a `GET /api/auth/me` | pending | pending |
| KAFF-102 sign-out | pending | pending |
| KAFF-101a sign-in | pending | pending |
| KAFF-103 change password | pending | pending |

---

## 1. Baseline — the gate before any test result is trusted

`Kaff.Api`, `Kaff.Api.Tests` and `Kaff.Domain.Tests` were killed before the build (D-069 §6 / the
skill's MSB3026 trap). `docker start kaff-db` rather than `docker compose up -d db`, so the connection
pool was not invalidated.

| Gate | Result |
|---|---|
| `docker start kaff-db` | container up, healthy |
| `dotnet build KaffErp.sln -c Release --no-incremental` | **Build succeeded, 0 Warning(s), 0 Error(s)** |
| `MSB3021` / `MSB3026` / `MSB3027` in the log | **none** — all six projects emitted a fresh output line, `Kaff.Api.dll` included |
| `Kaff.Domain.Tests.exe` | **97 / 97**, 0 failed |
| `Kaff.Api.Tests.exe` | **215 / 215**, 0 failed |
| `scripts/check-citations.ps1` | **883 checked, 0 broken, 0 legacy** |

Matches the brief's stated baseline on every figure.

---

## 2. Mutating the rule, not only the route — `ck_users_subcontractor_cannot_log_in`

The retrospective's change 1 names this constraint by name: *"Drop the database constraint. Delete the
domain guard. If nothing goes red, nothing covers it — however many tests pass."* It was the vacuous
guard behind `V-26-A`. D-088 claims that is fixed. **Proved by removing things, not by reading the
seeding code.**

Three mutations, each built clean (`0 warnings / 0 errors`) and run against both suites, each reverted.
The tree was confirmed clean by `git status` after each.

| # | What was removed | Domain | Api | The one test that moved |
|---|---|---|---|---|
| **MUT-A** | the **constraint only** — `HasCheckConstraint("ck_users_subcontractor_cannot_log_in", …)` deleted from `IdentityConfigurations.cs` | **97 / 97** | **215 / 215** | **none — nothing went red** |
| **MUT-B** | the **domain guard only** — D-088's `role == Role.Subcontractor && PasswordHash is not null` block deleted from `User.ChangeRole` | 96 / 97 | 214 / 215 | `UserTests` -> `ChangeRole_refuses_a_subcontractor_conversion_while_a_credential_is_stored`; `ChangeUserRoleTests` -> `Converting_an_account_that_holds_a_credential_into_a_subcontractor_is_refused`, **`found HttpStatusCode.InternalServerError {500}`** |
| **MUT-C** | **both** | — | 214 / 215 | the same Api case, now **`found HttpStatusCode.OK {200}`** |

### What the three readings mean together

**MUT-B reproduces `V-26-A` exactly.** Deleting D-088's guard returns the endpoint to a `500` on the
Verifier's own PROBE-1 input, and exactly two tests catch it — one per suite, which is what D-088
claims and it is true. **The domain guard is live and covered.**

**MUT-C is what makes MUT-B legible.** With the constraint gone as well, the same request answers
**`200`**: a `Role.Subcontractor` holding a live credential is written to the users table. So the
database constraint is not decorative — it is the thing standing between a `500` and a silent
violation of spec.md §9. It does real work.

### `V-27-A` — **MEDIUM** · the constraint the retrospective named is still covered by nothing

**MUT-A is the finding.** Delete `ck_users_subcontractor_cannot_log_in` and **the entire suite stays
green — 97/97 and 215/215.** The one guard the retrospective picked out by name, as the exemplar of
the class, is still a guard nothing turns red.

It is not the same defect as `V-26-A` and it is not a regression: D-088 closed the *reachable* half by
putting the rule where a `Result` can carry it, and that half is now genuinely covered (MUT-B). What
survives is the backstop. With the domain guard in place nothing in the application can reach the
constraint any more, so no application-level test can observe it — **the fix made the constraint
unreachable, and an unreachable guard is an untested one.**

**And the one mechanism that should have caught this cannot, by construction.**
`DatabaseInitializer.FindMissingGuardsAsync` verifies check constraints against a required list it
derives *from the EF model itself*
[Verified: 2026-08-27 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` ->
`FindMissingGuardsAsync`] — `designTimeModel.GetEntityTypes().SelectMany(… GetCheckConstraints())`.
Deleting the constraint from the model therefore deletes it from the checker's expectations in the
same edit. The checker detects a **database that drifted from the model**; it cannot detect a **model
that lost a rule**. `guardsInstalled: []` and `A_dropped_check_constraint_is_reported_as_a_missing_guard`
[Verified: 2026-08-27 @ `tests/Api.Tests/SchemaInvariantTests.cs` ->
`A_dropped_check_constraint_is_reported_as_a_missing_guard`] both stay green through MUT-A, because
both ask the model what to expect.

That test drops `ck_postings_amount_positive` **from the database at run time** and restores it, which
is the correct shape and the reason it can fail at all. Nothing does the equivalent for the model.

**This is the retrospective's own pattern, one level up:** a passing check and an absent check produce
identical output, and the check that reports *"guards installed"* is derived from the same artefact a
regression would edit. Severity **MEDIUM** — no money moves in slice 1, nothing is currently
unenforced, and the rule is enforced twice today. It is recorded because the constraint file is
exactly what a later migration edits without noticing, and on that day the suite, the health endpoint
and the smoke check would all still be green.

**Owner: QA → Backend**, per `agents.md` §3b. Not fixed here — the Verifier reports.

