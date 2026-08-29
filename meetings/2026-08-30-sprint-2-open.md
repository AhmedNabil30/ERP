# Sprint 2 — open · 2026-08-30

**Scrum Master.** Sprint 1 closed on 2026-08-27. **Sprint 2 has been executing for two days with no
recorded scope at all** — six commits landed against a board that still described the tree of three
days ago. This file opens the sprint against what it actually contains, and proposes what it should
contain next. **It does not lock the scope.** That is Nabil's, and *"do your magic and move the
board"* is a scheduling instruction, not a ruling on scope — the same distinction the sprint-1 close
drew about *"close sprint 1"*, and for the same reason.

**Everything below was re-established against the files today.** No claim is inherited from
`meetings/2026-08-27-sprint-1-close.md`, from `decisions.md`, or from the brief that produced this
session. Where a document said something that turned out to be stale, the correction is recorded
rather than made silently.

---

## 1. Gates, re-run rather than restated

At `c156613`, this session, through `/run-kaff-erp`:

| Gate | Result |
|---|---|
| `dotnet build KaffErp.sln -c Release --no-incremental` | **Build succeeded, 0 Warning(s), 0 Error(s)** |
| `Kaff.Domain.Tests.exe` | **107 / 107**, 0 failed |
| `Kaff.Api.Tests.exe` | **227 / 227**, 0 failed |
| `dotnet format --verify-no-changes` | exit **0** |
| `scripts/check-citations.ps1` | **935 checked, 0 broken, 0 legacy** |
| `git status` | clean · nothing unpushed |

Every figure in the brief that opened this session is confirmed. **This is the one thing on the board
that needed no correction**, and it is worth saying plainly, because the rest of this file is about
where green and verified came apart again.

---

## 2. The board as it now stands

### 2.1 What happened since the close, none of it recorded until now

| Commit | What | Entry | Independently verified |
|---|---|---|---|
| `f2b995b` | **KAFF-101b** — the staff sign-in screen. The first thing in this project that ever rendered | D-091 | **No** |
| `332c160` | **KAFF-103's screen**, and `AC-101b-F` closed with it | D-092 | **No** |
| `4e688c5` | D-092 updated with the live drive | D-092 | n/a — documentation |
| `4885edf` | **`V-27-A`** — the guard list a regression cannot edit into agreement | D-093 | **No** |
| `c01959b` | **`V-27-B`** — *"the exemption marker is now unforgeable"* | D-094 | **No** |
| `ca4db6c` | **`V-27-C`** — a role that is not a role, and two predicates that failed open | D-095 | **No** |
| `45a939d` | A malformed body is **400 in every environment**, not 500 in one | **none exists** | **No** |

**Six product commits, no independent pass on any of them.**

The four backend tickets are tech debt Nabil pulled forward from *"before slice 3"* on the grounds
that **"we do not wait to fix foundational security flaws."** That reasoning is right and the work
looks good. It is also the reason the next section is unwelcome.

### 2.2 Five acceptances have lapsed — the second consecutive time

The 2026-08-27 Verifier pass accepted **KAFF-109, 105a, 102, 101a and 103** at HEAD `559ac45`,
explicitly refusing to inherit any prior verdict. That pass closed the sprint's central finding: 19
points that did not stand, now standing.

**Then four commits moved code those stories' own criteria assert.** Established from
`git show --stat` and from the diffs, not from any report's summary of itself:

| Story | What moved after `559ac45` | Commit |
|---|---|---|
| **KAFF-109** | `User.ValidateDepartment` — this story's own path — gained an `Enum.IsDefined` refusal returning `IdentityErrors.UnknownRole`. **`V-27-C` was recorded against this story** | `ca4db6c` |
| **KAFF-101a** | `StaffSessionRules.MayHoldStaffSession` — **this story's own role bar** — went from a deny-list to an allow-list | `ca4db6c` |
| **KAFF-105a** | Its gate: `LiveSession.Marker` rewritten, `LiveSession.IsApplied` added; and `MayHoldStaffSession` inside `ResolveAsync` | `c01959b`, `ca4db6c` |
| **KAFF-102** | Sign-out resolves through `ResolveAsync`, which calls the changed `MayHoldStaffSession` | `ca4db6c` |
| **KAFF-103** | Behind `RequireLiveSession`, whose metadata production changed; and `ResolveAsync` as above | `c01959b`, `ca4db6c` |

`meetings/2026-08-27-sprint-1-retrospective.md` §3, change 3, in its own words: *"When a later commit
touches that story's files, the acceptance lapses and must say so out loud."* **This is the first time
that rule has been applied rather than argued about, and it costs 19 points on the day it fires.**
Applying it only when it is cheap would make it decoration.

**KAFF-101a and KAFF-103 have now had this happen twice** — `f807364` the first time, `ca4db6c` the
second. That is not bad luck. It is what happens when a shared gate keeps being improved underneath
stories that were certified against it, and it is an argument for verifying the *mechanism* on a
cadence rather than re-certifying five stories every time the mechanism moves.

### 2.3 The line I drew, and why it is not "every story lapses"

`ca4db6c` also changed **`PermissionEvaluator.Evaluate`** — the gate *every* permission-checked
endpoint runs through — and `45a939d` changed the request pipeline for *every* JSON-binding endpoint.
Read maximally, the lapse rule voids the entire sprint, which would make it useless.

**The line: a story lapses where a commit changed behaviour that story's own criteria assert. It is
carried with a named exposure where a commit changed a shared mechanism whose behaviour for that
story is unchanged — and "unchanged" has to be *pinned*, not asserted.**

Here it is pinned. Both role predicates are now allow-lists of exactly the roles that exist, and that
equivalence is a test rather than an argument
[Verified: 2026-08-30 @ `tests/Domain.Tests/UserTests.cs` -> `The_two_role_doors_admit_exactly_these`].
The deny-list restored goes red (D-095's `MUT-H` row). So for the nine roles that exist, the gate's
behaviour is measured to be identical, and the change is confined to inputs no accepted criterion
names.

**This is deliberately close to the argument the retrospective's change 4 forbids** — *"this cannot
have changed, therefore no test is possible"* — and it is on the right side of it only because the
equivalence was fault-injected by someone else and re-run by me today. If that test did not exist,
the honest answer would be that every story lapses.

**Carried with the exposure named:** KAFF-106 (`User.Create` → `ValidateDepartment`), KAFF-108
(`MoveToDepartment` → `ValidateDepartment`), and every permission-gated story via
`PermissionEvaluator.Evaluate` and via `Program.cs`.

### 2.4 Two stories were in the wrong bucket, and I put them there

**KAFF-106 and KAFF-110 have never been accepted.** The sprint-1 close puts both in *"built and
verified with a criterion still held"*. The status-sweep table I dictated to the cheap model said
`ACCEPTED` for both. The model transcribed exactly what it was given; **the defect is mine**, and it
is precisely the failure §M predicts when a brief carries a wrong fact into mechanical work — the
mechanical agent has no standing to doubt it.

Caught on reading the sweep's diff, corrected in `aa8a9ca`, and the correction is written into both
status lines rather than only here. `AC-106-H` and `AC-110-D` have **still never been examined by any
Verifier pass**: the 2026-08-27 pass covered five stories and neither of these is one of them.

**This is the third Scrum Master brief in two sprints to carry a wrong fact.** The previous two were
caught by the agents receiving them, under `agents.md` principle 7. This one was not — a small model
executing a dictated table has nothing to check the table against, which is exactly what makes the
split cheap. **The reviewing step is not optional; it is the other half of the split.**

### 2.5 Sprint 1, final

| Bucket | Stories | Pts |
|---|---|---:|
| **Accepted, standing** | 116, 113, 100, 111, 112, 114 | **20** |
| **Accepted 2026-08-27, then the code moved underneath the verdict** | 109, 105a, 102, 101a, 103 | **19** |
| **Built and verified with a criterion still held — never accepted** | 106, 110 | **10** |
| **Unbuilt** | 118 | **3** |
| | **15** | **57** |

**Nabil's lock stands: 15 stories / 57 points.** Nothing was added, cut or re-estimated here.

**20 of 57 points stand.** That is *fewer* than the 25 the close recorded, and the arithmetic is not a
regression in the code — it is 106 and 110 being put back in the bucket they were always in, minus
nothing, plus 19 points that were briefly recovered by the 2026-08-27 pass and lapsed three days
later.

**No story in this sprint has passed Nabil's acceptance gate.** `process/agile.md` §4 makes acceptance
Nabil running the demo script. **Two screens now exist, so for the first time there is something to
run one against** — that is new since the close and it is the most significant thing on this board.

---

## 3. The verification decision — the Verifier runs first, and it is already running

**Decision: no new story starts until the Verifier reports.** Dispatched this session, on the
strongest model per §M, against all six commits. Six reasons, in the order they weigh:

**1. The claim in `c01959b` is the same species that was already defeated once, in the same file.**
D-089 claimed `RequireLiveSession` applied its checks *"by construction"* because *"nothing else adds
that metadata."* The last Verifier attacked exactly that sentence and **defeated it in one line** —
`Marker.Instance` was `internal`, every feature slice compiles into `Kaff.Api`, and the suite reported
**215/215** against a route that applied nothing. D-094 now says the marker is **"unforgeable"** and
that `RequireLiveSession` is *"the only expression in the language"* that produces it. It may well be
right — `Marker` is a private nested type today
[Verified: 2026-08-30 @ `src/Api/Authorization/LiveSession.cs` -> `IsApplied`]. **But a strictly
stronger version of a claim that was false last week is the last claim on this board that should be
taken on trust.**

**2. The Verifier has caught a real defect on every run** (`agents.md` §M). The base rate is 1.0 and
these six commits are the least-examined code in the repository.

**3. The author was unreliable in a specific way that matters.** The Backend agent that wrote three of
the four fixes **died six times mid-work, once mid-restore**, and the orchestrating session verified
only `45a939d` from scratch. A death mid-restore is exactly how a mutation survives into a commit —
D-094 itself records building an unpaid probe endpoint and then deleting it. Whether it is *actually*
gone is a claim about the tree, not about the entry. I checked that one instance: `VerifierProbe` is
absent from `src/`, and the only surviving mention of `Marker.Instance` anywhere under `src/` is prose
in a doc comment [Verified: 2026-08-30 — searched every file under `src/`]. **One check is not a
clearance**, and I am not the right session to do the rest.

**4. `45a939d` is larger than its ticket and carries no `decisions.md` entry.** It changes
`RouteHandlerOptions.ThrowOnBadRequest` globally and adds a `StatusCodeSelector`
[Verified: 2026-08-30 @ `src/Api/Program.cs` -> `AddProblemDetails`], which reaches **every
JSON-binding endpoint**, not the `/api/setup` where the symptom was found. An undocumented global
change to the request pipeline is exactly what a fresh session should look at, and `CLAUDE.md`'s
Definition of Done requires the entry.

**5. Building on it compounds it.** KAFF-105b and KAFF-115 both sit behind `PermissionEvaluator` and
`LiveSession`. A defect found after they ship costs their rebuild as well; found now it costs one
session. **Two screens exist and both are unverified** — the frontend conventions D-091 set are being
copied forward already (D-092 says so explicitly), so a convention defect propagates rather than
sitting still.

**6. The five lapsed stories need re-establishing anyway**, and doing that in the same pass as the
four fixes that lapsed them is one session instead of two.

**The argument against, stated fairly.** Verifying costs a session, the suites are green, and the
fixes are visibly careful — D-093, D-094 and D-095 each record watching the mutation fail before
trusting the fix, which is the practice the retrospective credited. **That argument fails on the
retrospective's own finding:** a green suite is exactly what `V-27-B` produced against a route that
applied no checks. *Green is not accepted*, and the fixes' own quality is the strongest reason to
believe the residual risk sits in the mechanisms rather than in the routes — which is where both
`V-27-A` and `V-27-B` already were.

---

## 4. Sprint 2's scope — a proposal, not a lock

### 4.1 What the sprint already contains, delivered before it was opened

Two screens (`f2b995b`, `332c160`) and four tech-debt tickets (`4885edf`, `c01959b`, `ca4db6c`,
`45a939d`). **No points are claimed for any of it here.** The tech-debt tickets are not stories and
have no estimate; KAFF-101b is a 3-point story whose `AC-101b-A` and `AC-101b-D` are **not** closed.

### 4.2 The frontend path, and the problem in it

The path named is **KAFF-105b** (which is to close `AC-101b-A`, and is the second of D-091's two
conditions for turning `path: '**'` from a silent redirect into a 404), then **KAFF-115** (HR's
Project Team surface, `AC-101b-D`). The order is right — 115 reads the payload 105b defines, and 115's
own `Depends on` says so.

**The problem is that KAFF-105b does not build a shell.** All ten of its acceptance criteria —
`AC-105b-A` through `AC-105b-J` — are criteria about the `GET /api/auth/me` **payload**. Not one of
them renders anything. `AC-101b-A` requires *"they arrive at the staff shell, and the shell's contents
come from `GET /api/auth/me`"*; KAFF-105b builds the second half of that sentence and none of the
first.

**So D-091 deferred a criterion onto a story that cannot discharge it.** The deferral was honest and
correctly reasoned — the shell's contents genuinely do depend on 105b's payload — but the arithmetic
does not close.

**Three readings, and I am not picking one:**

1. **KAFF-105b grows a frontend half and is re-estimated.** `process/agile.md` puts a story spanning
   backend and frontend at **8**, not 3. Sprint 2's frontend path is then 8 + 3 = 11 points before
   anything else.
2. **A staff-shell story does not exist and the BA writes one.** Cleanest against the criteria as
   written; it adds a story Nabil has not scoped.
3. **`AC-101b-A` is re-deferred** to whichever story genuinely builds the shell, and KAFF-105b ships
   as the 3-point API story it is.

**Reading 3 is the one I would take if it were mine**, because it keeps KAFF-105b's estimate honest
and stops a criterion drifting a second time — but **it is a scope decision and it is Nabil's.**
Recorded as a question in §6.

**The same shape, smaller, in KAFF-115.** `AC-115-J` is *"Arabic, RTL, at mobile width"* — a screen
criterion — so KAFF-115 already spans backend and frontend at **3 points**. On `process/agile.md`'s
own scale that is light. Not re-estimated here; raised at refinement, which is where estimates move.

### 4.3 What I would put in sprint 2, and the size

| # | Item | Pts | Why here |
|---|---|---:|---|
| 0 | **Verifier pass over the six commits** | — | §3. Not a story; the sprint's first item and a gate on the rest. **Running now.** |
| 1 | **KAFF-105b**, API half as written | **3** | Defines the payload everything downstream reads. Backend, and the machine is serial anyway |
| 2 | **The staff shell** — story unwritten | **?** | §4.2. Blocked on Nabil's reading, and on the BA writing it if reading 2 or 3 wins |
| 3 | **KAFF-115** | **3**, likely light | Depends on 1. `AC-101b-D` lands with it |
| 4 | **`AC-106-H`, `AC-110-D`** | — | Deferrals that are now spent and have never been examined. Folded into item 0 |

**On size: 105b + 115 as written is 6 points, and that is not the real number.** With the shell and
two screens it is realistically 13–16, which `process/agile.md` calls *"too big — split it. A 13 is a
story nobody understands yet."* **My recommendation is to lock items 0, 1 and 3 and hold the shell
until §6's question is answered** — not because the shell is unimportant, but because it is the one
piece whose size nobody can state today, and committing to an unknown is how a sprint stops meaning
anything.

**A story that fails the Definition of Ready does not enter the sprint.** KAFF-105b and KAFF-115 both
pass it on their own terms. **The shell has no story, so it cannot pass anything** — it is not
`BLOCKED`, it is unwritten.

---

## 5. Routing — every finding to an owner

`agents.md` §3b: *a defect recorded in a register and assigned to nobody is a defect nobody is
fixing.* Nothing below is closed by being written here.

### 5.1 New this session

| Id | What | Owner |
|---|---|---|
| **The six unverified commits** | §2.1 | **Verifier** — dispatched this session, `qa/slice-1/verification-2026-08-30.md` |
| **`45a939d` has no `decisions.md` entry** | It changes `ThrowOnBadRequest` globally and adds a `StatusCodeSelector` [Verified: 2026-08-30 @ `src/Api/Program.cs` -> `AddProblemDetails`] — structural, and `CLAUDE.md`'s Definition of Done requires the entry. D-091 raised the symptom on one endpoint; the fix is repo-wide and undocumented | **Backend** |
| **`AC-101b-A` deferred onto a story that cannot discharge it** | §4.2 | **BA** to record where the criterion now lives; **Nabil** owns which reading |
| **KAFF-115 may be under-estimated** | `AC-115-J` makes it span both halves at 3 points | **Scrum Master** — refinement, before it is pulled |
| **A dictated sweep table carried a wrong fact and nothing caught it** | §2.4 | **Scrum Master** — recorded as D-096 |

### 5.2 `W-5` — opened with the Architect, and it grew

Framework-produced `400` / `404` / `415` carry no `messageKey`; only `401` and `403` are filled. No
criterion requires it, so it is a scope question and not a defect — **and it is now a bigger one than
when it was logged.** Before `45a939d`, the malformed-body `400` was a Development-only shape that
never reached staging (staging got a 500). **After it, a `messageKey`-less problem response is what
every JSON-binding endpoint returns, in every environment, for a malformed body.** The shape moved
from an artefact of one environment to a documented contract of the whole API.

D-095 records Backend deliberately declining to assert a status code in its new test *because* `W-5`
is open, so the question is now blocking test-writing as well as answering.

**Owner: Architect.** The question to answer: does a framework-produced refusal owe a `messageKey`,
given that `CLAUDE.md` forbids hardcoded user-facing strings and the frontend renders `messageKey` and
nothing else? Note the constraint that makes it non-trivial: the frontend's refusal region renders one
key from the server (D-091), so a response with no key has nothing to render.

### 5.3 `V-27-C`'s fail-closed question — Backend raised two, and here is where each sits

**Backend did raise it, in D-095's `🟡` section, and answered the design half itself:**

> *"What counts as a valid role is `spec.md` §9's to say. This refuses a value that names **no** role;
> it does not add, remove or reinterpret one. If Karim adds a tenth, `MayHoldStaffSession` must be
> edited to admit it — deliberately, which is the point."*

**That is the fail-closed choice, it is correct, and it needs no ruling.** An allow-list that must be
edited to admit a new role is the whole reason the deny-lists were inverted. It is recorded here so
nobody reads the friction as an oversight and "fixes" it back into a deny-list. **Owner: closed, no
action.**

**The half that is open is the data question.** D-095: *"The existing `role = '99'` row on the
Verifier's database is not migrated … **Architect / Nabil** if any environment that matters turns out
to hold one."* No data fix shipped, correctly — the row was created by a sweep on a disposable local
database, and a migration that rewrites a role is a business decision about what that account should
become.

**Owner: Architect**, to answer whether any environment that matters can hold one, and **Nabil** if
the answer is yes. **The deadline is not slice 3** — this is cheap now and stops being cheap the
moment a real user row exists.

### 5.4 `SM-32` — half closed, and the open half is the larger one

**Closed:** the writing-convention half. The Scrum Master's 2026-08-27 ruling was carried out —
`process/agile.md` now says *"a reference without `@` carries no verification claim and must not be
written where one is needed. Write the claim as a citation, or do not make it"*
[Verified: 2026-08-30 @ `process/agile.md` -> the SM-31 section]. The `@` boundary stays; widening the
regex would flag SM-31's own writing convention and every meeting file, and **a checker that cries
wolf gets muted, which is D-046's green light by another name.**

**Open, and it is the bigger half: the checker walks `*.md` and not source.** Every
`<c>File.cs</c> -> <c>Identifier</c>` in an XML doc comment is verified by nothing — and this codebase
is full of them deliberately, because the reasoning lives beside the code. `LiveSession.cs` alone
carries several. **935 is not repo coverage and must not be read as it.**

It bit twice in one week: the same non-existent identifier appeared in three places and the checker
saw one. It nearly bit a third time — D-095 records **not renaming** `ValidateDepartment`, because
four historical citations in `meetings/`, `qa/`, `proposals/` and `stories/` would have broken, and
**the checker caught all four.** That is the mechanism working exactly as intended, on the half it
covers.

**Owner: Backend.** Extending to `*.cs` needs the writing convention restated for XML docs first, so
it is not a regex change. **P2** — nothing today depends on it, and the cost of leaving it is a wrong
citation surviving in a doc comment.

### 5.5 QA cases the 2026-08-27 pass left failing or unexecutable — all four re-checked today

| Case | State, re-read today | Owner |
|---|---|---|
| **`TC-1-042`** | **Still fails against correct code.** Still cites the retired `AC-105a-F` and still asserts *"exactly `PortalRead` and `PortalApprove` are returned"* from `GET /api/auth/me` [Verified: 2026-08-30 @ `qa/slice-1/test-cases.md` -> `TC-1-042`]. `V-26-B`'s fix makes it **more** wrong, not less: the route now refuses `Role.Client` outright, so the case cannot be relocked to `AC-105a-H` at all — the behaviour it describes no longer exists at that route | **QA**, copied to **BA**. Unmoved since 2026-08-22, when `ac-id-map.md` instructed the rewrite |
| **`TC-1-079`** | **Still cannot be executed.** Still carries `PENDING Q27 (residual)` [Verified: 2026-08-30 @ `qa/slice-1/test-cases.md` -> `TC-1-079`], and the register still says Q27 is closed — so the case is pending on something no open entry names. It asks whether a role may be changed **to** `Role.Client` or `Role.Subcontractor` at all, which is adjacent to the open `Role.Subcontractor` conversion question | **BA** — number the residual or retire the marker. **Not mine to answer**, and note it is adjacent to one of Nabil's four |
| **`TC-1-027`'s Api half** | `AC-103-H` — Domain half passes, Api half has no test. And the criterion has now moved level, like `AC-105a-H` | **QA → Backend**, P2 |
| **`TC-1-046`** | `/api/auth/me` carries no money — satisfied structurally, by inspection, not by an assertion | **QA**, P2 |

**All four were routed in the sprint-1 close three days ago and none has moved.** So have `TC-1-120`
and `TC-1-094` (both **QA → Backend**, P2), and `W-2`, `W-3`, `W-4` and `W-10`. **A register row is
not a fix**, and this is the second consecutive document to say so about the same rows — which is
itself the finding. **These are QA's and they need a QA session, not another routing table.**

### 5.6 Carried, still open, unchanged since the close

`V-26-E` (**QA** rewrites the case; **Nabil** owns the half about what an accepted criterion is proved
against) · `V-26-G` (**QA**, copied **BA**) · `AC-105a-H` and `AC-103-H` moved level (**BA** to record
where each is now proved) · staging behind two Oracle firewalls, **Nabil's to open** — the stack runs
there and the pipeline cannot see it, which are different claims.

---

## 6. Questions standing with Nabil — none answered here, and none may be

`agents.md` §3b: **never resolved by any agent, not by consensus and not to unblock a sprint.**
Consensus among agents is the most confident possible way to be wrong.

**The four from the close, re-checked today, and none has moved:**

1. **`KAFF-118`'s cut from a locked sprint.** Unbuilt, 3 points. It depends on KAFF-119, deferred out
   of sprint 1, so its client-registration half cannot complete as written whatever is ruled. The
   standing proposal — cut it as a story, keep rule 2 as an acceptance check — is sound and is **still
   not the Scrum Master's to take.** *"Move the board"* is not a ruling on scope.
2. **Converting a user to `Role.Subcontractor` — refuse, or succeed and clear the credential?** D-088
   built the reversible half: `ChangeRole` refuses while a credential is stored. A later ruling can
   relax it and nothing is lost; clearing a credential the Owner did not ask to clear cannot be undone
   by a ruling that arrives afterwards. `spec.md` §9's *"record only, no login"* satisfies both
   readings, which is why it is a question. **`TC-1-079` is stuck adjacent to this.**
3. **The reach of a `mustChangePassword` session** beyond `/api/auth/me` and change-password.
   `KAFF-101a` rule 8, `AC-101a-F` and `AC-103-B` all assert the strict reading and all three cite
   **D-049 ruling 4, which names no endpoint.** `AC-101a-F` is covered by no test. **This one changed
   shape this week and is now cheaper to answer**: `AC-101b-F` was closed by `332c160`, the guard was
   observed redirecting on a cold reload, and D-092 is explicit that the guard is *convenience, not
   security* — the server is what refuses. So there is now a screen to demonstrate either reading
   against, which there was not on 2026-08-27.
4. **Q54 / N11 — retention, now that the audit column records a real end user's address.** Q54 itself
   **is answered** — Nabil ruled it on 2026-08-24, D-072 §3: PostgreSQL table partitioning by month on
   `audit_records` at slice 9. What is open is **N11, the consequence**: converting a *populated*,
   append-only, trigger-protected table into a partitioned one is a new table plus a data migration
   plus a swap. **The deadline is not slice 9; it is before the first real rows exist, and slice 3 is
   when money history starts.** D-079 does not reopen Q54 — before the trusted-proxy work the column
   held a Docker bridge address, which is not personal data by any reading; from the next deploy it
   holds a real end user's address, which is. Same ruling, same mechanism, **a subject that now
   exists.** **Owner: Architect**, by Nabil's own instruction.

   **And the register row is still stale.** The `Q54` row in `stories/questions-for-karim.md` still
   reads *"Not settled by any agent"* [Verified: 2026-08-30]. **This was routed to the BA in the
   sprint-1 close and has not been done.** It is bookkeeping against a ruling Nabil already made, not
   a resolution — and while it stands, the register says an answered question is open.

**New, and it is a scope question rather than a business rule:**

5. **Which reading closes `AC-101b-A`?** §4.2 — grow KAFF-105b to 8, write a shell story, or re-defer
   the criterion. **The scope lock is Nabil's** and this decides what sprint 2 actually is.

**The four smaller ones already routed to Nabil, confirmed still open:** whether a no-op sign-out
should leave a trace and naming whom (D-085) · whether the inactive account's generic `401` is what he
wants (D-084 §🟡 2) · whether the Owner may change his own role (D-082 §5) · D-089's two changes to
what an accepted criterion is proved against.

**And Q28 stands, unmoved** — the lockout is per account and trivially exploitable: anyone who knows a
site engineer's username can hold him out fifteen minutes at a time, from anywhere, indefinitely, and
the suite does it in five HTTP requests. The register records that **Karim was not shown this
consequence** when he ruled [Verified: 2026-08-30 @ `stories/questions-for-karim.md` -> the `Q28`
row].

---

## 7. Process, corrected today at a cost

Both in `c156613`. Both are the retrospective's §1 pattern rather than new failure modes.

**1. Disjoint file ownership is not sufficient — at most one agent on this machine at a time.**
`process/agile.md` ceremony 2, amended. On 2026-08-29 Frontend and Backend ran concurrently with
**genuinely disjoint** ownership — `src/Web/` against `src/Infrastructure/` and `tests/Api.Tests/` —
satisfying `agents.md` principle 3 throughout, and collided anyway on **port 5080** (which
`src/Web/proxy.conf.json` hardcodes, so both need the same one) and on **`Kaff.Domain.dll` /
`Kaff.Infrastructure.dll`**, which a running API holds open against the other's build. One agent
killed the other's API host by PID. Two stalls. **The machine is the shared resource, not the files.**
Principle 3 stops agents overwriting each other's work; this stops them being unable to build at all.
Both must hold.

**2. A check that cannot detect what it checks for.** `/run-kaff-erp`'s stop-the-API gotcha said to
run `Get-Process -Name Kaff.Api` — **and that does not match the process the same file's §1 tells you
to start.** `dotnet run --project ...` executes the app through `dotnet.exe`, so the name is `dotnet`
and the check throws *"Cannot find a process with the name"* while the DLLs are held open. It matches
only the apphost form, which does exist [Verified: 2026-08-30 — `Kaff.Api.exe` is present in
`src/Api/bin/Release/net10.0/`] but is not what the skill tells you to run. Replaced with a
`Win32_Process` command-line match, which catches both launch forms.

**That second one is `meetings/2026-08-27-sprint-1-retrospective.md` §1 exactly, three days after it
was written: a passing check and an absent check produced identical output.** The check reported *"not
running"* for the very thing it was checking for.

---

## 8. What this session did **not** do — as a count, not as prose

The retrospective's change 2, applied to a Scrum Master session: a tool that says *"N checked"* must
also say *"M unparsed"*.

| Skipped | Count | Why, and what it would cost to close |
|---|---|---|
| **Refinement, as a ceremony** | 1 | `process/agile.md` ceremony 1 requires walking every story aloud and asking each agent *"what do you not know?"*. **This was not that.** No agent was asked; the bucket-three questions in §6 are inherited plus one new scope question I found by reading. **A real refinement is owed before sprint 2's stories are pulled**, and §4.2's problem is exactly the kind it exists to surface |
| **Stories not re-read in full** | **9 of 27** | 100, 104, 107, 117, 119–124 were not opened this session. Their status lines were left untouched deliberately, but "untouched" is not "confirmed current" |
| **`decisions.md` read from D-091** | ~7,200 lines unread | Read D-091–D-095 in full and grepped for the rest. **A ruling before D-091 that this board contradicts would not have been noticed** |
| **The E2E suite** | not run | Unchanged since 2026-08-26. `TC-1-223`'s browser half is still unexercised, and **now that two screens exist there is something for it to drive** — which makes its absence newly expensive rather than newly cheap |
| **`/run-kaff-erp smoke`** | not run | I ran the build, both suites, `dotnet format` and the citation checker. **I did not start the stack**, so nothing in this file is claimed about a running application. The Verifier's brief asks for it |
| **The two screens, looked at** | 0 | I did not render either screen or take a screenshot. Everything I say about them comes from D-091, D-092 and the commits — **which is precisely why §3 sends the Verifier** |
| **`qa/slice-1/test-cases.md`** | 4 of ~260 cases checked | Only `TC-1-042` and `TC-1-079` re-read directly, plus two register rows. The other cases' states are inherited from the 2026-08-27 pass |
| **Points not re-estimated** | 2 | KAFF-105b and KAFF-115 are both flagged as likely light in §4.2 and **neither was changed.** Estimates move at refinement, with the agent who will build it in the room |
| **The Verifier's result** | pending | Dispatched, not returned. **Everything in §2.5 is provisional against it** — if it rejects one of the six commits, the board moves again |

**Nine skipped items.** None of them is a silent gap: where a skip touches a claim, it is named beside
that claim too.

---

## 9. The one thing Nabil should know

**The board was three days behind the work, and moving it forward cost 19 points rather than
recovering them.**

The 2026-08-27 Verifier pass did what the close asked — it reached all five stories and accepted all
five, closing the sprint's central finding. **Two days later four commits moved the code underneath
all five verdicts again**, and this is the second time for KAFF-101a and KAFF-103. The fixes are good;
`V-27-A`, `V-27-B` and `V-27-C` were real, and pulling them forward on *"we do not wait to fix
foundational security flaws"* was right.

**The pattern is not that the work is bad. It is that this project keeps improving shared mechanisms
underneath stories that were certified against them, and no verdict survives it.** Certifying five
stories every time `LiveSession` or the role doors move is not sustainable at slice 1 and will be
impossible at slice 5. **The thing worth verifying on a cadence is the mechanism, not the five stories
that sit behind it** — and that is a process change I am flagging rather than making, because it
changes what `ACCEPTED` means and that is not mine to redefine.

**And the sharper thing.** Both of the last pass's findings — `V-27-A` and `V-27-B` — were **not
defects in the code. They were defects in the machinery built to tell us the code is safe.** A green
light whose greenness was not evidence. The fixes for both now make claims of exactly the same
strength as the claims they replaced: *"a required list a regression cannot edit"*, *"unforgeable"*.
**They are probably right this time.** They were probably right last time too, and the difference
between probably-right and verified is one session — which is running now, and which is why nothing
new is being built until it reports.
