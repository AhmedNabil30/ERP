# Verification — slice 1: KAFF-111, 114, 112, 109, 100, 101a, 102, 103, 105a · 2026-08-26

**Verifier Agent, fresh session.** Read `CLAUDE.md`, `agents.md` §7 and §3c, `process/agile.md`
(SM-29 / SM-30 / SM-31), `spec.md` §9, `qa/slice-1/`, and the previous report
`qa/slice-1/verification-2026-08-25.md` (which covered 106 / 108 / 110 / 113 only). Every claim below
was re-established against the files **as they stand today at `e43e9ac`**, never inherited from a
`decisions.md` entry's description of itself.

**The Verifier reports. It does not fix.** Nothing under `src/`, `tests/`, `stories/`, `decisions.md`
or the rest of `qa/` was changed. This file is the only artefact created.

Citations are `[Verified: 2026-08-26 @ `File` -> `Identifier`]` per SM-31 — identifier, never a line
number.

**Three verdicts throughout, never two:** *satisfied* · *defect* · *not verifiable in this session,
with the reason*. A story that could not be verified is **not accepted**.

---

## 0. Progress of this report

This file was written incrementally and committed as each section closed. **Every story in the brief's
table was reached.**


| Story | Reached | Verdict |
|---|---|---|
| KAFF-109 change a user's role | **yes** | **REJECT** — two defects, `V-26-A` and `V-26-B` |
| KAFF-105a `GET /api/auth/me` | **yes** | **REJECT** — `V-26-B`, plus `V-26-G` |
| KAFF-102 sign-out | **yes** | **REJECT** — `V-26-C` |
| KAFF-103 change password | **yes** | **accept** — `V-26-D` (wrong comment, correct behaviour) |
| KAFF-111 revoke on deactivation | **yes** | **accept** |
| KAFF-114 revoke assignment | **yes** | **accept**, one uncovered case |
| KAFF-112 reactivate a user | **yes** | **accept**, one uncovered case |
| KAFF-100 bootstrap the Owner | **yes** | **accept** |
| KAFF-101a sign-in | **yes** | **accept** — `V-26-E` (observation) |

### Findings index

| Id | Severity | Story | What |
|---|---|---|---|
| `V-26-A` | **HIGH** | KAFF-109 | `PUT /api/users/{id}/role` returns a bare `500` with no `messageKey` on a reachable input |
| `V-26-B` | **HIGH** | KAFF-105a, KAFF-109 | `GET /api/auth/me` answers `200` to `Role.Subcontractor` and `Role.Client` |
| `V-26-C` | **MEDIUM** | KAFF-102 | A cookie the global kill already ended still writes a permanent audit row through sign-out |
| `V-26-F` | **MEDIUM** | KAFF-103 / D-086 | The statement ordering that keeps `SpecificRefusal` from leaking the axis is pinned by no test |
| `V-26-G` | **MEDIUM** | QA artefact | `TC-1-042` was never relocked; `AC-105a-H` has no live QA case |
| `V-26-D` | LOW | KAFF-103 | `AC-103-H` is unreachable for a different reason than the handler records |
| `V-26-E` | observation | KAFF-102 | `AC-102-F` is proved against a state the system forbids |

Also recorded, not numbered: **D-082 §4's premise is false** (§4.1) — the conclusion it supports holds,
but it is cited in three stories and a later handler could rely on it.

---

## 1. Baseline — the gate before any test result is trusted

`Kaff.Api`, `Kaff.Api.Tests` and `Kaff.Domain.Tests` were killed before the build, so no suite below
ran against a stale binary. This is the D-069 §6 / SKILL.md trap and it is closed by inspection of
the build log, not by the exit code alone.

| Gate | Result |
|---|---|
| `docker start kaff-db` | container up |
| `dotnet build KaffErp.sln -c Release` | **Build succeeded, 0 Warning(s), 0 Error(s), exit 0** |
| `MSB3021` / `MSB3026` / `MSB3027` in the log | **none** — every project re-linked, including `Kaff.Api.dll` into the test output |
| `Kaff.Domain.Tests.exe` | **94 / 94**, 0 failed |
| `Kaff.Api.Tests.exe` | **209 / 209**, 0 failed |
| `scripts/check-citations.ps1` | **698 checked, 0 broken, 0 legacy** — the figure the brief names, unregressed |

Every project produced a fresh output line in the build log (`Kaff.Domain`, `Kaff.Infrastructure`,
`Kaff.Api`, `Kaff.Api.Tests`, `Kaff.Domain.Tests`, `Kaff.E2E.Tests`), which is what distinguishes a
real build from the MSB3026 "succeeded and copied nothing" case.

---

## 2. The mechanical prohibition sweep

Run against the files, not against a test's assertion about them.

| Prohibition | Result |
|---|---|
| No `float` / `double` anywhere near money | **Clean.** Zero occurrences of either keyword in any `.cs` under `src/` or `tests/`, money-related or not |
| No stored balance column | **Clean.** `AccountBalance` is keyless and read-only, backed by the `account_balances` view [Verified: 2026-08-26 @ `src/Domain/Treasury/AccountBalance.cs` -> `AccountBalance`]. No entity carries a `Balance` property; every other hit on the word is prose |
| Every money property `HasPrecision(18, 4)` | **Clean at the level slice 1 reaches.** `Money` is a single value object over `decimal` with its own `Scale`/range guard [Verified: 2026-08-26 @ `src/Domain/Common/Money.cs` -> `MaxMagnitude`], converted centrally [Verified: 2026-08-26 @ `src/Infrastructure/Persistence/Converters/ValueConverters.cs`]; the model snapshot carries `HasPrecision(18, 4)` on every money column and `(18, 6)` on percentage columns. The one bare `decimal?` on an entity is `Project.AreaSquareMetres`, which is an area and not money [Verified: 2026-08-26 @ `src/Domain/Projects/Project.cs` -> `AreaSquareMetres`] |
| No endpoint updates or deletes a posting | **Clean.** No posting endpoint exists at all in slice 1; the shipped route table is health, setup ×2, auth ×4, users ×5, assignments ×2. Asserted mechanically for assignments [Verified: 2026-08-26 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` -> `No_endpoint_deletes_a_project_assignment`] |
| No typed credential is ever stored (D-062 §3) | **Clean.** `PasswordHash` and `SecurityStamp` are the only credential-shaped columns and both carry `[AuditRedacted]` [Verified: 2026-08-26 @ `src/Domain/Identity/User.cs` -> `PasswordHash`, `SecurityStamp`]. The submitted plaintext is a request-record field that reaches `PasswordHasher` and nothing else [Verified: 2026-08-26 @ `src/Api/Features/Auth/SignIn/Request.cs` -> `Request`]. No `SetReason`, no logger call and no audit path anywhere receives it — checked by grep over `src/`, not by reading `SignInTests` |
| Every endpoint checks role **and** assignment | **Clean for the gated set**, and the exemption categories are enumerated and asserted [Verified: 2026-08-26 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` -> `Every_mapped_endpoint_carries_a_permission_requirement`]. **But see §3 — the exemption categories have a hole the test cannot see** |
| Nobody creates and approves the same movement | Not reachable in slice 1 — no movement exists yet |
| Every state change writes an audit record | Satisfied by the interceptor for entity changes; **one place writes an audit record on a request the rest of the system refuses** — see `V-26-C` |
| No hardcoded user-facing strings | Clean at the API — every refusal carries a `messageKey`, including the ones the API does not write itself [Verified: 2026-08-26 @ `src/Api/Program.cs` -> `AddProblemDetails`]. **Except a 500 — see `V-26-A`** |

---

## 3. The defects

### `V-26-A` — **HIGH** · KAFF-109 · `PUT /api/users/{id}/role` returns a bare `500` on a reachable input

**What happens.** `User.ChangeRole` re-applies `ValidateDepartment` and the client-id rules and
nothing else [Verified: 2026-08-26 @ `src/Domain/Identity/User.cs` -> `ChangeRole`]. It does **not**
refuse `Role.Subcontractor`, and it does not clear the account's credential. A staff account with no
department — which is every `Role.Owner`, including the one KAFF-100's setup screen mints — therefore
passes every check in `ChangeRole` on a request naming `Role.Subcontractor`.

`SaveChangesAsync` then violates the check constraint
`ck_users_subcontractor_cannot_log_in` — `role <> 'Subcontractor' OR password_hash IS NULL`
[Verified: 2026-08-26 @ `src/Infrastructure/Persistence/Configurations/IdentityConfigurations.cs` ->
`ck_users_subcontractor_cannot_log_in`] — and the resulting `DbUpdateException` is unhandled.

**Executed, not reasoned.** A probe seeded a `Role.Owner` with a password hash and posted
`{"role":"Subcontractor"}` as the Owner:

```
PROBE-1 status=500 body={"type":"…rfc9110#section-15.6.1","title":"An error occurred while
processing your request.","status":500,"traceId":"…"}
```

**Why it is HIGH and not cosmetic.** Three separate project rules break at once:

1. `CLAUDE.md` — *"Domain errors are `Result<T>`, not exceptions."* This is a domain rule
   (spec.md §9, *"Subcontractor — record only, no login"*) enforced only at the database, and the
   database's refusal is not translated back into a `Result`.
2. `CLAUDE.md` — *"No hardcoded user-facing strings."* The `500` body carries **no `code` and no
   `messageKey`**, so the Arabic RTL shell has nothing to render. This is finding **V-A** of
   `qa/slice-1/verification-2026-08-23.md` reappearing through a door the `AddProblemDetails`
   customisation does not cover, because a `500` maps to neither `401` nor `403`.
3. The Owner is the only role that can call this endpoint, and the Owner is the account most likely
   to have no department. The reachable case is not an edge — it is the default shape of the first
   account the system ever creates.

**Not covered by any test.** `ChangeUserRoleTests` seeds every user through `User.Create` alone, so
no seeded user holds a `PasswordHash` and the constraint is satisfied vacuously in the whole suite
[Verified: 2026-08-26 @ `tests/Api.Tests/ChangeUserRoleTests.cs` -> `MakeUser`]. `Role.Subcontractor`
appears nowhere in that file. The suite is green and the endpoint 500s.

---

### `V-26-B` — **HIGH** · KAFF-105a and KAFF-109 · `GET /api/auth/me` answers `200` to the two roles that may never hold a staff session

**The gap is in the exemption category, exactly as suspected — and it is a third check, not the two
that were copied.** D-087 re-applied `IsActive` and the security-stamp comparison by hand on the
`SelfOnlyEndpoints` routes, because no gate runs there
[Verified: 2026-08-26 @ `src/Api/Features/Auth/WhoAmI/Handler.cs` -> `HandleAsync`]. What
`PermissionSubjectReader` + `PermissionEvaluator` do on a gated route is **three** things, not two:
`IsActive`, the stamp [Verified: 2026-08-26 @
`src/Infrastructure/Authorization/PermissionSubjectReader.cs` -> `ReadAsync`], **and the role bar** —
`Role.Subcontractor` is refused `RoleCannotLogIn` before the catalogue is consulted
[Verified: 2026-08-26 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `Evaluate`]. The
third one was not copied. `StaffSessionMinter.Issue` bars `Role.Client` and `Role.Subcontractor` by
construction [Verified: 2026-08-26 @ `src/Api/Identity/StaffSessionMinter.cs` -> `Issue`] — and that
bar, too, does not exist on this route.

**Executed:**

```
PROBE-4 (Role.Subcontractor)  me=200 {"role":"Subcontractor", …, "permissions":[]}
PROBE-5 (Role.Client)         me=200 {"role":"Client",        …, "permissions":[]}
```

**And it is reachable in production, through KAFF-109.** `ChangeRole` does not rotate
`SecurityStamp` — deliberately, and the code says so
[Verified: 2026-08-26 @ `src/Api/Features/Auth/WhoAmI/Handler.cs` -> `HandleAsync`, remarks] — so a
departmentless staff account converted to `Role.Subcontractor` keeps its live session and then reads
its own profile:

```
PROBE-2 change=200 {"role":"Subcontractor","revokedProjectIds":[]} | roleAfter=Subcontractor
        | me=200 {"role":"Subcontractor", …, "permissions":[]}
```

That is a `Role.Subcontractor` holding a working staff session and being answered by the one endpoint
D-050 says the frontend trusts to say who it is talking to. spec.md §9 is unambiguous: *"record only,
no login."*

**This is the answer to the `AC-105a-H` question.** `AC-105a-H` and `AC-102-F` are proved with
hand-minted `Role.Client` tokens because `StaffSessionMinter.Issue` refuses that role. The tests are
honest about what they assert — an empty company-wide set — and they pass. But **nobody asked whether
the endpoint should answer such a caller at all**, and the answer nobody asked for is `200`. The
hand-minted token was treated as a stand-in for an unreachable state; it is in fact a faithful
reproduction of a state KAFF-109 can produce for the sibling role. The criterion is covered; the
endpoint is not safe.

---

### `V-26-C` — **MEDIUM** · KAFF-102 · a replayed cookie can write append-only audit rows for a user the system has already killed

**`AC-102-B`'s known consequence is not the whole consequence.** D-051 N5 accepts that a captured
cookie stays usable until `JwtOptions.InactivityMinutes` expires, because there is no session table.
That trade is deliberate and this report does not reopen it. What follows from it and was **not**
priced:

`POST /api/auth/sign-out` is `AllowAnonymous` (rule 7) and re-checks neither `IsActive` nor the
security stamp [Verified: 2026-08-26 @ `src/Api/Features/Auth/SignOut/Handler.cs` -> `HandleAsync`].
It looks the user up by the token's id claim alone and, if the row exists, writes an
`AuditEventKind.SignedOut` record attributed to them.

**Executed.** A user deactivated *after* their token was minted — whose stamp rotation
`User.Deactivate` performs is the global kill of D-053, and whose token every other route in the
system now refuses:

```
PROBE-3  me=403 errors.auth.forbidden
         changePassword=403 errors.auth.forbidden
         signOut=204   ← accepted
         SignedOut audit rows written by that dead token = 1
```

So the two `SelfOnlyEndpoints` refuse the stale token correctly and sign-out does not. The audit
table is append-only and trigger-protected: **a row written here can never be corrected or removed**,
by anyone, ever. A holder of a captured cookie for a deactivated account can write an unbounded
number of them, each naming that person as having signed out at a time they did not.

**Severity is MEDIUM rather than HIGH** because nothing is granted and no money moves — the harm is
to the integrity of a record that CLAUDE.md makes permanent. The handler's own remarks already flag
the *adjacent* question (should an unauthenticated sign-out write a row at all?) as 🟡 for Nabil; it
does not raise this one, which is the harder half: should a sign-out write a row on the authority of
a token every gated route refuses.

---

### `V-26-D` — **LOW, observation** · KAFF-103 · `AC-103-H` is unreachable for a different reason than the handler states

`ChangePassword.Handler` handles a `SetOwnPassword` failure and comments that it is *"today only
`Role.Subcontractor` … unreachable through this door in practice, since `StorePasswordHash` already
refuses that role a credential, so no subcontractor can ever hold the session this endpoint requires"*
[Verified: 2026-08-26 @ `src/Api/Features/Auth/ChangePassword/Handler.cs` -> `HandleAsync`].

Half of that is now wrong: per `V-26-B`, a subcontractor **can** hold the session. What actually keeps
`AC-103-H` unreachable is one line earlier — `PasswordHasher.Verify` runs against a null
`PasswordHash` and fails, so the handler returns `auth.current_password_incorrect` before
`SetOwnPassword` is ever called:

```
PROBE-4  changePassword=400 errors.auth.current_password_incorrect
```

The endpoint's behaviour is correct. The reasoning recorded beside it is not, and it is the kind of
reasoning a later session would rely on. Recorded so it is corrected rather than inherited.

---

### `V-26-E` — **observation, not a defect** · `AC-102-F` is covered against a state the system forbids

`TC-1-021` reads *"Given a signed-in `Role.Client`, when they sign out…"*. No `Role.Client` can be
signed in: `StaffSessionMinter.Issue` throws for that role, sign-in refuses before minting, and
`User.ChangeRole` cannot convert a staff account into one — a staff account has `ClientId == null`,
so `role == Role.Client && ClientId is null` refuses it, and the inverse rule refuses the return trip
[Verified: 2026-08-26 @ `src/Domain/Identity/User.cs` -> `ChangeRole`]. The given is unsatisfiable.

The test proves the handler is safe if such a token ever existed, which is worth having as
defence in depth. It is recorded here so nobody reads `AC-102-F` as evidence that a live portal
sign-out path has been exercised. **`Role.Subcontractor` is the sibling case and is *not*
unreachable** — that is `V-26-B`.

---


D-084 timing tests, and what could not be verified — follow in the next increment of this file.)*

---

## 4. The claims in the brief, tested rather than read

### 4.1 `AC-109-K` — D-082 §4's conclusion is right and its **reasoning is wrong**

D-082 §4 argues that a mid-batch revocation failure is *structurally unreachable*, so a fault-injection
test would pass regardless and is not worth writing. KAFF-110 / KAFF-111 rest on the same argument
[Verified: 2026-08-26 @ `src/Api/Features/Users/DeactivateUser/Handler.cs` -> `HandleAsync`, remarks].

**The argument is false, and `V-26-A` is the counter-example.** A real fault exists at exactly the
point the argument says nothing can fail: the role change and every revocation are in the change
tracker together when `ck_users_subcontractor_cannot_log_in` rejects the save.

**So the fault was injected, using the real defect rather than a mock.** Target: a departmentless
`Role.Owner` holding a credential and **two active assignments**; request: `{"role":"Subcontractor"}`.

```
PROBE-6 status=500 roleBefore=Owner roleAfter=Owner assignments=2 stillActive=2  => ATOMIC=YES
```

**`AC-109-K` is satisfied, and now on evidence rather than on an argument.** The role did not move and
neither assignment was revoked. EF Core's implicit transaction around `SaveChangesAsync` is doing the
work, exactly as the handler claims.

What must be corrected is D-082 §4's premise, because it is load-bearing in three places (KAFF-109,
KAFF-110, KAFF-111) and it is wrong: **the batch can fail mid-flight.** It happens to be atomic
anyway. A future handler that opens its own transaction, or saves twice, would break the invariant
with no test to catch it — and the recorded reason for having no test would still read "unreachable".

### 4.2 `SpecificRefusal` cannot distinguish a gate refusal — verified, and the guarantee is the **ordering**

D-086 built the mechanism D-080 priced and declined hours earlier. It is safe, but not for the reason
its own remarks give — those argue from *what is disclosed*; the actual guarantee is *where the check
sits*.

`SpecificRefusal.Set` has exactly one caller and fires on exactly one decision
[Verified: 2026-08-26 @ `src/Api/Authorization/PermissionAuthorizationHandler.cs` ->
`HandleRequirementAsync`; the only other references in `src/` are the class itself and the reader @
`src/Api/Program.cs` -> `AddProblemDetails`]. `PermissionDecision.PasswordChangeRequired` is returned
**before `PermissionCatalogue` is consulted at all** — after the null-subject and subcontractor checks,
and before the grant match [Verified: 2026-08-26 @ `src/Domain/Authorization/PermissionEvaluator.cs` ->
`Evaluate`].

That ordering is what makes the key safe: a caller receiving `errors.auth.password_change_required`
learns nothing about whether they hold the permission, because the evaluator never looked. Reverse
the two statements and the same key becomes a "you would have been allowed" oracle on every endpoint
in the system. **The safety is one statement's position, and nothing in the suite pins it there** —
the same shape as `AC-101a-P`, which `TC-1-258` exists to pin. Recorded as `V-26-F`.

Every other refusal path is the blanket pair. No route emits a role-versus-assignment distinction: the
only `AuthorizationErrors` any endpoint returns are `Forbidden`, `NotAuthenticated`,
`InvalidCredentials`, `AccountLocked`, `CurrentPasswordIncorrect` and `PasswordTooShort`, none of which
names an axis [Verified: 2026-08-26 — grep over every call site in `src/`]. **D-080 holds.**

### 4.3 The D-084 timing tests are **robust, not flaky-by-construction**

`No_refusal_is_faster_than_the_hash_it_should_have_paid_for` [Verified: 2026-08-26 @
`tests/Api.Tests/SignInTests.cs` -> `No_refusal_is_faster_than_the_hash_it_should_have_paid_for`] is
built the way a timing test has to be to survive a loaded runner:

* **The statistic is the minimum of three attempts, not the mean.** Load and scheduler noise only ever
  *add* time. The assertion is `elapsed > baseline / 2`, so every source of noise pushes the
  measurement in the **passing** direction. A busy CI runner cannot make this go red.
* **The baseline is measured on the same runner inside the same test** — a known username with a wrong
  password — so it self-calibrates to whatever hardware it lands on rather than comparing to a constant.
* **The threshold is 2x against a real margin of roughly 75x.** The authors' own broken run measured
  61,475 ticks against a 4,653,877 baseline — 1.3%. No runner closes a gap that wide with noise.
* It warms the pipeline first, so JIT and first-query-plan costs do not land on the baseline.

Failing this test requires a *later* measurement to be more than twice as **fast** as an earlier one
doing identical work — the opposite of what load causes. **It will not be deleted by whoever it
blocks, because it will not block anybody.**

The pure-function sibling [Verified: 2026-08-26 @ `tests/Api.Tests/PasswordHasherTests.cs` ->
`Verifying_against_no_stored_hash_costs_what_verifying_against_one_costs`] measures the same property
with no HTTP in the way, which is the right belt-and-braces shape.

### 4.4 The exemption categories are narrow enough — **watched red, not read**

The brief says three categories; the test carries **two** exemption lists beside the ordinary gated
set: `AllowList` (5 members, anonymous) and `SelfOnlyEndpoints` (2 members, authenticated with no
permission) [Verified: 2026-08-26 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
`AllowList`, `SelfOnlyEndpoints`].

Three mutations were applied to `src/`, the solution rebuilt clean, the full Api suite run, and the
mutations reverted (`git checkout -- src/`; the suite is back at **209 / 209** and the tree is clean).

| Mutation | Test that went red | Verdict |
|---|---|---|
| Delete `.RequirePermission(Permission.UserManage)` from KAFF-109's endpoint | `EndpointPermissionCoverageTests` -> `Every_mapped_endpoint_carries_a_permission_requirement` | **A future ungated endpoint fails the test.** D-067 cannot recur silently |
| Delete `AllowAnonymous()` from KAFF-102's endpoint | `EndpointPermissionCoverageTests` -> `Every_allow_list_member_is_mapped_and_says_so_in_its_own_file`, plus both `SignOutTests` rule-7 cases | The allow-list cannot drift from the file it describes |
| Collapse the three-way freshness condition to `user is null` in KAFF-105a's handler | `MeTests` -> `A_deactivated_accounts_token_is_refused_not_answered_with_a_profile` **and** -> `A_password_changed_on_another_device_ends_this_endpoints_answer_too` | **Both halves of D-087's hand-copied check are genuinely covered.** Neither is a test that cannot fail |

**A fourth result fell out of the first mutation and is worth recording.** Removing the
`RequirePermission` did not merely open the endpoint — it made seven `ChangeUserRoleTests` cases fail
with a `DbUpdateException`, because with no gate running `IAuditContext` was never told who the
verified actor is and `ck_audit_records_actor_is_named_completely` refuses the save. **An ungated
endpoint that changes state cannot write to this database at all.** That is D-075 working as a second,
independent barrier behind D-067's, and it was not designed as one.

### 4.5 `AC-102-B` — the replayed cookie is **not** the whole consequence

See `V-26-C`. The accepted trade is that a captured cookie authenticates until `exp`. What follows from
it and was not priced is that `POST /api/auth/sign-out` will act on such a cookie **after the global
kill has already ended that session everywhere else**, and write a permanent audit row doing it. The
two `SelfOnlyEndpoints` refuse the same token `403`; sign-out returns `204`.

### 4.6 D-077 (audit IP, nullable subject) and D-079 (trusted proxies) — **sound**

* The IP column is written from `HttpContext.Connection.RemoteIpAddress` and from no header
  [Verified: 2026-08-26 @ `src/Api/Common/Middleware/AuditCorrelationMiddleware.cs` -> `InvokeAsync`],
  which keeps D-063 §2 intact.
* `UseForwardedHeaders` is registered **only** when `Kaff:TrustedProxyNetworks` is non-empty
  [Verified: 2026-08-26 @ `src/Api/Program.cs`, the `trustedProxyNetworks.Length > 0` block], and the
  shipped default is `[]` [Verified: 2026-08-26 @ `src/Api/appsettings.json` ->
  `TrustedProxyNetworks`], so development, CI and the test host never trust a header. The polarity
  is the correct one: `ForwardedHeadersOptions` with no known proxies means *trust every peer*, and the
  conditional is what stops that being the default.
* Staging is coherent, which is the half a config change could silently break: the allowlist names
  `172.28.0.0/24`, the network block at the bottom of the same file **pins** that subnet rather than
  letting Compose choose one, and the `api` service is `expose`d and never `ports`ed
  [Verified: 2026-08-26 @ `deploy/docker-compose.staging.yml`]. nginx is genuinely the only possible peer.
* The nullable subject is enforced at the database by `ck_audit_records_event_shape` and
  `ck_audit_records_entity_change_has_subject` rather than by `IsRequired()`, so the one legal
  exception — a sign-in failure against a username matching no row — is expressible and nothing else is
  [Verified: 2026-08-26 @ `src/Infrastructure/Persistence/Configurations/AuditConfiguration.cs` ->
  `ck_audit_records_entity_change_has_subject`], consumed at [Verified: 2026-08-26 @
  `src/Api/Features/Auth/SignIn/Handler.cs` -> `HandleAsync`].

### 4.7 The stack runs

`node .claude/skills/run-kaff-erp/driver.mjs smoke`, against the API on 5080 and the Angular dev server
on 4200, both started per the skill:

```
PASS  API /api/health returns 200          PASS  the Angular application mounted — kaff-root present=true
PASS  health reports healthy               PASS  SPA renders content — 108 chars
PASS  database reachable                   PASS  document direction is RTL — dir=rtl
PASS  database guards installed — []       PASS  page contains Arabic text
All checks passed.
```

Note this is the *shipped* smoke, which now asserts `kaff-root` first — the check that closes the
chromium-error-page trap the skill documents. Nothing below is claimed on a stack that was not running.

---

## 5. Two further defects

### `V-26-F` — **MEDIUM** · the ordering that makes `SpecificRefusal` safe is pinned by nothing

See §4.2. `PermissionEvaluator.Evaluate` must refuse `MustChangePassword` **before** matching grants,
or `errors.auth.password_change_required` becomes a per-endpoint "you would have been allowed" oracle —
the disclosure D-080 declined to make, arriving through D-086's key instead of through a status code.

Swapping those two statements changes no status code, breaks no existing assertion about which error a
caller who holds the permission receives, and reintroduces the axis leak. **`TC-1-258` exists precisely
because `AC-101a-P` had the same shape** — a safety that lives in a statement's position and cannot be
seen by asserting outcomes one at a time. This one has no `TC-1-258`.

The test that would catch it: a caller with `MustChangePassword` true who does **not** hold the
permission must receive the same `password_change_required` key as one who does.

### `V-26-G` — **MEDIUM** · `TC-1-042` was never relocked, so `AC-105a-H` has no live QA coverage

`stories/ac-id-map.md` retired `AC-105a-F` on 2026-08-22 and issued `AC-105a-H` in its place, with an
explicit instruction: *"`qa/slice-1/test-cases.md` -> `TC-1-042` cites it and asserts the withdrawn
rule: **relock it to `AC-105a-H`, whose assertion is the inverse — it cannot be carried across
unrewritten**"* [Verified: 2026-08-26 @ `stories/ac-id-map.md` -> the `KAFF-105a AC6` row].

**Four days later it still cites `AC-105a-F` and still asserts the withdrawn rule** — *"exactly
`PortalRead` and `PortalApprove` are returned and nothing else"* [Verified: 2026-08-26 @
`qa/slice-1/test-cases.md` -> `TC-1-042`]. The shipped endpoint returns `[]`, correctly, because both
grants are `ProjectScoped` and the payload carries company-wide rows only. **`TC-1-042` therefore fails
against correct code**, and it is the only case in the file mapped to this criterion — so `AC-105a-H`
has no live QA coverage at all, while `PermissionEvaluatorTests` -> `A_client_holds_no_company_wide_permission`
passes in the suite. A green build beside a QA file asserting the opposite is exactly the state a
verification pass exists to catch.

Not a code defect. Owner: the Scrum Master, per SM-30.

---

## 6. Per-story verdicts

A story with an open defect against it is **rejected**. A story whose criteria could not be exercised
is **not accepted** — it is recorded as unverified, which is a different thing from a pass.

| Story | Verdict | Reason |
|---|---|---|
| **KAFF-111** revoke on deactivation | **ACCEPT** | Both its QA cases are covered by named, passing tests, and `AC-109-K`'s sibling atomicity claim is now demonstrated under a real fault (§4.1). See the caveat on D-082 §4's *reasoning* below — it does not change the verdict |
| **KAFF-114** revoke an assignment | **ACCEPT, with one uncovered case** | 7 of 8 QA cases covered. `TC-1-120` has no test |
| **KAFF-112** reactivate a user | **ACCEPT, with one uncovered case** | 5 of 6 QA cases covered. `TC-1-094` has no test |
| **KAFF-100** bootstrap the Owner | **ACCEPT** | All 10 QA cases covered, including the concurrency one against real PostgreSQL. The route's anonymity is deliberate, allow-listed with a written reason, and the allow-list is now proved to fail closed (§4.4) |
| **KAFF-101a** sign-in | **ACCEPT** | Including `TC-1-258`. The one endpoint in this slice that gets the role bar right — see `V-26-B` |
| **KAFF-103** change password | **ACCEPT** | `V-26-D` is a wrong comment beside correct behaviour, and `AC-103-H` is uncovered at the API |
| **KAFF-102** sign-out | **REJECT** | `V-26-C` — a token the global kill already ended writes a permanent audit row |
| **KAFF-105a** `GET /api/auth/me` | **REJECT** | `V-26-B` — answers `200` to `Role.Subcontractor` and `Role.Client`. Compounded by `V-26-G`: its replacement criterion has no live QA case |
| **KAFF-109** change a user's role | **REJECT** | `V-26-A` (a reachable `500` with no `messageKey`) and `V-26-B` (the role change that produces the unsafe session) |

### KAFF-111 — accept

Both cases pass: `TC-1-091` [Verified: 2026-08-26 @ `tests/Api.Tests/DeactivateUserTests.cs` ->
`The_assignments_are_revoked_kept_on_file_and_audited_one_by_one`] and `TC-1-092`
[@ `DeactivateUserTests.cs` -> `The_next_request_on_the_same_session_is_refused_with_no_re_authentication`,
`Both_devices_are_refused_on_their_next_request`]. Rule 9 — a leaver with no assignments — has its own
case [@ `DeactivateUserTests.cs` -> `A_user_with_no_assignments_deactivates_and_writes_one_record`].

**This is the story the brief flags as never covered on its own criteria by any verification report.**
It is covered now, on its own criteria, and it passes. The one thing it shares with KAFF-109 and
KAFF-110 — D-082 §4's "structurally unreachable" claim — is addressed in §4.1: the conclusion holds,
the premise does not.

### KAFF-114 — accept, one case uncovered

`AC-114-A` … `AC-114-F` are each covered [@ `RevokeProjectAssignmentTests.cs` ->
`Access_ends_on_the_next_request_after_revocation`, `The_revoked_row_stays_on_file_with_every_column_populated`,
`Re_assignment_after_revocation_creates_a_new_row_and_leaves_the_old_one_untouched`,
`Revoking_an_already_revoked_assignment_is_refused`, `Nobody_but_the_owner_and_hr_can_revoke_an_assignment`,
`The_revocation_leaves_a_modified_audit_record_naming_what_changed`], and `AC-114-F`'s route half is
asserted against the host's own route table rather than against source text
[@ `EndpointPermissionCoverageTests.cs` -> `No_endpoint_deletes_a_project_assignment`].

**`TC-1-120` — "revoking the last person on a project is allowed" (KAFF-114 rule 7) — has no test.**
The QA case exists to pin an *absence*: no rule anywhere forbids emptying a project's team, and the
day somebody adds one, nothing goes red. Uncovered, P2, recorded.

### KAFF-112 — accept, one case uncovered

`AC-112-A`, `-B`, `-D`, `-G`, `-H` and `-I` each have a passing test
[@ `ReactivateUserTests.cs` -> `Twelve_audit_records_written_before_leaving_still_name_the_reactivated_user`,
`Reactivation_restores_no_assignment_and_leaves_the_revoked_rows_exactly_as_they_were`,
`Reactivating_with_no_temporary_password_still_clears_the_old_credential`,
`The_stored_credential_changes_when_a_temporary_password_is_issued_on_reactivation`,
`Reactivating_an_active_user_is_refused`, `Nobody_but_the_owner_can_reactivate_a_user`,
`Assigning_a_reactivated_user_to_their_old_project_creates_a_new_row_with_a_fresh_author`].

Rule 9a — `Reactivate()` rotating the stamp independently of the credential — is real in the entity
[Verified: 2026-08-26 @ `src/Domain/Identity/User.cs` -> `Reactivate`] and covered
[@ `ReactivateUserTests.cs` -> `A_token_minted_before_deactivation_is_still_refused_even_after_reassignment`].

**`TC-1-094` — "the username stays reserved while the account is off" (rule 4) — has no test.** It
asserts that `ux_users_user_name` is not filtered on `is_active`, which is exactly the kind of index
predicate a later migration adds without noticing. Uncovered, P2, recorded.

### KAFF-100 — accept

All ten cases are covered, and the two that matter most are covered properly rather than by proxy:
`TC-1-216` runs two genuinely concurrent requests against real PostgreSQL
[@ `CreateOwnerTests.cs` -> `Two_concurrent_requests_produce_exactly_one_owner_and_one_refusal`], and
`TC-1-218`'s "emptiness test, not a flag" is asserted through a deactivated Owner still closing the
screen [@ `CreateOwnerTests.cs` -> `A_deactivated_owner_still_refuses_a_second_setup`].

The handler is right about which of its two checks is load-bearing, and it catches the unique-violation
by **constraint name** rather than by error class, so the loser of the race gets `setup.already_completed`
and never a `500` [Verified: 2026-08-26 @ `src/Api/Features/Setup/CreateOwner/Handler.cs` ->
`IsBootstrapRace`]. **That is the pattern `V-26-A` is missing** — KAFF-100 translates a database refusal
back into a `Result`; KAFF-109 does not.

The two anonymous routes are narrow: `GET /api/setup` discloses `!Users.AnyAsync()` and nothing else,
`POST /api/setup` cannot succeed against a non-empty table. §4.4 proves the allow-list fails closed.

### KAFF-101a — accept

All 23 cases including `TC-1-258` are covered by `SignInTests` and `PasswordHasherTests`, and the two
that pin an *ordering* rather than an outcome were both **watched red** by their authors and are
robust under load (§4.3).

`SignIn.Handler` is the one place in this slice that gets the role bar right: `Role.Client` and
`Role.Subcontractor` are folded into the generic `401` before a session is minted
[Verified: 2026-08-26 @ `src/Api/Features/Auth/SignIn/Handler.cs` -> `HandleAsync`]. **That check
existing here is what makes its absence on `/api/auth/me` a defect rather than a design choice** — the
system already knows those two roles may not hold a session; one route forgot to ask.

🟡 The handler's own open question stands and this report does not close it: an inactive account is
folded into the generic `401` rather than given `errors.auth.account_inactive`, which the story's i18n
bullet names and no criterion reaches. Correctly flagged for Nabil rather than decided.

### KAFF-103 — accept, with `V-26-D`

Seven of seven QA cases are covered at the level they name, except `TC-1-027`'s API half.
`TC-1-027` (`AC-103-H`) is *Domain + Api*: the Domain half passes
[@ `tests/Domain.Tests/UserTests.cs` -> the `SubcontractorCannotLogIn` assertion], **the Api half has
no test** — and per `V-26-D`, the handler's recorded reason for not needing one is wrong.

The rest is sound, including the carve-out that makes `AC-103-B` work
[@ `ChangePasswordTests.cs` -> `Until_the_password_is_changed_every_other_endpoint_refuses_it_and_this_one_does_not`]
and the deactivated-account refusal, which `MUT2` proved is a test that can fail
[@ `ChangePasswordTests.cs` -> `A_deactivated_account_cannot_change_its_own_password`].

### KAFF-102 — reject

Its five QA cases all pass, `TC-1-019`'s deliberate replay half included
[@ `SignOutTests.cs` -> `A_replayed_cookie_still_works_because_nothing_is_revoked`], and the
cookie-attribute case that D-050 exists for
[@ `SignOutTests.cs` -> `The_cookie_is_cleared_with_the_same_name_path_and_attributes_it_was_minted_with`].
`Sign_out_never_rotates_the_security_stamp` pins the trade the other way, correctly.

**It is rejected on `V-26-C`, which no QA case asks about.** The suite proves sign-out does the right
thing for a live session and for no session; nobody asked what it does for a session the system has
already killed. It accepts it and writes an uncorrectable audit row.

### KAFF-105a — reject

`TC-1-045`, `TC-1-235`, `TC-1-236` and `AC-105a-A/B/C/G` are covered. `TC-1-046` ("the payload carries
no money") has no test but is satisfied structurally — the response record has seven fields and none is
a `Money` or a `decimal` [Verified: 2026-08-26 @ `src/Api/Features/Auth/WhoAmI/Response.cs` ->
`Response`].

**Rejected on `V-26-B`.** And its coverage picture is worse than the suite suggests: per `V-26-G`, the
one QA case mapped to `AC-105a-H` still asserts the criterion it replaced, so the endpoint's
best-tested behaviour is documented in `test-cases.md` as the opposite of what it does.

### KAFF-109 — reject

Nine QA cases, eight covered and passing; `TC-1-079` is `PENDING Q27 (residual)` and cannot be
executed. `AC-109-K` is now demonstrated under a real fault (§4.1).

**Rejected on `V-26-A` and `V-26-B`.** Both come from the same root: `User.ChangeRole` re-applies the
*creation* invariants and nothing about the *transition*. It does not ask whether the target role may
hold a session, whether the account holds a credential the new role may not, or whether the change
should end the session it silently leaves alive.

---

## 7. QA cases: what ran, what did not

**75 cases** map to these nine stories (`TC-1-001` … `TC-1-258`, per the story headings in
`qa/slice-1/test-cases.md`). All are `Api`, `Domain + Api` or `Api + E2E` — none is Web-only, so the
execution vehicle is `Kaff.Api.Tests` and `Kaff.Domain.Tests`, both green.

| Outcome | Count | Which |
|---|---|---|
| **Pass** — covered by a named test in a green suite | **69** | The bulk of `TC-1-001`…`006`, `007`…`018`, `019`…`022`, `023`…`029`, `045`, `046`, `075`…`080`, `091`…`098`, `113`…`119`, and `216`…`239`, `258` |
| **Fail** | **1** | **`TC-1-042`** — asserts the withdrawn `AC-105a-F` rule against correct code. See `V-26-G` |
| **Cannot be executed** | **1** | **`TC-1-079`** — `PENDING Q27 (residual)`. The case itself says it cannot be written until the residual half of Q27 is ruled. Not a gap in the build |
| **No test exists** | **4** | **`TC-1-120`** (KAFF-114 rule 7), **`TC-1-094`** (KAFF-112 rule 4), **`TC-1-027`'s Api half** (`AC-103-H`), and **`TC-1-046`** (satisfied structurally, but by inspection rather than by an assertion) |

`TC-1-258`, the case the brief singles out, **passes**, and both of its halves are genuinely covered —
the status-code half [@ `SignInTests.cs` -> `A_locked_account_answers_423_to_the_right_password_and_401_to_a_wrong_one`]
and the time-envelope half [@ `SignInTests.cs` -> `No_refusal_is_faster_than_the_hash_it_should_have_paid_for`],
which §4.3 establishes is a test that can fail without being one that fails spuriously.

---

## 8. What this session could **not** verify

Recorded rather than folded into a pass.

* **`TC-1-079`** — `PENDING Q27 (residual)`. Blocked on a ruling, not on a build.
* **The E2E half of `TC-1-223`** (`SameSite=Strict` as the whole CSRF control, `Api + E2E`). The Api
  half is covered; the browser half was not exercised — the E2E suite was not run against a real
  cross-site request, and asserting a browser's cookie policy from a `TestServer` proves nothing about
  a browser.
* **Whether `V-26-B`'s `Role.Client` half is reachable in production.** `Role.Subcontractor` is
  reachable and demonstrated; `Role.Client` was proved only from a hand-issued identity, because
  `User.ChangeRole` closes the staff-to-client transition (`V-26-E`). If a future story opens any path
  by which a client identity acquires a staff cookie, `V-26-B` becomes materially worse and nothing
  will announce it.
* **Anything about a real deployment.** The stack was run locally (§4.7). Staging was verified by
  reading `deploy/docker-compose.staging.yml`, not by connecting to it.

---

## 9. The one thing Nabil should know

**`GET /api/auth/me` will tell a subcontractor who they are.**

spec.md §9 says a subcontractor is *"record only, no login"*, and the system enforces that in three
places — the entity refuses the credential, the database refuses it with a check constraint, and the
sign-in door refuses the role outright. All three are real and all three work.

But a fourth place had to re-apply those checks by hand, because it deliberately sits outside the
permission gate, and it copied two of the three. So the endpoint the frontend trusts to answer *"who
am I and what may I do"* answers a subcontractor with a `200` and their name — and the route that puts
a subcontractor in that position, `PUT /api/users/{id}/role`, is one the Owner can call on any
departmentless account, including his own.

**The pattern is what matters, not the one route.** Every time an endpoint is exempted from the gate
for a good reason, somebody re-implements the gate's checks by hand, and hand-copies are one item
short. There are two such endpoints today and the exemption list is designed to grow. The list
records *why* each is exempt; nothing records *what each therefore owes*, and no test checks that it
paid. Two of the seven findings in this report — `V-26-B` and `V-26-F` — are that same shape: a safety
that exists only as a line somebody remembered to write, with nothing that goes red when the next
person does not.
