# Kaff ERP — product backlog

One epic per slice. The slice sequence is `agents.md`'s release plan and **does not change to suit a
sprint boundary** (`process/agile.md`) — a slice too big for one sprint splits into `3a` / `3b` and
both halves keep the slice's gate.

**Only slice 1 is refined.** Slices 2–9 are titles and estimates, on purpose: `process/agile.md` says
refine one sprint ahead and no further, because *"writing slice 7's criteria now would mean writing
them against assumptions Karim has not been asked about."*

Estimates are Fibonacci and mean **uncertainty**, not hours. A story that moves money is never a 1.

**Revised 2026-08-21 (second revision)**, applying Karim's second round of five rulings and the
Architect's three (**D-051**) on top of D-049 and D-050, and recomputing Ready / BLOCKED
**transitively** — action **SM-1** from the sprint-1 refinement.

---

## Where the work sits

| # | Epic | Gate (`agents.md`) | Stories | Points | Blocked |
|---|---|---|---:|---:|---|
| 1 | Foundation | permission tests pass | 27 | 92 | ~~Q42~~ **closed, D-055 §2** · Q43 |
| 2 | Masters | Excel import works | 13 | 48 | Q12, Q13, Q29 |
| 3 | Treasury | **the worked example reconciles** | 20 | 120 | Q14, Q15, Q16, Q29 |
| 4 | Spine | prices provably frozen | 17 | 84 | ~~N10~~ **approved and built, D-055 §3** · **Q-N10-1, Q-N10-2b, Q-N10-3** (Karim), Q18, Q19, Q20, Q30 |
| 5 | Billing | §15 passes end to end | 15 | 86 | Q21, Q30 |
| 6 | Execution | deltas sum correctly | 13 | 56 | Q22 · **Q52** (does not block) |
| 7 | Accounting | balance sheet balances | 12 | 61 | Q23, Q24, Q25, Q40 |
| 8 | Closure, warranty, portal | portal leaks nothing | 15 | 65 | Q26 · **N7** (Architect, not Karim) |
| 9 | Mobile and offline | offline cannot move money | 8 | 47 | none open yet |
| | | | **140** | **659** | |

Questions are numbered in `questions-for-karim.md`, which is now the **one** register — UX and QA
questions merged into it with their origin recorded (action **SM-4**).

**What moved since the last revision**

- **Slice 1 went from 19 blocked points to zero.** All five blocking questions — Q27, Q31, Q32, Q33,
  Q38 — were answered on 2026-08-21 in a second round (**D-051**), and N5 with them. **Slice 1 has no
  BLOCKED story.**
- **⚠️ It does have an open question of Karim's, and this bullet used to deny it.** The sentence
  *"and no open question of Karim's"* was true of the five it was counting and of nothing else: eight
  questions raised the same day in `ux/` and `qa/` had never been merged into the register (fixed by
  refinement action **SM-8**). **The one that mattered was Q42, and it is now CLOSED — D-055 §2.**
  HR holds **`UserRead`**, `CompanyWide`, granted to `Role.Hr` and `Role.Owner`
  [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.UserRead`; enum member at
  @ `src/Domain/Authorization/Permission.cs` -> `UserRead`], so HR can name a person to put on a project.
  **KAFF-113's endpoint was never affected; HR's half of its screen is now buildable.**

  > **This bullet said HR *"holds no user-read permission of any kind (`Permission.cs` has no such
  > member)"*. That was true when written and became false on 2026-08-22.** Corrected under SM-29.

  **Two things the closure does not do, and they must not be lost.** The ruling is **names and roles
  only** — no editing, no salary visibility if one is ever added. And **the permission is not the
  whole control: the endpoint's projection is.** A `UserRead` endpoint returning the full user row
  satisfies the permission and breaks the ruling, because the row also carries usernames, departments
  and active state. Whoever builds it projects name and role and stops.

  **Q43** is the same screen, one field down, and **still open**. It does not make a story `BLOCKED`;
  it stops a field.
- **One ruling reversed another.** **Q27 reverses D-049 ruling 6:** a role change no longer *refuses*
  while the user supervises a project — it **automatically revokes every assignment**, Supervisor and
  Junior alike. `spec.md` §9 carries both rulings, the first marked `⚠️ SUPERSEDED`, on purpose.
  **KAFF-109 was rewritten completely**; its old rules said the opposite of the current ones.
- **KAFF-105 split** into `105a` (identity and permissions) and `105b` (the project list), both
  `Ready`. Approved by the Architect and recorded in D-051 — **the second time a story was blocked
  whole when only part of it was unanswerable**, the first being KAFF-101.
- **Four re-estimates, all of them because an answer added work rather than only unblocking it:**
  KAFF-100 3 → 5 (an unauthenticated endpoint minting the Owner, resting on an atomic emptiness
  check), KAFF-115 2 → 3 (a new permission and a second surface for HR), KAFF-102 3 → 2 (N5 answered
  "no session table", so what is left is clearing a cookie), and KAFF-105's 3 becoming 2 + 3 across
  the split.
- Earlier this day: **KAFF-101 split** into `101a` and `101b` (**F-22**), and **KAFF-122 is
  `Superseded`** — the withholding rate moved off the client onto the contract (D-049 ruling 9), its
  3 points moving to **KAFF-416**, slice 4.

---


---

## Sprint 1 — the committed scope and the build order

**Locked by Nabil at 15 stories / 57 points.** Recorded here 2026-08-22 because until today **the only
record of what was committed lived in `meetings/`** — a durability gap of exactly the kind this project
keeps hitting. The backlog lists what exists; this section says what was *agreed*.

**Deferred — 10 stories, 33 points:** KAFF-101b, 104, 105b, 115, 117, 119, 120, 121, 123, 124.
**KAFF-107 folded** into KAFF-106 and KAFF-108 (16/59 → 15/57). **KAFF-122 superseded** by KAFF-416.


### Build order — re-derived 2026-08-22, after D-061

**Supersedes the 19:22 order.** That one was derived before the Architect closed V-01, and two
stories have since gone `BLOCKED` on open questions.

**What changed:** **V-01 is closed** (D-061 — the audit mechanism records events, not only entity
changes; `AttributeTo` solves the bootstrap actor). The V-01 gate that held 100, 102 and 118 is
**gone**. Two new blocks replace it, and both are questions rather than defects.

| # | Story | State |
|---|---|---|
| 1 | ~~**KAFF-116**~~ ✅ **ACCEPTED 2026-08-23** — Verifier recommended, D-068 concurs. `AuditRecord.GrantPath` (nullable `ProjectAccessPath`), migration `20260822210402_AuditGrantPath`, 5 new Api tests covering all four grant paths. Clean, **zero dependencies**, and unbackfillable by nature — it lands the grant-path column on the same table D-061 just extended. **Start here** |
| 2 | ~~**KAFF-106**~~ **BUILT — NOT ACCEPTED. Held open on V-A** (the 403 carries no `messageKey`, so the Arabic UI has nothing to render for `AC-106-B`). 8 of 11 criteria verified; `AC-106-J` carried forward explicitly, `AC-106-H` correctly deferred. Vertical slice at `src/Api/Features/Users/CreateUser/`, plus `PasswordHasher`; 13 Api tests; **four watched-to-fail mutations recorded** — `decisions.md` D-066. **The first business endpoint in the system.** V-05 closed: `AC-106-K` appended and enforced at the endpoint |
| 3 | ~~**KAFF-108**~~ **BUILT 2026-08-24, awaiting verification.** 7 criteria, 11 tests. **Shipped with no permission gate at all — D-067, a privilege-escalation primitive, fixed.** No `Response.cs`/`Validator.cs` and that is correct: 204 has no body, and the request's only rule is the domain's `ValidateDepartment` |
| 4 | **KAFF-113** | Clean apart from SM-10's deferred-story reach |
| 5 | **KAFF-110** | Clean |
| 6 | **KAFF-114** | Clean (needs 113) |
| 7 | **KAFF-111** | Clean (needs 110, 113) |
| 8 | KAFF-112 | needs 110, 111 |
| 9 | **KAFF-109** | needs 106, 113, 111. **Was fourth in the pre-review order, ahead of its own dependencies — V-06, and it was my error** |
| 10 | **KAFF-100** | **Unblocked by D-061.** `IAuditContext.AttributeTo` puts the new Owner on the `Created` record, and an authenticated request naming another actor throws |
| 11 | **KAFF-102** | **Unblocked by D-061** — sign-out is an `AuditEventKind.SignedOut` event |
| 12 | KAFF-103 | needs 100, 106 |
| 13 | **KAFF-118** | **Unblocked by D-061.** Rule 2 holds in full: no handler constructs a record |
| — | ~~**BLOCKED**~~ **`Ready`, one clause held** | **KAFF-101a** — ~~V-02 / N9~~ answered (D-062 §2); ~~rule 16~~ **rewritten by the BA, 2026-08-23**; ~~401-vs-403~~ **decided: 401** (D-063 §1); ~~the audit criterion~~ **written as `AC-101a-O`**; ~~`AC-101a-G`'s refusal shape~~ **RULED 2026-08-23 — the generic `401` / `errors.auth.invalid_credentials`, D-065 case 5, which closes D-063 A-02**. **Unblocked and buildable.** Three things remain and none of them stops the start: **(a) 🟡 the locked account — Q47 case 3, OPEN with Nabil**, who ruled a distinct `423` and had it flagged back because a 423 exists only when the username does. It is **struck out of `AC-101a-B` and covered by no criterion in either shape**, and **rule 14 is left unchanged and unruled**; D-065 sequences it last so it is a prerequisite for nothing. **(b) the IP column** (D-063 §2 — N-19, must land before this story **ships**) and **(c) the nullable subject** (D-063 §3) — **build dependencies, not questions**, and only `AC-101a-O` sits behind them |
| — | **BLOCKED** | **KAFF-105a** — V-03, rule 3 vs `AC-105a-C`. The BA refused to invent the tie-break and was right to: **neither source decides field-or-refusal.** N-04 / Q-UX-18 / SM-16. **Untouched by tonight's three rulings — checked, not assumed** |
**KAFF-116 is ACCEPTED. KAFF-106 and KAFF-108 are built and awaiting acceptance — KAFF-106 held open on V-A.**
**KAFF-116 is ACCEPTED. KAFF-106 and KAFF-108 are built and awaiting acceptance — KAFF-106 held open on V-A.**
**KAFF-113 is the front of the queue**, with KAFF-110 beside it; 114, 111, 112 and 109 behind those.
**The clean set was never five parallel starts** — it is one start and a queue, which is the honest
shape and the reason I have not described it otherwise.
**Backend is on V-A, V-B and V-E. The Architect is on A-04 — the endpoint-gate test that D-067 proved is needed.**

**~~Neither block is a question any more, and that is the change.~~ ~~Nothing in sprint 1 is waiting
on Karim tonight.~~ Struck 2026-08-23 — it was true when written and is not now.** The BA rewrite and
the Architect decisions all landed, and what they uncovered underneath is a question: **`AC-101a-G`
is Q47's fifth case, and Q47 is Karim's.** ~~KAFF-101a now waits on **one Karim answer** and **two
builds**.~~ KAFF-105a still waits on N-04 / Q-UX-18, which is the Architect's and Nabil's. ~~**One
sprint-1 story is waiting on Karim**~~ — and the sentence is left struck rather than deleted, because a
claim that ages between being written and being read is SM-29's whole subject.

**Struck again the same day, 2026-08-23, and the second strike is the demonstration.** **D-065 ruled
Q47** — four of its five cases, including the fifth that had just been added. **No sprint-1 story is
waiting on Karim.** KAFF-101a is `Ready` with **one clause held**: Q47 case 3, the locked account,
which Nabil ruled to a distinct `423` and which was **flagged back to him** rather than applied,
because a 423 exists only when the username does and five failed attempts manufacture one on demand.
**That residual is Nabil's, not Karim's.** The two remaining items — the **IP column** (D-063 §2) and
the **nullable subject** (D-063 §3) — are **builds, not questions**, and `AC-101a-O` is the only
criterion behind them. **KAFF-105a is untouched by D-065 — checked, not assumed.**

## Slice 1 — Foundation

**Epic:** auth, roles, assignment, audit, Client master
**Gate:** permission tests pass
**Refined.** Full stories in `slice-1-foundation/`.

| ID | Title | Pts | Status | Depends on |
|---|---|---:|---|---|
| KAFF-100 | Bootstrap the first Owner through a one-time setup screen | **5** | Ready | — |
| KAFF-101a | Sign in, and the server sets an `HttpOnly` session cookie | 5 | **`Ready` — one clause held** (Q47 case 3, D-065) | 100 |
| KAFF-101b | The staff sign-in screen, and where each role lands after it | 3 | Ready | 101a, 105a |
| KAFF-102 | Sign out | **2** | Ready | 101a |
| KAFF-103 | Change the temporary password on first sign-in | 5 | Ready | 100, 101a, 106 |
| KAFF-104 | Reset a forgotten password with an Owner-generated link | 5 | Ready | 101a, 103, 106 |
| KAFF-105a | `GET /api/auth/me` returns who I am and what I may do | **2** | **BLOCKED** — V-03, rule 3 vs `AC-105a-C` (N-04 / Q-UX-18 / SM-16) | 101a |

> **⚠️ This table records story *state*, and it contradicted the build order above it until 2026-08-23.**
> `KAFF-105a` read `Ready` here while the order three rows up read `BLOCKED`. **A Backend agent reading
> only this table would have started a blocked story.** Found by the BA against a file it does not own
> and fixed by the Scrum Master, who does. **When these two disagree the build order is authoritative**
> — it is recomputed every time a blocker moves; this table is a backlog inventory.
| KAFF-105b | `GET /api/auth/me` returns the projects I reach, and how | **3** | Ready | 105a, 113, 114 |
| KAFF-106 | The Owner creates a user with a role and a department | 5 | Ready | 100 |
| KAFF-107 | An HR user cannot be created or moved outside the HR department | 2 | Ready | 106 |
| KAFF-108 | Move a user between departments | 3 | Ready | 106 |
| KAFF-109 | Change a user's role — **rewritten, D-051 reverses D-049 ruling 6** | 5 | Ready | 106, 113, 111 |
| KAFF-110 | Deactivate a user, and their access ends on the next request | 5 | Ready | 106 |
| KAFF-111 | Deactivating a user revokes their project assignments | 3 | Ready | 110, 113 |
| KAFF-112 | Reactivate a user, who comes back with nothing | 3 | Ready | 110, 111 |
| KAFF-113 | Assign a user to a project, with seniority for site engineers | 5 | Ready | 106 |
| KAFF-114 | Revoke a project assignment without losing who could act when | 3 | Ready | 113 |
| KAFF-115 | The project team panel, and HR's separate Project Team screen | **3** | Ready | 113, 114, 105b |
| KAFF-116 | Every audit record says how the actor reached the project | 3 | Ready | — |
| KAFF-117 | The Owner reads the audit trail, and nobody else does | 5 | Ready | 116, 118 |
| KAFF-118 | Every state change in slice 1 writes an audit record | 3 | Ready | 106, 109, 110, 111, 113, 119 |
| KAFF-119 | Register a client, with a generated code and a duplicate-phone warning | 5 | Ready | 106 |
| KAFF-120 | An individual's contract cannot carry a withholding rate — **defect, now wiring** | 2 | Ready | 119 |
| KAFF-121 | Edit a client's name and contact details | 3 | Ready | 119 |
| KAFF-122 | ~~Set a corporate client's withholding category~~ | — | **Superseded** → KAFF-416 | — |
| KAFF-123 | Archive a client | 2 | Ready | 119 |
| KAFF-124 | Find a client by name, code or phone | 2 | Ready | 119 |

### The committable scope, computed transitively

**Action SM-1, recomputed after D-051.**

**Position: 26 stories, 92 points, all `Ready`. Nothing BLOCKED. No soft dependencies left.**
*(27 rows in the table above; KAFF-122 is `Superseded` and carries no points here.)*

The two figures this replaces are worth keeping visible, because the arithmetic moved twice for two
different reasons: *"14 Ready, 43 points"* was **wrong** — six `Ready` stories depended on a `BLOCKED`
one (**F-21**); *"20 Ready, 69 points, 5 BLOCKED"* was **right when it was written**; and **92 is what
five answers plus four re-estimates produce.** A rising Ready number is not always progress, and here
it is: nothing was unblocked cheaply and no rule was softened.

**The soft dependency is gone, not waived.** KAFF-101a, KAFF-103 and KAFF-106 named KAFF-100 as a
dependency and were `Ready` anyway, justified by a verifiable fact — the Api harness issues identities
directly (`TestAuthHandler`, slice 0) and slice-1 fixtures create `User` rows directly. **Q31 is now
answered and KAFF-100 is `Ready`, so the argument is retired rather than relied on**, and the demo's
first step exists. It is recorded here because F-21's warning still applies to the next such case:
*"the risk is not that the sprint stalls, it is that somebody unblocks it cheaply."*

**One dependency was added, not removed.** KAFF-118 gets KAFF-109 back (it was removed under **F-20**
while KAFF-109 was BLOCKED). A role change now writes four records where it used to write one, and
AC-118-D is where that is asserted.

### What to commit first

Not the BA's call, but four things are worth putting in front of the Scrum Master:

1. **92 points is more than one sprint**, and the Ready set no longer chooses itself by elimination.
   Choosing from it **is** the scope commit, and `process/agile.md` says refinement ends with one or it
   has not ended.
2. **KAFF-116 regardless of what else is cut.** The refinement's §5 makes the case and it has not
   weakened: audit records are append-only and trigger-protected, so the grant-path column **cannot be
   backfilled**. Karim's rulings made it sharper — there are now two roles that reach projects with no
   assignment row, and without the column *"Owner, globally"* and *"assigned on 3 June"* are identical
   in the record.
3. **The permission-mechanism stories** — 106, 107, 108, 109, 110, 111, 112, 113, 114, 115 — which
   **are** the slice gate. KAFF-109 joins them now that Q27 is answered, and it is the one whose rules
   inverted, so it should not be built by whoever remembers the old ones.
4. **KAFF-101a's security-stamp mechanism is BUILT** — decisions.md D-053 §1, 2026-08-22. D-051 (N5)
   had recorded that the stamp was rotated and its claim type defined while **nothing compared the
   two**; the comparison now runs on every authorized request and refuses a mismatch or an absence.
   Every "a password change kills every session" criterion in slice 1 rests on it, and they now rest
   on something real. Rule 11a and AC-101a-N are verification, not construction.

---

## Slice 2 — Masters

**Epic:** catalogue, أبواب, employees, workers, subcontractors, suppliers
**Gate:** Excel import works

| ID | Title | Pts |
|---|---|---:|
| KAFF-200 | Import the catalogue from Excel at setup, all-or-nothing | 5 |
| KAFF-201 | Re-importing is not a sync — a second import is a deliberate, reviewed act | 2 |
| KAFF-202 | Create and edit a catalogue item | 3 |
| KAFF-203 | Find a catalogue item by code or description | 3 |
| KAFF-204 | The باب tree, carrying each trade's default markup | 5 |
| KAFF-205 | Re-parent a باب, and move an item between أبواب | 3 |
| KAFF-206 | Archive a catalogue item without breaking what already references it | 3 |
| KAFF-207 | Employee register — exactly one record per costed person | 5 |
| KAFF-208 | Nobody appears in both populations: day labour and salaried | 3 |
| KAFF-209 | Register a worker from site, deduplicated by phone | 5 |
| KAFF-210 | Worker engagement history, day rate, frequency and rating | 3 |
| KAFF-211 | Subcontractor master with rates | 5 |
| KAFF-212 | Supplier master — one account serving many projects | 3 |
| | | **48** |

**Blocked by:** Q12 and Q13 (both D-045, both raised by Karim's own ruling and both due **before** this
slice opens rather than during it), and now **Q29** — whether the withholding rate on a subcontractor
or supplier follows ruling 9 onto the job, or stays on the party record where it is today. KAFF-211 and
KAFF-212 build those records.

**Carry into KAFF-209 from D-049 ruling 8:** the worker master is *"deduplicated by phone"* in exactly
the words §2 uses for the client, and Karim's ruling softened that to a warning **for the client**. It
was not asked about workers, and the unique index on the worker phone is still there. **Do not extend
the ruling; ask.** It is the same shape as Q29.

---

## Slice 3 — Treasury

**Epic:** postings, accounts, the five ledgers, non-cash types
**Gate:** **the worked example reconciles** — `spec.md` §15

| ID | Title | Pts |
|---|---|---:|
| KAFF-300 | The §15 worked example as a fixture — present and failing before anything else is built | 5 |
| KAFF-301 | Post a movement between two accounts, append-only | 8 |
| KAFF-302 | Create a project's account set when a project is created | 5 |
| KAFF-303 | Correct a mistake with a reversing posting, never an edit | 5 |
| KAFF-304 | Every balance is derived by summing postings | 5 |
| KAFF-305 | The posting-type × account-pair legality table | 8 |
| KAFF-306 | Client advance: in at signing, recovered to exactly zero, never negative | 8 |
| KAFF-307 | Hold: accumulates only, and nothing leaves it before handover | 8 |
| KAFF-308 | Release the hold once, in full, at handover | 5 |
| KAFF-309 | تشوينات: `MaterialAdvance` in at 75%, recovered as material is installed | 8 |
| KAFF-310 | Firm advance under an owner-approved hard cap, with aggregate exposure on the dashboard | 8 |
| KAFF-311 | عهدة: junior drafts, supervisor submits, accounts pays — one open at a time | 8 |
| KAFF-312 | `OwnerCurrentAccount`: injection as a liability, withdrawal as advance or drawing | 5 |
| KAFF-313 | A payment that would overdraw the Safe is refused and prompts an owner injection | 5 |
| KAFF-314 | The five ledgers never net against each other | 3 |
| KAFF-315 | Non-cash posting types from day one | 5 |
| KAFF-316 | Collections: method, date, reference — and cheque states | 5 |
| KAFF-317 | Withholding at source recorded on a corporate client's collection | 8 |
| KAFF-318 | Withholding Kaff carries as a liability on subcontractor and supplier payments | 5 |
| KAFF-319 | A refused posting reads as a translated message, not a 500 | 3 |
| | | **120** |

**Split it.** 120 points is not a sprint. Suggested: `3a` = KAFF-300…305, 319 (36 points, the engine and
its guards); `3b` = KAFF-306…318 (84 — itself likely two). Both keep the gate.

**Blocked by:** Q14 (confirm the تشوينات direction — D-034's fix is unconfirmed and §15 cannot be posted
if it is wrong), Q15 (which banks), Q16 (overdrafts), and **Q29** for KAFF-318.

**An ordering problem created by D-049 ruling 9, and it lands here.** KAFF-317 computes withholding on
a collection using a rate that now lives on the **contract** — and the story that lets Finance set it
is **KAFF-416, in slice 4, which runs after this one**. Slice 3 works against project fixtures, so it
is not a blocker, but **the fixtures must carry a rate somebody set deliberately**, and `Project`
defaults to `None` on purpose. The §15 worked example is the thing that will notice if they do not.

**Two accepted exposures to carry into this slice, from D-044 ruling 8:** the `FirmAdvance` and
`MaterialAdvance` database floors were removed on Karim's instruction. Nothing now stops a firm advance
recovery running past zero, and §15's *"تشوينات in equals تشوينات recovered"* is no longer enforced at
the point of posting. **The QA scenarios must cover both, because the database no longer will.**

---

## Slice 4 — Spine

**Epic:** opportunity, pipeline, quotation, conversion, BOQ freeze
**Gate:** prices provably frozen

| ID | Title | Pts |
|---|---|---:|
| KAFF-400 | Capture a lead and move it through the pipeline | 5 |
| KAFF-401 | Inactivity alerts on day 2 and day 4; `Stalled` on day 7; activity revives it | 3 |
| KAFF-402 | Closed Lost records a reason; reopening attaches to the same client | 3 |
| KAFF-403 | معاينة is billable, held as a deposit on the Opportunity, credited at Closed Won | 5 |
| KAFF-404 | Pre-contract expenses tracked against the Opportunity and never recovered from the client | 3 |
| KAFF-405 | Build a quotation from the catalogue, with per-project conditions and line markup | 8 |
| KAFF-406 | Margin shows total cost, total sell and profit % — never one blended figure | 3 |
| KAFF-407 | Who creates a project | 3 |
| KAFF-408 | Convert Closed Won into a Project of one of three types, carrying client and site visit | 8 |
| KAFF-409 | The حصر produces the binding BOQ; all three quantity states are retained | 8 |
| KAFF-410 | The BOQ freezes catalogue values **by copy** at signature — no reference to follow | 8 |
| KAFF-411 | Open estimates on old pricing are flagged for a human decision, never re-priced silently | 5 |
| KAFF-412 | Custom BOQ items queue for Technical Office review and never write the catalogue | 3 |
| KAFF-413 | Project state machine, and a project never mutates from one type into another | 5 |
| KAFF-414 | A stopped project logs, and does not bill | 3 |
| KAFF-415 | Linked projects: `design_to_execution` and `parent_child` | 8 |
| **KAFF-416** | **Finance sets the contract's withholding category** — replaces KAFF-122 | **3** |
| | | **84** |

**KAFF-416 — new, 2026-08-21, replacing slice 1's KAFF-122.** Karim moved the withholding rate off the
client and onto the contract, and gave it to Finance rather than Marketing (D-049 rulings 9–10, §6.7
amendment). `Project.WithholdingCategory` and `Project.SetWithholding(category, clientKind)` **already
exist and already refuse a rate on an individual's contract** [Verified: 2026-08-22 @
`src/Domain/Projects/Project.cs` -> `WithholdingCategory`, `SetWithholding`]; what does not exist is any endpoint that creates or edits
a project. The default is `None`, deliberately: between creation and Finance's decision a contract
must claim no rate rather than guess one, because a guessed rate and a decided one are
indistinguishable by the time an extract is issued.

**The permission side is now settled in full, and KAFF-416's permission is not `ProjectManage`.** The
row was split three ways on 2026-08-22 (**D-055 §§1–3**, approving N10), all
[Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs`]:

| Permission | Scope | Grants | Governs |
|---|---|---|---|
| `ProjectCreate` | `CompanyWide` | Owner, Technical Office | **opening** a project (`:213-215`) — **KAFF-407** |
| `ProjectManage` | `ProjectScoped` | Owner, Technical Office | **editing** a project; §9's assignment requirement still applies (`:200-202`) |
| **`ProjectFinancialsEdit`** | `ProjectScoped`, `TouchesMoney` | Owner, **Finance** | the contract's tax and financial settings alone (`:238-241`) — **KAFF-416** |

**The Finance department will never hold `ProjectManage`** — an accountant must not alter the
engineering scope of a project (D-055 §1). And **nobody merges the three back**: widening
`ProjectManage` to company-wide would fix creation by removing §9's assignment requirement from every
edit, which is the hole the split exists to avoid.

**Blocked by:** ~~**N10**~~ — **approved and built, D-055 §3. Slice 4 is no longer blocked on a
permission.** What blocks **KAFF-407 and KAFF-416** is three **workflow** questions, all Karim's, all
registered in `stories/questions-for-karim.md`:

- **Q-N10-1** — does opening a project put its creator on it? A Technical Office user who opens one
  holds no assignment row and cannot read it one line later.
- **Q-N10-2b** — Finance has no global reach [Verified: 2026-08-22 @
  `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `EvaluateAsync` — only `Role.Owner` and `Role.Hr`;
  everyone else falls through to the assignment lookup], so Finance cannot set a new contract's
  withholding until somebody assigns Finance to that project, while Karim said Finance sets it
  *"during contract creation or approval"*. **KAFF-416.**
- **Q-N10-3** — does opening a project need the Owner's approval? **A state machine, not a
  permission**, so it belongs in KAFF-407's story and choosing a permission now would foreclose it.

> **Corrected 2026-08-22 under SM-29.** This section said slice 4 was *"blocked on the scope
> question, **N10**, which is the Architect's and not Karim's"*, and cited
> `PermissionCatalogue.cs` at lines 180-182, which is not where `ProjectManage` is. Both were true on
> 2026-08-21 and false the next day. **Q17 is closed in full** — holder by D-052 §2, scope residual
> by D-055 §3.

Also open for slice 4: Q18 and Q19 (what متعثرة and تم تأجيلها mean, and which spelling Kaff uses),
Q20 (does a restarted project carry on), and **Q30** (may a rate change after the first extract).

**Protected, from the kickoff:** nobody puts a project status chip on a screen "because it's useful".
The five Arabic labels are unmapped on purpose until Q18 is answered.

---

## Slice 5 — Billing

**Epic:** the extract chain, three calculators, change orders
**Gate:** §15 passes end to end

| ID | Title | Pts |
|---|---|---:|
| KAFF-500 | Prepare an `Extract` from executed quantities | 5 |
| KAFF-501 | The approval chain: QC → Technical Office → Accounts → Owner → Issued | 8 |
| KAFF-502 | The Technical Office gate is splittable — `PartiallyApproved` until the NCR closes | 8 |
| KAFF-503 | Any rejection at any gate returns to Draft with a stored reason | 3 |
| KAFF-504 | The extract shows work value, hold this period, hold to date, advance recovered, تشوينات, net payable | 5 |
| KAFF-505 | Lump Sum billing calculator | 8 |
| KAFF-506 | Cost Plus billing calculator — supervised, exempt, non-billable | 8 |
| KAFF-507 | Design billing — five stages at fixed weights, billed on client approval | 8 |
| KAFF-508 | Change orders appear in their own section at their own prices | 5 |
| KAFF-509 | The change order chain, and an approved one raises the contract value | 5 |
| KAFF-510 | Subcontractor extracts, weekly, with 5% retained | 8 |
| KAFF-511 | A disputed issued extract resolves as a revised extract or a credit note | 5 |
| KAFF-512 | One `Adjustment` object for every case where money flows back | 5 |
| KAFF-513 | A stopped project issues no extract | 2 |
| KAFF-514 | Nobody creates and approves the same extract | 3 |
| | | **86** |

**Blocked by:** Q21 (rounding direction, and whether the contractual figure on an extract is 2 decimals
or 4 — the accountant's question, not Karim's) and **Q30** (whether a contract's withholding rate may
change once the first extract is issued — a rate that moves afterwards makes two extracts on one
contract irreconcilable).

**Q10 is closed.** The rate belongs to the contract, so a design extract and an execution extract for
one client can differ, which is what this slice needed (D-049 ruling 9).

---

## Slice 6 — Execution

**Epic:** daily log, عهدة, site expenses
**Gate:** deltas sum correctly

| ID | Title | Pts |
|---|---|---:|
| KAFF-600 | One daily log per engineer per project, only where he is assigned | 5 |
| KAFF-601 | The daily log records period deltas, never cumulative totals | 5 |
| KAFF-602 | Two engineers' entries for the same day sum; same engineer, same field, latest wins | 5 |
| KAFF-603 | Executed quantities feed the surveyed ↔ executed variance | 5 |
| KAFF-604 | Day labour captured from the daily log | 5 |
| KAFF-605 | Materials and تشوينات recorded on site | 5 |
| KAFF-606 | Site photos captured, and published deliberately rather than mirrored | 3 |
| KAFF-607 | Check-in and check-out | 3 |
| KAFF-608 | An engineer's expense entry is a draft that Accounts confirms and posts | 5 |
| KAFF-609 | عهدة requested from site: junior drafts, supervisor submits | 5 |
| KAFF-610 | No new عهدة before the previous one is cleared with receipts | 5 |
| KAFF-611 | A stopped project still accepts a log recording the stoppage and its reason | 2 |
| KAFF-612 | The weekly QC report | 3 |
| | | **56** |

**Blocked by:** Q22 alone — the عهدة ceiling formula and the 10,000 EGP per-request cap, both 🟡 in
§6.4.4 and both owned by Karim in §16 assumption 4.

**Q34 is closed and F-04 is fixed** (D-052 §1). `SiteExpenseConfirm` names `Role.Finance` and
`Role.TechnicalOffice` conditional on Operations / Administrative
[Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.SiteExpenseConfirm` — the citation read
`:238-248` until today, before three rows were added on 2026-08-22], so the site engineer §8 excludes
**by name** holds nothing, and `No_financial_permission_is_granted_to_a_bare_department`
[Verified: 2026-08-22 @ `tests/Domain.Tests/PermissionEvaluatorTests.cs` -> `No_financial_permission_is_granted_to_a_bare_department`] holds the class across
the money-touching permissions rather than the one row that failed. **There is a third layer now:**
the evaluator discards any role-less grant on a `TouchesMoney` permission at the point of decision
[Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `TouchesMoney`, D-053 §2].
KAFF-608 inherits a fixed grant instead of a question.

**Open but not blocking: Q52** — `PhotoPublish`
[Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.PhotoPublish` — the citation read
`:258` until today] is the last bare-department grant, deliberately left, because the Architect's ruling is scoped to *financial*
permissions and a photo moves no money. It needs its own ruling before slice 6 renders it.

---

## Slice 7 — Accounting

**Epic:** depreciation, accruals, close, statements
**Gate:** balance sheet balances

| ID | Title | Pts |
|---|---|---:|
| KAFF-700 | Asset register with depreciation, defaulted from Law 91/2005 Art. 25 and editable per asset | 8 |
| KAFF-701 | Accruals and prepayments as posting types | 5 |
| KAFF-702 | Month-end close makes a period immutable | 5 |
| KAFF-703 | Who performs the close | 2 |
| KAFF-704 | Trial balance export | 3 |
| KAFF-705 | Equity accounts, and profit rolling into retained earnings at year close | 5 |
| KAFF-706 | Revenue recognition at month close, with the contract asset / liability difference shown | 8 |
| KAFF-707 | Every expense tagged project or company at the moment of spending — never both, never neither | 5 |
| KAFF-708 | The monthly overhead spread — a report, not postings | 5 |
| KAFF-709 | Budget baseline from signed BOQ cost plus tolerance; only an approved change order moves it | 5 |
| KAFF-710 | Budget alerts fire on committed money, not on cash paid | 5 |
| KAFF-711 | Bank loans and financed equipment | 5 |
| | | **61** |

**Blocked by:** Q23 (who closes the period — `PeriodClose` is `Unresolved` in the catalogue with Finance
assumed, and since D-052 answered Q17 it is **the only `Unresolved` row left**: one `Unresolved: true`
[Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.PeriodClose` — the citation read
`:292-293` until today, which is not where the row is] and no other), Q24 (any bank loan or
financed equipment — §16 #16), Q25 (opening capital and retained earnings — §16 #17), and **Q40** (which
way a time axis runs in an Arabic chart — the first chart in the system is built here).

---

## Slice 8 — Closure, warranty, portal

**Gate:** portal leaks nothing

| ID | Title | Pts |
|---|---|---:|
| KAFF-800 | Practical completion and the snag list | 5 |
| KAFF-801 | Major snags block handover; minor snags do not | 5 |
| KAFF-802 | Handover releases the hold in full, even with minor snags open | 5 |
| KAFF-803 | Snag resolution: debit the subcontractor, or absorb it as internal cost | 5 |
| KAFF-804 | Warranty starts automatically on the handover date and runs four months | 3 |
| KAFF-805 | Callbacks attach to the warranty; cost falls on the party at fault | 5 |
| KAFF-806 | Subcontractor retention releases when the warranty ends | 5 |
| KAFF-807 | A project closes only when every account is settled | 5 |
| KAFF-808 | Design closure: documents delivered, last 10% collected, IP transfers | 5 |
| KAFF-809 | Furnishing as a linked child project | 3 |
| KAFF-810 | A separate `/api/portal/*` surface with unshared response types | 8 |
| KAFF-811 | A reflection test fails the build if anything cost-shaped is reachable from a portal response | 5 |
| KAFF-812 | The client approves priced change orders and design stages | 5 |
| KAFF-813 | Deliverables are watermarked until paid | 3 |
| KAFF-814 | Closure produces marketing assets, and a referral opportunity can be created manually | 3 |
| | | **65** |

**Blocked by:** Q26 (sub retention at 5%, released at warranty end, zeroable per subcontractor — §5.1 🟡,
§16 assumption 19).

**Q33 is answered and it lands here.** The portal is **a separate host** — *"a completely isolated
interface"* (D-051 Q33). That strengthens KAFF-810 and KAFF-811 rather than changing them: the
boundary becomes infrastructural instead of a matter of every future endpoint remembering.
**What it did not settle is `N7`** — separate deployment, or the same API behind a second origin. The
second still needs D-050's cookie and CORS worked through: `__Host-` forbids a `Domain` attribute, so
a second origin means a second cookie and a second session boundary, and CORS must name both origins
explicitly because credentials are in play. **It is the Architect's and Nabil's, not Karim's**, and it
must be settled before KAFF-810 is built.

---

## Slice 9 — Mobile and offline

**Gate:** offline cannot move money

| ID | Title | Pts |
|---|---|---:|
| KAFF-900 | Offline-first daily log on the phone | 8 |
| KAFF-901 | Additive sync — deltas merge without conflict | 8 |
| KAFF-902 | Same engineer, same day, same field: latest timestamp wins | 5 |
| KAFF-903 | عهدة requested offline creates a draft and nothing else | 5 |
| KAFF-904 | Invoice photos and site photos captured offline | 5 |
| KAFF-905 | Check-in and check-out offline | 3 |
| KAFF-906 | Money never moves offline — approval and disbursement are online, against a live balance | 8 |
| KAFF-907 | Arabic RTL on the phone, at mobile width, in the field | 5 |
| | | **47** |

**Blocked by:** nothing open yet — which almost certainly means this slice has not been read closely
enough. `process/agile.md`: *"A refinement session that produces no questions has not been run
properly."* Refine it when slice 8 is in flight, not before.

**One thing to bring to that refinement, from D-049 ruling 2:** sessions expire after 30 minutes of
inactivity, and D-050 puts the session in a `SameSite=Strict` cookie. **A site engineer with no signal
for an hour is, by that rule, signed out.** Slice 9 says money never moves offline — it does not say the
engineer must re-authenticate to write a daily log at the bottom of a stairwell. Nobody has been asked.

---

## What this backlog deliberately does not contain

From `spec.md` §1 and `CLAUDE.md`, and not to be added by anyone:

a tax module or ETA e-invoicing · multi-company, multi-branch, multi-currency · a general ledger with
free-form manual journal entries · the consultant role · supplier bidding, RFQ or quote comparison ·
bank guarantee letters · any endpoint that edits or deletes a posting · any stored balance column.

**And one more, added 2026-08-21:** a **global Finance/Audit role**. D-049 ruling 1 anticipates one
*"if added later"* and does not create one. Nobody at Kaff has asked for it, and a role that exists
before anybody needs it is a member of the permission model that means nothing.
