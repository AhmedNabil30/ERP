# Verification — 2026-09-06

**Verifier, fresh session.** `CLAUDE.md`: *"If you wrote the code, you do not certify it."* I wrote
none of the code in scope and none of the commits in scope.

**Brief:** `meetings/BRIEF-2026-09-06-verifier.md`. It says of itself that it was written by the
session that briefed both building agents and pushed every commit in scope, and that every claim in
it — including the gate figures — is a claim to check. §1a records where it was right and where it
was wrong.

**Auto-compact is OFF.** This file was created before any other action and is written finding-by-
finding, committed as it goes. A section marked `pending` at the end of this file is a section I did
not reach, and that is itself a finding — see §12.

---

## 0. Progress of this report

Everything is `pending` until reached. Nothing is marked done on an author's evidence.

| # | Item | State |
|---|---|---|
| 1 | Opening gate — `HEAD`, `git status`, stranded hosts, baseline measured | **done** — §1 |
| 1a | Corrections to the brief | **done** — §1a |
| 2 | Block 1 · KAFF-117 — `GET /api/audit`, Owner alone, *"even for their own projects"* | **done** — §2 |
| 3 | Block 1 · KAFF-127 — `GET /api/users` (the API half) | **done** — §3 |
| 4 | The role census — is there a second `V-33-A`? | **done** — §4, **yes** |
| 5 | Block 2 · D-096 applied to the twelve lapsed stories | **done** — §5, **45 carry / 3 lapse** |
| 6 | Block 3 · Which stories have no QA case | **done** — §6 |
| 7 | The five places a defect would be invisible | **done** — §7 (4 of 5; the E2E flake is §9) |
| 8 | Frontend units and the SPA build | **done** — §8 |
| 9 | E2E — run more than once | **done** — §9, three runs |
| 10 | Closing gate | **done** — §10 |
| 11 | Verdict per story | **done** — §11 |
| 12 | What I did not reach | **done** — §12 |

### Findings index

| ID | Severity | Subject |
|---|---|---|
| `V-34-A` | **MEDIUM** | **`GET /api/users` shipped against no acceptance criterion.** KAFF-127's criteria are `AC-127-A`…`I` and every one of them is a screen criterion; the story's business-rule table names no read endpoint either. The endpoint's permission, its payload and its refusals are asserted only by tests written in the same commit as the endpoint. `agents.md` §7 exists to stop exactly this: there is no statement of what *should* be true for a Verifier to execute against, only a statement of what *is* |
| `V-34-B` | **MEDIUM** | **The board contradicts itself about both sprint-4 stories, in one file.** `stories/backlog.md`'s sprint-4 lane table (lines 219–220) says KAFF-117 and KAFF-127 are **DELIVERED**; the master story inventory in the same file says KAFF-117 is `Ready` (line 816) and KAFF-127 is *"Proposed for sprint 4 Lane B. **Not pulled: scope is Nabil's**"* (line 825). **KAFF-127's story file was never touched by any of its three commits** — its header still reads `Status: **Ready** … Not pulled`. This is the third instance of the identical drift the board itself recorded for KAFF-116, KAFF-108 and KAFF-113, and block 2's whole arithmetic is read off these rows |
| `V-34-D` | **MEDIUM** | ⛔ **The stated basis for lapsing 48 points is factually wrong.** D-119 §3 and the brief both say *"`93fa417` changed the session gate on 2026-09-03."* The commit is dated **2026-09-01**, and **its entire `src/` diff is XML doc comments — zero executable lines.** It renamed one test and corrected six prose claims. Under D-096 §1 it cannot lapse anything, because it changed no behaviour any criterion could assert. §5 |
| `V-34-E` | **MEDIUM** | **`V-33-A`'s shape recurs on the newest endpoint.** `GET /api/users` — written after the repair, by an agent whose test cites `V-33-A` by name — uses a hand-written refused-role array with no exhaustiveness assertion. **Proven:** removing `Role.HeadOfDesign` leaves `ListUsersTests` **6/6 green, exit 0**, while the same removal reddens `ReadAuditTrailTests` 2/12. The guard exists on **3 of 16** gated endpoints. §4 |
| `V-34-F` | **LOW** | **`Permission.UserRead` is a grant that reaches nothing, and a Domain test's name says otherwise.** No endpoint declares `UserRead`; HR holds it; the Q42 problem it was created for on 2026-08-22 (*"HR could not name a single person to put on a project"*) is unsolved as slice 1 closes. `PermissionEvaluatorTests.Hr_may_read_the_user_list_and_still_reaches_nothing_financial` asserts a capability the shipped system refuses — the SM-33 / D-097 §2 shape. **The gating of `GET /api/users` itself is correct; do not open it to HR.** §3 |
| `V-34-G` | **LOW** | **`AuditRead`'s holder set has no catalogue-level pin.** Granting it to `Role.TechnicalOffice` left **Domain 127/127 green**; only the Api suite caught it. `HeadOfDesign`, `Subcontractor`, HR and `ProjectTeamRead` all have `CatalogueCompletenessTests` pins. The strictest permission in the system — the one Karim ruled on personally, and the one that from slice 3 carries every movement of money — has none. §2 |
| `V-34-H` | **MEDIUM** | **KAFF-125's verdict genuinely lapses — but on `e0fd5cf` / `b5c9e46` / `8ea9258`, not on the commit D-119 blamed.** `landingFor()` changed for two of nine roles after the 2026-09-04 verdict and the `pending` landing kind was deleted from the union; `client-manage.guard.ts` was rewritten to stop hiding refusals. Role-based landing and the route guard are KAFF-125's own criteria. **Bookkeeping, not a defect** — the new behaviour is correct. §5.6 |
| `V-34-I` | **LOW** | **Double-encoded UTF-8 in a shipped source file.** `src/Web/src/app/core/navigation/landing.ts` line 51 carries `âš ï¸` where line 39 carries `⚠️` — verified at byte level, written by `e0fd5cf`. **I swept every `.ts`/`.cs`/`.json`/`.html`/`.css`/`.ps1` under `src`, `scripts` and `tests`: this is the only occurrence, and `ar.json` is clean.** Harmless today (it is a comment), but it is proof that the mangling path the process warns about has already been taken once in this repo |
| `V-34-J` | **MEDIUM** | **No frontend story has ever had a QA case.** KAFF-125, 126, 127 and 128 have **zero** references in `test-cases.md`; every backend story has cases, **KAFF-117 included** (`TC-1-136…142`). The gap is categorical, not chronological — QA's process has never been applied to the Frontend lane, which is why every screen criterion in slice 1 has been discharged by a build session driving Chromium once. §6 |
| `V-34-C` | **LOW** | **KAFF-125 has two rows in the master inventory** (backlog.md lines 823 and 828), with different text and different accompanying notes, and `KAFF-124` is filed after `KAFF-128`. KAFF-125 is one of the twelve stories in D-119's lapsed set, so a carry-or-lapse verdict written onto one row leaves the other saying something else |

**By severity: no HIGH. Five MEDIUM** (`A`, `B`, `D`, `E`, `H`, `J` — six), **four LOW** (`C`, `F`,
`G`, `I`). *Counted: six MEDIUM, four LOW, ten in total.*

**⛔ No defective behaviour was found in shipped backend code.** Every business rule I attacked held,
and the two I fault-injected went red for the right reason. **Six of the ten findings are about the
project's records rather than its code** — `A`, `B`, `C`, `D`, `H`, `J` — which is the same
distribution D-119 §6 reported and the reason §13 exists.

**Three things I proved sound rather than reporting**, because a silent absence is not a result:
`GET /api/audit`'s permission gate (§2), the substring-assertion shape (§7.2), and
`clientManageGuard`/`userManageGuard`'s `await` (§7.3 — `V-33-C` may be closed).

**Two known-open findings that this pass can close**, on evidence in §9:
* **`V-33-E`** — `scripts/seed-demo.ps1` now creates the portal `Role.Client` account it said did not
  exist.
* **`V-33-F` / `V-31-A`** — narrowed, not closed: a **fresh** database reports
  `guardsInstalled: true, missingGuards: []` and runs the full E2E path, so the degradation is in
  that one database's **data**, not in the schema, the migrations or the guards. The Architect's
  repair story is a data-repair story.

---

## 1. Opening gate

**Recorded before anything was measured or mutated.**

| Gate | Value |
|---|---|
| `git rev-parse HEAD` | **`7e6f71d5ed176230cd53b16467875c217176a2a2`** |
| `git status --porcelain` | **`?? qa/slice-1/verification-2026-09-06.md`** — this report, and nothing else |
| Target in the brief | `32770e9` or later — **satisfied**, `7e6f71d` is one commit later (the brief itself) |
| `docker ps` | `kaff-db  Up 2 weeks (healthy)` |
| **Stranded hosts** | **none** — `Get-CimInstance Win32_Process` matched on **command line**, per D-109 §3 |

### Baseline, measured rather than repeated

Every figure below is one I ran in this session. The brief's column is what it claimed.

| Gate | Brief claimed | Measured | |
|---|---|---|---|
| `dotnet build KaffErp.sln -c Release -warnaserror` | 0 / 0 | **0 Warning(s), 0 Error(s), exit 0** | ✅ |
| `dotnet format --verify-no-changes` | 0 | **exit 0** | ✅ |
| Domain suite | 127/127 | **127/127, 0 failed, 0 skipped, exit 0** | ✅ |
| Api suite | 316/316 | **316/316, 0 failed, 0 skipped, exit 0** — 5m 12s | ✅ |
| Citations | 1157 / 0 / 0 | **1157 checked / 0 broken / 0 legacy, exit 0** | ✅ |
| Frontend units | 6/6 | **6/6, exit 0** — §8 | ✅ |
| SPA build under `strictTemplates` | clean | **clean, exit 0** — §8 | ✅ |
| E2E | 18/18 (**the building agent's own figure**) | **18/18, 0 skipped, exit 0 — ×3** — §9 | ✅ independently measured |

**The build result was read before every test result**, every time. The build was run with no
`Kaff.Api` or `Kaff.Api.Tests` process alive — checked by command line, not by process name — so no
suite in this report ran a stale binary.

## 1a. Corrections to the brief

**Where the brief was right.** Its five "invisible places" are real places; its gate figures for
build, format, Domain and citations reproduce exactly (§1); its warning that `git checkout` cannot
revert a mutation in an untracked file is correct and I worked around it (§2, §8).

**Where it needs correcting — recorded here so a later reader does not carry the wrong statement.**

1. **The brief describes KAFF-127 as a delivered story. The board does not.** Three commits shipped
   it, and neither `stories/KAFF-127-user-management-screens.md` nor `backlog.md`'s master inventory
   row was updated: both still say `Ready`, and the story file still says **"Not pulled: scope is
   Nabil's."** The brief was written by the session that pushed those three commits, so this is a
   thing it was in the best position to know and did not say. `V-34-B`.

2. **The brief's block-1 scope line — "KAFF-127 … plus `GET /api/users`" — understates the problem.**
   The endpoint is not merely extra scope; it is scope with **no acceptance criterion anywhere**.
   `V-34-A`. The brief's own defence of it (*"reported not slipped in"*, backlog.md line 220) answers
   the honesty question and not the verifiability one.

3. **Every backend gate figure the brief gave reproduces exactly**: build 0/0, format 0, Domain
   127/127, **Api 316/316**, citations 1157/0/0. No correction needed. Frontend and E2E in §8/§9.

4. ⛔ **The brief's — and D-119's — central factual claim about block 2 is wrong.** Both say
   *"`93fa417` changed the session gate on 2026-09-03."* It did neither. See §5 and `V-34-D`:
   `93fa417` is dated **2026-09-01**, and **its entire `src/` diff is XML documentation comments —
   zero executable lines changed.**

## 2. Block 1 · KAFF-117 — `GET /api/audit`

**Attacked first, per the brief, because its agent hit a rate limit before running a single test.**

### The strictest permission in the system, and it holds

`AC-117-B` is the unusual one: spec.md §9 is otherwise `role × assignment`, and D-049 ruling 1 makes
this permission refuse an **assigned** Technical Office lead the trail of the project they run.

**Mutation `MUT-34-1`, watched.** `PermissionCatalogue.cs` line 440,
`new(Permission.AuditRead, PermissionScope.CompanyWide, [owner], …)` →
`[owner, technicalOffice]`. Mutation confirmed present in the file by re-reading it and by
`git diff --stat` before building; **build 0 warnings 0 errors exit 0**, so no stale binary ran.

| Suite | Result |
|---|---|
| Domain | **127/127 green** — see `V-34-G` |
| `ReadAuditTrailTests` | ⛔ **2 failed / 12**, exit 2 |

Both failures are the right two and fail on the right assertion:

```
An_assigned_technical_office_user_is_refused_the_trail_of_their_own_project
  Expected ... Forbidden {403} because D-049 ruling 1: the trail is 'completely hidden
  from all other roles, EVEN FOR THEIR OWN PROJECTS' ... but found OK {200}.

Nobody_but_the_owner_reaches_the_trail_with_or_without_a_project_id
  Expected ... Forbidden {403} because TechnicalOffice holds no AuditRead ... but found OK {200}.
```

Reverted with `git checkout` (a **tracked** file — D-120 §3's trap does not apply here, and I
checked), re-read to confirm the revert, rebuilt, re-ran: **12/12 green**.

**Verdict: `AC-117-B` and `AC-117-C` are genuinely asserted.** *"Even for their own projects"* is not
prose in this repository; it is a test that goes red when the grant moves, and I watched it.

### What the suite does better than anything else in the repo

`AC-117-C`'s refused set is **derived from `Enum.GetValues<Role>()`** and the hand-written loop is
asserted equal to it (`The_refused_list_is_every_role_that_can_sign_in_and_is_not_granted`). It
counts its own refusals and asserts **14**. `AC-117-B`, `AC-117-E` and `AC-117-H` each carry a
**positive control** — a 403 against an empty trail proves nothing, and the author knew it. This is
the shape every other permission suite in slice 1 should have; §4 is about the one written after it
that does not.

### Verdict per criterion

| AC | Verdict | Basis |
|---|---|---|
| `AC-117-A` | **Pass** | company-wide read asserted across two projects **and** the null-`ProjectId` company rows, which a project-join reading would silently lose |
| `AC-117-B` | **Pass — watched red** | `MUT-34-1` |
| `AC-117-C` | **Pass — watched red**, and exhaustive by construction | `MUT-34-1`, `MUT-34-2` |
| `AC-117-D` | **Pass** | the second door — a forged `Role.Subcontractor` session is refused at the gate, not only at sign-in |
| `AC-117-E` | **Pass** | searched over the whole body in both directions, with the record's presence and the redaction placeholder as the positive control |
| `AC-117-F` | **Pass** | `OwnerGlobal` legible in the Owner's own trail |
| `AC-117-G` | **Pass** | stored reason read back |
| `AC-117-H` | **Pass** | absence asserted against **routes the host actually mapped**, with `GET /api/audit`'s presence as the positive control, plus the database trigger |
| `AC-117-I` | **N/A** | moved to `KAFF-128` as `AC-128-A` before the pull — rule 6, correctly applied |

**KAFF-117: eight of eight backend criteria satisfied. No defect found.** The rate limit cost this
story its author's test run; it did not cost it its tests.

## 3. Block 1 · KAFF-127 — `GET /api/users` and the user-management screens

### `GET /api/users` has no acceptance criterion — `V-34-A`

KAFF-127's criteria are `AC-127-A` … `AC-127-I`. **Every one is a screen criterion.** The story's
ten business rules name no read endpoint. `stories/backlog.md` line 220 defends the addition as
*"reported not slipped in"*, which answers whether it was honest and not whether it is verifiable.

**This is the exact arrangement `agents.md` §7 exists to prevent.** There is no statement of what
`GET /api/users` *should* do for me to execute against — only `ListUsersTests`, written in the same
commit as the endpoint. I can report that the tests are internally strong (`V-34-E` aside): the row's
member set is pinned by a **whitelist**, not a blocklist, which is D-106's and D-114 §1's lesson
correctly applied to the only payload in the system projected straight off `User`. What I cannot do
is verify it, because verification is *"does it do what the story says"* and the story says nothing.

### HR, `Permission.UserRead`, and a grant that reaches nothing — `V-34-F`

**The brief's reasoning holds and I confirm it.** `GET /api/users` returns `UserName`, `Phone`,
`Department`, `OperationsSubDepartment`, `IsActive` and `ActiveProjectNames` for every account — that
is the Owner's administration surface, not D-055 §3's *"names and roles only"*, so gating it
`UserManage` is right and HR's refusal is a ruling rather than an oversight. The author documented
exactly this in the test's own `<remarks>`, unprompted. **Do not open this endpoint to HR.**

**But the brief stops one step early.** `Permission.UserRead` is `CompanyWide`, granted to
`[owner, hr]`, and **no endpoint in the application declares it** — I enumerated every
`RequirePermission` in `src/Api/Features`: `AuditRead`, `ClientManage`, `ProjectAssignmentManage`,
`UserManage`, and nothing else. So:

* Nabil's Q42 ruling of 2026-08-22 created `UserRead` to solve a stated operational problem — *"HR
  held `ProjectAssignmentManage` and could not name a single person to put on a project."* **Slice 1
  is closing with that problem unsolved** and HR holding an inert grant.
* `tests/Domain.Tests/PermissionEvaluatorTests.cs` →
  **`Hr_may_read_the_user_list_and_still_reaches_nothing_financial`**. HR may not read the user list.
  There is one user list and HR gets 403 from it. **The test's name asserts a capability the shipped
  system does not provide** — which is precisely the SM-33 / D-097 §2 rule this project already
  applies: *a false claim in a test's own name is renamed in the change that finds it false.*

Severity **LOW**: no over-grant, nothing insecure, and the endpoint decision is correct. It is an
under-delivery plus a false name, and neither is on the board.

## 4. The role census — and yes, there is another one

**The brief told me to assume there is a second `V-33-A`. There is, and it is on the newest
endpoint in the repository.**

`V-33-A` was not *"`HeadOfDesign` is uncovered."* It was *"a hand-written list of refused roles stays
the length it was written at, so a role added to the enum is silently uncovered."* The repair
(`b413b2b`, D-118) answered that properly in two places, by **deriving** the refused set from
`Enum.GetValues<Role>()` and asserting the hand-written loop equals it. `5b13761` did the same for
`GET /api/audit`. Three suites hold that guard:

```
CreateClientTests.cs:427      Enum.GetValues<Role>().Except([Owner, MarketingSales, Subcontractor])
GetClientTests.cs:187         Enum.GetValues<Role>()...
ReadAuditTrailTests.cs:230    Enum.GetValues<Role>()...
```

**`GET /api/users` — written after the repair, by an agent briefed on it — went back to the
hand-written list.** `ListUsersTests.Every_role_but_the_owner_is_refused_and_no_username_reaches_the_body`
enumerates seven roles as a literal array with no exhaustiveness assertion behind it. Its own
`<remarks>` cites `V-33-A` by name as the reason `Role.HeadOfDesign` is in the array — the finding was
read, and the *instance* was fixed rather than the *shape*.

Counted across the application: **22 mapped endpoints, 16 of them permission-gated, and 3 carry the
guard.** `V-34-E`.

**Proven by mutation, not inferred — `MUT-34-2`, a paired mutation run in one build.** I removed the
identical line, `Role.HeadOfDesign`, from both refusal lists at once, so one build and one binary
answer both halves and neither result can be a stale-binary artefact. Both mutations were confirmed
present by `git diff --stat` before building; **build 0/0, exit 0**.

| Suite | After removing `Role.HeadOfDesign` | |
|---|---|---|
| `ReadAuditTrailTests` (guarded) | ⛔ **2 failed / 12**, exit 2 | the guard bites |
| `ListUsersTests` (unguarded) | ✅ **6/6 passed, exit 0** | **nothing noticed** |

The guarded suite names the missing role in its own failure text:

```
The_refused_list_is_every_role_that_can_sign_in_and_is_not_granted
  Expected covered to be a collection with 7 item(s) ... but {Finance, Hr, TechnicalOffice,
  SiteEngineer, MarketingSales, Client}
Nobody_but_the_owner_reaches_the_trail_with_or_without_a_project_id
  Expected refusals to be 14 ... but found 12 (difference of -2).
```

Reverted both (tracked files), re-read to confirm, **rebuilt**, re-ran: `ListUsersTests` 6/6,
`ReadAuditTrailTests` 12/12, both exit 0.

**Severity MEDIUM, not HIGH, and the distinction is honest:** the `ListUsers` array is complete
against today's nine roles, so nothing is uncovered right now. What is missing is the machine that
keeps it complete — which is the whole of what `V-33-A` cost and the whole of what D-118 bought.

**Where the census comes out clean.** All nine `Role` members are now referenced in `tests/Api.Tests`;
`Role.HeadOfDesign` is thinnest at 12 references across four files, and it is genuinely covered at
`CreateClient`, `GetClient`, `ListUsers` and `ReadAuditTrail`. The 2026-09-05 pass's `V-33-A` is
**repaired in substance**, and I confirm that.

## 5. Block 2 · D-096 applied to the twelve stories

### 5.1 The commit D-119 blamed did not change any behaviour — `V-34-D`

D-119 §3 and the brief both rest the whole 48-point lapse on one sentence:

> *"`93fa417` changed the session gate on 2026-09-03."*

**Both halves are wrong.**

```
> git show -s --format='%ci' 93fa417
2026-09-01 20:34:51 +0300

> git show 93fa417 -- src/   [all +/- lines, excluding /// comment lines]
(nothing)
```

**`93fa417`'s entire `src/` diff is XML documentation comments.** It rewrote three `<summary>` /
`<remarks>` blocks in `LiveSession.cs` to stop claiming the metadata was unforgeable — the claim
`V-30-A` disproved — and it renamed one test to match what its body checks. Its own commit message
says so plainly: *"The mechanism itself (D-094's compile-time pin) is unchanged."*

**D-096 §1's rule is behavioural, deliberately and explicitly: *"the line is behavioural, not
file-based."*** A commit that changes no behaviour cannot lapse a story, because there is no
behaviour any criterion asserts that could have moved. **On its stated basis, D-119 lapsed 48 points
for a documentation commit.**

This is the same failure D-119 itself diagnoses in §5 — *"the board records a state, and the state is
not derived from anything."* The sweep corrected the board by reading git, and then made a claim
about git it had not read.

### 5.2 So I ran the check D-096 actually asks for

Naming the wrong commit is not the same as reaching the wrong conclusion, so I did not stop there. I
enumerated **every commit touching `src/` between the 2026-08-30 pass (`dc76fe7`) and `HEAD`** — 21
commits — and classified what each one did to the shared mechanisms and to each story's own surface.

| Shared mechanism | Change since `dc76fe7` | Kind |
|---|---|---|
| `src/Api/Authorization/LiveSession.cs` | 25 lines | **comment-only** (verified: no non-`///` line in the diff) |
| `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` | 10 lines | **comment-only** (same check) |
| `src/Domain/Authorization/PermissionEvaluator.cs` | +35 | **purely additive** — one new method, `ProjectScopedPermissionsHeld`. **`Evaluate` is untouched** |
| `src/Domain/Authorization/PermissionCatalogue.cs`, `Permission.cs` | +41, +12 | **purely additive** — one new row, `ProjectTeamRead` → `[owner, hr]`. **No existing grant altered or removed** |
| `src/Domain/Auditing/IAuditContext.cs` | +23 | **purely additive** — `DuplicatePhoneAcknowledged = 6`, no renumbering |
| `src/Infrastructure/Persistence/DatabaseInitializer.cs` | +201 | **additive strengthening** — check constraints now pinned by **predicate** rather than by name (`V-30-D`) |
| `src/Api/Program.cs` | ±24 | `ForwardLimit` moved to configuration (`51a0c5a`) — §7.4 |

**⛔ The correction that matters most:** D-119 states that *"`LiveSession.cs`, `PermissionEvaluator.cs`
and `ProjectAccessPolicy.cs` have all moved since."* Two of those three moved **by comment only**, and
the third moved **only by addition, with `Evaluate` — the method every gated endpoint runs through —
byte-identical.** Three files did change; **no gate behaviour did.**

**And these files never appear in the changed set at all**, which settles nine of the twelve stories
outright: `src/Domain/Identity/User.cs`, `StaffSessionRules`, `SignIn/`, `CreateUser/`,
`MoveUserDepartment/`, `ChangeUserRole/`, `DeactivateUser/`, `ReactivateUser/`,
`AssignUserToProject/`, `RevokeProjectAssignment/`, `src/Domain/Auditing/AuditRecord.cs`.

### 5.3 Two of the twelve carry verdicts *newer* than the commit blamed for lapsing them

D-119 §4 and the brief both describe the twelve as carrying verdicts *"on or before 2026-08-30."*
The board says otherwise, in its own master inventory:

* **KAFF-105b** — *"BUILT `e56cd16` and **ACCEPTED 2026-09-04**"* (backlog.md line 804)
* **KAFF-125** — *"BUILT `7461332`, **ACCEPTED as an implementation 2026-09-04**"* (lines 823, 828)

`93fa417` is dated **2026-09-01**. **A commit cannot lapse a verdict given three days after it.**
Eight of the 48 points were on the list for a reason that cannot be true. `V-34-D`.

### 5.4 The licence for the carries, named rather than argued

D-096: a story may be carried past a shared-mechanism change **only where the equivalence is pinned
by a test** — *"the test is the whole of the licence."* For nine of the twelve the clause is not even
engaged, because the files their criteria assert did not change. For the shared permission machinery
every one of them routes through, the licence is:

* **The diff itself** — `Evaluate` unchanged, no catalogue row altered, additions only. This is a
  fact, not an argument.
* **Pinned by test**: `CatalogueCompletenessTests` → `The_set_of_unresolved_permissions_has_not_grown`,
  `A_head_of_design_holds_exactly_one_permission`, `No_permission_is_granted_to_a_subcontractor`,
  `Hr_holds_no_permission_that_touches_money`,
  `No_grant_is_held_by_department_alone_for_a_role_that_has_no_department`, and
  `Owner_and_hr_alone_hold_ProjectTeamRead_and_it_touches_no_money` for the one added row.
* **Fault-injected today, by this session, not cited from a previous one.** `MUT-34-1` (§2) granted
  a permission to a role in that catalogue and I watched the gate refuse to hold — 200 where 403 was
  required. D-096's own precedent was licensed exactly this way: *"it is on the right side of that
  line only because the equivalence was fault-injected by another session and re-run today."*
* **Api 316/316 at `HEAD`**, measured in this session, exercising all sixteen gated endpoints.

### 5.5 Verdict per story

| Story | Pts | Verdict date (board) | Post-verdict change to behaviour **its own criteria assert** | **Verdict** |
|---|---:|---|---|---|
| KAFF-100 | 5 | 2026-08-26 | `DatabaseInitializer` strengthened its constraint check; the bootstrap gate (`!Users.AnyAsync()`, `ux_users_bootstrap_owner_once`) and `POST /api/setup` are unchanged | **CARRY** |
| KAFF-101a | 5 | 2026-08-26 | none — `SignIn/`, `StaffSessionMinter`, `StaffSessionRules` are not in the changed set | **CARRY** |
| KAFF-105b | 5 | **2026-09-04** | none — `WhoAmI/` unchanged since its own build commit `e56cd16`; **and the verdict postdates `93fa417`** | **CARRY** — never belonged on the list |
| KAFF-106 | 5 | 2026-08-25 | none — `CreateUser/` unchanged | **CARRY** |
| KAFF-108 | 3 | 2026-08-25 | none — `MoveUserDepartment/` unchanged | **CARRY** |
| KAFF-110 | 5 | 2026-08-25 | none — `DeactivateUser/` unchanged | **CARRY** |
| KAFF-111 | 3 | 2026-08-26 | none — revocation lives in KAFF-110's handler, unchanged | **CARRY** |
| KAFF-112 | 3 | 2026-08-26 | none — `ReactivateUser/` unchanged | **CARRY** |
| KAFF-113 | 5 | 2026-08-25 | none — `AssignUserToProject/` unchanged | **CARRY** |
| KAFF-114 | 3 | 2026-08-26 | none — `RevokeProjectAssignment/` unchanged; and `No_endpoint_deletes_a_project_assignment` still pins `AC-114-F` against the routes the host maps | **CARRY** |
| KAFF-116 | 3 | 2026-08-24 | none — `AuditRecord.cs` unchanged, `IAuditContext` additive only; `ck_audit_records_grant_path` is now pinned by predicate, which strengthens the criterion rather than moving it | **CARRY** |
| **KAFF-125** | **3** | **2026-09-04** | ⛔ **yes — see below** | ⛔ **LAPSES** |

**Result: 45 of the 48 points carry. 3 lapse. And the one that lapses is not lapsed by `93fa417`.**

### 5.6 ⛔ KAFF-125 lapses, on `e0fd5cf` / `b5c9e46` / `8ea9258` — `V-34-H`

KAFF-125 is *"the staff shell — landings, the session resolver and the route guard."* Its criteria
assert **role-based landing**. `src/Web/src/app/core/navigation/landing.ts` → `landingFor()` is that
behaviour, and it changed for two of the nine roles after the verdict:

```
-      return { kind: 'pending', titleKey: 'landing.pending.owner.title' };
+      return { kind: 'users' };
-      return { kind: 'pending', titleKey: 'landing.pending.marketing_sales.title' };
+      return { kind: 'clients' };
-  | { readonly kind: 'pending'; readonly titleKey: string }
```

**The `pending` landing kind was deleted from the union entirely**, along with its catalogue keys and
the landing page's `@case`. `landing-page.ts` (+27/−8), `landing-page.html` (+18), `app.routes.ts`
(+65) and `client-manage.guard.ts` all moved too — `b5c9e46` rewrote the guard specifically because
it *"refused people by hiding the refusal"* (D-114 §3), which is KAFF-125's route-guard criterion.

**This is D-096 §1's first limb exactly: a later commit changed behaviour this story's own criteria
assert.** It is not a shared-mechanism carry and there is no equivalence to pin, because the
behaviour is deliberately *not* equivalent — the changes are correct and intended. The verdict is
simply older than the code.

**Severity MEDIUM, and it is a bookkeeping verdict, not a defect claim.** I found nothing wrong with
what `landingFor()` does now. What lapsed is the *statement* that KAFF-125 was verified, and
`AC-125-B` was already *"verified by code review only"* (`V-32-D`) before any of this.

**The irony worth recording:** D-119 lapsed twelve stories on a documentation commit and put
KAFF-125 on the list for that wrong reason — while the three commits that genuinely lapsed it sat
in the same sweep's own scope, unmentioned.

## 6. Block 3 · Which stories have no QA case — and the brief is wrong about which

**The brief says:** *"`qa/slice-1/test-cases.md` has no cases for anything built since 2026-09-04 —
not the Client master, not KAFF-117, not KAFF-127."*

**Two of those three are wrong.** I counted `KAFF-nnn` references and `TC-1-nnn` allocations across
all 264 cases in the file:

| Story | QA cases | |
|---|---|---|
| KAFF-117 read the audit trail | **`TC-1-136…142`** — seven | ✅ present, and written **before** the build |
| KAFF-119 register a client | `TC-1-151…159, 240, 241, 262, 263` | ✅ present |
| KAFF-120 · 121 · 122 · 123 | allocated | ✅ present |
| KAFF-124 list and search clients | `TC-1-186…194` | ✅ present |
| **KAFF-125** the staff shell | **none — zero references in the file** | ⛔ |
| **KAFF-126** the client screens | **none — zero references** | ⛔ |
| **KAFF-127** the user-management screens | **none — zero references** | ⛔ |
| **KAFF-128** the audit trail screen | **none — zero references** | ⛔ |

**The gap is not chronological, it is categorical — `V-34-J`.** Every **backend** story in slice 1
has QA cases, KAFF-117 included. **No frontend story has ever had one.** All four screen stories —
125, 126, 127, 128 — are absent from `test-cases.md` entirely, and they are the four stories whose
criteria are about rendering, RTL, mobile width and route guards: exactly the criteria the automated
suites are worst at and where `F-1` (§7.1) was found.

That reframes the brief's own point. It is not that QA fell behind last Thursday; it is that **QA's
process has never been applied to the Frontend lane at all**, which is why every screen criterion in
this slice has been discharged by a build session driving Chromium once and taking a screenshot —
the practice the brief's F-1 hint is itself a complaint about.

### KAFF-117's QA cases, executed

Since they exist, I executed them rather than working from story criteria — `agents.md` §175, which
the last three passes could not follow.

| Case | Verdict | Evidence |
|---|---|---|
| `TC-1-136` portal client refused, with and without a project id | **Pass** | `Role.Client` is in the enum-derived refused set; both query shapes |
| `TC-1-137` subcontractor has no login to try with | **Pass** | refused at sign-in **and** with a forged role-stamped session |
| `TC-1-138` redacted fields stay redacted on read back | **Pass** | whole-body search, both secrets, with presence + placeholder as positive control |
| `TC-1-139` a rejection shows its reason | **Pass** | |
| `TC-1-140` trail cannot be edited from the API | **Pass** | asserted against routes the **host mapped**, with `GET /api/audit` as positive control |
| `TC-1-141` nor from a psql prompt | **Pass** | `DELETE` by `ReadAuditTrailTests`, `UPDATE` by `AuditMechanismTests.An_audit_record_cannot_be_changed_afterwards` — two tests, both halves |
| `TC-1-142` an assigned user cannot read their own project's trail; fourteen refusals | **Pass — watched red** | `MUT-34-1`, §2 |

**Seven of seven.**

## 7. The five invisible places

### 7.1 `F-1` — confirmed, and then **bounded**

**Confirmed.** The two list pages are built identically and only one carries the fix:

| | container | `<bdi>` items | `justify-self` |
|---|---|---|---|
| `user-list-page.css` `.row-link` | `display: grid` | `.row-username`, `.row-phone` | **`start`, line 70**, with the comment *"load-bearing and was found by looking at the screenshot rather than at the overflow measurement"* |
| `client-list-page.css` `.row-link` | `display: grid` | `.row-code`, `.row-phone` | ⛔ **absent** |

`F-1` is real, is in shipped code, and is unfixed. It is already routed, so I do not re-report it.

**The brief's harder question — *"what else did that screenshot not see?"* — has a definite answer,
and it is: nothing else of this shape.** I enumerated every `<bdi>` in the application (nine, across
four templates) and checked each one's containing block:

| `<bdi>` | container | stretches? |
|---|---|---|
| `client-list` `.row-code`, `.row-phone` | **grid** | ⛔ **yes — `F-1`** |
| `user-list` `.row-username`, `.row-phone` | **grid** | no — `justify-self: start` |
| `client-form` `.code-value` (`.code-line`) | `display: flex` + `align-items: baseline` | **no** |
| `client-form` `.duplicate-code` (`.duplicate-row`) | `display: flex` + `align-items: baseline` | **no** |
| `user-form` `.identity-value` (`.identity-line`) | `display: flex` + `align-items: baseline` | **no** |

**The mechanism needs a grid item.** `justify-self` has no effect on a flex item, and a flex item is
content-sized along the main axis rather than stretched — so the three form-page `<bdi>`s cannot
exhibit `F-1` however the text is aligned. **Two grid cases exist, one is fixed, one is not.**

This turns *"assume there are more"* into a bounded result: **`F-1` is exactly one instance**, and the
class of defect is closed by inspection rather than by another screenshot.

### 7.2 The substring assertion — swept, and it does not recur

The KAFF-127 agent caught its own `body.Should().Contain("portal_client_demo")` surviving a rename,
and fixed it to a parsed exact match (`UserScreenTests.cs:318` records this). I swept every positive
`Should().Contain(` on a response body across `tests/Api.Tests` and `tests/E2E.Tests` — 13 remaining
sites. **None is the dangerous shape**, and I checked each rather than counting them:

* **Eleven are error-key assertions** (`errors.auth.invalid_credentials`, `errors.auth.forbidden`,
  `errors.auth.password_change_required`, …) where the key *is* the claim, and every one is paired
  with a status-code assertion.
* `MeTests.cs:231` — an error key, and it is paired with **two `NotContain` assertions** that are the
  real content of the test (D-080: no key that tells an attacker the account exists).
* `ReadAuditTrailTests.cs:346` — asserts the **redaction placeholder** is present, but only over
  records already filtered by an **exact** `changedProperties == nameof(User.PasswordHash)` match and
  guarded by `NotBeEmpty`. It is a positive control, not a whitelist.

**Not a finding.** Reported because the brief asked for the sweep and a silent absence is not a result.

### 7.3 The `await` that pins nothing — `V-33-C` is genuinely closed

**Mutation `MUT-34-3`, watched, on both guards at once.** The brief warned that deleting
`await resolver.ensureResolved()` outright fails `TS6133` and runs **no tests**, which reads as green.
So I mutated to the shape that keeps the reference and removes only the wait:

```
- await resolver.ensureResolved();
+ void resolver.ensureResolved();
```

applied to **both** `user-manage.guard.ts` and `client-manage.guard.ts`, confirmed present by
`git diff --stat` before running.

```
Test Files  1 failed (1)      Tests  2 failed | 4 passed (6)      exit 1

× clientManageGuard waits for the session before deciding, with no guard in front of it
× userManageGuard   waits for the session before deciding, with no guard in front of it
AssertionError: expected '/forbidden' to be 'true'
```

**Red, for exactly the right reason**: the guard decided *before* the session resolved and refused a
user who should have been admitted. The tests run each guard **with no other guard in front of it**,
which is the only arrangement in which the line does any work — and it is what `V-32-D`'s and
`V-33-C`'s E2E-only evidence could never have caught, because `sessionGuard` resolves first in the
real array. Reverted, re-read, re-run: **6/6, exit 0**.

**`V-33-C` may be closed.** I watched it fail. This is the one place the brief asked me to disbelieve
KAFF-127's claim and the claim holds.

### 7.4 `Kaff:ForwardedProxyHops` — still unreachable, and I confirm the previous pass

`docker ps` shows **`kaff-db` only** — no Caddy, no nginx, no staging stack. The value lives in
exactly one place (`deploy/docker-compose.staging.yml:68`), **nothing validates it at startup**, and
`Program.cs:284` defaults to `1` for any deployment that does not use that compose file.

`PermissionMechanismTests.cs:672` proves the mechanism at `forwardedProxyHops: 2`. Nothing in this
repository can prove staging has two proxies.

**The 2026-09-05 pass's conclusion stands unchanged and I re-confirm it rather than re-report it:**
*"the mechanism is proved, the deployment is not."* `deploy/README.md:172` already documents the
symptom. **Still not verifiable from this repository.** Carried to §12.

## 8. Frontend units and SPA build

| Gate | Brief claimed | Measured | |
|---|---|---|---|
| `npm test` (vitest) | 6/6 | **6 passed / 6, 1 file, exit 0** | ✅ |
| `npm run build` (`strictTemplates`) | clean | **Application bundle generation complete, exit 0** | ✅ |

Both re-measured **after** reverting `MUT-34-3` and re-confirmed green, so no figure in this report
was taken from a mutated tree.

## 9. E2E — run three times, against a database I built myself

**This was the brief's only gate figure that had never been independently measured** — *"E2E is the
building agent's own figure."*

### The stack, brought up from nothing

Per `V-33-F` / `V-31-A` the `kaff` development database is degraded, so I did not use it:

```
docker exec kaff-db psql -U kaff -d postgres -c "CREATE DATABASE kaff_v6 OWNER kaff;"
ConnectionStrings__KaffDatabase = ...Database=kaff_v6...
```

**A finding in its own right, and a reassuring one.** On a fresh database the API comes up clean:

```
GET /api/health → 200
{"status":"healthy","databaseReachable":true,"guardsInstalled":true,"missingGuards":[]}
```

`V-33-F` is therefore confirmed as a property of **that one degraded database**, not of the schema,
the migrations or the guards. A fresh `kaff_v6` installs every guard and reports healthy. The
Architect's repair story is a data-repair story.

`scripts/seed-demo.ps1` ran clean, and **it now creates the portal `Role.Client` account**
(`portal_client_demo`, scoped to the corporate client) — **`V-33-E`'s stated gap is closed.** That
finding said *"`scripts/seed-demo.ps1` creates no `Role.Client` user at all"*, and it now does.

### Three consecutive runs

`KAFF_E2E_BASE_URL='http://localhost:4200'` set — and **`skipped: 0` in every run is the proof it
took**, since without the variable the suite skips rather than fails.

| Run | total | failed | succeeded | skipped | exit |
|---|---|---|---|---|---|
| 1 | 18 | 0 | **18** | **0** | 0 |
| 2 | 18 | 0 | **18** | **0** | 0 |
| 3 | 18 | 0 | **18** | **0** | 0 |

**The brief's E2E figure of 18/18 is confirmed, independently, three times.**

### The unexplained flake

The brief's fifth invisible place: the first KAFF-127 run was 17/18, the portal test timing out at
32s while the dev server rebuilt; five runs since were 18/18; *"believed environmental, not proven."*

**It did not reproduce in three more runs — eight consecutive green runs now.** The test in question,
`UserScreenTests.A_portal_client_is_refused_the_staff_host_indistinguishably_from_a_wrong_password`,
passed each time.

**I did not prove it environmental and I am not recording it as closed.** Three passes cannot
distinguish a fixed flake from a rare one, and the original failure mode — a timeout while the dev
server rebuilds — is by construction only reachable on a *cold* dev server. My three runs all hit a
warm one, so **I did not re-test the condition that produced it.** Carried to §12 as unreached, not
reported as resolved.

## 10. Closing gate

| Gate | Value |
|---|---|
| `git rev-parse HEAD` | `f881d17` — this report's own commits, **nothing else** |
| `git status --porcelain` | **empty — tree clean** |
| Files changed under `src/` | **none.** Every mutation reverted, re-read, rebuilt and re-run green |
| Files changed under `tests/` | **none.** Same |
| Pushed | **no**, as instructed |

**Every mutation in this report followed the same cycle, without exception:** apply → **verify present
in the file** (`git diff --stat` plus re-reading the line) → build and **read the build's exit code**
→ run → watch red **and read the failure message** → revert → **verify reverted** → **rebuild** →
re-run green. Every file I mutated was **tracked**, so D-120 §3's `git checkout` trap did not apply —
and I checked that it was tracked rather than assuming.

**Scratch database `kaff_v6` was left in place.** It is not the `kaff` development database and
nothing in the repository points at it; drop it or keep it as a clean baseline.

**One operational mistake of mine, recorded because a silent one is worse:** my teardown process
match was too broad and terminated an unrelated editor process along with the API and dev server. No
repository state was affected — the tree was clean before and after — but the lesson is D-096 §3's
in the other direction: match narrowly enough as well as widely enough.

## 11. Verdict per story

### Block 1 — sprint 4

| Story | Verdict | Note |
|---|---|---|
| **KAFF-117** | ✅ **PASS — 8 of 8 backend criteria, and 7 of 7 QA cases** | The strictest permission in the system, watched red under `MUT-34-1`. The best-built permission suite in slice 1. Its rate limit cost it its author's test run, not its tests. `AC-117-I` correctly moved to KAFF-128 |
| **KAFF-127** (screens) | ⚠️ **CONDITIONAL** | `AC-127-G`'s guard half is **proven** (`MUT-34-3`, §7.3) and `V-33-C` may be closed. `V-33-E` closed — the seed now creates the portal client. But **no QA case exists for this story or any screen story** (`V-34-J`), and its board rows still say `Ready` / *"not pulled"* (`V-34-B`) |
| **KAFF-127** (`GET /api/users`) | ⛔ **NOT VERIFIABLE** | `V-34-A` — no acceptance criterion exists to verify against. The tests are strong but they were written in the endpoint's own commit. Plus `V-34-E`, its unguarded role list |

### Block 2 — the 48 lapsed points: **45 carry, 3 lapse**

| Story | Pts | Verdict |
|---|---:|---|
| KAFF-100, 101a, 105b, 106, 108, 110, 111, 112, 113, 114, 116 | **45** | ✅ **CARRY** — licence in §5.4, per-story reasons in §5.5 |
| **KAFF-125** | **3** | ⛔ **LAPSE** — `V-34-H`, and on `e0fd5cf` / `b5c9e46` / `8ea9258`, **not** on the commit D-119 blamed |

**None of the twelve lapses for the reason D-119 gave**, because that reason does not exist: `93fa417`
changed no behaviour. One lapses for reasons D-119 did not look at.

## 12. What I did not reach

**A section I could not reach is a finding, not a silence.** These are the things this pass did not
settle, listed so the next reader does not mistake absence for a pass.

1. **`Kaff:ForwardedProxyHops` against the real deployment.** Still unreachable — no staging stack
   exists on this machine. §7.4. Unchanged from 2026-09-05; the mechanism is proved, the deployment
   is not, and **no test in this repository can close it.**
2. **The E2E flake's actual condition.** Three green runs do not distinguish a fixed flake from a
   rare one, and all three hit a **warm** dev server, while the original failure needed a cold one.
   **I did not re-test the condition that produced it.** §9.
3. **`AC-125-C`** — Nabil's criterion, deliberately unmet, his call. Untouched, as it must be.
4. **The 80 `[Verified:]` markers in `.cs`/`.ts`** that `check-citations.ps1` does not read. It
   reported **1157 / 0 broken / 0 legacy** over `.md` only, which is the figure I quote in §1 and it
   is not a figure about the code. Known-open, re-confirmed still open.
5. **`N11`** — `audit_records` is still not partitioned, and slice 3 is next. Known-open,
   re-confirmed. `GET /api/audit` now reads that table on every Owner request, so the cost of leaving
   it grows from this sprint rather than from slice 3.
6. **KAFF-127's screen criteria `AC-127-A`…`F`, `H`, `I` were not independently re-driven.** I
   verified the guard (`AC-127-G`) by mutation and confirmed E2E 18/18 three times, which covers
   `AC-127-I`. The RTL/width/consequence-dialog criteria rest on the author's browser session plus
   those E2E assertions — **and there is no QA case for any of them** (`V-34-J`). Given `F-1` was
   exactly this class of defect surviving exactly this class of evidence, **this is the thinnest
   place in slice 1's acceptance and I am naming it rather than passing it.**
7. **Whether `93fa417`'s renamed test still asserts what its old name claimed.** I established the
   commit changed no behaviour, which is what block 2 turned on. I did not separately re-verify
   `V-30-B` (the reflection door), which remains the Architect's open question.

---

## 13. The one thing Nabil should know

**The code is in good shape. The record of it is not.**

Nothing I attacked in the engineering broke. The strictest permission in the system refused every
role I threw at it and went red the moment I moved the grant. The audit trail cannot be written to
through any route the host maps. The guard that pinned nothing now pins something, and I watched it
fail. Three independent E2E runs, a database I built myself, every backend gate figure reproduced.

**What did not hold up is the bookkeeping — for the third week running.** D-119 was written to correct
a board that "records a state not derived from anything," and it lapsed 48 points on a documentation
commit, dated it two days wrong, and included two stories whose verdicts postdate that commit by
three days. It was right that the numbers were not true. It was not right about why.

**The pattern is now four weeks old and it has a shape:** every one of these errors was a claim about
an artefact that the claimant did not open — the board about itself, D-119 about git, this brief
about the board. **The engineering in this repository is disciplined precisely because it refuses
that move**: `EndpointPermissionCoverageTests` reads the routes the host built rather than the
`Endpoint.cs` files, because D-067 proved the source text lies. That discipline has never been
applied to the project's own records.

**And the one place the two meet is where slice 1 is weakest.** Every screen criterion in this slice
has been discharged by a build session driving a browser once and looking at a screenshot — and
`F-1` is a real defect in shipped code that survived exactly that. **No frontend story has ever had a
QA case** (`V-34-J`). That is not a gap in someone's diligence; it is a gap in the process, and it is
the one I would fix before slice 2 adds more screens.
