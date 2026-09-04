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

**Sprint 1 closed and recomputed 2026-08-26** at `e9f3dcf`, by the Scrum Master. The build orders
below are spent — every story in them is built except `KAFF-118`. See *Sprint 1 — closed and
recomputed* below for the arithmetic; the slice-1 inventory table at the bottom now carries the same
verdicts and no longer disagrees with an order that has nothing left to sequence.

---

## Where the work sits

| # | Epic | Gate (`agents.md`) | Stories | Points | Blocked |
|---|---|---|---:|---:|---|
| 1 | Foundation | permission tests pass | **28** | **102** | ~~Q42~~ **closed, D-055 §2** · ~~Q43~~ **ANSWERED 2026-09-02, D-100.** **Slice 1 has no open Karim question** |
| 2 | Masters | Excel import works | 13 | 48 | Q12, Q13, Q29 |
| 3 | Treasury | **the worked example reconciles** | 20 | 120 | Q14, Q15, Q16, Q29 |
| 4 | Spine | prices provably frozen | 17 | 84 | ~~N10~~ **approved and built, D-055 §3** · **Q-N10-1, Q-N10-2b, Q-N10-3** (Karim), Q18, Q19, Q20, Q30 |
| 5 | Billing | §15 passes end to end | 15 | 86 | Q21, Q30 |
| 6 | Execution | deltas sum correctly | 13 | 56 | Q22 · **Q52** (does not block) |
| 7 | Accounting | balance sheet balances | 12 | 61 | Q23, Q24, Q25, Q40 |
| 8 | Closure, warranty, portal | portal leaks nothing | 15 | 65 | Q26 · **N7** (Architect, not Karim) |
| 9 | Mobile and offline | offline cannot move money | 8 | 47 | none open yet |
| | | | **141** | **669** | |

> **⚠️ Slice 1's row was re-derived on 2026-09-02 and the figure it replaces was three re-estimates
> stale.** It read **27 / 92**, computed 2026-08-21. Since then KAFF-105b went 3 → 5 and KAFF-115 went
> 3 → 8 at the 2026-09-01 refinement (`decisions.md` D-097 §3), and **KAFF-125 was cut today at 3**.
> Re-derived by summing the slice-1 inventory table at the bottom of this file rather than by adding to
> the old total — D-096 §4 is the record of what adding to a stale total costs here:
> 5+5+3+2+5+5+2+5+5+2+3+5+5+3+3+5+3+8+3+5+3+5+2+3+2+2 = **99** across 26 rows, **plus KAFF-125 at 3 =
> 102 across 27**; 28 rows counting `KAFF-122`, which is `Superseded` and carries no points. The grand
> total moves 659 → 669 and 140 → 141 by the same 10 and the same one row.

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

  ~~**Q43** is the same screen, one field down, and **still open**. It does not make a story `BLOCKED`;
  it stops a field.~~

  > **⚠️ ANSWERED 2026-09-02 — D-100, and the second half of that struck sentence was wrong by the
  > time it mattered.** Q43 stopped a field on 2026-08-21 and **blocked a story** from 2026-09-01, when
  > KAFF-105b was proposed for a sprint and rule 6 and `AC-105b-C` were found asserting the code with a
  > citation that does not grant it (`decisions.md` D-097 §3). **Nabil granted both halves:** the
  > reference code is **mandatory** alongside the project name, rendered as `[RefCode] Project Name`
  > because project names overlap in this industry and the code is the hard identifier that stops HR
  > staffing the wrong site; and the **team size** — the current headcount — is required, because it is
  > what lets HR spot an unstaffed site without drilling into it. **KAFF-105b is Ready.**


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

## Sprint 3 — open 2026-09-04 · the Client master · **10 of 14 points delivered on day one**

**Build order, in dependency order — `KAFF-119` first and alone; the other four all depend on it.**

| # | Story | Pts | State | Model, per `agents.md` §M |
|---|---|---:|---|---|
| 1 | **KAFF-119** | 5 | ✅ **DELIVERED** `86cc8b0` + `01c7b3a`, 2026-09-04. **`AC-119-A`…`K` discharged, each watched failing under a mutation of its own mechanism. `AC-119-L` HELD** — Arabic/RTL at mobile width, and there is no client form; **Frontend's, and it is not a pass.** **Not independently verified** — built and self-reported | **strongest** — it decides what the audit trail records for a duplicate, and it is the first generator of its kind in this database |
| 2 | **KAFF-121** | 3 | ✅ **DELIVERED** 2026-09-04, decisions.md **D-109**. `AC-121-A`…`H` discharged, each watched failing under a mutation of its own mechanism. **`AC-121-I` HELD** — Arabic/RTL at mobile width, and there is no client form; Frontend's. **Not independently verified.** `Client` gained `Rename`, `SetPrimaryPhone` and `SetClassification` (F-09's missing surface); `PhoneMatches` gained `excluding` (D-107 §2) | **strongest** for the missing `Name`/phone/`Kind` setters and rule 6's guard, which decide what a master record may become; **mid** for the rest |
| 3 | **KAFF-124** | 2 | ✅ **DELIVERED** 2026-09-04, decisions.md **D-110**. `GET /api/clients?search=&includeArchived=` — name, code and normalised phone. `AC-124-A`…`G` discharged, four of them watched. **`AC-124-H` half-held** (the empty `200` is pinned; the rendering needs a screen), **`AC-124-I` HELD**. **Not independently verified** | **mid** — criteria are written |
| 4 | KAFF-123 | 2 | `Ready`. **Proposed next** | **mid** |
| 5 | KAFF-120 | 2 | `Ready`, **re-estimate first**. Its remaining work rides on 119's and 121's endpoints, **both of which now exist** | **mid** |

**Gates, run in the 2026-09-04 sessions:** build Release `-warnaserror` **0/0** · `dotnet format`
exit **0** · `Domain.Tests` **124/124** (111 → 124) · `Api.Tests` **278/278** (241 → 255 → 267 → 278) ·
citations **1149 / 0 broken / 0 legacy**. **E2E has not been run since `01c7b3a`** — the last figure,
**6/6**, belongs to the KAFF-119 build session, and no client story has a screen to drive.

> ### ⚠️ Three undischarged criteria belong to no story in the table above
>
> **`AC-119-L`, `AC-121-I` and `AC-124-I` (plus half of `AC-124-H`) are one hole: there is no client
> screen.** Three stories are delivered; all three criteria are Frontend's and none is a pass. **One
> Angular client form serves create and edit, and one list serves search** — build them once, and
> before any of the three is put up for acceptance. Named here so they do not go the way `AC-106-J`
> did.

> ### ⚠️ D-110 §2 corrects a reading of D-108 that would be dangerous to carry forward
>
> D-108 and D-109 recorded that removing `.RequirePermission` reddens most of a client suite, because
> with no gate nothing calls `ActorVerifiedAs` and the audit constraint refuses the row. **That is a
> property of write endpoints only.** On KAFF-124's read the same mutation reddens **two** tests —
> the two that test the gate — and an ungated `GET /api/clients` happily returns every client in Kaff
> to a `Role.Client` caller. **On a read, the permission test is the entire control.**

> ### Found and routed, not fixed: `scripts/check-citations.ps1` checks citations in `.md` only
>
> Its identifier index is built from code, but the citations it *collects* come from markdown alone.
> **80 `[Verified:` markers live in `.cs` and `.ts` files and the gate has never read one** — they are
> also written in a different shape (`<c>…</c> -&gt; <c>…</c>`), so widening the file filter is not
> enough. An ad-hoc pass reached 35 of the 80 and found **0 broken**; the other 45 are unaudited.
> **Needs its own story** — D-110 §5.

> **`KAFF-118`'s cut is withdrawn, not decided** (`meetings/2026-09-04-sprint-3-standup.md` §2). Both
> premises of the routing expired: sprint 1 is closed, so there is **no locked sprint to cut from**,
> and its blocking dependency **KAFF-119 landed today**, so all six of 106, 109, 110, 111, 113, 119
> exist. **It is buildable for the first time and should be scheduled.** Not pulled here — the pull is
> Nabil's.

**`Ready` is not the same as pulled. Scope is Nabil's** — that rule has held since 2026-09-02 and is
not being quietly dropped because the stories finally passed the gate.

---

### ~~Sprint 3 — the Client master, and every story in it is `BLOCKED` today~~ — the opening verdict, kept as the record

> **`meetings/2026-09-04-sprint-3-refinement.md` is the ceremony and the reasoning. This is the
> summary.**
>
> **Why this sprint is the Client master, and not something else.** `agents.md`'s slice sequence
> defines slice 1 as *"Foundation: auth, roles, assignment, audit, **Client master**"*. Sprint 1
> deferred all five client stories and **they were never picked back up**; nine days then went into
> verification and repair loops which found a real defect every run and **advanced no slice.** Nabil
> named it on 2026-09-04 — *"we already had a roadmap, why are we not moving according to the plan?"*
>
> **Two framing corrections carried into this sprint.** **Projects are slice 4, not slice 1** — no
> slice-1 story creates one, and the missing `POST /api/projects` is the plan working as written, not
> a gap. **Nobody adds a project endpoint to slice 1 to make a demo look fuller.** And the Client
> master **also gates slice 4**, because `Project.Create` requires a `ClientId`.
>
> ### Refinement verdict — all five `BLOCKED`, and three of them transitively
>
> | Story | Verdict |
> |---|---|
> | **KAFF-119** | **`BLOCKED`** — Definition of Ready line 9. `AC-119-J`, `AC-119-K` and `AC-119-L` have **zero** test cases; a criterion with no scenario that can fail is `agents.md` §3c's own prohibition |
> | **KAFF-120** | **`BLOCKED`** — DoR lines 9 **and** 11. `AC-120-B` uncovered, **and three bare `:digits` citations that are all wrong today**, inside a story the board called `Ready`. `scripts/check-citations.ps1` is structurally blind to them: its legacy pattern needs a filename before the colon |
> | **KAFF-121 · 123 · 124** | **`BLOCKED` transitively.** Each passes all twelve DoR lines **on its own account** and each declares `Depends on: KAFF-119`. **F-21** is this file's own record of getting that wrong once — *"six `Ready` stories depended on a `BLOCKED` one"* |
>
> **The repairs need no ruling from anybody** — QA writes four test cases, the BA repoints three
> citations. **Hours, not a stalled sprint.** Saying so is not the same as waiving the gate.
>
> ### ✅ Repaired and re-audited the same day — **all five are `Ready`**
>
> **The `BLOCKED` verdict above stood for about four hours.** It is left in place rather than
> rewritten, because what the board claimed and when is the record (SM-29's own practice), and because
> the gate doing its job for one afternoon is the point of having it.
>
> | Repair | Evidence |
> |---|---|
> | **DoR 9** — `AC-119-J` (an enumerate-and-pin allow-list, **not** a blocklist), `AC-119-L` (the create form's RTL case, which the edit form and the list both had and it did not); `AC-119-K` and `AC-120-B` turned out to have a passing scenario already, uncited, so the **citation** was widened rather than a duplicate case written | All four criteria now carry test-case citations; re-counted by the Scrum Master, not taken on report |
> | **DoR 11** — KAFF-120's three bare `:digits` hints replaced with dated identifier citations, **and three more found and fixed in KAFF-121** that nobody had counted | **Zero** bare line-number hints remain across all five stories |
> | **Four blocklist-shaped absence cases rewritten** — `TC-1-156`, `TC-1-157`, `TC-1-173`, `TC-1-190` as allow-lists; `TC-1-183` to enumerate the routes the host actually mapped rather than grep source for the word "delete" | `V-32-A`'s shape, removed from the test cases before it could be implemented |
> | **N6 and the API contract** — **ANSWERED, D-107** | Below |
>
> **One correction the repair returned, and it matters for the test that was just written:** the
> allowed property set for `Client` is **fourteen** members, not the twelve the Scrum Master supplied —
> the brief omitted `Id` (inherited from `Entity`) and `Phone` (a computed property). **A whitelist
> that lists the wrong set fails on the first honest run**, which is exactly what a whitelist is for.
>
> **Flagged, out of scope, and not fixed:** `KAFF-122` carries the identical three broken hints and
> `KAFF-107` two more. Neither is in this sprint's five; both are owed.
>
> ### D-107 — the Architect's three rulings, and the build is unblocked
>
> * **N6:** a **PostgreSQL sequence declared on the EF model** (not in migration SQL — the test harness
>   builds its schema from the model, so a hand-written sequence would exist in production and not in
>   the suite). `nextval` is drawn **last**, after every validation. **The cost is gaps**, and that is
>   the whole cost.
> * **The duplicate warning:** a side-effect-free `POST /api/clients/phone-check` (required, not
>   optional — S-013 says the check fires **on blur**), plus an `acknowledgedDuplicatePhone` boolean on
>   create and edit, re-matched server-side, with an unacknowledged match answering **`409`**. The
>   **warning is never a `Problem`** — `toProblem` discards every field but three, so a `Problem` could
>   not name the matched client.
> * **`AC-119-E`:** **neither free text nor a new column** — `AuditEventKind.DuplicatePhoneAcknowledged`
>   through the mechanism **D-061 already built**, whose entity id *is* the matched client's. **The
>   unbackfillable part is already in the ground**, so KAFF-116's `GrantPath` argument does not apply.
> * **`AC-119-B`'s "ignored or refused" is settled structurally** — the create request carries no
>   `Code` member, so no path could store one. **The BA owes the criterion a one-line rewrite.**
>
> **One new question for Karim:** may the client-code sequence contain **gaps**? A PostgreSQL sequence
> is non-transactional, so a rolled-back insert burns a number and `C-10002` never exists — and a code
> is a reference that appears on extracts and ledgers.
>
> **`KAFF-122` stays `Superseded` and is not re-created in slice 1.** **`KAFF-120` is *not* stale in
> the same way** — that was checked, and `spec.md` §6.7's amendment says *"individual clients do not
> withhold"* is unchanged. But six domain tests already discharge `AC-120-C/D/E/G`
> [Verified: 2026-09-04 @ `tests/Domain.Tests/WithholdingTests.cs` -> `A_contract_for_an_individual_client_cannot_withhold`],
> so **its remaining work rides on KAFF-119's and KAFF-121's endpoints and its 2 points are probably
> now wrong.** Not re-estimated here: estimates move at refinement with the team.

---

## Sprint 2 — **CLOSED 2026-09-04** · ~~open · scope not yet locked~~

> ### ⚠️ This heading read *"open · scope **not yet locked**"* until 2026-09-04, and recorded **none** of the thirteen commits since `4bf81ce`.
>
> **Corrected loudly rather than rewritten, per SM-29's own practice** — and named as what it is: the
> same staleness this project keeps catching in every other artefact, sitting in the board itself.
>
> **Sprint 2 closed with `qa/slice-1/verification-2026-09-04.md`**, which verified four commits and
> **accepted all four**. What landed since the board was last current:
>
> | Commit | What | Verdict |
> |---|---|---|
> | `e56cd16` | **KAFF-105b** — `/api/auth/me` returns each caller's projects | **ACCEPTED**, with `V-32-A` (HIGH) and `V-32-B` (MEDIUM) routed |
> | `7461332` | **KAFF-125** — the staff shell | **ACCEPTED as an implementation.** `AC-125-B` verified by code only (`V-32-D`); **`AC-125-C` explicitly NOT accepted as satisfied** — Nabil's criterion, Nabil's call |
> | `ad92638` | E2E suite repaired, orphaned status page deleted | **ACCEPTED**, with `V-32-E` (LOW) |
> | `440e4bd` | A repeatable demo seed, through real endpoints only | **ACCEPTED** — and it found the API's client and project gap |
> | `1c499d4` | **`V-32-A` fixed** — the staff payload guarantee is a whitelist, watched red first (**D-106**) | Not yet independently verified |
> | `c153bd7` | Housekeeping — ten SM-33 citations, twenty orphaned i18n entries, one stale UX paragraph | Not yet independently verified |
> | eight more | Verifier increments, 2026-09-03 and 2026-09-04 | — |
>
> **The demo's ceiling, and it is a scope fact rather than a defect:** `kaff_demo` holds **0 projects,
> 0 clients, 0 assignments, 0 postings**. Everything sprint 2 built renders as two empty-state
> sentences, because there is no project for any of it to describe. **The blocker is two missing
> endpoints, not presentation** — and the client one is this sprint.

### The 2026-09-01 refinement, kept as the record

> ### ⚠️ Refined 2026-09-01 — **neither proposed story is Ready.** `meetings/2026-09-01-sprint-2-refinement.md`
>
> The refinement ceremony was run and **it changes the proposal below rather than confirming it.**
> Read the meeting file; this is the summary.
>
> | | Verdict |
> |---|---|
> | **KAFF-105b** | **`BLOCKED`** on six Definition of Ready lines. Five are BA/QA repairs needing no ruling; **the sixth is Karim's** — rule 6 and `AC-105b-C` assert HR sees a project's **code**, citing D-051 (Q32), which grants *"the project name and the list of assigned engineers"* and never mentions a code. That is **`Q43`, open**. **Re-estimated 3 → 5** |
> | **KAFF-115** | **`BLOCKED`** — transitively on KAFF-105b, and on its own account. **Re-estimated 3 → 8** (permission model 5, spans backend and frontend 8; take the higher). Frontend returned 8 independently |
> | **The staff shell** | **Still not a story, and the hole is bigger than the three costed readings.** `ux/navigation.md` -> `Landing summary` names a landing for every role; **no story builds S-004, S-005, S-009a or S-011**, and the whole API exposes three GET routes, so three of the four landings have no data to render. **`AC-101b-D` fails the same arithmetic as `AC-101b-A`** — HR lands on S-009a (the project *list*) and KAFF-115 builds S-009b (one project's team) |
> | **Item 0 — the Verifier pass** | **DONE.** Six commits ACCEPT, five acceptances re-established |
> | **Item 4 — `AC-106-H`, `AC-110-D`** | **Both DISCHARGED.** KAFF-106 and KAFF-110 remain **not accepted** as stories |
> | **Staging** | **The CI smoke check passes at HEAD.** *"Runs on staging"* is now tickable for every API-surface story and **still not for the two screens** — the check curls `/api/health` and never fetches the SPA |
>
> **Proposed instead: a repair-and-unblock sprint, no new feature surface, no story points** — meeting
> §5. **Scope is Nabil's and nothing is locked.**

> ### ✅ Superseded 2026-09-02 — **Nabil ruled on three of the seven questions.** `meetings/2026-09-02-sprint-2-locked.md`, `decisions.md` D-100
>
> **The table above was true when written and three of its rows are now false.** Corrected here loudly
> rather than rewritten, per SM-29's own practice.
>
> | | Then | Now |
> |---|---|---|
> | **KAFF-105b** | `BLOCKED` on `Q43` | **Ready, 5.** `Q43` **ANSWERED** — the reference code is mandatory beside the name (`[RefCode] Project Name`) and the team size is required |
> | **KAFF-115** | `BLOCKED`, three DoR failures unrepaired | **Ready, 8.** All three repaired 2026-09-02, including `AC-115-G`, which had been passing for the wrong reason |
> | **The staff shell** | *"Still not a story"* | **`KAFF-125`, cut 2026-09-02 at 3 points**, carrying `AC-101b-A` and `AC-101b-D`. **Cut is not committed** — whether it is built in sprint 2 is the one scope question this run put back to Nabil |
>
> **Sprint 2 remains a repair sprint by Nabil's own ruling** — *"an answer to Q43 does not change its
> shape. If we build new features on a porous foundation, the Zero-Trust posture collapses. Pay the
> technical debt first."* Ready is not the same as pulled: KAFF-105b and KAFF-115 are Ready and are
> **not** in sprint 2.

### The 2026-08-30 opening proposal, kept as the record

**Full reasoning: `meetings/2026-08-30-sprint-2-open.md`.** This is the summary; that file is the
record, and it carries what was *not* done as well as what was.

**Sprint 2 had been executing for two days with no recorded scope.** Six product commits landed
against a board describing the tree of three days earlier. What follows is the sprint as it actually
stands, plus a **proposal** for the rest. **Nabil locks scope. Nothing below is a commitment.**

### Already delivered, before the sprint was opened

| Commit | What | Entry | Independently verified |
|---|---|---|---|
| `f2b995b` | **KAFF-101b** — the staff sign-in screen. The first thing that ever rendered here | D-091 | **No** |
| `332c160` | **KAFF-103's screen**, and `AC-101b-F` closed with it | D-092 | **No** |
| `4885edf` | **`V-27-A`** — the guard list a regression cannot edit into agreement | D-093 | **No** |
| `c01959b` | **`V-27-B`** — *"the exemption marker is now unforgeable"* | D-094 | **No** |
| `ca4db6c` | **`V-27-C`** — a role that is not a role, two predicates that failed open | D-095 | **No** |
| `45a939d` | A malformed body is **400 in every environment**, not 500 in one | **none exists** | **No** |

The four tech-debt tickets were pulled forward by Nabil from *"before slice 3"* on the grounds that
**"we do not wait to fix foundational security flaws."** They are not stories and carry no estimate.
**KAFF-101b is a 3-point story and its `AC-101b-A` and `AC-101b-D` are not closed.**

### The proposal

| # | Item | Pts | Why here |
|---|---|---:|---|
| 0 | **Verifier pass over the six commits** | — | **Running now.** Not a story; a gate on everything else |
| 1 | **KAFF-105b**, API half as written | **3** | Defines the payload everything downstream reads |
| 2 | **The staff shell** — *story unwritten* | **?** | Blocked on Nabil's reading below, and on the BA writing it |
| 3 | **KAFF-115** | **3**, likely light | Depends on 1. `AC-101b-D` lands with it |
| 4 | **`AC-106-H`, `AC-110-D`** | — | Deferrals now spent, never examined by any pass. Folded into item 0 |

**Recommendation: lock 0, 1 and 3; hold the shell.** 105b + 115 as written is 6 points and that is not
the real number — with the shell and two screens it is realistically 13–16, which `process/agile.md`
calls *"too big — split it."* The shell is the one piece whose size nobody can state today.

### ⚠️ KAFF-105b cannot discharge the criterion deferred onto it

**All ten of KAFF-105b's criteria — `AC-105b-A` … `AC-105b-J` — are criteria about the
`GET /api/auth/me` payload. Not one of them renders anything.** `AC-101b-A` requires *"they arrive at
the staff shell, and the shell's contents come from `GET /api/auth/me`"*; KAFF-105b builds the second
half of that sentence and none of the first. D-091's deferral was honestly reasoned and the arithmetic
does not close.

Three readings, **none picked here** — grow KAFF-105b to 8 (`process/agile.md` puts a story spanning
backend and frontend there); write a shell story; or re-defer `AC-101b-A` to whichever story genuinely
builds the shell. **This is a scope decision and it is Nabil's.** It decides what sprint 2 is.

The same shape, smaller: `AC-115-J` is *"Arabic, RTL, at mobile width"*, so **KAFF-115 already spans
both halves at 3 points.** Not re-estimated here; estimates move at refinement.

### Sprint 1, final

| Bucket | Stories | Pts |
|---|---|---:|
| **Accepted, standing** | 116, 108, 113, 100, 111, 112, 114 | **25** |
| **Accepted 2026-08-27 at `559ac45`, then the code moved underneath the verdict** | 109, 105a, 102, 101a, 103 | **19** |
| **Built and verified with a criterion still held — never accepted** | 106, 110 | **10** |
| **Unbuilt** | 118 | **3** |
| | **15** | **57** |

**Nabil's lock stands: 15 stories / 57 points.** Nothing added, cut or re-estimated.

**25 of 57 stand — exactly the figure the sprint-1 close recorded three days and six commits ago.**
That is the finding, and it is worse than a drop would be. The 2026-08-27 Verifier pass recovered all
19 of the disputed points; `c01959b` and `ca4db6c` lapsed all 19 again two days later. **The number
did not move because verification and the fixes verification prompted cancelled each other out.**

Arithmetic, since it has been got wrong before: 3+3+5+5+3+3+3 = 25 · 5+2+2+5+5 = 19 · 5+5 = 10 · 3.
**25 + 19 + 10 + 3 = 57** across **7 + 5 + 2 + 1 = 15** stories.

**Why the 19 lapsed.** `ca4db6c` turned `StaffSessionRules.MayHoldStaffSession` — KAFF-101a's **own**
role bar, and called inside `LiveSession.ResolveAsync`, which 102, 103 and 105a all route through —
from a deny-list into an allow-list; it added an `Enum.IsDefined` refusal to `User.ValidateDepartment`,
which is KAFF-109's own path; and `c01959b` rewrote how `RequireLiveSession` produces its metadata.
`meetings/2026-08-27-sprint-1-retrospective.md` §3 change 3: *"When a later commit touches that
story's files, the acceptance lapses and must say so out loud."* **First time it has fired rather than
been argued about. KAFF-101a and KAFF-103 have now had it happen twice.**

**The shared-mechanism note, which qualifies every row above.** `ca4db6c` also changed
`PermissionEvaluator.Evaluate` — the gate *every* permission-checked endpoint runs through — and
`45a939d` changed the request pipeline for every JSON-binding endpoint. **A story lapses where a
commit changed behaviour its own criteria assert; it is carried with the exposure named where a shared
mechanism changed but its behaviour for that story is *pinned*, not asserted.** Here it is pinned
[Verified: 2026-08-30 @ `tests/Domain.Tests/UserTests.cs` -> `The_two_role_doors_admit_exactly_these`].
Carried on that basis: KAFF-106, KAFF-108, and every permission-gated story.

**Gates at `601ac04`**, re-run this session through `/run-kaff-erp`: build **0 warnings / 0 errors**
(`-c Release --no-incremental`), `dotnet format --verify-no-changes` exit **0**, Domain **107/107**,
Api **227/227**, `scripts/check-citations.ps1` **942 checked, 0 broken, 0 legacy**. **Green is not
accepted** — that is what `V-27-B` demonstrated at 215/215 against a route applying no checks at all.

### What sprint 2 must not start until

* **The Verifier reports.** Six commits, no independent pass on any. `qa/slice-1/verification-2026-08-30.md`.
* **Nabil rules on `AC-101b-A`'s reading**, or item 2 stays unwritten.
* **`KAFF-118`'s cut is still Nabil's** and he has not ruled. *"Move the board"* is not a ruling on
  scope, the same way *"close sprint 1"* was not.

---

## Sprint 1 — the committed scope and the build order

**Locked by Nabil at 15 stories / 57 points.** Recorded here 2026-08-22 because until today **the only
record of what was committed lived in `meetings/`** — a durability gap of exactly the kind this project
keeps hitting. The backlog lists what exists; this section says what was *agreed*.

**Deferred — 10 stories, 33 points:** KAFF-101b, 104, 105b, 115, 117, 119, 120, 121, 123, 124.
**KAFF-107 folded** into KAFF-106 and KAFF-108 (16/59 → 15/57). **KAFF-122 superseded** by KAFF-416.

### ~~Sprint 1 — closed and recomputed, 2026-08-26 at `e9f3dcf`~~ — **SUPERSEDED 2026-08-30**

> **⚠️ The five-bucket table below is the state at `e9f3dcf` and is no longer current. Read
> *"Sprint 1, final"* in the Sprint 2 section above instead.** The 2026-08-27 Verifier pass accepted
> all five of the *"rejected"* and *"moved-underneath"* stories at `559ac45`, collapsing the middle
> two buckets into one; `c01959b` and `ca4db6c` then lapsed all five again
> (`meetings/2026-08-30-sprint-2-open.md` §2.2).
>
> **Its arithmetic was right and stays right — 25 of 57 — and the current figure is the same 25.**
> That is not the table surviving; it is two opposite movements of 19 points cancelling.
>
> Left in place rather than edited, per SM-29's own practice: what a document claimed and when is the
> record. The correction is loud, not silent.

**Supersedes every build order below.** Every story in the 2026-08-25 order is built except
`KAFF-118`, so the order is spent; it is left in place because the reasoning that produced it is the
record of how the 111-vs-114 conflict was resolved, not because anything is still waiting in it.

**Nabil's lock stands: 15 stories / 57 points.** Nothing was added, cut or re-estimated. The
arithmetic below re-derives the lock rather than restating it: 26 slice-1 stories at 92 points
(`KAFF-122` superseded), minus the 10 deferred (33 points), minus `KAFF-107` folded into 106 and 108
(2 points) = **15 / 57**.

#### The five buckets, and the second and third are the honest part

| Bucket | Stories | Pts |
|---|---|---:|
| **Accepted** — verified by a session that did not write the code, no defect open against it, and its own behaviour is unchanged since that verification | KAFF-116, 108, 113, 100, 111, 112, 114 | **25** |
| **Verified, then the code moved underneath the verdict** — accepted at `e43e9ac`, changed at `f807364` / `4f9fc62` | KAFF-101a, 103 | **10** |
| **Rejected, fixed, not re-verified** | KAFF-109, 105a, 102 | **9** |
| **Built and verified with a criterion still held** | KAFF-106, 110 | **10** |
| **Unbuilt** | KAFF-118 | **3** |
| | **15** | **57** |

**25 of 57 points stand. 19 do not, and they are the sprint's whole finding.**

**Five stories' shipped code changed after the session that judged it, and no independent session has
looked at any of them since** — checked against `git show --stat`, not against a report's summary of
itself:

| Story | What changed after `2e56943` | Commit |
|---|---|---|
| KAFF-109 | `User.ChangeRole` gained the guard that closes `V-26-A` | `7ff500e` (D-088) |
| KAFF-105a | `WhoAmI/Endpoint.cs` + `Handler.cs` — the hand-copied checks replaced by `RequireLiveSession()` | `f807364` (D-089) |
| KAFF-102 | `SignOut/Handler.cs` — `LiveSession.ResolveAsync` before the audit row | `f807364` (D-089) |
| **KAFF-101a** | `SignIn/Handler.cs` and `StaffSessionMinter.cs` — the role bar became the shared `StaffSessionRules.MayHoldStaffSession` | `f807364` (D-089) |
| **KAFF-103** | `ChangePassword/Endpoint.cs` + `Handler.cs` rewritten onto `LiveSession`; the `V-26-F` ordering pinned | `f807364`, `4f9fc62` |

**The last two were *accepted*, and their acceptance is dated to a commit that is no longer HEAD.**
That is SM-29 applied to an acceptance rather than to a story: a verdict is a claim about a tree, and
this one aged in eleven hours. The fixes are good and the suite is green at HEAD (Domain **97/97**,
Api **215/215**, build **0/0**, `dotnet format` clean — all re-run 2026-08-26 on this tree). **A green
suite is not an independent verification**, and three of these five stories were green when they were
rejected.

**None of the 57 points has passed Nabil's acceptance gate.** `process/agile.md` §4 makes acceptance
Nabil running the demo script; no demo script has been run and there is no screen to run one against.
`ACCEPTED` in the tables below means *an independent session verified it and found no open defect* —
the meaning this file has used since KAFF-116. It does not mean Nabil has accepted it.

#### What sprint 2 must not start until

* **A Verifier pass over the five stories above**, in a fresh session, against HEAD. Not a re-read of
  `qa/slice-1/verification-2026-08-26.md` — that report judged a different tree.
* **`AC-106-H` and `AC-110-D` are now dischargeable.** Both were deferred to stories that did not
  exist; `KAFF-101a` and `KAFF-103` exist now (D-084, D-086). The deferrals were honest and are now
  spent — they belong in the same Verifier pass.
* **`KAFF-118` is unbuilt and its cut is Nabil's.** It depends on `KAFF-119`, deliberately deferred out
  of this sprint, so it cannot complete as written whatever he rules. The proposal to keep it as an
  acceptance check rather than a story still stands and is still not the Scrum Master's to take.

---

### Build order — re-derived 2026-08-25, and the 111-vs-114 conflict resolved

**Supersedes both orders below.** Two records disagreed and the disagreement is recorded rather than
quietly picked: the **table below** listed `114` at position 6 and `111` at position 7, while a
previous Scrum Master run published **`111 → 112 → 114 → 109 → 100 → 101a → 102 → 103 → 105a`**.

**Resolved against the dependency facts in this file, and then dissolved by one of them.**

* On the facts as the table states them, **114 wins**: `KAFF-114` depends on `113` alone; `KAFF-111`
  depends on `113` **and** `110`. Fewer dependencies, and the one it has was further along. The prose
  under the old table agrees — *"114, 111, 112 and 109 behind those"*.
* **But the conflict is moot, because `KAFF-111` is already built.** It has no endpoint or handler
  folder of its own and must not be given one: the revocation runs inside `KAFF-110`'s handler, as one
  request, one correlation id, one `SaveChangesAsync`
  [Verified: 2026-08-25 @ `src/Api/Features/Users/DeactivateUser/Handler.cs` -> `HandleAsync`], and
  `decisions.md` **D-074 §2** records why. A reader looking for "the KAFF-111 endpoint" will not find
  one.

**So the published order was not merely mis-sequenced — its first item was already done.** Both orders
were derived before D-074 was written, which is SM-29's subject exactly.

**`KAFF-112` moves up as a consequence.** It depended on `110` and `111`; both are built, so it is
startable now rather than third.

#### What is actually built, checked today

| Story | Pts | State |
|---|---:|---|
| KAFF-116 | 3 | **ACCEPTED** 2026-08-24 — D-070. *(An earlier draft of this row said 2026-08-23; D-070 is the accepting entry and it is dated the 24th.)* |
| KAFF-108 | 3 | **ACCEPTED** 2026-08-25 — 7 of 7, `qa/slice-1/verification-2026-08-25.md` §8 |
| KAFF-113 | 5 | **ACCEPTED** 2026-08-25 — 9 of 9, same report |
| KAFF-106 | 5 | **BUILT**, verified — 9 of 11, 2 deferred. **HOLD on `AC-106-J`**: Arabic, RTL, mobile width, and there is no screen. Deferred to **Frontend**, explicitly, and it is not a pass |
| KAFF-110 | 5 | **BUILT**, verified — 8 of 10, `AC-110-E` deferred to KAFF-104, and **`AC-110-D` deferred to KAFF-101a by Scrum Master ruling, 2026-08-25**, which clears the Verifier's hold (**W-9**) |
| KAFF-111 | 3 | **BUILT** inside KAFF-110's handler — D-074 §2. Not separately verified |

**24 of 57 points are built. 33 remain.** The brief that opened this run said *"21 points accepted"*
and listed KAFF-111 as unbuilt; both figures are corrected here rather than worked around.

#### The order for the remainder

| # | Story | Pts | Depends on | Status |
|---|---|---:|---|---|
| 0 | **The two audit prerequisites** — the **IP column** (D-063 §2, N-19) and the **nullable subject** (D-063 §3) | — | — | Not a story. Decided in full, built in none, and `KAFF-101a` cannot **ship** without them: a column never written cannot be backfilled into a table that is append-only by trigger |
| 0b | **`X-Forwarded-For` — routed to the **Architect**, due before KAFF-101a** | — | — | **Open.** D-063 §2 refused the header for two reasons. The first is permanent and unaffected: a caller-supplied string written into a table nobody can correct is attacker-controlled forensic data. **The second has expired.** It read *"reading it becomes legitimate only once `ForwardedHeadersOptions` is configured with an explicit `KnownProxies` / `KnownNetworks` allowlist, which is a deployment fact this project does not have — D-023, the staging target, is still open."* **D-023 is answered — D-076 — and staging runs nginx in front of the API** [Verified: 2026-08-25 @ `deploy/docker-compose.staging.yml` -> `KAFF_API_URL` -> `nginx.conf.template`]. So on the only deployed environment `HttpContext.Connection.RemoteIpAddress` is nginx's address on every row, and the allowlist the ruling asked for is now derivable from a compose file we own. **Same N-19 deadline as the column: rows written before it are wrong permanently.** Not the Scrum Master's to decide and not Karim's — it decides what the ledger records, so it is not a downgradable task either (`agents.md` §M) |
| 1 | KAFF-114 | 3 | 113 ✅ | Clean |
| 2 | KAFF-112 | 3 | 110 ✅, 111 ✅ | Clean — **moved up**, both dependencies are built |
| 3 | KAFF-109 | 5 | 106 ✅, 113 ✅, 111 ✅ | Clean |
| 4 | KAFF-100 | 5 | — | Clean. Unblocked by D-061 |
| 5 | KAFF-101a | 5 | 100, **item 0** | `Ready to start`; shippable only after item 0 |
| 6 | KAFF-102 | 2 | 101a | Unblocked by D-061 — sign-out is an `AuditEventKind.SignedOut` event |
| 7 | KAFF-103 | 5 | 100, 101a, 106 | Clean |
| 8 | KAFF-105a | 2 | 101a | `Ready` — D-072 §2 |
| — | **KAFF-118** | 3 | 106 ✅, 109, 110 ✅, 111 ✅, 113 ✅, **119** | ⚠️ **Depends on KAFF-119, which is deferred out of sprint 1.** See below |

#### KAFF-118 — the dependency that leaves the sprint

**`KAFF-118` names `KAFF-119` as a dependency and `KAFF-119` is in the deferred list at the top of this
section.** A story inside the sprint cannot be completed as written when one of its dependencies was
deliberately taken out of it.

A previous run proposed **cutting KAFF-118 as a story and keeping it as an acceptance check**. That
proposal stands and **the reasoning is sound** — 118's rule 2 (*no handler constructs an audit record*)
is a cross-cutting property of the mechanism, not a feature, and it is already asserted by the
interceptor's own tests.

**But the sprint scope is locked by Nabil at 15 stories / 57 points, and cutting 3 points from a locked
sprint is his call, not the Scrum Master's.** Routed to Nabil. Until he rules, `KAFF-118` sits last and
its client-registration half is not buildable in this sprint regardless.

---

### Build order — re-derived 2026-08-22, after D-061

**Supersedes the 19:22 order.** That one was derived before the Architect closed V-01, and two
stories have since gone `BLOCKED` on open questions.

**What changed:** **V-01 is closed** (D-061 — the audit mechanism records events, not only entity
changes; `AttributeTo` solves the bootstrap actor). The V-01 gate that held 100, 102 and 118 is
**gone**. Two new blocks replace it, and both are questions rather than defects.

| # | Story | State |
|---|---|---|
| 1 | ~~**KAFF-116**~~ ✅ **ACCEPTED 2026-08-23** — Verifier recommended, D-068 concurs. `AuditRecord.GrantPath` (nullable `ProjectAccessPath`), migration `20260822210402_AuditGrantPath`, 5 new Api tests covering all four grant paths. Clean, **zero dependencies**, and unbackfillable by nature — it lands the grant-path column on the same table D-061 just extended. **Start here** |
| 2 | ~~**KAFF-106**~~ **BUILT — V-A CLEARED 2026-08-25, awaiting an independent Verifier pass.** D-071 gave every refusal a `messageKey`, and `AC-106-B`'s logging half exists. `AC-106-J` (Arabic, RTL, mobile) is **explicitly deferred — there is no UI** |
| 3 | ~~**KAFF-108**~~ **BUILT — awaiting verification, with D-073 open against it** (the trail records the token's role, not the database's). 7 criteria, 11 tests. **Shipped with no permission gate at all — D-067, a privilege-escalation primitive, fixed.** No `Response.cs`/`Validator.cs` and that is correct: 204 has no body, and the request's only rule is the domain's `ValidateDepartment` |
| 4 | ~~**KAFF-113**~~ **BUILT 2026-08-25 — awaiting verification.** All nine criteria cited by tests |
| 5 | ~~**KAFF-110**~~ **BUILT 2026-08-25 — awaiting verification.** Eight of ten criteria covered; `AC-110-D` and `AC-110-E` correctly deferred to unbuilt KAFF-101a and KAFF-104 |
| 6 | **KAFF-114** | Clean (needs 113) |
| 7 | **KAFF-111** | Clean (needs 110, 113) |
| 8 | KAFF-112 | needs 110, 111 |
| 9 | **KAFF-109** | needs 106, 113, 111. **Was fourth in the pre-review order, ahead of its own dependencies — V-06, and it was my error** |
| 10 | **KAFF-100** | **Unblocked by D-061.** `IAuditContext.AttributeTo` puts the new Owner on the `Created` record, and an authenticated request naming another actor throws |
| 11 | **KAFF-102** | **Unblocked by D-061** — sign-out is an `AuditEventKind.SignedOut` event |
| 12 | KAFF-103 | needs 100, 106 |
| 13 | **KAFF-118** | **Unblocked by D-061.** Rule 2 holds in full: no handler constructs a record |
| — | ~~**BLOCKED**~~ ~~**`Ready`, one clause held**~~ **`Ready to start` — every question answered, not yet buildable end to end** | **KAFF-101a** — ~~V-02 / N9~~ answered (D-062 §2); ~~rule 16~~ **rewritten by the BA, 2026-08-23**; ~~401-vs-403~~ **decided: 401** (D-063 §1); ~~the audit criterion~~ **written as `AC-101a-O`**; ~~`AC-101a-G`'s refusal shape~~ **RULED 2026-08-23 — the generic `401` / `errors.auth.invalid_credentials`, D-065 case 5, which closes D-063 A-02**. ~~**(a) 🟡 the locked account — Q47 case 3, OPEN with Nabil**~~ **RULED 2026-08-24, D-072 §1 — `423` only when the submitted password is *correct*, the generic `401` otherwise. Q47 is closed in full and no question remains on this story.** ⚠️ **The ruling's ordering constraint is now `AC-101a-P` and rule 14a: the password is verified BEFORE the lockout decides the response, so a locked account still runs the full 600,000-iteration comparison** [Verified: 2026-08-24 @ `PasswordHasher.cs` -> `Iterations`]. **Checking the lockout first passes every status-code test and re-opens the enumeration oracle as a timing signal** — it is the defect, not the optimisation. **Two dependencies remain and both are Backend builds, not answers: (b) the IP column** (D-063 §2 — N-19, must land before this story **ships**, because a column never written cannot be backfilled into an append-only table) and **(c) the nullable subject** (D-063 §3). **Neither is built** [Verified: 2026-08-24 @ `AuditRecord.cs` -> `class AuditRecord`], and **`AC-101a-O` is the only criterion behind them** — the other fifteen are startable today. **That is why the status is `Ready to start` and not `Ready`:** `Ready` means buildable end to end, and this is not, yet |
| — | ~~**BLOCKED**~~ **`Ready`** | **KAFF-105a** — ~~V-03, rule 3 vs `AC-105a-C`~~ **RULED 2026-08-24, D-072 §2: the flag travels in the payload.** The API authenticates, issues the session token and returns `mustChangePassword: true`; **`AC-105a-C` is the side that changed**, from refusal to field. **N-04 / Q-UX-18 / SM-16 are closed** — by Nabil as decision owner, not by the Architect. The BA refused to invent the tie-break and was right to. **It was a four-story reconciliation, not three:** `KAFF-105a` rule 3 (field, unchanged), `AC-105a-C` (refusal → field), `KAFF-103` `AC-103-B` (`/api/auth/me` carved out of the refused set), `KAFF-100` `AC-100-F` (field, unchanged) — **and `KAFF-101a` `AC-101a-F`, which V-03's own table missed and which would otherwise have gone on commanding the refusal in the very story that mints the token.** **Every criterion here is buildable.** Its only remaining dependency is **KAFF-101a**, which is `Ready to start` but not shippable until (b) and (c) above land — that gates the **sequence**, not this story's readiness. 🟡 **One question is handed back to Nabil and it blocks nothing:** the token D-072 §2 issues is a **full** token, and whether any endpoint beyond the password-change one and `/api/auth/me` should refuse it **is a rule nobody has stated.** Three artefacts assert the strict reading — `KAFF-101a` rule 8, `AC-101a-F`, `AC-103-B` — and all three cite D-049 ruling 4, **which names no endpoint** |
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
| KAFF-100 | Bootstrap the first Owner through a one-time setup screen | **5** | ACCEPTED 2026-08-26 — all 10 QA cases covered, `qa/slice-1/verification-2026-08-26.md` §6. | — |
| KAFF-101a | Sign in, and the server sets an `HttpOnly` session cookie | 5 | **ACCEPTED 2026-08-26, then the code moved underneath the verdict.** | 100 |
| KAFF-101b | The staff sign-in screen, and where each role lands after it | 3 | Ready | 101a, 105a |
| KAFF-102 | Sign out | **2** | **REJECTED 2026-08-26** on `V-26-C` — a cookie the global kill had already ended still wrote a permanent audit row. | 101a |
| KAFF-103 | Change the temporary password on first sign-in | 5 | **ACCEPTED 2026-08-26, then the code moved underneath the verdict.** | 100, 101a, 106 |
| KAFF-104 | Reset a forgotten password with an Owner-generated link | 5 | Ready | 101a, 103, 106 |
| KAFF-105a | `GET /api/auth/me` returns who I am and what I may do | **2** | **REJECTED 2026-08-26** on `V-26-B` — `GET /api/auth/me` answered `Role.Subcontractor` and `Role.Client` with a `200` and their name, against spec.md §9 *"record only, no login"*. | 101a |

> **⚠️ This table records story *state*, and it contradicted the build order above it until 2026-08-23.**
> `KAFF-105a` read `Ready` here while the order three rows up read `BLOCKED`. **A Backend agent reading
> only this table would have started a blocked story.** Found by the BA against a file it does not own
> and fixed by the Scrum Master, who does. **When these two disagree the build order is authoritative**
> — it is recomputed every time a blocker moves; this table is a backlog inventory.
| KAFF-105b | `GET /api/auth/me` returns the projects I reach, and how | **5** | **BUILT `e56cd16` and ACCEPTED 2026-09-04** by a session that did not write it, with `V-32-A` (HIGH, **fixed** — D-106) and `V-32-B` (MEDIUM) routed. ~~**READY 2026-09-02.**~~ `Q43` — its sole remaining Definition of Ready failure — is **ANSWERED** (D-100): HR's entries carry the project name, its reference code and its team size. The other five failures were repaired 2026-09-01. It still discharges **neither** `AC-101b-A` nor `AC-101b-D`; both moved to **KAFF-125** on 2026-09-02. | 105a, 113, 114 |
| KAFF-106 | The Owner creates a user with a role and a department | 5 | BUILT, verified 2026-08-25 — 9 of 11 criteria satisfied. | 100 |
| KAFF-107 | An HR user cannot be created or moved outside the HR department | 2 | Ready | 106 |
| KAFF-108 | Move a user between departments | 3 | **ACCEPTED** 2026-08-25 — 7 of 7, `qa/slice-1/verification-2026-08-25.md` §8. *(This row read `Ready` until 2026-09-01 — an unbuilt state — while the same file's build order and "Sprint 1, final" table both had it accepted. Found by Backend at refinement; corrected by the Scrum Master, who owns the board.)* | 106 |
| KAFF-109 | Change a user's role — **rewritten, D-051 reverses D-049 ruling 6** | 5 | **REJECTED 2026-08-26** on `V-26-A` (a reachable `500` with no `messageKey`) and `V-26-B`. | 106, 113, 111 |
| KAFF-110 | Deactivate a user, and their access ends on the next request | 5 | BUILT, verified 2026-08-25 — 8 of 10 satisfied. | 106 |
| KAFF-111 | Deactivating a user revokes their project assignments | 3 | ACCEPTED 2026-08-26 — verified on its own criteria for the first time and both QA cases pass. | 110, 113 |
| KAFF-112 | Reactivate a user, who comes back with nothing | 3 | ACCEPTED 2026-08-26 — 5 of 6 QA cases covered; **`TC-1-094` has no test** (the username stays reserved while the account is off — an index predicate a later migration removes without noticing). | 110, 111 |
| KAFF-113 | Assign a user to a project, with seniority for site engineers | 5 | **ACCEPTED** 2026-08-25 — 9 of 9, same report. *(This row read `Ready` until 2026-09-01. **It is on KAFF-105b's dependency path**, so a reader taking it at face value would have judged KAFF-105b unstartable for the wrong reason. Found by Backend at refinement; corrected by the Scrum Master.)* | 106 |
| KAFF-114 | Revoke a project assignment without losing who could act when | 3 | ACCEPTED 2026-08-26 — 7 of 8 QA cases covered; **`TC-1-120` has no test** (revoking the last person on a project is allowed — the case exists to pin an absence, so nothing goes red the day somebody adds the rule). | 113 |
| KAFF-115 | The project team panel, and HR's separate Project Team screen | **8** | **READY 2026-09-02.** Its transitive block cleared with KAFF-105b, and its own three Definition of Ready failures were repaired the same day — `AC-115-H`'s unbuildable budget/balance given and its assertion against a dashboard endpoint that does not exist, `AC-115-I`'s unfailable *"when the code is read"* shape, and **`AC-115-G`, which passed for the wrong reason** (refused at the staff door by `MayHoldStaffSession`, not because *"`PortalRead` is not `ProjectRead`"*). Re-estimated 3 → 8 on 2026-09-01. | 113, 114, 105b |
| KAFF-116 | Every audit record says how the actor reached the project | 3 | Ready | — |
| KAFF-117 | The Owner reads the audit trail, and nobody else does | 5 | Ready | 116, 118 |
| KAFF-118 | Every state change in slice 1 writes an audit record | 3 | **UNBUILT.** Nothing of this story was started. | 106, 109, 110, 111, 113, 119 |
| KAFF-119 | Register a client, with a generated code and a duplicate-phone warning | 5 | ~~`BLOCKED` 2026-09-04 (DoR 9)~~ → ~~**READY 2026-09-04**~~ → ✅ **BUILT 2026-09-04**, `86cc8b0` + `01c7b3a`. `AC-119-A`…`K` discharged and each watched failing; **`AC-119-L` HELD** (Frontend — there is no client form). **Not independently verified.** N6 answered by **D-107**. One BA line still owed on `AC-119-B`. | 106 |
| KAFF-120 | An individual's contract cannot carry a withholding rate — **defect, now wiring** | 2 | ~~`BLOCKED` 2026-09-04 (DoR 9, 11)~~ → **READY 2026-09-04.** **Its 2 points are probably now wrong** — `AC-120-C/D/E/G` are already discharged by `tests/Domain.Tests/WithholdingTests.cs`, and what is left rides on KAFF-119's and KAFF-121's endpoints. Re-estimate at the next refinement, with the team. | 119 |
| KAFF-121 | Edit a client's name and contact details | 3 | ~~**READY**~~ → ✅ **BUILT 2026-09-04**, D-109. `AC-121-A`…`H` discharged and watched; **`AC-121-I` HELD** (Frontend — there is no client form). **Not independently verified.** ~~`Client` still has no setter for `Name`, the primary phone or `Kind`~~ — **built as `Rename`, `SetPrimaryPhone` and `SetClassification`**, the last taking the kind and the tax number together because spec.md §6.7 constrains the **pair** (D-109 §1). | 119 |
| KAFF-122 | ~~Set a corporate client's withholding category~~ | — | **Superseded** → KAFF-416. **Not to be built or re-created in slice 1** — re-confirmed 2026-09-04. Carries three broken `:digits` citations, flagged and not fixed. | — |
| KAFF-123 | Archive a client | 2 | **READY.** Passed all twelve DoR lines on its own account throughout; blocked only transitively, now cleared. | 119 |
| KAFF-124 | Find a client by name, code or phone | 2 | ~~**READY**~~ → ✅ **BUILT 2026-09-04**, D-110. `AC-124-A`…`G` discharged; **`AC-124-H` half-held and `AC-124-I` HELD** (Frontend — there is no client list screen). **Not independently verified.** **`AC-124-C` works only because `Client.Create` upper-cases the code** (D-107) — the handler upper-cases the term to meet it, and the test says so. | 119 |
| **KAFF-125** | **The staff shell: session resolution, chrome, and role-based landing** | **3** | **BUILT `7461332`, ACCEPTED as an implementation 2026-09-04.** Two exceptions, neither a code defect: **`AC-125-B` is verified by code review only** — `V-32-D` established that *nothing asserts it*, and deleting the `await` it rests on left E2E at 6/6, because `src/Web` has **zero `.spec.ts` files**. And **`AC-125-C` is NOT accepted as satisfied**: it is deliberately unmet, `ux/screen-inventory.md`'s S-005 and the criterion now require opposite things, and **it is Nabil's criterion and his call.** ~~**CUT 2026-09-02**, on Nabil's ruling~~ — *"a dedicated frontend ticket must be cut for the visual shell itself … you cannot discharge a UI rendering dependency with a JSON response."* **`AC-101b-A` and `AC-101b-D` move here** from KAFF-105b and KAFF-115, which cannot discharge them (D-097 §3). **Deliberately not marked Ready or BLOCKED against a sprint** — whether it is built in sprint 2 is a scope question standing with Nabil. Renders S-004, S-005's identity half and the shell chrome today; **S-006 and S-011 have no endpoint to feed them and S-009a's route is an open UX question**. | 101a, 101b, 105a |

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
