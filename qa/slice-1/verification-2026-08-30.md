# Verification — the six unverified commits, and the five lapsed acceptances · 2026-08-30

**Verifier, fresh session. I wrote none of this code.** `agents.md` §7: the Verifier reports; it does
not fix. Every finding below is routed to an owner from `agents.md` §3b.

**Tree judged: `aa8a9ca`** (working tree clean at that commit for `src/`, `tests/`, `stories/`,
`decisions.md`). The brief named `c156613` as the baseline; `c156613` is HEAD~1 — `aa8a9ca` (a
stories-only status sweep) landed after it. Both commits after `45a939d` are markdown only
[Verified: 2026-08-30 — `git show --stat` on both].

**Three verdicts, never two: accept · reject · not verifiable in this session, with the reason.**

---

## 0. Progress of this report

Committed per increment, because ten agents have died in five days on this project.

| Increment | Contents | State |
|---|---|---|
| 1 | Baseline; the `c01959b` attack — five mutations | done |
| 2 | `4885edf` — the mechanism claim, tested at the mechanism | done |
| 3 | `ca4db6c` — the two allow-lists and `Enum.IsDefined` | done |
| 4 | `45a939d` — reach beyond its own route, and what it changed | done |
| 5 | The five lapsed acceptances, re-established at HEAD | done |
| 6 | `AC-106-H`, `AC-110-D`; the two screens; prohibition sweep; skipped count | done |

### Findings index

| ID | Severity | What | Owner |
|---|---|---|---|
| `V-30-A` | **MEDIUM** | The exemption marker is **not** unforgeable. Reflection produces it, the suite reports **227/227** against a route applying none of the three checks — and the claim "the only expression in the language that can produce this metadata" is stated as fact in three places | Backend |
| `V-30-B` | **LOW** | `IsApplied` proves *metadata*, not *behaviour*. A route can carry the marker and not the filter; nothing asserts a dead session is actually refused on a self-only route | Backend |
| `V-30-C` | **LOW** | `45a939d` has no `decisions.md` entry, and it is the widest-reaching of the six commits | Backend |
| `V-30-D` | **LOW** | D-093's *"30 of 30 covered"* is covered **by name only**, and I measured it: replacing a constraint's predicate with `1 = 1` leaves **227/227** green. D-093 records this gap in prose; the measurement is new, and it is the shape that matters for slice 3's money constraints | Backend / Architect |
| `V-30-E` | **INFO** | Brief correction: the brief's citation for `45a939d` names `AddProblemDetails`, which that commit did not touch | Scrum Master |
| `V-30-F` | **INFO** | Brief correction: the Scrum Master's `AllowList` hypothesis is **wrong**. The door is shut | Scrum Master |
| `V-30-G` | **MEDIUM** | `45a939d`'s "every JSON-binding endpoint" is asserted only against **test-host probe routes**, in an environment where half the fix was already the default. No shipped endpoint, and no Development host, is exercised by any test | Backend |
| `V-30-H` | **LOW** | `SKILL.md`'s Gotchas describe a `driver.mjs click` command that does not exist — the dispatch switch has `health`, `api`, `shot`, `eval`, `smoke`, `flow` and nothing else | Backend |
| `V-30-I` | **INFO** | `AC-106-H` and `AC-105a-C` contradict each other in committed text about whether `GET /api/auth/me` is reachable while `mustChangePassword` is true. Both behaviours observed. **Routed, not resolved** — it is one of Nabil's four open questions | BA → Nabil |

### Verdicts at a glance

**Six commits: all ACCEPT, none rejected.** Five lapsed acceptances (KAFF-109, 105a, 102, 101a, 103):
**all re-established at `aa8a9ca`**, each on a mutation I watched fail today or a live observation of
my own — none on the author's evidence. `AC-106-H` and `AC-110-D`: **both discharged**; the stories
that own them, KAFF-106 and KAFF-110, remain **not accepted**. Skipped: **14**, counted in §12.

---

## 1. Baseline — verified, not trusted

Every number the brief gave, re-measured today before any mutation. `/run-kaff-erp` throughout.

| Gate | Brief said | Measured |
|---|---|---|
| `docker ps` | `kaff-db` up | `kaff-db  Up 7 days (healthy)` |
| Stranded hosts | none running | none — `Get-CimInstance Win32_Process` matched on **command line**, per the skill's 2026-08-30 correction, not `Get-Process -Name` |
| Build, `-c Release --no-incremental` | 0 / 0 | **0 warnings, 0 errors** |
| `dotnet format --verify-no-changes` | exit 0 | **exit 0** |
| Domain suite | 107/107 | **107/107** |
| Api suite | 227/227 | **227/227** (3m 36s) |

The process check is worth one line of its own: the corrected `Win32_Process` / command-line form is
what I used, and it is the only form that can answer the question, because `/run-kaff-erp` §1 starts
the API as `dotnet run`. The brief is right about this.

---

## 2. The one attack this pass exists for — `c01959b` / D-094

**The claim under test**, stated in three places today:

* D-094: *"a private nested type cannot be named from outside its containing class, so
  `RequireLiveSession` is now the only expression in the language that can produce this metadata."*
* The code: *"A private nested type cannot be named or constructed from outside `LiveSession` at all,
  so `RequireLiveSession` is now the only expression in the language that can produce this metadata"*
  [Verified: 2026-08-30 @ `src/Api/Authorization/LiveSession.cs` -> `Marker`].
* The failing test's own message, which is what an author reads:
  *"There is no other way to satisfy this — the metadata it stamps is a private nested type, so no
  expression outside `LiveSession` can produce it"*
  [Verified: 2026-08-30 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
  `Every_self_only_member_is_mapped_and_requires_authentication_with_no_permission_of_its_own`].

Five mutations, each applied, watched, reverted, `git status` confirmed clean after each.

### `MUT-B1` — the exact forge D-089 permitted. **The fix holds.**

A new slice `src/Api/Features/Auth/VerifierProbe30/` writing
`.WithMetadata(new LiveSession.Marker())` in place of `.RequireLiveSession()`:

```
error CS0122: 'LiveSession.Marker' is inaccessible due to its protection level
```

D-094's central evidence is real and reproduces. The one-dot forge that produced 215/215 is gone.

### `MUT-B5` — the accessibility is genuinely pinned.

`private sealed class Marker;` widened to `internal` — the one-word edit D-094 worried about.
Build stays clean; `Nothing_outside_LiveSession_can_produce_the_metadata_that_proves_a_route_paid`
fails with *"found at least one item {\"Marker\"}"*. **That test earns its place.** Reverted.

### `V-30-A` — **MEDIUM** · the marker is not unforgeable, and the suite says 227/227

**`MUT-B2`.** The same probe slice, forging the marker by reflection instead of by name — one
expression, inside a feature slice, in the shipped assembly:

```csharp
object forged = Activator.CreateInstance(
    typeof(LiveSession).GetNestedType("Marker", BindingFlags.NonPublic)!, nonPublic: true)!;

app.MapGet(Route, () => Results.Ok(new { probe = true }))
    .WithMetadata(forged)
    .WithName("VerifierProbe30");
```

Listed in `SelfOnlyEndpoints` alongside change-password and me. No `RequireLiveSession()`, so
**none of the three checks run**: not `IsActive`, not the security stamp, not `MayHoldStaffSession`.

| | Result |
|---|---|
| `dotnet build KaffErp.sln -c Release` | **0 warnings, 0 errors** |
| `EndpointPermissionCoverageTests` | **7 / 7** |
| **Full Api suite** | **227 / 227** |

**This is V-27-B's own reading, one notch down in ergonomics and identical in outcome.** The
predecessor's headline was *"the suite reported 215/215 against a route that applied none of the three
checks."* Today it reports **227/227** against a route that applies none of the three checks.

**What is genuinely better, and I want this on the record because the fix is not worthless.** The
D-089 forge was *one member access away and read as innocent* — `.WithMetadata(LiveSession.Marker.Instance)`
looks like an author who found the API. `Activator.CreateInstance(GetNestedType(...))` cannot be
written by accident and cannot be mistaken for correct usage by a reviewer. D-094 raised the cost of
the forge from "a plausible mistake" to "an unmistakable act." That is real, and it is not nothing.

**What is wrong is the claim, not the mechanism.** *"The only expression in the language"* and
*"cannot be named or constructed from outside"* are false as written — `Activator.CreateInstance` is
an expression in the language and constructs it from outside. The claim now sits in the failing
test's message, which is exactly where D-094 found the previous false claim and exactly why it
rewrote it: *"the failing test was the instruction manual."* The instruction manual is accurate about
what to do and **overstated about what is impossible**, which is the property that let D-089 stand
unchallenged for four days.

**Retrospective change 4 is the rule this trips.** *"A self-sealing argument needs a demonstration."*
There is a demonstration here — `CS0122` — and it is real, but it demonstrates something narrower than
the sentence it is offered for. `CS0122` proves *the type cannot be named*. It does not prove *the
metadata cannot be produced*, and the sentence asserts the second.

**Routed to Backend. Two things, and only the first is required:**

1. **Correct the three sentences to what `CS0122` actually establishes.** Something like: *the type
   cannot be named from outside `LiveSession`, so no ordinary expression can produce this metadata;
   reflection still can, and a route that does so is a deliberate forgery rather than a mistake.* The
   fix is honest prose, not more code — and honest prose is the whole remedy the retrospective asked
   for.
2. **Optionally, close it.** A marker that is a *value* is forgeable by anything holding its `Type`;
   that is a property of endpoint metadata, not of this design. Closing it means asserting the
   *behaviour* rather than the *metadata* — see `V-30-B`.

**I am not proposing the fix as a requirement.** Whether the reflection door is worth closing is a
judgement about cost, and it is Backend's and the Architect's, not mine. The **claim** is not a
judgement: it is either true or false, and it is false.

### `V-30-B` — **LOW** · the marker proves metadata, not behaviour

`IsApplied` asks whether a `Marker` is in the endpoint's metadata collection
[Verified: 2026-08-30 @ `src/Api/Authorization/LiveSession.cs` -> `IsApplied`]. `RequireLiveSession`
does two separable things — `AddEndpointFilter(...)` **and** `.WithMetadata(new Marker())`
[Verified: 2026-08-30 @ `src/Api/Authorization/LiveSession.cs` -> `RequireLiveSession`] — and only
the second is what any test reads.

`MUT-B2` is the proof: metadata present, filter absent, every assertion green. The strongest
available assertion for a self-only route is not *"does it carry the marker"* but *"does a request
carrying a stale security stamp actually get `403` from it"* — which no forgery can satisfy, because
it is a fact about the response rather than about a collection. `ChangePasswordTests` and
`WhoAmITests` already establish exactly that for the two real members (§5 below); what does not exist
is a **sweep** that requires it of every future `SelfOnlyEndpoints` member. Routed to Backend as an
option, not a defect on its own.

### `V-30-F` — the Scrum Master's `AllowList` hypothesis is wrong. **The door is shut.**

The brief asked me to test rather than reason, and offered the hypothesis explicitly as unverified.

**`MUT-B3`.** The probe route with **no** `AllowAnonymous()`, no permission, no filter, no metadata —
added to `AllowList` with a reason string, exactly as the hypothesis describes.

`Every_mapped_endpoint_carries_a_permission_requirement` passes, as predicted — `IsAllowListed`
matches on method and route alone
[Verified: 2026-08-30 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` -> `IsAllowListed`].
**But the sweep is not one test.** `Every_allow_list_member_is_mapped_and_says_so_in_its_own_file`
goes red:

```
Expected mapped!.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() not to be <null>
because GET /api/auth/probe30 is allow-listed here, so its own slice must say AllowAnonymous()
```

**`AllowList` membership does require the route to actually be `AllowAnonymous`**, which is precisely
the condition the hypothesis asked about. Saying so plainly, as the brief asked: **the hypothesis is
wrong, and `AllowList` is not the same shape as the required-guard list `V-27-A` was about.**
`V-27-A`'s list had no counter-assertion; this one has a mirror test that reads the route's real
metadata. Routed to the Scrum Master as a correction, not a defect.

### `MUT-B4` — the door that *is* open, confirmed, and already named

The remaining shape, which D-094's own *"What is still true and is not closed by this"* paragraph
names: an `AllowAnonymous` route in `AllowList`, resolving its caller through `ICurrentUser.UserId`,
acting on that row, naming no claim type.

Probe written exactly so — `AllowAnonymous()`, `ICurrentUser current`, `db.Users.FirstOrDefaultAsync`,
returning the caller's username and role with none of the three checks applied.
`EndpointPermissionCoverageTests`: **7 / 7**. `No_feature_handler_reads_the_callers_identity_from_the_token_itself`
greps for `KaffClaimTypes` and this names none
[Verified: 2026-08-30 @ `tests/Api.Tests/EndpointPermissionCoverageTests.cs` ->
`No_feature_handler_reads_the_callers_identity_from_the_token_itself`].

**This is not a new finding.** It is D-094's own stated ceiling, and the test's own remarks state it
too (*"Its ceiling, named"*). I confirmed it rather than repeating it, which is what the evidence rule
asks. Recording it so the next session does not rediscover it as new: **the anonymous hand-roll is
open by construction, it is documented in both the decision and the code, and closing it needs a
reviewer or a wider grep, not a filter — sign-out cannot carry one.**

**All five mutations reverted. `git status` clean. Build 0/0, Api 227/227 restored** before §3 began.

---

## 3. `4885edf` / D-093 — the mechanism claim, tested at the mechanism

D-093's claim is not "30 tests exist"; it is **"30 are covered by one mechanism."** The brief is right
that testing the mechanism is cheaper than 30 mutations and is the load-bearing thing. Three
mutations, applied to `ck_users_subcontractor_cannot_log_in` — the constraint `V-26-A` and `V-27-A`
were both about.

### `MUT-C1` — deleted from the model only. **The mechanism fires.**

The four lines removed from `IdentityConfigurations` exactly as the 2026-08-27 pass removed them
[Verified: 2026-08-30 @ `src/Infrastructure/Persistence/Configurations/IdentityConfigurations.cs` ->
`UserConfiguration`]. Build clean, and then:

```
System.InvalidOperationException : Refusing to start: database guards are missing —
ck_users_subcontractor_cannot_log_in. The append-only and non-negative-balance rules are not
enforced on this database.
```

**190 of 227 failed. The host does not boot.** D-093 reported 180 of 217 on a smaller suite; the
proportion is the same. The union in `FindMissingGuardsAsync` is what does it — the hand-written half
still requires the name after the model half has forgotten it
[Verified: 2026-08-30 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` ->
`FindMissingGuardsAsync`]. **`V-27-A` is closed on evidence, and D-064's one-directional gap is
genuinely shut.**

### `MUT-C2` — deleted from **both** places. **Exactly one test catches it, and it is the right one.**

Removed from `IdentityConfigurations` **and** from `RequiredCheckConstraints`
[Verified: 2026-08-30 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` ->
`RequiredCheckConstraints`]. Build clean; the host boots; **1 of 227 failed**:

```
failed SchemaInvariantTests.Thirty_check_constraints_are_required
  Expected DatabaseInitializer.RequiredCheckConstraints to contain 30 item(s), but found 29
```

D-093 §5's stated purpose, met precisely: the two-list agreement test is satisfied by a consistent
deletion, and the count is the third statement that makes it loud
[Verified: 2026-08-30 @ `tests/Api.Tests/SchemaInvariantTests.cs` ->
`Thirty_check_constraints_are_required`]. **Removing a constraint is now three edits across two
files.** That is the friction D-093 designed, and it works.

### `V-30-D` — the ceiling, measured rather than asserted

D-093 names this gap itself: *"It does not verify the constraint's expression, only its name."*
**Measured, because a recorded gap and a measured one are different things** — the retrospective's
change 4 is precisely about arguments offered in place of demonstrations.

**`MUT-C3`.** Name kept, predicate replaced:

```csharp
table.HasCheckConstraint("ck_users_subcontractor_cannot_log_in", "1 = 1");
```

**Build clean. Api suite 227/227. Domain suite unaffected.** The constraint is present in
`pg_constraint` under the required name, so `FindMissingGuardsAsync` is satisfied; both
`SchemaInvariantTests` assertions compare *names*; nothing anywhere reads the expression.

**Why this is `LOW` and not higher, stated so nobody escalates it wrongly.** For *this* constraint the
green is honest: `User.ChangeRole` refuses the conversion in the domain first, with
`errors.identity.subcontractor_cannot_log_in` and a `409`, so the constraint is genuine defence in
depth and no legitimate request reaches it
[Verified: 2026-08-30 @ `tests/Api.Tests/ChangeUserRoleTests.cs` ->
`Converting_an_account_that_holds_a_credential_into_a_subcontractor_is_refused`]. `V-26-A` closed the
door that used to reach it.

**Why it is worth recording anyway, and this is the part for slice 3.** D-093's own sentence:
*"`ck_postings_amount_positive`, `ck_postings_distinct_accounts` and `ck_postings_not_self_reversing`
— the slice-3 money rules §5 names — are three of the thirty."* Those three have **no domain guard in
front of them today**, because the code that would post has not been written. When it is, the
mechanism will report them covered on the strength of their names, and a migration that changes
`amount > 0` to `amount >= 0` will pass every gate this repository has. CLAUDE.md puts the
safe-never-negative rule in the database *specifically because* application code is the weaker place
for it — a predicate nothing reads is the same exposure one level down.

**Routed to Backend and the Architect together**, because whether to build the schema-wide expression
comparison (D-064's "Not done" paragraph already scopes it) is an architecture call, not a bug fix.
**I am not asking for it in slice 1.** I am asking that *"30 of 30 covered"* be read as *"30 of 30
present by name"* wherever it is repeated, and that the money constraints get expression-level cover
before slice 3 closes, not after.

---

## 4. `ca4db6c` / D-095 — the two allow-lists and the enum refusal

Both predicates are allow-lists today, and each is used at exactly the doors D-095 names
[Verified: 2026-08-30 @ `src/Domain/Identity/Role.cs` -> `MayHoldStaffSession`;
@ `src/Domain/Identity/Role.cs` -> `MayHoldPermissions`]. `MayHoldStaffSession` is asked by
`SignIn.Handler`, `StaffSessionMinter` and `LiveSession.ResolveAsync`; `MayHoldPermissions` is asked
by `PermissionEvaluator.Evaluate`, at both overloads
[Verified: 2026-08-30 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `Evaluate`].
Seven roles at the staff door, eight at the evaluator, differing by exactly `Role.Client`.
`Role.Subcontractor` is in neither.

### `MUT-D1` — the mutation D-095's own evidence table does **not** contain

D-095 watched three failures: the reproduction, `MayHoldStaffSession` restored to a deny-list, and
both restored after the fix. **`MayHoldPermissions` alone was never mutated** — so the evaluator half,
which is the half every permission-gated endpoint in the system runs through, was fixed on the
strength of the other half's demonstration.

Restored it to the pre-`ca4db6c` deny-list, `role is not Role.Subcontractor`. Build clean;
**Domain 106/107**, `A_role_outside_the_enum_is_refused_at_every_door` red. **The gap in D-095's
evidence is a gap in the record, not in the code** — the test does cover it. Reverted.

### `MUT-D2` — the `Enum.IsDefined` refusal, removed

Deleted from `User.ValidateDepartment`, the join `User.Create` and `User.ChangeRole` share
[Verified: 2026-08-30 @ `src/Domain/Identity/User.cs` -> `ValidateDepartment`]. Build clean:

| Suite | Result |
|---|---|
| Domain | **106 / 107** — `UserTests.A_role_outside_the_enum_is_refused_at_every_door` red |
| Api | **224 / 227** — `ChangeUserRoleTests.A_role_outside_the_enum_is_refused_and_never_persisted` red at `-1`, `0` and `99`, all three parameters |

D-095's claim that the guard sits at the shared join rather than in one slice's validator is
**established**: the Domain test asserts `User.Create` refuses `(Role)99` too
[Verified: 2026-08-30 @ `tests/Domain.Tests/UserTests.cs` ->
`A_role_outside_the_enum_is_refused_at_every_door`], which is the half a `ChangeUserRole` validator
would have left open. Reverted.

**One thing D-095 routed and I am not resolving:** the `Role.Subcontractor` conversion question stands
with Nabil, and the `role = '99'` rows D-095 declined to migrate are a data question for the Architect.
Neither moved. Not mine to answer.

---

## 5. `45a939d` — the reach, driven rather than argued

### `V-30-E` — brief correction, first

The brief cites this commit as
`[Verified: 2026-08-30 @ `src/Api/Program.cs` -> `AddProblemDetails`]`. **`45a939d` does not touch
`AddProblemDetails`.** The identifier exists, so the citation checker passes it — which is SM-31's
own blind spot: a citation that resolves to the wrong thing rather than to nothing. The commit's two
edits are `Configure<RouteHandlerOptions>` and an `ExceptionHandlerOptions` with a
`StatusCodeSelector` replacing a bare `app.UseExceptionHandler()`
[Verified: 2026-08-30 — `git diff 45a939d~1 45a939d -- src/Api/Program.cs`]. `AddProblemDetails` and
its `CustomizeProblemDetails` are untouched, which is the point the commit makes about the refusal
body being unaffected. Routed to the Scrum Master.

### The behaviour holds beyond the route its tests name — **driven in Development**

The brief asks the right question and the tests do not answer it. **Every assertion in
`MalformedRequestTests` runs against test-host probe routes** — `ProbeEndpoint.BodyBindingRoute` and
`ProbeEndpoint.BadRequestThrowRoute`
[Verified: 2026-08-30 @ `tests/Api.Tests/MalformedRequestTests.cs` ->
`A_malformed_json_body_is_refused_as_a_client_error`] — in the `Testing` environment, where the
file's own remarks admit *"`ThrowOnBadRequest` was already `false` here."* **No shipped endpoint and
no Development host is exercised by any test in the suite.**

So I drove it. API started through `/run-kaff-erp` §1 with `ASPNETCORE_ENVIRONMENT=Development` — the
environment the defect lived in — and probed with `driver.mjs api`:

| Route | `{value: "x"}` | `{"a":1}}}` | `[]` |
|---|---|---|---|
| `POST /api/setup` | **400** | **400** | **400** |
| `POST /api/auth/sign-in` | **400** | **400** | **400** |
| `POST /api/auth/change-password` | 401 | 401 | 401 |

**Nine malformed requests, zero 500s, in the environment that used to give them.** Change-password
answers `401 / errors.auth.not_authenticated` before binding runs, exactly as the commit's own
comment predicts — the fallback policy, not this fix. **The reach claim is true.**

**The second question — did anything that depended on the Development-only throw change?** Yes, one
thing, and it is the intended direction: the API log carries **zero error-level entries** across
those nine requests (`grep -c "^fail:"` gives `0`). Previously each was logged as *"An unhandled
exception has occurred while executing the request."* A developer now loses the parse-error detail
the throw used to surface; the response still carries a `traceId`. **That is a diagnostics trade,
deliberate and stated in the commit, not a defect.** The 400 body carries no `messageKey` — `W-5`,
open with the Architect, deliberately not widened. Confirmed, not resolved.

### `V-30-G` — **MEDIUM** · the fix is global, the regression cover is not

Nothing in the suite would notice if `Configure<RouteHandlerOptions>` were deleted **and** the
environment reverted to deciding, because the suite only ever runs as `Testing`, where the framework
default already matches the fix.
`The_bad_request_behaviour_is_set_explicitly_rather_than_by_environment` reads the options value out
of the built host [Verified: 2026-08-30 @ `tests/Api.Tests/MalformedRequestTests.cs` ->
`The_bad_request_behaviour_is_set_explicitly_rather_than_by_environment`], which catches the line
being removed — that is real cover and it is why this is `MEDIUM` and not higher. What it does not
catch is a future change that reintroduces environment-dependence some other way, and **no assertion
anywhere names a shipped JSON-binding route.** Routed to Backend: one case against
`POST /api/auth/sign-in` would close it.

### `V-30-C` — **LOW** · no `decisions.md` entry

Confirmed: the commit touches three files, none of them `decisions.md`, and no `D-` entry describes
it [Verified: 2026-08-30 — `git show --stat 45a939d`]. This is the widest-reaching of the six commits
— it changes the refusal shape of every JSON-binding endpoint present and future — and CLAUDE.md's
Definition of Done requires the entry when anything structural changes. **I agree with the Scrum
Master's routing to Backend, and I have not written the entry**, which is not mine to write.

---

## 6. The five lapsed acceptances — the lapse is right, and the behaviour still holds

**The lapse is correct as policy.** Retrospective change 3: an acceptance is a claim about a commit,
and four commits have since moved code those five stories' criteria assert. `ca4db6c` rewrote
`MayHoldStaffSession`, which is KAFF-101a's own role bar and is called inside
`LiveSession.ResolveAsync` [Verified: 2026-08-30 @ `src/Api/Authorization/LiveSession.cs` ->
`ResolveAsync`], the path 102, 103 and 105a all route through. The word `ACCEPTED` had to stop being
true, and it did.

**And the behaviour is unchanged, which is the separate question.** Both rewritten predicates admit
**exactly the same nine roles** as the deny-lists they replaced:

| Role | old staff bar | new `MayHoldStaffSession` | old evaluator bar | new `MayHoldPermissions` |
|---|---|---|---|---|
| Owner · Finance · TechnicalOffice · SiteEngineer · HeadOfDesign · MarketingSales · Hr | admitted | **admitted** | admitted | **admitted** |
| Client | refused | **refused** | admitted | **admitted** |
| Subcontractor | refused | **refused** | refused | **refused** |

Pinned as a nine-row table rather than asserted
[Verified: 2026-08-30 @ `tests/Domain.Tests/UserTests.cs` -> `The_two_role_doors_admit_exactly_these`].
The only inputs whose answer changed are values outside the enum, which no legitimate request
produces. `Enum.IsDefined` is true for all nine, so `User.Create` and `User.ChangeRole` are unchanged
for every real role. `c01959b` changed the marker's **accessibility**, not what
`RequireLiveSession`'s filter does.

**Verdicts, re-established at `aa8a9ca` by execution, not by re-reading the 2026-08-27 report.**

| Story | Verdict | Established today by |
|---|---|---|
| **KAFF-101a** — sign-in | **ACCEPT** | `MUT-F1` (the `IsActive` half dropped) turns `An_inactive_account_is_refused_like_a_stranger` and `A_deactivated_user_loses_the_open_session_and_cannot_sign_in_again` red; `MUT-D1` / `MUT-D2` cover the role bar and the enum; sign-in driven live end to end through the screen |
| **KAFF-102** — sign-out | **ACCEPT** | `204` observed live twice in the drive; the route's `AllowAnonymous` exemption re-tested by `MUT-B3`, which proves `AllowList` membership is not free |
| **KAFF-103** — change your own password | **ACCEPT** | `AC-103-D`, `AC-103-E` and `AC-103-F` all driven or mutated today — §7. `MUT-G` turns four tests red including `The_change_ends_every_other_session` |
| **KAFF-105a** — `GET /api/auth/me` | **ACCEPT** | Driven live: `200` with `permissions: []` while `mustChangePassword: true`, which is `AC-105a-C` and D-072 §2 exactly; `MUT-F2` turns the forced-change gate red |
| **KAFF-109** — change a user's role | **ACCEPT** | `MUT-D2` turns `A_role_outside_the_enum_is_refused_and_never_persisted` red at all three parameters |

**None of these five is accepted on the author's evidence.** Each rests on a mutation I applied and
watched fail today, or on a live observation I made myself.

---

## 7. The two screens — D-092's three half-driven criteria, now driven

**D-092's honesty survives this pass and is worth restating**, because the record is the point: when
asked to separate *observed* from *code-reviewed*, the Frontend agent downgraded three of its own
claims rather than defend them. **I did not silently upgrade any of them.** I drove the halves it
named as missing, and they now close on my evidence, not on its.

Stack through `/run-kaff-erp`: API on 5080, SPA on 4200, `smoke` **8 of 8**, including
`the Angular application mounted — kaff-root present=true` (the check a Chromium error page cannot
satisfy). Driven against a **scratch database** created for this pass, so the existing dev rows were
untouched; the forced-change user was created **through the application** — signed in as the
bootstrapped Owner, then `POST /api/users` with `temporaryPassword` — never by a raw `INSERT`.

| Criterion | D-092 said | Observed today |
|---|---|---|
| `AC-103-D` — a wrong current password refused | *"Not observed… code-reviewed only for that half"* | **Driven at both levels.** Screen: submitted a wrong current password, stayed on `/change-password`, **one** `[role="alert"]` reading `كلمة المرور الحالية غير صحيحة.`, no raw key visible. API: `400 / errors.auth.current_password_incorrect`. **Exactly one alert region — no field-level error**, which is D-091's ⚠️ discipline holding |
| `AC-103-E` — 8 characters, nothing more | *"Not observed: a 7-character password actually refused"* | **Driven.** `Abcdef1` (7 chars) leaves the submit button **disabled** — the client blocks it; the server independently answers `400 / errors.auth.password_too_short`. `abcdefgh` — eight lower-case letters, no digit, no symbol — is **accepted** and lands on `/`. A mismatched confirm also disables submit, and `confirmPassword` never reaches the wire |
| `AC-103-F` — ends every other session | *"Not observed this session — only one device was driven"* | **Established at the mechanism, which is stronger than a second browser.** `MUT-G` removed the stamp rotation from `User.StorePasswordHash` and **four tests went red**: `ChangePasswordTests.The_change_ends_every_other_session`, `SignInTests.A_password_change_kills_the_session_on_the_other_device` (which signs in twice and asserts *both* devices die), `MeTests.A_password_changed_on_another_device_ends_this_endpoints_answer_too`, and `PermissionMechanismTests.Rotating_the_security_stamp_kills_every_existing_session` |
| `AC-101b-F` — reload returns here | already observed by D-092 | **Re-observed at HEAD.** Sign-in posted the credential, called `GET /api/auth/me`, then navigated to `/change-password` |
| `AC-101b-C/G`, `AC-103-I` — Arabic, RTL, mobile | observed | **Re-observed and looked at.** Screenshot at **390px**: `shots/v30-sign-in-390.png` — brand `كف` top-right, language switch top-left, labels right-aligned above their inputs, `دخول` disabled on an empty form, no untranslated key. `dir=rtl`, `lang=ar`, `scrollWidth - clientWidth = 0` on both screens |

**`AC-101b-A` and `AC-101b-D` are not closed and I did not touch them.** They are deferred to
KAFF-105b and KAFF-115, neither of which carries a criterion that builds a shell. The brief is right
that this is a Scrum Master / BA / Nabil matter and not a defect. Recorded, not resolved.

### One thing about the tooling, found while driving

**`driver.mjs` has no `click` command**, though `SKILL.md`'s Gotchas describe one — *"The driver's
`click` matches trimmed `innerText` first and falls back to a CSS selector."* The dispatch switch has
exactly `health`, `api`, `shot`, `eval`, `smoke` and `flow`
[Verified: 2026-08-30 @ `.claude/skills/run-kaff-erp/driver.mjs` -> `main`]. The command table in
`SKILL.md` is correct; the Gotchas paragraph describes a command that does not exist. I drove the
forms through `eval` with synthetic `input` events instead. Routed to Backend as a documentation
defect in the shared skill.

---

## 8. The mechanical prohibition sweep — re-run across all six commits

`CLAUDE.md`'s never-break list, checked against the files today rather than against the last report.

| Prohibition | Result |
|---|---|
| No `float` / `double` near money | **0 occurrences** anywhere under `src/`, excluding `obj/` |
| Money is `decimal` with explicit EF precision | **171** `HasPrecision` calls in `src/Infrastructure/`; exactly **2** bare `decimal` properties in `src/Domain/`, both inside value objects — `Money.Amount` and `Percentage.Fraction` |
| Never store a balance | `AccountBalance` is `HasNoKey()` + `ToView(...)` [Verified: 2026-08-30 @ `src/Infrastructure/Persistence/Configurations/TreasuryConfigurations.cs` -> `AccountBalanceConfiguration`]. `NormalBalance` is a debit/credit **direction** enum, not an amount. **No stored balance column** |
| Never update or delete a posting | **No `MapDelete` anywhere in `src/Api/`.** The only two `MapPut`s are `ChangeUserRole` and `MoveUserDepartment`, neither touching postings. Every `Posting` property has a **private setter**, and `ReversesId` is present [Verified: 2026-08-30 @ `src/Domain/Treasury/Posting.cs` -> `ReversesId`] |
| The hold only grows | Enforced in the domain: a posting *out of* a Hold ledger is refused unless it is `HoldRelease` or a reversal [Verified: 2026-08-30 @ `src/Domain/Treasury/Posting.cs` -> `Create`] |
| Every endpoint checks role **and** assignment | `Every_mapped_endpoint_carries_a_permission_requirement` and `Every_permission_requirement_declares_the_scope_its_catalogue_row_names` both green; `ca4db6c` touched `PermissionEvaluator.Evaluate` and `MUT-D1` / `MUT-F2` both turn it red, so the gate is covered in the file the commit changed |

**`ca4db6c` touched the gate every permission-checked endpoint runs through, and the gate is intact.**

### Closing gates — re-run after every mutation was reverted

| Gate | Result |
|---|---|
| `git status` | **clean** |
| Build, `-c Release --no-incremental` | **0 warnings, 0 errors** |
| `dotnet format --verify-no-changes` | **exit 0** |
| Domain | **107 / 107** |
| Api | **227 / 227** |
| `scripts/check-citations.ps1` | **960 checked · 0 broken · 0 legacy** (935 at the brief's baseline; this report adds the rest) |
| `/run-kaff-erp smoke` | **8 / 8** |

---

## 9. One false start of my own, recorded because the pattern is the project's

Midway through the live drive I read a forced-change Owner successfully creating a user — a `201`
where `403 / errors.auth.password_change_required` was owed — and began writing it up as a **HIGH**
finding. **It was my own stale binary.** I had reverted `MUT-F2` (which deletes the
`MustChangePassword` short-circuit from `PermissionEvaluator`) but restarted the API with
`--no-build`, so the process was running the mutant.

I caught it because the same run also showed `GET /api/auth/me` returning a **non-empty** permission
list for a forced-change caller, which contradicts `WhoAmI/Handler.cs`'s own stated behaviour — two
symptoms with one cause is a mutation, not a defect. Rebuilt, restarted, re-probed: `permissions: []`
and `403 / errors.auth.password_change_required` on both `POST /api/users` and
`PUT /api/users/{userId}/role`.

**This is `SKILL.md`'s stale-binary gotcha arriving through the door it does not watch.** That gotcha
is written about the *build* being stale; this was the *running process* being stale after a clean
build, which reads identically from the outside. *"Read the build result before the test result"*
does not help when the binary was built correctly and then the wrong one was launched. Recorded so
the next session does not spend the same hour, and recorded rather than quietly deleted, because a
verifier who hides a near-miss is asking to be trusted on the ones nobody saw.

**And the corrected finding is the interesting one.** On the clean binary, a forced-change caller who
**does not hold** `UserManage` gets `403 / errors.auth.password_change_required` — **byte-identical
to the holder's answer**, on shipped routes. `V-26-F`'s property, which the 2026-08-27 pass could
only establish against a test-host probe, **holds on the real surface**.

---

## 10. `AC-106-H` and `AC-110-D` — the two criteria no Verifier pass had examined

### `AC-106-H` — **DISCHARGED**

*"Given the Owner creates a user with a temporary password, when that user signs in and calls any
endpoint other than the change-password endpoint, then it is refused with
`errors.auth.password_change_required`."*

Driven end to end on shipped surface, through the KAFF-106 creation path rather than a seeded row:

1. Owner bootstrapped via `POST /api/setup` → `201`.
2. `POST /api/users` with `temporaryPassword` → `201`, `mustChangePassword: true`.
3. That user signs in → `204`.
4. `POST /api/users` → **`403 / errors.auth.password_change_required`**.
5. `PUT /api/users/{userId}/role` → **`403 / errors.auth.password_change_required`**.
6. `PUT /api/users/{userId}/department` → **`403`**.

And it fails when the rule is broken: `MUT-F2` deleted the `MustChangePassword` short-circuit from
`PermissionEvaluator.Evaluate` and turned Domain to **104 / 107** and
`ChangePasswordTests.Until_the_password_is_changed_every_other_endpoint_refuses_it_and_this_one_does_not`
red.

**🟡 One clause is answered by an open question, and I am not answering it.** The criterion says
*"any endpoint other than the change-password endpoint"*, and `GET /api/auth/me` **deliberately
answers 200** to a forced-change caller — observed today, with `permissions: []`. That is
`AC-105a-C` and D-072 §2, built on purpose to close the dead-end loop. **Two committed criteria
disagree with each other**, and the reconciliation is one of the four questions standing with Nabil —
*the `mustChangePassword` reach beyond `/api/auth/me` and change-password*. **Recorded and routed to
the BA. Not resolved here.** I mark `AC-106-H` discharged for every permission-gated endpoint, which
is what the mechanism actually guarantees.

### `AC-110-D` — **DISCHARGED**

*"Given a deactivated user, when they attempt to sign in with their correct password, then it is
refused, and the refusal does not reveal that the account was deactivated rather than never
existing."*

Covered twice, and **both halves fail when the rule is broken**. `MUT-F1` dropped the `IsActive` half
of the sign-in door [Verified: 2026-08-30 @ `src/Api/Features/Auth/SignIn/Handler.cs` ->
`HandleAsync`] and turned red:

* `SignInTests.An_inactive_account_is_refused_like_a_stranger` — a deactivated account holding a
  **correct** credential gets `401`, `errors.auth.invalid_credentials`, and **no `Set-Cookie`**: the
  indistinguishability half.
* `SignInTests.A_deactivated_user_loses_the_open_session_and_cannot_sign_in_again` — the open session
  dies on the next request and the fresh sign-in is refused too.

**🟡 Carried, not resolved:** the story's own i18n bullet names `errors.auth.account_inactive` and
nothing reaches it; the generic `401` is what D-065's reasoning gives. That is recorded in D-084 as a
question for Nabil and the test says so in its own remarks. **If he rules the other way, that test is
what changes.** Not mine to decide.

**Neither story is accepted.** `AC-106-J` (Arabic/RTL at mobile width) has no screen, and `AC-110-E`
is deferred to KAFF-104, out of sprint 1. **KAFF-106 and KAFF-110 stay in "built and verified with a
criterion still held."** Discharging a criterion is not accepting a story.

---

## 11. Verdicts — per commit

| Commit | Verdict | Reason |
|---|---|---|
| `f2b995b` — KAFF-101b, the sign-in screen | **ACCEPT** | Driven live at HEAD: sign-in through the screen posts the right body, navigates, and holds the server's refusal in one page-level region with no raw key. 390px screenshot looked at — RTL, Arabic, no overflow |
| `332c160` — KAFF-103's screen, `AC-101b-F` | **ACCEPT** | All three of D-092's self-downgraded halves driven today (§7); `AC-101b-F`'s in-session redirect re-observed at HEAD |
| `4e688c5` — D-092 updated | **ACCEPT** | `decisions.md` only; its claims re-tested in §7 and they hold |
| `4885edf` — `V-27-A` | **ACCEPT**, with `V-30-D` recorded | The mechanism fires: one-file deletion refuses the host boot (190/227 red); two-file deletion is caught by the count. Name-level only — measured, and honestly stated by D-093 itself |
| `c01959b` — `V-27-B` | **ACCEPT**, with `V-30-A` recorded | The fix is real: the one-dot forge is now `CS0122` and the accessibility is pinned by a test that fails when widened. **The claim attached to it is false** and must be corrected — a prose defect, not a code defect |
| `ca4db6c` — `V-27-C` | **ACCEPT** | Both predicates are allow-lists at every door; `MUT-D1` and `MUT-D2` both go red. Behaviour identical for all nine real roles, pinned as a table |
| `45a939d` — the malformed body | **ACCEPT**, with `V-30-C` and `V-30-G` recorded | Reach established live in Development across two shipped endpoints and nine bodies. Missing its `decisions.md` entry and missing regression cover on shipped surface |

**No commit is rejected.** The two `MEDIUM` findings are a false claim in prose (`V-30-A`) and a
coverage gap (`V-30-G`); neither makes the code it accompanies wrong.

---

## 12. What this session did **not** do — as a count, not as prose

Retrospective change 2: a checker that says *"N checked"* must also say *"M unparsed."* **Fourteen.**

1. **`ck_users_subcontractor_cannot_log_in` is 1 of 30 check constraints mutated.** The other **29**
   were not individually deleted. I tested the *mechanism* instead, which is D-093's actual claim —
   but the distribution across the 29 is untested.
2. **29 of 30 constraint predicates unverified.** `MUT-C3` proved name-level coverage on one. The
   other 29 predicates could each be `1 = 1` and nothing here would notice.
3. **The 8 required triggers and 3 required indexes were not mutated this pass.** D-093 cites an
   earlier pass's `MUT-G4` on `trg_postings_append_only`; I did not re-run it.
4. **`Result.Failure` sites: ~4 of ~103 individually mutated.** The 2026-08-27 pass named ~99 never
   mutated; I reached `UnknownRole`, `CurrentPasswordIncorrect`, `PasswordTooShort` and
   `SubcontractorCannotLogIn`. **~99 remain.**
5. **The E2E Playwright suite was not run.** `KAFF_E2E_BASE_URL` was never set. 5 tests, unexecuted.
6. **`driver.mjs flow`** — the language-switch direction flip — **not run.** I observed `dir=rtl` and
   `lang=ar` statically and in a screenshot; I did not watch the direction *flip*.
7. **The change-password screen was not screenshotted at 390px by me.** Its `dir`, `lang` and
   `scrollWidth - clientWidth = 0` were read programmatically; D-092's own 390px shot is the only
   looked-at image of that screen, and it is the author's evidence, not mine.
8. **No second browser was driven.** `AC-103-F` rests on `MUT-G` and four red tests, not on watching
   a second device get refused.
9. **The reflection forge of `V-30-A` was not driven through HTTP.** I established the suite reports
   227/227 against it; I did not send a request with a stale stamp to the forged route and watch it
   answer `200`.
10. **`MUT-B4`'s anonymous hand-roll was not driven through HTTP either** — same shape, same gap.
11. **The other four `AllowList` members were not individually re-tested.** `MUT-B3` tested the
    mechanism once; `/api/health`, `GET /api/setup`, `POST /api/setup` and `POST /api/auth/sign-in`
    were not each re-probed for their `AllowAnonymous` metadata.
12. **QA's `qa/slice-1/test-cases.md` was not executed case by case.** I worked from the stories'
    criteria and the six commits' claims; `TC-` identifiers are not traced in this report.
13. **KAFF-106 and KAFF-110 are not accepted as stories** — only `AC-106-H` and `AC-110-D` are
    discharged. See §10.
14. **No slice-3 money invariant was tested.** `agents.md` §7's first suite — the §15 worked example,
    hold equals exactly 20%, advance reaches exactly zero, تشوينات nets to zero, no sequence produces
    a negative safe — **has nothing to run against yet.** Zero of those assertions exist. Named here
    so the total is never read as coverage of the thing this project is actually for.

---

## 13. The four business questions — touched, recorded, not answered

None moved, and I answered none of them.

| Question | Where this pass touched it |
|---|---|
| KAFF-118's cut | Not touched |
| The `Role.Subcontractor` conversion | §4 — `ChangeUserRoleTests` refuses a credentialled conversion and permits a credentialless one; **which is right is D-088's open half** |
| The `mustChangePassword` reach beyond `/api/auth/me` and change-password | §10 — **`AC-106-H` and `AC-105a-C` contradict each other in committed text**, and I observed both behaviours. Routed to the BA |
| Q54 / N11's retention consequence | Not touched |

**One data observation, routed rather than acted on.** D-095 left the `role = '99'` row unmigrated and
routed it to Architect / Nabil. **No such row exists on `kaff-db` today** — three users, all with real
roles [Verified: 2026-08-30 — `SELECT user_name, role FROM users`]. That does not close the question
of what such an account should become; it records that there is no live instance to decide about on
this machine.

---

## 14. The one thing Nabil should know

**The suite reported 227/227 against a route that applied none of its checks — for the second
consecutive pass, in the same file, against the same claim.**

The fix in `c01959b` is genuine and that goes first: the forge that produced 215/215 four days ago is
now a compiler error, and the accessibility that makes it one is pinned by a test that fails when
somebody widens it. That is real engineering, and it raised the cost of the attack from *a plausible
mistake* to *an unmistakable act*.

But the sentence written beside it — *"the only expression in the language that can produce this
metadata"* — is false, and it is false in the **failing test's own message**, which is precisely
where D-094 found the previous false sentence and precisely why it rewrote it. The pattern the
retrospective named is not *"we missed something."* It is that **a passing check and an absent check
produce identical output** — and a claim stronger than its evidence is how a check comes to be
believed without being examined. D-089's claim went four days unchallenged because it sounded
structural.

**The remedy is one paragraph of honest prose, not more code.** Say what `CS0122` proves — the type
cannot be *named* — and say that reflection can still produce the value, so a route that does it is a
forgery rather than an error. A reader who knows where the real boundary is can defend it. A reader
who has been told there is no boundary to defend cannot.
