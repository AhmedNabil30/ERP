# Kaff ERP — The Agent Team

**Stack:** .NET 10 (LTS) · ASP.NET Core · EF Core · PostgreSQL · Angular 22 · xUnit · Playwright
**Mobile:** .NET MAUI or Flutter — decided at the mobile phase, not now.

---

## Principles that make a team of agents work

**1. Sessions have no memory. Files do.** Three files are the team's shared brain: `spec.md` (business truth), `CLAUDE.md` (rules and conventions), `decisions.md` (why things are the way they are). Every agent reads them first and updates them last.

**2. The author never certifies its own work.** An agent asked to test what it just wrote will write tests that pass. Verification is always a separate session that reads `spec.md`, not the implementation.

**3. File ownership is disjoint.** Two agents run concurrently only when they cannot touch the same files. Otherwise they run in sequence.

**4. Dictate architecture, don't let it emerge.** .NET permits many respectable structures. Agents will pick different ones in different sessions. `CLAUDE.md` names one pattern and forbids the alternatives.

**5. One agent, one slice, one gate.** Never hand an agent a whole module.

**6. Somebody's only job is to ask.** From slice 1 the team runs a refinement session before every
sprint, and the Scrum Master's job in it is not to plan — it is to make each agent say what it does
not know. See `process/agile.md`. A refinement that produces no questions has not been run properly.

**7. Every brief ends by inviting its own correction.** The last line of any brief handed to an agent
is: **"Report anything in this brief that was wrong."** Not a courtesy — on 2026-08-22 four briefed
agents returned four corrections, two of which changed a ruling the Scrum Master had already made.
One of them found that **SM-31, as first ruled, would have reported green over 77 defective
citations** — the rule's own failure mode, inside the rule's own remedy, four hours after its author
wrote that this class of problem is not fixed by asking people to be careful.

A brief is written by an agent that cannot see the files the receiving agent is about to open. It
will contain stale claims. The only question is whether they come back.

**8. Findings are routed, not logged.** See §3b. A defect recorded in a register and assigned to
nobody is a defect nobody is fixing.

**9. Nobody hand-rolls the run commands.** The stack is started and driven through the
`/run-kaff-erp` skill — `.claude/skills/run-kaff-erp/`. Ports, environment variables and the smoke
check live there, verified, in one place. An agent that invents its own `dotnet run` invocation gets
a different stack from everyone else's, and "works on my machine" becomes unfalsifiable. See §B0.

**10. Match the model to the task. Budget is a real constraint on this project.** See §M below. The
short version: **routine work runs on a small model, judgement about money, permissions or
architecture does not.**

---

## §M · Which model runs which agent

Adopted 2026-08-25, at Nabil's direction, after the Scrum Master was killed by a spend limit **seven
times in four days** — each death costing a full context rebuild on the next run, which is itself
expensive. Budget exhaustion is not a background annoyance here; it is the single largest cause of
lost work in this project so far.

**The rule is not "use the cheap model". It is "stop spending the expensive one on work that does not
need it."** Most of what these agents do is mechanical: renaming a citation, marking a status,
sweeping a register, listing which keys are missing from a catalogue. None of that benefits from the
strongest model, and all of it has been consuming it.

| Task | Model | Why |
|---|---|---|
| Architect — permission scope, the audit mechanism, anything touching money or the ledger | **strongest** | These decisions are unbackfillable. D-061, D-063 and D-073 all turned on a distinction a weaker model would have flattened |
| Verifier | **strongest** | Its whole job is finding what another agent missed. It has caught a real defect on every run |
| Scrum Master — refinement, routing, ruling on a rule | **strongest** | Judgement, and it has repeatedly declined to do the wrong obvious thing |
| Backend / Frontend — implementing a story whose criteria are already written | **mid** | The thinking is in the story. This is transcription with a compiler as the gate |
| BA — writing stories from an answered ruling | **mid** | |
| QA — test cases from stable AC identifiers | **mid** | |
| Citation sweeps, status updates, register bookkeeping, i18n key inventories, renames | **small** | Mechanical, verifiable, and the checker catches a mistake |

**Where the line actually is.** Not "important vs unimportant" — everything here is important. It is
**"is the answer discoverable by following a rule, or does it require judging a trade-off nobody has
written down?"** Marking `AC-110-D` deferred follows a rule. Deciding *whether* a deferred criterion
is honest is a judgement.

**Never downgrade for:**
- Anything that decides who may touch money, or what the ledger records.
- Anything appended to `decisions.md` as a **decision**. Recording one is bookkeeping; making one is not.
- A Verifier pass. A cheap verification that misses a defect costs more than the defect.
- Anything where the agent must **refuse** — a business rule that does not exist, a story that
  contradicts `spec.md`. Refusing well is the most valuable behaviour in this project and the easiest
  to lose.

**How.** The `model` parameter on the spawn selects it. Say which model a delegated task runs on when
you brief it, so the choice is visible and can be argued with.

**And split the task before downgrading it.** A brief that mixes a register sweep with a ruling has to
run on the strongest model for the ruling's sake, and pays that rate for the sweep. Send the sweep
separately. That is most of the saving.

---

## Phase A — Before any code

### 1. Architect Agent
**Produces:** `architecture.md`, the database schema, the module map, and `CLAUDE.md` itself.

Owns: the folder structure and the pattern (vertical slices — one folder per feature, containing endpoint, handler, validator, DTOs — **not** layered Clean Architecture); the `Posting` model and account tree; the interface shape of the three billing calculators; the permission model; how non-cash postings are typed; migration and seeding strategy.

**Must encode:** money as `decimal(18,4)` with EF precision configured explicitly, since EF Core will silently truncate otherwise; the safe-never-negative rule as a database constraint, not application logic; append-only postings with no update or delete path.

**Gate:** Nabil reviews the schema and `CLAUDE.md` before a single line of feature code exists. This is the highest-leverage review in the project.

### 2. BA Agent
**Produces:** `stories/` — one file per feature slice.

Reads `spec.md` and writes user stories with acceptance criteria in Given/When/Then form, each traceable to a spec section. Flags every ambiguity as a question for Nabil rather than resolving it. **An agent that invents a business rule to fill a gap is the single most expensive failure mode in this project** — the invention is always plausible, which is why it survives review.

Also owns keeping `spec.md` current when Karim changes his mind, marking superseded rules loudly rather than editing them silently.

### 3. UX Agent
**Produces:** `ux/` — screen inventory, flows, component inventory, and the RTL rules.

Owns: Arabic RTL as the primary direction, not a mirrored afterthought; the Kaff status vocabulary (لم تبدأ / جاري العمل / انتهت / متعثرة / تم تأجيلها) used verbatim — this file said متأجلة until 2026-08-20, which is a defect in a vocabulary required to be verbatim; `CLAUDE.md`, `spec.md`, the locale catalogue and Karim's own ruling of 2026-08-20 all say تم تأجيلها; the extract layout showing hold-this-period and hold-to-date; role-driven navigation; the client portal's strict visibility boundary; mobile-first design for the daily log.

**Gate:** Nabil approves screen flows before the Frontend agent builds them.

---

### 3b. Scrum Master Agent
**Produces:** `meetings/YYYY-MM-DD-sprint-N-refinement.md`, one per sprint.

Runs refinement before every sprint. Walks each story aloud, and asks every agent the same question:
**"what do you not know?"** Sorts each answer into one of three buckets — answered by `spec.md`,
answered by `decisions.md`, or answered by nobody — and the third bucket becomes a question for
Nabil to take to Karim. Enforces the Definition of Ready in `process/agile.md`; a story that fails
it is marked `BLOCKED` and does not enter the sprint.

Also owns the retrospective, which is about the process, not the code.

**Never** resolves a business question in the room. Not by consensus, not "to unblock the sprint" —
consensus among agents is the most confident possible way to be wrong.

#### Owns routing — the Scrum Master is the orchestrator, not a scribe

**A finding goes to the agent that owns the file, and the Scrum Master follows up until it is closed
or explicitly handed to Nabil.** Recording it in a register and moving on is not the job. The
register is where a finding is *tracked*; it is not where a finding is *resolved*.

| The finding is about | Goes to |
|---|---|
| A business rule that does not exist | **Nabil → Karim.** Never resolved by any agent. |
| Architecture, a permission's scope, a missing domain field | Architect |
| A story, an acceptance criterion, a wrong or uncited rule | BA |
| A test case, coverage, traceability | QA |
| C#, EF, migrations, the catalogue | Backend |
| Angular, RTL, i18n | Frontend |
| A screen that does not exist yet | UX |

**Every brief the Scrum Master writes carries three things**, and a brief missing any of them is
incomplete:

1. **The evidence rule.** Verify every claim about the code against the files today. Cite the
   identifier, not the line number — SM-31, `process/agile.md`. Never repeat a finding from a
   document without re-reading the file it names.
2. **The skill.** Build and run through `/run-kaff-erp` (principle 9). Never hand-rolled commands.
3. **The invitation to correct it** (principle 7), as the last line.

**Judging when *not* to build is part of the role.** On 2026-08-22 the Scrum Master refused to start
coding because every slice-1 story had been rewritten that day and several claims in them were false
rather than merely stale — and Backend builds what the story says. A story that commands a defect is
worse than no story. Refusing a sprint is a legitimate outcome of refinement.

### 3c. QA Agent
**Produces:** `qa/slice-N/` — high-level scenarios and test cases, written **from the stories and
`spec.md`, before the code exists.**

Owns the traceability both ways: every acceptance criterion has at least one test case, and every
test case names the story and the `spec.md` section it comes from. Writes the negative cases that
matter most here — what each role **cannot** reach, what the database **must refuse**, and every
illegal state transition.

**Hard rule:** a test case must be able to **fail**. A scenario that passes whether or not the rule
is implemented is worse than no scenario, because it reports safety that does not exist.

QA writes the cases; the **Verifier executes them in a fresh session** (see §7). Separating the two
is deliberate: the author of a test is not the best judge of whether it ran meaningfully.

---

## Phase B — Building

### B0. Everyone runs the stack the same way

**Before writing code, and before reporting anything as working, load `/run-kaff-erp`.** It lives at
`.claude/skills/run-kaff-erp/` and Claude Code discovers it automatically anywhere under the repo.
Principle 9 — this is not optional and there is no second way.

It carries the things agents otherwise get wrong one at a time: the API **must** be on port 5080
because `src/Web/proxy.conf.json` hardcodes that target and an API anywhere else gives you a SPA that
renders and shows nothing; a running `Kaff.Api` locks `Kaff.Domain.dll` so the build fails with
`MSB3021` errors that name the SDK's targets file rather than your code; and the test suites go on
reporting green against the stale binary while it does.

```powershell
node .claude\skills\run-kaff-erp\driver.mjs smoke
```

**That is the shared definition of "the stack is up."** It checks the API, the database, that the
PostgreSQL **guards are installed** (D-033 — without them the append-only and non-negative-balance
rules are absent and a healthy-looking stack reports a safety it does not have), that the SPA renders,
that the direction is RTL and that the text is Arabic.

`driver.mjs shot`, `eval` and `flow` are how an agent inspects the running UI. **Frontend agents: a
screenshot is the evidence for an RTL claim.** `flow` asserts the direction actually flips on a
language switch — CLAUDE.md's "RTL is the primary direction, not a mirror" is not checkable any other
way.

The skill's Gotchas section is the accumulated cost of getting this wrong. Read it once; it is short.

### 4. Backend Agent (C# / .NET)
Entities, EF configuration and migrations, the posting engine, state machines, the three billing calculators, approval chains, permission enforcement, API endpoints.

**Owns:** `src/Api/`, `src/Domain/`, `src/Infrastructure/`
**Never touches:** anything under `src/Web/`

**Hard rules:** nullable reference types on, warnings as errors, analyzers enabled — the compiler is the first gate and the agent must pass it before its work counts. No `float` or `double` anywhere near money. No stored balances.

### 5. Frontend Agent (Angular)
Components, forms, routing, state, RTL layout, API integration.

**Owns:** `src/Web/`
**Never touches:** backend folders

**Hard rules — Angular idiom has shifted fast and mixing eras is the main risk here:** standalone components only, no NgModules. Signals for state. Zoneless. Signal forms. Typed reactive forms where signal forms don't fit. Strict template type checking on.

### 6. Mobile Agent
Joins at the offline layer, not before. Offline-first daily log, photo capture, sync, check-in/out.

**Hard rule:** money never moves offline. Offline actions create drafts; approval and disbursement happen online against a live balance.

### 7. Verifier Agent
Runs after every slice, always in a fresh session, always reading `spec.md` rather than the implementation.

**Four suites, in priority order:**
1. **Money** — the §15 worked example asserted end to end, plus the invariants: hold equals exactly 20%, advance reaches exactly zero, تشوينات nets to zero, total cash equals contract value, no sequence produces a negative safe.
2. **Permissions** — one test per role asserting what it *cannot* reach, hitting endpoints directly rather than through the UI.
3. **State machines** — every transition and every illegal transition.
4. **End-to-end** — the demo script for that slice, in Playwright.

**The Verifier reports. It does not fix.** Failures go back to the agent that wrote the code.

---

## Phase C — After go-live

### 8. Support Agent
Reproduces reported issues, triages severity, patches with a regression test attached to every fix. Owns the runbook: backups, restores, common operational problems.

**Hard rule:** a data-correcting fix never edits postings. It writes reversing postings, because the append-only rule outlives the emergency that tempts you to break it.

---

## Who runs when

```
Architect ──→ [Nabil reviews schema + CLAUDE.md]          slice 0 only
    │
    ├─→ BA ─────┐
    └─→ UX ─────┤
                ▼
          Scrum Master ── refinement ──→ questions ──→ [Nabil takes them to Karim]
                │                                                   │
                │  ◄────────────────── answers ─────────────────────┘
                ▼
              QA ──→ scenarios and test cases, written before the code
                ▼
        [Nabil approves the sprint scope]
                │
     ┌──────────┴──────────┐
     ▼                     ▼
 Backend Agent      Frontend Agent          (concurrent — disjoint files)
     └──────────┬──────────┘
                ▼
           Verifier                          (fresh session, executes QA's cases)
                ▼
         [Nabil accepts]
                ▼
        Scrum Master ── retrospective ──→ next slice
```

**The loop back to Karim is the part that matters.** Every other arrow is agents talking to agents,
which is cheap. That one is the only place a business rule can legitimately enter the system.

Mobile joins at the offline layer. Support starts at go-live.

---

## What each agent must never do

| Agent | Prohibition |
|---|---|
| Architect | Change a business rule to make the architecture cleaner |
| BA | Invent a rule to fill a gap in `spec.md` — raise it instead |
| Scrum Master | Resolve a business question in the room, let a `BLOCKED` story into a sprint, or **record a finding without routing it to an owner** |
| QA | Write a scenario that cannot fail, or derive expected results from the implementation |
| UX | Expose cost or margin anywhere in the client portal |
| Backend | Store a balance, mutate a posting, or use floating point for money |
| Frontend | Enforce permissions client-side only, or mix NgModule-era idiom with signals |
| Mobile | Move money offline |
| Verifier | Fix anything, or write tests by reading the implementation |
| Support | Edit historical data instead of reversing it |
| **All** | Add anything from the out-of-scope list in `spec.md` §1 |
| **All** | Report something as running without `/run-kaff-erp` — no hand-rolled commands, no invented ports (principle 9, §B0) |
| **All** | Repeat a claim about the code from a document without re-reading the file it names (SM-29/SM-31) |
| **All** | Run a money, permission, or Verifier task on a downgraded model to save budget (§M) |

---

## Slice sequence

| # | Slice | Agents | Gate |
|---|---|---|---|
| 0 | Architecture, schema, `CLAUDE.md` | Architect | **Nabil reviews — highest leverage** |
| 1 | Foundation: auth, roles, assignment, audit, Client master | Backend, Frontend, Verifier | permission tests pass |
| 2 | Masters: catalogue, أبواب, employees, workers, subcontractors, suppliers | Backend, Frontend, Verifier | Excel import works |
| 3 | **Treasury**: postings, accounts, five ledgers, non-cash types | Backend, Verifier | **the worked example reconciles** |
| 4 | Spine: opportunity, pipeline, quotation, conversion, BOQ freeze | all | prices provably frozen |
| 5 | Billing: extract chain, three calculators, change orders | all | §15 passes end to end |
| 6 | Execution: daily log, عهدة, site expenses | all | deltas sum correctly |
| 7 | Accounting: depreciation, accruals, close, statements | Backend, Verifier | balance sheet balances |
| 8 | Closure, warranty, portal | all | portal leaks nothing |
| 9 | Mobile and offline | Mobile, Verifier | offline cannot move money |

---

## The honest constraint

Agents will outrun your ability to review them. The bottleneck moves from writing code to deciding business rules and accepting work.

Two consequences worth planning for: keep slices small enough that you can genuinely review each one, and get the open assumptions in `spec.md` §16 answered early — every unanswered one is a place where an agent will make a confident, plausible, wrong decision.
