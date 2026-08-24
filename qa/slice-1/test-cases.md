# Slice 1 — test cases

**252 live cases**, grouped by story, in `KAFF-1nn` order. Every acceptance criterion in the slice-1
stories appears below with a case, an explicit `PENDING` row, or a named coverage gap.

**Revised 2026-08-21** against `decisions.md` **D-049** (Karim's ten rulings), **D-050** (the access
token moved into an `HttpOnly` cookie) and **D-051** (Karim's five answers and the Architect's three).

**Revised again 2026-08-22 — the AC relock (SM-23) finished, and ten cases added for the three new
permissions (D-055 §§1–3, D-056 §3).**

### The count, because three documents disagreed about it and none of them was right

| | |
|---|---|
| `TC-1-nnn` identifiers in this file | **258** |
| Of which `TC-1-000` | **not a case** — the format template in *How to read a case* below |
| Real cases, `TC-1-001` … `TC-1-257` | **257**, **no gap and no duplicate** [Verified: 2026-08-22, every id in the range present exactly once as a heading] |
| `RETIRED` | **5** — `TC-1-175` … `TC-1-179` |
| **Live** | **252** |

**Where the wrong numbers came from, recorded so they are not re-derived.** The brief for this session
said **241**; the header of this file said **243** and its own totals line agreed with itself; a sweep
of the file counted **248** identifiers. All three were arrived at differently and none is the live
count. **243 was 242 live cases plus the `TC-1-000` template**, carried forward without recounting
after the split of `TC-1-143` added `TC-1-246` and `TC-1-247`. **248 counted identifiers, template
included.** **241 has no derivation this session could reconstruct** — it predates the two cases the
2026-08-21 split added, which is a plausible origin and is not offered as more than that. Nothing was
deleted to reconcile them and no id was reused.

### The relock — every citation now carries a stable `AC-<story>-<LETTER>` ID or says why not

**SM-23, `stories/ac-id-map.md`, completed 2026-08-22.** Nabil: *"Rely strictly on the stable
identifiers to guarantee tests never detach from requirements again."*

- **No positional AC label survives as a citation.** The four remaining occurrences of the
  `KAFF-1nn ACn` form in this file — `KAFF-104 AC2`, `KAFF-105b AC3`, `KAFF-105b AC6`, `KAFF-110 AC4` —
  are inside relock notes that record what a case *used to* cite. They are history, not citations.
- **Every stable ID cited in this file exists in the map.** Nothing was invented
  [Verified: 2026-08-22 — 177 distinct IDs cited, every one present in `stories/ac-id-map.md`].
- **Citations of the form `KAFF-1nn rule N`, `KAFF-1nn audit section` and `permission-matrix.md` are
  left alone, deliberately.** They cite a business rule, a story section or the matrix — **not an
  acceptance criterion** — and the map covers criteria only. Two that *read* as AC citations and were
  not have been corrected and annotated: `TC-1-164` and `TC-1-032`.
- **Three cases were found citing a criterion that says something other than what they test**, and
  every one is raised rather than relabelled quietly: `TC-1-040` (**F-29**), `TC-1-127` (**F-30**), and
  `TC-1-032`, whose criterion does not exist at all. **`TC-1-041`, `TC-1-043`, `TC-1-044` and
  `TC-1-085` were the same shape and were resolved earlier today**; their notes stand.
- **No case cites one of the four withdrawn historical labels**, so **nothing was retired by this
  relock** [Verified: 2026-08-22 — KAFF-101a's old `AC1`, KAFF-103's old `AC3`, KAFF-110's earlier
  `AC4` and KAFF-121's old `AC2` are asserted by no case; the three cases that once did — `TC-1-007`
  and `TC-1-222`, `TC-1-168`, `TC-1-086` — were reversed or demoted to `PENDING` by earlier rulings,
  each with its note]. **`TC-1-086` stays `PENDING Q35`**: KAFF-110 withdrew the mandatory-reason rule
  *to a question*, which is not the same as the rule being wrong, and QA-3 is still unasked.
- **52 of the map's 229 criteria are named by no case in this file** — down from **66** before the
  relock, because fourteen were already covered by cases citing a description instead of an ID and
  read as uncovered. **This is the relock's most useful by-product:** a positional label hides both
  kinds of gap, a stable ID separates *covered but mislabelled* from *genuinely uncovered*. The 52 are
  a coverage list for the next session, not a defect in the relock. The largest blocks
  [Verified: 2026-08-22]: **KAFF-104 nine of fourteen**, then KAFF-100, KAFF-105a and KAFF-112 with
  four each, KAFF-111 and KAFF-117 with three.

## How to read a case

```
**TC-1-000 · what it checks**
`AC-1nn-X` · P1 · Api · spec.md §9 · D-044 ruling 1
Given … When … Then …
*Fails if:* the one defect this case catches.
```

- **Priority** — P1 blocker · P2 major · P3 minor. See `qa/README.md`.
- **Layer** — `Domain` (`tests/Domain.Tests`) · `Api` (`tests/Api.Tests`, **real PostgreSQL**) ·
  `E2E` (Playwright).
- **The citation is the source of the expected result.** It is `spec.md` or a `decisions.md`
  D-number, never the implementation.
- **`Fails if:`** is the mutation from `qa/strategy.md` §5, written as the defect the case catches.
  **A case with no `Fails if:` line does not get written.** `decisions.md` **D-046** and **D-048** are
  four worked examples of a case that could not fail shipping here and certifying nothing.
- **`PENDING Qnn`** — the story is BLOCKED on that question and the criterion cannot be written
  without inventing the answer. Question numbers are `stories/questions-for-karim.md`, which is now
  the **one** register (backlog, 2026-08-21, action SM-4) — so the `Q-BA-` / `Q-UX-` prefixes this
  file used to carry are gone, and `qa/questions.md` **F-01** is closed with them.
  **A PENDING row is not a passing case. It is uncovered.**
- **`NO STORY`** — the ruling exists and the story has not been written yet. Different from PENDING:
  nothing is being invented, the expected result is citable, there is simply nothing to hang it on.
  Also uncovered.
- **`RETIRED`** — the case is kept, struck through, with the ruling that killed it. A retired case is
  not deleted, because a deleted case comes back as a "missing" one in the next session.

## What the two rounds of rulings did to these cases

**Six expected results reversed.** A reversed expected result is the most dangerous kind of stale
case: it passes against the behaviour the ruling removed, and certifies it.

| Case | Was | Is now | Ruling |
|---|---|---|---|
| `TC-1-152` | a duplicate phone is **refused** | it **warns**, names the client, and saves | D-049 §8 |
| `TC-1-153` | the unique index refuses a direct insert | `ix_clients_phone` is **non-unique**; the insert succeeds | D-049 §8 |
| `TC-1-168` | a phone edited into a collision is refused | it warns and proceeds | D-049 §8 |
| `TC-1-181` | an archived client's phone stays reserved | it warns, says *archived*, and saves | D-049 §8 |
| `TC-1-040` | HR's `/api/auth/me` lists **every project** | HR's payload lists **no project** | D-051 Q32 |
| `TC-1-075` … `TC-1-080` | a role change is **refused** while the user supervises | a role change **revokes every assignment** | **D-051 Q27, which reverses D-049 §6** |

**Two stories split, one superseded.**

| Was | Is now | Which cases went where |
|---|---|---|
| KAFF-101 | **101a** the API and the cookie · **101b** the screen | `TC-1-007`…`016`, `018` → 101a. `TC-1-017`, `TC-1-195` → 101b |
| KAFF-105 | **105a** identity and roles · **105b** the project list | `TC-1-042`, `045`, `046` → 105a. `TC-1-037`…`041`, `043`, `044` → 105b |
| KAFF-122 | **Superseded** → KAFF-416, slice 4 | `TC-1-175`…`179` retired; two rewritten under KAFF-120 |

**D-048 (2026-08-20) closed two of this file's own findings**, and six cases changed direction
because of it — see *"Three findings closed"* below. It is listed here rather than only there because
it landed **between** the first version of these cases and this revision, which is exactly the window
in which a stale expected result survives review.

**Four cases QA had flagged as needing a re-map, now fixed:** `TC-1-143` (its dependency on a BLOCKED
KAFF-109 is gone — F-20 closed), `TC-1-166` (the two-key mismatch is resolved — F-08 closed),
`TC-1-168` and `TC-1-174` (KAFF-121 now names both paths — F-09 stands as a code gap, not a story
gap).

**The token moved (D-050).** Every case that said *"a token is returned"* or *"with that same token"*
now says *"a cookie is set"* / *"with that same session cookie"*. That is not cosmetic: a case that
reads a token out of a response body cannot be written any more, because there is nothing to read.

## Story coverage index

| Story | Status | Cases | PENDING / NO STORY |
|---|---|---|---|
| KAFF-100 bootstrap the first Owner | **Ready** — Q31 answered | TC-1-001…006, 216…219 | 0 |
| KAFF-101a sign in, and the cookie | Ready | TC-1-007…016, 018, 220…230 | 0 |
| KAFF-101b the sign-in screen | BLOCKED Q33 | TC-1-017, 231 | 1 |
| KAFF-102 sign out | Ready | TC-1-019…022, 232 | 0 |
| KAFF-103 set first password | Ready | TC-1-023…029 | 0 |
| KAFF-104 reset forgotten password | **Ready** — Q38 answered | TC-1-030…036, 233, 234 | 1 |
| KAFF-105a `/api/auth/me` — identity | Ready | TC-1-042, 045, 046, 235, 236 | 0 |
| KAFF-105b `/api/auth/me` — projects | **Ready** — Q32 answered | TC-1-037…041, 043, 044 | 0 |
| KAFF-106 Owner creates a user | Ready | TC-1-047…059 | 0 |
| KAFF-107 HR role bound to HR department | Ready | TC-1-060…066 | 0 |
| KAFF-108 move a user between departments | Ready | TC-1-067…074 | 0 |
| KAFF-109 change a user's role | **Ready** — Q27 answered | TC-1-075…080, 237…239 | 1 |
| KAFF-110 deactivate a user | Ready | TC-1-081…090 | 0 |
| KAFF-111 a deactivated user's assignments | **Ready** — D-049 §5 | TC-1-091…092 | 0 |
| KAFF-112 reactivate a user | **Ready** — D-049 §5 | TC-1-093…098 | 0 |
| KAFF-113 assign a user to a project | Ready | TC-1-099…112 | 0 |
| KAFF-114 revoke an assignment | Ready | TC-1-113…120 | 0 |
| KAFF-115 project team panel | Ready | TC-1-121…128 | 0 |
| KAFF-116 how access was granted | Ready | TC-1-129…135 | 0 |
| KAFF-117 read the audit trail | **Ready** — D-049 §1 | TC-1-136…142 | 0 |
| KAFF-118 every change is audited | Ready | TC-1-143…150, 246, 247 | 0 |
| KAFF-119 register a client | **Ready** — D-049 §7, §8 | TC-1-151…159, 240, 241 | 0 |
| KAFF-120 individuals do not withhold | Ready | TC-1-160…166, 242 | 0 |
| KAFF-121 edit a client | Ready | TC-1-167…174 | 0 |
| ~~KAFF-122 corporate withholding~~ | **Superseded → KAFF-416** | TC-1-175…179 retired | — |
| KAFF-123 archive a client | Ready | TC-1-180…185 | 1 deferred |
| KAFF-124 list and search clients | Ready | TC-1-186…194 | 0 |
| — HR's project team screen | **NO STORY** — D-051 Q32 | TC-1-243…245 | 3 |
| — Arabic / RTL / i18n, cross-cutting | Ready | TC-1-195…201 | 0 |
| — the permission matrix, executed | Ready — **the gate** | TC-1-202…215 | 0 |
| — `ProjectCreate` / `ProjectFinancialsEdit` / `UserRead` | Ready — **new 2026-08-22** | TC-1-248…254 slice 1 · TC-1-255…257 slice 4 | 0 |

**Totals: 252 live cases · P1 195 · P2 50 · P3 8.**
**4 PENDING** — one on Q33 (`TC-1-017`), one on Q27's residue (`TC-1-079`), one on the reset link's
lifetime, which is the **story's** to settle and not Karim's (`TC-1-036`), and one on **Q35**
(`TC-1-086`, the reason on a deactivation — `qa/questions.md` QA-3, still unasked).
~~**3 NO STORY** — `TC-1-243`…`TC-1-245`, HR's team screen: Karim ruled it, nobody has written it.~~
**Corrected 2026-08-22: the story arrived.** `KAFF-115` carries HR's surface as `AC-115-H` and
`AC-115-I` and names the permission `ProjectTeamRead`. The three cases are relocked and are now
**BLOCKED on F-30** — the catalogue row does not exist — which is a smaller and much more tractable
gap than "nobody has written it".
**1 DEFERRED** to slice 4 (`TC-1-184`). **4 more written for slice 4** (`TC-1-255`…`TC-1-257`, and
`TC-1-242` already was). **2 DISPUTED, do not run** — `TC-1-040` (**F-29**) and, on its narrowed half,
`TC-1-127` (**F-30**). **5 RETIRED** (`TC-1-175`…`TC-1-179`), **and the relock retired none.**

**Down from fifteen PENDING to four.** The rulings closed twelve; `TC-1-086` **became** a PENDING on
2026-08-21, because it had been written asserting one of two plausible answers (SM-12). That is a
correction in the right direction — three of the four are not Karim's at all, and this one is.

**Cases expected to fail on first run** — the correct state for a case written before its code:
`TC-1-031`, `TC-1-091`, `TC-1-097`, `TC-1-098`, `TC-1-129`…`TC-1-133`, `TC-1-160`…`TC-1-165`,
`TC-1-168`, `TC-1-169`, `TC-1-174`, `TC-1-225`, `TC-1-230`, `TC-1-233`, `TC-1-243`…`TC-1-245`.

**None of them is a live defect in shipped code any more.** `TC-1-215` was the last one and **F-04
was fixed on 2026-08-21 (D-052 §1)** — see the regression note on that case. `TC-1-019` and
`TC-1-086` have left this list for a different reason: both asserted an outcome the rulings settle
the other way, and both were corrected rather than left to fail (SM-12). Everything below is
**unbuilt work**, in three groups:

| Group | Cases | Why they fail |
|---|---|---|
| ~~**F-04 — a live hole**~~ | ~~`TC-1-215`~~ | **CLOSED — D-052 §1, 2026-08-21.** `SiteExpenseConfirm` now names `Role.Finance` and `Role.TechnicalOffice` + Operations/Administrative [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`], so a site engineer parked there holds nothing. Pinned by `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense`] and `No_financial_permission_is_granted_to_a_bare_department` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department`], both green, 70/70 Domain. **The Api half of `TC-1-215` is still unrunnable** — no endpoint requires the permission until slice 6, KAFF-608 — which is unbuilt work, not a defect |
| **N5 — declared, not implemented** | `TC-1-019`, `TC-1-097`, `TC-1-225`, `TC-1-230`, `TC-1-233` | `User.SecurityStamp` rotates and **nothing compares it**. D-051 assigns the comparison to KAFF-101a |
| **D-049 §5 — ruled, not wired** | `TC-1-091`, `TC-1-098` | Deactivation and reactivation do not touch assignment rows |
| **Not built yet** | the remainder | The endpoint or the domain method does not exist. `TC-1-160`…`TC-1-165` and `TC-1-169` wait on **KAFF-416, slice 4**; `TC-1-243`…`TC-1-245` wait on a story nobody has written |

### Four findings closed since these cases were first written — do not re-open them

**`TC-1-215` is now a regression case too. F-04 was fixed on 2026-08-21 by D-052 §1** — the
`SiteExpenseConfirm` grant names `Role.Finance` and `Role.TechnicalOffice` + Operations /
Administrative [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`], and two Domain tests hold
it: `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense`]
for the row, and `No_financial_permission_is_granted_to_a_bare_department` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department`]
for the class of **twelve** money-touching permissions (eleven until `ProjectFinancialsEdit` joined
the list on 2026-08-22 — **F-34**). **Its Api half remains unrunnable until slice 6 (KAFF-608)**,
which is unbuilt work and not a defect. **QA-1 is answered; the ⚠F-04 cells in
`permission-matrix.md` are closed.**

**`TC-1-067`, `TC-1-068`, `TC-1-082`, `TC-1-084`, `TC-1-213` and `TC-1-214` are now regression cases,
not defect cases.** They previously said *"expected to fail on first run"* and cited **F-10** and
**F-11**. Both were **fixed on 2026-08-20 by D-048** — QA's own finding, and the most valuable one so
far: `IPermissionSubjectReader` now takes **only the user id** from the principal and reads role,
department, sub-department, client scope and liveness from the users table on **every** authorized
request, company-wide and project-scoped alike. D-048 verified it by reverting the fix and watching
five tests go red.

**F-18 is closed too** — `AuditRead` was an assumption and D-049 §1 made it a ruling.

**A case that reports a fixed defect is worse than no case**, because the next session reads it as a
live hole and either "fixes" it again or loses an hour proving it was never broken. The six cases keep
their numbers and their `Fails if:` lines, and now name the mechanism they guard.

---

# KAFF-100 · Bootstrap the first Owner — **Ready**, Q31 answered

> **D-051 Q31 chose shape B:** a one-time setup screen that appears **only when the users table is
> empty**, creates the Owner, and locks permanently afterwards. Karim's reason is an audit argument,
> not a convenience one — *"I do not want hidden database scripts. My name and account creation date
> must appear naturally in the Audit Trail from day one."* A seeded account has no actor, and the
> first row in the trail would name nobody.
>
> **`qa/questions.md` F-02 is closed by that ruling**, and the cases below are no longer written to
> hold under either shape. They are written against the screen — and against the two things D-051
> says the story has to answer: **the emptiness check must be atomic against a concurrent second
> request**, and *"locks permanently"* must mean the emptiness test, **not a flag anyone can clear**.
>
> **This is the most privileged endpoint that will ever exist here**: unauthenticated, and it mints
> the account that approves every movement of money in Kaff. Four of the nine cases below exist only
> because of that sentence.

**TC-1-001 · an empty database ends up with exactly one Owner**
`AC-100-A` · P1 · Api · D-051 Q31 · spec.md §9
Given a database with no users, when the setup request is posted with a named person's full name and
phone, then exactly one `User` exists, with `Role.Owner`, no department and `IsActive` true.
*Fails if:* the endpoint creates a second privileged account, or is present and creates nobody, which
leaves the system unopenable with no error saying so.

**TC-1-002 · the first Owner has no department**
`AC-100-A` · P1 · Api · spec.md §9 · KAFF-100 rule 2
Given the bootstrapped Owner, when the row is read, then `Department` and `OperationsSubDepartment`
are both null.
*Fails if:* the Owner is placed in a department — a department-only grant (`SiteExpenseConfirm`,
`PhotoPublish`) would then reach him by a path nobody wrote. `SiteExpenseConfirm` names its roles since **D-052 §1**, so `PhotoPublish` [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PhotoPublish`] is now the only such grant — and the last one. F-04 closed; this case still guards the shape.

**TC-1-003 · the creation of the most privileged account is not invisible**
`AC-100-A` · P1 · Api · D-051 Q31 · CLAUDE.md audit
Given the setup request succeeded, when audit records are read, then exactly one `Created` record
exists for that user, `ActorUserId` **is the newly created Owner's own id**, and the **after state
carries the person's name and the role**.
*Fails if:* the bootstrap path bypasses the audit interceptor — which is precisely what D-041 shows
can happen without anybody noticing, and it would defeat the only reason Karim chose this shape. Or
if the record names nobody, which is the state D-051 Q31 chose this shape to avoid.

**Corrected 2026-08-21 (SM-12).** This case previously asserted `ActorUserId` **is null** *"because
nobody was signed in"*. **D-051 Q31 exists precisely to prevent that record**
(`decisions.md:1774-1778`): Karim's reason for the setup screen is an audit argument — *"my name and
account creation date must appear naturally in the Audit Trail from day one"* — and the entry's own
next line is that *"a seeded account has no actor — the first row in the trail would name nobody."*
An unauthenticated request does not make the actor unknown here: the request creates the person it is
attributable to, so **the actor is the new Owner**. A null actor on this row is the defect, not the
expectation.

**TC-1-004 · the screen cannot be used twice**
`AC-100-B` · P1 · Api · D-051 Q31
Given a database that already contains an Owner, when the setup request is posted again, then it is
**refused**, no second user is created and no second audit record is written.
*Fails if:* the second call is silently ignored and answers 200 — an endpoint that reports success
for something it did not do is how a duplicate Owner gets discovered months later.

**TC-1-005 · the first account is a person, not a shared login**
`AC-100-G` · P2 · Api · CLAUDE.md audit · D-049 ruling 4
Given the setup form and the created row, when each is read, then a full name and a phone are present
and the username is none of `admin`, `root`, `kaff`.
*Fails if:* a shared `admin` login ships — every audit record then names an account rather than a
human, and the trail becomes unreadable at exactly the point it matters most.

**TC-1-006 · nobody but the holder ever knows the first credential**
`AC-100-H` · P1 · Api · D-049 ruling 4 · D-051 Q31
Given the setup request in which Karim typed his own password, when the response body, the application
log and the audit record are inspected, then none carries the password or its hash, and no other
account can sign in as him.
*Fails if:* the credential is echoed, logged, or generated server-side and displayed — under shape B
the whole non-repudiation argument rests on the first Owner's password never having been anyone
else's.

**QA-4 answered 2026-08-21 (D-052 §3), and the half this case left unasserted can now be asserted:**
**the first Owner is not forced to change the password he typed.** Nabil scoped D-049 ruling 4 rather
than excepting it — the forced change exists for an account created *for somebody else* with a
credential its creator knows, and nobody else has ever known this one. **`AC-100-F`** already says
so. **Add to this case:** when the first Owner signs in, he reaches the application and is **not**
routed to the change-password screen. Asserting the opposite would add a step Karim's stated reason
does not support. **This closes RSK-19's QA-4 residue only** — the rest of RSK-19 (atomicity, the permanent lock, the request shape) is untouched and still rides on `TC-1-216`…`TC-1-219`.

**TC-1-216 · a concurrent second request does not create a second Owner**
`AC-100-B` · P1 · Api, **real PostgreSQL** · D-051 Q31 (*"atomic against a concurrent second
request"*)
Given an empty database, when two setup requests are posted **simultaneously** with different names,
then exactly one `User` row exists afterwards, exactly one audit record exists, and the losing request
is refused.
*Fails if:* the emptiness check is a read followed by an unrelated write — a check-then-act on the
most privileged endpoint in the system, where the race creates a **second Owner nobody authorised**
and every audit record afterwards has two possible authors. The unique username index does not save
this: the two requests carry different usernames.

**TC-1-217 · any user at all closes the screen, not just an Owner**
`AC-100-B` · P1 · Api · D-051 Q31 (*"only when the users table is empty"*)
Given a database containing exactly one user who is **not** an Owner — a Finance user restored from a
backup, say — when the setup request is posted, then it is refused.
*Fails if:* the guard is written as *"is there an Owner"* rather than *"is the table empty"*. A
database that has lost its Owner row is a support incident, and this endpoint must not be its
back door.

**TC-1-218 · "locks permanently" is the emptiness test, not a flag**
`AC-100-B` · P1 · Api, **raw SQL** · D-051 Q31 (*"must mean the emptiness test, not a flag anyone
can clear"*)
Given a bootstrapped system, when every configuration row, feature flag and settings key the
application reads is enumerated, then **none of them re-opens the setup endpoint**; and when the
endpoint is called after any of them is flipped, it is still refused while a user exists.
*Fails if:* a `bootstrap_completed` flag exists. Anything one `UPDATE` can clear is not a lock, and
this is the one endpoint where "somebody with database access could" is not an acceptable answer —
they would not need the endpoint, but an application bug that clears the flag would.

**TC-1-219 · the setup endpoint mints an Owner and nothing else**
`AC-100-A` · P1 · Api · D-051 Q31 · D-044 ruling 1
Given an empty database, when the setup request supplies `Role.Finance`, then `Role.Client`, then a
`ClientId`, then a department, then each is refused or ignored and the account created is an Owner
with no department and no client.
*Fails if:* the request shape lets the caller choose the role. An unauthenticated endpoint that
accepts a role parameter is an unauthenticated user-creation endpoint, and it is reachable by anyone
who can reach the server before Karim gets there.

---

# KAFF-101a · Sign in, and the server sets the session cookie — Ready

> **Re-mapped from KAFF-101**, which split on 2026-08-21 (backlog; D-051 records the Architect's
> approval). `101a` is the API and the cookie and is `Ready`; the screen is `101b`, BLOCKED on Q33.
>
> **D-050 changed what a sign-in case may assert.** The access token is carried in an
> `HttpOnly; Secure; SameSite=Strict` cookie named `__Host-kaff-auth`. **The response body carries no
> token, in any field, under any name**, and `localStorage` / `sessionStorage` are prohibited for it.
> Every case below that used to read a token out of a response body now reads a `Set-Cookie` header —
> there is nothing else left to read.
>
> **D-049 rulings 2 and 3 closed the old `TC-1-018`**: 8 characters minimum, no forced complexity,
> 5 failures lock for 15 minutes, 30 minutes of idle ends the session, and a password change or a
> deactivation kills every session everywhere.

**TC-1-007 · a valid credential opens a session and hands JavaScript nothing**
`AC-101a-A` · P1 · Api · **D-050**
Given an active user with a password set, when that username and password are posted, then the
response body contains **no token in any field**, a `Set-Cookie` header sets `__Host-kaff-auth`, and
the next request carrying that cookie is authenticated.
*Fails if:* the endpoint issues a token without verifying the hash, issues one for an account with a
null `PasswordHash`, or returns the token in the body "for the SPA" — the third is the one D-050
exists to prevent and the only one that looks like a feature.

**TC-1-008 · a successful sign-in is on the record**
`KAFF-101a` audit section · P2 · Api · CLAUDE.md audit
Given a successful sign-in, when audit records are read, then one names the user, the time and the
request path.
*Fails if:* sessions start invisibly, so *"was he signed in when that extract was approved"* has no
answer.

**TC-1-009 · a wrong password, an unknown username and a locked account are indistinguishable**
`AC-101a-B` · P1 · Api · spec.md §9 · KAFF-101a rules 13, 14
Given an active user, when the correct username with a wrong password is posted, then a username that
does not exist, then the username of an account locked by five failures, then all three responses are
identical in status, body and `messageKey`.
*Fails if:* the API distinguishes them. *"No such user"* turns the login form into a directory of
Kaff's staff; *"locked"* tells an attacker both that the username is real and that their lockout
worked.

**TC-1-010 · and they take the same time**
`AC-101a-B` · P2 · Api · spec.md §9
Given the same attempts repeated, when response times are compared over a sample, then no
distinguishable envelope separates them.
*Fails if:* the unknown-user path returns before hashing, so timing enumerates accounts even though
the bodies match.

**TC-1-011 · a subcontractor cannot sign in**
`AC-101a-G` · P1 · Api · spec.md §9 *"record only, no login"*
Given a `User` with `Role.Subcontractor`, when sign-in is attempted, then it is refused with
`errors.auth.role_cannot_log_in` and the refusal is audited.
*Fails if:* a subcontractor record becomes a login, which puts an outside party inside the permission
model.

**TC-1-012 · a deactivated user cannot sign in, and their open session dies**
`AC-101a-H` · P1 · Api · spec.md §9 · D-048
Given a signed-in Finance user holding a live session cookie, when the Owner deactivates them, then
their very next request is refused, and a fresh sign-in with the correct password is refused too.
*Fails if:* deactivation stops new sessions but not existing ones, or the reverse. D-048 makes the
first half instant by re-reading the user row per request; the second half is this endpoint's.

**TC-1-013 · the session grants nothing by itself**
`AC-101a-K` · P1 · Api · spec.md §9 *"Role alone is insufficient"*
Given a valid session for a Site Engineer assigned to no project, when any `ProjectScoped` endpoint is
called for any project, then it is refused with `errors.auth.not_assigned_to_project`.
*Fails if:* authentication is mistaken for authorization — the single most common shape of this bug.
**Executable today** through `TestAuthHandler` without a login endpoint.

**TC-1-014 · the credential never leaves the database**
`AC-101a-L` · P1 · Api · CLAUDE.md audit · `[AuditRedacted]`
Given any successful and any failed sign-in, when the response body, the application log and the audit
records are inspected, then none contains the password, the hash or the security stamp.
*Fails if:* a hash reaches a log line, where it outlives every rotation policy the system has.

**TC-1-015 · a failed sign-in is recorded too, and so is the lockout**
`KAFF-101a` audit section · P2 · Api · CLAUDE.md audit · D-049 ruling 3
Given three failed attempts against one username and then five against another, when audit records are
read, then a record exists for each failure carrying the attempted username and a null actor id, and a
**separate record** records the lockout.
*Fails if:* only successes are recorded. Repeated failures against one username is the signal that
matters, and *"the account was locked at 14:02"* is the fact somebody will ask about.

**TC-1-016 · a portal client signs in and reaches only the portal**
`AC-101a-A, rule 16` · P1 · Api · spec.md §12 · D-035
Given a `Role.Client` user, when they sign in and then call an internal `ProjectRead` endpoint on their
own project, then sign-in succeeds and the internal call is refused with 403.
*Fails if:* `Role.Client` regains `ProjectRead`, which is the exact leak D-035 closed.
**Note:** *where* they sign in — same host or a separate one — is Q33 and belongs to `TC-1-017`.

**TC-1-018 · a temporary password has exactly one destination**
`AC-101a-F` · P1 · Api · **D-049 ruling 4**
Given a user whose password was set by the Owner and never changed, when they sign in and then call
any endpoint other than the change-password endpoint, then the request is refused with
`errors.auth.password_change_required`.
*Fails if:* the temporary password opens a working session. Karim's reason is the requirement: the
forced change protects *"the integrity of the audit trail (non-repudiation)"* — until it happens,
every action that account takes has two possible authors.
*(This case replaces the old `TC-1-018`, which was PENDING on password policy and session lifetime.
D-049 rulings 2 and 3 answered both; they are now `TC-1-226`…`TC-1-229`.)*

**TC-1-220 · the cookie carries every attribute that makes it safe**
`AC-101a-A, rule 1` · P1 · Api · **D-050**
Given a successful sign-in, when the `Set-Cookie` header is parsed, then the cookie is named
`__Host-kaff-auth` and carries **`HttpOnly`**, **`Secure`**, **`SameSite=Strict`**, **`Path=/`** and
**no `Domain`** — all five, on one header.
*Fails if:* any one is missing. `HttpOnly` absent hands the token to any injected script, which is the
whole of D-050's reasoning; `Secure` absent puts it on the wire in clear; `SameSite` absent or `Lax`
removes the only CSRF control there is; a `Domain` attribute or a path other than `/` makes the
`__Host-` prefix invalid and **a browser will reject the cookie outright**, so sign-in silently stops
working — this case fails loudly instead.

**TC-1-221 · the `__Host-` prefix is a constraint, not a naming convention**
`KAFF-101a rule 1` · P1 · Api · **D-050**
Given the cookie name in configuration (`JwtOptions.CookieName`), when it begins with `__Host-`, then
the server never emits it with a `Domain` attribute, never with a path other than `/`, and never
without `Secure`; and given a cookie of the same name **set by a neighbouring subdomain**, when it is
presented, then it is not accepted as a session.
*Fails if:* the prefix is treated as part of a name. D-050: the prefix *"closes cookie fixation from a
neighbouring host"* — a value only, with the attributes dropped, closes nothing.

**TC-1-222 · no response body anywhere carries a token**
`AC-101a-A, rule 2` · P1 · Api · **D-050**
Given every endpoint in slice 1 — sign-in, sign-out, `/api/auth/me`, change password, and every error
response from each — when each response body is inspected field by field, then none contains the
session token under any name, and `Session` has no field it could be placed in.
*Fails if:* one endpoint leaks it. D-050 makes the type shape the guarantee — *"the `Session` type has
no token field — the shape itself refuses the mistake"* — so this case also fails if that field is
added back, before anybody populates it.

**TC-1-223 · `SameSite=Strict` is the CSRF control and it is the whole of it**
`KAFF-101a rule 3` · P1 · Api + E2E · **D-050**
Given a signed-in session, when a state-changing request is issued **from a different site** (a
cross-origin form post and a cross-site `fetch`), then the browser attaches no cookie and the request
is refused as unauthenticated.
*Fails if:* the attribute is relaxed to `Lax` or `None`. D-050 is explicit: *"if that ever relaxes to
`Lax` or `None`, an anti-forgery token is required the same day"*. There is no anti-forgery token, so
this case is the only thing standing between a relaxation and a textbook CSRF.

**TC-1-224 · the `Authorization` header still authenticates, deliberately**
`KAFF-101a rule 4` · P2 · Api · **D-050**
Given a valid bearer token, when a request carries it in an `Authorization` header **and no cookie**,
then it is authenticated; and when a request carries **both** a header and a cookie, then the header
is used.
*Fails if:* the cookie path replaces the header path. D-050 keeps the header open for
service-to-service callers and the integration suite, neither reachable by an XSS payload in the SPA —
removing it would break the suite, and re-adding it later under time pressure is how the wrong
precedence gets written.

**TC-1-225 · a session carrying a stale security stamp is refused — `N5`**
`KAFF-101a rule 11` · P1 · Api · **D-049 ruling 2 · D-051 N5**
Given a signed-in user, when `User.SecurityStamp` is rotated (by a password change on another device,
or by `SetPasswordHash` directly), then the existing session's very next request is refused, because
the stamp claim it carries no longer matches the stored one.
*Fails if:* nothing compares them. **It does not today, and this is declared rather than
implemented:**
**Expected to fail on first run.** See `qa/risk-register.md` **RSK-06**.

**Scope this correctly, or the case tests nothing.** D-048 already makes **deactivation** and every
role or department change instant, by re-reading the user row on every authorized request — and D-048
explicitly **rejected** a stamp in the token as the fix for that, because it *"only closes liveness,
not the role and department staleness"*. What the stamp is for is the half the row re-read cannot see:
**a password change**, where the user row still says active with the same role. So this case must
exercise a rotation that changes **nothing else about the user**, or D-048's mechanism will refuse the
request for an unrelated reason and the case will pass without the comparison ever existing.

**And note the trap D-051 names:** a validation with a *"skip when the claim is absent"* fallback would
make this case pass while leaving the revocation bypassable. Run it against a session that carries the
claim **and** one that does not — the second must also be refused.

**TC-1-226 · five failures lock the account for fifteen minutes**
`AC-101a-C` · P1 · Api · **D-049 ruling 3**
Given an active user, when five consecutive wrong passwords are posted, then the sixth attempt fails
**even with the correct password**; and after fifteen minutes the correct password succeeds.
*Fails if:* the threshold or the window is a literal somebody rounded, or the lock is advisory and the
correct password still opens a session — which makes the counter decoration.

**TC-1-227 · a success resets the counter**
`AC-101a-D` · P2 · Api · D-049 ruling 3
Given four consecutive failures, when the correct password is posted, then sign-in succeeds and five
**further** failures are required before the account locks.
*Fails if:* the counter is never cleared, so a user who mistypes twice a month is locked out on an
ordinary Tuesday months later, and nobody can reproduce it.

**TC-1-228 · eight characters is enough, and nothing more is demanded**
`AC-101a-E` · P1 · Api · **D-049 ruling 3**
Given a password of exactly 8 lower-case letters with no digit and no symbol, when it is set and then
used to sign in, then both succeed; and given 7 characters, then it is refused.
*Fails if:* a complexity rule is added. Karim's reason is itself a requirement — *"so site workers
don't struggle to log in"* — and a rule that makes the site engineer write his password inside his
helmet is worse than a simple one he remembers.

**TC-1-229 · thirty idle minutes ends the session, and activity slides it**
`AC-101a-J` · P1 · Api · **D-049 ruling 2** · `JwtOptions.InactivityMinutes`
Given a session with no requests for 30 minutes, when a request is made, then it is refused; and given
a session used every 20 minutes for two hours, then it is still valid.
*Fails if:* the expiry is absolute rather than sliding — an engineer is signed out mid-daily-log — or
the number is typed into a handler rather than read from `JwtOptions.InactivityMinutes`, in which case
changing it means a code change and the two halves drift.

**TC-1-230 · a password change kills every other session**
`AC-101a-I` · P1 · Api · **D-049 ruling 2**
Given the same user signed in on two devices, when they change their password on one, then the session
on the other device is refused on its next request.
*Fails if:* only the changing device is affected. This is the half `TC-1-225`'s stamp check exists to
deliver, and it is the reason a stolen phone is recoverable at all. **Expected to fail on first run**,
for the same reason as `TC-1-225`.

---

# KAFF-101b · The sign-in screen, and where each role lands — **BLOCKED** Q33

> Split out of KAFF-101 on 2026-08-21. The old story asserted that a portal client signs in through
> the same endpoint as staff, citing sources that do not address it (`qa/questions.md` **F-22**).
> **D-051 Q33 answers half of it** — *"clients sign in at a different URL … their portal must be a
> completely isolated interface"* — and leaves open whether that is a separate deployment or the same
> API behind a second origin, which is exactly what a cookie's `Domain` and the CORS origin list turn
> on (D-050).

**TC-1-017 · PENDING Q33**
`AC-101b-A, AC-101b-B, AC-101b-C, AC-101b-D` · P1
Where a `Role.Client` signs in, and where each internal role lands after signing in. **Cannot be
written:** D-051 Q33 puts the portal on a different URL, and the residual question — separate
deployment or second origin — decides whether the session cookie can be shared at all, whether
`__Host-` still holds, and which origins `Kaff:AllowedOrigins` must name. Asserting either shape here
would fix an infrastructure decision in a test.
*(The Arabic/RTL rendering of the screen is `TC-1-195`, which is re-mapped to this story and can be
written, because it asserts direction and i18n rather than destination.)*

**Relock 2026-08-22.** This case cited `KAFF-101b AC — where each role lands`, which is a description
and not an identifier. The four criteria it would cover are, from `stories/ac-id-map.md`:
**`AC-101b-A` — *"a staff sign-in arrives at the staff shell"***, **`AC-101b-B` — *"a client never
sees the staff shell"***, **`AC-101b-C` — *"the portal is not discoverable from here"*** and
**`AC-101b-D` — *"HR lands on the team surface"***. **All four stay uncovered** — the case is still
`PENDING Q33` and relabelling it covers nothing. The IDs are here so the four read as uncovered by a
named question rather than as absent.

**TC-1-231 · the browser store stays empty**
`AC-101a-M` (the E2E half; KAFF-101b carries no matching AC) · P1 · E2E · **D-050**
Given a completed sign-in in a real browser, when `localStorage` and `sessionStorage` are inspected,
then neither contains a token or any part of one, and `document.cookie` does not expose the session
cookie either.
*Fails if:* the SPA caches anything token-shaped. Slice 0 shipped the `localStorage` version, so this
is a regression case as much as a new one — and `document.cookie` is in the assertion because an
`HttpOnly` cookie that is readable from script is not `HttpOnly`.

---

# KAFF-102 · Sign out — Ready

> **Q4 is answered (D-049 ruling 2): sessions are per-device.** Signing out on the site phone does not
> sign the same user out on the office computer. **D-051 N5 settles the mechanism**: routine
> per-device sign-out clears the cookie in that browser; the global kill — stolen phone, password
> change — rotates `User.SecurityStamp`. No session table. The Architect accepts the known limit
> rather than hiding it: **there is no way to revoke one *other* device**, so losing a phone means
> signing out everywhere.

**TC-1-019 · the session stops working in the browser, and the replay is accepted**
`AC-102-A, AC-102-B` · P1 · Api · spec.md §9 *"hiding UI elements is presentation, not security"* ·
**D-051 N5**
Given a valid session cookie, when the user signs out and then calls any authenticated endpoint
**from that browser**, then the request is refused with `errors.auth.not_authenticated` — the cookie
is gone (`AC-102-A`). And when the cookie value captured beforehand is **replayed by a tool that
ignores `Set-Cookie`**, within the inactivity window, then it **is still accepted** (`AC-102-B`).
*Fails if:* sign-out does not clear the cookie in the browser — or if the replay stops being accepted
without a decision, because that means somebody added per-session state and the trade was changed by
drift rather than by a ruling.

**Corrected 2026-08-21 (SM-12).** This case previously asserted that the **replay is refused**, and
called itself expected-to-fail. **D-051 N5 settles it the other way** (`decisions.md:1823-1829`):
routine per-device sign-out clears the cookie in that browser, the global kill rotates
`User.SecurityStamp`, and there is **no session table** — *"with no per-session identity there is no
way to revoke one **other** device"*, accepted as the right trade for a first-party SPA on one origin.
`AC-102-B` (`stories/slice-1-foundation/KAFF-102-sign-out.md:44-47`) now states the replay *"**is
still accepted**"* deliberately, and exists so that the day it stops being true, somebody decided it.

**The story is right and this case was stale — so the old instruction here, *"do not resolve it by
rewording the AC"*, is withdrawn.** It was written before the ruling and against an AC that did not
yet say this; the AC was not reworded to match the code, it was written to match a decision. Saying
so beats deleting the line, which would look like QA quietly dropped its own guard.

**This case no longer covers F-26.** The password-change kill is `TC-1-225`, `TC-1-230` and
`TC-1-233`, which are the ones still expected to fail. RSK-06 stays with them.

**TC-1-020 · sign-out is not deactivation**
`AC-102-E` · P2 · Api · spec.md §9
Given a user who has signed out, when they sign in again with the same credentials, then they are
signed in, `IsActive` was never changed, and an audit record exists for the sign-out.
*Fails if:* sign-out sets `IsActive` false, so leaving your desk locks you out permanently.

**TC-1-021 · a portal user can sign out and reveals nothing doing it**
`AC-102-F` · P2 · Api · spec.md §12
Given a signed-in `Role.Client`, when they sign out, then the same guarantees hold and no other
client's name, id or project appears in the response.
*Fails if:* the sign-out response echoes tenant context.

**TC-1-022 · my other device is untouched**
`AC-102-C` · P1 · Api · **D-049 ruling 2**
Given the same user signed in on two devices, when they sign out on one, then the other device's next
request still succeeds.
*Fails if:* sign-out rotates the security stamp. It is the obvious way to make `TC-1-019` pass and it
breaks this ruling in the same commit — which is why the two cases sit next to each other.
*(This case replaces the old `TC-1-022`, which was PENDING on whether sign-out is per-device.)*

**TC-1-232 · the cookie is cleared with matching attributes**
`AC-102-D, rule 3` · P1 · Api · **D-050**
Given a sign-out, when the response headers are read, then `__Host-kaff-auth` is cleared with the
**same name, the same `Path=/`, the same `Secure` and the same `SameSite=Strict`** it was set with,
and with an expiry in the past.
*Fails if:* any attribute differs. **A cookie cleared with different attributes is not cleared at
all** — the browser treats it as a different cookie, sets the new one, and leaves the live session
cookie in place. The user sees a sign-out screen and is still signed in, which is the worst possible
combination of appearance and fact.

---

# KAFF-103 · Change the temporary password on first sign-in — Ready

> **D-049 ruling 4 replaced the invitation.** Onboarding is *"a temporary password set by the Owner,
> which the user MUST change on first sign-in"* — site engineers often have no company email, so a
> reset link cannot be the primary path. **This reverses the old story's rule that the Owner must
> never hold a working credential**: the Owner does hold one, briefly and deliberately, and the forced
> change is what closes the window. Karim's reason is non-repudiation — until the change, every action
> that account takes has two possible authors.
>
> **The old `TC-1-024` (single-use invitation) is therefore gone**, and `TC-1-023` and `TC-1-025` are
> rewritten. Nothing about an invitation link survives; there is no invitation.

**TC-1-023 · a new user changes the temporary password and is then free**
`AC-103-A` · P1 · Api · **D-049 ruling 4**
Given the Owner created the account with a temporary password, when the user signs in and changes it,
then the rest of the system becomes reachable and an audit record of `Modified` names **the user** as
the actor.
*Fails if:* the record names the Owner — which would say the Owner set a credential the Owner does not
know, and is the exact inversion the ruling exists to prevent.

**TC-1-024 · the Owner's credential stops working the moment it is changed**
`AC-103-C` · P1 · Api · **D-049 ruling 4**
Given the Owner knows the temporary password, when the user has changed it, then a sign-in with the
temporary password is refused and is **indistinguishable from any other wrong password**.
*Fails if:* the old hash is retained as a fallback "in case they forget", which turns Karim's bounded
window into a permanent second credential for every account in Kaff.
*(This case replaces the old `TC-1-024`, "the invitation is single use". D-049 ruling 4 removed the
invitation — there is nothing to use twice.)*

**TC-1-025 · the creator never learns the chosen password**
`AC-103-G` · P1 · Api · spec.md §9 separation of duties · D-049 ruling 4
Given the Owner created the account, when the Owner reads the user record, the API response and the
audit trail **after** the change, then no field carries the new password or its hash in plain or
recoverable form.
*Fails if:* the Owner holds a working credential for every user in Kaff after the forced change too —
at which point the change is theatre and the trail is unreliable for everyone at once.

**TC-1-026 · changing a password rotates the security stamp**
`KAFF-103 rule 4` · P2 · Domain · slice 0 `User.SetPasswordHash`
Given a user with a stamp, when a password hash is set, then the stamp is a different value.
*Fails if:* the stamp is static, so nothing distinguishes a session opened before the change from one
opened after it. **Rotation alone is not the guarantee** — `TC-1-225` is the case that asserts
somebody compares it.

**TC-1-027 · a subcontractor record can never be given one**
`AC-103-H` · P1 · Domain + Api · spec.md §9
Given a `User` with `Role.Subcontractor`, when a password set is attempted, then it is refused with
`errors.identity.subcontractor_cannot_log_in`.
*Fails if:* the refusal lives only in the endpoint, so the next caller of `SetPasswordHash` creates a
subcontractor login.

**TC-1-028 · redaction is present, not absent**
`KAFF-103` audit section · P1 · Api · slice 0 `AuditRecord`, `KaffJson`
Given a password is set, when the record's before and after JSON are read, then `PasswordHash` and
`SecurityStamp` appear **marked redacted**, not omitted.
*Fails if:* the keys are dropped — an absent key reads as "unchanged", which is the opposite of what
happened.

**TC-1-029 · the current password is required, and eight characters is the whole rule**
`AC-103-D, AC-103-E` · P1 · Api · **D-049 rulings 3, 4** · KAFF-103 rule 5
Given a signed-in session, when a change is submitted with no current password, then with the wrong
one, then each is refused with `errors.auth.current_password_incorrect` and the stored hash is
unchanged; and when a new password of exactly 8 lower-case letters is submitted with the correct
current password, then it is accepted, while 7 characters is refused with
`errors.auth.password_too_short`.
*Fails if:* the current password is not demanded — an unattended signed-in phone then **is** a password
reset, and the session in question is by definition one nobody has yet proved they own. Or if a
complexity rule is added: Karim ruled none, *"so site workers don't struggle to log in"*.
*(This case replaces the old `TC-1-029`, which was PENDING on what makes a password acceptable and how
an invitation reaches an engineer with no email. Ruling 3 answered the first and ruling 4 removed the
second.)*

**Q37 stays open and does not block:** whether a temporary password expires if the person never signs
in. No case is written for an expiry, because none was ruled — and the consequence is on the record:
an account created and forgotten keeps a credential the Owner knows, indefinitely.

---

# KAFF-104 · Reset a forgotten password — **Ready**, Q38 answered

> **D-051 Q38:** the employee tells the office; **the Owner generates a temporary reset link**; it goes
> to their **registered phone by SMS or WhatsApp**. Karim's reason for not simply letting the Owner
> type a new password is the same as ruling 4's, applied consistently: *"that would compromise the
> non-repudiation of the Audit Trail"* — if the Owner sets a password the user keeps, every action
> that account takes has two possible authors.
>
> **The old `TC-1-035` is therefore rewritten rather than deleted.** It asserted that `UserManage` is
> not a route to reset somebody else's password. Half of that survives and is sharper: the Owner
> **triggers** a reset and must **not be able to set the credential**. The other half — that the Owner
> may not initiate one at all — is now wrong.
>
> **A reset is a password change, so it kills every active session** (D-049 ruling 3, and D-051 Q38
> says the story must settle exactly this). That is `TC-1-233`.

**TC-1-030 · a user recovers access, and the pair is on the record**
`AC-104-N` · P1 · Api · CLAUDE.md audit · D-051 Q38
Given a forgotten password, when the Owner generates a reset link and the user completes it with a new
password, then sign-in with the new password succeeds and **two** audit records exist — the generation
naming the Owner as actor, the completion naming the user.
*Fails if:* only the completion is recorded. The Owner initiating a reset for somebody else is the act
that needs a name against it, and without it a reset is indistinguishable from the user's own change.

**Relock 2026-08-22.** Cited `KAFF-104 AC — audit`, a description. The map's
**`AC-104-N` — *"both ends are audited"*** is this case's assertion in the criterion's own words —
two records, the generation and the completion. Matched on the title, not counted.

**TC-1-031 · the old sessions die**
`AC-104-E` · P1 · Api · **D-049 ruling 3** · slice 0 `User.SetPasswordHash`
Given a session opened before the reset, when any authenticated endpoint is called with it after the
reset completes, then the request is refused.
*Fails if:* recovering a password does not end the session of whoever had the old one — which is the
scenario the reset exists for. **Expected to fail on first run**; see `TC-1-225` and RSK-06.

**TC-1-032 · an unknown username reveals nothing, and nothing is sent**
`AC-104-?` — **CITATION UNRESOLVED, 2026-08-22** · P1 · Api · spec.md §9
Given a username that does not exist, when a reset is requested for it, then the response is identical
to the response for a real username and **no message is dispatched anywhere**.
*Fails if:* the reset path becomes an account-enumeration oracle, or sends a link to a number an
attacker supplied. Note that under D-051 Q38 the Owner is the one requesting, so the enumeration risk
is smaller and the **wrong-number** risk is larger — see `TC-1-234`.

**Relock 2026-08-22 — this citation could not be resolved and was not guessed.** The case cited
`KAFF-104 AC2`, which `stories/ac-id-map.md` translates to **`AC-104-B` — *"the link works once"***.
That is single use, and this case asserts non-enumeration; the two are unrelated, so the case had
already drifted off its label. **No KAFF-104 row in the map describes an unknown username revealing
nothing** — the fourteen criteria are the Owner never holding the credential, single use, expiry, a
second link killing the first, sessions dying, no re-change, deactivated users, no phone / no reset,
lockouts, nothing but the credential, subcontractors, Owner-only, the token never being readable
later, and both ends audited. **Finding for the BA: either the criterion this case tests exists under
a title that does not describe it, or KAFF-104 has no criterion for it and the case is uncovered
work.** The case is left runnable and its assertion is unchanged; only the label is marked. **Do not
close this by pointing it at whichever criterion looks nearest** — that is the defect the map exists
to end.

**TC-1-033 · a deactivated user cannot reset back in**
`AC-104-G` · P1 · Api · spec.md §9 · D-049 ruling 5
Given a user whose `IsActive` is false, when a reset is requested, then no link is issued and the
response is still indistinguishable from the success case.
*Fails if:* password recovery is a route back in for somebody who has left Kaff. A returning employee
comes back through reactivation, with zero assignments (KAFF-112).

**TC-1-034 · a reset changes nothing but the credential**
`AC-104-J` · P1 · Api · spec.md §9
Given a Finance user in `Department.Finance` with two active project assignments, when they complete a
reset, then role, department and both assignments are unchanged.
*Fails if:* the reset path rebuilds the user and silently drops assignment rows or resets the
department.

**TC-1-035 · the Owner starts a reset and never holds the result**
`KAFF-104` permissions section · P1 · Api · **D-051 Q38**
Given the Owner holding `UserManage`, when the Owner generates a reset link for another user, then it
succeeds and the link's target credential is **never displayed to the Owner, returned in the response,
or written to a log**; and when the Owner attempts to set that user's password directly, then no such
endpoint exists.
*Fails if:* the reset endpoint returns the link, or a new password, to the caller. Karim's whole reason
for the link is that the Owner must not end up knowing the credential — a link echoed into the Owner's
browser is the thing he ruled against, arriving as a convenience.
*(This case replaces the old `TC-1-035`, which refused the Owner any role in a reset at all. D-051 Q38
gives him the initiating role and takes away the credential.)*

**TC-1-036 · PENDING — the link's lifetime and single use**
`KAFF-104 rules — link mechanics` · P1
How long a reset link stays valid, and whether it is single-use. **Cannot be written:** D-051 Q38 says
in as many words that *"the story must decide link lifetime, single-use, and what happens to active
sessions on reset"*. The third is answered by D-049 ruling 3 and is `TC-1-233`; the first two are
**not Karim's** — they are the story's and the Architect's, and a number asserted here would become the
product's policy by accident. **Ask the BA, not Karim.**
*Fails-if it were written anyway:* nothing, which is the point — a case asserting "24 hours" cannot
fail against a system that chose 24 hours for no reason.

**TC-1-233 · a reset kills every active session**
`AC-104-E` · P1 · Api · **D-049 ruling 3 · D-051 Q38**
Given a user signed in on three devices who has forgotten their password, when the Owner generates a
reset and the user completes it, then **all three** sessions are refused on their next request,
including the device the reset was completed on if it held an older session.
*Fails if:* only the sessions on other devices die, or none do. The scenario this exists for is a
person who lost control of a device — a reset that leaves the thief signed in is a reset that
accomplished nothing, and D-049 ruling 3 already says a password change ends every session everywhere.
**Expected to fail on first run** — the mechanism is `TC-1-225`'s stamp comparison, which does not
exist.

**TC-1-234 · the link goes to the registered phone and nowhere else**
`KAFF-104` rules · P1 · Api · **D-051 Q38**
Given a reset generated for a user, when the dispatch is inspected, then it is sent to
`User.Phone` **as stored**, and a phone number supplied in the request is ignored or refused; and given
a user with no phone on file, then the reset cannot be generated at all.
*Fails if:* the destination is a request parameter. An Owner-triggered reset that accepts a
destination is a way to redirect any employee's credential to any number — and `User.Email` is
optional while `User.Phone` is not, which is why Karim chose SMS or WhatsApp in the first place.

---

# KAFF-105a · `GET /api/auth/me` — identity and roles — Ready

> **Re-mapped from KAFF-105**, which split on 2026-08-21 with the Architect's approval (D-051).
> `105a` returns identity and roles and is unblocked; `105b` returns the project list. The split
> exists because *"the frontend needs it to know anyone is signed in at all"* — after D-050 the
> session lives in a cookie the page cannot read, so **this endpoint is the only thing that can tell
> the UI whether anybody is signed in.**
>
> **The route is `GET /api/auth/me`, not `/api/me`.** `AuthService` already names it, and a mismatch
> would surface as a 404 in a browser rather than as a failing test.

**TC-1-042 · a portal client is bounded to two permissions**
`AC-105a-F` · P1 · Api · spec.md §12 · D-035
Given a `Role.Client` user, when `GET /api/auth/me` is called, then exactly `PortalRead` and
`PortalApprove` are returned and nothing else.
*Fails if:* an internal permission is returned to a portal user, which is D-035 reopening.

**TC-1-045 · the endpoint and the catalogue cannot drift**
`AC-105a-E` · P2 · Api · D-012
Given a permission added to `PermissionCatalogue` with a grant for Finance, when a Finance user calls
the endpoint, then it appears in the response **with no change to the endpoint's code**.
*Fails if:* the payload is built from a hand-written list — a second copy of the permission model,
which is the drift D-012 designed the catalogue as data to prevent.

**TC-1-046 · the payload carries no money**
`KAFF-105a` money section · P1 · Api · spec.md §12 · §6.1
Given the response contract, when it is inspected, then it carries no balance, contract value, cost or
margin field.
*Fails if:* a convenient "project value" is added for the dashboard. It would reach the portal, where
§12 forbids it absolutely, and HR, where D-044 ruling 2 does — in one step, from one field.

**TC-1-235 · the route is `/api/auth/me`, and no token is in it**
`AC-105a-B, rule 2` · P1 · Api · **D-050**
Given a signed-in session, when `GET /api/auth/me` is called, then it answers 200 and the body carries
**no session token in any field**; and when `GET /api/me` is called, then it is a 404 rather than a
second implementation.
*Fails if:* a token, or any part of one, is returned "so the SPA can hold it" — D-050 removed the field
from `Session` precisely so the shape refuses that. Or if both routes answer, in which case the two
will drift and only one will be tested.

**TC-1-236 · an unauthenticated call is refused, not answered with an empty profile**
`KAFF-105a rule 10` · P1 · Api · spec.md §9
Given no session cookie and no `Authorization` header, when the endpoint is called, then it is refused
with `errors.auth.not_authenticated` — not 200 with a null user.
*Fails if:* it answers an empty profile. **The frontend has to distinguish "signed out" from "signed in
as nobody", and only one of those is a real state** — an empty 200 makes an authorization bug look
like a logged-out user, and the SPA would render a shell for a session that does not exist.
*(The forced-password-change signal on this endpoint is `AC-105a-E`, covered by `TC-1-018`, which
asserts it for every endpoint rather than only this one.)*

---

# KAFF-105b · `GET /api/auth/me` — the project list — **Ready**, Q32 answered

> **D-051 Q32 answers the question this was blocked on, and reverses `TC-1-040`.** *"HR may only see
> the project name and the list of assigned engineers … If the main project dashboard contains
> financial data, HR must be routed to a separate 'Project Team' tab/screen that contains zero
> financial details."*
>
> **Note the shape of the answer: a separate surface, not a filtered view** — the same pattern §12
> uses for the client portal, and the same reason. *A filtered view leaks the first time somebody
> adds a field.* So HR does **not** get projects in this payload; HR gets a different screen, which
> has no story yet and whose cases are `TC-1-243`…`TC-1-245`.
>
> `qa/questions.md` **F-13** and **F-03**, and QA-2, are closed by this ruling.

**TC-1-037 · an engineer sees his own seniority, per project**
`AC-105b-A` · P1 · Api · spec.md §9 · D-044 ruling 5
Given a Site Engineer who is Supervisor on project A and Junior on project B, when the endpoint is
called, then both assignments are listed, each with its own level.
*Fails if:* one seniority is reported for the person — the screen then says something false the first
time an engineer is Junior on one site and Supervisor on another.

**TC-1-038 · and the permissions follow the level**
`AC-105b-A` · P1 · Api · spec.md §9
Given the same user, when the payload is read, then project A carries `DraftSubmit` and project B does
not.
*Fails if:* the permission set is computed per user rather than per project, so a supervisor on one
site can submit on every site.

**TC-1-039 · the Owner's reach needs no assignment row**
`AC-105b-B` · P1 · Api · D-010 · D-044 ruling 3
Given the Owner with no `ProjectAssignment` rows at all, when the endpoint is called, then every
project that exists is listed, each marked as reached by Owner-global rather than by assignment.
*Fails if:* the Owner's project list is empty — the reach exists in the policy, is invisible to the
frontend, and navigation shows Karim nothing.

**TC-1-040 · HR's payload names no project at all — `DISPUTED F-29`, do not run**
`AC-105b-C` · P1 · Api · **D-051 Q32** · D-044 ruling 2
Given a `Role.Hr` user with no assignment rows, when the endpoint is called, then **no project is
listed** — not by name, not by code, not by id, and not as a count — and `ProjectAssignmentManage`
appears as a company-level capability rather than against a project.
*Fails if:* the project list is populated for HR. **This case reverses its own previous expected
result**, which read *"every project is listed carrying `ProjectAssignmentManage`"* — that was
`qa/questions.md` F-13, and Karim's answer is that HR's project surface is a separate screen, not this
payload. A test still asserting the old result would certify the leak the ruling closed.

**Relock 2026-08-22 — the label resolves and the case contradicts it. Raised as `F-29`, not fixed.**
The citation `KAFF-105b AC — HR` is a description; the map's HR criterion is
**`AC-105b-C` — *"HR gets names, and only names"***. **The criterion says the opposite of this case.**
`AC-105b-C` [Verified: 2026-08-22 @ `stories/slice-1-foundation/KAFF-105b-api-me-project-list.md:80-85`]
reads *"all three are listed with name and code"*, with no value, cost, margin, balance, budget, status
or client field and no `ProjectRead`; **`AC-105b-E`** then flags each entry as reachable
through the Project Team surface only. This case asserts **no project, "not by name, not by code, not
by id, and not as a count"**.
**D-051 Q32 verbatim** (`decisions.md:1787`): *"HR may only see the project name and the list of
assigned engineers."* That is a **bounded** payload, not an empty one — the ruling's *"separate
surface, not a filtered view"* is about the **dashboard**, not about `/api/auth/me`. So the reversal
this case performed on 2026-08-21 over-read the ruling by one step, and the story did not follow it.
**QA does not settle which is right.** The case is marked `DISPUTED` and **the Verifier must not run
it** until the BA and Nabil reconcile the criterion and the case — a case whose expected result is the
strict opposite of its own criterion certifies something either way it lands.

**TC-1-041 · and HR sees nothing financial**
`AC-105b-C` · P1 · Api · D-044 ruling 2 · D-051 Q32
Given the same HR user, when the payload is read, then `ProjectRead` is absent and no treasury, gate
or movement permission appears anywhere in it.
*Fails if:* HR gains a grant. Global reach is only safe while the capability half stays narrow, and
this is the assertion that pins it.

**Relock 2026-08-22.** Cited `KAFF-105b AC — HR`. **`AC-105b-C`** carries this half verbatim —
*"the payload … contains **no** value, cost, margin, balance, budget, status or client field"* and
*"`ProjectRead` appears nowhere in my permissions"*
[Verified: 2026-08-22 @ `stories/slice-1-foundation/KAFF-105b-api-me-project-list.md:84-85`].
**This half of the criterion is not disputed** — only `TC-1-040`'s half is. This case is runnable.

**TC-1-043 · client Y appears nowhere in a client X payload**
`AC-105b-G` · P1 · Api · spec.md §12
Given client X has one project and client Y has another, when X's portal user calls the endpoint, then
only X's project is named and Y's project appears in no field — **not as a name, not as an id, not in a
count**.
*Fails if:* a total or a count leaks the existence and size of another client's work.

**Relock 2026-08-22 — the label was wrong before today, and the map caught it.** This case cited
`KAFF-105b AC3`, which the map translates to **`AC-105b-C` — *"HR gets names, and only names"***. This
case is about a **portal client's** payload, not HR's. The criterion it actually asserts is
**`AC-105b-G` — *"a portal client is bounded"*** — matched from the map's *Criterion* column, not
deduced from a position. One of the thirty-one drifted cases, still drifted after the last re-derivation.

**TC-1-044 · a revoked assignment disappears**
`AC-105b-H` · P2 · Api · slice 0 `ProjectAssignment.IsActive`
Given an assignment revoked this morning, when the endpoint is called, then that project is not listed.
*Fails if:* the query ignores `RevokedAt`, so a revocation is cosmetic. **This matters more after
D-051 Q27**, which makes a role change revoke every assignment at once — the payload is where the user
finds out.

**Relock 2026-08-22 — the label was wrong before today.** This case cited `KAFF-105b AC6`, which the
map translates to **`AC-105b-F` — *"the surfaces are separate types, not one type filtered"***. This
case tests neither surface typing nor filtering. Its criterion is **`AC-105b-H` — *"a revoked
assignment disappears"***, whose title is this case's title verbatim. Matched from the map, not counted.

---

# KAFF-106 · The Owner creates a user — Ready

**TC-1-047 · the Owner creates a Finance user**
`AC-106-A` · P1 · Api · D-044 ruling 1
Given the Owner, when a user is created with role Finance and department Finance, then the user exists,
is active and has `PasswordHash` null.
*Fails if:* the endpoint creates the account with a password, which puts a working credential for a
colleague in the Owner's hands.

**TC-1-048 · and it cannot be used yet**
`AC-106-A` · P1 · Api · slice 0 `User`
Given the newly created user, when a sign-in is attempted, then it is refused.
*Fails if:* a null hash is treated as "any password matches", which is a real and classic bug.

**TC-1-049 · the record answers "who gave this person the treasury"**
`AC-106-A` · P1 · Api · CLAUDE.md audit
Given the creation, when the audit record is read, then it is `Created` on `User`, actor = the Owner,
before null, and the **after state carries the role and the department**, with `PasswordHash` and
`SecurityStamp` redacted.
*Fails if:* the after state omits role or department — the record then proves an account was made and
cannot say what it was allowed to do.

**TC-1-050 · nobody else can, whatever their role**
`AC-106-B` · P1 · Api · D-044 ruling 1
Given Finance, Technical Office, Site Engineer, Marketing, HR and a portal Client in turn, when each
attempts to create a user, then every attempt is refused with 403 `errors.auth.forbidden`.
*Fails if:* any second role can mint logins — whoever can set a department can hand out
project-assignment power, which makes this the most privileged operation in the system.

**TC-1-051 · and every refusal is logged with its reason**
`AC-106-B` · P2 · Api · `PermissionAuthorizationHandler`
Given each refusal above, when the application log is read, then each carries the permission, the user
and the `PermissionDecision`.
*Fails if:* "Forbidden" ships with no explanation, which is the failure mode that becomes a two-hour
support call.

**TC-1-052 · HR cannot mint a login**
`AC-106-C` · P1 · Api · D-044 ruling 1
Given `Role.Hr`, which holds `ProjectAssignmentManage`, when a user creation is attempted, then it is
refused with 403.
*Fails if:* user creation is folded into `ProjectAssignmentManage` — HR would then grant itself the
financial visibility Karim's ruling denies it.

**TC-1-053 · and HR's real job still works**
`AC-106-C` · P1 · Api · D-044 rulings 1, 3
Given the same HR user one call later, when they assign an existing user to a project, then it
succeeds.
*Fails if:* the refusal above was implemented by removing HR's grant rather than by scoping it.

**TC-1-054 · an Operations user must carry a sub-department**
`AC-106-D` · P2 · Domain + Api · spec.md §9
Given the Owner, when a user is created in Operations with no sub-department, then it is refused with
`errors.identity.operations_requires_sub_department`.
*Fails if:* an Operations user with no sub-department exists — grants written against
Operations/Administrative then match or miss unpredictably.

**TC-1-055 · and nobody else may carry one**
`AC-106-D, rule 5` · P2 · Domain + Api · spec.md §9 *"Only Operations subdivides"*
Given the Owner, when a Marketing user is created with a sub-department, then it is refused with
`errors.identity.sub_department_only_for_operations`.
*Fails if:* a Finance user can be parked in Operations/Administrative by sub-department alone.

**TC-1-056 · a portal client cannot be given a department**
`AC-106-E` · P1 · Domain + Api · spec.md §12 · D-035
Given the Owner, when a `Role.Client` user is created with `Department.Hr`, then it is refused with
`errors.identity.external_role_cannot_hold_department` and no user is created.
*Fails if:* an outsider inherits a department-only grant, skipping both the project check and the
client check — one of the two paths that nearly leaked the portal.

**TC-1-057 · nor can a subcontractor**
`AC-106-E, rule 6` · P1 · Domain + Api · spec.md §9 · D-035
Given the Owner, when a `Role.Subcontractor` user is created with any department, then it is refused.
*Fails if:* the check covers `Role.Client` only, leaving the same hole open under a different role.

**TC-1-058 · a client user names a client, and nobody else does**
`AC-106-F` · P1 · Domain + Api · spec.md §12
Given the Owner, when a `Role.Client` user is created with no client id it is refused with
`errors.identity.client_user_requires_client`; when a Finance user is given a client id it is refused
with `errors.identity.non_client_user_cannot_carry_client`.
*Fails if:* a portal user with a null `ClientId` exists — `ProjectAccessPolicy` would then evaluate
their reach against nothing.

**TC-1-059 · usernames do not collide, in either case**
`AC-106-G` · P2 · Api · slice 0 unique index
Given a user `nabil` exists, when `NABIL` is created, then it is refused and the existing user is
untouched.
*Fails if:* the uniqueness is case-sensitive, so two accounts exist for one person and the trail has
two actors for one human.

*(`AC-106-J` — Arabic RTL at 390px — is TC-1-197.)*

---

# KAFF-107 · The HR role is bound to the HR department — Ready

**TC-1-060 · HR in HR is fine**
`AC-107-A` · P2 · Domain + Api · D-044 ruling 2
Given the Owner, when a user is created with `Role.Hr` and `Department.Hr`, then the user is created.
*Fails if:* the constraint is written as "HR may hold no department", which would make HR
uncreatable.

**TC-1-061 · HR anywhere else is refused, including nowhere**
`AC-107-B` · P1 · Domain + Api · D-044 ruling 2
Given the Owner, when a `Role.Hr` user is created in Finance, then Marketing, then
Operations/Administrative, then with **no department at all**, then all four are refused with
`errors.identity.hr_role_requires_hr_department`.
*Fails if:* the null case is allowed — an HR user with no department is exactly as unconstrained as
one in the wrong department.

**TC-1-062 · an existing HR user cannot be moved out**
`AC-107-C` · P1 · Domain + Api · D-044 ruling 2
Given an HR user in HR, when the Owner moves them to Operations/Administrative, then the move is
refused and the department is unchanged.
*Fails if:* creation is guarded and the move is not — the same hole reached one step later.

**TC-1-063 · HR reaches nothing financial**
`AC-107-D` · P1 · Api · D-044 ruling 2
Given a signed-in `Role.Hr` user, when endpoints requiring `ProjectRead`, `SiteExpenseConfirm`,
`TreasuryPostProject`, `FinancialMovementApprove`, `AccountManage` and `PhotoPublish` are called in
turn, then all six are refused with 403.
*Fails if:* any of the six is reachable. Two of them — `SiteExpenseConfirm` and `PhotoPublish` — are
granted **by department with no role named**, which is why the department constraint is a second
mechanism and not a belt-and-braces nicety.

**TC-1-064 · a Marketing user moved to HR gains nothing**
`AC-107-E` · P1 · Domain · D-044 ruling 2 · D-035
Given a `Role.MarketingSales` user, when they are placed in `Department.Hr`, then they do **not** hold
`EmployeeManage`.
*Fails if:* the HR grant is written against the department again. It was, until 2026-08-20, and a test
asserted the opposite of this — this case is the reversal.

**TC-1-065 · the refusal is legible in both languages**
`KAFF-107` i18n section · P2 · E2E · CLAUDE.md i18n
Given the refusal above surfacing on screen, when `ar.json` and `en.json` are read, then
`errors.identity.hr_role_requires_hr_department` exists in both.
*Fails if:* the key renders as itself on screen — which it does today: the domain error exists from
D-044 and has no catalogue entry.

**TC-1-066 · no department-only grant can match `Role.Hr`**
`KAFF-107 rule 5` · P1 · Domain · D-044 ruling 2 · D-035
Given `PermissionCatalogue`, when every grant with a `Department` and no `Role` is enumerated, then no
`Role.Hr` user can satisfy any of them, because `User.Create` pins HR to `Department.Hr` and no
department-only grant names HR.
*Fails if:* a future department-only grant is written against `Department.Hr` — this case fails the
moment it is, which is the point of writing it now.

---

# KAFF-108 · Move a user between departments — Ready

**TC-1-067 · a move takes effect on the next request, on the same session**
`AC-108-A` · P1 · Api · spec.md §9 · **D-048**
Given a user in Operations/Technical holding a **session opened before the move**, when the Owner moves
them to Operations/Administrative and they call a `SiteExpenseConfirm` endpoint on that same session,
then the request **succeeds**.
*Fails if:* the department used for the decision comes from the token rather than the database.
**This is a regression case, not a defect case.** It was expected to fail until 2026-08-20: the
department was read from a token claim and never revalidated (**F-10**). **D-048 fixed it** —
`IPermissionSubjectReader` takes only the user id from the principal and reads role, department,
sub-department, client scope and liveness from the users table on **every** authorized request. The
case now guards the fix rather than reporting the hole, and it fails again the day somebody puts a
department claim back in the token to save a query.

**TC-1-068 · and the reverse takes effect just as fast**
`AC-108-B` · P1 · Api · spec.md §9 · **D-048**
Given a user in Operations/Administrative holding `SiteExpenseConfirm`, when the Owner moves them to
Marketing, then their next request to that endpoint on the same session is refused with 403.
*Fails if:* a stale department keeps granting a permission after the person has left the department —
**the dangerous direction**, and the one worth naming separately: the granting direction failing is an
inconvenience, the revoking direction failing is a permission nobody can take away. Regression cover
for **F-10** (D-048); the upstream unit is `A_stale_department_claim_grants_nothing`.

**TC-1-069 · department rules are re-applied on a move**
`AC-108-C` · P2 · Domain + Api · spec.md §9
Given a Finance user, when the Owner moves them to Operations with no sub-department, then it is
refused with `errors.identity.operations_requires_sub_department`.
*Fails if:* validation lives in the create path only.

**TC-1-070 · and in the other direction**
`AC-108-C` · P2 · Domain + Api · spec.md §9
Given the same user, when they are moved to Marketing carrying a sub-department, then it is refused
with `errors.identity.sub_department_only_for_operations`.
*Fails if:* a stale sub-department survives a move out of Operations.

**TC-1-071 · HR stays in HR**
`AC-108-D` · P1 · Domain + Api · D-044 ruling 2 · KAFF-107
Given an HR user, when the Owner moves them to Finance, then it is refused and the department is
unchanged.
*Fails if:* KAFF-107's constraint is bypassed by the move endpoint.

**TC-1-072 · nobody but the Owner can move anyone**
`AC-108-E` · P1 · Api · D-044 ruling 1
Given HR, then Finance, then Technical Office, when each attempts a department move, then each is
refused with 403.
*Fails if:* a second role can change a department — which is a way of granting capability without
touching a role, and is why this endpoint is `UserManage`-privileged.

**TC-1-073 · assignments survive the move**
`AC-108-F` · P2 · Api · slice 0 `ProjectAssignment.Create`
Given a Technical Office user assigned to two projects, when the Owner moves them between departments,
then both assignments are still active and unchanged.
*Fails if:* the move rebuilds the user and drops assignment rows. `ProjectAssignment` constrains the
role, not the department, so nothing here should touch them.

**TC-1-074 · the move is on the record, with both sides**
`KAFF-108` audit section · P1 · Api · CLAUDE.md audit
Given a successful move, when the audit record is read, then it is `Modified` on `User`, actor = the
Owner, before and after both carry department and sub-department, and `ChangedProperties` names them.
*Fails if:* only the after state is recorded — the record then cannot say what capability the person
gave up, only what they gained.

---

# KAFF-109 · Change a user's role — **Ready**, Q27 answered — **and it reverses D-049 §6**

> **Read this before touching a case in this section.** D-049 ruling 6 said a role change is
> **refused** while the user is an active Supervisor, because auto-removing one *"leaves a
> construction site headless"*. **D-051 Q27 says the opposite and is the answer:** moving a Site
> Engineer to the Technical Office **automatically revokes every project assignment they hold —
> Supervisor and Junior alike** — because *"their direct link to the site must be severed
> automatically to prevent lingering responsibilities"*. If they are still needed, HR re-assigns them
> in the new role.
>
> **Both rulings weigh the same two risks and land on opposite sides. The second wins.** D-051 says
> the reversal is left visible in `spec.md` §9 rather than edited away, *"because a rule that changed
> direction is exactly the kind a future session will 'correct' back if it only sees the current
> state."* The same applies here: **`TC-1-080` is written as the reversal, not as a fresh case**, and
> any case asserting a refusal on supervision is now wrong.
>
> ~~⚠ **`stories/KAFF-109` has not been rewritten yet.** Its rule 2 and `AC-109-A`–`AC-109-C` still say
> *refused*, and it is still marked `BLOCKED — Q27`.~~ **This warning was stale and is withdrawn,
> 2026-08-22.** The story is `Status: Ready`
> [Verified: 2026-08-22 @ `stories/slice-1-foundation/KAFF-109-change-a-users-role.md:3`] and its
> criteria carry the reversal: `AC-109-A` *"a supervisor comes off site, **and is not refused**"*,
> `AC-109-B` *"junior assignments go too"*, `AC-109-C` *"the mirror case"*. **The cases below
> now cite the story's own criteria rather than a D-number standing in for a story that had not caught
> up.** Kept struck through rather than deleted — SM-29: an undated claim about the state of another
> file is how this warning outlived the day it was true.
>
> `qa/questions.md` **F-06** is closed by D-049 §6's own note and stands: `User` has no `ChangeRole`
> and should not get one — the rule needs assignment rows the entity cannot reach, so **the guard is
> handler work.**

**TC-1-075 · a role change takes effect immediately**
`AC-109-F` · P1 · Api · kickoff §3 · D-048
Given a Finance user holding a **session cookie issued before the change**, when the Owner changes
their role to Technical Office and they call a `TreasuryPostProject` endpoint with that same cookie,
then it is refused with 403.
*Fails if:* the role is trusted from the token. **Both scopes must be covered by this case**, and
the company-wide half is the one that was broken: until **D-048** the re-read lived in
`ProjectAccessPolicy`, which is never called for a `CompanyWide` permission (**F-11**). D-048 moved it
to `IPermissionSubjectReader`, ahead of the scope split, so role comes from the database either way.
**Regression cover** — and it matters more after D-051 Q27, because a role change now also strips every
assignment, so a stale role grants reach the database has already revoked.

**TC-1-076 · the department rules are re-applied**
`AC-109-G` · P1 · Domain + Api · D-044 ruling 2
Given a Marketing user in `Department.Marketing`, when the Owner changes their role to `Role.Hr`
without moving their department, then it is refused with
`errors.identity.hr_role_requires_hr_department`.
*Fails if:* the role change path skips `ValidateDepartment`, producing an HR user outside HR by a route
KAFF-107 does not cover.

**TC-1-077 · only the Owner may**
`AC-109-I` · P1 · Api · D-044 ruling 1
Given HR, which can staff projects, when a role change is attempted, then it is refused with 403.
*Fails if:* HR can promote somebody to Owner, which grants `FinancialMovementApprove` on every project
in Kaff. **This is sharper after Q27**, because a role change now also strips assignments — an HR user
who could change roles could clear a project's entire team through the role field.

**TC-1-078 · the before-state is in the trail**
`AC-109-J` · P1 · Api · CLAUDE.md audit
Given a successful role change, when the audit record is read, then it names the actor, the **old**
role and the new role.
*Fails if:* the old role is omitted — the trail then cannot answer *"who could approve that extract on
the day it was approved"*, which is the single question this record exists for.

**TC-1-079 · PENDING Q27 (residual)**
`KAFF-109 rule 10` · P1
Whether a role may be changed **to** `Role.Client` or `Role.Subcontractor` at all. **Cannot be
written:** D-051 Q27 answered the assignment half and said nothing about this one. An internal login
becoming a portal identity is a different kind of account, not a different role — `Role.Client`
requires a `ClientId` and forbids a department, and the two shapes are incompatible, so *"convert
them"* and *"refuse it"* are both plausible and only one is Karim's.

**TC-1-080 · a role change revokes every assignment, and is not refused**
`AC-109-A` · P1 · Api · **D-051 Q27, reversing D-049 §6**
Given a Site Engineer who is **Supervisor on project A** and Junior on project B, when the Owner
changes their role to Technical Office, then the change **succeeds**, both assignment rows are revoked,
and the user reaches neither project on their next request.
*Fails if:* the change is **refused** with `errors.identity.role_change_blocked_by_supervision`. That
was the expected result under D-049 §6 and it is now wrong — a test still asserting it passes against
the behaviour Karim reversed and certifies it. It also fails if only the Supervisor row is revoked and
the Junior row survives, which is the gap D-051 names explicitly: *"blocking on Supervisor alone let a
Junior-only engineer through, leaving rows `ProjectAssignment.Create` would refuse to create."*
*(This case replaces the old `TC-1-080`, which was PENDING because refuse / revoke / downgrade were all
plausible. Q27 chose revoke.)*

**TC-1-237 · a Junior-only engineer is not a special case**
`AC-109-B` · P1 · Api · **D-051 Q27** (*"whether Supervisor or Junior"*)
Given a Site Engineer who is **Junior on three projects and Supervisor on none**, when the Owner changes
their role, then the change succeeds and **all three** rows are revoked.
*Fails if:* the revocation is written against `AssignmentLevel.Supervisor`, which is what the reversed
ruling made natural to write. The engineer then keeps three rows the domain would refuse to create for
his new role — the integrity gap the original question was raised about, surviving one level down.

**TC-1-238 · the mirror case is the same mechanism**
`AC-109-C` · P1 · Api · **D-051 Q27**
Given a Finance user with two `Standard` assignments, when the Owner changes their role to
`Role.SiteEngineer`, then the change succeeds and both `Standard` rows are revoked — because
`ProjectAssignment.Create` refuses `Standard` **for** a Site Engineer, so leaving them would leave rows
the system would never have allowed.
*Fails if:* the revocation only fires when the **old** role is `SiteEngineer`. D-051: *"the mirror case
… is covered by the same mechanism: revoke on any role change, then re-assign."* One rule, both
directions.

**Relock 2026-08-22 — three cases, three criteria, matched on title.** `TC-1-080`, `TC-1-237` and
`TC-1-238` all cited `KAFF-109 AC — Q27`, one description standing for three rules. The map separates
them and the titles are the case titles: **`AC-109-A` — *"a supervisor comes off site, and is not
refused"***, **`AC-109-B` — *"junior assignments go too"***, **`AC-109-C` — *"the mirror case"***.
None had a citation before today.

**TC-1-239 · the revoked rows stay as history, and the whole act is one story in the trail**
`AC-109-J, AC-109-K` · P1 · Api · **D-051 Q27** · CLAUDE.md audit · slice 0 `ProjectAssignment.Revoke`
Given a role change that revokes two assignments, when the assignment rows and the audit records are
read, then **no row was deleted** — each carries `RevokedAt` and `RevokedByUserId` — and the `Modified`
record on `User` and the two `Modified` records on `ProjectAssignment` share one `CorrelationId`.
*Fails if:* the rows are deleted, in which case *"who was allowed to act on the day that extract was
approved"* has no answer; or if the revocations are written with no correlation, in which case the
trail shows an engineer losing three projects with nothing saying why. D-051: *"`ProjectAssignment.Revoke`
already does the right thing and keeps the row as history."*

---

# KAFF-110 · Deactivate a user — Ready

**TC-1-081 · access ends on the next request — project-scoped**
`AC-110-A` · P1 · Api · spec.md §9 · kickoff §3
Given a Finance user who has just succeeded on a `ProjectRead` request, when the Owner deactivates
them and they repeat the identical request with the same token, then it is refused and no state was
changed by the attempt.
*Fails if:* deactivation waits for token expiry.

**TC-1-082 · access ends on the next request — company-wide**
`AC-110-A` · P1 · Api · spec.md §9 · **D-048**
Given a Marketing user who has just succeeded on a **`CompanyWide`** endpoint (`ClientManage`), when
the Owner deactivates them and they repeat the identical request on the same session, then it is
refused.
*Fails if:* the liveness check lives only where a project is resolved. **It did until 2026-08-20** —
`ProjectAccessPolicy` was invoked only for project-scoped permissions, so a deactivated Finance user
kept `TreasuryPostCompany` and could still move company money until their token expired (**F-11**,
found by QA reading the handler against the catalogue). **D-048 fixed it and verified it by reverting
the fix and watching five tests go red.** This case is now the regression guard, and it is still the
highest-value case in this story — the upstream unit is
`A_deactivated_user_loses_company_wide_permissions_too`.

**TC-1-083 · a deactivated Owner is not exempt**
`AC-110-B` · P1 · Api · spec.md §9
Given two Owner accounts A and B, when A deactivates B and B calls a project-scoped endpoint with a
token issued before the deactivation, then B is refused.
*Fails if:* the Owner branch of the access policy skips the active check — which it did once already
(kickoff §3), and the Owner is the account that most needs to be revocable.

**TC-1-084 · and not exempt company-wide either**
`AC-110-B` · P1 · Api · spec.md §9 · D-044 ruling 1
Given the same deactivated Owner B, when B calls the `UserManage` endpoint, then B is refused.
*Fails if:* a departed Owner keeps the ability to create users — including creating **another
Owner**, which is a permanent back door into every permission in the system. Regression cover for
**F-11** (D-048); the upstream unit is `A_deactivated_owner_cannot_administer_users`.

**TC-1-085 · they cannot come back in through the front or the side**
`AC-110-D`, `AC-110-E` · P1 · Api · spec.md §9
Given a deactivated user, when they attempt to sign in with the correct password, and separately
request a password reset, then both are refused and neither reveals that the account was deactivated
rather than never existing.
*Fails if:* the refusal distinguishes "deactivated" from "unknown", which tells an outsider who used
to work at Kaff.

**Relock 2026-08-22 — one label, two criteria.** This case cited `KAFF-110 AC4`, which the map
translates to **`AC-110-D` — *"they cannot sign in again"***. That covers the sign-in half only. The
reset half is **`AC-110-E` — *"and cannot recover their way back in"*** (the old `AC4b`, an ordinary
criterion and not a child of `AC-110-D` — the map's trap 1). Both are live criteria, so this case is
relocked to both rather than retired. **`AC-110-E` had no citation anywhere in this file before
today**, so a criterion that reads as covered was covered only by accident.

**Not the withdrawn `AC4`.** `stories/ac-id-map.md` names KAFF-110's *earlier* `AC4` as a historical
label with no stable ID. This case asserts live rules (`AC-110-D` and `AC-110-E`, both in the map), so
it is a relabel and not a retirement. Recorded because the distinction is the whole of the map's
*What this file does not cover* section.

**TC-1-086 · PENDING Q35 — is a deactivation refused when no reason is given?**
`AC-110-G` · P1 · Api · CLAUDE.md audit *"why where the flow requires it"*
**No case is written.** The two candidate assertions are *"the request is refused and no user is
deactivated"* and *"the deactivation succeeds and the record simply carries no reason"*, and nothing
states which.

**Corrected 2026-08-21 (SM-12).** This case asserted the **refusal** and called itself
expected-to-fail. `AC-110-G`
(`stories/slice-1-foundation/KAFF-110-deactivate-a-user.md:75-78`) says only that the reason is
*"stored **when it is given**"* — which describes a reason that may be absent, not one that is
demanded. So the case asserted one of two plausible answers against a story that states the other,
and **a case that encodes a guess is worse than a `PENDING`**, which is this file's own rule and the
reason nothing here was ever resolved by QA.

**Unblocked by Q35** — QA's own `qa/questions.md` **QA-3**, still unasked: *"When you switch
someone's account off, should the system make you type why, or is that optional?"* If the answer is
mandatory, the refusal comes back here **and** `AC-110-G` needs rewording; if optional, this case retires
and `TC-1-087` carries the whole rule. **Do not resolve it by reading `User.Deactivate`** — it takes
only a timestamp, which is the current implementation and not an answer. **Risk:** RSK-07.

**TC-1-087 · and the reason is stored verbatim**
`AC-110-G` · P1 · Api · spec.md §7
Given a deactivation performed with a reason in Arabic, when the audit record is read, then `Reason`
holds that text exactly, unmodified and unnormalised.
*Fails if:* the reason is truncated, transliterated, or cleared by the interceptor before the save
succeeds (a known gap, kickoff action A4).

**TC-1-088 · the record survives the person**
`AC-110-H, rule 6` · P1 · Api · CLAUDE.md audit
Given a deactivated user who wrote twelve audit records, when those records are read, then all twelve
still name them, the user row still exists, and **no endpoint deletes a user**.
*Fails if:* a delete path exists — a deleted user makes every record they wrote unreadable, retroactively.

**TC-1-089 · only the Owner may**
`AC-110-I` · P1 · Api · D-044 ruling 1
Given HR, then Finance, when each attempts a deactivation, then each is refused with 403.
*Fails if:* HR can switch off the Owner.

**TC-1-090 · twice is refused, not absorbed**
`AC-110-J` · P2 · Domain + Api · slice 0 `User.Deactivate`
Given an already-inactive user, when the Owner deactivates them again, then it is refused with
`errors.identity.user_already_inactive`.
*Fails if:* the second call silently succeeds and overwrites `DeactivatedAt`, losing the real date
somebody left.

---

# KAFF-111 · A deactivated user's assignments — **Ready**, D-049 §5

> **D-049 ruling 5 answers it:** leavers are never deleted, they **stay on historical project teams**,
> and a returning employee gets a new password and **zero project assignments**. So deactivation must
> revoke the active rows and leave the revoked rows in place as the history Karim wants to keep.
>
> **D-049 states the code gap in the same breath, and it is why both cases below are expected to fail:**
> *"`User.Deactivate` does not touch assignment rows, and `Reactivate` does not either — so today a
> returning employee would come back **with every assignment still active**, which is the opposite of
> the ruling. `ProjectAssignment.Revoke` already does exactly that; nothing calls it on deactivation."*

**TC-1-091 · deactivation revokes the active assignments and keeps the rows**
`AC-111-A, AC-111-B` · P1 · Api · **D-049 ruling 5**
Given a user with active assignments on projects A and B, when the Owner deactivates them with a
reason, then both rows are **revoked** — `RevokedAt` and `RevokedByUserId` populated — **and neither
row is deleted**, so both still appear in each project's history.
*Fails if:* the rows are left active, which is today's behaviour and the opposite of the ruling; or if
they are deleted, which destroys the record of who could act on the day something was approved.
**Expected to fail on first run.** It is handler work — the `User` entity cannot reach assignment rows.
*(This case replaces the old `TC-1-091`, which was PENDING because revoking and not revoking were both
defensible. Ruling 5 chose revoking.)*

**Relock 2026-08-22 — one label, two criteria.** Cited `KAFF-111 AC — D-049 §5`, a D-number standing
in for an identifier. The two halves are **`AC-111-A` — *"the assignments are revoked"*** and
**`AC-111-B` — *"and the rows survive"***, and this case asserts both in one Given/When/Then.
Neither had a citation before today. **Three of KAFF-111's remaining criteria are named by no case in
this file** [Verified: 2026-08-22], which the relock made visible and which is a coverage gap for the
BA and QA to close, not something to paper over by pointing more cases at them.

**TC-1-092 · access is refused regardless of the row**
`KAFF-111 rules 1, 2, 3` · P1 · Api · spec.md §9 · KAFF-110 · KAFF-115
Given a deactivated user with an assignment row on project A that was **not** revoked — a row left
behind by the gap above, or by a partial failure — when they call a project endpoint on A, then they
are refused **regardless of the row**.
*Fails if:* access is decided by the assignment alone, so switching an account off does not switch off
its reach. This is the case that keeps `TC-1-091`'s defect from being an access defect as well as a
data one.

---

# KAFF-112 · Reactivate a user, who comes back with nothing — **Ready**, D-049 §5

> **D-049 ruling 5:** a returning employee gets **a new password and zero project assignments**. HR
> re-staffs them deliberately. Both halves of the old PENDING are answered.

**TC-1-093 · a returning user is the same user**
`AC-112-A` · P1 · Api · spec.md §2 *"A second copy of any master record is a defect"*
Given a deactivated user with twelve audit records naming them, when the Owner reactivates them, then
it is the same user id and all twelve records still resolve to it.
*Fails if:* reactivation creates a new row, giving one human two actors in the trail.

**TC-1-094 · the username stays reserved while the account is off**
`KAFF-112 rule 4` · P2 · Api · slice 0 unique index
Given a deactivated user `mona`, when a new user `mona` is created, then it is refused.
*Fails if:* the unique index is filtered on `IsActive`, so a departed colleague's name can be reused
and the audit trail acquires two people with one identity.

**TC-1-095 · reactivating an active user is refused**
`AC-112-G` · P2 · Domain + Api · slice 0 `User.Reactivate`
Given an active user, when the Owner reactivates them, then it is refused with
`errors.identity.user_already_active`.
*Fails if:* the call silently succeeds and clears `DeactivatedAt` on somebody who never left.

**TC-1-096 · only the Owner may**
`AC-112-H` · P1 · Api · D-044 ruling 1
Given HR, then Finance, when each attempts a reactivation, then each is refused with 403.
*Fails if:* anyone but the Owner can restore access to a departed employee.

**TC-1-097 · the old password does not come back**
`AC-112-D` · P1 · Api · **D-049 ruling 5 · D-051 N5**
Given a user deactivated six months ago, when the Owner reactivates them, then the old password does
**not** sign them in, a new temporary password is required, and `SecurityStamp` has been **rotated** by
the reactivation.
*Fails if:* the old credential works. And note the specific gap D-051 N5 names: *"`Reactivate` does not
rotate the stamp … reactivation is the one path that should rotate and does not."* A returning
employee's old sessions — and anyone holding that old credential — would otherwise be live again the
moment the account comes back. **Expected to fail on first run.**
*(This case replaces the old `TC-1-097`, PENDING on whether the old password still works.)*

**TC-1-098 · they come back with zero assignments**
`AC-112-B` · P1 · Api · **D-049 ruling 5**
Given a user who was deactivated while assigned to eight projects, when the Owner reactivates them,
then they hold **no active assignment at all**, the eight revoked rows are untouched, and they reach
none of the eight until HR assigns them again.
*Fails if:* reactivation restores the revoked rows. Karim ruled that a returning employee comes back
with nothing and is re-staffed deliberately — auto-restoring eight projects to somebody who has been
away six months is precisely the *"lingering responsibility"* D-051 Q27 uses the same words to refuse.
*(This case replaces the old `TC-1-098`, PENDING on whether assignments return.)*

---

# KAFF-113 · Assign a user to a project — Ready

**TC-1-099 · HR staffs a project it was never assigned to**
`AC-113-A` · P1 · Api · D-044 ruling 3
Given `Role.Hr` with no assignment rows anywhere, when a Technical Office user is assigned to a
project, then the assignment is created.
*Fails if:* HR is required to be assigned in order to assign — on a brand-new project nobody is
assigned, so nobody could ever make the first assignment.

**TC-1-100 · and still cannot open that project**
`AC-113-B` · P1 · Api · D-044 rulings 2, 3
Given the same HR user one call later, when a `ProjectRead` endpoint on that project is called, then
it is refused with 403.
*Fails if:* HR gains `ProjectRead` — global reach then becomes global visibility, and the only thing
holding the line is the absence of one grant.

**TC-1-101 · HR's reach stops at a project that does not exist**
`AC-113-C` · P1 · Api · D-044 ruling 3
Given `Role.Hr`, when an assignment is attempted against a project id that names nothing, then the
request is refused with 403 and **not a 500**.
*Fails if:* global reach is implemented as "skip the check", so a typo'd identifier becomes an
authorization success.

**TC-1-102 · the same engineer, two seniorities**
`AC-113-D` · P1 · Api · D-044 ruling 5
Given a Site Engineer, when HR assigns them Supervisor on project A and Junior on project B, then both
rows exist with their own levels.
*Fails if:* the level is stored on the user, so the second assignment overwrites the first.

**TC-1-103 · and the capability follows the project**
`AC-113-D` · P1 · Api · spec.md §9
Given the same engineer, when `GET /api/auth/me` is read, then `DraftSubmit` is present on A and absent on B.
*Fails if:* the minimum-level check is not evaluated per project.

**TC-1-104 · seniority is refused where §9 does not put it**
`AC-113-E` · P1 · Domain + Api · spec.md §9
Given a Finance user, when HR assigns them with level Supervisor, then it is refused with
`errors.identity.assignment_level_not_applicable`.
*Fails if:* a Finance user can be made a Supervisor, acquiring a seniority §9 attaches to the Site
Engineer role alone.

**TC-1-105 · and a Site Engineer must have one**
`AC-113-E` · P2 · Domain + Api · spec.md §9 · slice 0 `ProjectAssignment.Create`
Given a Site Engineer, when they are assigned with level `Standard`, then it is refused.
*Fails if:* an engineer lands at `Standard`, where he is neither a junior who drafts nor a supervisor
who submits — an assignment that grants nothing and looks like it grants something.

**TC-1-106 · a portal client is not assignable**
`AC-113-F` · P1 · Domain + Api · spec.md §12 · D-035
Given a `Role.Client` user, when HR attempts to assign them to a project, then it is refused with
`errors.identity.client_is_not_assignable`.
*Fails if:* a portal user acquires an assignment row and reaches the project through the internal path
instead of the client-of-project path.

**TC-1-107 · nor is a subcontractor**
`AC-113-F` · P1 · Domain + Api · spec.md §9
Given a `Role.Subcontractor` user, when an assignment is attempted, then it is refused.
*Fails if:* the check names `Role.Client` only.

**TC-1-108 · nobody else can staff a project**
`AC-113-G` · P1 · Api · D-044 rulings 1, 3
Given Finance, then Technical Office, then a **Supervisor Site Engineer already assigned to the
project**, when each attempts to assign a user to it, then every one is refused with 403.
*Fails if:* being on a project is treated as permission to staff it — the supervisor case is the one
that catches this, because his assignment makes the reach check pass.

**TC-1-109 · an inactive user is not assignable**
`AC-113-H` · P2 · Api · spec.md §9 · KAFF-110
Given a deactivated user, when HR attempts to assign them, then it is refused.
*Fails if:* an assignment resurrects a switched-off account onto a live project team.

**TC-1-110 · no duplicate active assignment**
`AC-113-I` · P2 · Api · slice 0 partial unique index
Given a user already assigned to project A, when HR assigns them to project A again, then it is
refused.
*Fails if:* two active rows exist for one user on one project and the level lookup becomes
non-deterministic.

**TC-1-111 · but re-assignment after revocation is legal**
`AC-113-I` · P2 · Api · slice 0 `ProjectAssignment`
Given the first assignment was revoked, when HR assigns the same user to the same project, then a new
active row is created.
*Fails if:* the unique index covers revoked rows too, so nobody ever returns to a project they left.

**TC-1-112 · the assignment is on the record, per project**
`KAFF-113` audit section · P1 · Api · CLAUDE.md audit
Given an assignment created by HR, when the audit record is read, then it is `Created` on
`ProjectAssignment` with `ProjectId` set and actor = the HR user.
*Fails if:* `ProjectId` is null, so the trail cannot be filtered per project — which is what
`AuditRecord.ProjectId` exists for.

---

# KAFF-114 · Revoke a project assignment — Ready

**TC-1-113 · access ends on the next request**
`AC-114-A` · P1 · Api · spec.md §9
Given a Site Engineer assigned to project A holding a valid token who has just succeeded on a project
request, when HR revokes the assignment and the engineer repeats the identical request with the same
token, then it is refused with `errors.auth.not_assigned_to_project`.
*Fails if:* the assignment is cached in the token or in memory rather than read per request.

**TC-1-114 · the row survives, fully populated**
`AC-114-B` · P1 · Api · slice 0 `ProjectAssignment`
Given a revoked assignment, when the table is read, then the row is present with `AssignedAt`,
`AssignedByUserId`, `RevokedAt` and `RevokedByUserId` all populated.
*Fails if:* revocation deletes the row, and six months later the trail cannot answer who was allowed to
act on the day an extract was approved.

**TC-1-115 · re-assignment is legal**
`AC-114-C` · P2 · Api · slice 0 `ProjectAssignment`
Given a user whose assignment to A was revoked, when HR assigns them to A again, then a new active row
is created and the revoked row is untouched.
*Fails if:* the revoked row is mutated back into an active one, destroying the record of the gap.

**TC-1-116 · twice is refused**
`AC-114-D` · P2 · Domain + Api · slice 0 `ProjectAssignment.Revoke`
Given an already-revoked assignment, when it is revoked again, then it is refused with
`errors.identity.assignment_already_revoked`.
*Fails if:* the second call overwrites `RevokedAt` and `RevokedByUserId` with a later actor.

**TC-1-117 · nobody else can**
`AC-114-E` · P1 · Api · D-044 rulings 1, 3
Given Finance, then Technical Office, then a Supervisor Site Engineer on that project, when each
attempts a revocation, then each is refused with 403.
*Fails if:* an engineer can remove a colleague from a project he is on.

**TC-1-118 · revocation is not deletion**
`AC-114-F` · P1 · Api · CLAUDE.md append-only
Given any assignment, when the API surface is enumerated, then no route deletes one.
*Fails if:* a `DELETE /assignments/{id}` is added for tidiness. The shape of this case — assert the
absence — is the correct test for anything on the out-of-scope list.

**TC-1-119 · the revocation is on the record with both fields named**
`KAFF-114` audit section · P1 · Api · CLAUDE.md audit
Given a revocation, when the audit record is read, then it is `Modified` on `ProjectAssignment`,
`ProjectId` set, `ChangedProperties` names `RevokedAt` and `RevokedByUserId`, actor = Owner or HR.
*Fails if:* the revocation writes no record, so somebody loses access with nobody named as having done
it.

**TC-1-120 · revoking the last person on a project is allowed**
`KAFF-114 rule 7` · P2 · Api · spec.md §9 — absence noted deliberately
Given a project with exactly one assignment, when it is revoked, then it succeeds and the project has
an empty team.
*Fails if:* somebody invents a minimum-team rule nobody asked for, which would block a legitimate
handover mid-project.

---

# KAFF-115 · The project team panel — Ready

**TC-1-121 · the Owner is not on every team**
`AC-115-A` · P1 · Api · D-010 · D-044 ruling 3
Given an Owner never assigned to project A and one Technical Office user who has been, when A's team
panel is read, then it contains exactly one member and the Owner is not in it.
*Fails if:* the panel is built from "everybody the access check would let in" — Karim then appears on
every project team in the company.

**TC-1-122 · nor is HR**
`AC-115-B` · P1 · Api · D-044 ruling 3
Given an HR user who assigned everyone on project A and holds no assignment row, when the panel is
read, then HR is not in it.
*Fails if:* the same mistake for the second row-less actor. There are now two, and there was one when
the panel was first specified.

**TC-1-123 · seniority shows, per project**
`AC-115-C` · P2 · Api · D-044 ruling 5
Given a Site Engineer who is Supervisor on A and Junior on B, when A's and B's panels are read, then
each shows the level for that project.
*Fails if:* the panel joins to a per-user level, contradicting the ruling on the first screen it is
visible.

**TC-1-124 · revoked members are gone**
`AC-115-D` · P2 · Api · slice 0 `ProjectAssignment.IsActive`
Given a member whose assignment was revoked, when the panel is read, then they are absent.
*Fails if:* the panel shows history, which is the audit trail's job and makes the current team
unreadable.

**TC-1-125 · an empty team says so**
`AC-115-F` · P3 · E2E · spec.md §4.5 (same principle)
Given a project with no assignments, when the panel renders, then `team.empty` is displayed.
*Fails if:* a blank area or a phantom row appears — expected on a new project, and the reason HR has
global reach in the first place.

**TC-1-126 · a client cannot read it**
`AC-115-G` · P1 · Api · spec.md §12
Given a `Role.Client` user whose client owns project A, when A's team panel is requested, then it is
refused with 403.
*Fails if:* `PortalRead` is treated as equivalent to `ProjectRead`. §12 lists what the client sees and
the team is not on the list.

**TC-1-127 · HR cannot read the *in-project* panel it staffed**
`AC-115-H` · P1 · Api · D-044 ruling 2 · **D-051 Q32**
Given the HR user who created every row on project A, when **the in-project panel** — the one gated on
`ProjectRead` — is requested, then it is refused with 403.
*Fails if:* the in-project panel endpoint is given a softer permission "because HR obviously needs it".
The answer is a second surface, not a softer gate on this one.

**Relock 2026-08-22 — the citation resolves and the case as written was half wrong. Raised as `F-30`.**
Cited `KAFF-115 rule 5`, which is not an identifier. The map's criterion is
**`AC-115-H` — *"HR reads the team, and reaches nothing else"*** — and **the criterion says HR
*reads* the team.** [Verified: 2026-08-22 @
`stories/slice-1-foundation/KAFF-115-project-team-panel.md:82-87`]: *"When I open the Project Team
screen for project A, then I see its name, its code and its members with their roles and levels"*, with
no money field, **and** the project dashboard refused with 403. Rule 5 names the mechanism —
two surfaces, and HR's is the new narrow **`ProjectTeamRead`**.
**So this case's title was true of the wrong endpoint.** It has been narrowed to the in-project panel,
which `AC-115-H` still refuses HR; **the half it was missing — that HR reads its own surface — is
`TC-1-243`**, which was sitting under **NO STORY** while this case asserted the opposite of the same
criterion. Two cases, one criterion, pointing in opposite directions, and neither cited it.
🟡 **`ProjectTeamRead` does not exist in the catalogue** [Verified: 2026-08-22 — the identifier appears
in four story files and in **no file under `src/`**]. `AC-115-H` cannot be executed until it does, and
under **SM-30** the row and its test land together. **F-30**, owner Architect and Backend.

**TC-1-128 · the panel shows no money**
`KAFF-115` money section · P1 · Api · spec.md §6.1 · CLAUDE.md
Given the panel response contract, when it is inspected, then it carries no rate, cost, salary or any
other money-shaped field.
*Fails if:* a day rate is added for convenience, putting payroll in front of everyone who can open a
project.

*(`AC-115-J` — Arabic RTL at 390px — is TC-1-198.)*

---

# KAFF-116 · Every audit record says how the actor reached the project — Ready

**TC-1-129 · an assigned actor**
`AC-116-A` · P1 · Api · spec.md §9
Given a Technical Office user assigned to project A, when they change something on A, then the audit
record carries `ProjectId` = A and grant path `Assignment`.
*Fails if:* the field does not exist. It does not today — `AuditRecord` has no grant-path column
(F-07). **Expected to fail on first run**, which is the correct state for a story that has not been
built.

**TC-1-130 · the Owner leaves a trace after all**
`AC-116-B` · P1 · Api · D-010
Given an Owner with no assignment row on project A, when they change something on A, then the record
carries `OwnerGlobal` — not `Assignment`, and not null.
*Fails if:* the value is derived by looking for an assignment row afterwards, which for the Owner finds
none and would record the wrong answer for the one actor whose authority leaves no row.

**TC-1-131 · HR's staffing is distinguishable from an assigned actor's**
`AC-116-C` · P1 · Api · D-044 ruling 3
Given an HR user with no assignment row, when they assign somebody to project A, then the record
carries `HrGlobal`.
*Fails if:* HR and the Owner collapse into one "global" value, so the trail cannot say which of two
very differently-privileged paths was used.

**TC-1-132 · a portal action**
`AC-116-D` · P1 · Api · spec.md §12
Given a `Role.Client` acting on their own project, when the action writes a record, then it carries
`ClientOfProject`.
*Fails if:* a client action is recorded as an assignment, which would imply a row that must never
exist.

**TC-1-133 · company-level changes carry none**
`AC-116-E` · P2 · Api · slice 0 `AuditRecord.ProjectId`
Given the Owner creating a user, when the record is read, then `ProjectId` and the grant path are both
null.
*Fails if:* the field defaults to `OwnerGlobal`, which makes "by what authority" meaningless by
answering it everywhere.

**TC-1-134 · the field cannot be added later — no API path**
`AC-116-F` · P1 · Api · CLAUDE.md append-only
Given an existing audit record, when an update is attempted through the API, then no such endpoint
exists.
*Fails if:* an admin correction endpoint is added, which is exactly the temptation CLAUDE.md names.

**TC-1-135 · nor a database one**
`AC-116-F` · P1 · Api, **raw SQL** · D-043
Given an existing audit record, when `UPDATE audit_records SET …` is executed directly, then the
database refuses it.
*Fails if:* the trigger is dropped by a migration. This is why the case bypasses the domain: the
question is what happens when something other than our C# reaches the table.

---

# KAFF-117 · Read the audit trail — **Ready**, D-049 §1

> **`AuditRead` is answered and is no longer `Unresolved`: company-wide, `Role.Owner` alone.**
> Karim, 2026-08-21: the trail is *"completely hidden from all other roles, **even for their own
> projects**"*. What he rejected is the part worth remembering — a project-scoped audit read for
> the people working on that project — because from slice 3 the trail records every movement of
> money, so scoping it by project would reopen the zero-financial-visibility rule from a direction
> nobody was watching. `qa/questions.md` **F-18** is closed.
>
> **The governance shape stands and Karim accepted it in those words:** the only person who reaches
> every project is the only person who can read the record of what he did there. **Nobody reopens
> it by adding a reader** — D-049 anticipates *"a Global Finance/Audit role (if added later)"* and
> deliberately does not create one.

**TC-1-136 · a portal client cannot reach it**
`AC-117-C` · P1 · Api · spec.md §12
Given a `Role.Client` user, when the audit trail is requested with and without a project id, then both
are refused with 403.
*Fails if:* the trail is reachable with a project filter, letting a client read every internal change
on his own job — including, from slice 3, every movement of money.

**TC-1-137 · a subcontractor has no login to try with**
`AC-117-D` · P1 · Domain + Api · spec.md §9
Given a `Role.Subcontractor`, when authentication is attempted, then it is refused before the trail is
reachable at all.
*Fails if:* the subcontractor short-circuit is removed from the evaluator, in which case a mistaken
catalogue grant would let one in.

**TC-1-138 · redacted fields stay redacted when read back**
`AC-117-E` · P1 · Api · slice 0 `[AuditRedacted]`
Given a user whose password was set and later reset, when their audit records are read by whoever
holds `AuditRead`, then no reading contains the password hash or the security stamp, in any field, in
either the before or after state.
*Fails if:* redaction happens on write but the read endpoint returns the raw JSON column — the same
secret, one layer later.

**TC-1-139 · a rejection shows its reason**
`AC-117-G` · P2 · Api · spec.md §7
Given a state change recorded with a reason, when the record is read, then the reason is returned with
it.
*Fails if:* the reason is stored and not displayed — §7 forbids a silent step-back, and a reason
nobody can see is silent.

**TC-1-140 · the trail cannot be edited from the API**
`AC-117-H` · P1 · Api · CLAUDE.md append-only
Given any audit record, when an update or a delete is attempted through the API, then no such endpoint
exists.
*Fails if:* one is added. Evidence that can be edited is not evidence.

**TC-1-141 · nor from a psql prompt**
`AC-117-H` · P1 · Api, **raw SQL** · D-043
Given any audit record, when a direct `UPDATE` and a direct `DELETE` are executed, then both are
refused by the trigger.
*Fails if:* the guard covers `postings` and not `audit_records`.

**TC-1-142 · an assigned user cannot read their own project's trail**
`AC-117-B, AC-117-C` · P1 · Api · **D-049 ruling 1**
Given a Technical Office user with an **active assignment on project A who made changes on it this
morning**, when they request the audit trail for project A, then they are refused with 403; and given
Finance, Site Engineer, Head of Design, Marketing, HR and a portal Client in turn, with and without a
project id, then all fourteen requests are refused.
*Fails if:* a project filter makes the trail reachable. *"Even for their own projects"* is the ruling,
and this is the criterion that proves it — a project-scoped audit read is the exact shape Karim
rejected, and from slice 3 it would show every movement of money on that project to everyone assigned
to it.
*(This case replaces the old `TC-1-142`, which was PENDING because `AuditRead` was an assumption marked
`Unresolved`. D-049 ruling 1 made it a ruling, and `TC-1-207` no longer expects it in the unresolved
set.)*

---

# KAFF-118 · Every state change in slice 1 writes an audit record — Ready

**TC-1-143 · the slice-1 entities are covered, one record each**
`AC-118-A` · P1 · Api · CLAUDE.md audit
Given a fresh database and **a user holding no project assignments**, when that user is created,
moved between departments, deactivated and reactivated; an assignment is created and revoked; a
client is created, edited and archived — then each act produces **exactly one** record with the
correct `AuditAction`, actor, timestamp and `ChangedProperties`.
*Fails if:* any entity is silently uncovered. D-041 is the entry that makes *"the mechanism exists"*
an insufficient claim: it existed, was clean, was covered by 51 passing tests, and could not execute
once.

**Split 2026-08-21 (SM-12).** This case asserted *"exactly one"* record across a list that included
**deactivation**, while `AC-118-C`
(`stories/slice-1-foundation/KAFF-118-every-slice-1-change-is-audited.md:52-55`) requires **four** —
one `User` and three `ProjectAssignment`, sharing one `CorrelationId`. Both are right for their own
subject: `AC-118-A`'s user holds no assignments, `AC-118-C`'s holds three, which is why the story separates them
and says so in as many words (*"as AC1c below, not as a step in AC1's list, because it writes more
than one record"*). One case asserting both counts could not pass. **The cascades are now
`TC-1-246` (`AC-118-C`) and `TC-1-247` (`AC-118-D`);** the `Given` above names the no-assignments precondition
that makes *"exactly one"* true, which is the part that was missing rather than wrong.

**TC-1-246 · deactivation writes four records on one `CorrelationId`**
`AC-118-C` · P1 · Api · CLAUDE.md audit · **D-049 ruling 5** · KAFF-111
Given a user with **three active project assignments**, when the Owner deactivates them, then
**four** records exist — one `User` and three `ProjectAssignment` — sharing **one** `CorrelationId`.
*Fails if:* the three revocations happen without records — a user losing three projects with nothing
in the trail saying why — or if they are written under three different correlation ids, so one act
reads as four unrelated ones.

**TC-1-247 · a role change writes four records on one `CorrelationId` too**
`AC-118-D` · P1 · Api · CLAUDE.md audit · **D-051 Q27** · KAFF-109
Given a Site Engineer with **three active project assignments**, when the Owner changes their role to
Technical Office, then **four** records exist — one `User` carrying the old **and** the new role, and
three `ProjectAssignment` — sharing one `CorrelationId`; and each assignment record names its project
so the trail filters per project (KAFF-116).
*Fails if:* the revocations D-051 Q27 makes a role change perform are written without records, or the
`User` record carries only the new role, which loses the answer to *"what did this person hold
before?"*.
**Re-mapped:** the role change was named here while KAFF-109 was BLOCKED, which was `qa/questions.md`
**F-20**. Q27 unblocked KAFF-109, so the dependency is real work rather than a dangling reference.

**TC-1-144 · no handler writes its own record**
`AC-118-A, rule 2` · P2 · Domain · CLAUDE.md *"one mechanism in `Domain/`"*
Given the slice-1 feature folders, when they are searched, then no handler constructs an `AuditRecord`.
*Fails if:* per-feature audit code appears, which guarantees the next feature forgets.

**TC-1-145 · one request is one story**
`AC-118-E` · P2 · Api · slice 0 `AuditCorrelationMiddleware`
Given a request that changes two entities, when the resulting records are read, then they share a
`CorrelationId`.
*Fails if:* each record gets a fresh correlation id, so one action reads as two unrelated events.

**TC-1-146 · redaction is visible, not silent**
`AC-118-F` · P1 · Api · slice 0 `KaffJson`
Given a password set on a user, when the record's before and after JSON are read, then `PasswordHash`
and `SecurityStamp` are **present and marked redacted**.
*Fails if:* they are omitted — an omitted key is indistinguishable from an unchanged one.

**TC-1-147 · the reason lands where the flow requires it**
`AC-118-G` · P1 · Api · spec.md §7
Given a deactivation performed with a reason, when the record is read, then the reason is stored
verbatim on it.
*Fails if:* the interceptor clears the reason before the save succeeds — a known gap (kickoff A4)
which this case would expose.

**TC-1-148 · a read writes nothing**
`AC-118-H` · P2 · Api · CLAUDE.md *"state change"*
Given `GET /api/auth/me`, the client list and the team panel each called ten times, when the audit table is
counted before and after, then the count is unchanged.
*Fails if:* reads are audited, burying the records that matter under navigation noise.

**TC-1-149 · a failed write writes nothing**
`AC-118-I` · P1 · Api · CLAUDE.md audit
Given a user creation refused by a domain rule, when the audit table is counted before and after, then
the count is unchanged.
*Fails if:* a half-record is written for a change that did not happen, so the trail asserts something
false.

**TC-1-150 · the trail outlives the actor**
`AC-118-J` · P1 · Api · KAFF-110
Given a user who made changes and was then deactivated, when their records are read, then all of them
still name them.
*Fails if:* records are joined to active users only, so deactivating somebody erases their history
from view.

---

# KAFF-119 · Register a client — **Ready**, D-049 §7 and §8

> **Two rulings landed here and one of them reversed a database constraint.**
>
> **§7 — client codes are generated.** Sequential, `C-10001`, manual entry and editing both forbidden.
> This closes the first half of D-040 and the old `TC-1-158`.
>
> **§8 — duplicate phones are allowed, with a warning.** Karim: *"a corporate client and its CEO might
> be registered as two separate entities sharing the same contact number."* `ux_clients_phone` **was a
> unique index — the database refused the save outright.** It is now `ix_clients_phone`, non-unique.
>
> **What that costs, stated rather than buried.** Nothing now prevents two client records for one
> person. The control has moved from a database constraint to a human reading a warning, and a human
> dismissing a warning is a well-understood failure mode. D-049 puts the consequence for QA plainly:
> ***"a missed match used to mean a wrongly-accepted save; it now means a warning nobody sees."***
> **Matching is more load-bearing after this ruling, not less** — which is why `TC-1-152`,
> `TC-1-240` and `TC-1-241` are P1 and why `TC-1-153` now asserts the absence of the constraint rather
> than its presence. See `qa/risk-register.md` **RSK-18**.

**TC-1-151 · a client is registered once, and the record names who did it**
`AC-119-A` · P1 · Api · spec.md §2
Given a signed-in Marketing user, when a client is registered with a name, a phone and a kind, then the
client exists, is active, carries a generated code, and an audit record names the Marketing user with
the kind in the after state.
*Fails if:* the record is created with no audit entry, so nobody can say who opened a client file.
*(The after state no longer carries a withholding category — it moved to the contract, D-049 §9.)*

**TC-1-152 · the same phone in three formats warns once, about the same client, and does not block**
`AC-119-C, AC-119-D` · P1 · Api · **D-049 ruling 8** · spec.md §2 amendment
Given a client "شركة النور" registered with `01001234567`, when a registration is attempted with
`+20 100 123 4567`, and again with `0020 100 1234567`, then **each returns a warning that names شركة
النور**, and **neither is refused**; and when the operator proceeds, a second client is created with
the same phone and its own code.
*Fails if:* **the save is refused.** That was this case's expected result until 2026-08-21 and it is now
wrong — a test still asserting a 409 passes against the constraint Karim removed and would block the
CEO-and-company case he ruled for. It also fails if the match runs on the entered text rather than the
normalised digits: the operator then sees no warning, saves happily, and the duplicate is invisible
until somebody reconciles two files months later.

**TC-1-153 · the database no longer refuses it, and that is the point**
`AC-119-D` · P1 · Api, **raw SQL** · **D-049 ruling 8**
Given a client with `phone_normalised` = `01001234567`, when a second row with the same value is
inserted directly, then it **succeeds**, and when the index is inspected it is `ix_clients_phone`,
**non-unique** — while `ux_clients_code` is still unique.
*Fails if:* a unique index is re-added on the phone. That would refuse a save Karim ruled must be
allowed, and it would do it from the database, where the error surfaces as a constraint violation
rather than as anything a Marketing user can act on.
*(This case previously asserted the opposite — that the unique index refuses the insert. The reversal
is deliberate and this case is the one that will catch somebody "fixing" the missing constraint.)*

**TC-1-154 · a portal client cannot reach the client master**
`AC-119-G` · P1 · Api · spec.md §12 · D-035
Given a `Role.Client` user, when they list, read or create a client, then every attempt is refused with
403.
*Fails if:* `ClientManage` reaches a portal user — the client master is every client Kaff has, and §12
is absolute.

**TC-1-155 · nobody outside Marketing and the Owner may register one**
`AC-119-H` · P1 · Api · spec.md §2 · D-044 ruling 4
Given Finance, then Technical Office, then HR, then a Site Engineer, when each attempts to register a
client, then each is refused with 403.
*Fails if:* a second department can open client files, and §2's *"exactly one module owns each entity"*
stops being true on the first master record.

**TC-1-156 · the entity carries no money**
`AC-119-I` · P1 · Domain · spec.md §6.1 · CLAUDE.md *"Never store a balance"*
Given the `Client` entity, when its properties are enumerated, then none is a balance, a credit limit
or any other money value.
*Fails if:* a `Balance` or `CreditLimit` column is added — the stored balance CLAUDE.md forbids
outright, arriving on the most innocent-looking form in the system.

**TC-1-157 · and neither does the contract or the table**
`AC-119-I` · P1 · Api · spec.md §6.1
Given the client API response contract and the `clients` table, when each is inspected, then neither
carries a money field.
*Fails if:* the entity is clean and the read model is not.

**TC-1-158 · the code is generated, sequential, and cannot be typed**
`AC-119-B` · P1 · Api · **D-049 ruling 7** · §2 amendment
Given the last client created carries `C-10001`, when the next client is registered, then it carries
`C-10002`; and when a request supplies its own code, then that value is **never stored** — ignored or
refused; and when an edit attempts to change a code, then no path exists.
*Fails if:* the code is a form field. Karim's reason is the requirement — a code is *"a stable reference
for extracts and ledgers rather than something a person can mistype or change"* — and a mistyped code
on a signed contract is not correctable once extracts reference it. **Note N6:** two clients created in
the same instant must not collide; `ux_clients_code` is unique, so a naive read-max-and-add-one loses
the race as a failed insert, and this case should be run **concurrently** as well as sequentially.
*(This case replaces the old `TC-1-158`, PENDING on whether `Client.Code` should exist at all.)*

**TC-1-159 · the decision to save past a warning is in the trail**
`AC-119-E` · P1 · Api · **D-049 ruling 8** · CLAUDE.md audit
Given a client saved past a duplicate-phone warning, when its audit record is read, then the record
**shows that a duplicate was matched and names the client it matched**.
*Fails if:* nothing is recorded. *"Asks whether to proceed"* is only meaningful if the answer is
recoverable — this record is the **only durable trace that a human made the call**, and it is what the
later question *"why are there two files for this man"* is answered from. It is also the signal D-049
names for revisiting the ruling: *"two client records with one phone and overlapping projects."*
*(This case replaces the old `TC-1-159`, PENDING on what a duplicate shows and whether two clients may
share a phone. Ruling 8 answered both.)*

**TC-1-240 · normalisation folds every form Kaff actually types**
`AC-119-C, rule 5` · P1 · Domain · **D-049 ruling 8** · slice 0 `PhoneNumber`
Given the numbers `01001234567`, `+20 100 123 4567`, `0020 100 1234567`, `010 0123 4567`,
`(010) 0123-4567` and the same number written in **Arabic-Indic digits (٠١٠٠١٢٣٤٥٦٧)**, when each is
normalised, then all six produce the identical stored value.
*Fails if:* any one of them does not. **This is the whole of the control now.** Under the old unique
index a normalisation miss produced a duplicate the database would later trip over; after ruling 8 it
produces silence — no warning, no constraint, two files. The Arabic-Indic case is not decoration:
`PhoneNumber.Normalise` handles it today and the users are Arabic-speaking, so a regression there is
invisible to an English-reading reviewer.

**TC-1-241 · the warning is data, not decoration**
`AC-119-C, AC-119-F, rule 4` · P1 · Api · **D-049 ruling 8** · §2 amendment
Given a registration whose phone matches an existing client, when the API response is inspected, then
it carries the **matched client's name and code** as fields — and where the match is archived, a flag
saying so — rather than a rendered sentence; and given a match against an **archived** client, then the
warning still fires.
*Fails if:* the response says only *"this number exists"*, which is not what was ruled — the operator
has to know **whose** number it is to decide anything. Or if the warning text is assembled server-side
as prose, which breaks the no-server-prose rule (`problem-details.ts`) and makes the Arabic version
somebody's afterthought. Or if archived clients are excluded from the match: an archived client is
still a client, and §3 requires a reopened opportunity to attach to the original.

---

# KAFF-120 · An individual's contract cannot carry a withholding rate — Ready

> **D-049 §9 moved the rate off the client and onto the contract.** `Client.WithholdingCategory` no
> longer exists; `Project.WithholdingCategory` does, defaulting to `None`, and
> `Project.SetWithholding(category, clientKind)` refuses a rate on an individual's contract.
> `Client.SetWithholding` became `Client.SetTaxRegistration`, which refuses a registration number on an
> individual. **That closes both halves of D-040 and retires RSK-11 as a live defect.**
>
> **Six Domain tests already exist** in `tests/Domain.Tests/WithholdingTests.cs` and are not
> duplicated here. They cover: the 1 / 3 / 5 % rate table; one client holding two contracts at two
> rates; all three categories refused on an individual's contract with nothing stored; `None` always
> legal; the default being `None`; and a tax registration number refused on an individual while
> clearing it and setting it on a corporate both succeed.
>
> **What the cases below cover is what those six do not:** the API surface, the database and the
> contract shape, the i18n catalogue, and the one thing the domain **cannot** check — that
> `SetWithholding` is told the truth about the client's kind.
>
> **The i18n key is settled** (`qa/questions.md` **F-08** closed): `errors.master.individual_does_not_withhold`.

**TC-1-160 · a rate on an individual's contract is refused through the API**
`AC-120-C` · P1 · Api · spec.md §6.7 amendment · D-049 ruling 9
Given a project whose client is an `Individual`, when `ContractingAndSupplies`, then `Services`, then
`ProfessionalFees` is set through the API, then all three are refused with
`errors.master.individual_does_not_withhold` and the stored category stays `None`.
*Fails if:* the endpoint bypasses `Project.SetWithholding` and writes the property directly. The domain
test proves the method; this proves the wire. **NO ENDPOINT EXISTS YET** — `ProjectManage` is granted
to nobody (Q17), so this case runs against **KAFF-416, slice 4**, and is listed here so the slice-4
session does not treat the domain test as full coverage.

**TC-1-161 · `None` is accepted on an individual's contract, through the API**
`AC-120-D` · P2 · Api · spec.md §6.7
Given a project whose client is an `Individual`, when `None` is set, then it is accepted.
*Fails if:* the fix is written as *"an individual's contract may not carry the field"*, which makes the
field unsettable and therefore makes an individual's contract unapprovable. Same slice-4 caveat.

**TC-1-162 · a tax registration number on an individual is refused through the API**
`AC-120-A` · P1 · Api · spec.md §6.7 amendment
Given a Marketing user and a client of kind `Individual`, when a tax registration number is set, then
it is refused with `errors.master.individual_does_not_withhold` and the stored value is unchanged.
*Fails if:* the guard lives in `Client.SetTaxRegistration` and the endpoint reaches the property by
another route — a create-with-registration path, or a general-purpose update that binds the field.
*(This case replaces the old `TC-1-162`, which asserted the update path on `Client.SetWithholding`.
That method no longer exists.)*

**TC-1-163 · the client record has no category to set, anywhere**
`AC-120-F` · P1 · Api, **raw SQL** · **D-049 ruling 9** · migration
`WithholdingOnContractAndSoftPhoneDedup`
Given the `Client` entity, the client API request and response contracts, and the `clients` table, when
each is inspected, then **none carries a withholding category**, no endpoint accepts one, and the
`clients.withholding_category` column is **absent from the database**.
*Fails if:* the column survives the migration on an existing database, or the DTO keeps the field "for
compatibility". A field that exists and is ignored is a field somebody will populate, and §5.4's design
plus execution case is exactly where the two values would then disagree.
*(This case replaces the old `TC-1-163`, which asserted a registration-number refusal now covered by
the Domain suite and by `TC-1-162` at the API.)*

**TC-1-164 · the contract's column defaults to `None` in the database, not only in C#**
`KAFF-120 rule 7` — **no criterion covers this; see the note below** · P1 · Api, **raw SQL** · **D-049 ruling 9** · the migration's own note
Given the `projects` table, when `withholding_category` is inspected, then its default is the text
`'None'`; and given a row inserted directly with no value for it, when the row is read back through EF,
then it materialises as `WithholdingCategory.None` rather than failing to cast.
*Fails if:* the default is `''`. D-049 records that EF scaffolded exactly that and it was corrected in
both `Up` and `Down` — *"`\"\"` is not a member of it — an existing row backfilled with `\"\"` would have
failed to materialise on the next read, as a cast error naming nothing."* A cast error naming nothing
is the worst possible symptom, and this case names it.
*(This case replaces the old `TC-1-164`, "the rule lives in the domain", which the Domain suite now
covers directly.)*

**Relock 2026-08-22 — the label said `AC` and there is no such criterion.** The citation read
`KAFF-120 AC — rule 7`, which reads as an acceptance criterion and is not one. **No KAFF-120 row in
`stories/ac-id-map.md` describes the database default.** The eight are: a tax registration number on
an individual refused; the refusal reading as Arabic; a rate on an individual's contract refused;
`None` always legal; a corporate contract unaffected; no category on the client record; the rules
living in the domain; one key not two. The nearest, **`AC-120-D` — *"`None` is always legal"*** — is
`TC-1-161`'s and is about the API accepting `None`, not about what the column defaults to when nobody
sets it. **Left as a rule citation and raised for the BA: either KAFF-120 needs a criterion for the
stored default, or this case is uncovered work.** Not pointed at the nearest-looking criterion — that
is the defect the map exists to end. Same shape as `TC-1-032`.

**TC-1-165 · a corporate contract is unaffected**
`AC-120-E` · P2 · Api · spec.md §6.7
Given a project whose client is `Corporate`, when a category is set through the API, then it is
accepted and stored.
*Fails if:* the fix over-reaches and refuses withholding entirely, which would break every corporate
collection from slice 3 (KAFF-317). Slice-4 caveat as above.

**TC-1-166 · one key, in both catalogues, and only one**
`AC-120-H` · P2 · E2E · CLAUDE.md i18n · D-047
Given `ar.json` and `en.json`, when they are searched, then
`errors.master.individual_does_not_withhold` is present in **both**, and
`errors.master.individual_client_does_not_withhold` is present in **neither**; and when the refusal
surfaces on screen in Arabic, then it resolves rather than rendering as a raw key.
*Fails if:* the second key is reintroduced. Two documents carried two keys for one refusal until
2026-08-21 (`qa/questions.md` F-08) — the story has been corrected and the UX flow was already right,
so what is left is a case that fails the moment somebody invents the synonym again. **This case is why
F-08 is closed rather than forgotten.**

**TC-1-242 · the client's kind comes from the database, never from the request**
`AC-120-G` · P1 · Api · **D-049 ruling 9** (*"the client's kind is passed in rather than looked
up"*)
Given a project whose client is an `Individual`, when the withholding endpoint is called with a request
that asserts `clientKind = Corporate` — or when a handler is written that takes the kind from anything
other than the stored `Client` row — then the rate is still refused with
`errors.master.individual_does_not_withhold`.
*Fails if:* the handler forwards a caller-supplied kind into `Project.SetWithholding`. **The domain
cannot catch this**, and D-049 says why the signature is shaped that way: *"the domain holds only
`ClientId`"*. So the guard the ruling installed is only as good as the argument the handler passes, and
**nothing in the six Domain tests can detect a lie in that argument** — this is the one case that can.
See `qa/risk-register.md` **RSK-20**. Runs against KAFF-416, slice 4.

---

# KAFF-121 · Edit a client's contact details — Ready

**TC-1-167 · a correction is recorded with its before-state**
`AC-121-B` · P1 · Api · CLAUDE.md audit
Given a client with an address, when Marketing changes it, then the new address is stored and the audit
record carries both the old and the new value.
*Fails if:* only the after state is kept — *"the phone number on file when we sent that invoice"* is a
question that gets asked, and it has no answer without the before.

**TC-1-168 · changing the phone re-runs the check, warns, and proceeds**
`AC-121-C, AC-121-D` · P1 · Api · **D-049 ruling 8** · §2 amendment
Given clients A and B with different phones, when A's phone is edited to B's phone — and again to B's
phone written as `+20 100 123 4567` — then **each returns a warning naming B**, **the edit is not
refused**, and on proceeding A's phone is changed and the audit record records that a duplicate was
matched.
*Fails if:* **the edit is refused as a duplicate.** That was this case's expected result and it is now
wrong: the constraint is gone from the database, so refusing here would need new application code
contradicting the ruling. It also fails if the edit path skips the check entirely — the operator then
gets no warning at all, which after ruling 8 is the *only* control there is.
**There is still no domain method to change the primary phone** (`qa/questions.md` **F-09**), so this
case cannot pass until one exists — and if it is added without the check, this case catches it.
**Expected to fail on first run.**

**TC-1-169 · a kind change cannot smuggle a tax registration past §6.7**
`AC-121-F` · P1 · Domain + Api · spec.md §6.7 amendment · D-049 ruling 9
Given a corporate client **with a tax registration number**, when its kind is changed to `Individual`
without clearing the number, then it is refused with `errors.master.individual_does_not_withhold`.
*Fails if:* the rule is enforced on `SetTaxRegistration` and not on a kind change — the second door
into the same illegal state, and the one nobody looks at because the field being edited is not the
field being broken.
*(This case previously read *"a kind change cannot smuggle a **category** past §6.7"*. The category is
no longer on the client at all — D-049 ruling 9 — so the same shape of defect now arrives through the
registration number, which stayed. There is no `ChangeKind` method on `Client` today, which means this
case, like `TC-1-168`, is written against a path that has to be built.)*

**TC-1-170 · nobody outside Marketing and the Owner may edit**
`AC-121-G` · P1 · Api · spec.md §2 · D-044 ruling 4
Given Finance, then Technical Office, then HR, then a Site Engineer, then a portal Client, when each
attempts an edit, then each is refused with 403.
*Fails if:* the read permission and the write permission are the same permission.

**TC-1-171 · internal notes stay internal**
`AC-121-H` · P1 · Api · spec.md §12
Given a client with notes, when any endpoint reachable by `Role.Client` is called, then the notes appear
in no response.
*Fails if:* the client DTO is shared between the internal and portal surfaces — the argument for
unshared response types (D-035, slice 8's KAFF-810) starts here.

**TC-1-172 · editing does not archive, and archiving is not an edit**
`KAFF-121 rule 9` · P2 · Domain + Api · slice 0 `Client.Archive`
Given an active client, when contact details are edited, then `IsActive` is unchanged; and when the
client is archived, no contact field changes.
*Fails if:* a general-purpose update endpoint accepts `isActive` in the body, turning archival into a
field edit with no confirmation and no distinct audit action.

**TC-1-173 · no edit can introduce a money field**
`KAFF-121 rule 10` · P1 · Api · spec.md §6.1
Given the edit request contract, when it is inspected, then it accepts no money value.
*Fails if:* a "credit limit" is accepted here even though it is absent from the create form.

**TC-1-174 · the name is editable, and the code is not**
`AC-121-A, AC-121-E` · P1 · Domain + Api · spec.md §2 · **D-049 ruling 7**
Given a client whose name was mistyped at registration, when Marketing corrects it, then the new name
is stored and the audit record carries both values; and given the same request supplying a different
`Code`, then the stored code is **unchanged by any route through the API**.
*Fails if:* there is no path to change the name — there is none today, `SetContactDetails` covers
alternate phone, email, address and notes only (**F-09**), so a mistyped client name is permanent. Or
if the code moves: ruling 7 forbids editing it, and `Client` deliberately has no setter, so a code that
changes means somebody added one. **Promoted from P2 to P1 and re-cited**, because the two halves are
one endpoint and the second half is a money-adjacent invariant — extracts and ledgers reference the
code. **Expected to fail on first run.**

*(`AC-121-I` — Arabic RTL at 390px — is `TC-1-199`.)*

---

# ~~KAFF-122 · Corporate client withholding~~ — **SUPERSEDED**, cases retired

> **Karim moved the withholding rate off the client and onto the contract (D-049 rulings 9 and 10),
> and gave it to Finance rather than Marketing.** Nothing in slice 1 creates or edits a contract —
> `ProjectManage` is `ProjectScoped` and cannot authorise a create even now that D-052 §2 gave it holders (**F-27**) — so the story's 3 points moved to **KAFF-416, slice
> 4**, and the story is `Superseded` in the backlog.
>
> **The five cases below are retired, not deleted.** A deleted case comes back next session as a
> missing one. Where a retired case's assertion still matters, it says where it went.

**~~TC-1-175~~ · RETIRED — an individual is still refused through this path**
Retired by **D-049 ruling 9**: there is no "this path" any more. The refusal lives on
`Project.SetWithholding` and is covered by `WithholdingTests.A_contract_for_an_individual_client_cannot_withhold`
in the Domain suite, and at the API by `TC-1-160` under KAFF-416.

**~~TC-1-176~~ · RETIRED — the value is auditable**
Retired **as a client-level case** and **carried into slice 4**: the audit record for a withholding
change is now a record on `Project`, written when Finance sets the rate. **KAFF-416 must carry it** —
§6.7's failure mode is staff quietly adjusting to close a gap, and the field that creates the gap is
still the same field. *Fails if it is forgotten:* a rate changes with nobody named against it.

**~~TC-1-177~~ · RETIRED — nothing here computes tax**
Retired as a KAFF-122 case and **kept alive as `TC-1-163`**, which asserts the client record holds no
category at all. The wider assertion — *"a classification and a registration number, no rate table, no
calculation, no tax report"* — belongs to slice 3's KAFF-317 and KAFF-318. `spec.md` §1 and CLAUDE.md:
*"two fields and two accounts is the whole of it."*

**~~TC-1-178~~ · RETIRED — PENDING on where the category belongs**
**Answered.** D-049 ruling 9: on the contract. The reasoning QA wrote into this PENDING is the
reasoning Karim's ruling used — §6.7 sets the rate by what is supplied, and §5.4 lets one client hold a
design contract at 5% and an execution contract at 1% simultaneously, which one value per client cannot
express. Pinned as a Domain test
(`WithholdingTests.One_client_can_hold_two_contracts_at_two_different_rates`) so it cannot regress into
a client-level field.

**~~TC-1-179~~ · RETIRED — PENDING on whether Marketing or Finance owns the field**
**Answered.** D-049 ruling 10: **Finance**, *"during contract creation or approval"*. Karim's reason —
the rate *"directly dictates ledger entries and money reconciliation. It is a strict accounting
parameter, not a marketing detail."* The permission case moves to **KAFF-416**: Marketing must be
refused, and the endpoint waits on a permission that can authorise creating a contract — D-052 §2 named `ProjectManage`'s holders and left it `ProjectScoped` (**F-27**, KAFF-407).

**One thing left open and carried, not closed:** **Q29** — §6.7's next paragraph gives subcontractor
and supplier withholding the same shape, and those rates are **still on the party record**. Karim's
ruling named the client only. Extending it would be inventing the ruling he did not give. It lands on
slice 2's KAFF-211 / KAFF-212 and slice 3's KAFF-318.

---

# KAFF-123 · Archive a client — Ready

**TC-1-180 · an archived client leaves the list but not the database**
`AC-123-A` · P1 · Api · spec.md §2, §3
Given an active client, when Marketing archives them, then they no longer appear in the default list,
the row still exists, and an audit record naming the actor with `IsActive` in `ChangedProperties` is
written.
*Fails if:* archiving deletes — §2 requires full history and §3 requires a reopened opportunity to
attach to the same client, and both are impossible if the row can disappear.

**TC-1-181 · an archived client still surfaces in the duplicate check, and still does not block**
`AC-123-B` · P1 · Api · **D-049 ruling 8** · spec.md §2 amendment, §3
Given an **archived** client holding `01001234567`, when a new client is registered with that phone,
then a warning fires **naming that client and stating that it is archived**, and **the save is not
blocked**.
*Fails if:* the registration is **refused**. That was this case's expected result until 2026-08-21 —
the unique index made it true — and it is now wrong. It also fails if archived clients are excluded
from the match: an archived client is still a client, §3 requires a reopened opportunity to attach to
the original, and the moment a returning client is most likely to acquire a second file is the moment
they come back. **Q39** — whether the system should offer to bring the archived client back — is open
and does not block: there is no unarchive path in slice 1 at all.

**TC-1-182 · archiving twice is refused**
`AC-123-C` · P2 · Domain + Api · slice 0 `Client.Archive`
Given an archived client, when they are archived again, then it is refused with
`errors.master.already_archived`.
*Fails if:* the second call silently succeeds.

**TC-1-183 · no delete exists**
`AC-123-D` · P1 · Api · spec.md §2
Given any client, when the API surface is enumerated, then no route deletes one.
*Fails if:* a delete is added for "cleaning up test data" — the same temptation CLAUDE.md refuses for
postings, arriving on a master record.

**TC-1-184 · DEFERRED to slice 4 (rule 5)**
`KAFF-123 rule 5` · P2
Whether a client with an open project or an unsettled account may be archived at all. Not decidable in
slice 1 — projects and postings do not exist yet, and §11 makes closure an accounting condition. The
story raises it deliberately so the next session does not assume it was considered and allowed.
**Slice 4 must revisit.**

**TC-1-185 · nobody outside Marketing and the Owner may archive**
`AC-123-E` · P1 · Api · D-044 ruling 4
Given Finance, then HR, then a portal Client, when each attempts to archive a client, then each is
refused with 403.
*Fails if:* a client can archive himself off Kaff's books.

---

# KAFF-124 · Find a client by name or phone — Ready

**TC-1-186 · a phone in any format finds the client**
`AC-124-A` · P1 · Api · spec.md §2
Given a client stored as `01001234567`, when `+20 100 123 4567`, `0020 100 1234567` and `01001234567`
are searched in turn, then all three return that client.
*Fails if:* search matches the entered text — Marketing then fails to find the client they are about
to duplicate, at the exact moment the duplicate is cheapest to prevent.

**TC-1-187 · partial name search works in Arabic**
`AC-124-D` · P2 · Api · spec.md §2
Given a client whose name is Arabic, when a substring of it is searched, then the client is returned.
*Fails if:* the search collation or normalisation breaks on Arabic, which is every real client Kaff
has.

**TC-1-188 · archived clients are hidden by default and findable on request**
`AC-124-E` · P2 · Api · spec.md §2, §3
Given one active and one archived client, when the default filter is used and then the archived filter,
then the first returns one result and the second returns two.
*Fails if:* archived clients are unreachable, so a reopened opportunity cannot attach to the original.

**TC-1-189 · a portal client cannot list clients**
`AC-124-F` · P1 · Api · spec.md §12 · D-035
Given a `Role.Client` user, when the client list is called with any filter and any search term, then it
is refused with 403 and **no client name appears in the response body**.
*Fails if:* the refusal body echoes the query or a partial result — this endpoint returns every client
in Kaff.

**TC-1-190 · no money in the payload**
`AC-124-G` · P1 · Api · spec.md §6.1
Given the list response contract, when it is inspected, then it carries no balance, contract value,
total billed or any other money-shaped field.
*Fails if:* a "total billed" column is added to the list for convenience.

**TC-1-191 · an empty search says so**
`AC-124-H` · P3 · E2E · spec.md §4.5 (same principle)
Given a search matching nothing, when the results render, then `clients.list.empty` is displayed.
*Fails if:* a blank area or a phantom row appears.

**TC-1-192 · only Marketing and the Owner reach the list**
`KAFF-124 rule 3` · P1 · Api · D-044 ruling 4
Given Finance, Technical Office, HR and a Site Engineer, when each calls the client list, then each is
refused with 403.
*Fails if:* the list is given a softer permission than the create endpoint.

**TC-1-193 · the list writes nothing**
`KAFF-124` audit section · P2 · Api · CLAUDE.md *"state change"*
Given the list called ten times, when the audit table is counted before and after, then the count is
unchanged.
*Fails if:* searches are audited.

**TC-1-194 · normalisation happens on the server**
`KAFF-124 rule 1` · P2 · Api · slice 0 `PhoneNumber`
Given a raw query string containing spaces and a `+20` prefix, when it is sent unmodified to the API,
then it matches.
*Fails if:* the client normalises before sending, creating a second implementation of
`PhoneNumber.Normalise` that will drift from the first.

*(`AC-124-I` — Arabic RTL at 390px — is TC-1-200.)*

---

# HR's project team screen — ~~**NO STORY**~~ **the story arrived**, D-051 Q32

> **Corrected 2026-08-22 (SM-29).** These three cases carried **NO STORY** and cited `D-051 Q32`
> because, when they were written, no story named HR's surface. **That is no longer true and was not
> re-checked before today.** `KAFF-115` is `Status: Ready`, carries the surface as rule 5 and names the
> permission **`ProjectTeamRead`**, and carries `AC-115-H` and `AC-115-I` for it
> [Verified: 2026-08-22 @ `stories/slice-1-foundation/KAFF-115-project-team-panel.md:3, :28, :82-93`];
> `KAFF-105b` `AC-105b-E` routes HR to it. **`qa/questions.md` F-24's "there is no story"
> half is closed; its "there is no permission" half is not** — `ProjectTeamRead` is named in four story
> files and in **no file under `src/`** [Verified: 2026-08-22]. The three cases below are relocked to
> the criteria that now exist and stay **unrunnable** — but for a different and much smaller reason
> than before: a missing catalogue row, not a missing story. See **F-30**.

> **Karim ruled it and nobody has written it.** D-051 Q32: *"HR may only see the project name and the
> list of assigned engineers … If the main project dashboard contains financial data, HR must be
> routed to a separate 'Project Team' tab/screen that contains zero financial details."*
>
> **The shape of the answer is the load-bearing part: a separate surface, not a filtered view** — the
> same pattern §12 uses for the client portal, and the same reason. *A filtered view leaks the first
> time somebody adds a field.* D-051 also says it *"implies a new narrow permission rather than
> granting HR `ProjectRead`"*, and that **naming it is the story's**.
>
> These three cases are written against the ruling, not against a permission name, so they survive
> whatever the permission ends up being called. They are **uncovered until a story exists** — they are
> not PENDING, because nothing here is being invented. `qa/questions.md` **F-03** and **F-13**, and
> **QA-2**, are closed by the ruling; what replaces them is a missing story.

**TC-1-243 · HR sees a project's name and its team, and that is the whole payload**
`AC-115-H` · P1 · Api · **BLOCKED F-30** (no `ProjectTeamRead` row) · D-051 Q32
Given a `Role.Hr` user and a project that has a contract value, a budget, three assigned engineers and
a client, when HR calls the team surface for that project, then the response carries **the project's
name, its code, and the assigned users with their levels** — and **nothing else**: no contract value,
no budget, no cost, no margin, no client financial detail, no balance, no client id.
*Fails if:* the response is built from the internal project DTO with fields omitted. That is the
filtered view Karim's answer explicitly avoids, and the leak arrives the first time somebody adds a
field to the shared type — which is D-035's mechanism, for the third time in this system.

**TC-1-244 · it is a separate surface, and the internal one is still refused**
`AC-115-H, AC-115-I` · P1 · Api · **BLOCKED F-30** · D-044 ruling 2 · D-051 Q32
Given the same HR user immediately after a successful call to the team surface, when they call the
internal project endpoints — project read, project detail, the team panel of `KAFF-115`, and any
endpoint requiring `ProjectRead` — then **every one is refused with 403**.
*Fails if:* HR is granted `ProjectRead` to make the screen work. D-051 forbids that route by name, and
so does `ux/questions.md`: *"do not solve this by granting HR `ProjectRead`, and do not solve it by
reusing the internal project list."* Either hands HR the project surface D-044 ruling 2 was written to
remove. Note this case must be run **after** a successful call, because the failure it catches is a
permission widened rather than a screen added.

**TC-1-245 · HR reaches no financial detail by any route in the system**
`AC-107-D, AC-115-H` · P1 · Api · **BLOCKED F-30** · D-044 ruling 2 (*"zero financial visibility"*) · D-051 Q32
Given a `Role.Hr` user, when **every endpoint the slice exposes** is called in turn — including the new
team surface, the audit trail, `/api/auth/me`, the client list, the client detail, the team panel, and
every endpoint requiring `ProjectRead`, `SiteExpenseConfirm`, `TreasuryPostProject`,
`FinancialMovementApprove`, `AccountManage`, `PhotoPublish` or `AuditRead` — then **no response
contains a money-shaped field**, and every one of the seven permission-gated endpoints is refused
with 403.
*Fails if:* one route leaks. This is the exhaustive form of `TC-1-063`, extended over the surface
Q32 adds, and it is written as a **sweep over the endpoint list** rather than as a fixed set, so a new
endpoint added in slice 2 fails this case instead of quietly widening HR. **One of the seven —
`PhotoPublish` — is granted by department with no role named** [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PhotoPublish`], which
is why HR's department pin (`TC-1-066`) is a second mechanism and not a nicety. `SiteExpenseConfirm`
was the other until **D-052 §1** named its roles and closed F-04.

---

# Arabic, RTL and i18n — cross-cutting

All E2E, all at **390px**, in Arabic, against the running stack.

**TC-1-195 · the login screen**
`AC-101b-H` · P3 · E2E · CLAUDE.md RTL — **re-mapped from KAFF-101**
Given S-001 at 390px in Arabic, when it renders, then `dir` is RTL, no string is a literal, and the
body does not scroll horizontally.
*Fails if:* a `margin-left` survives instead of `margin-inline-start`, which shows as an off-centre
form only in Arabic.
**Relock 2026-08-22.** Cited `KAFF-101b AC — RTL`. The map gives KAFF-101b a criterion of its own:
**`AC-101b-H` — *"Arabic, RTL, at mobile width"***. The note on `TC-1-231` that *"KAFF-101b carries no
matching AC"* is about the **browser-store** assertion and stays correct; it is not about this one.

**TC-1-196 · the set-password screen**
`AC-103-I` · P3 · E2E · CLAUDE.md RTL
Given S-003 at 390px in Arabic, when it renders, then RTL holds, no literals, no overflow.
*Fails if:* the password field forces `dir=ltr` on its container rather than on the input, dragging the
label with it. Engineers will do this on a phone.

**TC-1-197 · the user form**
`AC-106-J` · P3 · E2E · CLAUDE.md RTL, i18n
Given S-007 at 390px in Arabic, when it renders, then RTL holds, **every** role and department label
resolves from the catalogue, and there is no overflow.
*Fails if:* an enum member is rendered as its C# name — nine roles and four departments is thirteen
chances to leave one out.

**TC-1-198 · the team panel**
`AC-115-J` · P3 · E2E · CLAUDE.md RTL
Given S-009 at 390px in Arabic, when it renders, then RTL holds, names and Latin codes are
bidi-isolated, and there is no overflow.
*Fails if:* a Latin project code inside an Arabic row is reordered by the bidi algorithm and displays
its characters in the wrong order — a bug that is invisible in English and wrong on every screen in
Arabic.

**TC-1-199 · the client edit form**
`AC-121-I` · P3 · E2E · CLAUDE.md RTL
Given S-014 at 390px in Arabic, when it renders, then RTL holds, phone numbers and emails inside Arabic
labels are bidi-isolated, and there is no overflow.
*Fails if:* `+20` migrates to the wrong end of a phone number.

**TC-1-200 · the client list**
`AC-124-I` · P3 · E2E · CLAUDE.md RTL
Given S-011 at 390px in Arabic, when it renders, then RTL holds, Latin phone numbers inside Arabic rows
are bidi-isolated, and there is no overflow.
*Fails if:* the search field's `dir=auto` flips the whole row when the user types a digit.

**TC-1-201 · nothing is hardcoded, and no project status word appears**
all stories · P2 · E2E · CLAUDE.md i18n · kickoff §7
Given every slice-1 screen in Arabic and again in English, when each is rendered, then no raw i18n key
and no untranslated literal is visible in either; and **none of** لم تبدأ · جاري العمل · انتهت ·
متعثرة · تم تأجيلها **appears anywhere in slice 1**.
*Fails if:* somebody puts a project status chip on a screen "because it's useful". What those five
words mean is Q18, and one of them has two spellings across the continuity files (Q19) — so
rendering any of them now would ship a guess about Kaff's own vocabulary.

---

# The permission matrix, executed — **this is slice 1's gate**

Expected outcomes per cell are `qa/slice-1/permission-matrix.md`. These cases are how the matrix is
run. All hit **endpoints directly**, never the UI (`CLAUDE.md`).

**TC-1-202 · every role against every company-wide permission**
`permission-matrix.md` · P1 · Api · spec.md §9
Given each of the nine roles in turn, when an endpoint requiring each `CompanyWide` permission is
called, then the outcome matches the matrix cell exactly.
*Fails if:* any grant has been added or removed without the matrix and its citation changing with it.

**TC-1-203 · every role against every project-scoped permission, assigned**
`permission-matrix.md` · P1 · Api · spec.md §9
Given each role assigned to the project at the level the matrix names, when each `ProjectScoped`
endpoint is called, then the outcome matches.
*Fails if:* a minimum assignment level is dropped, silently promoting every junior to a supervisor.

**TC-1-204 · every role against every project-scoped permission, unassigned**
`permission-matrix.md` · P1 · Api · spec.md §9 *"Role alone is insufficient"*
Given each role holding the grant but **not assigned** to the project, when each `ProjectScoped`
endpoint is called, then every one is refused — except the Owner, HR and the project's own portal
client, whose reach is granted by `IProjectAccessPolicy`.
*Fails if:* the assignment half of "role × assignment" is skipped. This is the single most important
case in the slice.

**TC-1-205 · a subcontractor is refused before anything else is considered**
`permission-matrix.md` · P1 · Domain + Api · spec.md §9
Given `Role.Subcontractor`, when every permission is evaluated, then every one returns
`RoleCannotLogIn`, **including for a permission whose catalogue row was deliberately given a
subcontractor grant in the test**.
*Fails if:* the short-circuit is removed and the catalogue becomes the only defence. The refusal is
before the catalogue lookup on purpose.

**TC-1-206 · `ProjectManage` is held by the Owner and the Technical Office, and by nobody else**
`permission-matrix.md` · P1 · Domain · **D-052 §2** (answering Q17, raised at D-012)
Given `PermissionCatalogue`, when `ProjectManage`'s grants are read, then they are exactly
`Role.Owner` and `Role.TechnicalOffice`, and the row is **not** marked `Unresolved`.
*Fails if:* a third role is added to make something work in slice 4 — Karim named the two and named
the excluded (*"Site Engineers and Marketing have no business creating projects"*).
**Rewritten 2026-08-21.** This case asserted *"the list is empty and the row is marked
`Unresolved`"*, which was correct until D-052 §2 answered the oldest open question in the catalogue
[Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`]. A case still expecting nobody would fail against a
correctly-applied ruling — the same shape as `TC-1-207`'s note below.
**🟡 The scope is still wrong and this case does not hide it:** the row is `ProjectScoped`, so it can
authorise *editing* a project and **cannot authorise opening one**, which is the half Karim ruled on.
See `permission-matrix.md` §1's 🟡 block. Architect's, raised not taken, slice 4 / **KAFF-407**.

**TC-1-207 · the unresolved set is exactly one, and has not grown**
`permission-matrix.md` · P1 · Domain · D-012 · D-049 ruling 1 · **D-052 §2**
Given the catalogue, when `Unresolved` is enumerated, then it is exactly `{ PeriodClose }` — and
neither `AuditRead` nor `ProjectManage` is in it, while both keep the grants their rulings gave them.
*Fails if:* a new assumption is added without a question, or an existing one is quietly resolved by
whoever needed it that day. **The expected set has shrunk twice in two days**: D-049 ruling 1
answered `AuditRead`, D-052 §2 answered `ProjectManage` (`Permission.PeriodClose` is the only
`Unresolved: true` row left) [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PeriodClose`]. A case still expecting the old set would fail against a correctly-applied
ruling — and somebody would "fix" it by marking a row `Unresolved` again. Both halves matter each
time: the row must leave the set **and** keep the grant the ruling gave it.

**TC-1-208 · no grant references `Role.Subcontractor`**
`permission-matrix.md` · P1 · Domain · spec.md §9
Given every row in the catalogue, when its grants are read, then none names `Role.Subcontractor`.
*Fails if:* one is added — harmless today because of TC-1-205, and a live hole the moment that
short-circuit is refactored.

**TC-1-209 · every grant cites `spec.md`**
`permission-matrix.md` · P1 · Domain · D-012
Given every row, when `SpecReference` is read, then it is non-empty.
*Fails if:* a permission is added with no traceable source — "a rule with no citation is not a rule,
it is a question".

**TC-1-210 · the right role without an assignment is refused**
`permission-matrix.md` · P1 · Api · spec.md §9
Given a Finance user holding `FinancialMovementPrepare` but not assigned to project A, when the
endpoint is called for A, then it is refused with `NotAssignedToProject`.
*Fails if:* the policy returns granted by default when no row is found.

**TC-1-211 · an assignment to one project does not open another**
`permission-matrix.md` · P1 · Api · spec.md §9
Given a Finance user assigned to project A, when they call the same endpoint for project B, then it is
refused.
*Fails if:* the assignment lookup ignores `ProjectId`.

**TC-1-212 · global reach stops at a project that does not exist**
`permission-matrix.md` · P1 · Api · D-010 · D-044 ruling 3
Given the Owner and then HR, when each acts against a project id that names nothing, then both are
refused.
*Fails if:* reach is implemented as a bypass, making a typo an authorization success.

**TC-1-213 · the session's claims decide nothing — both scopes**
`permission-matrix.md` · P1 · Api · **D-048** · kickoff §3
Given a Marketing user whose session carries a principal claiming `Role.Owner`, when they call a
**project-scoped** endpoint and then a **company-wide** endpoint, then **both are refused**.
*Fails if:* the role is taken from the principal. **D-048 removed the question rather than answering
it: the token now supplies only the user id**, and role, department, sub-department, client scope and
liveness are read from the users table on every authorized request. So this case asserts something
stronger than it used to — not *"the claim is checked"* but *"the claim is not consulted"* — and it
fails the moment a second axis is read back out of the principal for speed.
**Note this was staleness, not forgery** (D-048): the token is signed, so nobody mints claims. The
case is written with a forged-looking claim because that is the cheapest way to prove the claim is
ignored.

**TC-1-214 · the department is read from the database, not the session**
`permission-matrix.md` · P1 · Api · spec.md §9 *"Enforcement is server-side"* · **D-048**
Given a Marketing user whose session carries a department claim of `Operations` / `Administrative`,
when they call a `SiteExpenseConfirm` endpoint, then it is refused.
*Fails if:* the department is taken from the principal and never re-read. **Fixed 2026-08-20 (F-10,
D-048); this is regression cover.** It stays P1 rather than being retired because **one permission is
still granted by department with no role named** — `PhotoPublish` [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PhotoPublish`],
deliberately, and it is the last one. `SiteExpenseConfirm` was the other until **D-052 §1** closed
F-04. The department axis is still the one most worth getting wrong, and D-048's own closing line
stands: every department-only grant is *"a standing invitation to this defect."*

**TC-1-215 · a Site Engineer in Operations/Administrative does not confirm site expenses**
`permission-matrix.md` · P1 · Domain + Api · spec.md §8 *"entered by Finance or Admin, **not the
engineer**"* · **D-052 §1**
Given a `Role.SiteEngineer` user placed in `Department.Operations` / `Administrative`, when a
`SiteExpenseConfirm` endpoint is called, then it is refused.
*Fails if:* the grant matches on department alone with no role named.

**Fixed 2026-08-21 (F-04, D-052 §1); the Domain half of this case is regression cover, not a defect
case.** The `Permission.SiteExpenseConfirm` row [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm`] now grants it to `finance` (by role) and
to a second grant naming `Role = Role.TechnicalOffice` **plus** Operations / Administrative, so a
`Role.SiteEngineer` parked in that sub-department matches nothing. The Architect's ruling is the
mechanism, not the row: *"Financial permissions like `SiteExpenseConfirm` must never be granted to a
bare department without specifying a role."*

**The two tests that hold it down, both green:**
`A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `A_site_engineer_in_the_admin_sub_department_still_cannot_confirm_a_site_expense`]
asserts exactly the four outcomes this case describes — SiteEngineer in Ops/Admin →
`RoleNotGranted`; Finance → `Granted`; TechnicalOffice in Ops/Admin → `Granted`; TechnicalOffice in
Ops/**Technical** → `RoleNotGranted`, because every criterion on a grant has to match.
`No_financial_permission_is_granted_to_a_bare_department` [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department`]
pins the *class* across **twelve** money-touching permissions (eleven until `ProjectFinancialsEdit`
joined the list on 2026-08-22 — **F-34**), so the shape cannot come back on a
different row. **70/70 Domain green on a clean rebuild** — and D-052 records why the build's exit
code is checked before the test result is believed.

**The Api half is a different state and is not covered by any of that.** This case is labelled
`Domain + Api`; **no endpoint requires `SiteExpenseConfirm` yet**, so the Api half has nothing to
call and stays **unrunnable until slice 6, KAFF-608**. Do not read the green Domain result as the
whole case passing. D-052 is explicit that *"no endpoint calls it"* was a statement about reach and
never about whether the rule was right — which is why the Domain half was the half that mattered.

**QA-1 is answered by the ruling and no longer blocks this case.** See `permission-matrix.md`
**F-04** and RSK-05, both now closed. Kept at P1 rather than retired: `PhotoPublish`
[Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.PhotoPublish`] is still a bare-department grant, deliberately, and the department
axis is the one this project has now got wrong three times.

---

# The three permissions added 2026-08-22 — catalogue and evaluator **now**, endpoints in **slice 4**

> **Why this section exists.** `ProjectCreate`, `ProjectFinancialsEdit` and `UserRead` were added to
> the catalogue on 2026-08-22 (D-055 §§1–3) and shipped **reachable and named in no test anywhere**,
> while the Domain suite stood at 74/74 green (**D-056 §3**). ~~Backend has since written three
> tests.~~ **Four tests are in play, not three — F-31, 2026-08-22.** Three were written
> (`An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`,
> `Finance_edits_a_contracts_tax_settings_but_not_its_engineering_scope`,
> `Hr_may_read_the_user_list_and_still_reaches_nothing_financial`) and one was **repointed** from
> `ProjectManage` to `ProjectCreate` (`Only_the_owner_and_the_technical_office_may_open_a_project`),
> which is the only one that covers `ProjectCreate` at all
> [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `Only_the_owner_and_the_technical_office_may_open_a_project`,
> @ `PermissionEvaluatorTests.cs` -> `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`,
> @ `PermissionEvaluatorTests.cs` -> `Finance_edits_a_contracts_tax_settings_but_not_its_engineering_scope`,
> @ `PermissionEvaluatorTests.cs` -> `Hr_may_read_the_user_list_and_still_reaches_nothing_financial`].
> **No `TC-` case in this file named any of the three until today.**
>
> **The distinction that governs every case below, and it is the point of the section.** The three
> rows exist in code **now**; their **endpoints are slice 4** — the
> `Permission.ProjectFinancialsEdit` row says *"NO ENDPOINT YET. `Project.SetWithholding` is slice 4,
> KAFF-416"* [Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectFinancialsEdit`]. So the cases split in two, and each case says which it is and why:
>
> | Kind | Layer | When it runs | What it can assert |
> |---|---|---|---|
> | **Catalogue / evaluator** | `Domain` | **now, slice 1** | scope, grants, `TouchesMoney`, and the evaluator's three refusals — `ProjectNotSpecified`, `NotAssignedToProject`, `RoleNotGranted` |
> | **Endpoint** | `Api` | **slice 4** | the route, the gate on it, and the response **projection** |
>
> **A case that reads like an endpoint case and cannot run is how a suite fills with undated `PENDING`
> rows.** Nothing below is `PENDING`: the slice-1 cases are runnable today and the slice-4 cases have a
> citable expected result and a named slice. Neither is uncovered for want of an answer.
>
> **This is `process/agile.md` SM-30's level, deliberately.** *"What SM-30 does not require: an
> endpoint … their tests are catalogue and evaluator tests. That is the right level, and it is the
> level at which the mutation was watched to fail."*

## Slice 1 — catalogue and evaluator · runnable today

**TC-1-248 · `ProjectCreate` is company-wide, and that is the only instrument that reaches the act**
`permission-matrix.md` · P1 · Domain · **D-055 §3** · D-052 §2 · spec.md §2
Given `PermissionCatalogue`, when `ProjectCreate` is read, then its scope is **`CompanyWide`** and its
grants are exactly `Role.Owner` and `Role.TechnicalOffice`; and when the evaluator is called for a
Technical Office user with **`projectId: null`** — which is what a create request looks like — then the
decision is `Granted`.
*Fails if:* the row is made `ProjectScoped`. The evaluator then returns **`ProjectNotSpecified`** for
every caller, because a create request cannot name the project it is about to create, and *nobody can
open a project at all* — the exact state D-052 left behind and D-055 §3 fixed. It also fails if a third
role appears: Karim named the two and named the excluded — *"Site Engineers and Marketing have no
business creating projects."*

**TC-1-249 · widening `ProjectManage` to `CompanyWide` is caught here, and only here**
`permission-matrix.md` · P1 · Domain · **D-055 §3** · spec.md §9 *"Role alone is insufficient"*
Given `PermissionCatalogue`, when `ProjectManage` is read, then its scope is **`ProjectScoped`**; and
given a Technical Office user who **holds the grant and is not assigned** to the project, when the
evaluator is called with that project id, then the decision is **`NotAssignedToProject`**.
*Fails if:* somebody merges `ProjectCreate` back into `ProjectManage` by making `ProjectManage`
company-wide. **That is the smaller diff and it is the mistake this design exists to prevent** — it
fixes creation *by removing spec.md §9's assignment requirement from every project **edit***, silently,
for every holder. D-056 §3 watched the mutation: it turns **exactly one** test red, and before
2026-08-22 it turned nothing red at all. **This case is the QA-side of that one test**
(`An_unassigned_holder_of_ProjectManage_cannot_edit_a_project`);
it is written here so the guard survives somebody deleting the unit test as redundant.

**TC-1-250 · `ProjectFinancialsEdit` reaches the contract's tax setting and never the engineering scope**
`permission-matrix.md` · P1 · Domain · **D-055 §1** · D-049 rulings 9, 10 · spec.md §6.7
Given `PermissionCatalogue`, when `ProjectFinancialsEdit` is read, then its scope is **`ProjectScoped`**,
its grants are exactly `Role.Owner` and `Role.Finance`, and **`TouchesMoney` is true**; and given an
**assigned** Finance user, when the evaluator is called for `ProjectFinancialsEdit` then for
`ProjectManage`, then the first is `Granted` and the second is **`RoleNotGranted`**.
*Fails if:* Finance is added to `ProjectManage` instead. That was the one-line alternative and Karim
refused it in terms — *"The Finance department will never hold `ProjectManage`. An accountant must not
alter the engineering scope of a project."* **A grant written to reach one field would hand over the
whole record**, which is D-035, D-044 ruling 2 and F-04 seen from the other side. It also fails if
`TouchesMoney` is dropped: the flag puts the row in the **written-out list of twelve** money-touching
permissions [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department`], and that
list is written out rather than read from the flag **precisely so a permission cannot quietly stop
being financial and still pass** — so assert the row belongs in the list, not merely that the flag is
set.

**TC-1-251 · HR's global reach does not reach `ProjectFinancialsEdit`**
`permission-matrix.md` · P1 · Domain · **D-044 ruling 2** (*"zero financial visibility"*) · D-055 §§1–2
Given a `Role.Hr` user in `Department.Hr`, when the evaluator is called for `ProjectFinancialsEdit` on a
project reached by **`ProjectAccessPath.HrGlobal`** — reach granted, not denied — then the decision is
**`RoleNotGranted`**.
*Fails if:* reach is mistaken for capability. **HR is the one role that reaches every project without
an assignment row**, so it is the role for which a new project-scoped financial grant is most nearly
free: the assignment half of "role × assignment" does not stop HR, and only the grant list does. Karim
gave HR *"zero financial visibility (cannot see project costs, margins, or the safe)"* and a contract's
withholding rate is a cost. This case is the pair to `TC-1-252` — reading the user list must not
become reading the money.

**TC-1-252 · `UserRead` is names and roles, company-wide, and moves no money**
`permission-matrix.md` · P2 · Domain · **D-055 §2** (Q42) · D-044 rulings 1, 2
Given `PermissionCatalogue`, when `UserRead` is read, then its scope is **`CompanyWide`**, its grants
are exactly `Role.Owner` and `Role.Hr`, and **`TouchesMoney` is false** — it is absent from the list of
twelve; and given a `Role.Hr` user, when the evaluator is called for `UserRead` then for `UserManage`,
then the first is `Granted` and the second is **`RoleNotGranted`**.
*Fails if:* reading the list becomes editing it. `UserManage` is the Owner's alone (D-044 ruling 1) and
it is *"the most privileged operation in the system"* (`TC-1-050`) — whoever can create a user can set
a department and hand out project-assignment power. **Company-wide is correct and is not the risk
here:** a login list is not a project's data and HR must search it before anybody is assigned anywhere.
**The risk is the projection, and no permission case can catch it** — that is `TC-1-255`.

**TC-1-253 · the evaluator's three refusals, asserted against the three new rows**
`permission-matrix.md` · P1 · Domain · **D-055 §§1–3** · spec.md §9
Given each of the three new rows in turn, when the evaluator is called, then:
**(a)** `ProjectManage` and `ProjectFinancialsEdit` with **no project named** are refused with
**`ProjectNotSpecified`**, while `ProjectCreate` and `UserRead` with no project named are **`Granted`**
to their holders;
**(b)** a holder of `ProjectManage` and a holder of `ProjectFinancialsEdit` who are **not assigned** are
refused with **`NotAssignedToProject`**;
**(c)** a **non-holder** of each of the three — Marketing against `ProjectCreate`, Technical Office
against `ProjectFinancialsEdit`, Finance against `UserRead` — is refused with **`RoleNotGranted`**.
*Fails if:* the three refusals collapse into one. They are three different facts about a request and
the matrix reads them as different cells: `ProjectNotSpecified` says *the route is wrong*,
`NotAssignedToProject` says *§9 stopped you*, `RoleNotGranted` says *you never held it*. A handler that
answers a bare 403 for all three makes the scope split in `TC-1-248` and `TC-1-249` unobservable from
outside the domain — and **(a) is the assertion that proves company-wide and project-scoped are doing
different work on these rows** rather than being two spellings of the same thing.

**TC-1-254 · every catalogue row is named in a test, and every test a row cites exists — `F-28`**
`process/agile.md` **SM-30** · P2 · Domain · **D-057 §1** · D-056 §3
Given every row in `PermissionCatalogue`, when the Domain test sources are searched for the row's
member name, then **each row is named in at least one test**; and given every test name cited in a
row's comment, then **each identifier exists under `tests/`**.
*Fails if:* a row ships with no test — which is invisible to the Definition of Done, because *"a row
with no test does not make any test fail"* and the suite stayed **74/74 green** while three rows had
none. ~~It also fails **today**, on the second half: the `ProjectManage` row cites
`Opening_a_project_needs_no_project`, which exists only in `proposals/N10-project-creation.md` as a
*proposed* name and **is absent from `tests/`**. **Expected to fail on first run** — see
`qa/questions.md` **F-28**.~~ **Withdrawn 2026-08-22 — F-32: F-28 has been fixed.** The row now
cites `An_unassigned_holder_of_ProjectManage_cannot_edit_a_project` and
`Only_the_owner_and_the_technical_office_may_open_a_project`, and both exist
[Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.ProjectManage`,
@ `PermissionEvaluatorTests.cs` -> `Only_the_owner_and_the_technical_office_may_open_a_project`].
**The case is unchanged and is not retired** — only the prediction about its first run is
withdrawn, and it is withdrawn because the code moved, not because the expected result was
rewritten to match it. **Backend owes the mechanised half** (D-057 §1: *"the
enforceable half is coverage, not prose"*); until it exists this case is executed by reading, which is
weaker and is recorded as weaker.

## Slice 4 — endpoint cases · written now, **cannot run in slice 1**

> **Why these are written now and not in slice 4.** *"'No endpoint calls it' is a statement about
> reach, not about whether a rule is wrong"* (D-052, quoted back by D-056). The rule each case below
> asserts is decided **today**, by a ruling already made; only the route is missing. Writing them now
> is what stops the slice-4 session reading a green Domain suite as full coverage — which is the exact
> mistake `TC-1-215`'s Api half records.

**TC-1-255 · a `UserRead` endpoint returns names and roles, and nothing else — SLICE 4**
`permission-matrix.md` · P1 · Api · **SLICE 4, no endpoint exists** · **D-055 §2** (Q42) · D-044 ruling 2
Given an HR user holding `UserRead` and a users table whose rows carry username, department,
sub-department, `IsActive`, `DeactivatedAt`, `PasswordHash` and `SecurityStamp`, when the user-read
endpoint is called, then the response carries **the full name and the role, and no other field** — no
username, no department, no sub-department, no active state, no client id, no credential field — and
the same holds for every filter, every search term and every error body.
*Fails if:* the endpoint returns the user row. **The permission is not the control — the projection
is**, and the catalogue says so on the row itself: *"THE PERMISSION IS NOT THE WHOLE CONTROL — THE
ENDPOINT'S PROJECTION IS. Whoever builds the read endpoint projects name and role and stops. The user
row also carries usernames, departments and active state, and returning it would satisfy this
permission while breaking the ruling"*
[Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> `Permission.UserRead`].
*(The words above are the row's own — the paraphrase that stood here until 2026-08-22 was presented
as a quotation and was not one. **F-33.**)*
`questions-for-karim.md` -> `Q42` warned in terms not to close Q42 *"by handing HR the Owner's user
list"*, because that repeats **one screen over** the mistake Q32 was answered to avoid. Nabil's ruling
is narrower than the warning's worst case — **names and roles** — and nothing in the permission model
can hold that line. **`TC-1-252` cannot catch this and no Domain test can**; this case is the only one
that can, and it is the reason the pair is written as two cases rather than one.
*Fails also if:* the response is built from the internal user DTO with fields omitted. That is the
filtered view D-051 Q32 rejected and D-035 has now cost this project three times — a field added to the
shared type arrives on HR's screen with nobody deciding it should.

**TC-1-256 · the create-project endpoint is gated on `ProjectCreate`, not on `ProjectManage` — SLICE 4**
`permission-matrix.md` · P1 · Api · **SLICE 4, KAFF-407** · **D-055 §3**
Given the create-project route, when its authorization requirement is read, then it names
**`ProjectCreate`**; and when an Owner and a Technical Office user call it, then both succeed, and when
Marketing, Finance, HR and a Site Engineer call it, then all four are refused with 403; and when a
Technical Office user **assigned to no project** calls it, then it **succeeds**.
*Fails if:* slice 4 gates the create endpoint on `ProjectManage`. The request has no project to name,
so the evaluator answers `ProjectNotSpecified` and **the feature does not work at all** — at which
point the cheapest fix in the room is to widen `ProjectManage`, and `TC-1-249` is what stands between
that fix and spec.md §9. The last clause is the one that matters: a create must succeed with **no
assignment**, because there is nothing yet to be assigned to.

**TC-1-257 · the withholding endpoint is gated on `ProjectFinancialsEdit` and still requires the assignment — SLICE 4**
`permission-matrix.md` · P1 · Api · **SLICE 4, KAFF-416** · **D-055 §1** · D-049 rulings 9, 10 · spec.md §6.7
Given the endpoint behind `Project.SetWithholding`, when its authorization requirement is read, then it
names **`ProjectFinancialsEdit`**; and when an **assigned** Finance user calls it, then it succeeds;
and when an **unassigned** Finance user calls it, then it is refused with
`errors.auth.not_assigned_to_project`; and when Marketing, the Technical Office and HR call it — HR on
a project it reaches globally — then all three are refused with 403.
*Fails if:* the endpoint is gated on `ProjectManage`, which refuses Finance the one field Karim
assigned to them (D-049 ruling 10) and admits the Technical Office to a strict accounting parameter.
It also fails if the assignment check is skipped because the row *"is only a tax field"* — the row is
`ProjectScoped` deliberately, and §9's *"role alone is insufficient"* applies to money more than to
anything else. **🟡 Q-N10-2b is open and this case does not resolve it:** Finance has no global reach,
so on a newly-opened project Finance cannot set the category until HR or the Owner assigns Finance to
it, while Karim said Finance sets it *"during contract creation or approval"*. **That is a workflow
question for Karim** (D-055 §1) — this case asserts the permission as ruled and will need revisiting,
deliberately, if the answer changes the workflow.
*(The other half of this endpoint — that the client's kind is read from the database and never taken
from the request — is `TC-1-242`, F-25, RSK-20. The two are separate: this case is who may call it,
that case is what it may believe.)*
