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

This file is written incrementally and committed as each section closes, so a session that dies
mid-run still leaves something a reader can act on. **A story not listed as reached below has not
been verified and must not be read as passing.**

| Story | Reached | Verdict |
|---|---|---|
| KAFF-109 change a user's role | **yes** | **REJECT** — two defects, `V-26-A` and `V-26-B` |
| KAFF-105a `GET /api/auth/me` | **yes** | **REJECT** — `V-26-B` |
| KAFF-102 sign-out | **yes** | **REJECT (conditional)** — `V-26-C` |
| KAFF-103 change password | **yes** | accept, with `V-26-D` (observation) |
| KAFF-111 revoke on deactivation | **yes** | accept |
| KAFF-114 revoke assignment | **yes** | accept |
| KAFF-112 reactivate a user | **yes** | accept |
| KAFF-100 bootstrap the Owner | **yes** | accept |
| KAFF-101a sign-in | **yes** | accept, with `V-26-E` (observation) |

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
| Every money property `HasPrecision(18, 4)` | **Clean at the level slice 1 reaches.** `Money` is a single value object over `decimal` with its own `Scale`/range guard [Verified: 2026-08-26 @ `src/Domain/Common/Money.cs` -> `Money(decimal)`], converted centrally [Verified: 2026-08-26 @ `src/Infrastructure/Persistence/Converters/ValueConverters.cs`]; the model snapshot carries `HasPrecision(18, 4)` on every money column and `(18, 6)` on percentage columns. The one bare `decimal?` on an entity is `Project.AreaSquareMetres`, which is an area and not money [Verified: 2026-08-26 @ `src/Domain/Projects/Project.cs` -> `AreaSquareMetres`] |
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

*(Sections 4 onward — per-story verdicts, QA case execution, the `AC-109-K` atomicity claim, the
D-084 timing tests, and what could not be verified — follow in the next increment of this file.)*
