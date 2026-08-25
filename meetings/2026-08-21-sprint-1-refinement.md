# Sprint 1 refinement — Foundation

**Date:** 2026-08-21
**Run by:** Scrum Master
**Present:** BA · UX · QA · (Architect, Backend, Frontend — represented by their open actions, see §6)
**Slice:** 1 — auth, roles, assignment, audit, Client master
**Gate (`agents.md`):** permission tests pass
**Scope reviewed:** 27 rows / 26 live stories / 92 points, **zero BLOCKED on entry**
**Outcome:** **16 stories, 59 points committed. 10 stories, 33 points deferred.** Nine new bucket-three
questions, one of which touches the slice gate.

This is the second refinement of sprint 1. The first (2026-08-20) ran against a backlog where the
committable set chose itself by elimination — eleven stories were BLOCKED and the question was which of
the remainder could move. That is no longer true. **Karim answered fifteen questions across two rounds
(D-049, D-051), the Architect and Nabil answered four more (D-050, D-051), and slice 1 entered this
meeting with nothing blocked.** Choosing a scope is therefore a real decision for the first time, and
`process/agile.md` is explicit that making it is what ends the meeting.

---

## 1. Committed scope, and why

**Committed — 16 stories, 59 points.**

| ID | Title | Pts | Why it is in |
|---|---|---:|---|
| KAFF-100 | Bootstrap the first Owner | 5 | Nothing else has an actor without it (D-051 Q31) |
| KAFF-101a | Sign in, `HttpOnly` session cookie | 5 | No permission can be tested without an authenticated identity. **Carries the stamp comparison (D-051 N5)** |
| KAFF-102 | Sign out | 2 | The other end of the session the audit trail must bound |
| KAFF-103 | Change the temporary password on first sign-in | 5 | D-049 ruling 4's non-repudiation half; 100 and 106 are incomplete without it |
| KAFF-105a | `GET /api/auth/me` — identity and permissions | 2 | The frontend cannot know anyone is signed in without it (D-050) |
| KAFF-106 | Owner creates a user with a role and department | 5 | **The gate.** Every permission test needs a subject |
| KAFF-107 | HR is bound to the HR department | 2 | D-044 ruling 2's second mechanism. **But see §4 — this story is now nearly empty** |
| KAFF-108 | Move a user between departments | 3 | Department is an independent grant axis (D-048) |
| KAFF-109 | Change a user's role | 5 | **The gate**, and the one whose rules inverted (D-051 Q27) |
| KAFF-110 | Deactivate a user | 5 | **The gate.** D-048 made revocation instant; this is what exercises it |
| KAFF-111 | Deactivation revokes assignments | 3 | D-049 ruling 5 — today a returner comes back with everything |
| KAFF-112 | Reactivate a user, who comes back with nothing | 3 | The other half of ruling 5, plus the stamp rotation N5 flags as missing |
| KAFF-113 | Assign a user to a project | 5 | **The gate.** The second half of *"permission = role × assignment"* |
| KAFF-114 | Revoke a project assignment | 3 | **The gate.** Access must end without losing who could act when |
| KAFF-116 | Every audit record says how the actor was granted access | 3 | **Cannot be backfilled.** See below |
| KAFF-118 | Every state change in slice 1 writes an audit record | 3 | The audit mechanism itself, once, in `Domain/` |

**Deferred — 10 stories, 33 points.** KAFF-101b (3), KAFF-104 (5), KAFF-105b (3), KAFF-115 (3),
KAFF-117 (5), KAFF-119 (5), KAFF-120 (2), KAFF-121 (3), KAFF-123 (2), KAFF-124 (2).

### The reasoning, stated plainly because this is a real decision now

**The gate is *permission tests pass*, and the gate chose the scope.** Ten of the sixteen —
106, 107, 108, 109, 110, 111, 112, 113, 114, and 118 behind them — are the permission spine. The other
six are the minimum that makes the spine testable and auditable: an Owner to act (100), a session to
act in (101a, 102, 103), an identity endpoint to read it back (105a), and the grant-path column (116).

**Why the Client master defers as a block.** KAFF-119, 120, 121, 123, 124 are 14 points and carry no
permission content beyond `ClientManage`, which the catalogue already grants and which no gate test
needs. Two independent reasons reinforce it: **KAFF-121's headline behaviour has no domain path** —
`Client` has no name setter and no primary-phone setter; `SetContactDetails` covers only alternate phone, email, address and notes (`src/Domain/MasterData/Client.cs` -> `SetContactDetails`) — and **KAFF-119 waits on
N6**, the client-code generator's concurrency contract, which is the Architect's and is not settled.
Committing them would mean committing work whose first step is an unwritten decision.

**Why 104 defers.** The password-reset link (D-051 Q38) requires a delivery channel that does not
exist. Nothing in the pinned stack sends an SMS or a WhatsApp message. That is **already registered as
N7** — see §6.

**Why 115, 105b and 117 defer.** All three depend on `ProjectTeamRead`, a permission that exists in
five story files and **nowhere in `src/`** (verified: zero matches across the source tree). Building
the permission is legitimate slice-1 work; building it *and* two screens *and* the audit-read screen
on top of the spine is not one sprint. 117 additionally waits on 116 and 118 landing first.

**KAFF-116 is committed regardless of what else is cut**, and the case has not weakened since the last
meeting. `audit_records` is append-only and trigger-protected, so the grant-path column **cannot be
backfilled** — every row written before it lands is permanently missing the field. Verified today:
`AuditRecord.cs` -> `GrantPath` and a repo-wide search for
`AccessPath` / `GrantedVia` / `HowGranted` returns nothing. Karim's rulings made it
sharper, not softer — there are now **two** roles that reach a project with no assignment row, and
without the column *"Owner, globally"* and *"assigned on 3 June"* are identical in the record.

**A second, unrecorded half of the same gap, found today.** @ `PermissionEvaluator.cs` -> `ProjectAccess` defines the record, and @ `ProjectAccessPolicy.cs` -> `GlobalReachAsync` serves the Owner **and** HR through
one branch. So the policy that admits the request **structurally cannot tell `OwnerGlobal` from
`HrGlobal`**, which is exactly what KAFF-116 rule 6 requires it to supply. F-07 records the missing
column; nobody had recorded the missing distinction upstream of it. **Action SM-13.**

---

## 2. "What do you not know?" — the three buckets

The BA, UX and QA agents were each given a narrow brief naming the same sixteen stories, and each was
required to cite the file and line it verified against. What follows is their answers, sorted.

### Bucket 1 — answered by `spec.md`. Cite it and move on.

| Raised by | Question | Answer |
|---|---|---|
| BA | Roles, departments, and what "permission = role × assignment" binds | §9 |
| BA | Does seniority live on the user or the assignment | §9 amendment 7 |
| BA | Owner and HR reach without an assignment row | §9 amendments 1-4 |
| BA | Does a role change revoke assignments | §9 ⚠️ SUPERSEDED block |
| BA | Password and lockout rules; session length; onboarding; leavers | §9 |
| BA | Is a subcontractor ever a login | §9 — no |
| QA | Who reads the audit trail | §9 — the Owner, alone |

**Note what bucket 1 looked like this time.** UX reported it as *"almost empty, and that is the honest
finding"* — `spec.md` has no section on users, sessions or passwords at all, and everything the design
needed came from a ruling instead. That is a consequence of D-047's amendment-block convention working:
the rulings reached `spec.md` and are now citable there rather than only in `decisions.md`.

### Bucket 2 — answered by `decisions.md`. Link the D-number and move on.

| Raised by | Question | Answer |
|---|---|---|
| BA · UX | How the first Owner comes to exist | D-051 Q31 |
| BA · UX · QA | Role change vs. existing assignments | **D-051 Q27**, reversing D-049 ruling 6 |
| UX · QA | What HR sees of a project, and in what shape | D-051 Q32 — a separate surface, not a filtered view |
| QA | Can sign-out kill another device | **D-051 N5** — no, and the limit is accepted not hidden |
| QA | Who the bootstrap audit actor is | D-051 Q31 — the new Owner; a seeded account would name nobody |
| BA · QA | Where the stamp comparison belongs | D-051 N5 — *"It belongs to KAFF-101a and the story must say so"* |
| QA | Does reactivation rotate the stamp | D-051 N5 — it should, and does not. KAFF-112 work |
| UX | Where the access token lives | D-050 |
| BA | Are company-wide permissions revalidated | D-048 — yes, per request, from the database |
| BA · UX | `UserManage` scope; HR as a role; HR's global reach; seniority | D-044 rulings 1, 2, 3, 5 |

**Two bucket-two items are load-bearing because a current document contradicts them.** See §4 —
`TC-1-019` and `TC-1-003` each assert the opposite of a ruling. Both are corrections QA makes by
citing the D-number, not questions.

### Bucket 3 — answered by nobody. **This is the output of the meeting.**

Nine new items, plus two already registered and still unasked. **None was resolved in the room.**
Numbering is proposed; the register is the BA's file and SM-8 assigns the numbers.

| Proposed | Question, as Nabil should ask it | Touches | Raised by |
|---|---|---|---|
| **Q34** *(registered, unasked)* | *"When somebody on site spends money, who signs it off? Is it always the office, or is there anyone on the site who can?"* | **the slice-1 gate** — see §3 | QA-1, third sprint unasked |
| **Q41** *(registered)* | May a role be changed **to** `Role.Client` or `Role.Subcontractor`? | KAFF-109 rule 11 — `TC-1-079` is unwritable | BA · QA |
| **Q42** | *"HR puts people onto projects. To do that HR has to pick the person from a list. What is HR allowed to see about the people in that list — just their name and job, or more? And should the list show everybody, or only people who could work on a site?"* | **S-010's user picker.** Not KAFF-113's criteria — see §5 | UX Q-UX-16 |
| **Q43** | *"When HR picks a project to put someone on, is it enough to show the name — or should the reference code be there too, in case two projects are called the same thing?"* Same question covers the team-size count. | **Already answered without a ruling** in four stories — see §4 | UX Q-UX-22 |
| **Q44** | *"You set your own password on the setup screen. Should the system still make you change it immediately afterwards?"* | KAFF-100 AC6, which has no test case | QA-4 · UX Q-UX-17 |
| **Q45** | Are `admin` / `root` / `kaff` reserved usernames? A named blocklist appears in an AC with no source. | KAFF-100 AC7 | BA |
| **Q46** | Does the first Owner carry **no department**? D-051 Q31 never mentions one, §9 does not exclude the Owner, and `User.ValidateDepartment` permits it. | KAFF-100 | BA |
| **Q47** | Should a wrong password, an unknown username and a **locked** account really be indistinguishable? It trades away *"your account is locked"*, which cuts against ruling 3's own stated reason. | KAFF-101a AC2 | BA |
| **Q48** | Must the change-password endpoint demand the current password? The story's source column reads *"§9 — the same reasoning"*; §9 says nothing. | KAFF-103 AC4 | BA |
| **Q49** | May the last engineer be revoked off a project, leaving it unstaffed? Source column: *"§9 — absence noted deliberately"*. | KAFF-114 | BA |
| **Q50** | On reactivation, is the credential **cleared** (leaving a null-password account) or **replaced in the same request**? D-049 ruling 5 says only *"gets a new password"*. | KAFF-112 AC4/AC5 | BA |

**And one that is not a question but belongs in bucket three by shape.** The BA found **four refusals
in committed stories derived from slice-0 *code* rather than from a ruling** — deactivate-twice
(`KAFF-110:25`), reactivate-an-active-user (`KAFF-112:34`), revoke-an-already-revoked-assignment
(`KAFF-114:18`) and case-insensitive username uniqueness (`KAFF-106:31`). `agents.md` forbids deriving
expected results from the implementation; the same prohibition applies to deriving a rule from it. They
are almost certainly right, and *almost certainly right* is what bucket three is for.

**Nothing above was resolved here.** `process/agile.md`: consensus among agents is the most confident
possible way to be wrong.

---

## 3. F-04 — the only live defect, and it touches the gate

**Verified today at the mechanism, not inferred.**

* @ `PermissionCatalogue.cs` -> `SiteExpenseConfirm` grants to `[finance, operationsAdmin]`
* @ `PermissionCatalogue.cs` shows `operationsAdmin` sets `Department` and `OperationsSubDepartment`, **and names no `Role`**
* @ `PermissionEvaluator.cs` -> `Matches` — a null role skips the role check entirely
* @ `User.cs` -> `ValidateDepartment` refuses Client/Subcontractor with a department and HR outside HR, and **does nothing about a `Role.SiteEngineer` in Operations/Administrative**
* `spec.md` §8: *"Site financial expenses are entered by Finance or Admin, **not the engineer**."*
* No test asserts this cell. A search for `SiteExpenseConfirm` under `tests/` returns two **comments**.

**"Not reachable until slice 6" is only half true, and it is the wrong half.** The Api half needs an
endpoint that does not exist. **The Domain half is reachable today** — `PermissionEvaluator.Evaluate`
is pure and synchronous by design, and `Domain.Tests/PermissionEvaluatorTests.cs` already exercises it.
Constructing a `PermissionSubject(SiteEngineer, Operations, Administrative)` and asserting the decision
is a ten-line test that goes red on today's `main`. That test is already written: **`TC-1-215`, P1**.

**So the sprint has a P1 permission case that fails against code it is shipping, and its gate is
"permission tests pass".** I am not going to paper over that, and I am also not going to fix it, because
**the fix shape cannot be chosen without Q34, which has now gone unasked for three sprints:**

* **If a site engineer may never confirm** (the reading §8 supports) — one line: either a grant naming
  both the department and a role, or the same invariant `ValidateDepartment` already applies to HR.
  The catalogue fix is preferable: it closes the cell for **every** role, not only the engineer, and
  leaves `PhotoPublish`'s department-only shape alone, which §9 genuinely supports.
* **If an engineer sitting in Admin may confirm** — the catalogue is already correct, `spec.md` §8 gets
  a 📌 AMENDMENT block per D-047, and `TC-1-215`'s expected result inverts. The fix is documentation.

**Either way it is one line. The blocker is the question, not the work** — which is what makes leaving
Q34 unasked the actual defect here.

**The Scrum Master's ruling on the gate, and it is a scoping ruling, not a business one:** the gate
cannot be declared passed with `TC-1-215` red. It can be declared *"passed with one accepted,
documented exception"* — **and that is Nabil's sentence to say, not mine.** Action SM-9 puts Q34 at the
top of the next message to Karim precisely so that nobody has to say it.

**This is the third appearance of one mechanism** — D-035 (a portal client with a department), D-044
ruling 2 (an HR user in another department), now this. D-048's closing line is still the right one: *a
grant written against a department alone is satisfied by any role that carries that department.* Two
such grants remain.

---

## 4. Findings — things that are wrong now, not questions

Every row below was verified against the current files today. Nothing is carried from a note.

### 4.1 Story defects — the BA's

| # | Finding | Evidence |
|---|---|---|
| **N-01** | **KAFF-107's only additive deliverable does not exist.** The story asserts `errors.identity.hr_role_requires_hr_department` is *"missing from `ar.json` and `en.json` today"* and that adding both is part of the story. **It is present** — both `en.json` and `ar.json` carry the key at line 46. D-047 added it and made it structural. **KAFF-107 needs re-estimating or folding into KAFF-106; as written it is a 2-point story with nothing in it.** | KAFF-107, AC section lines 31-34 |
| **N-02** | **Four committed stories carry criteria that cannot be executed inside this sprint.** KAFF-118 declares a dependency on KAFF-119 (`:5`) and its AC1 (`:42`) needs client create/edit/archive while AC5 (`:79`) needs the team panel; KAFF-111 rule 4 (`:39`) and AC3 read the team panel; KAFF-110 AC4 (`:65`) exercises password recovery; KAFF-113 AC4 (`:66`) needs the per-project permission report. **All four reach into deferred stories.** | see §7 |
| **N-03** | **Q43 has already been answered in four stories without a ruling.** D-051 Q32, as this project's own register records it, is *"the project's name and its assigned engineers, **and nothing else**."* `KAFF-107:69`, `KAFF-113:34`, `KAFF-105b:35` and `KAFF-115:85` all describe HR's surface as carrying *"a project's name, **its code** and its assigned people"* — **and cite D-051 Q32 for it.** `process/agile.md`: an uncited rule is a question for Karim, not a story. | verified at `KAFF-113:32-36` |
| **N-04** | **KAFF-105a contradicts itself on a payload shape.** Rule 3 (`:40`) says `GET /api/auth/me` *reports* whether a temporary password must still be changed — a field. Its own AC (`:72`) says the call is **refused**. The dispatcher branches on one of them. Not a business rule; a BA and Architect decision. | UX Q-UX-18 |
| **N-05** | **The i18n key divergence is systematic and mostly inside the committed set.** Q-UX-21 raised it against deferred KAFF-101b. It is wider: KAFF-100 section 62 says `errors.setup.already_initialised` where the flows say `errors.setup.already_completed` — **two names for one server refusal, F-08's exact shape**; KAFF-103 section 47's five keys are **completely disjoint** from the flows'; KAFF-110 section 42, KAFF-112 section 48, KAFF-113 section 41, KAFF-114 section 28 all omit the `.action.` segment; @ `ux/rtl-and-i18n.md` fixes this (section 6). **Neither spelling exists in the catalogues yet**, so the cost is a text edit now and a migration later. | Stories listed above |
| **N-06** | **Three stories describe audit records the one sanctioned mechanism cannot produce.** KAFF-101a and KAFF-102 require records for sign-in, failed sign-in, lockout and sign-out — **none is an entity state change**, and `AuditSaveChangesInterceptor` writes only for `Added/Modified/Deleted` entries, while KAFF-118 rule 2 forbids a handler building one by hand. KAFF-100 requires `ActorUserId` = the new Owner on an **anonymous** request, and the actor comes from `ICurrentUser`, which returns null there (@ `HttpContextCurrentUser.cs` -> `ICurrentUser`). **No story names a mechanism.** This is Architect work and it is inside the committed set. | |

### 4.2 Test-case defects — QA's

| # | Finding | Evidence |
|---|---|---|
| **N-07** | **`TC-1-019` asserts the opposite of a ruling.** It requires a replayed cookie after sign-out to be refused, and instructs *"do not resolve it by rewording the AC"*. **D-051 N5 settles it the other way**, and `KAFF-102 AC1b` now states the replay **is still accepted**, deliberately, as the accepted trade. Bucket 2: **the story is right and the case is stale.** | `test-cases.md:502-509` vs `KAFF-102:44-47` |
| **N-08** | **`TC-1-003` reproduces what D-051 Q31 refused.** It asserts `ActorUserId` is null on the bootstrap record; the ruling exists precisely because *"a seeded account has no actor — the first row in the trail would name nobody."* | `test-cases.md:186-187` vs `decisions.md:1777-1778` |
| **N-09** | **`TC-1-143` and `KAFF-118 AC1b` disagree on a count.** The case asserts deactivation writes *"exactly one"* record; AC1b requires **four** on one `CorrelationId`. Both are committed. | `test-cases.md:1662` |
| **N-10** | **`TC-1-086` encodes one of Q35's two possible answers** — it asserts deactivation without a reason is refused, while `KAFF-110 AC6` says the reason is *"stored when it is given"*. A case that encodes an unasked question's answer is worse than a `PENDING`. | `test-cases.md:1205-1210` |
| **N-11** | **Test-case AC labels drifted after the stories were renumbered.** `TC-1-006` cites `KAFF-100 AC3` and asserts AC8; `TC-1-005` cites AC4, asserts AC7; `TC-1-042` cites `KAFF-105a AC3`, asserts AC6; `TC-1-197` cites `KAFF-106 AC8`, asserts AC10. **The Definition of Done line *"every QA test case for the story executed, with its result recorded"* cannot be mechanically checked against labels that point at the wrong criterion.** | `test-cases.md:204, 2252` |
| **N-12** | **Coverage gaps in committed stories:** KAFF-100 AC5, AC6, AC9; KAFF-101a AC13 (its only case, `TC-1-231`, was re-mapped to deferred KAFF-101b); KAFF-102 AC1b; KAFF-106 AC9; KAFF-109 AC5 and AC8; KAFF-111 AC4, AC5, AC7 — **KAFF-111 has two cases for seven criteria.** | |
| **N-13** | **One `PENDING` in the committed set, on a stale number.** `TC-1-079` is marked `PENDING Q27`; Q27 is answered, and the BA re-registered the residual as Q41. Story and cases disagree on whether it blocks. | `test-cases.md:1110` |

### 4.3 Code gaps — the Architect's and Backend's

| # | Finding | Evidence |
|---|---|---|
| **N-14** | **No lockout state on `User`.** @ `User.cs` shows no failure counter and no lockout-until. KAFF-101a rules 7 and 14 with AC3/AC4, and KAFF-100 rule 10, have nothing to hang on. | |
| **N-15** | **No "must change temporary password" flag on `User`.** **Five committed stories depend on a field that does not exist** — KAFF-101a rule 8/AC6, KAFF-103 rule 2, KAFF-105a rule 3/AC3, KAFF-106 AC8, KAFF-112 rule 4/AC5. | |
| **N-16** | **No way to clear a credential.** `User.SetPasswordHash` refuses null or whitespace (`:160-163`) and `Reactivate` (`:183-193`) touches neither `PasswordHash` nor `SecurityStamp`. KAFF-112 rule 3 has no method — and this is what Q50 is really asking about. | |
| **N-17** | **F-26 confirmed LIVE and it is the widest item in the sprint.** @ `ICurrentUser.cs` defines `KaffClaimTypes.SecurityStamp`; @ `User.cs` holds it and rotates it in multiple methods. **Nothing compares them** — `SecurityStamp` returns zero hits across `src/Api/`. It reaches KAFF-101a AC8/AC9/AC14, KAFF-102 AC2, KAFF-103 AC6, KAFF-110 AC3, KAFF-112 AC4b. D-051 N5 assigns the comparison to KAFF-101a, **which is committed** — so the sprint as scoped does close it, *provided AC14 is built and not read as a restatement of AC8/AC9*. | D-051 N5 |
| **N-18** | **F-05 is still live.** @ `PermissionCatalogue.cs` grants `ProjectRead` to `Role.HeadOfDesign` while `permission-matrix.md` still says HeadOfDesign holds nothing. The comment was corrected in slice 0; the matrix was not. `TC-1-202` surfaces it the day the matrix runs. Nabil's, not Karim's. | |
| **N-19** | **`ProjectAccess` cannot distinguish `OwnerGlobal` from `HrGlobal`** — see §1. KAFF-116 requires it to. | @ `PermissionEvaluator.cs` -> `ProjectAccess` and @ `ProjectAccessPolicy.cs` -> `GlobalReachAsync` |
| **N-20** | **No database constraint behind KAFF-100's atomic emptiness test.** @ `IdentityConfigurations.cs` has only the username unique index and the phone index; `GuardScripts.cs` has nothing about users. Flagged so nobody assumes it is already there — this is rule 6's build work on *the most privileged endpoint that will ever exist here*. | |

### 4.4 Process defect — the register regressed

**Action SM-4 made `stories/questions-for-karim.md` the single master register. It has not been swept
since.** A search for `Q-UX-16`, `17`, `18`, `19`, `20`, `21`, `22` and `QA-4` returns **zero matches**.
The merge table maps Q-UX-1 through Q-UX-15 exhaustively and stops there; the seven new UX questions and
QA-4 were raised in the same 2026-08-21 revision that merged the register, minutes after it was merged.

**The consequence is that a headline in that file is now false.** The opening of `questions-for-karim.md`
states *"**What blocks sprint 1, in one message. Nothing.** … Slice 1 has no BLOCKED story and no open
question of Karim's."* Nine bucket-three items say otherwise, and one of them (Q34) touches the gate.

**This is F-01 recurring in a new shape.** F-01 was *two registers with colliding numbers*; the fix was
to merge them. The failure that survived the fix is that **merging is an event and a register is a
process** — nothing makes the next question raised in `ux/` or `qa/` arrive in the master file. Action
SM-8, and a retrospective item in §8.

---

## 5. What did not survive checking

Recorded because §8 is about exactly this.

**UX reported that Q-UX-16 blocks KAFF-113 and KAFF-114, and that the committed scope is therefore
unsafe. It does not, and I checked before accepting it.** Q-UX-16 asks what HR may see of a *user*,
which blocks **S-010's user picker** — a screen. **KAFF-113's nine acceptance criteria are every one of
them server-side**: HR staffs a project it was never assigned to; HR still cannot open
it; HR's reach stops at a project that does not exist; two seniorities; seniority refused where §9 does
not put it; clients and subcontractors not assignable; nobody else can staff; an inactive user is not
assignable; no duplicate active assignment. **Not one of them requires a list of users.** KAFF-114's six
are the same shape.

**So: the endpoint enters the sprint, the picker does not, and the gate is still reachable** — the gate
is *permission tests pass*, which is an API assertion. **What is genuinely lost is a demo step**: HR
cannot be shown staffing a project through the UI, as @ `slice-1-flows.md` already carries that
marker. I am recording that as a cost of the scope, not hiding it.

**UX was right about the underlying gap, and it is worth stating separately from the scope claim.** HR
holds exactly two permissions — `ProjectAssignmentManage` and `EmployeeManage`
(@ `PermissionCatalogue.cs`) — and `Permission.cs` has **no user-read member at all**, with
`UserManage` company-wide and Owner-only. **HR can reach every project and cannot name a single person
to put on one.** That is a real hole and Q42 is the right response to it. UX also named the trap in
advance, which is the more useful half: **`EmployeeManage` will look like the answer and is not.** @ `User.cs` and @ `Employee.cs` are different
entities and the Employee register is slice 2. Closing Q42 with *"HR has EmployeeManage"* would invent
the rule that a costed person and a login are one record.

**One claim I could not fully square, left open rather than decided.** QA registered *"must the first
Owner change the password he typed himself"* as **QA-4, a question for Karim**; UX routed the same
question to **Nabil** as a policy consequence of D-051 Q31 plus D-049 ruling 4. They are not obviously
both wrong. **It goes to Nabil first, who decides whether it needs Karim** — that is Nabil's call and
not mine, and both agents' reasoning is in §2 for him to read.

---

## 6. Definition of Ready, run against the committed sixteen

| # | Criterion | Result |
|---|---|---|
| 1 | Every AC is Given / When / Then | ✅ **PASS** — checked across all 16; no criterion is missing a clause |
| 2 | Every rule cites `spec.md` or a D-number | ❌ **FAIL** — 33 rule rows across 13 of the 16 cite only `CLAUDE.md`, `slice 0 <code>`, or another story. Heaviest: KAFF-118 rules 1,2,4,5,6,7; KAFF-114 rules 2,4,6; KAFF-112 rules 7,8,9; KAFF-111 rules 3,5,6,9 |
| 3 | **No rule in the story is uncited** | ❌ **FAIL** — Q45…Q50 and the four code-derived refusals |
| 4 | Permissions named explicitly, role and assignment | ✅ **PASS**, two soft spots: KAFF-116 (`:39`) and KAFF-118 (`:34`) say "none directly" / "none" and name neither. Defensible for cross-cutting stories; neither says which endpoints inherit which check |
| 5 | Money named explicitly, or stated as none | ✅ **PASS** — all 16 carry the bullet, all 16 correctly say none |
| 6 | Arabic strings are i18n keys, never literals | ⚠️ **PASS on format, FAIL on fact** — N-01 and N-05 |
| 7 | The audit record it writes is stated | ❌ **FAIL on three** — N-06 |
| 8 | QA has ≥1 scenario that **fails** if the rule breaks | ⚠️ **MOSTLY** — one `PENDING` (N-13), ten uncovered criteria (N-12), four cases that assert the wrong outcome (N-07…N-10) |
| 9 | Not BLOCKED on an open question | ⚠️ **See below** |

### The ruling on criterion 9, and I want it on the record

**Applied literally, criteria 3 and 9 together admit nothing to this sprint.** Six of the sixteen carry
an uncited rule, and an uncited rule is by definition an open question. A refinement that concludes
"zero points enter" when the backlog entered with zero blocked would be process theatre, and the wrong
kind: it would spend the sprint and produce nothing while the questions sat unasked anyway.

**So I am drawing a line, and it is a scoping line, not a business one.** An uncited rule blocks its
story when **answering it differently changes what gets built**. Q46 (does the Owner carry a
department), Q48 (must the change-password endpoint demand the current password) and Q50 (cleared or
replaced) are of that kind — each changes an endpoint's shape. Q45, Q47 and Q49 add or remove a refusal
on a path that exists either way.

**I am not deciding which is which, and I am not waiving anything.** `agents.md` forbids me to resolve a
business question and forbids me to let a `BLOCKED` story into a sprint, and those two prohibitions
point in opposite directions here. The resolution that respects both: **the sixteen are recommended to
Nabil with all nine bucket-three items attached, and Nabil accepts the scope with a named waiver or
sends stories back.** The waiver is his signature, not my judgement. Action SM-9 and SM-14.

**What I will say without a waiver:** the ten permission-spine stories — 106 through 114, plus 118 —
carry the fewest bucket-three items of anything in the backlog, and they *are* the gate. If Nabil
returns a shorter scope, that is the scope it should be shortened to.

---

## 7. Actions

| # | Action | Owner | Before |
|---|---|---|---|
| **SM-3** *(carried)* | `User.ChangeRole` does not exist — verified, `src/Domain/Identity/User.cs` has only `SetPasswordHash`, `Deactivate`, `Reactivate`, `MoveToDepartment`. `Client` has no name or primary-phone setter (@ `Client.cs` -> `SetContactDetails`). **Half of this is now cheaper than it was:** the `Client` half serves KAFF-121, which is deferred, so only the `ChangeRole` half is sprint-blocking | Architect | KAFF-109 build |
| **SM-6** *(carried, close it)* | The permission spine blocks on nothing and **is** the gate. It is the committed scope. Start it | Backend | now |
| **SM-8** | **Sweep the register and keep sweeping it.** Q-UX-16…22 and QA-4 never reached `questions-for-karim.md`; the file's own headline is false as a result. Merge them with origins, assign the numbers this meeting proposed, and correct the *"nothing blocks sprint 1"* block | BA | before SM-9 |
| **SM-9** | **One message to Karim, and Q34 leads it** — Q34, Q41, Q42, Q43, Q44, Q45, Q46, Q47, Q48, Q49, Q50, plus the four code-derived refusals. Q17 rides along: `ProjectManage` is granted to nobody (verified in `PermissionCatalogue.cs`, grants list empty, `Unresolved: true`), so **no project can be created at all** — it blocks nothing today and blocks slice 4 outright | **Nabil** | scope acceptance |
| **SM-10** | **Scope the committed stories' criteria to the committed set.** KAFF-118 (dependency on 119, AC1, AC5), KAFF-111 (rule 4, AC3), KAFF-110 (AC4), KAFF-113 (AC4) all reach into deferred stories. Move the criterion with the story or restate it executably. Also: KAFF-113 `:32-36` and `:66` forward-reference deferred 105b/115 and call `/api/me` where KAFF-105a fixes `/api/auth/me` | BA | build starts |
| **SM-11** | **KAFF-107 re-estimate or fold.** Its stated deliverable already exists (`en.json:46`, `ar.json:46`). Decide whether 2 points remain in it | BA | build starts |
| **SM-12** | **Correct the four cases that assert the wrong outcome** — `TC-1-019` (D-051 N5), `TC-1-003` (D-051 Q31), `TC-1-143` (KAFF-118 AC1b), `TC-1-086` (→ `PENDING Q35`). Re-label the drifted AC citations. Cover the ten uncovered criteria, KAFF-111's five first | QA | build starts |
| **SM-13** | **`ProjectAccess` cannot report how access was granted** — one branch serves Owner and HR. KAFF-116 requires the distinction and F-07 records only the missing column, not this. Specify the policy's side | Architect | KAFF-116 build |
| **SM-14** | **Name the audit mechanism for acts that change no entity** — sign-in, failed sign-in, lockout, sign-out, and the bootstrap actor on an anonymous request. Three committed stories require records the interceptor cannot produce and KAFF-118 forbids a handler from building | Architect | KAFF-101a build |
| **SM-15** | **One i18n pass over the whole slice-1 key list** against `rtl-and-i18n.md` §6, not one line in KAFF-101b. Both catalogues are still empty of `auth.*`, so the cost is a text edit today | BA | build starts |
| **SM-16** | **Decide N-04** — is `password_change_required` a field or a refusal? KAFF-105a says both. Not a business question | BA + Architect | KAFF-105a build |
| **SM-17** | **F-05** — `permission-matrix.md:247` still says HeadOfDesign holds nothing while `PermissionCatalogue.cs:157-159` grants it `ProjectRead`. The code is right; the matrix is stale | Architect | gate run |
| **SM-18** | **Q-UX-20 → an N-number:** does the staff sign-in endpoint refuse a `Role.Client` credential now that the portal is a separate host? `KAFF-101a` rule 16 still says it is accepted, which predates D-051 Q33. Touches committed work | Architect | KAFF-101a build |
| **SM-19** | **N7 already covers the SMS/WhatsApp gap — do not re-file it.** `questions-for-karim.md:160` records that nothing in the pinned stack sends either. **What is genuinely missing from N7/N8: who monitors delivery failures, and what the Owner sees when a message is rejected.** Add that; KAFF-104 is deferred so it blocks nothing | Nabil + Architect | KAFF-104, next sprint |

**On the two "new" actions this meeting was handed:** the SMS/WhatsApp path is **already registered as
N7**, not new — see SM-19. Q17 is **already registered and correctly described**; it is added to SM-9's
message rather than raised again.

---

## 8. Retrospective — the process, not the code

### The failure mode that bit twice today, and how it was handled

**An agent resumed from its own transcript trusts its earlier findings over the current files.** The QA
agent re-reported F-10, F-11, F-12 and F-18 as live P1 defects after all four had been fixed or
answered. Each cost time to disprove.

**What was done differently in this session, and it worked.** Every agent brief carried an explicit
evidence rule — *every finding must cite the file path and line number you verified it against in the
current files* — and an explicit list of closed findings that re-reporting would itself be a defect.
**Not one of the three agents re-reported a closed finding.** All three returned file:line citations,
and the ones I spot-checked held: the i18n keys (the BA's N-01), @ `PermissionEvaluator.cs` (QA's
F-04 mechanism), KAFF-113 (UX's N-03), test cases against KAFF-102 (QA's
N-07).

**But the check still earned its keep, which is the point.** One claim did not survive — UX's assertion
that Q-UX-16 blocks KAFF-113 and KAFF-114. Reading KAFF-113's nine criteria showed every one of them is
server-side and none needs a list of users; the blocked thing is a screen. **Had I taken it, I would
have cut 8 points out of the permission spine and put the slice gate out of reach, to protect against a
gap that does not touch it.**

**Proposed addition to `process/agile.md`, and it costs one line in a brief:** *a finding reported into
refinement cites the file and line it was verified against today, or it is not a finding.* And its
corollary, which is the Scrum Master's own job and not delegable: **a citation is evidence that the file
says what the agent says it says; it is not evidence that the conclusion drawn from it is right.** The
one claim that failed today had a correct citation and a wrong inference.

### The register regressed because merging is an event and a register is a process

F-01 found two question registers with colliding numbers. SM-4 merged them. **Within the same day,
seven new UX questions and one QA question were raised into `ux/questions.md` and `qa/questions.md` and
never reached the master file** — and the master file now carries a headline stating that nothing blocks
sprint 1, which is not true.

The merge was correct and it was not sufficient. **Nothing in the process makes the *next* question
arrive.** The cheapest fix is a step in this ceremony rather than a new mechanism: **refinement re-reads
`ux/questions.md` and `qa/questions.md` for anything not in the master register, every time.** That is
what caught it today, by accident. It should not be by accident.

### F-21's defect survived its own fix, one level down

The last refinement found six `Ready` stories depending on a `BLOCKED` one, and SM-1 recomputed the
backlog **transitively at the story level**. Today, four committed stories carry **acceptance criteria**
that can only be executed against deferred stories (N-02). Same defect, one level of granularity down,
and the recomputation did not reach it because it counted stories.

**The Definition of Ready has a line for "not BLOCKED"; it has no line for "executable within the
committed scope".** That is what SM-10 is fixing by hand this sprint. Proposed permanent line:
**a story's criteria must be executable against the stories committed alongside it, or they move with
the story they depend on.**

### The good news is worth recording too

**Every one of the three agents found something in another agent's work, and none of them found it in
their own.** The BA found a story asserting a translation key is missing when it exists. UX found four
stories that have already answered an unasked question about a project code. QA found four test cases
asserting the opposite of a ruling — including one that instructs the reader *not* to fix it by
rewording the story, when the story is the thing that is right.

`agents.md` principle 2 — the author never certifies its own work — held on every occasion it was tested
today, for the second refinement running. It is the cheapest control this project has.

### One number that should not be read as progress

The last meeting corrected *"14 Ready, 43 points"* down; today the backlog entered at **92 points with
nothing blocked** and left with **59 committed and nine new questions**. **The question count going up
is the meeting working.** `process/agile.md`: a refinement that produces no questions has not been run
properly — and this backlog entered claiming, in writing, that it had no open questions at all.

---

# Addendum — after D-052, same day

Three rulings landed after the meeting closed: the Architect on F-04, Karim on Q17, Nabil on Q44.
Recorded as **D-052** (`decisions.md:1851`). Two of the three changed code. This addendum is the
re-run, not a new meeting.

## A1. The gate is clear, and it was verified here rather than accepted

**F-04 is fixed, and there is no documented exception.** The Architect's ruling: *"No documented
exceptions. The gate must pass with 100% compliant code. Financial permissions like
`SiteExpenseConfirm` must never be granted to a bare department without specifying a role."*

`SiteExpenseConfirm` now grants to `Role.Finance`, and to `Role.TechnicalOffice` **conditional on**
Operations/Administrative (@ `PermissionCatalogue.cs`). Every criterion on a grant must match, so
a `Role.SiteEngineer` parked in that sub-department holds nothing.

**Checked by the Scrum Master rather than taken on report**, because §3 of this meeting made the gate
turn on it:

| Check | Result |
|---|---|
| Clean rebuild of `Kaff.Domain.Tests`, Release | **exit 0**, 0 warnings — checked *before* the test result, per D-052's own note about a stale binary reporting green |
| `PermissionEvaluatorTests` | 20 / 20 |
| Full Domain suite, executable invoked directly (D-046) | **70 / 70**, 0 failed, 0 skipped |
| The assertion `TC-1-215` describes | present in @ `PermissionEvaluatorTests.cs` -> `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense` — SiteEngineer in Ops/Admin → `RoleNotGranted`; Finance → `Granted`; TechnicalOffice in Ops/Admin → `Granted`; TechnicalOffice in Ops/**Technical** → `RoleNotGranted` |
| The mechanism, not the row | @ `PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department` pins **eleven** money-touching permissions |

**`TC-1-215`'s Domain half is green. Its Api half is not, and must not be reported as passing** — the
case is labelled `Domain + Api` and no endpoint requires the permission until slice 6 (KAFF-608). QA
has rewritten it as a regression case with that split stated.

**Pinning the class rather than the row is the part worth keeping.** F-04 was the third appearance of
one mechanism — D-035, D-044 ruling 2, now this. Fixing the row would have invited a fourth.

**🟡 `PhotoPublish` is the last bare-department grant** (`PermissionCatalogue.cs:258`) and is
deliberately left: the ruling is scoped to *financial* permissions and a photo moves no money, so
extending it would be applying a rule nobody gave. Registered as **Q52**.

## A2. Q17 answered — and what it left behind is bigger than what it closed

Karim: only the Owner and the Technical Office open a project — *"Site Engineers and Marketing have no
business creating projects."* `ProjectManage` grants `[owner, technicalOffice]`
(@ `PermissionCatalogue.cs`) and is no longer `Unresolved`. **Verified: `PeriodClose`
is now the only `Unresolved: true` row in the catalogue** — down from two, and from five at slice 0.

**But the permission still cannot do the thing Karim just ruled on.** The row is `ProjectScoped`, so
the evaluator refuses when the request names no project — and a **create** request cannot name one,
because the project does not exist yet. As written it authorises *editing* a project and cannot
authorise *opening* one. Company-wide would drop the assignment requirement from editing and weaken
§9; splitting create from edit is the alternative. **Raised, not taken** — the Architect's, registered
as **N10**, and **slice 4's blocker is now N10 rather than Q17.**

## A3. Q44 answered — Nabil, and the routing is the point

The first Owner is **not** forced to change the password he typed himself at the setup screen. Nobody
else ever knew it, so the non-repudiation the forced-change rule protects is not at risk. Recorded as
the **scope of an existing rule, not an exception to it**.

**§5 of this meeting recorded that QA and UX disagreed about whether this was Karim's question or
Nabil's, and routed it to Nabil rather than resolving it. That is the mechanism working**, and it is
worth noting that the answer came back in hours and closed a criterion that had been standing on a
*reading* of D-049 ruling 4 — `KAFF-100`'s rule now stands on a ruling instead.

## A4. Definition of Ready, re-run. **Scope holds — revised to 15 stories, 57 points.**

| # | Criterion | Before | Now |
|---|---|---|---|
| 1 | Every AC is Given/When/Then | ✅ | ✅ |
| 2 | Every rule cites `spec.md` or a D-number | ❌ 33 rows | ⚠️ improved — D-052 rulings are cited; slice-0-code citations remain |
| 3 | No uncited rule | ❌ 11 items | ⚠️ **9** — Q34 and Q44 closed |
| 4 | Permissions named explicitly | ✅ | ✅ |
| 5 | Money named explicitly | ✅ | ✅ |
| 6 | Arabic strings are i18n keys | ⚠️ | ⚠️ N-01 corrected; N-05's key divergence stands (SM-15) |
| 7 | Audit record stated | ❌ 3 stories | ❌ unchanged — N-06 is Architect work (SM-14) |
| 8 | QA has a scenario that **fails** if the rule breaks | ⚠️ | ✅ **materially better** — see A5 |
| 9 | Not BLOCKED, and executable within the committed scope | ⚠️ | ✅ **SM-10 done** — AC-level breaks rescoped |
| — | **The gate** | ❌ one P1 case red on shipped code | ✅ **clear, no exception** |

**The scope survives, and it gets smaller by one story for a reason worth recording.**

**KAFF-107 folds into KAFF-106 and KAFF-108. 16 stories / 59 points → 15 stories / 57 points.**

N-01 found that the story's stated deliverable already exists. What was left was checked rather than
assumed: the refusal is in the domain at `User.cs:232-235`, reached from `Create` and from
`MoveToDepartment`, and it is already asserted at `PermissionEvaluatorTests.cs:304-347`. **What
remained was two endpoint-level refusal tests — AC2 on the create path (KAFF-106) and AC3 on the move
path (KAFF-108), both committed.** The BA's phrasing is the right one: *a story whose criteria all
execute through someone else's endpoint is a test plan.*

**And its warning is accepted with the fold, not waved past:** parking assertions in a third story is
how they get lost when it slips — which is F-21's shape again. So the fold is conditional on **both**
106 and 108 naming the bare-department mechanism explicitly, or the refusal reads as arbitrary to
whoever builds it. **Action SM-21.**

## A5. What the two agents changed, and what they found that was not asked for

**QA** converted `TC-1-215` to a regression case with the Domain/Api split stated; corrected the
expected-failures table from *"only one live defect in shipped code"* to none; added an **A·D** symbol
to the permission matrix for a grant that names role **and** department, keeping the old bare-department
**D** for `PhotoPublish`; carried D-052's `ProjectManage` 🟡 into the matrix as **F-27**, because a cell
reading *"Owner and Technical Office may create a project"* with no caveat would assert a capability
that does not work; and fixed SM-12's four cases — `TC-1-019` (D-051 N5), `TC-1-003` (D-051 Q31), `TC-1-143` **split into three**, `TC-1-086` → `PENDING Q35`.

**Two things QA found that nobody asked for, both the same defect class as the four:** `TC-1-206` and
`TC-1-207` still asserted *"nobody holds `ProjectManage`"* with an `Unresolved` set of two. And the
AC-label drift is **31 cases, not 4** — whole blocks shifted because stories inserted criteria
mid-list. All 31 fixed. **One left deliberately unfixed and it is the right call:** `TC-1-032` asserts that an unknown
username reveals nothing on a password reset, and **no `KAFF-104` criterion states that rule** —
fixing it would mean inventing it. **Action SM-22, and it is a bucket-three item in KAFF-104**, which
is deferred, so it blocks nothing this sprint.

**BA** swept the register (below), applied D-052, rescoped the AC-level breaks — KAFF-118's
client steps, KAFF-110's recovery half into AC4b, KAFF-111 and KAFF-113 restated against executable scope — and fixed
the `/api/auth/me` route error in two stories.

**Four things the BA found that were not in its brief**, all worth the
addendum: the *forced-change* rule is **KAFF-100 rule 8**, not rule 4; **KAFF-107 carried a second stale claim** about
`SiteExpenseConfirm`, false since D-052; **`backlog.md` carried the same
false headline as the register**; and **KAFF-120 also states that
`ProjectManage` is granted to nobody**. Three of the four are the same failure as N-01.

## A6. The register, swept — SM-8 closed

Eight questions raised on 2026-08-21 that never reached the master file are merged, with origins:
**Q42** (what HR may see of a user), **Q43**
(project code and team size), **Q45–Q51** (reserved usernames, the Owner's department,
indistinguishable refusals, current-password on change, the last engineer, cleared-vs-replaced, and
the four slice-0-derived refusals as one row), **Q52** (`PhotoPublish`), and **N9**. Q-UX-19 folded into **N7/N8** rather than
re-filed, so **only** *who monitors delivery failures* was genuinely new.

**Open count: 33 for Karim, 7 for Nabil and the Architect** (N2, N4, N6, N7, N8, N9, N10).

**The false headline is gone**, in both `questions-for-karim.md` and `backlog.md`. What replaces it is
honest, and one clause of it is not quite: the ask-list calls **Q42**
*"the only one that blocks committed work"*, while the body correctly says *"Q42
blocks a screen, not a story's readiness."* **Under this sprint's scope no screen is committed at
all** — KAFF-101b is deferred and every committed story is server-side. Q42 blocks deferred work and a
demo step. Left as is rather than re-opened, because the qualifying sentence sits directly
above it, but recorded here so the next reader takes the body over the bullet. **A register that
swings from one confident headline to the opposite one has not been fixed, only re-pointed.**

## A7. Retrospective, added

**The evidence rule paid for itself a second time, in the other direction.** The same rule surfaced **31 mislabelled test cases, two stale catalogue assertions, a second false
headline in `backlog.md`, and three stories describing code that had moved** — none of which anyone
asked either agent to look for. **The rule does not only prevent bad reports; it makes agents read the
current file, and reading the current file is what finds things.**

**And the Architect's correction of itself belongs here.** D-052 records a distinction: *"That is true of the **Api** half only; the **Domain** half needed nothing
but a call to the evaluator."* The reusable part is the principle — ***"no
endpoint calls it" is a statement about reach, not about whether a rule is wrong.*** A permission rule
lives in the evaluator, and the evaluator is callable the moment it compiles.

**One process gap this round exposed, and it is cheap to close.** The AC-label drift was not a QA
defect: stories renumber their criteria when one is inserted mid-list, and nothing tells the cases.
Thirty-one drifted before anyone noticed, and the Definition of Done line *"every QA test case
executed, with its result recorded"* cannot be checked mechanically against a label pointing at the
wrong criterion. **Proposed: acceptance criteria get stable identifiers — an inserted criterion takes
the next free number rather than pushing its neighbours down.** The same argument `process/agile.md`
already makes for story IDs: *"a renumbered story silently detaches its tests."* It is true one level
down and nobody had said so.

## A8. Actions — added and closed

**Closed this round:** SM-8 (register swept), SM-10 (criteria rescoped), SM-11 (KAFF-107 → fold),
SM-12 (four cases corrected, plus 31 labels and two stale assertions), and **SM-9's first line** —
Q34 needs no message to Karim; it was answered and fixed.

**Still open, unchanged:** SM-3 (`User.ChangeRole`), SM-6 (build the spine — now unblocked and clear),
SM-9 (the message to Karim, now 33 questions led by Q42 and Q43), SM-13, SM-14, SM-15, SM-16, SM-17,
SM-18, SM-19.

| # | Action | Owner | Before |
|---|---|---|---|
| **SM-21** | KAFF-107 folds into KAFF-106 and KAFF-108. Both must name the bare-department mechanism explicitly, or the refusal reads as arbitrary | BA | build starts |
| **SM-22** | `TC-1-032` asserts that an unknown username reveals nothing on a password reset and **no criterion states it**. Bucket three, deferred — add it to the register rather than to the case | BA | KAFF-104, next sprint |
| **SM-23** | Stable AC identifiers — an inserted criterion takes the next free number instead of renumbering neighbours. Same argument `process/agile.md` makes for story IDs, one level down | BA + QA | next refinement |
| **SM-24** | **N10** — `ProjectManage` is `ProjectScoped` and create requests name no project. Editing vs opening requires splitting them | Architect | slice 4 |
| **SM-25** | **Q52** — `PhotoPublish` is the last bare-department grant, outside D-052's financial scope | Nabil → Karim | slice 6 |

---

# Addendum 2 — after D-053 and Nabil's execution broadcast · 2026-08-22

**Scope locked by Nabil at 15 stories / 57 points.** Two of the four directives arrived built; two are
delegated and had not landed when this was written.

## B1. D-053 verified here, not accepted on report

Both directives were checked against the current files and the suites were run, build exit code read
**before** the test result:

| Check | Result |
|---|---|
| `Kaff.Domain.Tests` Release build | **exit 0**, 0 warnings |
| Domain suite, executable invoked directly | **71 / 71**, 0 failed, 0 skipped |
| `Kaff.Api.Tests` Release build | **exit 0**, 0 warnings |
| Api suite, real PostgreSQL | **43 / 43**, 0 failed, 0 skipped |

**The session kill exists.** @ `ICurrentUser.cs` -> `SecurityStamp` carries the claim;
@ `PermissionSubjectReader.cs` compares it in the `WHERE` clause — `&& user.SecurityStamp ==
securityStamp` — so rotation invalidates every token for that user at once, and the comparison stays
ordinal at the database. Both guards exist and are named for what they do:
`Rotating_the_security_stamp_kills_every_existing_session` and
`A_request_with_no_security_stamp_is_refused` (@ `PermissionMechanismTests.cs`). **F-26 is closed** — it was the widest item in
the sprint, reaching KAFF-101a AC8/AC9/AC14, KAFF-102 AC2, KAFF-103 AC6, KAFF-110 AC3 and KAFF-112
AC4b.

**Two harness decisions in D-053 deserve to survive into QA's relock, because both are the difference
between a test and a decoration:**

* The rotation test goes through a **password change**, leaving the account active. Through
  `Deactivate` it would have passed whether or not the stamp mechanism existed, because `IsActive`
  refuses first. That is this project's recurring question — *what would this look like if the thing
  it checks were broken?* — applied before the test was written rather than after.
* `TestAuthHandler` emits the stamp **only when a header supplies one**. A double with an
  always-matching stamp would have disabled the global sign-out for the entire suite. That is D-046's
  catalogue of green-results-that-are-not-evidence, avoided in advance.

**The money guard is now at the point of decision.** `PermissionDefinition.TouchesMoney`
(`PermissionCatalogue.cs:58, 75`) marks eleven permissions, and `PermissionEvaluator.cs:135` discards
any grant with a null `Role` on those before matching:
`(!definition.TouchesMoney || grant.Role is not null) && Matches(grant, subject)`. Verified the flag is
set on all eleven rows, and that
`The_evaluator_refuses_a_bare_department_grant_on_money_even_if_one_reaches_the_catalogue`
(`PermissionEvaluatorTests.cs:177`) tests it against a definition the shipped catalogue deliberately no
longer contains.

**D-052 protected the rows that exist; D-053 protects the rows added tomorrow.** That distinction is
the whole value of the second fix and it should not be read as belt-and-braces: F-04's mechanism has
now produced three separate leaks (D-035, D-044 ruling 2, F-04), every one found by somebody reading
rather than by anything failing.

## B2. Q52 — contained, with one leak into committed work

**Contained in the register.** `questions-for-karim.md:140` carries Q52 with the right scoping —
*blocks nothing before slice 6* — and names `PermissionCatalogue.cs:258` as the last bare-department
grant, deliberately left because the Architect's ruling is scoped to financial permissions and a photo
moves no money. It is fifth on the ask-list (`:193`). **No PhotoPublish code exists or is scheduled in
slice 1**, and `TouchesMoney` is correctly **not** set on it, so the financial gap stays closed while
the photo question stays open.

**One leak, and it is the SM-10 defect class again.** `KAFF-107:68` has an acceptance criterion that
calls *"an endpoint requiring `ProjectRead`, `SiteExpenseConfirm`, `TreasuryPostProject`,
`FinancialMovementApprove`, `AccountManage` and **`PhotoPublish`**"* — HR's zero-financial-visibility
sweep. **None of those endpoints exists in slice 1**, and `PhotoPublish`'s is slice 6. The criterion is
sound and the level is wrong: the equivalent assertion already runs against the evaluator at
`PermissionEvaluatorTests.cs:304-347`.

KAFF-107 is folding into KAFF-106 and KAFF-108 (**SM-21**), so this criterion travels with the fold.
**Action SM-26: it lands as an evaluator-level assertion, not an endpoint-level one.** Written the
other way it would either fail for five sprints or quietly get dropped — and dropping it would remove
the only thing asserting HR's zero financial visibility, which is D-044 ruling 2's whole point.

## B3. SM-10 — verified landed, all four

Checked in the files rather than taken from the BA's report:

* **KAFF-118** — KAFF-119 is off the dependency line (`:5`, `:12`); the three client steps are now
  **AC1a**, marked *"moves with KAFF-119, deferred"* (`:54`); AC5 restated against `/api/auth/me` and
  the assignment read, naming the client list (KAFF-124) and team panel (KAFF-115) as deferred (`:95`, `:99`, `:101`)
* **KAFF-110** — AC4 split; the recovery half is **AC4b**, *"moves with KAFF-104, deferred"* (`:68`, `:73`)
* **KAFF-111** — rule 4 and AC3 restated against active-assignment rows rather than the team panel (`:39`, `:69`, `:74`)
* **KAFF-113** — AC4's second half restated (`:78-84`), route corrected to `/api/auth/me` in both 113
  and 118, and `:37-42` now states that the `ProjectTeamRead` surface arrives later and that **no
  slice-1 test may assert it**, because `Permission.cs` has no such member

Each carries its reasoning inline rather than a silent edit, which is what makes it auditable next
session. **SM-10 is closed.** The Definition of Ready line it exposed — *a story's criteria must be
executable against the stories committed alongside it* — has now caught a fifth instance in KAFF-107
(B2 above), which is the argument for making it a permanent line rather than a one-off sweep.

## B4. The two delegated pieces — not landed at the time of writing

* **N10, project-creation scope → Architect.** `d:\ERP\proposals\` does not exist yet. Design only, no
  code, due before slice 4. The extra question Nabil attached is the valuable half and worth
  restating: **does `ProjectAssignmentManage` carry the same defect?** Somebody must make the first
  assignment on a just-created project, and if that permission is also `ProjectScoped` in a way that
  needs an existing project it fails for the same reason — the two are one problem, not two.
* **Stable AC identifiers → BA.** `d:\ERP\stories\ac-id-map.md` does not exist yet. **The relock must
  not start before it does.** QA re-derived AC citations once already and produced 31 wrong ones; a
  second re-derivation without the map recreates precisely the defect the map exists to end. When it
  lands, QA relocks against the **map**, not against the stories.

## B5. The Backend brief, scoped to what remains

Nabil's go-ahead covers the permission spine, and the two items previously named for Backend — the
stamp comparison and F-04 — are **done**. What is left, in dependency order:

1. **KAFF-106** — nothing exists, and every other story needs a subject. Carries folded KAFF-107 **AC2** (the create path)
2. **KAFF-100** — the atomic emptiness check. **No database constraint backs it** (`IdentityConfigurations.cs:52-57`, `GuardScripts.cs`) — that is build work, on the most privileged endpoint in the system
3. **KAFF-108** — carries folded KAFF-107 **AC3** (the move path)
4. **KAFF-109** — **`User.ChangeRole` does not exist** (SM-3, still open) plus the revoke-on-role-change cascade. `ProjectAssignment.Revoke` already does the right thing and keeps the row as history; nothing calls it. **Build this against D-051 Q27, not D-049 ruling 6 — the rules inverted**
5. **KAFF-113, KAFF-114** — the assignment endpoints
6. **KAFF-110, KAFF-111, KAFF-112** — deactivation must revoke assignments (D-049 ruling 5; nothing calls `Revoke` on deactivate today), and **`Reactivate` must rotate the stamp and does not** (D-051 N5's residue, now that the comparison exists and the omission has teeth)
7. **KAFF-101a, 102, 103, 105a** — session lifecycle
8. **KAFF-116, KAFF-118** — the audit mechanism

**Four things Backend must not build around, because inventing past them is the failure this process
exists to catch.** All are Architect items and all are open: **N-14** no lockout state on `User`;
**N-15** no must-change-password flag, which five committed stories depend on; **N-16** no way to clear
a credential, which is what Q50 is really asking; **N-19 / SM-13** `ProjectAccess` cannot distinguish
`OwnerGlobal` from `HrGlobal`, which KAFF-116 requires. **SM-14** — the audit mechanism for acts that
change no entity — blocks item 7's audit criteria specifically.

## B6. One papercut, found by running the suite

The Api harness's **default** connection string and its own error message suggest
`Username=postgres;Password=postgres` (`PostgresDatabase.cs:34`, `:78`), while `README.md:77` and
`docker-compose.yml` both use `kaff/kaff`. My first run failed with `28P01`; the second, with the
README's credentials, gave 43/43. **D-046 fixed this in the README direction and left the harness
pointing the other way** — so a developer who follows the harness's own suggestion gets an
authentication failure that names nothing to do with the cause. One line. **Action SM-27.**

Worth noting what the harness got *right* in the same breath, because it is the more important half:
it refused to fall back to an in-memory provider, and said why — *"the rules they check … live in the
database, and a provider that does not run them would report safety that does not exist."* That is a
red result that is evidence, which is the standard this project has spent three days establishing.

## B7. Actions

**Closed:** SM-10 (verified in all four stories), and the two items previously scoped to Backend —
the stamp comparison and F-04 — both landed in D-053. **F-26 closed.**

| # | Action | Owner | Before |
|---|---|---|---|
| **SM-26** | KAFF-107's zero-financial-visibility criterion names six endpoints that do not exist, `PhotoPublish` among them. It travels with the SM-21 fold as an **evaluator-level** assertion. Do not let it land as an endpoint test — it would fail for five sprints, and dropping it would remove the only assertion of D-044 ruling 2 | BA + QA | build starts |
| **SM-27** | The Api harness's default connection string and error message suggest `postgres/postgres`; the README and `docker-compose.yml` use `kaff/kaff`. D-046 fixed one side only | Backend | next Api change |
| **SM-28** | **Hold the AC relock until `stories/ac-id-map.md` exists.** QA relocks against the map, never by re-deriving — one re-derivation already produced 31 wrong citations | QA | after the BA lands the map |

---

# Sprint 1 — state for Nabil · 2026-08-22

Written for the acceptance decision. The detail is above and in `decisions.md` D-052…D-054; this is
the judgement.

## C1. Where it stands: ready to build

**15 stories, 57 points, gate clear, nothing BLOCKED.** Verified here rather than reported:
`KaffErp.sln` Release **0 warnings**, `dotnet format --verify-no-changes` clean, **Domain 71/71**,
**Api 43/43** — and the Api suite now passes with `KAFF_TEST_DB` **unset entirely**, which is D-054's
real result: the default path nobody had ever exercised, which is precisely why it was free to be
wrong for three days.

The three things standing between the sprint and its gate are closed: **F-04** (D-052 catalogue, D-053
evaluator), **F-26** the session kill (D-053), and **Q17** project creation (D-052). `PeriodClose` is
the last `Unresolved` row in the catalogue, down from five at slice 0.

**Build order for Backend** — the two items you named are done, so: KAFF-106 → KAFF-100 → KAFF-108 →
**KAFF-109** (`User.ChangeRole` still does not exist, SM-3; build it against **D-051 Q27**, not D-049
ruling 6 — the rules inverted) → 113/114 → 110/111/112 → 101a/102/103/105a → 116/118.

## C2. Decisions waiting on you, most urgent first

**1. Q-N10-2 — urgent because it is two of Karim's own rulings contradicting each other.** D-052 gave
`ProjectManage` to the Owner and Technical Office, from a ruling about *opening* a project. D-049
rulings 9-10 gave **Finance** the contract's withholding category — *"a strict accounting parameter,
not a marketing detail"* — and `Project.SetWithholding` already exists. **Finance holds no
`ProjectManage` grant, so an edit endpoint gated on it refuses Finance the one field Karim assigned
them.** Nobody invented anything; two correct rulings met. Ask it with Q42 and Q43 in one message.
Blocks KAFF-416's *estimate*, not slice 1.

**2. Approve N10.** `Permission.ProjectCreate`, company-wide, Owner + Technical Office; `ProjectManage`
left project-scoped so §9's assignment requirement keeps applying to every edit. **Zero impact on the
committed 15** — one enum member, one catalogue row, one test repointed and two added, no migration.
Yours and the Architect's, not Karim's. The proposal also checked `ProjectAssignmentManage` and found
it is **not** the same defect — global reach already solves it, and reach is unavailable to N10 because
there is nothing yet to reach.

**3. The message to Karim — 33 questions, led by Q42 and Q43.** Q42 is the one with a consequence you
can see: **HR can reach every project and cannot name a single person to put on one.** `Permission.cs`
has no user-read member at all. It blocks HR's demo step, not the gate.

**4. A waiver, or stories back.** Six committed stories still carry an uncited rule (Q45-Q50).
`process/agile.md` says an uncited rule is a question, not a story; applied literally, nothing enters
the sprint. **I drew a scoping line and declined to draw a business one:** the stories came to you with
every bucket-three item attached, and accepting the scope with a named waiver is your signature, not my
judgement. Nothing has changed that.

## C3. Risks you are carrying

| Risk | Why it matters |
|---|---|
| **N-14, N-15, N-16 — three fields `User` does not have** | No lockout state, no must-change-password flag, no way to clear a credential. **Five committed stories depend on the flag alone.** Architect, open. Highest-probability cause of a mid-sprint stall |
| **N-19 / SM-13 — `ProjectAccess` cannot tell `OwnerGlobal` from `HrGlobal`** | One branch serves both. KAFF-116 requires the distinction, and **KAFF-116 cannot be backfilled** — the table is append-only and trigger-protected. If it slips past slice 1 the gap is permanent |
| **SM-14 — no audit mechanism for acts that change no entity** | Sign-in, failed sign-in, lockout, sign-out, and the bootstrap actor on an anonymous request. Three committed stories require records the interceptor cannot produce and KAFF-118 forbids a handler from hand-building one |
| **Accepted, not open** | N5's all-or-nothing revocation — a lost phone signs you out everywhere; and D-044 ruling 8's two removed ledger floors, which land in slice 3 with QA scenarios as their only cover |

## C4. Is the process holding? Yes — and one part is not, and it nearly cost you a defect

**It is holding.** Every significant finding in three refinement rounds came from one agent reading
another's work: QA found the missing `spec.md` amendments, the BA found the untranslated key and three
stories describing code that had moved, UX found four stories that had answered an unasked question,
the Architect corrected its own *"not reachable until slice 6"*. `agents.md` principle 2 has held every
time it was tested.

**What is not holding is story currency, and five instances is a pattern, not an incident.** KAFF-107
said a translation key was missing when it existed; KAFF-120 and KAFF-122 said `ProjectManage` was
granted to nobody; four places told Backend to build a `SecurityStamp` comparison that already existed;
`backlog.md` and the register both carried a headline saying nothing blocked sprint 1.

**My read: this is structural, not careless.** `spec.md` has amendment blocks, `decisions.md` has
D-numbers and superseded markers, `qa/questions.md` has strike-through. **Stories have no staleness
mechanism at all** — a story asserts the state of the code in the present tense, is written once, and
is read as current forever. The code moved four times in three days. The stories could not have kept
up, because nothing tells them to.

**The near-miss is the reason to act rather than note it.** `AC-108-A` asserted the F-04 leak as
**correct behaviour** — it named no role, so it claimed any user moved into Operations/Administrative
gains `SiteExpenseConfirm`. **KAFF-108 is third in Backend's build order, and Backend builds what the
story says.** That story would have instructed the code to re-open a defect that three layers of
protection had just been built to close. Not *"documentation drifted"* — **a story can command a
defect.**

**The fix is the one already working elsewhere and it costs a clause.** The agent briefs this week
required every claim to cite the file and line it was verified against *today*; it stopped four closed
findings being re-reported and surfaced 31 mislabelled test cases nobody had asked about. **Apply the
same rule to stories: any claim a story makes about the state of the code carries the date and the
`file:line` it was checked against.** A dated claim is checkable in seconds. An undated one is
re-litigated by whoever reads it next, which is what happened five times. **SM-29.**

**One thing I would not change.** Three questions were routed away from Karim this week — Q44 to you,
N9 and N10 to the Architect, Q-UX-21 to the BA — and every one came back faster than a Karim round
trip. The register's split between *"for Karim"* and *"for Nabil and the Architect"* is doing real
work: it protects the only genuinely scarce input in this project, which is Karim's attention.

## C5. Actions added

| # | Action | Owner | Before |
|---|---|---|---|
| **SM-29** | A story's claim about the state of the code carries its verification date and `file:line`. Five stale assertions in three days, one of which asserted a defect as correct behaviour on a story third in the build order | BA | build starts |
| **SM-30** | **Q-N10-2 into the next Karim message** — Finance holds no `ProjectManage` grant, and D-049 rulings 9-10 gave Finance the withholding category. Two rulings in conflict, not a gap. Before KAFF-416 is estimated | Nabil → Karim | slice 4 estimate |
| **SM-31** | Approve or reject N10's `ProjectCreate` recommendation. No code before slice 4; no impact on the committed 15 | Nabil + Architect | slice 4 |

**Closed:** SM-23 (228 stable AC IDs, 232-row map), SM-24 (N10 proposal delivered), SM-27 (D-054), and
SM-28 is unblocked — **QA relocks against `stories/ac-id-map.md`, never by re-deriving.** `AC-108-B2`
is in the map with no old reference: a case citing it is new work, not a relabel.