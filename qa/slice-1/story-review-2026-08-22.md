# Slice-1 story review — the committed fifteen, read against `spec.md` and the code

**Verifier, 2026-08-22.** Fresh session. **I did not write these stories and I have not fixed
anything** — `agents.md` §7: the Verifier reports, the BA fixes, the Scrum Master routes.

**Why this exists.** `meetings/2026-08-22-sprint-1-execution.md` §9 makes one thing the gate on the
sprint moving to code: *"the rewritten stories are verified by someone who did not write them."*
Every story in `stories/slice-1-foundation/` was rewritten on 2026-08-22 and several claims were
found false rather than merely stale. KAFF-106 is first in the build order. Backend builds what the
story says.

---

## What I ran, and what I read

| | |
|---|---|
| `dotnet build KaffErp.sln --configuration Release` | **0 errors, 0 warnings**, exit 0 |
| `dotnet test tests\Domain.Tests` | **75 / 75**, exit 0 |
| `scripts/check-citations.ps1` | **287 checked · 0 broken · 101 legacy · exit 1** |
| Api suite | **not run.** It needs the PostgreSQL container; nothing in this review turns on it |

`Kaff.Api` was not running, so no `MSB3021` and no stale binaries. The brief's stated state —
build 0/0, Domain 75/75, checker 287/0/101 — **is accurate.** Api 43/43 I did not re-measure.

Every claim below was checked against the file it names, today. No finding here is repeated from
another document without re-reading its source.

---

## The committed fifteen, and one disagreement about what they are

**The fifteen**, from the build order in `meetings/2026-08-22-sprint-1-execution.md` §6 and §9:

> KAFF-106 → 100 → 108 → 109 → 113/114 → 110/111/112 → 101a/102/103/105a → 116/118

**`stories/backlog.md` does not record a sprint commitment at all.** It lists 26 `Ready` stories and
92 points for slice 1 and says choosing from that set *"is the scope commit"*. The only record of the
fifteen is in `meetings/`. That is finding **V-14** below, and it matters because `stories/` is the
directory Backend reads.

---

## Findings, ranked by whether Backend building it as written produces a defect

| # | Story | Class | Defect if built as written? |
|---|---|---|---|
| **V-01** | 100 · 101a · 102 · 118 | FALSE | **Yes — permanent.** Unbackfillable audit gap |
| **V-02** | 101a | UNCITED / open question | **Yes — security.** A staff-origin session for a portal user |
| **V-03** | 105a | FALSE (internal contradiction) | **Yes.** Every new user dead-ends at sign-in |
| **V-04** | 105a | FALSE | **Yes.** Project-scoped portal rows in a company-wide list |
| **V-05** | 106 · 108 | STALE | **Yes — coverage.** The HR-department refusal loses its create-path criterion |
| **V-06** | 109 | FALSE (build order) | **Likely.** The shared revocation gets written twice or untested |
| **V-07** | 100 · 103 · 106 · 109 · 110 · 112 · 113 · 114 · 102 | FALSE | **Yes, at the seam.** Backend emits keys the SPA cannot resolve |
| **V-08** | 100 · 103 · 106 · 101a | UNFALSIFIABLE in scope | No defect; the criteria cannot be executed |
| **V-09** | 106 · 113 | FALSE (citation) | No code defect |
| **V-10** | 106 · 112 · 113 | SM-31 breach | No code defect; the checker exits 1 |
| **V-11** | 100 · 101a | FALSE | Minor, but it can provoke one |
| **V-12** | 102 · 113 | UNCITED, unwaived | No defect today; an invented rule shipping unnoticed |
| **V-13** | 118 | UNFALSIFIABLE (ambiguous count) | No defect; the case cannot be written |
| **V-14** | KAFF-107 / `backlog.md` | STALE | **Possible.** Backend may build a story that is not in the sprint |
| **V-15** | 112 | STALE | No defect |
| **V-16** | 114 · 100 | UNFALSIFIABLE as phrased | No defect; restate |
| **V-17** | seven stories | count discrepancy | No defect; Nabil's and the Architect's |

---

### V-01 · FALSE — four committed stories require audit records the one sanctioned mechanism cannot write, and the gap cannot be backfilled

**Stories:** KAFF-100, KAFF-101a, KAFF-102, KAFF-118. **Backend building it as written: yes, and the
damage is permanent.**

**The exact text.**

* KAFF-100, *Permissions, money, audit, i18n*: *"one `Created` record on `User`. **`ActorUserId` is
  the newly created Owner itself**"* — asserted again in **AC-100-A**.
* KAFF-101a, same section: *"a successful sign-in writes a record… A **failed** sign-in writes one
  too, with the attempted username and no actor id… **A lockout writes its own record**"* — asserted
  in **AC-101a-C** and **AC-101a-G**.
* KAFF-102, same section: *"a record naming the user, the time and the request path."*
* KAFF-118 rule 2: *"It is **one mechanism** in `Domain`/`Infrastructure`, not per-feature code. **No
  slice-1 handler constructs an `AuditRecord`.**"*

**The evidence.** The one mechanism writes only for tracked entity entries in `Added`, `Modified` or
`Deleted`, and returns nothing for a `Modified` entry whose changed-property list is empty
[Verified: 2026-08-22 @ `src/Infrastructure/Persistence/Interceptors/AuditSaveChangesInterceptor.cs`
-> `WriteAuditRecords`]. Against that:

* **Sign-out touches no entity.** No record. KAFF-102's requirement has no mechanism.
* **A clean successful sign-in touches no entity** — `RecordSuccessfulSignIn` writes
  `FailedSignInAttempts = 0` and `LockedOutUntil = null`, which are already those values
  [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `RecordSuccessfulSignIn`]. Zero changed
  properties, no record.
* **A failed sign-in against an unknown username has no `User` row to modify at all**, so *"the
  attempted username and no actor id"* is unreachable through the interceptor by construction.
* **KAFF-100's actor is null.** On an anonymous request `ICurrentUser.UserId` returns null and
  `DisplayName` returns the literal `"anonymous"` [Verified: 2026-08-22 @
  `src/Api/Identity/HttpContextCurrentUser.cs` -> `UserId`], and the interceptor passes
  `_currentUser` straight into the record. **The first row in the trail would name nobody — which is
  the precise outcome D-051 (Q31) rejected the seed for.**

**Why this is top of the list and not a scheduling note.** `AuditRecord` is append-only and
trigger-protected; KAFF-116 exists in slice 1 for exactly this reason — *"a field never written
cannot be backfilled."* Backend has two ways out of the contradiction and both are defects: build a
per-feature audit path (breaks KAFF-118 rule 2 and `CLAUDE.md`'s *"one mechanism in `Domain/`"*), or
ship the endpoints without the records (a permanent hole in the sign-in trail).

**This is not a new discovery — it is N-06 / SM-14 from the 2026-08-21 refinement, still open, and
the 2026-08-22 rewrite did not touch it.** No committed story names a mechanism. **Architect.**

---

### V-02 · UNCITED / open question — KAFF-101a rule 16 is undecided, and the story says so itself

**Backend building it as written: yes, security.**

**The exact text**, rule 16: *"A `Role.Client` credential **authenticates**, and reaches only
`PortalRead` / `PortalApprove` — **but the portal is a separate host, so a client's session is
refused at the staff origin.** Where that boundary is enforced… is **N7** and lands in slice 8."*
And, in the story's own *Questions* section: *"**N9** … rule 16 still says a `Role.Client` credential
'authenticates' here, which was written before D-051 (Q33) made the portal a separate host… **Should
be answered before this story is built**, since it decides one of its rules."*

**Why it is a defect and not a note.** Built as written, the staff sign-in endpoint mints a valid
`__Host-kaff-auth` cookie [Verified: 2026-08-22 @ `src/Api/Options/JwtOptions.cs` -> `CookieName`] on
the staff origin for a portal user. It reaches nothing **today** only because of what the catalogue
happens to contain — `Role.Client` holds `PortalRead` and `PortalApprove` and nothing else
[Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.PortalRead`].
That is a property of the current data, not a control. `spec.md` §12 is absolute about the client
boundary, and D-035 is the record of it nearly being crossed once already.

**The Definition of Ready line this fails** is the last one: *"Not `BLOCKED` on an open question."*
A story whose own text says a rule *"should be answered before this story is built"* is BLOCKED on
that rule. KAFF-101a is eighth in the build order and it is committed. **Architect (N9), then BA.**

---

### V-03 · FALSE — KAFF-105a rule 3 and AC-105a-C require opposite behaviour from the same endpoint

**Backend building it as written: yes.**

* **Rule 3:** *"It **reports** whether the signed-in user must still change a temporary password,
  because the shell has to send them to that screen and nowhere else."*
* **AC-105a-C:** *"When the shell calls this endpoint / Then it is **refused** with
  `errors.auth.password_change_required`."*

The endpoint cannot both report the flag and refuse the call. **Rule 3 is the one that is right, and
the frontend already depends on it:** `Session` carries `mustChangePassword` and `AuthService` holds
nothing else that could carry it [Verified: 2026-08-22 @
`src/Web/src/app/core/auth/auth.service.ts` -> `mustChangePassword`]. `ux/slice-1-flows.md` S-004
routes on it.

**Consequence of building AC-105a-C.** `GET /api/auth/me` is the only thing that can tell the SPA
anybody is signed in — the story says so itself, and D-050 is why. Refusing it during a forced change
leaves the shell with no profile; KAFF-105a rule 8 and **AC-105a-D** then make "no profile" mean
"signed out", so **every newly created user is bounced back to the sign-in screen in a loop and can
never reach the change-password screen.** That takes out KAFF-103 and KAFF-106's demo path with it.

**Note the collision is three-way, so fixing one story is not enough.** KAFF-103 **AC-103-B** lists
`GET /api/auth/me` among the endpoints that must be refused, and KAFF-100 **AC-100-F** requires
*"`GET /api/auth/me` **reports** that no password change is required."* One of these has to give and
it is a business-shaped choice about the forced-change gate, not a drafting fix. **BA, with the
Architect.**

---

### V-04 · FALSE — KAFF-105a rule 4 and AC-105a-F disagree about which permissions the payload carries

**Backend building it as written: yes.**

* **Rule 4:** *"**Company-wide** permissions are returned as a flat set."*
* **AC-105a-F:** *"Given I am `Role.Client` / When I call this endpoint / Then only `PortalRead` and
  `PortalApprove` are returned."*

`PortalRead` and `PortalApprove` are **`ProjectScoped`**, not `CompanyWide`
[Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.PortalRead`].
Build rule 4 and a portal client gets an **empty** set, so AC-105a-F fails. Build AC-105a-F and the
payload carries project-scoped rows in a company-wide list — for the one role `spec.md` §12 draws a
hard boundary around, and on the endpoint whose own *Money* bullet warns that *"a convenient 'project
value' here would reach the portal (§12)… in one step."*

The per-project permission list is **KAFF-105b**, deferred. The clean reading is that a portal
client's two rows belong there and AC-105a-F is in the wrong story — but that is the BA's call, not
mine. **BA.**

---

### V-05 · STALE — the HR-department refusal lost its create-path criterion when KAFF-107 was folded away

**Backend building it as written: yes, as a coverage hole.**

The 2026-08-21 refinement §A4 folded KAFF-107 into KAFF-106 and KAFF-108 and made the fold
**conditional**: *"the fold is conditional on **both** 106 and 108 naming the bare-department
mechanism explicitly… **Action SM-21**."*

**KAFF-108 holds up its half** — rule 3 and **AC-108-D** ("HR stays in HR").

**KAFF-106 does the opposite.** Its *Not in this story* section reads: *"The HR-role/HR-department
constraint, which has its own story because it closes a specific hole (**KAFF-107**)."* KAFF-107 is
not in the build order and is not committed. So:

* No committed acceptance criterion exercises `errors.identity.hr_role_requires_hr_department` **on
  the create path**. KAFF-106's criteria run A–J and none of them does.
* KAFF-106's i18n bullet does not list the key either.

The domain itself is safe — the refusal is in `ValidateDepartment`, reached from both `Create` and
`MoveToDepartment` [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `ValidateDepartment`], and
it is pinned [Verified: 2026-08-22 @ `tests/Domain.Tests/CatalogueCompletenessTests.cs` ->
`An_hr_user_cannot_be_placed_in_another_department`]. **What is missing is the endpoint-level refusal
test the fold was made conditional on**, and the story text actively tells Backend it is somebody
else's job. That is F-21's shape one level down and it is the shape A4 said it was watching for.

**Two smaller errors in the same area, both in KAFF-108's i18n bullet:** it calls
`errors.identity.hr_role_requires_hr_department` *"the key KAFF-107 adds"*. KAFF-107 adds nothing —
it is not committed — and the key **already exists in both catalogues**
[Verified: 2026-08-22 @ `src/Web/public/locales/ar.json` ->
`errors.identity.hr_role_requires_hr_department`]. **BA.**

---

### V-06 · FALSE — KAFF-109's declared dependencies contradict the committed build order

**Backend building it as written: likely.**

KAFF-109's header: *"**Depends on:** KAFF-106, KAFF-113, KAFF-111."* `stories/backlog.md` agrees.
The build order puts **109 fourth, before 113/114 and before 110/111/112**.

Two consequences, both real:

1. **AC-109-A, AC-109-B, AC-109-C and AC-109-D all revoke assignments that only KAFF-113 can
   create.** Fixtures can fake the rows, but the criteria as written are not executable against the
   stories committed *ahead* of them, which is the SM-10 rule.
2. **Rule 4 says the revocation mechanism is written once and lives in `Domain/`** — *"This is the
   same revocation mechanism KAFF-111 uses for deactivation."* Building 109 four stories before 111
   is the standard way that becomes two mechanisms.

One of the two artefacts is wrong. **Scrum Master** decides which; **BA** then makes the story match.

---

### V-07 · FALSE — the i18n key divergence (N-05 / SM-15) is still live, and it is inside the committed set

**Backend building it as written: yes, at the API/SPA seam.**

`ux/rtl-and-i18n.md` hard rule 1: *"`errors.*` keys are **owned by the backend**… When a backend error
key appears, add it to both catalogues; **do not rename it to fit a UI convention.**"* So whatever the
story says, Backend emits — and the SPA resolves nothing.

| Story says | UX flows / convention say | Checked |
|---|---|---|
| KAFF-100: `errors.setup.already_initialised` (rule i18n, **AC-100-B**) | `errors.setup.already_completed` [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `errors.setup.already_completed`] | **neither key exists in either catalogue** |
| KAFF-103: `auth.change_password.title` · `.current` · `.new` · `.confirm` · `.submit` · `.required_notice` | `auth.password.title` · `auth.field.current_password` · `auth.field.new_password` · `auth.field.confirm_password` · `action.save` · `auth.password.must_change` [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `auth.password.title`] | **completely disjoint** |
| KAFF-110: `users.deactivate` · `users.deactivate.confirm` | `users.action.deactivate` · `users.confirm.deactivate.title` [Verified: 2026-08-22 @ `ux/rtl-and-i18n.md` -> `users.confirm.deactivate.title`] | convention breach |
| KAFF-112: `users.reactivate` · `users.reactivate.confirm` | `users.action.reactivate` · `users.confirm.reactivate.title` | convention breach |
| KAFF-113: `assignments.add` | `assignments.action.assign` [Verified: 2026-08-22 @ `ux/slice-1-flows.md` -> `assignments.action.assign`] | convention breach |
| KAFF-114: `assignments.revoke` · `assignments.revoke.confirm` | `assignments.action.revoke` · `assignments.confirm.revoke.title` | convention breach |
| KAFF-109: `users.role.change_revokes_assignments_notice` | `users.confirm.change_role.revokes` | convention breach |
| KAFF-102: `auth.logout` · `auth.logout.confirmed` | `auth.action.sign_out` shape | convention breach |

**`errors.setup.already_initialised` is the one that is a defect rather than untidiness** — it is a
server-returned `messageKey`, Backend owns it, and the SPA is being built against the other spelling.
Two names for one server refusal is F-08's exact shape, and the refinement logged it as **N-05** with
action **SM-15** on 2026-08-21. **It survived the 2026-08-22 rewrite untouched.** The cost is a text
edit now and a migration later. **BA, then Frontend/UX to confirm the winner.**

---

### V-08 · UNFALSIFIABLE in scope — three committed stories carry an RTL criterion nothing in the sprint can render

**Backend building it as written: no defect — the criteria simply cannot pass or fail.**

* **AC-100-I** — *"Given the setup screen at 390px in Arabic…"*
* **AC-103-I** — *"Given the screen at 390px in Arabic…"*
* **AC-106-J** — *"Given the user form at 390px in Arabic…"*
* **AC-101a-M** — *"Given a completed sign-in **in the browser**…"*

No Frontend story is committed — KAFF-101b, KAFF-105b and KAFF-115 are all deferred — and `src/Web`
contains exactly one feature component, the slice-0 status page. Backend never touches `src/Web/`
(`agents.md` §4). `meetings/2026-08-21-sprint-1-refinement.md` states outright that *"under this
sprint's scope no screen is committed at all"* — **that statement and these four criteria cannot both
be true.**

SM-10 rescoped four criteria of exactly this shape on 2026-08-21 (AC-110-E, AC-111-C, AC-113-D,
AC-118-H) and left these behind. **A criterion that cannot be run reports safety that does not
exist** — `agents.md` §3c. Either move them with the screen stories, or the sprint scope has to admit
a frontend half. **BA, then Scrum Master.**

*(`AC-105a-B`'s trailing clause — *"and `localStorage` and `sessionStorage` remain empty"* — and
`AC-102-A`'s *"in a browser"* have the same problem in smaller form. The rest of both criteria is
executable server-side.)*

---

### V-09 · FALSE — two committed stories attribute a quotation to a file that does not contain it

**Backend building it as written: no code defect. It is a citation defect of the exact class SM-29
exists for.**

KAFF-106 and KAFF-113 both say the register *"warned in terms not to close Q42 'by handing HR the
Owner's user list'"*, and both cite `questions-for-karim.md` at line 131.

* Line 131 of `stories/questions-for-karim.md` is the sentence *"was the one row this register existed
  to surface, and a reader who remembers it needs to see where it"*.
* **The quoted sentence is not in that file at all.** The register's Q42 row makes the point in
  different words [Verified: 2026-08-22 @ `stories/questions-for-karim.md` -> `Q42`].
* The quotation is real, and its actual homes are `decisions.md` D-055 §2 and the catalogue comment
  [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.UserRead`].

The **substance** — that `UserRead` is a permission and the endpoint's projection is the control — is
correct and is repeated in three committed stories, so nothing is at risk in the build. The citation
is wrong twice over. **BA.**

---

### V-10 · SM-31 breach — three committed stories still carry line-number citations, and the checker exits 1

**Backend building it as written: no code defect. But the gate is red.**

`scripts/check-citations.ps1` today: **287 checked, 0 broken, 101 legacy, exit 1.** By file:

| File | Legacy citations |
|---|---|
| `meetings/2026-08-21-sprint-1-refinement.md` | 76 |
| `qa/slice-1/test-cases.md` | 12 |
| `stories/questions-for-karim.md` | 6 |
| `qa/questions.md` | 4 |
| **`stories/slice-1-foundation/KAFF-106-…`** | **1** |
| **`stories/slice-1-foundation/KAFF-112-…`** | **1** |
| **`stories/slice-1-foundation/KAFF-113-…`** | **1** |

**`meetings/2026-08-22-sprint-1-execution.md` §8 says `stories/`, `qa/`, `process/`, `proposals/` and
`decisions.md` are at zero. That is false today for `stories/` and `qa/`.** It may have been true when
written; it is not now, and §8 is the paragraph a later session will trust.

There is a second, quieter residue the checker cannot see because it is not inside backticks:
**KAFF-118 `AC-118-H` cites KAFF-105a by line range**, and `stories/ac-id-map.md` cites four
historical AC labels by bare line offsets. The KAFF-118 one happens to resolve correctly today; that
is luck, and SM-31's whole argument is that a line number always resolves. **BA and QA** (already
assigned in the execution log §9; recorded here because three of them are in committed stories).

---

### V-11 · FALSE — both anonymous-endpoint claims are wrong, and they contradict each other

* **KAFF-101a**, *Permissions*: *"the endpoint is anonymous — **it is the only one in the system that
  is.**"*
* **KAFF-100**, *Permissions*: *"anonymous — **the second and last endpoint in the system that is**
  (the first is sign-in, KAFF-101a)."*

`/api/health` is anonymous and shipped in slice 0 [Verified: 2026-08-22 @
`src/Api/Features/Health/GetHealth/Endpoint.cs` -> `AllowAnonymous`]. So KAFF-101a's claim is false,
KAFF-100's count is off by one, and the two stories disagree with each other on top of that.

**Why this is more than pedantry.** `/api/health` is what `/run-kaff-erp`'s smoke check calls. A
Backend agent reading *"the only one in the system"* as a rule has a documented reason to lock the
health endpoint down, and the shared definition of "the stack is up" stops working. **BA.**

---

### V-12 · UNCITED and unwaived — three rules of the waived shape that nobody waived

The Architect's signed waiver covers Q45–Q51 across seven stories (see V-17). These three are the
same shape and are **not marked, not waived and not on the register**:

* **KAFF-102 rule 7** — *"Signing out when already signed out is not an error worth a refusal."*
  Source column: *"§9 — no source requires a refusal, and inventing one would be inventing a rule."*
  **This is Q49's shape (a rule read out of a silence) answering Q51's question.** Q51 asks, in
  Karim's words, whether four idempotent acts *"should be refused with an error, or quietly do
  nothing"* — and KAFF-102 rule 7 is a fifth act of the same kind, answered the **opposite** way from
  its four siblings, off the register entirely.
* **KAFF-113 rule 8** — *"An inactive user is not assignable, and an assignment does not resurrect
  one."* Source: *"§9 · KAFF-110"*. §9 says nothing about it, and a story is not a source. It is not
  enforced in the domain either — `ProjectAssignment.Create` checks role and level and never touches
  `IsActive` [Verified: 2026-08-22 @ `src/Domain/Identity/ProjectAssignment.cs` -> `Create`] — so this
  is pure new handler work resting on nothing.
* **KAFF-113 rule 9** — *"A user may hold only one active assignment per project."* Source: *"slice 0
  `ProjectAssignment`"*, i.e. the partial unique index [Verified: 2026-08-22 @
  `src/Infrastructure/Persistence/Configurations/IdentityConfigurations.cs` -> `HasFilter`]. That is
  Q51's shape exactly — a rule read off an implementation.

*(Two more cite only other stories rather than a source: **KAFF-110 rule 10** ("KAFF-101a · KAFF-104")
and **KAFF-118 rule 7** ("KAFF-110"). Lower stakes, same Definition-of-Ready line.)*

**They are all probably right.** *Probably right* is what the register is for. **BA to register;
Nabil to decide whether the waiver stretches or the questions get asked.**

---

### V-13 · UNFALSIFIABLE — AC-118-A and AC-118-C cannot both be satisfied by one run

**AC-118-A:** *"When a user is created, moved between departments, deactivated and reactivated; **and
an assignment is created and revoked** / Then **each produces exactly one audit record**."*

**AC-118-C:** *"Given a user with three active project assignments / When the Owner deactivates them /
Then **four** records exist."*

If the assignment in AC-118-A's sequence is still active when the deactivation runs, the deactivation
writes two records and AC-118-A fails. If it is revoked first, AC-118-A passes — but nothing in the
criterion says which, so **the same test can be written two ways and only one of them can fail.**
This is N-09's count disagreement one level down, inside one story. **BA: state the order.**

---

### V-14 · STALE — KAFF-107 was folded out of the sprint and `stories/` does not know

`meetings/2026-08-21-sprint-1-refinement.md` §A4: *"**KAFF-107 folds into KAFF-106 and KAFF-108.**
16 stories / 59 points → 15 stories / 57 points."* It is absent from the build order.

But `stories/slice-1-foundation/KAFF-107-hr-role-is-bound-to-the-hr-department.md` still reads
**`**Status:** Ready`**, 2 points, with its own acceptance criteria — and `stories/backlog.md` still
carries it as a separate `Ready` row worth 2 points inside a 26-story, 92-point slice. **Nothing under
`stories/` records the fold, and `stories/` is what Backend reads.**

`stories/README.md` lists five statuses and none of them means "folded". That is a real gap in the
vocabulary, not just a missed edit. **BA, and the status vocabulary is the Scrum Master's.**

---

### V-15 · STALE — KAFF-112 says a correction is owed that has already been made

KAFF-112, in its Q50 note: *"the same correction is owed to its row in `questions-for-karim.md`"*
(cited by line number — see V-10).

The register's Q50 row already carries it: *"**⚠️ The mechanism half is CLOSED, 2026-08-22 — that
citation was doubly stale.**"* [Verified: 2026-08-22 @ `stories/questions-for-karim.md` -> `Q50`].
The claim was true when written and is now false. **BA.**

---

### V-16 · UNFALSIFIABLE as phrased — two criteria that cannot be executed in the form they are written

* **AC-114-F** — *"Given a revoked assignment / When a delete is attempted against it through the API
  / Then **there is no endpoint that performs one**."* The `When` presupposes a call that cannot be
  made. As written it is a route-table assertion wearing Given/When/Then, and a lazy reading passes it
  by doing nothing. Restate it as *"no route in the application maps DELETE against
  `ProjectAssignment`"*, which fails the day one appears.
* **AC-100-D** — *"When the codebase is searched for a setup flag… / Then none exists."* This one
  **can** fail and is legal, but it is a review step, not an xUnit or Playwright case, and QA should
  be told which it is before it is recorded as executed.

**BA to restate AC-114-F; QA to classify AC-100-D.**

---

### V-17 · The waiver is recorded in **seven** committed stories, not six

**Checked, per the brief.** The signed waiver text — *"I accept the six stories containing uncited
rules…"* — appears, correctly worded and with the date, in:

**KAFF-100 · KAFF-101a · KAFF-103 · KAFF-106 · KAFF-110 · KAFF-112 · KAFF-114** — seven stories,
twelve marked rules, all seven committed.

`decisions.md` D-055 §4 says six. **No seventh joined quietly** — KAFF-106 raises the discrepancy in
its own text and routes it to the Scrum Master rather than resolving it, which is the correct
behaviour and I am confirming it rather than reporting it. What I am reporting is the substance: **the
Architect signed for six and seven stories are being built on the signature.** That is Nabil's and
the Architect's to close, not the BA's.

Every waived rule I checked is genuinely uncited and every waiver names its open question
(Q45–Q51). **No waiver is missing from a story that carries a marked rule.**

---

## Stories that are clean, by name

These are safe to build on the strength of their own text. Each of them makes claims about the code
and **every claim I checked in them is true today.**

| Story | What I verified in it |
|---|---|
| **KAFF-116** — audit records how access was granted | All four claims true: the `ProjectAccessPath` members and their values; `Granted` derived from `Path` [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `record ProjectAccess`]; all four policy branches set it; and `AuditRecord`'s property list is exactly as the story states, with **no** grant-path field [Verified: 2026-08-22 @ `src/Domain/Auditing/AuditRecord.cs` -> `class AuditRecord`]. The `audit.grant.*` keys are correctly stated as absent. **No findings.** |
| **KAFF-111** — deactivation revokes assignments | The two hard claims are true: `Revoke` keeps the row, and `Deactivate` / `Reactivate` do not call it. AC-111-C was correctly rescoped off the deferred team panel. Only the shared i18n naming issue of V-07 touches it. |
| **KAFF-114** — revoke an assignment | Both waived rules are properly marked and registered; the soft-close, the computed `IsActive`, the per-request assignment lookup and the active-rows-only unique index all check out. Only **V-16**'s wording. |
| **KAFF-110** — deactivate a user | Every claim true, including the subject read filtering on `IsActive` before any role is considered. The withdrawn mandatory-reason rule is handled exactly the way `stories/README.md` asks. Only **V-07**. |
| **KAFF-108** — move between departments | The corrected `AC-108-A` is now right, `AC-108-G`'s retirement-and-reissue follows the ID scheme, and the `SiteExpenseConfirm` grant is as described. Only the two small i18n errors noted inside **V-05**. |

**KAFF-109, KAFF-113, KAFF-118 and KAFF-100** are substantively sound — their code claims are all
true, including KAFF-109 rule 7's careful enumeration of what `User` does and does not have — and
carry only the findings listed against them above.

---

## AC identifier integrity — clean

Checked mechanically against `stories/ac-id-map.md`, which `process/agile.md` makes the authority.

* **229 distinct AC IDs in `stories/slice-1-foundation/`, 229 in the map, and the two sets are
  identical.** No ID in a story is missing from the map; no ID in the map is missing from a story.
* **Every story's letters run contiguously from `A`** with no gaps — so nothing was inserted and
  nothing renumbered its neighbours.
* **The one retirement is correct.** `AC-108-B2` is struck through with its date and reason and
  reissued as `AC-108-G`, which sits between `B` and `C` in reading order exactly as
  `stories/README.md` rule 3 requires. **Do not tidy it.**
* **All 175 distinct AC IDs cited across `qa/` resolve** to the map. No drifted citations remain.

---

## Permission claims — clean

Every permission named in a committed story was checked against the catalogue.

* **No committed story treats `ProjectManage` as covering creation.** KAFF-113's table states the
  three-way split correctly — `ProjectCreate` company-wide for opening, `ProjectManage`
  project-scoped for editing, `ProjectFinancialsEdit` project-scoped and money-touching for the
  contract's tax settings — and says in terms not to merge them back.
* **The three new rows are described accurately** wherever they appear, scopes and grants included.
* **`UserRead`** is correctly stated as company-wide, Owner and HR, names and roles only, in all three
  committed stories that mention it.
* **`ProjectTeamRead` (F-30, open) is named in four stories — KAFF-107, KAFF-113, KAFF-115 and
  KAFF-105b — and only KAFF-113 is committed.** It is absent from `src/` and from `tests/`, confirmed
  today. **KAFF-113 is buildable without it**: it states the absence explicitly, says *"no slice-1
  test may assert it"*, and its endpoint takes a project id from wherever the caller got it. The
  other three are deferred (and KAFF-107 is folded — V-14). **F-30 does not block the sprint.**

---

## Corrections to the brief I was given

`agents.md` principle 7. Two things in the brief did not survive contact with the files:

1. **"Note `ProjectTeamRead` is named in four stories… confirm which four and whether the stories are
   buildable without it."** True, but the framing implies the four are at risk. **Only one of the four
   is committed**, and it is the one that handles the absence correctly. The other three are out of
   the sprint.
2. **"Six stories carry Nabil's signed waiver."** The waiver text quoted in the brief attributes it to
   Nabil; in `decisions.md` D-055 §4 and in all seven stories it is signed *"the Architect"*. And it
   is **seven** stories, not six — see V-17.

Everything else in the brief checked out, including the build, Domain and checker numbers.

---

## What I did not do

* **I fixed nothing and edited no story.** `agents.md` §7.
* **I did not run the Api suite** (43/43 unverified by me) or the E2E suite — neither bears on a story
  review, and the Api suite needs the PostgreSQL container up.
* **I did not review the twelve uncommitted stories** — KAFF-101b, 104, 105b, 107, 115, 117, 119, 120,
  121, 122, 123, 124 — except where a committed story depends on one. KAFF-107 appears here only
  because of V-05 and V-14.
* **I did not check QA's test cases for coverage**, only that their AC IDs resolve.
* **I resolved no ambiguity and invented no rule.** V-03's three-way collision, V-12's three uncited
  rules and V-02's N9 are raised, not answered.
