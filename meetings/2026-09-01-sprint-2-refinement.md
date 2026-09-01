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

---

## 3. Definition of Ready — the candidate set, story by story

`process/agile.md`: *a story that fails the Definition of Ready is marked `BLOCKED` and does not enter
the sprint.* Checked out loud, per story, against the files today.

**Verdict first, because it is not the comfortable one: neither candidate story is Ready.**

| Story | Proposed | Verdict | Points |
|---|---|---|---|
| **KAFF-105b** | sprint 2, item 1 | **BLOCKED** — six DoR failures, one of them Karim's | **3 → 5** |
| **KAFF-115** | sprint 2, item 3 | **BLOCKED** — transitively, and on its own account | **3 → 8** |
| **The staff shell** | sprint 2, item 2 | **Not a story.** Cannot be Ready or BLOCKED; it does not exist | unknown, and §3.1 says why |
| **KAFF-118** | carried | **BLOCKED**, unchanged — depends on KAFF-119, deferred out of the sprint. Its cut is Nabil's and he has not ruled | 3 |

### 3.1 The staff shell — the hole is larger than the three readings on the table

`meetings/2026-08-30-sprint-2-open.md` §4.2 costed three readings for `AC-101b-A`: grow KAFF-105b to 8,
write a shell story, or re-defer the criterion. **All three understate it, and the ceremony found why.**

`ux/navigation.md` -> `Landing summary` names a slice-1 landing for every role. Here is what each one
needs, established today:

| Role | Lands on | Story that builds it | Endpoint it reads |
|---|---|---|---|
| Owner | S-006 User list | **none** | **none — there is no list-users route** |
| Finance, TechnicalOffice, SiteEngineer, HeadOfDesign | S-005 My profile | **none** | `/api/auth/me` ✅ |
| MarketingSales | S-011 Client list | **none** — clients are KAFF-119…124, deferred out of sprint 1 entirely | **none** |
| Hr | S-009a HR project list | **none** — KAFF-115 builds **S-009b**, one project's team | **none** |

**No file under `stories/` builds S-004, S-005, S-009a or S-011** [Verified: 2026-09-01 — searched
`stories/` for each screen id; only KAFF-110 so much as mentions S-006, and it does not build it].

And the API cannot feed them. **The entire application exposes three GET routes** — `/api/auth/me`,
`/api/health` and `/api/setup` [Verified: 2026-09-01 @ `src/Api/Features/Auth/WhoAmI/Endpoint.cs` ->
`MapGet`; @ `src/Api/Features/Health/GetHealth/Endpoint.cs` -> `MapGet`; @
`src/Api/Features/Setup/GetSetupAvailability/Endpoint.cs` -> `MapGet`]. There is no route that lists
users, projects, clients or a project's team. **Three of the four landings have no data to render.**

**So growing KAFF-105b to 8 does not produce a shell.** It produces a payload and a chrome that lands
five of the nine roles on a blank page. That is the fourth reading, and nobody had costed it because
nobody had checked what the landings needed.

**And the same arithmetic failure applies to `AC-101b-D`, which every document so far has treated as
safe.** `AC-101b-D` requires HR to land on *"the Project Team surface"*. UX makes HR's landing
**S-009a, the project *list***; KAFF-115 builds **S-009b, one project's team panel** — every one of its
criteria is per-project, and `AC-115-H` opens *"the Project Team screen **for project A**"*
[Verified: 2026-09-01 @ `stories/slice-1-foundation/KAFF-115-project-team-panel.md` -> `AC-115-H`].
**`AC-101b-D` is deferred onto a story that does not build the screen it lands on, exactly as
`AC-101b-A` is.** The sprint-2 opening found that failure once. It happens twice, and the second one
was invisible until this ceremony.

**This is a scope decision and it is Nabil's.** I am not picking a reading, and I am not proposing the
shell for this sprint — see §5.

### 3.2 KAFF-105b — BLOCKED, on six lines

| # | DoR line | Failure |
|---|---|---|
| 1 | **No rule in the story is uncited. An uncited rule is a question for Karim, not a story** · **Not `BLOCKED` on an open question** | **Rule 6 and `AC-105b-C` assert HR receives the project *code*.** Both cite **D-051 (Q32)**, which grants *"the project name and the list of assigned engineers"* and **says nothing about a code**. The citation does not support the rule. The question is **`Q43`, registered and open with Karim** [Verified: 2026-09-01 @ `stories/questions-for-karim.md` -> the `Q43` row]. **The story bakes an unasked answer into a criterion and a test.** This one cannot be fixed by any agent |
| 2 | **QA has written at least one scenario that fails if the rule is broken** | **`AC-105b-F` cannot fail.** *"When the code is read, Then they are different types"* is a manual code-review instruction wearing Given/When/Then clothing. QA's own hard rule: a scenario that passes whether or not the rule is implemented is worse than none, because it reports safety that does not exist |
| 3 | Money behaviour named explicitly | **`AC-105b-C`'s given cannot be constructed.** It names projects with *"budgets, balances and contract values set"*. **`Budget` exists nowhere in `src/Domain/`** — it is a slice-7 concept (KAFF-709). **A stored balance is forbidden outright** by `CLAUDE.md`. Only `ContractValue` is arrangeable [Verified: 2026-09-01 @ `src/Domain/Projects/Project.cs` -> `ContractValue`] |
| 4 | Every claim about the state of the code carries a date and a stable identifier | **`AC-105b-E` asserts a 403 from *"the project dashboard endpoint"*, which does not exist** — see §3.1. And its given is *"the HR payload from `AC-105b-C`"*, so it inherits failure 3 as well. The criterion is joined by "And", so the whole of it is unexecutable |
| 5 | Permissions named explicitly | **`AC-105b-G` asserts a response body the code cannot return.** Its Then is *"only X's project is named"* — a `200` payload — for a `Role.Client`, who is refused before the handler runs [Verified: 2026-09-01 @ `src/Domain/Identity/Role.cs` -> `MayHoldStaffSession`; @ `src/Api/Authorization/LiveSession.cs` -> `ResolveAsync`]. **The story's own rule 10 already says a client *"is not expected here at all"*, so the rule and the criterion disagree** |
| 6 | **If the story adds a permission catalogue row, the test that names it is written before the row is** — SM-30 | See the ruling below. The row cannot land without falsifying an existing test's **name** |

**On failure 5, the BA and QA disagreed and I settle it on the evidence rather than by consensus.** The
BA is right that the *given* is constructible — `TestAuthHandler` mints a principal from headers and
bypasses the staff door, so a defence-in-depth case is writable. **The BA is wrong about the *Then*.**
`AC-105b-G` asserts a filtered payload, and there is no payload: the door refuses first. The criterion
is correct in *intent* and wrong in *assertion*, and the fix is one line of the BA's — assert the
refusal, not the filter. **Note the asymmetry with `AC-115-G`**, which asserts *"refused with 403"* and
therefore **passes** — but passes **for the wrong reason**: it is refused at the staff door by
`MayHoldStaffSession`, not because *"`PortalRead` is not `ProjectRead`"* as the criterion claims. A
test that would stay green if the rule it names were deleted. **QA's, and it is the same defect class.**

**QA named that class, and it is the third sighting:** any given naming a signed-in `Role.Client` at a
staff-door endpoint is dead on arrival — `TC-1-021` (found as `V-26-E`), `TC-1-042` (found as `V-26-G`),
and now `AC-105b-G` / `AC-115-G`. **Recorded as one class with one rule**, not as four one-off fixes:
such a case is written against the evaluator (`MayHoldPermissions`, which does admit `Client`), never
against a staff session that can never exist.

#### Ruling SM-33 — a test name that states a count is a claim, and it is renamed in the change that falsifies it

**B3-9. Scrum Master's ruling, 2026-09-01. This is process, not business, so it is mine to make.**

Adding `ProjectTeamRead` for `Role.Hr` makes HR hold four permissions, which makes
`Hr_holds_exactly_three_permissions_and_none_touches_money` **false in its own name**
[Verified: 2026-09-01 @ `tests/Domain.Tests/CatalogueCompletenessTests.cs` ->
`Hr_holds_exactly_three_permissions_and_none_touches_money`]. That name is cited in **five** files of
record — `decisions.md`, `process/agile.md`, `qa/questions.md`,
`stories/slice-1-foundation/KAFF-107-hr-role-is-bound-to-the-hr-department.md` and
`proposals/N10-project-creation.md` [Verified: 2026-09-01 — searched each directory] — several of
which the implementing agent must not edit. *(The Architect reported eight; five are documents and the
remainder were compiled assemblies. Corrected here.)*

**D-095 met this trap and escaped by not renaming.** That was right there and is wrong here, and the
distinction is the ruling:

> **A name that is merely *narrow* stays. A name that is *false* is renamed in the same change that
> falsifies it.** `ValidateDepartment` still validates the department — narrow, and D-095 was right to
> keep it. `…exactly_three…` becomes a lie the moment the fourth grant lands, and **a false test name
> is worse than a stale one, because the name is what a reader takes for the assertion.**
>
> **The citations move in the same commit, and the Scrum Master moves the ones in `meetings/`, `qa/`
> and `proposals/`** — the implementing agent may not edit those, and that constraint is what made
> D-095 choose the other way. Historical records are corrected as marked amendments, never silently.
>
> **And the general rule, which is the cheaper half: a test name must not encode a count that a
> legitimate future change falsifies.** Assert the property in the name, the arithmetic in the body.
> `Hr_holds_no_permission_that_touches_money` cannot go false when HR is granted a fourth
> non-financial permission; the count belongs inside the test, where it fails loudly without lying.

### 3.3 KAFF-115 — BLOCKED, and re-estimated 3 → 8

1. **Transitively blocked.** It depends on KAFF-105b, which is BLOCKED, and KAFF-105b is where
   `ProjectTeamRead` is born. **F-21's warning applies exactly** — *"the risk is not that the sprint
   stalls, it is that somebody unblocks it cheaply"* — and the cheap unblock here is granting HR
   `ProjectRead`, which hands HR the project surface D-044 ruling 2 exists to remove.
2. **`AC-115-H` carries the same two defects as `AC-105b-C` and `AC-105b-E`** — a given naming a budget
   and a balance that cannot be set, and an assertion against a project dashboard endpoint that does
   not exist.
3. **`AC-115-I` cannot fail** — the same *"when the code is read"* shape as `AC-105b-F`.
4. **`AC-115-G` passes for the wrong reason** — §3.2.
5. **`AC-101b-D` is deferred onto it and it does not build S-009a** — §3.1.

**Re-estimated 3 → 8**, which the brief asked for and which the story's own header has been asking for
since 2026-08-30. `process/agile.md`'s table puts *"touches money or the permission model"* at **5** and
*"touches both, or spans backend and frontend"* at **8**. KAFF-115 does both at once: it births a
permission row, needs two distinct response types, an SM-30-bound test, a second route and component
per D-051 (Q32), and `AC-115-J`'s Arabic-RTL-at-390px criterion. **Take the higher, not the sum — 8.**
Frontend, asked independently and without being given a number, returned **8** with the same reasoning.
Not 13: the criteria are concrete enough that this is not *"a story nobody understands yet."*

### 3.4 KAFF-105b — re-estimated 3 → 5

**As written** — ten payload criteria, no rendering — it is not a frontend story, so 8 is wrong. But it
**births a permission row**, and `process/agile.md` puts *"touches the permission model"* at **5**.
The 3 was set on 2026-08-21 before `ProjectTeamRead` had a name.

**If Nabil takes the reading that grows it to carry the shell, the estimate is not 8 — the story should
be split instead**, because §3.1 shows the shell is not one story's worth of work and
`process/agile.md` calls a 13 *"too big — split it."*

### 3.5 What is genuinely Ready, and it is not nothing

Two things carried out of the sprint-2 opening are now closed, and closing them is real progress:

- **Item 0, the Verifier pass over the six unverified commits: DONE.** `qa/slice-1/verification-2026-08-30.md`
  — all six ACCEPT, none rejected, and the five lapsed acceptances re-established at `aa8a9ca`, each on
  a mutation watched failing rather than on the author's evidence.
- **Item 4, `AC-106-H` and `AC-110-D`: both DISCHARGED** by that same pass. They had been deferred to
  stories that did not exist, and no pass had ever examined them.

**KAFF-106 and KAFF-110 remain *not accepted* as stories** — only those two criteria were discharged.
That distinction is the verification report's own and it is kept.
