# Sprint 4 — locked, 2026-09-05

**Nabil, 2026-09-05: *"Run before and scrum master decides."*** Two rulings in five words.
`HEAD` = `93a517b`.

---

## 1. Ruling 1 — verification runs before sprint 4

**Accepted and gated.** No sprint-4 story starts until a Verifier pass reports.

**And it settles the sequencing, not the who.** The Verifier must be a session that did not write this
code, and the coordinating session did — all seven unverified stories, and this brief. `CLAUDE.md`:
*"If you wrote the code, you do not certify it."* `process/agile.md` §3: *"a fresh session, always."*

**What the Scrum Master can do about it is write the brief, and it is written:**
**`meetings/BRIEF-2026-09-05-verifier.md`**. It carries the seven stories, the four suites in
`agents.md` §7's priority order, **seven places to look chosen because a defect there would be
invisible**, and **three claims worth disbelieving** — including the three mutation-run false
negatives caught in one day (D-109 §3), one of which was one step from banking *"the permission gate
is not asserted."*

**Start it in a new session.** It reads `spec.md` and `qa/slice-1/test-cases.md`, not the
implementation.

---

## 2. Ruling 2 — the scope. **13 points, not 28.**

| Lane | Story | Pts |
|---|---|---:|
| **A — Backend** | **KAFF-117** — the Owner reads the audit trail, and nobody else does | **5** |
| **B — Frontend** | **KAFF-127** — the user-management screens | **8** |

### Why these two

**KAFF-117** because both its dependencies cleared this week — KAFF-116 (accepted 2026-08-24, though
the board said `Ready` until yesterday) and KAFF-118 (built 2026-09-05). And because slice 1's
acceptance gate is *permission tests pass*, and this is the strictest permission in the system: Nabil,
verbatim — *"strictly limited to the Owner (Global) … completely hidden from all other roles, **even
for their own projects**."* **No other permission in this codebase has that last clause**, and an
assignment-based system is exactly where it would be got wrong.

**KAFF-127** because it converts **KAFF-106 from unacceptable to acceptable**. §2a rule 4: a story
whose screen criterion is undischarged is not accepted. `AC-106-J` was homeless for nineteen days.
**Closing a delivered story beats opening a new one**, and with seven stories delivered and none
accepted, that is not a close call.

### Why not the other three

| Story | Pts | Why not |
|---|---:|---|
| **KAFF-115** — the project team panel | 8 | Nothing wrong with it. **It opens a new capability while KAFF-106 is still unacceptable**, and with verification pending it would be a third unverified surface. Sprint 5, Lane B, first |
| **KAFF-107** — HR outside the HR department | 2 | ⚠️ **DoR failure** — broken `:digits` citations, flagged at the sprint-3 refinement and never fixed. A story with a known DoR failure is not pullable. **The repair is the BA's and it is now blocking a sprint, which is what it took to stop being cosmetic** |
| **KAFF-104** — reset a forgotten password | 5 | Least urgent of the three: the Owner can already issue a temporary password through KAFF-106 |

### Why 13 and not 28

**Sprint 3 delivered 22 points in a week and none of it is accepted.** A velocity counted from
delivered-but-uncertified work is a number that flatters — it measures how fast code is written, not
how much of it survives being checked.

**13 is set to leave room for the verification pass to return work**, which is the likeliest single
event in this sprint. If it returns nothing, the sprint finishes early and KAFF-115 is pulled
forward. That is a better failure mode than the reverse.

---

## 3. §2a rule 6 — written today, applied the same hour

**`KAFF-117` carries `AC-117-I`: "Arabic, RTL, at mobile width."** Pulling it into a Backend lane
would have created **the eighth homeless UI criterion in this slice.**

Rule 5 would have caught it — after delivery. It caught `AC-106-J` after **nineteen days**, and
`AC-119-L`, `AC-121-I` and `AC-124-I` after one day each, and only because the pipeline was being
written that week and somebody happened to be looking. **Rule 5 is a cure that kept being needed,
which is the definition of a missing rule upstream of it.**

**New, in `process/agile.md` §2a:**

> **6. A backend story carrying a UI criterion is not pullable until the Frontend story that will
> discharge it exists on the board.** Not "is scheduled" — **exists**, with an identifier, so the
> criterion has somewhere to move to on the day the backend story is pulled rather than on the day
> somebody notices.
>
> **The test is mechanical: name the story identifier the criterion moves to. If you cannot, the
> backend story is not `Ready`.**

**Applied immediately: `KAFF-128 — the audit trail screen` (3) is cut**, and `AC-117-I` moved into it
as `AC-128-A` **before a line of KAFF-117 was written**. First criterion in this project to move
before its backend story rather than after it.

**KAFF-128 is not in sprint 4** — §2a rule 1 forbids starting a screen against an unmerged API, and
KAFF-117 is being built in this sprint. Sprint 5, Lane B, behind KAFF-115.

### And the quieter half, which rule 6 also names

Rule 5 can only see a criterion that was written down. **Four of the five identity endpoints carry no
UI criterion at all** — move department, change role, deactivate, reactivate. That is not the same as
being fine: *"the Owner creates a user"* is not a delivered capability while the only way to do it is
a POST body. Rule 6 makes the question mandatory: **ask what screen discharges this.** *"None, by
design"* is a fine answer and gets written down. **No answer is a story that is not `Ready`.**

---

## 4. The board after this meeting

| | |
|---|---|
| Sprint 3 | **CLOSED** — 22/22 delivered, **0 accepted** |
| Sprint 4 | **LOCKED** — 13 points, gated on verification |
| Slice 1 | **31 stories / 121 points** (was 30 / 118 — KAFF-128 at 3) |
| Grand total | **144 / 688** |
| Stories cut today | **KAFF-127** (8, sprint 4) · **KAFF-128** (3, sprint 5) |
| Criteria moved | `AC-106-J` → `AC-127-B` · `AC-117-I` → `AC-128-A` — **both moved, not copied** |

---

## 5. Still owed, and one of them is now late rather than pending

| Item | Owner | Note |
|---|---|---|
| **`N11`** — partition `audit_records` from the start | **Architect** | ⚠️ **This is the item that is late, not deferred.** Its deadline is *before slice 3*, slice 3 is the next slice, and it has not moved in a week. Converting a populated, trigger-protected, append-only table is a new table plus a migration plus a swap — the cost only goes up |
| **`V-31-A` (HIGH, open)** | Architect | A misfloored account that has taken a posting cannot be repaired by any supported operation. **A repair story, not another detector** |
| **`KAFF-107`'s broken citations** | BA | **Now blocking sprint scope**, which is what it took |
| **`scripts/check-citations.ps1` reads `.md` only** | — | 80 `[Verified:` markers in `.cs`/`.ts` unchecked. Routed at D-110 §5, still not cut |
| **`Q57`, `AC-125-C`, six provisional answers** | Nabil / Karim | Unchanged from 2026-09-04 |

---

## 6. For Nabil — one line

**Nothing is asked of you.** Both decisions were delegated and both are taken. **Start a fresh session
and point it at `meetings/BRIEF-2026-09-05-verifier.md`** — that is the only thing standing between
this board and its first accepted points since 2026-08-26.
