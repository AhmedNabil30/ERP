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

| Story | Reached | Verdict | Where |
|---|---|---|---|
| KAFF-109 change a user's role | **yes** | **ACCEPT, with `V-27-C` recorded against a rule it does not state** | §6 |
| KAFF-105a `GET /api/auth/me` | **yes** | **ACCEPT** | §8 |
| KAFF-102 sign-out | **yes** | **ACCEPT** | §9 |
| KAFF-101a sign-in | **yes** | **ACCEPT** | §10 |
| KAFF-103 change password | **yes** | **ACCEPT** | §11 |

**All five reached. All five accepted.** Every prior verdict was re-established at HEAD `559ac45`;
none was inherited. The three defects recorded below are new findings, and none of them rejects a
story — two are about the mechanisms that verify the code rather than the code, and the third is a
rule no story states.

### Findings index, so far

| Id | Severity | Against | What |
|---|---|---|---|
| `V-27-A` | **MEDIUM** | the guard mechanism, not a story | `ck_users_subcontractor_cannot_log_in` — and the check-constraint class generally — is covered by nothing, and `FindMissingGuardsAsync` cannot notice because its required list is derived from the same model a regression edits (§2, §5) |
| `V-27-B` | **MEDIUM** | D-089's mechanism | `LiveSession.Marker` can be claimed without being paid for: `Instance` is `internal` and every feature slice compiles into `Kaff.Api` (§3) |
| `V-27-C` | **MEDIUM** | KAFF-109 | `PUT /api/users/{id}/role` accepts and persists a `Role` value outside the enum — `role = '99'` — and both role-based security predicates are deny-lists that fail open on it (§6) |
| — | note | D-090 | The tests pin *before the grant match*, not *before the catalogue lookup* as the entry's prose says; the literal mutation leaves the suite green (§4) |
| — | note | `AllowList` | On `AllowAnonymous` sign-out no metadata assertion is possible, so one source grep is the whole mechanical cover (§3.1) |

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

### Closing gates — re-run after every mutation was reverted

Eleven mutations were applied and reverted during this pass. The tree was confirmed clean by
`git status` after each, and these are the gates at the end of it:

| Gate | Result |
|---|---|
| `git status` | clean — no file under `src/` or `tests/` differs from `559ac45` |
| `dotnet build KaffErp.sln -c Release --no-incremental` | **0 Warning(s), 0 Error(s)**, all six projects relinked |
| `Kaff.Domain.Tests.exe` | **97 / 97** |
| `Kaff.Api.Tests.exe` | **215 / 215** |
| `dotnet format --verify-no-changes` | exit **0** |
| `scripts/check-citations.ps1` | **913 checked, 0 broken, 0 legacy** — 883 plus this report's own 30, every one resolving |

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

---

## 3. D-089's central claim, tested by writing the endpoint it forbids

D-089's claim is that `RequireLiveSession()` makes the three checks apply **by construction** to every
route outside `RequirePermission`, because it *"stamps the route with `LiveSession.Marker` … and
nothing else adds that metadata"*, so `IsSelfOnlyListed` cannot be satisfied by a route that skips
them. **Tested by writing such an endpoint, not by reading the mechanism.**

A new slice was added under `src/Api/Features/Auth/VerifierProbe/` — discovered automatically by the
assembly scan [Verified: 2026-08-27 @ `src/Api/Common/Endpoints/IEndpoint.cs` -> `AddKaffEndpoints`],
so no shared registration file had to be edited, which is how a real one would arrive. Its handler
reads the caller's own row through `ICurrentUser.UserId` and applies **none** of the three checks. It
was then named in `SelfOnlyEndpoints` with a plausible reason, in the same shape as the two real
entries.

### `MUT-D` — the honest author. The claim holds, twice over.

```
failed  Every_mapped_endpoint_carries_a_permission_requirement
        found at least one item {"GET /api/auth/probe-exempt"}
failed  Every_self_only_member_is_mapped_and_requires_authentication_with_no_permission_of_its_own
        Expected …GetMetadata<LiveSession.Marker>() not to be <null>
                                                        Api 213 / 215
```

**Two independent refusals, not one.** Being named on the list did not exempt the route — it fell
through to the ungated check, which is D-067's own failure — *and* the marker assertion fired
separately. A new exempt endpoint cannot silently skip the three checks. **That half of D-089 is
real and is now confirmed by a session that did not write it.**

Worth recording: `No_feature_handler_reads_the_callers_identity_from_the_token_itself` did **not**
fire, because this handler used `ICurrentUser.UserId` rather than `KaffClaimTypes` — precisely the
ceiling Backend named for itself. **The ceiling is real and, here, did not matter**: the other two
assertions caught the route anyway. See §3.1.

### `V-27-B` — **MEDIUM** · the marker can be claimed without being paid for

**`MUT-E`: the same endpoint, with `.WithMetadata(LiveSession.Marker.Instance)` written in place of
`.RequireLiveSession()`. It compiles, and the suite reports 215 / 215.**

```
Api  total: 215   failed: 0   succeeded: 215
```

An endpoint that applies none of the three checks, reachable by any authenticated caller, acting on
the caller's row — and **every assertion in `EndpointPermissionCoverageTests` is satisfied.**

**Why it is reachable.** `Marker`'s constructor is private, so no caller can build one — but
`Instance` is `internal`, and **every feature slice compiles into `Kaff.Api`**
[Verified: 2026-08-27 @ `src/Api/Authorization/LiveSession.cs` -> `Marker`]. `internal` is the
assembly, and the assembly is where endpoints live. The guarantee is enforced by convention, not by
construction.

**The realistic path is not sabotage — it is the failing test's own advice.** An author who adds a
self-only route and sees `MUT-D`'s second failure reads a message telling them the route *"must
declare `RequireLiveSession()`, which is the only thing that adds this metadata"*. The sentence is
false, `Instance` is one dot away in the same assembly, and attaching it turns the red test green
while applying nothing. That is `decisions.md` D-046's green light in the one mechanism written to
prevent it.

**Not HIGH:** no shipped route does this, and it takes a deliberate act rather than the one-item-short
hand-copy that produced `V-26-B`. `V-26-B` is genuinely closed for the two real routes (§4, §5).
**But the claim written in three places — the `Marker` summary, `IsSelfOnlyListed`'s remarks, and
D-089 — is stronger than the code**, and the whole value of the mechanism is that a reader can trust
it without re-deriving it.

**Owner: QA → Backend.** `Instance` has exactly one reference in shipped source — `RequireLiveSession`
itself, in the same class [Verified: 2026-08-27 — searched every `.cs` under `src/`]. Nothing else in
`src/` names `Marker` at all. Not fixed here.

### 3.1 The named ceiling — judged

`No_feature_handler_reads_the_callers_identity_from_the_token_itself` scans `src/Api/Features/` for
`KaffClaimTypes` [Verified: 2026-08-27 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
`No_feature_handler_reads_the_callers_identity_from_the_token_itself`]. Backend named its ceiling
honestly: a handler could still query by `ICurrentUser.UserId`.

**Judgement: the ceiling does not matter for the authenticated half, and it is the whole of the cover
for the anonymous half.**

* **Authenticated routes** — `MUT-D` walked straight through this test using `ICurrentUser` and was
  caught twice by the metadata assertions instead. The scan is redundant there, which is the right
  kind of redundant.
* **`POST /api/auth/sign-out`** is the exposure. It is `AllowAnonymous` and can take no refusing
  filter (rule 7), so **no metadata assertion covers it** and this source scan is the only mechanical
  thing standing between it and a second `V-26-C`. A future edit that resolved the caller through
  `ICurrentUser.UserId` instead of `LiveSession.ResolveAsync` would pass every test in the suite and
  reintroduce the permanent-audit-row defect exactly.

That is a **named, open gap**, not a defect: nothing today has that shape, and the test catches the
shape all three original defects actually had. It is recorded so the next session does not read the
test's name as broader cover than it is. **The `AllowList` half of the exemption surface is covered by
one grep and a reviewer.**

---

## 4. `V-26-F`'s pin — and exactly which ordering it holds

D-090 claims that moving the two `MustChangePassword` blocks below the catalogue lookup turns
**exactly two** tests red. Re-executed, and the answer is yes — with one precision the entry does not
carry, found by getting the mutation wrong first.

### `MUT-F` — the leak, reproduced

Both blocks moved so that neither runs before the grant match: the block deleted from the first
overload entirely (it delegates), and the second overload's moved below `matching.Count == 0`.

```
Domain  failed  PermissionEvaluatorTests
        -> The_password_change_refusal_is_identical_for_a_caller_who_holds_the_permission_and_one_who_does_not
        found PermissionDecision.RoleNotGranted {4}, required PasswordChangeRequired {8}      96 / 97

Api     failed  ChangePasswordTests
        -> The_forced_change_refusal_is_the_same_for_a_caller_who_holds_the_permission_and_one_who_does_not
                                                                                            214 / 215
```

**Exactly two, one per suite, and nothing else in either.** The Domain failure is the leak stated
plainly: a Finance caller with the flag set, who holds no grant on `UserManage`, is told
`RoleNotGranted` where an Owner with the same flag is told `PasswordChangeRequired` — a per-endpoint
*"you would have been allowed"* oracle, arriving through a `messageKey` and changing no status code.
`V-26-F` is closed, at both levels, and the pin can fail.

### The precision D-090 does not carry, and a later session would trip on

**A first, weaker mutation left the entire suite green — 97 / 97 and 215 / 215.** In it, the first
overload's check was moved *below the catalogue lookup* but still *above the delegation*, and only the
second overload's went below the grant match.

D-090's own wording is *"moved below the catalogue lookup"*. Read literally, that mutation is what it
describes, and it does not go red. **The property the tests actually pin is narrower and is the
correct one: the refusal must precede the *grant match*, not the *catalogue lookup*.**

That distinction is not a defect and the tests are not weak — it is the right property. Consulting
`PermissionCatalogue.TryGet` discloses only whether a permission *exists*, which is static, public by
construction and unreachable from any route, since every endpoint declares a real permission. Nothing
leaks. **But a future session that reads D-090, performs the mutation its sentence describes, and sees
green will conclude the pin is broken when it is not.** Recorded here so that reading is available;
the fix is one word in D-090, and it is **BA/Backend bookkeeping, not a code change.**

### `SpecificRefusal` still cannot distinguish a gate refusal — D-086, re-established

`SpecificRefusal.Set` has one caller and fires on one decision
[Verified: 2026-08-27 @ `src/Api/Authorization/PermissionAuthorizationHandler.cs` ->
`HandleRequirementAsync`], read in one place
[Verified: 2026-08-27 @ `src/Api/Program.cs` -> `AddProblemDetails`]. **And the mechanism is
deliberately not wired into the route that could most easily have used it:** every refusal from
`RequireLiveSession` is the blanket `403` / `errors.auth.forbidden`, with neither
`AuthorizationErrors.RoleCannotLogIn` nor `SpecificRefusal` reachable from it
[Verified: 2026-08-27 @ `src/Api/Authorization/LiveSession.cs` -> `RequireLiveSession`]. **No route
leaks which of the three axes failed** — inactive, stale stamp and barred role are one response.

The `MUT-F` run is the evidence that this is held by a test rather than by care: the Api half of the
pin fails on the `messageKey` alone, which is the only channel by which the axis could escape.

---

## 5. The rest of the guards — the same treatment, and the two halves diverge

`V-27-A` raised the question for one constraint. The brief asks it of the others. Four more mutations,
one build each, all reverted.

| # | Removed | Result |
|---|---|---|
| **MUT-G4** | `trg_postings_append_only` from `001_guards.sql` | **182 / 215 failed.** The host refuses to boot: *"Refusing to start: database guards are missing — trg_postings_append_only."* |
| **MUT-G1** | `ck_postings_amount_positive` from the model | **1 red**, and **for the wrong reason** — see below |
| **MUT-G2** | `ck_audit_records_actor_is_named_completely` from the model | **1 red** — `AuditMechanismTests` -> `An_actor_is_named_completely_or_not_at_all` |
| **MUT-G3** | `ck_users_client_scope` from the model | **nothing red** |

### The triggers are covered twice. The check constraints are covered by nothing systematic.

**`MUT-G4` is the strongest result in this section.** Removing the append-only trigger — CLAUDE.md's
*"Never update or delete a posting"* — does not merely turn a test red; **the application refuses to
start**, and 182 tests fail because there is no host [Verified: 2026-08-27 @ `src/Api/Program.cs`, the
`missingGuards.Count > 0` block]. `requiredTriggers` and `requiredIndexes` in
`FindMissingGuardsAsync` are **hand-written name lists**, so a removed trigger is genuinely detected
rather than defined away. The file's own comment worries that a hand-maintained list is one somebody
forgets to extend — true, and it is nonetheless the half that works.

**The check-constraint half is the half that does not**, and `MUT-G1` shows why in miniature.
`ck_postings_amount_positive` is the money rule spec.md §6.1 states, and its one red test failed like
this:

```
Npgsql.PostgresException : 42704: constraint "ck_postings_amount_positive"
                           of relation "postings" does not exist
```

That is `A_dropped_check_constraint_is_reported_as_a_missing_guard` failing because **its own
`ALTER TABLE … DROP CONSTRAINT` could not find the constraint it hard-codes** — not because
`FindMissingGuardsAsync` reported anything. `before.Should().BeEmpty()` passed, exactly as `V-27-A`
predicts: the model no longer required it, so the checker no longer expected it. **The one test that
proves the guard checker sees constraints at all is itself the only reason a removed money constraint
turns red, and it does so by accident of naming.**

`MUT-G3` is `V-27-A` again on a different rule: `ck_users_client_scope` is spec.md §12's *"a portal
user is scoped to exactly one client; nobody else carries one"*, and deleting it is invisible to
215 Api tests and 97 Domain tests.

`MUT-G2` is the counter-example that keeps this honest — `W-1`'s constraint **is** covered, by a named
behavioural test that goes red on its own terms. So the gap is not universal; it is unsystematic,
which is worse to reason about.

### `V-27-A`, restated at its real scope

The finding is not about one constraint. **`DatabaseInitializer.FindMissingGuardsAsync` derives its
required check-constraint list from the same EF model a regression would edit**, so for the whole
class of check constraints the guard checker, `/api/health`'s `guardsInstalled`, and the `smoke`
command are all incapable of noticing a rule that was removed rather than lost. Coverage exists only
where somebody happened to write a behavioural test — one of the three sampled here, plus the audit
one, and not the two spec-stated rules.

**Severity stays MEDIUM for slice 1** — no postings endpoint exists, so no money is at risk today.
**It is the slice-3 risk that matters:** `ck_postings_amount_positive`, `ck_postings_distinct_accounts`
and `ck_postings_not_self_reversing` are money rules whose enforcement nothing would miss, on the day
somebody edits `TreasuryConfigurations.cs`. **Owner: QA → Backend, before slice 3.**

### What was not mutated, and the count

**Sampled 4 of 30 check constraints** declared across the five configuration files
[Verified: 2026-08-27 — counted from `HasCheckConstraint` call sites under
`src/Infrastructure/Persistence/Configurations/`], and **1 of 8 required triggers**. The remaining
**26 constraints and 7 triggers were not individually mutated** — see §9 for the full skipped count.
The four sampled were chosen to span identity, audit, treasury and the one the retrospective named;
the mechanism finding (`V-27-A`) is structural and applies to all 30 regardless of which were run.

---

## 6. KAFF-109 — change a user's role · **ACCEPT**

Rejected on 2026-08-26 for `V-26-A` (a reachable `500` with no `messageKey`) and `V-26-B`. Both are
closed. `V-26-A` is closed here; `V-26-B` is closed at the door and is judged in §7.

### `V-26-A` is closed, on evidence

**The guard is real and covered** — `MUT-B` (§2) deletes it and exactly two tests go red, one per
suite, the Api one reproducing the Verifier's own PROBE-1 as a `500`. **The refusal is the shape
D-080 requires** and D-088 claims: `409` with `errors.identity.subcontractor_cannot_log_in`, an
existing key carrying real Arabic and English, no new error and no new catalogue row
[Verified: 2026-08-27 @ `src/Domain/Identity/IdentityErrors.cs` -> `SubcontractorCannotLogIn`;
@ `src/Domain/Identity/User.cs` -> `ChangeRole`]. A refused change revokes nothing — asserted, with
both assignments still active [Verified: 2026-08-27 @ `tests/Api.Tests/ChangeUserRoleTests.cs` ->
`Converting_an_account_that_holds_a_credential_into_a_subcontractor_is_refused`].

**The open half stays open and this report does not close it.** Whether converting to
`Role.Subcontractor` should refuse or clear the credential is Nabil's, per D-088's 🟡. What is
verified is only that the built half is the reversible one and that the account holding **no**
credential still converts, so nobody reads the refusal as *"a user may never become a
subcontractor"* [@ `ChangeUserRoleTests.cs` -> `Converting_an_account_with_no_credential_into_a_subcontractor_succeeds`].

### `MUT-I` — no input produces a `500`

Every role name, three out-of-range integers and five malformed bodies, against a credentialed and a
credentialless target — **36 requests, no `500`**:

```
"Owner"…"MarketingSales"  -> 200          null, "", "NotARole", {}, []  -> 400 (no messageKey)
"Client"                  -> 400 errors.identity.client_user_requires_client
"Hr"                      -> 400 errors.identity.hr_role_requires_hr_department
"Subcontractor"           -> 409 errors.identity.subcontractor_cannot_log_in   (credentialed)
"subcontractor"           -> 409 errors.identity.subcontractor_cannot_log_in   (case-insensitive)
-1, 0, 99                 -> 200
```

The `400`s carrying no `messageKey` are **`W-5`**, already open with the Architect as a scope question
— framework-produced `400`/`404`/`415` are unfilled while `401` and `403` are. Confirmed still true,
not re-logged as new.

### `V-27-C` — **MEDIUM** · a role outside the enum is accepted and persisted

**`-1`, `0` and `99` each answer `200`.** Read back from the users table after the sweep:

```
STORED role column for the swept account = '99'
```

An account holds a role that exists in neither `Role`, spec.md §9's list of nine, nor
`PermissionCatalogue`. The enum is stored as text, so the column reads `'99'`. No check constraint
refuses it: `ck_users_client_scope` and `ck_users_operations_sub_department` are both satisfied by a
value that is neither `'Client'` nor `'Operations'`. There is **no `Validator.cs` in the
`ChangeUserRole` slice** [Verified: 2026-08-27 — the folder holds `Endpoint.cs`, `Handler.cs`,
`Request.cs`, `Response.cs` only], and `ChangeRole` re-applies the creation invariants without
asking whether the role is a role.

**Why MEDIUM and not HIGH.** No privilege is gained: `PermissionEvaluator` matches no grant, so such
an account is refused everything, and only the Owner can call this endpoint at all. Nothing in slice 1
moves money.

**Why it is not LOW, and this is the part worth reading.** Both role-based security predicates are
**deny-lists that fail open on an unknown value**:

* `MayHoldStaffSession` is `role is not (Role.Client or Role.Subcontractor)`
  [Verified: 2026-08-27 @ `src/Domain/Identity/Role.cs` -> `MayHoldStaffSession`] — `(Role)99`
  therefore **may hold a staff session**, and `GET /api/auth/me` will answer it.
* `PermissionEvaluator.Evaluate` bars `subject.Role == Role.Subcontractor`
  [Verified: 2026-08-27 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `Evaluate`] — same
  shape.

Neither is wrong for the nine real roles. Both answer *"permitted"* for every value outside them,
which is the wrong default for a predicate whose whole job is to refuse. And any audit row such an
account later authors carries `actor_role = '99'` **into an append-only, trigger-protected table
where it can never be corrected**.

**This is not a rule KAFF-109 states, and the story is not rejected for it.** It is a gap between the
enum and the wire that the story never had to think about. **Owner: QA → Backend**; the sibling
question — should an unknown role fail closed everywhere — is **Architect's**, and matters before
slice 3, when a role gates money.

### Verdict — **ACCEPT**

Both defects it was rejected for are closed, each confirmed by watching the guard fail rather than by
reading D-088. `V-27-C` is recorded against it but is a new finding about a rule the story does not
make, and it grants nothing to anybody. The story's own criteria hold.

---

## 7. The two remaining mutations, shared by the last four stories

Two more, each built clean and reverted, because the four stories below share one mechanism and
judging them separately would mean mutating it four times.

### `MUT-H` — the shared role bar, deleted in `Domain/`

`StaffSessionRules.MayHoldStaffSession` made to answer `true` for every role. **Five red, spanning
every door that uses it:**

```
SignInTests   -> The_staff_session_minter_refuses_a_client_and_a_subcontractor
SignInTests   -> Five_different_refusals_are_one_answer
MeTests       -> A_subcontractor_session_is_refused_not_answered_with_a_profile
MeTests       -> A_hand_minted_portal_client_session_is_refused_by_the_staff_door
SignOutTests  -> A_client_role_session_can_sign_out_too                          Api 210 / 215
```

The predicate moved to `Domain/` per CLAUDE.md and **every one of the three doors that consumes it has
its own failing test.** `V-26-B` is closed at the mechanism, not only at the route that reported it.

*One observation, not a defect:* the Domain suite stays **97 / 97** through this mutation. The rule
lives in `Domain/` and is covered entirely from the Api suite. Nothing is wrong — the doors are what
matter — but the cheapest possible test of the predicate itself does not exist.

### `MUT-J` — `IsActive` and the stamp comparison, deleted from `ResolveAsync`

**Four red, one per exempt route plus both halves on `/api/auth/me`:**

```
SignOutTests       -> A_cookie_the_global_kill_already_ended_writes_no_audit_row   (V-26-C's own test)
MeTests            -> A_deactivated_accounts_token_is_refused_not_answered_with_a_profile
MeTests            -> A_password_changed_on_another_device_ends_this_endpoints_answer_too
ChangePasswordTests-> A_deactivated_account_cannot_change_its_own_password         Api 211 / 215
```

**All three checks are live on all three exempt routes**, each observed failing. That is what D-089
claims and it is true.

---

## 8. KAFF-105a — `GET /api/auth/me` · **ACCEPT**

Rejected on 2026-08-26 for `V-26-B`. Closed: the endpoint carries `RequireLiveSession()`
[Verified: 2026-08-27 @ `src/Api/Features/Auth/WhoAmI/Endpoint.cs` -> `Map`], the handler reads the
row that filter already checked [Verified: 2026-08-27 @ `src/Api/Features/Auth/WhoAmI/Handler.cs` ->
`HandleAsync`], and `MUT-H` and `MUT-J` show all three checks failing on this route by name.

**`AC-105a-C` survives the fix**, which was the thing most at risk: the route still answers `200` with
`mustChangePassword: true` rather than refusing, because it carries no `RequirePermission` and
`LiveSession` deliberately does not consult the flag [@ `MeTests.cs` ->
`A_forced_password_change_is_announced_as_a_field_on_a_200_not_a_refusal`]. D-072 §2's dead-end loop
stays closed.

### `AC-105a-H` — judged. Honestly covered in substance; **no longer honestly stated**

The brief asks whether the criterion is honestly covered at the Domain level alone, after `V-26-B`'s
fix made `/api/auth/me` refuse `Role.Client` outright (SM-32). **Two separate answers, and they differ.**

**The substance is genuinely covered, and I did not take that on the author's word.** `MUT-K` reopened
D-035 — `Permission.PortalRead` flipped from `ProjectScoped` to `CompanyWide` in the catalogue — and
**two Domain tests went red**, `A_client_holds_no_company_wide_permission` among them. The test also
asserts both rows' scope explicitly before evaluating, so it cannot pass vacuously. And the coverage
is not indirect: `WhoAmI.Handler` builds its payload from
`PermissionEvaluator.CompanyWidePermissionsHeld(subject)` verbatim, which is the exact function the
Domain test exercises. **What the criterion protects — no project-scoped row leaking into a
company-wide payload — is proved, and the proof can fail.**

**The criterion's text is now false about the system.** `AC-105a-H` reads *"Given I am `Role.Client` /
When I call this endpoint / Then the permission set is empty"*
[Verified: 2026-08-27 @ `stories/slice-1-foundation/KAFF-105a-api-me-identity.md` -> the `AC-105a-H`
block]. A `Role.Client` calling this endpoint now receives `403` and **no permission set at all**. The
Given/When is unsatisfiable at this route, exactly as `V-26-E` found for `AC-102-F`'s sibling.

**The system is stricter than the criterion requires** — refusing the caller is a superset of
returning them an empty set — so nothing is unsafe. But a reader of KAFF-105a will believe a portal
client can call `/api/auth/me`, and that is SM-29's named failure mode: a story asserting a state the
code no longer has. **This does not reject the story**; the rule holds and is proved. It is
**BA bookkeeping**, already routed as `SM-32`, and this report confirms it is still owed.

### Verdict — **ACCEPT**

`V-26-B` closed at the mechanism and watched failing. `AC-105a-H` covered in substance at the level
where it is a fact about the rule. `V-26-G` / `TC-1-042` is a QA artefact defect, already routed, and
**not** a defect in this code — the brief is explicit that it fails against correct code.

---

## 9. KAFF-102 — sign-out · **ACCEPT**

Rejected on 2026-08-26 for `V-26-C`: a cookie the global kill had already ended was accepted and wrote
a permanent, uncorrectable audit row.

**Closed, and by construction rather than by a second check.** The handler asks `LiveSession` the same
question every other exempt route asks, **before** naming an actor
[Verified: 2026-08-27 @ `src/Api/Features/Auth/SignOut/Handler.cs` -> `HandleAsync`], and writes the
`SignedOut` row only inside `if (user is not null)`. `MUT-J` turns
`A_cookie_the_global_kill_already_ended_writes_no_audit_row` red, so the ordering is pinned and not
merely present.

**Rule 7 survives the fix, which is what a refusing filter would have broken.** Sign-out stays
`AllowAnonymous` and cannot take `RequireLiveSession()`, so it calls `ResolveAsync` directly; a caller
with no session still gets `204` and a cleared cookie, and writes nothing
[@ `SignOutTests.cs` -> `Signing_out_with_no_session_is_not_an_error`,
`Signing_out_with_no_session_writes_no_audit_record`]. `AC-102-B`'s deliberate trade is still asserted
the right way round [@ `SignOutTests.cs` -> `A_replayed_cookie_still_works_because_nothing_is_revoked`,
`Sign_out_never_rotates_the_security_stamp`].

**`V-26-E` confirmed, and its shape is exactly as the close records it.**
`A_client_role_session_can_sign_out_too` now asserts `204`, an empty body, a cleared cookie and
**`named.Should().BeFalse()`** — no `SignedOut` row [Verified: 2026-08-27 @
`tests/Api.Tests/SignOutTests.cs` -> `A_client_role_session_can_sign_out_too`]. `AC-102-F`'s own text
— a portal user can sign out — still passes; its **audit half is inverted**. That is Nabil's to accept
per D-089 §🟡 1, not this report's to wave through, and it is **not** a reason to reject: the endpoint
does the safer thing, and the change is recorded rather than silent.

**The 🟡 the handler raises is still open and still correctly open** — whether an unauthenticated
sign-out should leave any trace, and naming whom. D-085, Nabil's.

### Verdict — **ACCEPT**

---

## 10. KAFF-101a — sign-in · **ACCEPT**

**Prior `ACCEPTED` treated as carrying no weight.** `f807364` rewrote the role bar in
`SignIn/Handler.cs` and `StaffSessionMinter.cs` after that verdict.

**The change is a substitution, not a relocation.** `role is Role.Client or Role.Subcontractor` became
`!user.Role.MayHoldStaffSession()`, and the predicate is exactly that pair
[Verified: 2026-08-27 @ `src/Domain/Identity/Role.cs` -> `MayHoldStaffSession`]. **The statement has
not moved**: `PasswordHasher.Verify` still runs before anything else decides, the role bar still sits
after both password branches, and it is still folded into the generic `401`
[Verified: 2026-08-27 @ `src/Api/Features/Auth/SignIn/Handler.cs` -> `HandleAsync`]. The one answer
that is not the generic `401` — `423` for a locked account — is still reachable only by a caller who
has already proved they hold the password.

**Both orderings are pinned by tests that can fail.** `MUT-H` turns
`The_staff_session_minter_refuses_a_client_and_a_subcontractor` and `Five_different_refusals_are_one_answer`
red — the second being the disclosure test, which is the one that matters: five distinct refusal
reasons must be one answer.

**`TC-1-258` passes**, both halves: the status-code half [@ `SignInTests.cs` ->
`A_locked_account_answers_423_to_the_right_password_and_401_to_a_wrong_one`] and the time-envelope
half [@ `SignInTests.cs` -> `No_refusal_is_faster_than_the_hash_it_should_have_paid_for`]. **I did not
re-derive the timing test's robustness** — §4.3 of the 2026-08-26 report establishes it and that
analysis is unaffected by `f807364`, which did not touch the hash ordering. Recorded as inherited
rather than re-established.

**🟡 unchanged and still Nabil's:** an inactive account is folded into the generic `401` rather than
given `errors.auth.account_inactive`. D-084 §🟡 2.

### Verdict — **ACCEPT**

---

## 11. KAFF-103 — change your own password · **ACCEPT**

**Prior `ACCEPTED` treated as carrying no weight.** `f807364` and `4f9fc62` rewrote the endpoint and
handler onto `LiveSession` and added `V-26-F`'s pin after that verdict.

**The rewrite is sound and every check it delegates is live.** The handler takes the row
`RequireLiveSession` already checked, on the same scoped `DbContext` so the row is tracked and savable
[Verified: 2026-08-27 @ `src/Api/Features/Auth/ChangePassword/Handler.cs` -> `HandleAsync`], and
`MUT-J` turns `A_deactivated_account_cannot_change_its_own_password` red.

**`AC-103-B`'s carve-out survives**, which the rewrite could easily have broken: a caller who must
change their password still reaches this endpoint while every other refuses them
[@ `ChangePasswordTests.cs` -> `Until_the_password_is_changed_every_other_endpoint_refuses_it_and_this_one_does_not`].

**`V-26-F` is pinned** — §4. `MUT-F` turns exactly two red, one of them this story's
[@ `ChangePasswordTests.cs` -> `The_forced_change_refusal_is_the_same_for_a_caller_who_holds_the_permission_and_one_who_does_not`].

**`V-26-D` is closed and credited.** The handler's comment now states both reasons and names which one
was false, rather than the single wrong reason it carried
[Verified: 2026-08-27 @ `src/Api/Features/Auth/ChangePassword/Handler.cs` -> `HandleAsync`, the
`changed.IsFailure` block]. The close's §2.1 says D-089's *"Not done"* list never credited it; the
code does now say it, so the record and the code agree at last.

### `AC-103-H` is still uncovered at the Api, and has now moved level like `AC-105a-H`

`TC-1-027` is *Domain + Api*. The Domain half passes [@ `tests/Domain.Tests/UserTests.cs` — the
`SubcontractorCannotLogIn` assertion]. **The Api half still has no test** — no case in
`ChangePasswordTests` names `Role.Subcontractor` [Verified: 2026-08-27 — listed every `[Fact]` in the
file].

And the criterion has moved for the same reason `AC-105a-H` did: `RequireLiveSession` now refuses
`Role.Subcontractor` with `403` **before the handler runs**, so `SetOwnPassword`'s refusal is
unreachable from the wire entirely. An Api test for `AC-103-H` would now assert a `403` from the gate,
not the domain refusal the criterion describes. **Uncovered, P2, already routed** (close §2.3), and
this report adds that the replacement is not the same test the criterion originally implied.

### Verdict — **ACCEPT**

`TC-1-027`'s Api half is an uncovered case, not an open defect — the behaviour is right and is proved
one level down and one level up.

---

## 12. The mechanical prohibition sweep — re-run after three fix commits

Clean on 2026-08-26; `7ff500e`, `f807364` and `4f9fc62` have landed since. Run against the files.

| Prohibition | Result |
|---|---|
| No `float` / `double` near money | **Clean.** Four occurrences of either word under `src/` and `tests/`, **all four in prose** — two comments quoting CLAUDE.md's own rule, one about double-counting a correction, one about a test double. Zero as a type |
| No stored balance | **Clean.** `AccountBalance` is the keyless view; the only `Balance`-named properties are `RawBalance`, `SignedBalance` and `NormalBalance` on it and on `Account`, all excluded by name in `SchemaInvariantTests` -> `No_entity_stores_a_balance` |
| `HasPrecision(18, 4)` on every money property | **Clean, and enforced rather than asserted.** Precision is applied centrally through the value converters, and two model-level tests fail on a bare decimal [Verified: 2026-08-27 @ `tests/Api.Tests/SchemaInvariantTests.cs` -> `Every_money_property_is_decimal_18_4`, `No_decimal_column_is_left_at_the_provider_default`] |
| No endpoint updates or deletes a posting | **Clean.** No posting endpoint exists; the shipped table is health, setup ×2, auth ×4, users ×5, assignments ×2. The absence is asserted from the host's own route table [@ `EndpointPermissionCoverageTests.cs` -> `No_endpoint_deletes_a_project_assignment`], and `MUT-G4` shows the database-level append-only trigger is load-bearing enough that its absence stops the application booting |
| No typed credential stored | **Clean.** `PasswordHash` and `SecurityStamp` remain the only credential-shaped columns, both `[AuditRedacted]`. The three rewritten handlers introduced no new path: `ChangePassword` passes the plaintext to `PasswordHasher` and nothing else, and `No_audit_record_the_door_writes_contains_the_credential` still passes |
| Every endpoint checks role **and** assignment | **Clean for the gated set, and the exemption surface is now mechanically bounded** — §3. Two exemption lists, five and two members, each entry reasoned, and `MUT-D` proves a new unpaid exemption is refused. **Qualified by `V-27-B`** |
| Nobody creates and approves the same movement | Not reachable in slice 1 — no movement exists |
| Every state change writes an audit record | **Improved since 2026-08-26.** The one place that wrote a row the rest of the system would refuse (`V-26-C`) no longer does, and `MUT-J` pins it |
| No hardcoded user-facing strings | **Clean at the API for every refusal the application writes.** `W-5` stands unchanged — framework-produced `400`s carry no `messageKey`, re-observed in `MUT-I`'s sweep. Already the Architect's scope question |

**Nothing regressed across the three fix commits.**

---

## 13. QA cases — what ran, and what it produced

**49 cases** map to these five stories, from the story coverage index
[Verified: 2026-08-27 @ `qa/slice-1/test-cases.md` -> the *Story coverage index* table]:
KAFF-101a `TC-1-007…016, 018, 220…230, 258` (23) · KAFF-102 `TC-1-019…022, 232` (5) ·
KAFF-103 `TC-1-023…029` (7) · KAFF-105a `TC-1-042, 045, 046, 235, 236` (5) ·
KAFF-109 `TC-1-075…080, 237…239` (9).

All are `Api`, `Domain + Api` or `Api + E2E`, so the vehicle is the two suites, both green.

| Outcome | Count | Which |
|---|---|---|
| **Pass** — covered by a named test in a green suite | **45** | The bulk of all five sets |
| **Fails against correct code** | **1** | **`TC-1-042`** — cites the retired `AC-105a-F` and asserts the withdrawn rule. `V-26-B`'s fix makes it *more* wrong: the route now refuses the caller the case describes. **Routed to QA, not a defect in the code** |
| **Cannot be executed** | **1** | **`TC-1-079`** — `PENDING Q27 (residual)`. Blocked on a ruling, and the close notes the register says Q27 is closed, so the marker itself is BA's |
| **No test exists** | **2** | **`TC-1-027`'s Api half** (`AC-103-H` — §11, and it has moved level) and **`TC-1-046`** (the `/api/auth/me` payload carries no money — satisfied structurally, by inspection, not by an assertion) |

**`TC-1-258` passes**, both halves — §10.

---

## 14. What this session did **not** do — as a count, not as prose

The retrospective's change 2: *"a checker that says N checked must also say M unparsed."* Applied to
this pass.

| Skipped | Count | Why, and what it would cost to close |
|---|---|---|
| **Check constraints not individually mutated** | **26 of 30** | 4 were sampled (§2, §5). `V-27-A` is a **structural** finding about `FindMissingGuardsAsync` and applies to all 30 regardless — but *which* of the other 26 have a behavioural test is unmeasured, and on today's evidence the answer is "some do, some do not" |
| **Required triggers not individually mutated** | **7 of 8** | `MUT-G4` covered one and showed the class is detected by a hand-written list plus a start-up refusal. The other seven are assumed covered by the same mechanism, **not observed** |
| **SQL guard branches not individually mutated** | **16 of 17** `RAISE EXCEPTION` sites | `TreasuryGuardTests` exercises ten of them behaviourally; I confirmed the file's names, not each branch failing |
| **Domain guards not individually mutated** | **~99 of 103** `Result.Failure` sites under `Domain/` | Four were mutated — `ChangeRole`'s subcontractor bar, `MayHoldStaffSession`, `PermissionEvaluator`'s ordering, `PortalRead`'s scope. **The retrospective's change 1, applied in full, is a bigger job than one verification pass** and this pass did not do it |
| **E2E suite** | **not run** | `TC-1-223`'s browser half (`SameSite=Strict` as the whole CSRF control) is unexercised, exactly as on 2026-08-26. Asserting a browser's cookie policy from a `TestServer` proves nothing about a browser |
| **`/run-kaff-erp smoke`** | **not run this session** | Every result in this report comes from the test host, and **nothing here is claimed about a running stack.** The 2026-08-26 pass ran it green at `e43e9ac`; three commits have landed since and I did not re-run it |
| **Staging** | **not connected to** | Unchanged from 2026-08-26 and from the close: the pipeline still cannot see it |
| **The D-084 timing analysis** | **inherited, not re-derived** | §4.3 of the 2026-08-26 report. `f807364` did not touch the hash ordering, so the analysis carries — recorded as inherited rather than silently re-asserted |
| **`V-27-B`'s exploitability as a live request** | **not executed** | I proved the *coverage mechanism* is satisfied by an unpaid route (215/215 green). I did **not** separately drive a `Role.Subcontractor` request at that probe route to watch it answered. The finding is about the mechanism and is demonstrated; the endpoint's behaviour follows from a handler that contains no check, which is visible in the file |

**Nine skipped items.** None of them is a silent gap in a verdict below: where a skip touches a story,
it is named in that story's section too.

---

## 15. The one thing Nabil should know

**The three fixes are real. Every one of them was watched failing, and none of them is where the risk
now is.**

`V-26-A`, `V-26-B`, `V-26-C` and `V-26-F` are closed, and closed at the mechanism rather than at the
route that reported them — the role bar moved into `Domain/` and every door that uses it has a test
that goes red when it is deleted; the exempt-route checks are one filter and a new endpoint that skips
them is refused twice over. **Five stories, 19 points, all five accepted.**

**What this pass found instead is that two of the mechanisms we now rely on to tell us the code is
safe cannot tell us that.**

* **`V-27-B`** — the test that enforces *"an exempt route must pay for its exemption"* prints a message
  saying `RequireLiveSession()` *"is the only thing that adds this metadata."* **That sentence is
  false.** The marker is one dot away in the same assembly, and attaching it turns the failing test
  green while applying none of the three checks. The suite reported **215 / 215** against exactly such
  an endpoint.
* **`V-27-A`** — the constraint the retrospective picked out **by name**, as the exemplar of the whole
  problem, is *still* covered by nothing. Delete it and 312 tests stay green. And the health check
  that reports `guardsInstalled` cannot notice, because it asks the same file a regression would edit.

**This is the retrospective's own pattern, one level up.** It said: *a passing check and an absent
check produced identical output.* Sprint 1 fixed the seven instances. **Both findings above are that
same shape living inside the machinery built to prevent it** — a green light whose greenness is not
evidence.

**Nothing is unsafe today.** No shipped route claims an unpaid exemption, both subcontractor rules are
enforced twice over, and no money exists yet. **The date that matters is slice 3.** That is when
`ck_postings_amount_positive` and the netting rules start standing between the ledger and a wrong
number — and today, on the evidence of `MUT-A` and `MUT-G1`, **removing one of those constraints is
something this project would not notice.**

