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
| `V-30-D` | **MEDIUM** | `Thirty_check_constraints_are_required` asserts the count of the **hand-written list against itself**, not against the database. Deleting a name from both `RequiredCheckConstraints` and its configuration leaves the count assertion the only tripwire, and the model half of `The_written_out_..._agree` cannot see a constraint the model never declared | Backend |
| `V-30-E` | **LOW** | `AC-110-D` is discharged by a test that passes for the wrong reason — see §7 | QA |
| `V-30-F` | **INFO** | Brief correction: the Scrum Master's `AllowList` hypothesis is **wrong**. The door is shut | Scrum Master |

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
