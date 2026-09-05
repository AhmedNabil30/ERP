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
| 1a | Corrections to the brief | pending |
| 2 | Block 1 · KAFF-117 — `GET /api/audit`, Owner alone, *"even for their own projects"* | pending |
| 3 | Block 1 · KAFF-127 — `GET /api/users` and the user-management screens | pending |
| 4 | The role census — is there a second `V-33-A`? | pending |
| 5 | Block 2 · D-096 applied to the twelve lapsed stories | pending |
| 6 | Block 3 · Which stories have no QA case | pending |
| 7 | The five places a defect would be invisible | pending |
| 8 | Frontend units and the SPA build | pending |
| 9 | E2E — run more than once | pending |
| 10 | Closing gate | pending |
| 11 | Verdict per story | pending |
| 12 | What I did not reach | pending |

### Findings index

| ID | Severity | Subject |
|---|---|---|
| `V-34-A` | **MEDIUM** | **`GET /api/users` shipped against no acceptance criterion.** KAFF-127's criteria are `AC-127-A`…`I` and every one of them is a screen criterion; the story's business-rule table names no read endpoint either. The endpoint's permission, its payload and its refusals are asserted only by tests written in the same commit as the endpoint. `agents.md` §7 exists to stop exactly this: there is no statement of what *should* be true for a Verifier to execute against, only a statement of what *is* |
| `V-34-B` | **MEDIUM** | **The board contradicts itself about both sprint-4 stories, in one file.** `stories/backlog.md`'s sprint-4 lane table (lines 219–220) says KAFF-117 and KAFF-127 are **DELIVERED**; the master story inventory in the same file says KAFF-117 is `Ready` (line 816) and KAFF-127 is *"Proposed for sprint 4 Lane B. **Not pulled: scope is Nabil's**"* (line 825). **KAFF-127's story file was never touched by any of its three commits** — its header still reads `Status: **Ready** … Not pulled`. This is the third instance of the identical drift the board itself recorded for KAFF-116, KAFF-108 and KAFF-113, and block 2's whole arithmetic is read off these rows |
| `V-34-C` | **LOW** | **KAFF-125 has two rows in the master inventory** (backlog.md lines 823 and 828), with different text and different accompanying notes, and `KAFF-124` is filed after `KAFF-128`. KAFF-125 is one of the twelve stories in D-119's lapsed set, so a carry-or-lapse verdict written onto one row leaves the other saying something else |

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
| Api suite | 316/316 | *see below* | |
| Citations | 1157 / 0 / 0 | **1157 checked / 0 broken / 0 legacy, exit 0** | ✅ |
| Frontend units | 6/6 | *§8* | |
| SPA build under `strictTemplates` | clean | *§8* | |
| E2E | 18/18 (**the building agent's own figure**) | *§9* | |

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

3. **Api suite count.** *(recorded in §1 once measured — see §1.)*

## 2. Block 1 · KAFF-117

pending

## 3. Block 1 · KAFF-127

pending

## 4. The role census

pending

## 5. Block 2 · the twelve lapsed stories

pending

## 6. Block 3 · QA case coverage

pending

## 7. The five invisible places

pending

## 8. Frontend units and SPA build

pending

## 9. E2E

pending

## 10. Closing gate

pending

## 11. Verdict per story

pending

## 12. What I did not reach

pending
