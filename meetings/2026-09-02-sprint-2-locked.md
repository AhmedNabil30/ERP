# Sprint 2 — Nabil's rulings applied, the repair work executed, and one question back to him · 2026-09-02

**Scrum Master.** `agents.md` §3b, `process/agile.md` ceremony 2. This run follows
`meetings/2026-09-01-sprint-2-refinement.md`, which ended with **seven questions standing with Nabil**
and a repair sprint **proposed, not locked**.

**Nabil ruled on three of the seven.** They are applied below, the ticket he ordered is cut, and the
repair work is done. **The other four are untouched and none was answered by any agent.**

**Decisions from this run: `decisions.md` D-100 (mine), D-101 (Architect), D-102 (Backend).**

---

## 1. The three rulings, and where each landed

### 1.1 `Q43` — ANSWERED, both halves

> *"The Reference Code is mandatory alongside the project name (format: `[RefCode] Project Name`). In
> construction/engineering ERPs, project names frequently overlap (e.g. 'Capital Site - Phase 1' vs
> 'Phase 2'). The RefCode is the hard identifier that prevents HR from misallocating staff to the wrong
> site."*
>
> *"Team Size: Yes, displaying the current headcount is required. It serves as the primary visual
> indicator, allowing HR to spot unstaffed sites at a glance without drilling down."*

**The format is part of the ruling, not a suggestion**, and it carried to every place the code and the
count are read. One distinction was held throughout and is recorded in D-100 §1 because it will be got
wrong otherwise: **the payload carries three fields; `[RefCode] Project Name` is a display format and
belongs to the rendering stories, never to the JSON.** A pre-formatted display string in an API response
is a translation decision taken on the server.

| Where | What changed |
|---|---|
| `stories/questions-for-karim.md` | The `Q43` row **moved out of the open list into the answered table**, with both halves and the format, dated 2026-09-02 against D-100. Every sentence still asserting it open was corrected loudly rather than deleted |
| KAFF-105b | Rules 6 and 6a, `AC-105b-C` and `AC-105b-F` carry the reference code and the team size, now **cited** where they were previously asserted against a citation that does not grant them |
| KAFF-115 | `AC-115-H` cites the ruling and renders `[RefCode] Project Name` |
| KAFF-113 | **Nothing.** See §1.4 |

**Team size is the count of active `ProjectAssignment` rows** — the set KAFF-115 rules 1 and 4 already
define — **derived on read, never stored**, for the same reason a balance is never stored.

### 1.2 The register row does not read *open* any more, and the prose around it was stale before today

The BA found, moving it, that the sentence *"the first row now is Q43"* had **already been false since
2026-08-24** — `Q-N10-2b`, `Q-N10-1`, `Q-N10-3` and `Q55` have stood above it since. Corrected against
the list as it is today rather than as the prose described it. That is SM-29 firing on a file nobody had
re-read.

### 1.3 KAFF-105b is Ready, and KAFF-115 with it

**`Q43` was KAFF-105b's sole remaining Definition of Ready failure** — the other five were repaired on
2026-09-01. **Ready at 5.**

KAFF-115's transitive block cleared with it, and **its own three failures were repaired the same day**,
which nobody had done:

| Failure | Repair |
|---|---|
| `AC-115-H` | Its given named *"a budget … and a balance"*. `Budget` exists nowhere in `src/Domain/` and a stored balance is forbidden outright; and its last And asserted a `403` from a **project dashboard endpoint that does not exist**. Restated against what is arrangeable and what the rule actually lives in |
| `AC-115-I` | *"When the code is read, then they are different types"* — a manual review instruction that cannot fail. Now a reflection assertion, the shape `TC-1-046` already uses |
| **`AC-115-G`** | **It passed for the wrong reason**, which is worse than failing. It asserts *"refused with 403 — `PortalRead` is not `ProjectRead`"*; a `Role.Client` is refused at the staff door by `MayHoldStaffSession` **before any handler or permission check runs**, so the criterion would have stayed green if the rule it names were deleted |

**Ready at 8.** Both stories Ready — and **neither is in sprint 2**; see §3.

### 1.4 One correction the BA returned, and it is the invitation paying for itself again

**KAFF-113 has no project picker.** My brief said the ruling should carry to one, on the strength of the
register's own *"the picker in KAFF-113's screen"*. The story's criteria are backend and permission logic
throughout, and the only picker in that flow is a **user** picker on S-010, by which point the project is
already chosen. **The BA changed nothing and reported it**, which is what the brief asked for and the
correct outcome. `agents.md` principle 7, fourth sprint running.

---

## 2. Nabil's repair list was four items stale, and correcting it is the reason this sprint was short

**Every item below was verified against the files and the git history today, not taken from a brief.**
Nabil named these as open; they are not.

| He listed as open | State, established 2026-09-02 |
|---|---|
| **`POST /api/setup`'s `500`** | **Fixed at `45a939d`** — *"A malformed request body is 400 in every environment, not 500 in one"* — and it now has the `decisions.md` entry it shipped without: **D-099**, written 2026-09-01 at `e93029b` |
| **`V-27-A`, `V-27-B`, `V-27-C`** | **Fixed and independently accepted.** `qa/slice-1/verification-2026-08-30.md` §11 — **all six commits `ACCEPT`, none rejected**, each on a mutation the Verifier watched fail, not on the author's evidence |
| **`V-30-A`, `V-30-C`, `V-30-H`** | **Repaired 2026-09-01** at `93fa417`, `e93029b` and `f47416d` |
| **The QA block, as eight looping items** | **Six of ten closed** at `95b050e`; **two needed no QA at all** — `TC-1-120` and `TC-1-094` were correct as written and three routings had failed to establish that; **two remain blocked** on named questions (`TC-1-042` on `SM-32`, `TC-1-079` on `Q56`) |
| *(not on his list, and also done)* | **The staging SPA assertion** landed at `3d98fa1` — the smoke check now fetches `${STAGING_URL}/` and asserts the served document carries `<kaff-root>`, closing the frontend half of *"runs on staging"*. **I have not observed that workflow run green**; it is committed and pushed and that is all I can say |

**What actually remained** — and this is what sprint 2 executed: **`V-30-D`, the safe-balance layer
underneath it, `V-30-B`, and `V-30-G`.**

---

## 3. The repair work, and who did it

**One agent on the machine at a time** throughout (`process/agile.md` ceremony 2, amended 2026-08-30).
The BA ran first and touched no stack; the Architect and Backend each had the machine to themselves and
each stopped it afterwards.

### 3.1 Architect — the safe floor · D-101 · `c7ae3d1`, `172eab0` · strongest model

**Nabil is right that `V-30-D` is the one that matters, and the layer under it was worse.** The Architect
**falsified two of my own claims on the machine before acting on them**, which is the behaviour §M says
never to downgrade:

- *"Verified by nobody"* was **wrong**. `tests/Api.Tests/TreasuryGuardTests.cs` -> `The_safe_balance_cannot_go_negative`
  verifies the rule **behaviourally** against a real PostgreSQL — funds a Safe with 1,000, spends 5,000,
  requires `KAFF_NEGATIVE_BALANCE`. Gutting the trigger body while keeping its name turns **1 of 227
  red**, that test alone. **The ceremony reasoned outward from `FindMissingGuardsAsync` and never
  searched `tests/` for the rule itself.**
- *"No account set"* was **wrong**. `AccountTreeSeeder` -> `MainSafeCode` inserts `SAFE-MAIN` on every
  start-up; **fourteen account rows exist on the dev database today.** That sentence is the one that made
  this look deferrable.

**The finding underneath survives both corrections and was exploitable.** A Safe row inserted with
`enforce_non_negative = false` took an overdraw to **−4,000.0000** while `FindMissingGuardsAsync`
returned nothing missing and `/api/health` reported `guardsInstalled: true`.

**The behavioural assertion already existed; the data one was missing, so the data one was written** —
one query inside `FindMissingGuardsAsync`, which is already the mechanism that refuses the host's
start-up (D-033) and feeds the `guardsInstalled` field `deploy-staging.yml` greps. **So the staging
pipeline now asserts the floor is real on every deploy.** Watched failing both ways: an unfloored row
moves `/api/health` from `200 healthy` to **`503 degraded`** naming it.

**And the thing I asked to be weighed came back inverted.** `trg_accounts_configuration_immutable` is
`BEFORE UPDATE`, so it closes the *flip* and not the *value* — an `INSERT` never meets it, and on an
already-wrong row **it is the mechanism that makes the wrong value permanent.** It is the one guard in
that file whose correctness makes a defect harder to repair. **The repair for such a row is not an
`UPDATE`; the row must be closed and the account reopened.**

**`V-30-B` — ruled: not now, with a trigger condition**, which is a real answer rather than a deferral.
Both `SelfOnlyEndpoints` members already have all three checks covered concretely by hand-written tests;
a sweep would assert more weakly what those assert precisely, and would have to invent a request body for
an arbitrary route. **It becomes worth building on any one of:** a third `SelfOnlyEndpoints` member; a
member whose own tests miss one of the three checks; or a self-only route that touches money or a posting.

### 3.2 Backend — `V-30-D` and `V-30-G` · D-102 · `2f4b276`, `8767c90` · mid model

**`V-30-D` is closed at expression level.** The Architect's measurement decided its shape and Backend
re-verified it rather than inheriting it: comparing the **authored** SQL to PostgreSQL's re-print is not
stable — `amount > 0` comes back as `CHECK ((amount > (0)::numeric))`, so a checker built that way reports
thirty failures on a correct database the day it ships. **PostgreSQL's re-print is itself the stable
normal form**, and that is what is now snapshotted and compared on every call.

**Watched failing on both mutations, including the one Nabil named:** `MUT-C3` (name kept, predicate
→ `1 = 1`) and the money case (`amount > 0` → `amount >= 0`) each now take `/api/health` to **`503
degraded`** with the mismatch named. **D-093's friction survives** — the snapshot is a second file the
guarded edit cannot update by accident, which is the property `V-27-A` existed to restore.

**`V-30-G` is closed on both halves, and the open machine question is answered by trying it.**
`meetings/2026-09-01-sprint-2-refinement.md` §2.3 item 1 asked whether the Api test host can run as
`Development` without tripping the start-up guard refusal. **It can** — the refusal is conditioned on
`!IsDevelopment()`. So the suite now carries a regression case against the **shipped** `POST /api/auth/sign-in`
route and a second against a **Development** host, which is what `V-30-G` said no test in the repository
did.

### 3.3 Gates, measured today rather than inherited

| | Start of run | End of run |
|---|---|---|
| Build `-c Release --no-incremental` | 0 / 0 | **0 warnings / 0 errors** |
| `dotnet format --verify-no-changes` | exit 0 | **exit 0** |
| Domain | 107 / 107 | **107 / 107** |
| Api | 227 / 227 | **235 / 235** — 8 new tests, all of them things that previously stayed green when the rule was broken |
| `/run-kaff-erp smoke` | 8 / 8 | **8 / 8** |
| `scripts/check-citations.ps1` | 1053 · 0 broken · 0 legacy | **1083 · 0 broken · 0 legacy** |

---

## 4. `KAFF-125` — the ticket Nabil ordered cut

> *"KAFF-105b (Backend) remains the API payload ticket. It technically satisfies the backend portion of
> the ACs the moment it returns the correct role/permission data structure. A dedicated frontend ticket
> must be cut for the visual shell itself — the layout, sidebar, header, and role-based routing.* **You
> cannot discharge a UI rendering dependency with a JSON response.**"

**`KAFF-125 · The staff shell: session resolution, chrome, and role-based landing`. 3 points.**
`stories/slice-1-foundation/KAFF-125-staff-shell.md`.

**`AC-101b-A` and `AC-101b-D` move to it.** KAFF-101b's deferrals are re-pointed in a dated amendment;
the IDs do not move, because an AC-ID is an identity and not a position.

**Its criteria are bounded by what an endpoint can feed, and the story says so in a table rather than in
prose.** The whole API exposes three `GET` routes.

| Role | Ruled landing | Can `KAFF-125` render it? |
|---|---|---|
| Finance, TechnicalOffice, SiteEngineer, HeadOfDesign | S-005 My profile | **Partially, today.** `/api/auth/me` carries display name, role and department. S-005 also wants *"the projects I am assigned to with my level"* — **that field is KAFF-105b's and is not built.** The identity half renders now; the assignment half renders the day KAFF-105b ships, with no change to this story's code |
| Owner | S-006 User list | **No. There is no list-users route** and no story builds one |
| MarketingSales | S-011 Client list | **No.** Clients are KAFF-119…124, deferred out of sprint 1 entirely |
| Hr | S-009a HR project list | **Undecided, and deliberately.** Its *data* is what KAFF-105b's payload carries — but `ux/screen-inventory.md` -> `S-009a` and `ux/navigation.md` both describe *"HR's own routes against its own API"*, a dedicated HR endpoint nothing builds. **Two different shapes, and no reading was picked** |

**No criterion asserts S-006, S-011 or S-009a rendering real data.** `agents.md` §3c's hard rule cuts both
ways: **a criterion that cannot pass is as bad as one that cannot fail.**

**Where a ruled landing has nothing to render, the story raises a question and does not invent an interim
one.** *"The Owner sees My profile until S-006 exists"* is exactly the plausible fill `agents.md` names as
this project's most expensive failure mode. Four questions are carried in the story, all routed, none
answered: the Owner's landing and MarketingSales's landing (**UX, then Nabil**), S-009a's route (**UX +
BA, then Nabil**), and **B3-8** — who holds the `/api/auth/me` result inside the shell and what
invalidates it, which decides whether a revoked assignment leaves the navigation wrong for one second or
one session (**Architect**).

**On the estimate, and I am recording the weakness rather than the number alone.** 3 rests on evidence the
BA established and I did not expect: `AuthService`'s three session signals and `mustChangePasswordGuard`
**already exist**, so this is chrome, dispatch and per-role routes on top of a service that is built.
**Frontend has not confirmed it independently**, which KAFF-115's 8 had and this does not. If Frontend
returns a different number at the next refinement, the number moves, not the story.

**One defect found on the way and left for its owner:** `src/Web/src/app/app.routes.ts`'s wildcard route
still attributes the staff shell to *"KAFF-105b's shell"* — the exact confusion this ruling corrects.

---

## 5. The question for Nabil — the demo against the repair sprint

**His closing words this run:** *"we are still in sign in page we want to move to have demo to client."*

**That pulls against his own ruling 3** — *"an answer to Q43 does not change its shape … pay the technical
debt first."* He ordered the shell ticket **cut**; cutting is not building. **Whether it is built in sprint
2 is scope, scope is his lock and not mine, so I have not decided it, have not assumed the demo overrides
the ruling, and have not widened the sprint to accommodate it.**

**Here is the trade, priced.**

### What a demo needs that does not exist

**Two screens exist in this application: the sign-in screen (S-001) and the change-password screen
(S-003).** There is no shell. **After sign-in, every one of the nine roles lands nowhere** — not five of
them, all of them. That is the whole of the gap, and it is a frontend gap, not a foundation one.

### What a demo shows today, repairs done and no shell

Real, and it runs — driven live at HEAD by the Verifier on 2026-08-30, not claimed:

- sign in through the screen, in Arabic, RTL, at 390px, no untranslated key;
- a wrong password refused, and the refusal reading the same for a wrong password, an unknown username
  and a locked account — one message, because the server sends one `messageKey`;
- a temporary password forcing a change, unskippable, surviving a reload;
- a password change ending every other session;
- sign-out.

**That is the security foundation demonstrated, and it is the thing this project has spent three sprints
buying.** It is not a product tour.

### What building the shell costs, in three options

| | Scope | Points | What the client sees | What it still does not show |
|---|---|---:|---|---|
| **A** | **Repairs only** — as ruled | **0 new** | The door, above. Sprint closes on a Verifier pass | Anything past sign-in |
| **B** | **+ `KAFF-125`** | **3**, frontend only | Sign in and **stand somewhere real**: header, side navigation, locale switch, the drawer at 390px, and S-005 showing your name, role and department | The Owner and MarketingSales still land nowhere — open questions 1 and 2. No Kaff data on any screen |
| **C** | **+ `KAFF-125` + `KAFF-105b`** | **8** | The above, **plus the first screen that shows Kaff's own data**: the projects you are on and your seniority on each | Still no user list, no client list, no project dashboard, no money |

**None of the three shows a project, an extract or a pound.** Those are slices 3 to 5 and no amount of
sprint-2 scope reaches them. **A demo of C is honestly described as *"the system knows who you are, what
you may do, and which sites you are on"*** — which is what slice 1 is, and it is worth showing.

### What changed since he ruled, and it is the part that should decide this

**The foundation debt he ruled must be paid first is paid, today** — `V-30-D`, the safe-balance layer,
`V-30-B` and `V-30-G`, in D-101 and D-102. **The shell is no longer competing with repairs.** What is
still owed before this sprint closes is **a Verifier pass over D-101 and D-102** — both changed
`FindMissingGuardsAsync`, which decides whether the host starts, and `CLAUDE.md` is unambiguous that the
author does not certify its own work.

**One constraint on option C specifically:** KAFF-125 is Frontend and KAFF-105b is Backend, and **one
agent runs on this machine at a time**. They serialise; they do not run in parallel.

> ### The question
>
> **Does sprint 2 close on the repairs and a Verifier pass (A), or do you pull `KAFF-125` (B) or
> `KAFF-125` + `KAFF-105b` (C) into it now that the debt you ruled against is paid?**
>
> **It is yours. No agent may take it, and I have not.**

---

## 6. The questions still standing

**Four business questions, none touched, none answerable by any agent.** Plus the scope question above,
and the ones raised by this run's own work.

| # | Question | Owner | State |
|---|---|---|---|
| 1 | **`KAFF-118`'s cut** from a locked sprint | **Nabil** | Unchanged. Unbuilt, 3 points, depends on KAFF-119 which is deferred out of the sprint, so it cannot complete as written whatever he rules |
| 2 | **`Q56`** — may a role change **to** `Role.Subcontractor`: refuse, or succeed and clear the credential? | **Karim** | **Now numbered**, which it was not before 2026-09-01. Both readings recorded in D-088, neither chosen. The reversible half is what is built |
| 3 | **The `mustChangePassword` reach** beyond `/api/auth/me` and change-password | **Nabil** | **`V-30-I`** — `AC-106-H` and `AC-105a-C` contradict each other **in committed text**, and the Verifier observed **both** behaviours. The code takes `AC-105a-C`'s side deliberately and says so |
| 4 | **`Q54`'s retention period** | **Karim** | **Split.** The mechanism is settled (D-072 §3); **no number was ever given** and the original question asked for one in as many words. Partitioning can be built without it; it cannot drop anything without it |
| 5 | **Does the shell get built in sprint 2?** | **Nabil** | §5. New, and the only one this run put back to him |
| 6 | **The Owner's and MarketingSales's slice-1 landings**, where the ruled screen has no endpoint | **UX, then Nabil** | New, raised by `KAFF-125` and deliberately not filled in |
| 7 | **Does S-009a render from `/api/auth/me`, or from the dedicated HR API `ux/` describes?** | **UX + BA, then Nabil** | New. Two different shapes, neither built. **`AC-101b-D` now sits on a story that cannot yet say where HR lands** |
| 8 | **B3-8** — who holds the `/api/auth/me` result in the shell, and what invalidates it | **Architect** | Carried from 2026-09-01, unresolved, now named inside the story it decides |
| 9 | **`TC-1-042`** (`SM-32`) and **`TC-1-079`** (`Q56`) | **BA / Karim** | The two QA rows that need a decision rather than a session. Unchanged |

---

## 7. What this run did **not** do — as a count, not as prose

`meetings/2026-08-27-sprint-1-retrospective.md` §3: a tool that says *"N checked"* must also say
*"M skipped."* **Nine.**

1. **Nothing built today has been independently verified.** D-101 and D-102 both changed
   `FindMissingGuardsAsync`, which decides whether the host starts. **A Verifier pass is owed before this
   sprint closes**, and no session other than the authors has looked at either change.
2. **`ux/navigation.md` was not corrected.** It still describes `mustChangePassword` as a **refusal**,
   the reading D-072 §2 replaced on 2026-08-24. Routed to UX at the 2026-09-01 refinement and again
   today; still not done. **KAFF-125 is written against D-072 §2 and says so**, so no story commands the
   defect — but the file is still wrong for whoever reads it next.
3. **`src/Web/src/app/app.routes.ts` was not fixed.** Its wildcard route still attributes the staff shell
   to *"KAFF-105b's shell"*. Found by the BA; it is Frontend's file.
4. **The staging SPA smoke step was not observed passing.** `3d98fa1` is committed and pushed; whether
   the workflow has run green is unverified in this session. **Nobody has fetched the SPA from staging**,
   in this run or any other.
5. **No E2E Playwright test was run.** `KAFF_E2E_BASE_URL` was never set, in this run or the last two.
6. **`qa/slice-1/test-cases.md` was not executed case by case.** No `TC-` identifier was run.
7. **No slice-3 money invariant was tested, because none exists to test.** The §15 worked example, hold
   equals exactly 20%, advance reaches exactly zero, تشوينات nets to zero — **zero of those assertions
   exist.** Named again so 235/235 and 1083 citations are never read as coverage of the thing this system
   is for.
8. **Slices 2 and 9 were not refined**, and no retrospective was held. `process/agile.md` puts the
   retrospective after acceptance, and nothing has been accepted.
9. **`KAFF-125` was not sized by Frontend.** The 3 is the BA's, on evidence, and it is the one estimate in
   this file with no independent second opinion.
