# Sprint 2 — refinement · 2026-09-01

**Scrum Master.** Slice 1 remainder into slice 2. `agents.md` §3b, `process/agile.md` ceremony 1.

**This ceremony was owed.** The sprint-1 close recorded it as a debt in its own words — *"No
refinement ceremony — no agent was asked 'what do you not know?' — one is owed before stories are
pulled."* Sprint 2 then executed for two days with no recorded scope. This is the ceremony, and it
runs before stories are pulled, not after.

**Nabil's instruction opening this run:** *"i fixed the staging and now working fine lets go to
sprint 2."*

**What this meeting produces:** a staging verdict established from the pipeline rather than from the
message; every agent's answer to *"what do you not know?"*, sorted into three buckets; a Definition
of Ready verdict per candidate story; and a **proposed** sprint 2 commitment. **Nabil locks scope.
Nothing here is a commitment.**

---

## 1. Staging — verified, not taken on trust

Nabil says staging is fixed and working. That is a claim about a machine, and on this project a claim
is not a verification. **D-096 exists because an acceptance was a claim about a tree that had moved.**

**Two claims, and the sprint-1 close was careful to keep them apart** — `meetings/2026-08-27-sprint-1-close.md`
§4: *"The application runs there. The pipeline cannot see it."*

| | Claim | Who establishes it |
|---|---|---|
| **a** | The application runs on staging | Nabil, on the box. He has almost certainly done this |
| **b** | **The CI smoke check reaches it and passes on its own** | The pipeline. **This is the Definition of Done line** |

Only **b** is tickable, and only the pipeline can tick it. So I asked the pipeline.

### The measurement

`.github/workflows/deploy-staging.yml` -> `Smoke check` curls `${{ vars.STAGING_URL }}/api/health`
from a GitHub runner — that is, from **outside both of the Oracle firewalls** `deploy/README.md` §4
describes — and greps for `"guardsInstalled":true`, retrying 30 times at 10-second intervals before
failing.

**The same commit, `dc76fe7`, which is HEAD, ran that check twice:**

| Attempt | When | Smoke check | Duration |
|---|---|---|---|
| 1 | 2026-08-30 04:56:47Z → 05:02:01Z | **failure** | 5m 14s — the full retry loop exhausted |
| 2 | 2026-08-30 22:40:59Z → 22:41:10Z | **success** | **11s** — it answered on the first or second curl |

**Nothing in the tree changed between them.** Same SHA, same workflow, same steps. The change was on
the machine, which is exactly what Nabil said he did.

**And the step genuinely ran rather than being gated away.** `Smoke check` carries
`if: vars.STAGING_URL != ''`; a false condition reports the step as `skipped` in zero seconds, not as
`success` in eleven. Attempt 1 closes this off completely: **the same step with the same condition
reported `failure`**, which an unset `STAGING_URL` could not have produced. The variable is set and
the check is live.

### What the passing check actually proves

More than it looks like, because of how staging is wired. In `deploy/docker-compose.staging.yml`,
**only the `web` service publishes a port**; `api` and `db` are `expose` only, reachable on the
internal network alone. So one external 200 carrying `guardsInstalled: true` proves the whole chain:

- a GitHub runner reached the box on port 80 — **both Oracle firewalls are open**, the VCN security
  list and the instance's own iptables REJECT that `deploy/README.md` §4 warns is *"the step that is
  easy to get half-right"*;
- nginx served, and its `KAFF_API_URL` prefix is right, or `/api/health` would arrive at the API as
  `/health` and answer 401;
- the API reached PostgreSQL — `databaseReachable`;
- **D-033's database guards are installed**, which is the field that matters. Without them the
  append-only and non-negative-balance rules are absent and a healthy-looking stack reports a safety
  it does not have.

### What it does not prove, and this is where the per-story answer comes from

**The CI smoke check is a curl against `/api/health`. It never fetches the SPA.** It says nothing
about a screen rendering, nothing about direction being RTL, and nothing about the text being Arabic.
Those are assertions of the eight-check browser smoke in `/run-kaff-erp`, which runs against a
**local** stack, not against staging.

So *"runs on staging"* becomes tickable **per story, by surface**:

| Story | Surface | *Runs on staging* | Why |
|---|---|---|---|
| KAFF-100, 101a, 102, 103, 105a, 106, 108, 109, 110, 111, 112, 113, 114, 116 | API only | ✅ **tickable** | Their surface is the API, and the API is proven reachable and healthy from outside, with guards installed, at HEAD |
| **KAFF-101b** (`f2b995b`) | The sign-in **screen** | ⬜ **not tickable** | The web image is built, pushed and deployed — `Pull and restart` succeeded — but **nothing fetches the page**. Deployed is not the same as rendering |
| **KAFF-103's screen** (`332c160`) | The change-password **screen** | ⬜ **not tickable** | Same reason |

**The honest summary: the line moved from "the pipeline cannot see it" to "the pipeline sees the API."**
That is most of the way, and it is the half that had been blocking every backend story. It is not the
whole line, and I am not ticking the frontend half on the strength of a deploy step that copies files.

### Routed

**The gap is small and it is one step, not a project.** A second smoke assertion — fetch `${STAGING_URL}/`
and check the served document carries `dir="rtl"` and `lang="ar"` — would close the frontend half of
this line for every screen from here on. It is a change to a workflow file and it is **Backend's**,
being CI rather than `src/Web/`. **Not done in this run and not smuggled into the sprint;** raised
here so the next session does not have to rediscover why two of the rows above are unticked.

### The Definition of Done statement is updated rather than left stale

`meetings/2026-08-27-sprint-1-close.md` §4 says of this line: *"⬜ The application runs there. The
pipeline cannot see it."* **That was true when written and is now false.** It is corrected in place,
loudly, per SM-29's own practice — the correction names what changed, what it does not cover, and the
date. Amended in this run.

### One brief correction of my own, under SM-31

The brief opening this session said `scripts/check-citations.ps1` stands at **960 checked**. Re-run
today, it reports **969 checked, 0 broken, 0 legacy**. The floor this ceremony must not regress below
is 969, not 960. Small, and recorded because a figure carried forward unchecked is how the other two
wrong facts in Scrum Master briefs got as far as they did (D-096 §4).

---

## 2. The ceremony — "what do you not know?"

**Six agents asked, six answered.** Architect and Backend on the strongest model (§M: never downgrade
anything that decides who may touch money or what the ledger records); BA, UX, QA and Frontend on mid.
Every brief carried the three things §3b requires — the evidence rule, the skill, and the invitation to
correct it. **Read-only throughout: no agent built, ran, or started the stack**, so the
one-agent-per-machine constraint was never contended (`process/agile.md` ceremony 2, amended
2026-08-30).

**The invitation earned its place again — four agents corrected me, and two of the corrections change
a verdict.** They are recorded first, because a ceremony that quietly absorbs its own errors is the
thing SM-29 exists to stop.

### 2.0 What was wrong in my brief

| # | I said | The truth, established today | Who caught it |
|---|---|---|---|
| 1 | *"Where each role lands is written down only for HR"* | **Wrong.** `ux/navigation.md` -> `Landing summary` gives a slice-1 landing for **all nine roles** — Owner to S-006, Finance / TechnicalOffice / SiteEngineer / HeadOfDesign to S-005, MarketingSales to S-011, Hr to S-009a | BA and UX, independently |
| 2 | *"`V-30-D` matters because slice 3's money constraints are 3 of the 30"* | **True, and it understates the exposure badly.** See §2.1 — the safe-balance rule is **not a check constraint at all** | Architect |
| 3 | *"The false unforgeable sentence stands in three places"* | **An undercount. At least six sentences, plus a test name** | Backend |
| 4 | *"`scripts/check-citations.ps1` walks `*.md` and `.json`"* | **It walks `*.md` only** for citations [Verified: 2026-09-01 @ `scripts/check-citations.ps1` -> `$docs`]; the other extensions appear only in the target index. So a citation *pointing at* `ar.json` is checked and one *written inside* `ar.json` is invisible. The blind spot is one line wider than `process/agile.md` records | Backend |
| 5 | *"960 checked"* | **969** — §1 | Me, on re-measuring |

**Three of these five came back because the brief asked for them.** That is `agents.md` principle 7
paying for itself for the third sprint running.

### 2.1 The finding that outranks everything else in this ceremony

**The rule `CLAUDE.md` puts in the database above all others — *"the safe balance can never go
negative, enforced by a database constraint, not application code"* — is verified by nobody, and it is
not one of the thirty things `V-30-D` measured.**

It is a **constraint trigger running a plpgsql function**
[Verified: 2026-09-01 @ `src/Infrastructure/Persistence/Sql/001_guards.sql` -> `kaff_check_non_negative_balance`],
registered in the **trigger** list rather than among the check constraints
[Verified: 2026-09-01 @ `src/Infrastructure/Persistence/DatabaseInitializer.cs` -> `trg_postings_non_negative_balance`].
`FindMissingGuardsAsync` checks triggers by `tgname`, indexes by `indexname`, the view by `viewname`
and constraints by `conname`. **Every guard class in this repository is verified by name only** — not
only the constraints `V-30-D` sampled.

And underneath it the Architect found a second layer no artefact in this project has ever recorded:
**which accounts are floored is data, not code.** The trigger reads `accounts.enforce_non_negative`,
and the guard file says in its own words that a database seeded before 2026-08-20 keeps the old floors.
`SchemaInvariantTests` asserts that flag on an `Account` constructed from metadata, not on any row in
the database. **A database whose Safe row carries `false` passes every guard check, passes every name
check, and runs a trigger that floors nothing.**

Harmless today: there is no `Posting`, no account set and no money. **Exactly wrong for slice 3**, whose
gate is *"the worked example reconciles"*. `V-30-D` asked *when* the name-level gap gets fixed. The
honest answer is that the question is larger than the finding that raised it.

**Routed: Architect as owner, with Backend — due before the first posting endpoint ships.** Not before
slice 9, and not "when somebody notices". It is not sprint-2 work and I am not proposing it as such.

### 2.2 The three buckets

`process/agile.md`: answered by `spec.md` · answered by `decisions.md` · **answered by nobody** — and
the third is the whole point of the meeting.

#### Bucket 1 — answered by `spec.md`, or by a document that already existed

Somebody had not read the section. Cite it in the story and move on.

| Question | Where it was already answered |
|---|---|
| Where does each role land after sign-in? | `ux/navigation.md` -> `Landing summary`, all nine roles. **The destinations were never the gap** |
| What is the staff shell, as a design? | `ux/navigation.md` -> `Shell shapes` — header, side navigation at inline-start, drawer from the right at 390px. Three shells, and the file says do not invent a fourth. The dispatcher even has a screen id: `ux/screen-inventory.md` -> `S-004` |
| Does the two-surface rule (D-051 Q32) contradict `ux/navigation.md`? | **No** — checked because I asked. HR shares the shell *chrome*, which carries no project data; the separation is at the route, API and response-type level. No defect |
| Does a shell force Q18's Arabic status vocabulary early? | **No.** `ux/components.md` §5 and the S-009a wireframe both forbid a status chip on any slice-1 screen |
| Is a `messageKey`-less 400 user-visible today? | **No.** `toProblem` falls back to `errors.unknown`, and that key exists in Arabic [Verified: 2026-09-01 @ `src/Web/src/app/core/api/problem-details.ts` -> `toProblem`] |
| `AC-105b-C`'s *"balances set"* | **Answered by `CLAUDE.md`, in the negative.** *"Never store a balance."* There is nothing to set and there never will be |

#### Bucket 2 — answered by `decisions.md`

| Question | Entry |
|---|---|
| May a `Role.Client` hold a staff session? | **D-062 §2** — *"strictly forbidden."* Implemented [Verified: 2026-09-01 @ `src/Domain/Identity/Role.cs` -> `MayHoldStaffSession`] and applied before any handler runs [Verified: 2026-09-01 @ `src/Api/Authorization/LiveSession.cs` -> `ResolveAsync`] |
| Is `mustChangePassword` a refusal, or a field on `/api/auth/me`? | **D-072 §2 — a field.** But `ux/navigation.md` still describes the *refusal* reading, and **`Q-UX-18` can now be closed against D-072 §2.** Routed to UX and BA: a shell story written against the stale reading is exactly how a story comes to command a defect |
| What is the retention *mechanism* for `audit_records`? | **D-072 §3** — partition by month, drop expired partitions |
| What form must the missing `45a939d` entry take? | **D-057 §4**, SM-29's *"Applied"* clause — work still outstanding goes under a *"Not done"* heading |

#### Bucket 3 — **answered by nobody.** This is the meeting's product

Nothing below is resolved here. Each is routed, and the ones marked **Karim** may not be answered by
any agent, by consensus, or to unblock a sprint.

| # | Question | Owner | Blocks |
|---|---|---|---|
| **B3-1** | **May HR see a project's *code*, and its team size?** D-051 (Q32) grants *"the project name and the list of assigned engineers"* and says nothing about a code. **Registered as `Q43`, still open** [Verified: 2026-09-01 @ `stories/questions-for-karim.md` -> the `Q43` row] | **Karim** | `AC-105b-C` and KAFF-105b rule 6 — §3.2 |
| **B3-2** | **What is the audit retention *period*?** D-072 §3 ruled the mechanism and **never gave a number**, and the original Q54 explicitly asked for one. Partitioning can be *built* without it; it cannot *drop* anything without it | **Karim** | Nothing today. N11, before slice 3 |
| **B3-3** | **Which story builds the staff shell, and what does it land on?** §3.1 — the hole is larger than the three readings on the table | **Nabil**, then BA | `AC-101b-A`, **and now `AC-101b-D`** |
| **B3-4** | **May a role change *to* `Role.Client` or `Role.Subcontractor` at all?** The Client half is `Q41`, open. **The Subcontractor half carries no `Q`-number anywhere in the register** — it lives only in D-088's prose and in Verifier reports | **Karim**; the BA must number the second half | `TC-1-079` |
| **B3-5** | **When a guard is present but *wrong*, does the host refuse to start?** D-033 answers only the *missing* case | **Architect** | Nothing in sprint 2. Slice 3 |
| **B3-6** | **Should global project reach become per-permission?** `EvaluateAsync` dispatches on `subject.Role` alone and is never handed a permission, so HR's global reach attaches automatically to **every** `ProjectScoped` permission HR is ever granted. Correct for `ProjectTeamRead`; a silent widening surface for the grant after it | **Architect** | Nothing. Decide before a fourth HR grant, not after |
| **B3-7** | **What is the API's refusal contract — which statuses carry a `messageKey`?** `W-5` cannot be ruled because there is no document to rule *into*. And **`413` is in scope and nobody has looked at it**: a 400 needs a client bug, but **a 413 is reachable by a site engineer photographing a wall in slice 6**, and nothing configures a limit | **Architect + UX** | Nothing in sprint 2 |
| **B3-8** | **Who holds the `GET /api/auth/me` result in the shell, and what invalidates it?** `AC-105b-I` requires the list to be empty *"on the next call"*; nothing states whether the shell resolves once per load, per navigation, or on a timer — and that decides whether a revoked assignment leaves the navigation in one second or one session | **Architect**, inside the shell story | `AC-105b-I` in practice |
| **B3-9** | **How is a test renamed when its name is the claim, and that name is cited in documents the implementing agent must not edit?** SM-31 says cite a stable identifier; it does not say what to do when the identifier stops being **true**. Mine, and I rule it in §3.2 | **Scrum Master** | KAFF-105b's permission row |
| **B3-10** | **Can `audit_records` be partitioned without weakening the guards that make it evidence?** The primary key is `Id` alone, so partitioning forces a composite key onto an append-only, trigger-protected table. And **a row arriving for a month with no partition fails the audit insert, which fails the business operation with it** — the system stops at midnight on the first of a month unless a `DEFAULT` partition exists | **Architect** | Nothing in sprint 2. Before slice 3 |

### 2.3 Three questions that need the machine, left unanswered on purpose

The ceremony forbade building. Three agents hit a wall and **said so rather than guessing**, which is
the correct behaviour and is recorded here so it is not mistaken for an answer:

1. **`V-30-G`** — whether the Api test host can run as `Development` without tripping the startup guard refusal. **Backend.**
2. **Whether comparing a checked-in expression against PostgreSQL's own re-printed definition is stable**, or false-positives on formatting alone. A checker that cries wolf gets muted, which is D-046's green light by another name. **Architect.**
3. **Whether a no-truncate statement trigger survives on a partitioned parent** in PostgreSQL 16. **Architect.**

**Serialised, not dropped.** Each needs one session with the machine to itself.
