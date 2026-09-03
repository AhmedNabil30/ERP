# Sprint 3 refinement — slice 1's Client master, at last

**Scrum Master, 2026-09-04.** Sprint 2 closed this morning with
`qa/slice-1/verification-2026-09-04.md`. This ceremony opens sprint 3.

**Nabil's standing instruction today: *"always go with the plan."*** He challenged the board — *"we
already had a roadmap, why are we not moving according to the plan?"* — and he was right. This
meeting is the answer to that challenge, so it starts with the arithmetic rather than with a plan.

---

## 0. Why this ceremony exists — the count nobody had made

**`agents.md`'s slice sequence defines slice 1 as "Foundation: auth, roles, assignment, audit,
*Client master*."** Sprint 1 committed 15 of slice 1's 27 stories and deferred 10, **and the deferred
ten included every client story — KAFF-119, 120, 121, 123, 124.** The Client master was never picked
back up.

Nine days then went into verification and repair loops. **They found a real defect on every single
run** — that is not a criticism of them and the defects were real. But **they advanced no slice**, and
nobody said so out loud until Nabil asked. The Verifier finally named it in
`qa/slice-1/verification-2026-09-04.md` §8: *"Sprint 2 is closing with a slice-1 deliverable unbuilt —
that is a scope fact for Nabil, not a defect in any of these four commits."*

### Two corrections to my own framing, before anything else

**1. Projects are slice 4, not slice 1, and I was wrong to call their absence the largest gap.** No
slice-1 story creates or edits a project. The missing `POST /api/projects` is **the plan working as
written**, not a hole in it. Nobody is to build a project endpoint to make a demo look fuller —
`KAFF-122` and `KAFF-120` both already name that temptation and refuse it.

**2. The Client master is the real slice-1 gap, and it also gates slice 4.** `Project.Create` requires
a `ClientId`, so `POST /api/projects` on its own would unblock nothing. The client stories are
therefore both the correct next work *on the plan's own terms* and the unblocker for the slice after.

---

## 1. Before the ceremony — `V-32-A`, fixed first

The Verifier's one HIGH finding was fixed before any refinement ran, because it is a guarantee about
money and slice 3 is Treasury.

`AC-105b-F`'s anti-leak guarantee was a whitelist for HR's type and a **seven-word blocklist** for the
staff type. `Amount`, `Total`, `Price`, `Rate`, `Retention`, `Hold` and `Advance` were on none of
them — **and several of those are `spec.md` §14's own mandated vocabulary**, so the terminology
`CLAUDE.md` requires everyone to use was disproportionately the terminology the guard could not see.

**Watched failing in the order that proves a fix rather than asserts one:**

| Step | Api suite |
|---|---|
| Reproduce — a `decimal RetainedAmount` on `ProjectEntry`, blocklist unchanged | **241 / 241 green.** The defect |
| Fix applied, mutation still present | **240 / 241, one red**, the message naming `RetainedAmount` |
| Mutation reverted | **241 / 241 green** |

Recorded as **D-106**. The staff type is now pinned to its exact allowed surface, the same shape HR's
half already used ten lines above.

---

## 2. The ceremony — "what do you not know?"

`agents.md` principle 6: the Scrum Master's job here **is not to plan — it is to make each agent say
what it does not know.** *A refinement that produces no questions has not been run properly.*

Four agents were asked. Per §M, Backend, Frontend and QA ran on the **mid model** (reading stories
against code they can check), and the Architect on the **strongest** (its answers are unbackfillable).
All four were read-only; none touched the machine.

### Bucket 1 — answered by `spec.md`

* Client ownership and phone deduplication — §2 and its 2026-08-21 amendment.
* Codes generated, sequential, `C-10001`, never typed and never edited — §2 amendment.
* The duplicate is a **warning, not a refusal**, and does not block the save — §2 amendment.
* Withholding belongs to the **contract**, not the client; *"individual clients do not withhold"* is
  unchanged and now enforced in two places — §6.7 amendment.
* The portal boundary is absolute: a client must never see another client's data — §12.
* No stored balance, no money on the client record — §6.1 and `CLAUDE.md`.

**QA re-read every cited section today and every business-rule row in all five stories cites
correctly.** That is worth saying plainly, because it is the first time in this project a refinement
has found the *rules* clean.

### Bucket 2 — answered by `decisions.md`

* `ClientManage`, `CompanyWide`, Owner and Marketing — **D-044 ruling 4**, and the row is live
  [Verified: 2026-09-04 @ `src/Domain/Authorization/PermissionCatalogue.cs` -> `Permission.ClientManage`].
* The phone unique index reversed to non-unique, and *"matching harder matters more now"* — **D-049
  ruling 8**.
* Withholding moved to the contract, and Finance not Marketing sets it — **D-049 rulings 9 and 10**.
* The three-way split of the project permission — **D-055 §§1–3**.
* Code generation is slice 1's work; the mechanism is **N6**, tracked as open.

### Bucket 3 — answered by nobody

**This is the bucket the ceremony exists to produce.** It came back with seven items, and they sort
into three different owners — which matters, because routing them all to Karim would have been the
lazy and wrong answer.

| # | The gap | Owner |
|---|---|---|
| 1 | The client-code generator's **mechanism** — sequence, counter row, or retry | **Architect** (N6) |
| 2 | Whether **gaps in the client-code sequence** are acceptable to Kaff. A sequence is non-transactional, so a rolled-back insert burns a number and `C-10002` never exists. A code is a reference on extracts and ledgers | **Karim** — new, and nobody has established it |
| 3 | The duplicate-warning **API contract** — propose-then-confirm over a stateless API, two calls or one call twice | **Architect** |
| 4 | When the phone search returns **more than one** match, must the acknowledgement name *which* client was accepted? `AC-119-C` and `AC-124-B` both make multiple matches normal | **Architect**, with a business half |
| 5 | Whether `AC-119-E`'s *"names the client it matched"* is satisfied by free text in `AuditRecord.Reason`, or needs a **structured, queryable column**. `audit_records` is append-only by trigger, so a column added later **cannot be backfilled** | **Architect**, with a business half for Karim |
| 6 | `AC-119-B`'s *"ignored or refused"* — **two different behaviours, and a test cannot assert both.** The business rule is settled (a supplied code is never stored); only the wire shape is open. `ux/slice-1-flows.md` repeats the same ambiguous phrase verbatim rather than resolving it | **BA / Architect** — *not* Karim |
| 7 | Five narrow UX gaps: the 390px layout of the **three-action** duplicate dialog (`ux/components.md` §13 specifies it for desktop only, and explicitly says it is **not** `kaff-confirm-dialog`, so its mobile sheet rule cannot be borrowed); which control renders `ClientKind`; list pagination; whether the edit form's duplicate check fires on blur or on submit; whether changing kind to `Individual` clears the tax-registration field client-side or waits for the server's refusal | **UX** |

**Item 5 is the one with a deadline.** It is cheap now and unbackfillable once client rows exist. It
does not block the *build* — free text satisfies `AC-119-E` as literally written, and
`DeactivateUser` already uses `SetReason` exactly that way — but it must be answered before the
endpoint carries production data.

**Item 2 is the only genuinely new question for Karim**, and it is one sentence.

### What the ceremony found that I did not expect

**The `ux/` documents answer far more than I assumed.** I briefed Frontend expecting the
duplicate-phone warning to be largely uninvented. It is not: `ux/components.md` §13 defines
`kaff-duplicate-warning` in full — its inputs, its three actions, which is primary, that Cancel
preserves form state, that nothing in the dialog may mutate the matched record, and that **the
comparison is server-side only**. `ux/rtl-and-i18n.md` §§2–4 settle the bidi question completely:
`<bdi>` for a standalone rendered value, `dir="ltr"` on code and phone inputs, and `I18nService.t()`
already isolating interpolated values. **Neither was a gap. Only the mobile shape of that one dialog
is.**

---

## 3. Definition of Ready — the audit, and it is where the ceremony earned its keep

`process/agile.md`'s checklist, all twelve lines, walked against all five stories.

| Story | Verdict |
|---|---|
| **KAFF-119** | **`BLOCKED`** — DoR line 9 |
| **KAFF-120** | **`BLOCKED`** — DoR lines 9 **and** 11 |
| **KAFF-121** | Passes all twelve **on its own account** |
| **KAFF-123** | Passes all twelve **on its own account** |
| **KAFF-124** | Passes all twelve **on its own account** |

### DoR line 11 — three citations that are wrong today, inside a story marked `Ready`

`KAFF-120`'s *"Not in this story"* section cites three permission rows as `` (`:213-215`) ``,
`` (`:200-202`) `` and `` (`:238-241`) `` — the bare line-number form **SM-31 retired in every
position, as the claim and as a hint after it.** I re-read the catalogue myself rather than take the
finding:

| The story says | Where the row is | What the cited line actually holds |
|---|---|---|
| `ProjectCreate` at `:213-215` | `Permission.ProjectCreate` | the **continuation of `ProjectManage`** — a different permission |
| `ProjectManage` at `:200-202` | `Permission.ProjectManage` | mid-sentence in a comment about a merge risk |
| `ProjectFinancialsEdit` at `:238-241` | `Permission.ProjectFinancialsEdit` | mid-sentence in a comment about scope reasoning |

All three wrong, all three live, in a story the board called `Ready`. **This is `V-32-C`'s shape one
level up:** `scripts/check-citations.ps1`'s legacy pattern requires a filename before the colon, and
these have none — **so the checker is structurally blind to them and reported 0 legacy while three sat
in front of it.** That is the third distinct blind spot found in that one script in two days.

### DoR line 9 — four criteria with no scenario that can fail

`AC-119-J`, `AC-119-K`, `AC-119-L` and `AC-120-B` have **zero** test-case citations in
`qa/slice-1/test-cases.md`. I verified each count myself. `AC-119-L` is the create form's Arabic RTL
case — the **edit** form and the **list** both have one and the create form does not.

### And four existing cases are written in the shape that just failed

`TC-1-156`, `TC-1-157`, `TC-1-173`, `TC-1-183` and `TC-1-190` are all absence assertions worded as
*"then none is a balance, a credit limit, or any other money value"* — **a blocklist of named things.**
That is precisely `V-32-A`, which was fixed this morning after a money field walked past a green
241/241. They are being rewritten as **enumerate-and-pin allow-lists**, against the two working
precedents this repo now has.

### The transitive computation, which is the part that has been got wrong here before

`KAFF-121`, `KAFF-123` and `KAFF-124` each declare **`Depends on: KAFF-119`**, and KAFF-119 is
`BLOCKED`. `backlog.md` records the cost of missing this once already — **F-21**, *"six `Ready`
stories depended on a `BLOCKED` one"* — and warns that *"the risk is not that the sprint stalls, it
is that somebody unblocks it cheaply."*

> **Ruling: all five client stories are `BLOCKED` today.** Two on their own account, three
> transitively. **None enters a sprint until the repairs land.**

**The repairs need no ruling from anybody**, which is exactly why they are cheap: QA writes four test
cases, the BA repoints three citations. Both were routed the moment the audit finished. **A `BLOCKED`
verdict here is hours of work, not a stalled sprint** — and saying so is not the same as waiving it.

---

## 4. `KAFF-120` is smaller than the board thinks, and `KAFF-122` stays superseded

**`KAFF-122` is `Superseded` and is not to be built or re-created in slice 1.** The withholding rate
moved off the client onto the contract (D-049 ruling 9); its 3 points went to `KAFF-416` in slice 4.

**I asked whether `KAFF-120` was stale in the same way. It is not, and that framing of mine was
wrong.** `KAFF-120` was already rewritten around ruling 9 — its subject is the *refusal*, on the
contract and on the client, and `spec.md` §6.7's amendment says in terms that *"individual clients do
not withhold"* is **unchanged**.

But its **remaining work is nearly nothing of its own.** Six domain tests already exist
[Verified: 2026-09-04 @ `tests/Domain.Tests/WithholdingTests.cs` ->
`A_contract_for_an_individual_client_cannot_withhold`], and they fully discharge `AC-120-C`,
`AC-120-D`, `AC-120-E` and `AC-120-G`. `AC-120-H` is already true. What is left — `AC-120-A`,
`AC-120-B`, `AC-120-F` — **rides entirely on the create and edit endpoints KAFF-119 and KAFF-121 must
build anyway.**

**Its 2 points should be re-examined at the next estimation**, and I am not re-estimating it here
because estimates move at refinement with the team present, not by a Scrum Master's arithmetic between
ceremonies.

---

## 5. Corrections returned to me — `agents.md` principle 7

Four agents, four corrections. Recorded because the last line of every brief invites them and because
the record of what a brief got wrong is how the next one gets better.

1. **The API has fourteen routes, not thirteen.** My brief inherited the Verifier's §8 list, which
   omitted `GET /api/setup`. Counted directly from every `Map*` call in `src/Api/Features`. The
   load-bearing half — **none of them mentions a client** — holds.
2. **`KAFF-120` is not stale in `KAFF-122`'s way.** §4 above. My brief asked the question; the answer
   is no, and the story is current.
3. **`AC-119-E` does not need an unbackfillable schema field to work.** I framed it as possibly
   needing one. `AuditRecord.Reason` is free text and `DeactivateUser` already uses `SetReason` for
   exactly this kind of fact. The urgency is real but its shape changed: it is a question about
   future *queryability*, not about whether the criterion can be met.
4. **`V-32-A`'s fix was not "one line".** My own brief said so. It is one assertion replacing five.
   The diagnosis, the location and the remedy were all right; only the count was off.

**And one thing I checked and found correct rather than wrong:** the Verifier's `V-32-B` and `V-32-C`
findings both reproduced exactly, including that the checker resolves an identifier by plain substring
and so keeps a renamed test's citations green forever.

---

## 6. Questions standing with Nabil

**None of these is any agent's to answer, and none was answered here.**

| # | Question | Blocks |
|---|---|---|
| 1 | **`KAFF-118`'s cut.** Unbuilt, and it depends on `KAFF-119`. *"Move the board"* is not a ruling on scope | Nothing today |
| 2 | **Q56** — a staff member who becomes a subcontractor while holding a working login | Nothing in slice 1 |
| 3 | **The `mustChangePassword` reach.** `AC-106-H` and `AC-105a-C` contradict each other in committed text | Nothing today |
| 4 | **Q54's retention period** for audit rows carrying a personal IP address — Karim's | Nothing today |
| 5 | **`AC-125-C`.** The Verifier **explicitly did not accept it as satisfied**. `ux/screen-inventory.md`'s S-005 and the criterion now require opposite things and no implementation can satisfy both. **His criterion, his call** — and it *"should not stay open into slice 4"* | KAFF-125's acceptance |
| 6 | **Q39** — when a duplicate match names an **archived** client, is showing them enough, or should the system offer to bring them back? | Nothing — there is no unarchive path in slice 1 either way |
| 7 | **NEW — may the client-code sequence contain gaps?** Bucket-3 item 2. One sentence for Karim | Nothing — the Architect can pick a mechanism and a later change is a migration, not a rebuild |

---

## 7. Housekeeping — folded in, not made a project of

The Verifier's §15 listed 18 cleanup items. The document and code ones were done in one sweep on a
small model and are recorded in the commit; **nothing was made a project of.**

Ten SM-33 citations repointed, twenty orphaned i18n entries removed, the `UserRead` row's two drifted
`file:line` citations converted, and `ux/navigation.md`'s stale `mustChangePassword` paragraph amended
after being flagged by D-091, D-100 **and** D-104 without moving.

**Two i18n sets were deliberately kept.** The five `status.kaff.*` keys are reserved by `ar.json`'s own
`_note` and by `CLAUDE.md`'s verbatim vocabulary. The four `audit.grant.*` keys **are** orphaned — I
confirmed it, where the Verifier had flagged it unverified, and they duplicate the values of
`enum.ProjectAccessPath.*` — but deleting them is a judgement about whether `KAFF-117` needs them, and
a judgement is not a sweep's to make. **Routed to whoever builds KAFF-117.**

### The databases — enumerated, and dropped by nobody

**Not one was touched.** `kaff` is unbootable and its repair needs surgery `CLAUDE.md` forbids. Listed
for Nabil, ~78 MB in total:

| Database | Note |
|---|---|
| `kaff_test_0…3241`, `…3533`, `…43cb` ×2, `…4ca5` ×2, `…5026` ×2 | **Eight** abandoned fixtures. The fixture drops its own, so these are crashed runs |
| `kaff_v30`, `kaff_design_time` | Leftovers, referenced by nothing |
| `kaff` | Still carries `V-31-A`'s `PROBE-UNFLOORED` account; `/api/health` reports `503 degraded`. **Needs a ruling, not a cleanup** |
| `kaff_verify` | `ck_babs_not_own_parent` drift, and leftover accounts with unrecorded passwords |
| `kaff_demo` | Live, and the demo runs against it |

---

## 8. What this ceremony did **not** do

*Silence must never read as success.*

1. **Did not resolve a single business question.** Seven stand with Nabil, one of them new.
2. **Did not build any client endpoint.** All five stories are `BLOCKED`; `agents.md` §3b forbids
   letting a `BLOCKED` story into a sprint, and the Architect's N6 answer is a precondition either way.
3. **Did not re-estimate `KAFF-120`**, though §4 shows its 2 points are probably wrong now. Estimates
   move at refinement with the team, not by arithmetic between ceremonies.
4. **Did not verify anything against a running stack.** No screenshot, no smoke run, no E2E execution
   in this session — the suites were run, the app was not. Every UI claim here is read, not observed,
   and is labelled as such.
5. **Did not audit the remaining ~150 i18n keys for orphans.** Two blocks were checked because a
   commit touched them; others may exist.
6. **Did not fix `scripts/check-citations.ps1`**, which now has three known blind spots: it never
   opens a non-`.md` file, it resolves an identifier by plain substring, and it cannot see a bare
   `:digits` hint. It is a backlog item, not a sweep.
7. **Did not drop a database, and did not touch `kaff`.**

---

**Report anything in this record that was wrong.** Four corrections came back into it from four
briefed agents today, two of which changed something I had already written down.
