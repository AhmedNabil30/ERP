# Sprint 3 — close and standup, 2026-09-05

**Ceremony held in the coordinating session at Nabil's instruction ("Scrum master do the standup and
move things up"). `HEAD` = `6bcebe5`, tree clean, `main` == `origin/main`.**

---

## 1. Gates — run in this session, at `6bcebe5`, Release

| Gate | Result | How |
|---|---|---|
| Build, Release, `-warnaserror` | **0 warnings / 0 errors** | `dotnet build -c Release -warnaserror` |
| `Domain.Tests` | **125 / 125** | `Kaff.Domain.Tests.exe`, 846 ms |
| `Api.Tests` | **295 / 295** | `Kaff.Api.Tests.exe`, 4 m 55 s |
| `dotnet format` | **exit 0** | run at `6bcebe5` before the commit |
| SM-31 citations | **1154 checked · 0 broken · 0 legacy** | `scripts/check-citations.ps1` |
| **E2E** | ⚠️ **not run at this commit** | Last figure **11 / 11**, produced at `e0fd5cf` by the build session on 2026-09-04. `6bcebe5` changes `Program.cs` — **the figure is not this commit's and is not carried forward as though it were** |

**Movement since the last standup (`01c7b3a`, 2026-09-04):** Domain **111 → 125**, Api **255 → 295**,
E2E **6 → 11**, citations **1148 → 1154**.

---

## 2. Sprint 3 is CLOSED — 22 of 22 delivered, **0 accepted**

| Lane | Story | Pts | State |
|---|---|---:|---|
| A | KAFF-119 — register a client | 5 | DELIVERED `86cc8b0` + `01c7b3a` |
| A | KAFF-121 — edit contact details | 3 | DELIVERED `1684cb9` |
| A | KAFF-124 — find a client | 2 | DELIVERED `5e8f1ad` |
| A | KAFF-123 — archive a client | 2 | DELIVERED `5a9d6d9` |
| A | KAFF-120 — an individual carries no rate | 2 | DELIVERED `b5c9e46` |
| B | KAFF-126 — the client screens | 8 | DELIVERED `e0fd5cf` |

**Plus KAFF-118 (3), built 2026-09-05 at `6bcebe5`, outside the sprint.** It is counted as delivered
work and **not** added to the sprint's 22: it was never committed to sprint 3, and inflating a closed
sprint's velocity is how an estimate stops meaning anything.

**Also delivered outside any story:** the staging move behind Caddy (`51a0c5a`), which was Nabil's
instruction rather than a backlog item.

### ⛔ The one thing that matters on this board: **nothing is accepted**

Seven stories, every one built and **self-reported by the agent that built it**.

`CLAUDE.md`: *"If you wrote the code, you do not certify it."*
`process/agile.md` §3: *"Verification — a fresh session, always."*

**§2a's own text predicted this in writing, on the day the pipeline was adopted:**

> *"What it does not buy: verification. A leading Backend lane accumulates unverified contracts, and
> `CLAUDE.md`'s 'if you wrote the code, you do not certify it' does not relax because a queue is
> moving. … a Frontend lane building against three unverified endpoints is three times the exposure,
> not one third of it."*

It is now **seven**, not three. **A Verifier session is the highest-value item on this board, ahead of
every story in the sprint-4 proposal.** It takes no points and it is not a story; it takes a fresh
session, which this one cannot be.

**Recommendation: verification runs before sprint 4 starts, not alongside it.**

---

## 3. What moved on the board today

| Move | Why |
|---|---|
| Sprint 3 → **CLOSED**, with 22/22 delivered and 0 accepted stated at the top rather than in a footnote | §2a rule 4: *"the board must not read as though it is"* accepted |
| Sprint 4 → **opened as a proposal, not locked** | Scope is Nabil's. Ceremony proposes; it does not commit |
| **KAFF-116** row corrected: `Ready` → **ACCEPTED 2026-08-24** | §4.1 below |
| **KAFF-118** row corrected: `UNBUILT` → **BUILT 2026-09-05** | Its block lifted itself when KAFF-119/121/123 landed |
| **KAFF-127 cut**, and `AC-106-J` **moved** into it | §4.2 below |
| Slice 1: **29 / 110 → 30 / 118**; grand total **142 / 677 → 143 / 685** | One row added, by addition and not by re-counting — D-096 §4 |

---

## 4. Two board defects found by going to read the board

### 4.1 KAFF-116 was `Ready` and `ACCEPTED` at the same time, in one file

`stories/backlog.md` carried **two rows for KAFF-116**. The sprint-1 status table said **ACCEPTED
2026-08-24 — D-070**. The master story table said **`Ready`**.

**The master table is the one a session reads to pick work.** So the cost of that drift is a session
rebuilding an accepted story — which is what nearly happened when KAFF-118 was picked up yesterday,
because KAFF-117 sits directly behind KAFF-116 and 118 both.

`AuditRecord.GrantPath` is built and asserted
[Verified: 2026-09-05 @ `tests/Api.Tests/AuditMechanismTests.cs` -> `A_grant_path_is_refused_without_a_project_and_may_never_be_None`],
so ACCEPTED is the true row. Corrected.

**This is the third row found stale the same way** — KAFF-108 and KAFF-113 both read `Ready` until
2026-09-01 while the same file recorded them accepted.

> **⚠️ The pattern is not "rows go stale". It is that they go stale in the table people *act on*,
> while the correct value sits in a table people only *cite*.** Three occurrences is not bad luck; it
> is a structural property of keeping one story's state in two places. **Routed to the next
> refinement as a process item, not a story:** either the master table stops carrying a state column,
> or the sprint tables do. It cannot be both, and the one that survives has to be the one work is
> picked from.

### 4.2 `AC-106-J` was homeless for nineteen days, and nothing on the board was pointing at it

KAFF-106's screen criterion was marked *"deferred to Frontend"* on 2026-08-25. **Frontend is a role,
not a story.** A criterion deferred to a role is a criterion nobody is holding, and this one outlasted
`AC-119-L`, `AC-121-I` and `AC-124-I` put together — those three were homeless for one day each before
KAFF-126 was cut for them.

`process/agile.md` §2a rule 5 already names the shape: *"A UI criterion sitting on a delivered backend
story is a defect in the board."* This is the last instance of it in slice 1.

**`KAFF-127 — the user-management screens` is cut, 8 points, `Ready`.** `AC-106-J` is **moved** into it
as `AC-127-B` — moved, not copied — and struck in place in KAFF-106 with a pointer.

**And the story is cut against the endpoints, not only the criterion**, because the hole is wider than
the thing that happened to be written down:

| Endpoint | Story | Screen | UI criterion |
|---|---|---|---|
| `POST /api/users` | KAFF-106 | none | `AC-106-J` — the only one |
| `PUT /api/users/{userId}/department` | KAFF-108 | none | **none** |
| `PUT /api/users/{userId}/role` | KAFF-109 | none | **none** |
| `POST /api/users/{userId}/deactivate` | KAFF-110 | none | **none** |
| `POST /api/users/{userId}/reactivate` | KAFF-112 | none | **none** |

**Four of the five carry no UI criterion at all, which is why only one showed up as a defect.** That
is not the same as being fine: *"the Owner creates a user"* is not a delivered capability while the
only way to do it is a POST body. **The absent criteria are the quieter half of the same defect** —
rule 5 catches the criterion that exists and says nothing about the four that were never written.

> **Routed to the BA:** when a backend story ships a capability a human is meant to use, the missing
> UI criterion is the defect, not the criterion that got deferred.

---

## 5. Sprint 4 — **proposed, not locked**

### Lane A — Backend · 12 points

| # | Story | Pts | Note |
|---|---|---:|---|
| 1 | **KAFF-117** — the Owner reads the audit trail | 5 | **Both dependencies cleared this week**: KAFF-116 (§4.1) and KAFF-118 (D-116). Nabil, verbatim: *"The Audit Trail is strictly limited to the Owner (Global) … completely hidden from all other roles, even for their own projects."* Slice 1's gate is *permission tests pass*, and this is the strictest permission in the system |
| 2 | **KAFF-107** — an HR user cannot be created or moved outside HR | 2 | ⚠️ **Carries broken `:digits` citations**, flagged at the sprint-3 refinement and never fixed. **A DoR failure — repair before pulling** |
| 3 | **KAFF-104** — reset a forgotten password | 5 | The last unbuilt door in identity; `AC-110-E` is deferred to it |

### Lane B — Frontend · 16 points

| # | Story | Pts | Note |
|---|---|---:|---|
| 1 | **KAFF-115** — the project team panel | 8 | `READY` since 2026-09-02 and **its API is merged** — `GET /api/auth/me` already returns both surfaces as two types. §2a rule 1 satisfied |
| 2 | **KAFF-127** — the user-management screens | 8 | Cut today, §4.2 |

**Backend leads by one story (§2a rule 2).** KAFF-117 is Lane A's first because Lane B's first needs
nothing new from it — which is what a lead is for. **28 points is above the observed rate and is not a
commitment**; the ordering is the proposal, the volume is Nabil's.

---

## 6. What did not move, and is still owed

Every item below was owed at the 2026-09-04 standup and is owed at this one. **Nothing on this list
moved in a week.**

| Item | Owner | Note |
|---|---|---|
| **Verification of the entire Client master** | Verifier | **New, and it outranks everything else here.** §2 |
| **`V-31-A` (HIGH, open)** | Architect | A misfloored account that has taken a posting cannot be repaired by any supported operation. **A repair story is owed, not another detector** |
| **`N11`** — partition `audit_records` from the start | Architect | **Deadline is slice 3, not slice 9**, and slice 3 is the next slice after this one. Converting a populated, trigger-protected, append-only table is a new table plus a migration plus a swap. **This is the item most likely to be regretted** |
| **`scripts/check-citations.ps1` reads `.md` only** | — | 80 `[Verified:` markers in `.cs`/`.ts` have never been checked, and they use a different shape, so widening the file filter is not enough. Routed as its own story at D-110 §5; **still not cut** |
| **`KAFF-107` / `KAFF-122` broken `:digits` citations** | BA | KAFF-107 is now **proposed for sprint 4**, so this stops being cosmetic |
| **Local `kaff` database unbootable** | Nabil | Plus `kaff_ui`, `kaff_demo` and `kaff_e2e` scratch databases still enumerated |
| Four `audit.grant.*` orphaned i18n keys | — | Kept pending a KAFF-117 judgement — **which sprint 4 Lane A would now make** |

---

## 7. For Nabil

**Two decisions, and one of them has a deadline attached.**

1. **Does verification run before sprint 4, or alongside it?** The recommendation is *before*. Seven
   stories are delivered and none is certified, and every sprint-4 story adds to that number.
2. **Sprint 4 scope.** The proposal is Lane A: 117 → 107 → 104; Lane B: 115 → 127. **28 points is
   above the observed rate**; the ordering is what the ceremony is proposing, not the volume.

**Still open from 2026-09-04, and unchanged:**

- **⛔ `Q57`** — may the client-code sequence contain gaps? A burnt number cannot be backfilled, and
  §4.8 of the last standup records that closing it costs more than D-107 assumed.
- **`AC-125-C`** — not a ruling, an unperformed check: sign in as `sara_finance_demo` and see whether
  the screen is S-005. **Now cheaper than it was** — staging is HTTPS since `51a0c5a`, so a browser
  will actually keep the auth cookie, which it would not do before.
- **Six provisional answers**, all reversible, all still pending Karim.

**And one thing that is nobody's decision and is simply late: `N11`.** Slice 3 is the next slice.
