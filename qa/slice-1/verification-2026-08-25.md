# Verification — slice 1: KAFF-106, KAFF-108, KAFF-113, KAFF-110 · 2026-08-25

**Verifier Agent, fresh session.** Read `spec.md` §9 / §8 / §12, `CLAUDE.md`, `agents.md` §7,
`process/agile.md`, the four story files, `decisions.md` D-044 / D-048 / D-049 / D-062 / D-066 /
D-067 / D-069 / D-071 / D-072 / D-073, and the previous report
`qa/slice-1/verification-2026-08-23.md`. Every finding below was **re-established against the files
as they stand today**, not inherited.

**The Verifier reports. It does not fix.** Nothing under `src/`, `tests/`, `stories/`, `decisions.md`
or `qa/` was changed. This file is the only artefact created.

Every claim carries `[Verified: 2026-08-25 @ `File` -> `Identifier`]` per SM-31 — identifier, never a
position.

**Three categories throughout, never two:** *satisfied* · *deferred, with the reason and the owner* ·
*not verifiable in this session*. A criterion that nobody can execute is recorded as such, not folded
into a pass.

---

## 0. Baseline — the gate before any test result is trusted

No `Kaff.Api`, `Kaff.Api.Tests` or `Kaff.Domain.Tests` process was running when the build ran, so
nothing was locked, nothing was skipped by a failed copy, and no suite ran against a stale binary.

| Gate | Result |
|---|---|
| `docker start kaff-db` | container up |
| `dotnet build KaffErp.sln -c Release` | **Build succeeded, 0 warnings, 0 errors, exit 0** |
| `dotnet build KaffErp.sln -c Release --no-incremental` | **Build succeeded, 0 warnings, 0 errors, exit 0** |
| `MSB3021` / `MSB3026` / `MSB3027` in either build | **none** |
| `Kaff.Domain.Tests.exe` | **75 / 75**, 0 failed, exit 0 |
| `Kaff.Api.Tests.exe` | **106 / 106**, 0 failed, exit 0 |
| `scripts/check-citations.ps1` | exit 1 — 502 checked, **0 broken**, **97 legacy line-number citations** |

**The full rebuild is deliberate.** An incremental build finished in under four seconds, which is
exactly the shape D-069 §6 warns about — so it was re-run with `--no-incremental` and the suites were
run after that. The `MSB3026`-on-success trap is closed for this run by inspection of the build log,
not by the exit code alone.

**The brief's baseline figures are correct**: build 0/0 exit 0, zero `MSB302x`, Domain 75/75, Api
106/106.

**Two things the brief asked for that this session could not run**, both refused by the sandbox rather
than by the stack, and both recorded here rather than worked around:

* **`node .claude/skills/run-kaff-erp/driver.mjs smoke`** — the API could not be started
  (`dotnet run --project src/Api/Kaff.Api.csproj` was denied). **Nothing in this report is claimed to
  be "running"**; every HTTP-level assertion below comes from `Kaff.Api.Tests`, which drives the real
  `Program` through `WebApplicationFactory` — the same pipeline, the same policy provider, the same
  gate.
* **The `EndpointPermissionCoverageTests` mutation** — deleting `.RequirePermission(...)` from
  KAFF-108's endpoint to watch the coverage test go red was denied (edits to `src/` were refused).
  §3 below establishes the same claim by a different route and states exactly what is left resting on
  D-069's own record.

The legacy citation count is unchanged at **97** and **none of them is in any of the four story
files, in `decisions.md`, or in any file this work touched** — 67 are in
`meetings/2026-08-21-sprint-1-refinement.md`, the rest in `qa/slice-1/test-cases.md` and
`qa/questions.md` [Verified: 2026-08-25 — counted per file across `*.md`]. Under D-068 the Definition
of Done's citation line is scoped to the change in front of the reviewer, so this does not block any
of the four stories; the backlog of 97 is still owed and is still the Scrum Master's.

---

## 1. KAFF-106 — the Owner creates a user with a role and a department

**11 criteria, `AC-106-A` … `AC-106-K`** [Verified: 2026-08-25 @
`stories/slice-1-foundation/KAFF-106-owner-creates-a-user.md` -> `AC-106-K`]. The count in the brief
and in the previous report is right.

| AC | Verdict | Evidence |
|---|---|---|
| `AC-106-A` | **Satisfied** | [@ `CreateUserTests.cs` -> `The_owner_creates_a_finance_user`], [@ `CreateUserTests.cs` -> `The_password_the_owner_sets_is_temporary_and_is_not_stored_as_typed`], audit half at [@ `CreateUserTests.cs` -> `The_creation_leaves_an_audit_record_naming_the_owner_the_role_and_the_department`]. **Its "can sign in only to change that password (AC-103-B)" clause is a cross-reference, is not built, and is not counted as passed here** — see the not-verifiable row below |
| `AC-106-B` | **Satisfied — V-A is closed** | See §2 |
| `AC-106-C` | **Satisfied** | HR refused at the endpoint inside the six-role sweep [@ `CreateUserTests.cs` -> `Nobody_but_the_owner_can_create_a_user`]; the "unaffected" half now has a real endpoint behind it, not only a probe [@ `AssignUserToProjectTests.cs` -> `Hr_staffs_a_project_it_was_never_assigned_to_and_still_cannot_open_it`] |
| `AC-106-D` | **Satisfied** | [@ `CreateUserTests.cs` -> `An_operations_user_must_carry_a_sub_department`] and the inverse [@ `CreateUserTests.cs` -> `Only_operations_users_may_carry_a_sub_department`] |
| `AC-106-E` | **Satisfied**, wider than asked | [@ `CreateUserTests.cs` -> `An_external_role_cannot_be_given_a_department`] covers `Role.Subcontractor` too |
| `AC-106-F` | **Satisfied**, both halves | [@ `CreateUserTests.cs` -> `A_client_user_names_a_client_and_nobody_else_does`] |
| `AC-106-G` | **Satisfied** | [@ `CreateUserTests.cs` -> `A_username_cannot_be_taken_twice_in_a_different_case`]. Built under the D-062 §1 waiver; **Q51 stays open and this verification does not close it** |
| `AC-106-H` | **Deferred, with reason** | Nothing is built. **No gate anywhere reads `MustChangePassword`** — the only references under `src/` are the property, its EF configuration, and the response DTO [Verified: 2026-08-25 @ `src/Domain/Identity/User.cs` -> `MustChangePassword`; @ `src/Api/Features/Users/CreateUser/Response.cs` -> `MustChangePassword`] — and `errors.auth.password_change_required` exists in no file in the repository [Verified: 2026-08-25 — absent from `src/`, `tests/` and both locale catalogues]. Owner: **KAFF-101a / KAFF-103**, exactly as D-066's "Not done" section says |
| `AC-106-I` | **Satisfied** | [@ `CreateUserTests.cs` -> `Eight_lower_case_characters_are_accepted_as_a_temporary_password`], with the boundary below it [@ `CreateUserTests.cs` -> `Seven_characters_are_refused`] |
| `AC-106-J` | **DEFERRED — explicitly, and it is not a pass** | See the box below |
| `AC-106-K` | **Satisfied, at the endpoint** | All four placements including the null one [@ `CreateUserTests.cs` -> `An_hr_user_cannot_be_created_outside_hr_at_the_endpoint`], plus the second half [@ `CreateUserTests.cs` -> `An_hr_user_in_the_hr_department_is_created_normally`] |

**Not verifiable in this session:** the sign-in clause inside `AC-106-A` — *"can sign in **only** to
change that password"*. It is the same behaviour as `AC-106-H` and there is no sign-in endpoint to
exercise it against; the shipped route table is `GET /api/health`, `POST /api/users`,
`PUT /api/users/{userId}/department`, `POST /api/users/{userId}/deactivate` and
`POST /api/projects/{projectId}/assignments` [Verified: 2026-08-25 — the `IEndpoint` implementors
under `src/Api/Features/`].

> ### ⚠️ `AC-106-J` — Arabic, RTL, at mobile width. **DEFERRED. It has not been verified and must not be counted.**
>
> **There is no screen.** `src/Web/src/app/features/` contains exactly one feature, `status`
> [Verified: 2026-08-25 — the only component files there are `status-page.ts`, `status-page.html`,
> `status-page.css`]. The `users.*`, `enum.Role.*`, `enum.Department.*` and
> `enum.OperationsSubDepartment.*` keys the story's i18n bullet lists **are in neither catalogue** —
> `en.json` holds 107 keys and not one begins `users.`, `enum.` or `assignments.`
> [Verified: 2026-08-25 @ `src/Web/public/locales/en.json` -> `errors.auth.forbidden`, the nearest
> named thing to the absence].
>
> **Reason:** the Angular user form is Frontend's and has not been built. **Owner: Frontend Agent.**
> **KAFF-106 must not be marked Done on an 11-of-11 reading.** The honest score is **9 satisfied, 2
> deferred, 1 clause not verifiable** — and the previous verifier's own warning still applies: *"the
> temptation on a green suite is to read 11 of 11."*

---

## 2. `AC-106-B`, both halves — the `messageKey` and the log

### The `messageKey` half — **closed, and D-071's fix does reach every refusal the gate produces**

The refusal now carries the key, asserted for all six roles beside the status and beside "created
nothing" [@ `CreateUserTests.cs` -> `Nobody_but_the_owner_can_create_a_user`]. **V-A is closed.**

**Does it reach every refusal, or only the one that was reported?** Four checks, three of which are
independent of the create-user endpoint.

1. **The fix is at the one point every refusal routes through.** One `CustomizeProblemDetails`
   callback on `AddProblemDetails`, mapping 401 to `AuthorizationErrors.NotAuthenticated` and 403 to
   `AuthorizationErrors.Forbidden` [Verified: 2026-08-25 @ `src/Api/Program.cs` ->
   `AddProblemDetails`; @ `src/Domain/Authorization/SeparationOfDuties.cs` -> `AuthorizationErrors`].
   `UseExceptionHandler` and `UseStatusCodePages` are both registered and both write through
   `IProblemDetailsService`, which is where the callback runs [Verified: 2026-08-25 @
   `src/Api/Program.cs` -> `UseStatusCodePages`].
2. **It is asserted on routes that carry no feature code at all**, so a later per-endpoint patch that
   left the siblings silent turns it red [@ `PermissionMechanismTests.cs` ->
   `A_refusal_from_the_gate_names_a_key_the_ui_can_render`] — 401 on an anonymous call to a
   company-wide probe, and 403 on a real gate refusal.
3. **Three of the four shipped feature endpoints assert the key on their own 403**:
   [@ `CreateUserTests.cs` -> `Nobody_but_the_owner_can_create_a_user`],
   [@ `DeactivateUserTests.cs` -> `Nobody_but_the_owner_can_deactivate_a_user`],
   [@ `AssignUserToProjectTests.cs` -> `Nobody_but_the_owner_and_hr_can_staff_a_project`] and
   [@ `AssignUserToProjectTests.cs` -> `Hrs_reach_stops_at_a_project_that_does_not_exist`].
4. **The fourth does not, and it is KAFF-108's.**
   [@ `MoveUserDepartmentTests.cs` -> `Nobody_but_the_owner_can_move_a_user_between_departments`]
   asserts the status and not the key. `AC-108-E` asks only for 403, so **the criterion is
   satisfied** — but the endpoint-level cover for the key is carried entirely by the mechanism test
   at (2). Recorded as a coverage note, not a defect.

**Two residuals, neither of them scored against a criterion.**

* **The callback fills a key for 401 and 403 only** [Verified: 2026-08-25 @ `src/Api/Program.cs` ->
  `AddProblemDetails`]. A framework-produced **400** (a malformed body, an enum member the converter
  cannot parse), **404** (an unmatched route) or **415** still reaches the client with no
  `messageKey`. Domain refusals are unaffected — those go through
  [@ `src/Api/Common/Results/ResultExtensions.cs` -> `Problem`], which sets `code` and `messageKey`
  itself. No story criterion requires a key on a malformed request and none is asserted; **routed to
  the Architect as a question of scope, not reported as a defect.**
* **`TryAdd`'s specificity guarantee is untested.** D-071 argues that a handler which already named a
  more specific key keeps it. Nothing asserts that — changing `TryAdd` to an assignment would flatten
  a domain `Forbidden` to the generic key and turn no test red [Verified: 2026-08-25 — the identifier
  `TryAdd` appears in no file under `tests/`]. It is latent today because **no shipped handler returns
  an `ErrorType.Forbidden`**: every `Error.Forbidden` in the system is declared in
  [@ `src/Domain/Authorization/SeparationOfDuties.cs` -> `AuthorizationErrors`] and none is returned
  by a handler. SM-30's shape — an absence. **Routed to QA → Backend.**

### The *logging* half — **it exists.** The brief's suspicion that it may not is wrong

`AC-106-B` says *"every refusal is logged"*, and that is a separate claim from the key. It is met:

* **The gate logs every non-granted decision**, with the permission, the user, the project and the
  reason [Verified: 2026-08-25 @ `src/Api/Authorization/PermissionAuthorizationHandler.cs` ->
  `HandleRequirementAsync`]. The log line is reached on the path taken by *every* refusal, because
  `Granted` returns before it and nothing else does.
* **It is emitted in every environment, not only Development.** The category falls under
  `Logging:LogLevel:Default`, which is `Information` in the base configuration
  [Verified: 2026-08-25 @ `src/Api/appsettings.json` -> `Logging`]. A `Warning` default would have
  silenced it; it does not.
* **Observed today**, in this session's Api run: `Refused SiteExpenseConfirm for user … on project …:
  RoleNotGranted.`

**What is missing is an assertion, not an implementation.** No test can fail on the log line
disappearing. `spec.md` §9 does not require a refusal to write an *audit record*, and none is written
— that reading is unchanged and correct. **Routed to QA as coverage, not to Backend as a defect.**

---

## 3. KAFF-108 — move a user between departments

**7 live criteria** — `AC-108-A`, `B`, `G`, `C`, `D`, `E`, `F` — plus **`AC-108-B2`, retired**
[Verified: 2026-08-25 @ `stories/slice-1-foundation/KAFF-108-move-a-user-between-departments.md` ->
`AC-108-G`]. The out-of-alphabet order is deliberate and correct under `stories/README.md` rule 3.

| AC | Verdict | Evidence |
|---|---|---|
| `AC-108-A` | **Satisfied** | [@ `MoveUserDepartmentTests.cs` -> `A_move_changes_what_the_next_request_reaches_without_a_new_token`]. The stamp is captured **before** the move and replayed after it, so "the same token" is real rather than assumed, and the role is `Role.TechnicalOffice` as the corrected criterion requires |
| `AC-108-B` | **Satisfied** | [@ `MoveUserDepartmentTests.cs` -> `The_reverse_move_takes_effect_on_the_next_request_too`] — the revoking direction, on the same captured stamp |
| `AC-108-G` | **Satisfied, at the endpoint** | [@ `MoveUserDepartmentTests.cs` -> `A_site_engineer_gains_nothing_from_the_same_move`], which also asserts the move itself succeeded, so the refusal cannot be a refused move in disguise |
| `AC-108-C` | **Satisfied**, both directions | [@ `MoveUserDepartmentTests.cs` -> `The_department_rules_are_re_applied_on_a_move`], with "neither refusal moved anybody" |
| `AC-108-D` | **Satisfied**, all four destinations including null | [@ `MoveUserDepartmentTests.cs` -> `An_hr_user_cannot_be_moved_out_of_hr_at_the_endpoint`], plus the second half [@ `MoveUserDepartmentTests.cs` -> `An_hr_user_may_be_moved_within_hr`] |
| `AC-108-E` | **Satisfied — D-067 is closed** | [@ `MoveUserDepartmentTests.cs` -> `Nobody_but_the_owner_can_move_a_user_between_departments`]; the gate is present [Verified: 2026-08-25 @ `src/Api/Features/Users/MoveUserDepartment/Endpoint.cs` -> `MoveUserDepartment`] |
| `AC-108-F` | **Satisfied** | [@ `MoveUserDepartmentTests.cs` -> `Assignments_survive_the_move`] — both rows present and both active |

**Deferred: none. Not verifiable at criterion level: none** — KAFF-108 has no UI criterion. The
Definition of Done's *"Arabic RTL correct at mobile width"* and *"runs on staging"* lines are **not
verifiable for this story either**, for the same reason as `AC-106-J`: there is no department-move
screen, and CI has never executed (D-072 §5). Recorded, not scored.

### Would `EndpointPermissionCoverageTests` genuinely have caught D-067?

**Yes — and the answer does not rest on the entry's word, though one part of the evidence does.**

**What I established directly.**

1. **The enumeration reads the routes the host built, not the source.** `ShippedEndpoints` walks
   every registered `EndpointDataSource`, keeps `RouteEndpoint`s, and drops only those whose handler
   `MethodInfo` resolves to an assembly other than the one declaring `PermissionRequirement` — i.e.
   `Kaff.Api` [Verified: 2026-08-25 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
   `ShippedEndpoints`]. **The filter fails closed**: an endpoint whose handler cannot be identified
   is treated as shipped surface.
2. **KAFF-108's route survives that filter by construction.** It is registered through the same
   discovery every other slice uses [Verified: 2026-08-25 @
   `src/Api/Common/Endpoints/IEndpoint.cs` -> `MapKaffEndpoints`], and its handler is
   `Kaff.Api.Features.Users.MoveUserDepartment.Handler.HandleAsync` [Verified: 2026-08-25 @
   `src/Api/Features/Users/MoveUserDepartment/Handler.cs` -> `HandleAsync`] — declared in `Kaff.Api`,
   so `handler != shipped` is false and the route is not skipped.
3. **The enumeration is proven live, today, on a route of exactly that class.**
   `Every_allow_list_member_is_mapped_and_says_so_in_its_own_file` finds `GET /api/health` in
   `ShippedEndpoints` by method and raw route pattern and then reads its `IAllowAnonymous` metadata
   [Verified: 2026-08-25 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
   `Every_allow_list_member_is_mapped_and_says_so_in_its_own_file`]. That test is **green in this
   session's 106/106 run**, and it cannot be green unless the walk really produced a `Kaff.Api`
   `IEndpoint`-registered minimal route with a resolvable `MethodInfo`. `GET /api/health` and
   `PUT /api/users/{userId:guid}/department` are registered by the identical mechanism.
4. **The move route is not allow-listed.** The allow-list has exactly one member, `GET /api/health`
   [Verified: 2026-08-25 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` -> `AllowList`], so
   an ungated move route would fall to `Every_mapped_endpoint_carries_a_permission_requirement` and
   be named in the failure message.
5. **The check is for a `PermissionRequirement` policy specifically, never for authorization in
   general** [Verified: 2026-08-25 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
   `DeclaredPermissions`], and the fallback policy that admitted D-067's attacker only requires an
   authenticated caller [Verified: 2026-08-25 @ `src/Api/Program.cs` -> `SetFallbackPolicy`]. So
   "authenticated" cannot satisfy it.

**What still rests on D-069's record, and I am saying so rather than implying otherwise.** I could
not re-run the mutation — deleting the `RequirePermission` line and watching the test go red — because
source edits were refused in this session. D-069 §3 records the red and quotes its message,
`found at least one item {"PUT /api/users/{userId:guid}/department"}`. That string is the endpoint's
`RoutePattern.RawText` **including the route constraint**, which matches
[Verified: 2026-08-25 @ `src/Api/Features/Users/MoveUserDepartment/Endpoint.cs` -> `Route`] — a form
you do not produce by writing the entry from memory. Combined with (1)–(5), I am satisfied the
machine covers this route. **Anyone who wants the watched-to-fail evidence first-hand should re-run
that one mutation; it takes a minute and it is the only unexecuted item in this section.**

**One prose defect in the machine's own file, and it is D-067's exact shape.** The `AllowList`
doc comment reads *"Two members means two decisions"* above a list with **one** member
[Verified: 2026-08-25 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` -> `AllowList`]. Harmless
today, and it is a comment describing a state the code is not in — in the file written because a
comment described a state the code was not in. **Routed to Backend.**

### D-073 — the audit trail's actor role, and whether it blocks KAFF-108

**The mechanism D-073 names is real, and I confirm both halves.**

* **Authority is read from the database on every request** [Verified: 2026-08-25 @
  `src/Infrastructure/Authorization/PermissionSubjectReader.cs` -> `ReadAsync`, whose `WHERE` filters
  `user.IsActive` and compares `SecurityStamp`], reached from
  [@ `src/Api/Authorization/PermissionAuthorizationHandler.cs` -> `BuildSubjectAsync`]. That is D-048.
* **The audit actor's role is read from the token claim** [Verified: 2026-08-25 @
  `src/Api/Identity/HttpContextCurrentUser.cs` -> `Role`], carried into the record by
  [@ `src/Infrastructure/Persistence/Interceptors/AuditSaveChangesInterceptor.cs` -> `ResolveActor`]
  and stored at [@ `src/Domain/Auditing/AuditRecord.cs` -> `ActorRole`].

So the permission system distrusts the token and the trail believes it, about the same user on the
same request. That is worth fixing and it is the Architect's.

**But D-073's stated concrete case does not land on KAFF-108, and this is the part of the brief that
is wrong.** The brief says: *"move a user out of Technical Office, they act — the gate decides on the
new role, the trail records the old one."* That cannot happen.

* **A department move does not change the role.** `MoveToDepartment` writes `Department` and
  `OperationsSubDepartment` and nothing else [Verified: 2026-08-25 @ `src/Domain/Identity/User.cs` ->
  `MoveToDepartment`].
* **`AuditRecord` carries no department at all** — the actor columns are `ActorUserId`,
  `ActorDisplayName` and `ActorRole` [Verified: 2026-08-25 @ `src/Domain/Auditing/AuditRecord.cs` ->
  `ActorDisplayName`]. There is nothing on the record for a stale department to be wrong *in*.
* **The real trigger is a role change, and there is none.** `Role` is assigned once, in the
  constructor, and no method mutates it [Verified: 2026-08-25 @ `src/Domain/Identity/User.cs` ->
  `Role`]. KAFF-109 is not built. The same is true of `ActorDisplayName`: there is no rename path.

**So no shipped code path can write a wrong `ActorRole` today.** D-073's premise — that the two halves
disagree — is correct; its worked example is attributed to the wrong story.

**What *is* reachable today, and it is the sharper version of the same defect.** `ActorRole` is
`Role?`, is not `IsRequired()` in the EF configuration, and carries no check constraint
[Verified: 2026-08-25 @ `src/Domain/Auditing/AuditRecord.cs` -> `ActorRole`; @
`src/Infrastructure/Persistence/Configurations/AuditConfiguration.cs` -> `ActorDisplayName`, which is
the neighbouring property that *is* required]. **Nothing requires the role claim to be present**, so
a request whose token omits it writes a permanently unattributed row into an append-only table. That
is not hypothetical: it is precisely the failure Backend hit (§6).

**My judgement: D-073 does not block KAFF-108.** It is a **separate defect**, and KAFF-108 can be
accepted without it. The reasons, in order:

1. **It is not an authorization hole.** Nothing is permitted that should be refused. D-048 holds and
   the coverage machine holds.
2. **KAFF-108 cannot produce the wrong value.** A department move leaves `Role` untouched and the
   record has no department field. There is no act in KAFF-108 whose attribution can be wrong.
3. **KAFF-108's endpoint doc is correct about what it claims.** It documents the no-reissue behaviour
   as being about *authority* [Verified: 2026-08-25 @
   `src/Api/Features/Users/MoveUserDepartment/Endpoint.cs` -> `MoveUserDepartment`], and that
   statement is true. It makes no claim about attribution, so it is not a comment describing a state
   the code is not in.
4. **Holding KAFF-108 would put the fix in the wrong place.** The decision D-073 asks for — read the
   role from the database, record both roles, or rotate the stamp on a role change — is owed **before
   KAFF-109 ships and before the first production rows exist**, not before a story that cannot trip
   it. Attaching it to KAFF-108 would let it be closed by a change to the move endpoint, which is
   exactly where it does not belong.

**Its real deadlines, and both are close.** (a) **KAFF-109**, which introduces the first role change
and with it the first genuinely stale role claim; (b) **KAFF-101a**, which mints the first real
tokens — a nullable `ActorRole` with no issuer contract behind it is a permanently unattributed row
waiting to happen. **Routed to the Architect, as D-073 already says.** The null half is worth ruling
on in the same breath as the staleness half.

---

## 4. KAFF-113 — assign a user to a project, with seniority for site engineers

**9 criteria, `AC-113-A` … `AC-113-I`.** First verification.

| AC | Verdict | Evidence |
|---|---|---|
| `AC-113-A` | **Satisfied** | [@ `AssignUserToProjectTests.cs` -> `Hr_staffs_a_project_it_was_never_assigned_to_and_still_cannot_open_it`], which **asserts** HR holds zero assignment rows rather than assuming it. The Owner's half is separate [@ `AssignUserToProjectTests.cs` -> `The_owner_staffs_a_project_without_an_assignment_row_of_their_own`] |
| `AC-113-B` | **Satisfied** | Same test, one line later: `ProjectRead` on the project just staffed is 403 with `errors.auth.forbidden` |
| `AC-113-C` | **Satisfied** | [@ `AssignUserToProjectTests.cs` -> `Hrs_reach_stops_at_a_project_that_does_not_exist`] — 403, explicitly asserted below 500 |
| `AC-113-D` | **Satisfied, at the level the story now specifies** | [@ `AssignUserToProjectTests.cs` -> `The_same_engineer_is_supervisor_on_one_project_and_junior_on_another`]. The evaluator half uses the access the **database** holds for each project, read through the shipped policy rather than constructed in the test |
| `AC-113-E` | **Satisfied**, both directions | [@ `AssignUserToProjectTests.cs` -> `A_seniority_is_refused_for_every_role_but_the_site_engineer`], and it asserts **no row was created with a corrected level** — the coercion a helpful handler performs |
| `AC-113-F` | **Satisfied** | [@ `AssignUserToProjectTests.cs` -> `Clients_and_subcontractors_are_not_assignable`] |
| `AC-113-G` | **Satisfied** | [@ `AssignUserToProjectTests.cs` -> `Nobody_but_the_owner_and_hr_can_staff_a_project`]. The Supervisor site engineer in the list is assigned to that very project, so the refusal is unambiguously about the role half |
| `AC-113-H` | **Satisfied** | [@ `AssignUserToProjectTests.cs` -> `A_deactivated_user_is_not_assignable`] — 409 with `errors.identity.user_is_inactive`, and the leaver is still inactive afterwards |
| `AC-113-I` | **Satisfied**, both halves | [@ `AssignUserToProjectTests.cs` -> `A_second_active_assignment_is_refused_and_re_assignment_after_revocation_is_not`], including that the revoked row stays on file |

**Deferred: none. Not verifiable at criterion level: none.**

**The three things the brief singled out, checked against the code rather than the story.**

* **Seniority is per assignment, not per user.** `Level` is a property of `ProjectAssignment`
  [Verified: 2026-08-25 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Level`], and **`User`
  carries no assignment level at all** [Verified: 2026-08-25 — the identifier `AssignmentLevel` does
  not appear in `src/Domain/Identity/User.cs`; the nearest enclosing named thing is
  @ `src/Domain/Identity/User.cs` -> `Role`]. There is no second place for it to disagree from.
* **`AssignmentLevel` applies only to site engineers.** Both directions are refused in the entity —
  a non-`Standard` level for any other role, and `Standard` for a site engineer
  [Verified: 2026-08-25 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Create`] — and the handler
  passes the level through untouched [Verified: 2026-08-25 @
  `src/Api/Features/Assignments/AssignUserToProject/Handler.cs` -> `HandleAsync`].
* **HR has global reach on a project-scoped permission, and the reach is bounded by the project
  existing.** The catalogue row stays `ProjectScoped` and the endpoint declares
  `ProjectScope.FromRoute()` [Verified: 2026-08-25 @
  `src/Api/Features/Assignments/AssignUserToProject/Endpoint.cs` -> `AssignUserToProject`], while the
  policy answers HR and the Owner without an assignment row through a branch that first checks the
  project exists [Verified: 2026-08-25 @
  `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `GlobalReachAsync`]. **Reach is not
  capability**: HR is absent from `ProjectRead`, which `AC-113-B` asserts one line after `AC-113-A`.
  The scope agreement is additionally held by
  [@ `EndpointPermissionCoverageTests.cs` -> `Every_permission_requirement_declares_the_scope_its_catalogue_row_names`],
  so widening the row to `CompanyWide` to "fix" HR's reach turns a second test red as well.

**Built that no criterion asked for, and all of it is right.** The unique-index race is mapped to the
same refusal rather than a 500 [Verified: 2026-08-25 @
`src/Api/Features/Assignments/AssignUserToProject/Handler.cs` -> `IsDuplicateActiveAssignment`]; a
route naming a user that does not exist returns a translatable 404
[@ `AssignUserToProjectTests.cs` -> `Assigning_a_user_who_does_not_exist_is_refused`]; and the audit
record distinguishes `HrGlobal` from `OwnerGlobal`
[@ `AssignUserToProjectTests.cs` -> `The_assignment_leaves_an_audit_record_naming_the_project_and_how_hr_reached_it`].

**Asked for and not built:** the story's i18n bullet — `assignments.action.assign`,
`assignments.assign.title`, `assignments.field.user`, `assignments.field.level`,
`assignments.hint.level_per_project`, `enum.AssignmentLevel.*`. **None is in either catalogue**
[Verified: 2026-08-25 @ `src/Web/public/locales/ar.json` -> `errors.auth.forbidden`, the nearest
named thing to the absence]. The story says as much and there is no screen to render them; **Frontend's,
with KAFF-115.** Recorded so nobody assumes they exist.

---

## 5. KAFF-110 — deactivate a user, and access ends on the next request

**10 criteria, `AC-110-A` … `AC-110-J`.** First verification.

| AC | Verdict | Evidence |
|---|---|---|
| `AC-110-A` | **Satisfied** | [@ `DeactivateUserTests.cs` -> `The_next_request_on_the_same_session_is_refused_with_no_re_authentication`], including "no state was changed by the attempt", asserted on the audit-record count |
| `AC-110-B` | **Satisfied** | [@ `DeactivateUserTests.cs` -> `A_deactivated_owner_is_refused_on_a_company_wide_endpoint_too`] — the F-11 path, on its own route, on a deactivated **Owner** |
| `AC-110-C` | **Satisfied** | [@ `DeactivateUserTests.cs` -> `Both_devices_are_refused_on_their_next_request`] — two independent clients, neither the one the Owner is using |
| `AC-110-D` | **NOT VERIFIABLE** | *"they cannot sign in again"*. **There is no sign-in endpoint.** The story does **not** defer this criterion the way it defers `AC-110-E`, so it is not deferred — it is unexecutable. It belongs to **KAFF-101a** and must be re-verified there. TC-1-085 covers it and cannot run either |
| `AC-110-E` | **Deferred, with reason** | The story marks it *"moves with KAFF-104, deferred"*. There is no password-recovery endpoint. **Owner: KAFF-104** |
| `AC-110-F` | **Satisfied** | [@ `DeactivateUserTests.cs` -> `The_assignments_are_revoked_kept_on_file_and_audited_one_by_one`] — three rows revoked, three rows still present, four records, sharing the **request's** correlation id taken from the response header rather than from each other |
| `AC-110-G` | **Satisfied, and correctly bounded by Q35** | [@ `DeactivateUserTests.cs` -> `A_supplied_reason_is_stored_verbatim_and_an_absent_one_is_accepted`] — verbatim, in Arabic, on all four records, and the absent case is accepted rather than refused. **Q35 is open and nothing here answers it** |
| `AC-110-H` | **Satisfied** | [@ `DeactivateUserTests.cs` -> `Everything_the_leaver_did_still_names_them_and_the_row_still_exists`] |
| `AC-110-I` | **Satisfied** | [@ `DeactivateUserTests.cs` -> `Nobody_but_the_owner_can_deactivate_a_user`], with the key asserted, and with "no refused attempt revoked anything" |
| `AC-110-J` | **Satisfied** | [@ `DeactivateUserTests.cs` -> `Deactivating_an_already_inactive_user_is_refused_and_touches_nothing`], which compares the revocation timestamps before and after. Built under the D-062 §1 waiver; **Q51 stays open** |

**8 satisfied · 1 deferred (`AC-110-E`) · 1 not verifiable (`AC-110-D`).**

**"Access ends on the next request" — is it genuinely the next request, with no re-authentication?**
**Yes, and the test file is built so that it cannot quietly become the weaker claim.**

* The stamp is captured **before** the act and replayed afterwards, by a helper named for exactly
  that [Verified: 2026-08-25 @ `tests/Api.Tests/DeactivateUserTests.cs` -> `StaleSession`]. Nothing
  in the file re-reads a stamp after the act. Re-reading it would turn *"the next request is
  refused"* into *"a request made with a token issued afterwards is refused"* — a different, much
  weaker claim, and the suite would still be green.
* No token is re-issued and no session store exists. The refusal comes from the subject read, whose
  `WHERE` filters `user.IsActive` and compares the stamp at the database
  [Verified: 2026-08-25 @ `src/Infrastructure/Authorization/PermissionSubjectReader.cs` ->
  `ReadAsync`]. **Two independent refusals**, either of which alone would end the access.
* The stamp rotation is real: `Deactivate` rotates it [Verified: 2026-08-25 @
  `src/Domain/Identity/User.cs` -> `Deactivate`], and **`Reactivate` deliberately does not**
  [Verified: 2026-08-25 @ `src/Domain/Identity/User.cs` -> `Reactivate`] — which is KAFF-112's
  subject and is not a defect here, exactly as the brief says.
* The `SendAsync` helper omits the role, department and sub-department headers on purpose
  [Verified: 2026-08-25 @ `tests/Api.Tests/DeactivateUserTests.cs` -> `SendAsync`], so the assertion
  is about the database and would pass on a token-driven gate only if it supplied them. It does not.

**The deactivation reason.** `AC-110-G` asserts only what is cited — stored verbatim **when
supplied**, and an omitted one accepted. **Whether the Owner must type one is `QA-3` / Q35 and is
still open** [Verified: 2026-08-25 @ `qa/slice-1/test-cases.md` -> `TC-1-086`, which is written as
`PENDING Q35` with no case at all]. The story is silent on it by deliberate withdrawal rather than by
omission, and **I am not scoring it.** The optionality is documented at the request level
[Verified: 2026-08-25 @ `src/Api/Features/Users/DeactivateUser/Request.cs` -> `Reason`], which names
where the gate would go if Karim answers yes. That is the correct outcome and it should not be read
as an answer.

**Asked for and not built:** the story's i18n keys — `users.action.deactivate`,
`users.confirm.deactivate.title` / `.body`, `users.danger.title`,
`users.danger.deactivate_explains`, `users.field.deactivation_reason`, `users.state.inactive`,
`action.confirm`, `action.cancel`. None is in either catalogue. **Frontend's, with the screen.**

---

## 6. Backend's `ActorRole` fix — cause or symptom?

**Symptom.** The cause is D-073 and it is open.

**What the fix was.** The two failing tests were fixed **in the test helpers**, by reading the actor's
role out of the database at request time and sending it as a claim header
[Verified: 2026-08-25 @ `tests/Api.Tests/DeactivateUserTests.cs` -> `ActorRoleAsync`, called from
@ `tests/Api.Tests/DeactivateUserTests.cs` -> `DeactivateAsync`; and
@ `tests/Api.Tests/AssignUserToProjectTests.cs` -> `SessionAsync`, called from
@ `tests/Api.Tests/AssignUserToProjectTests.cs` -> `AssignAsync`]. Both carry an honest comment saying
why: *"a real token carries one and the audit record's `ActorRole` is read from it rather than from
the database."*

**No production code changed.** `HttpContextCurrentUser.Role` still reads the claim
[Verified: 2026-08-25 @ `src/Api/Identity/HttpContextCurrentUser.cs` -> `Role`].

**This is defensible and I am not calling it wrong.** The tests were asserting `ActorRole` against a
harness that was not sending a role claim, which is not what a real token does; making the harness
realistic is the right repair for the *test*. Backend also **diagnosed the cause correctly in
passing**, which is how D-073 came to be raised.

**But it has a consequence nobody recorded, and it is the reason this counts as symptom-level.**
The helpers now read the role **from the database, at the moment of the request** — so in the test
harness the claim and the database **can never disagree**. The exact divergence D-073 describes has
been made **structurally unobservable by the suite**, and the null-role case that produced the two
failures is now unreachable in tests as well. A green suite from here on says nothing about either.

**And the reachable half is unguarded.** `ActorRole` is nullable, not `IsRequired()`, and has no
check constraint [Verified: 2026-08-25 @ `src/Domain/Auditing/AuditRecord.cs` -> `ActorRole`;
@ `src/Infrastructure/Persistence/Configurations/AuditConfiguration.cs` -> `ActorDisplayName`]. A
request without the claim writes a permanently role-less row into an append-only table, and nothing
— not the entity, not the configuration, not the database, not a test — refuses it. **That is the
same shape as the grant-path check constraint D-070 built, and the argument for it is identical.**
Routed to the Architect with D-073.

---

## 7. Findings, routed

| # | Finding | Severity | Owner |
|---|---|---|---|
| **W-1** | **D-073, and the null half.** The trail's `ActorRole` comes from the token claim while authority comes from the database. Not reachable today — no role-change method exists — but `ActorRole` is nullable with no constraint, so a token missing the claim writes a permanently unattributed row **now**. Due before KAFF-109 and before KAFF-101a mints real tokens. Consider `ActorDisplayName` in the same decision | **Defect (forensic)** | **Architect** |
| **W-2** | The `ActorRole` test helpers read the role from the database at request time, so the claim and the database can never disagree in the suite. The divergence in W-1 is now **unobservable by any test**, as is a missing role claim | Coverage | **QA → Backend** |
| **W-3** | *"Every refusal is logged"* (`AC-106-B`) is implemented and emits in every environment, but **no test can fail on it disappearing** | Coverage | **QA** |
| **W-4** | `TryAdd` in the problem-details callback is untested: flattening a specific domain `Forbidden` key to the generic one turns no test red. Latent — no handler returns an `ErrorType.Forbidden` today | Coverage | **QA → Backend** |
| **W-5** | Framework-produced **400 / 404 / 415** responses carry no `messageKey`; only 401 and 403 are filled. No criterion requires it. A scope question, not a defect | Question | **Architect** |
| **W-6** | `EndpointPermissionCoverageTests`'s `AllowList` comment says *"Two members means two decisions"* above a one-member list — a comment describing a state the code is not in, in the file written because of one | Stale | **Backend** |
| **W-7** | **No `decisions.md` build entry for KAFF-108, KAFF-110 or KAFF-113.** D-066 covers KAFF-106 and D-070 covers KAFF-116; D-067 is a defect entry, not a build entry. KAFF-110 folds KAFF-111's revocation into one transaction and KAFF-113 maps a unique-index race to a keyed 409 — both structural. This is V-E repeating | Process | **Backend** |
| **W-8** | **KAFF-110 and KAFF-113 story files still read `Status: Ready`** though both are built and both now have full endpoint suites. SM-29's own subject: a file asserting a state in the present tense that is now wrong. KAFF-106 reads `BUILT — 8 of 11 verified, V-A open`, which is also now stale — V-A is closed | Stale | **BA** |
| **W-9** | **`AC-110-D` is a live, un-deferred criterion with nothing to execute it against.** It should either be marked deferred to KAFF-101a in the story, the way `AC-110-E` is marked to KAFF-104, or KAFF-110 stays open on it. Today it reads as ordinary and is invisible on a green suite | Story | **BA** |
| **W-10** | `qa/slice-1/test-cases.md` has **no case for `AC-108-G`** (KAFF-108 runs TC-1-067…074, covering A, B, C, D, E, F and the audit section). The criterion is covered in code but not in QA's traceability | Traceability | **QA** |
| **W-11** | 97 legacy line-number citations remain, 67 of them in one meeting file. None in any file this work touched, so D-068 scopes them out of these four stories — still owed | Process | **Scrum Master** |
| **W-12** | The `EndpointPermissionCoverageTests` mutation was **not re-executed this session** (source edits refused). §3 establishes coverage by five independent means; the watched-to-fail red itself still rests on D-069 §3 | Evidence gap | **Verifier → whoever re-runs it** |

**Carried forward from 2026-08-23, re-checked today:**
**V-A closed** (§2). **V-B closed** — `PasswordHasher` now has three tests including the recompute
assertion [Verified: 2026-08-25 @ `tests/Api.Tests/PasswordHasherTests.cs` ->
`The_stored_form_names_the_parameters_the_hash_was_actually_produced_with`]. **V-C ruled** by D-069 §4
and now held by
[@ `EndpointPermissionCoverageTests.cs` -> `Every_permission_requirement_declares_the_scope_its_catalogue_row_names`].
**V-D deferred** to slice 4 by D-069 §5. **V-E closed** by D-070 — and reopened in a new place as
W-7. **V-I** is with Karim per D-072 §4. **V-J** is W-11. **V-K** is W-8, now covering three stories.

**Nothing in these four slices adds a stored balance, mutates a posting, uses floating point for
money, or touches anything on the out-of-scope list** [Verified: 2026-08-25 — no `float`, `double` or
`Balance` property is introduced by any file under `src/Api/Features/Users/`,
`src/Api/Features/Assignments/`].

---

## 8. Recommendation — accept or hold, per story

**KAFF-108 — ACCEPT.** 7 of 7 criteria satisfied. D-067's gate is present and the machine that would
have caught its absence genuinely covers this route. **D-073 does not block it**: a department move
cannot produce a wrong `ActorRole`, because `MoveToDepartment` does not touch the role and the record
carries no department. It is a separate defect (W-1) against the Architect, due before KAFF-109.

**KAFF-113 — ACCEPT.** 9 of 9 criteria satisfied. Seniority is per assignment with no second copy on
`User`; the level is refused in both directions; HR's global reach sits on a permission that stays
project-scoped and is bounded by the project existing. The i18n keys are Frontend's and are recorded
as absent, not as done.

**KAFF-110 — HOLD, on one criterion.** 8 of 10 satisfied, 1 deferred with a stated owner
(`AC-110-E` → KAFF-104), and **`AC-110-D` not verifiable** — the story asserts a deactivated user
cannot sign in and there is nothing to sign in to. The hold is **W-9**, and it is a story fix or an
explicit deferral, not code: either the criterion is marked deferred to KAFF-101a the way its
neighbour is marked to KAFF-104, or the story stays open until KAFF-101a exists. **Everything
buildable in this story is built and asserted, including the mechanism the story is named for.**
Q35 and Q51 remain open and this verification closes neither.

**KAFF-106 — HOLD, and the hold has moved.** V-A is closed, so `AC-106-B` now passes on both halves.
9 of 11 satisfied, **2 deferred**: `AC-106-H` (KAFF-101a / KAFF-103) and **`AC-106-J`, which is the
hold** — Arabic, RTL, at mobile width, with no screen and no `users.*` or `enum.*` key in either
catalogue. **It is deferred to Frontend, explicitly, and it is not a pass.** The story cannot be read
as 11 of 11.

**One item that belongs to no story and should not wait for one:** W-1. It is the only finding here
that writes something permanent into a table whose entire purpose is to be believed later.

---

## 9. Errors in this brief

Per `agents.md` principle 7. Five, one of which changes a conclusion.

1. **The D-073 worked example is attributed to the wrong story, and this is the one that matters.**
   The brief says *"move a user out of Technical Office, they act — the gate decides on the new role,
   the trail records the old one."* **A department move does not change the role**
   [Verified: 2026-08-25 @ `src/Domain/Identity/User.cs` -> `MoveToDepartment`] **and the audit record
   has no department field** [Verified: 2026-08-25 @ `src/Domain/Auditing/AuditRecord.cs` ->
   `ActorDisplayName`]. `ActorRole` cannot go stale through KAFF-108. The mechanism the brief
   describes is real; its trigger is a **role change**, and there is no role-change method on `User`
   at all — which the brief itself says two sentences later. The two halves of the brief's own
   paragraph contradict each other, and the correct half is the second one. This is why my answer on
   whether D-073 blocks KAFF-108 is **no**, and it would have been a different answer had the example
   held.
2. **`AC-106-B`'s logging half is implemented.** The brief says *"it may have no implementation."* It
   has one — at the gate, on every non-granted decision, at `Information`, which is the base
   configuration's default level [Verified: 2026-08-25 @
   `src/Api/Authorization/PermissionAuthorizationHandler.cs` -> `HandleRequirementAsync`;
   @ `src/Api/appsettings.json` -> `Logging`] — and refusal lines of exactly that shape appear in this
   session's own Api run. What is missing is a test, not the behaviour.
3. **The brief's status table says KAFF-113 and KAFF-110 are BUILT. The code is; the story files are
   not.** Both still read `Status: Ready`. The brief is right about the world and the files are
   wrong — W-8.
4. **Two of the brief's instructions could not be carried out in this session**, and the reason is
   the sandbox rather than the stack: the API could not be started, so
   `driver.mjs smoke` was not run; and source edits were refused, so the
   `EndpointPermissionCoverageTests` mutation was not re-executed. §0 and §3 say precisely what rests
   on each.
5. **A correction to the previous report that this session cannot settle, and the reason is worth
   recording.** 2026-08-23's V-A gave two reasons, and one was *"there is no `StatusCodePages` or
   forbidden handler"*. `UseStatusCodePages` **is** registered today [Verified: 2026-08-25 @
   `src/Api/Program.cs` -> `UseStatusCodePages`], and D-071 records the fix as **only** the
   `CustomizeProblemDetails` callback — no middleware added. That points to the middleware having
   been there on the 23rd and that half of the reason having been wrong, while the **conclusion** —
   a 403 carrying no `messageKey` — was right. **Git cannot settle it**: the repository has two
   commits and the earlier one already contains the D-071 callback
   [Verified: 2026-08-25 — `git log` shows `8e5c962` and `37fdaa5`, and `8e5c962`'s `Program.cs`
   contains `AddProblemDetails` with the callback]. Recorded as unresolvable rather than asserted
   either way. *(Everything else in the brief checked out: the baseline figures, both lock hazards,
   the criterion counts, D-071's placement and mechanism, D-069's allow-list of one, and
   `Reactivate` deliberately not rotating the stamp.)*

---

*Verified 2026-08-25 in a session that wrote none of this code, against `spec.md` and the four
stories' acceptance criteria rather than against the implementation's intent. Nothing was fixed and
nothing outside this file was changed. Every figure comes from a run made today, after a full
non-incremental rebuild whose log was read for `MSB302x` before any test result was trusted.*
