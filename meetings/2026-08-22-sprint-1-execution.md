# Sprint 1 — execution log · 2026-08-22

**Run by:** Scrum Master · **Not a refinement.** No story was walked, no bucket-three question was
raised, nothing entered or left the sprint scope.

**Why a new file rather than a fourth addendum to `2026-08-21-sprint-1-refinement.md`.** That file is
880 lines, dated the 21st, and already carries three addenda plus a *"Sprint 1 — state for Nabil ·
2026-08-22"* section. **It has stopped being the minutes of a meeting and become a rolling log under a
date that is wrong for most of its contents** — which is the same staleness mechanism SM-29 was
adopted to fix, in the file where the fixing was decided. A dated record of one day's work stays
readable. One that accretes undated addenda goes stale invisibly.

---

## 1. What was verified, not reported

Run directly, exit codes checked before the results were trusted (D-046):

| | |
|---|---|
| Build, Release, warnings as errors | **0 errors, 0 warnings**, exit 0 |
| `dotnet format --verify-no-changes` | **clean**, exit 0 |
| Domain | **74 / 74** |
| Api, live PostgreSQL 16 | **43 / 43** |
| `scripts/check-citations.ps1` | **284 identifier citations, 0 broken, 101 legacy** |

**Not my verification, cited as the coordinator's.** The **E2E suite ran against a live stack for the
first time — 5/5 passed** (API on 5080, Angular dev server on 4200, `kaff-db` container), and Arabic
RTL was confirmed correct at 1280x900 with the English switch flipping the document to LTR.
**This changes one line of the Definition of Done from unverified to verified — the E2E half.**
*"Runs on staging"* is **still unverified**, and **CI has still never run**. Both remain open against
the slice-1 gate and neither was in this run's scope.

Re-run after every batch of edits, not once at the start.

---

## 2. Registers closed — D-055 §8's outstanding action

D-055 §8 required **Q17, Q42, Q-N10-2 and F-27** to be marked closed at their source. It had not been
done. **Done now**, with the D-number and the date, in `stories/questions-for-karim.md`,
`qa/questions.md` and `qa/slice-1/permission-matrix.md`.

The sweep closed more than it was asked to:

* **N10** — the residual of Q17, still carried as an open Architect decision. Closed by D-055 §3.
* **F-26** — the `SecurityStamp` global kill, marked *"declared and not implemented"* in two files.
  **Built on 2026-08-22** (D-053 §1) and found only by re-reading the source rather than the finding.
* **F-25's permission half** — `ProjectFinancialsEdit` is the permission that was missing. Its other
  half, the `ClientKind` the caller supplies, is untouched and is the half that was always the risk.

**Registered as newly open for Karim:** **Q-N10-2b**, and also **Q-N10-1** and **Q-N10-3**, which
D-055 §8 lists as open and which **had never reached the master register at all** — they lived only in
the proposal. Slice 4 is no longer blocked on a permission; it is blocked on those three workflow
questions.

---

## 3. Rulings

### SM-30 — adopted, amended · `decisions.md` D-057

A permission catalogue row and a test naming it land in the same change; the row's comment cites the
test **by a name that exists**.

The argument against was that the Definition of Done already covers it. **It does not: the DoD is a
slice gate that tests for red, and a row with no test makes nothing fail.** Three rows shipped
reachable and untested at 74/74 green.

**The amendment came from the rule's own first failures — two of them, both found on adoption day.**
The `ProjectManage` row cited a test that never existed; **D-056 §2, the entry proposing SM-30**, cited
a test renamed in the same run that wrote it. So the enforceable half is coverage, not prose, and
Backend owes one test that fails when a catalogue row is named in no test.

### SM-31 — adopted, then amended within the hour · `decisions.md` D-059

**Cite a stable identifier, not a position. The date stays.**

D-058's evidence was mechanical and I re-measured it rather than accepting it: **~68 citations into
`PermissionCatalogue.cs` across 30 distinct line numbers**, of which exactly one was right. `:258`
cited ten times — a blank line. `:238` cited sixteen times — mid-comment.

**One part decided against D-058's framing.** It left open whether SM-29's date survives. **It does.**
The date says *when the claim was checked* and nothing else carries that; the line number said *where*,
which an identifier says better and stably. Separable, and only one of them was doing harm.

**Then QA broke my own rule for me.** SM-31 first allowed a line number *"as a convenience hint"*. QA
measured the exemption instead of applying it: **77 bare hints repo-wide, invisible to the checker**,
decaying at the same rate as the claims they were exempted from. **SM-31 as I ruled it would have
reported green over 77 stale pointers** — D-058's finding reproduced inside D-058's own remedy, by me,
four hours after writing that this class is not fixed by asking people to be careful. **Exemption
withdrawn.**

---

## 4. What the agents delivered, and where I disagreed

Four agent runs. **Every one of them corrected me**, because every brief ended with an instruction to
report anything in it that did not survive contact with the files.

| Agent | Delivered | Where it corrected me |
|---|---|---|
| **QA** (relock) | 248 IDs reconciled, cases for the three new rows split into slice-1 catalogue/evaluator and slice-4 endpoint | My brief said 241 cases; there are 248. Found **F-30**: `ProjectTeamRead` named in four stories, **absent from `src/` entirely** |
| **BA** (stories) | Waiver applied, 153 verified citations added, false story claims corrected | D-055 §7 and my brief both said `ProjectAccessPath` has **three** grant paths. **It has four** — `PortalClient`. KAFF-116 was right all along |
| **QA** (migration) | `qa/` to zero, twice — `@`-citations then bare hints. **F-31, F-32, F-33, F-34** | The hint exemption (§3). Also swept `qa/risk-register.md`, a fourth file my brief missed |
| **BA** (migration) | `stories/` to **zero line-number tokens of any kind**, 165 identifier citations | **My worked example of a correct citation was itself broken** — a C# enum member is never self-qualified at its declaration site |

**Where I disagreed, or went further than asked:**

* **QA proposed no rule; I adopted its evidence as one.** The hint finding was filed as a flag. It is
  now SM-31's amendment and a change to the checker.
* **The BA reported the `ProjectAccessPolicy` staleness and correctly did not fix it** (`src/` is
  Backend's). I did not fix it either, and I disagree with treating it as a comment defect: **the
  stale sentence is the *argument for* HR's global reach** — safe *because* the grant set is small.
  It is F-28 with a comment instead of a test name. Backend's, one sentence.
* **I stopped the sweep at 103 rather than zero**, against the pull to finish. §6.

---

## 5. Blocked, and on whom

| What | On whom |
|---|---|
| **Q-N10-1, Q-N10-2b, Q-N10-3** — three workflow questions about opening a project | **Karim.** Slice 4. Cheapest answered together |
| **Q43, Q45–Q51, Q52, Q28, Q35–Q37, Q39, Q41, Q12–Q16, Q18–Q26, Q29, Q30, Q40** | **Karim.** None blocks a committed sprint-1 story |
| **SM-30 enforcement test** — one test that fails when a catalogue row is named in no test | **Backend** |
| **F-30** `ProjectTeamRead` — named in four stories, in no source file | **Architect + Backend.** Slice 2 |
| **`ProjectAccessPolicy` remarks** — "HR holds just two" since `UserRead` made it three | **Backend.** One sentence |
| **`meetings/` ~76 legacy citations** | **Scrum Master**, next session |
| **~25 `.md` -> `.md` line citations** in `stories/` and `qa/` | **BA, QA** |

**Nothing blocks a committed sprint-1 story.** Q42 was the one that did, and D-055 §2 closed it.

---

## 6. Judgement: the sprint does not move to code today

Build order unchanged: 106 → 100 → 108 → 109 → 113/114 → 110/111/112 → 101a/102/103/105a → 116/118.

**Why not, and it is not caution for its own sake.** Every story in `stories/slice-1-foundation/` was
rewritten today — waivers applied, 165 citations migrated, several claims found **false rather than
merely stale**. KAFF-106 is first in the build order and its file was edited hours ago. **Backend
builds what the story says, and a story can command a defect** — that is SM-29's founding case, where
`AC-108-A` asserted the F-04 leak as correct behaviour. Handing Backend a set of stories that changed
this afternoon, unread by anyone since, is the exact risk the law was written for.

**The honest scope of this run was the rulings and the registers.** Starting the build to look
productive is how the 2026-08-21 run ended — killed at 01:19 mid-edit of a test file, tree not
building, ninety seconds from looking finished.

**On the SM-30 enforcement test: it lands with the first Backend session, before any new catalogue
row** — not with slice 4. If it arrives with the slice-4 endpoints it arrives *after* the next batch
of rows, which is the same gate-cannot-see-an-absence problem it exists to solve.

---

## 7. Retrospective — is this converging?

**The failure class — a document asserting a code state that is no longer true — has now been found
in seven artefacts by seven authors in three days.** Stories (21st) · `decisions.md` D-056 ·
`decisions.md` D-055 §4's "Applied" · a source comment (F-28) · the Scrum Master's own enforcement
sweep (D-057 §5) · the citation corpus entire (D-058) · and SM-31's own carve-out (D-059 §9).

**Six of the seven fixes were rules asking the next author to be more careful. None held.** D-057 §5's
lasted four hours and was written by the agent enforcing the rule it broke.

**My read: it is converging, but not because the rules got stricter — because two of today's changes
are not requests.**

1. **SM-31 changes what a citation *is*.** It does not ask anyone to check anything. The common case —
   an unrelated edit elsewhere — can no longer break a citation, and the uncommon case, a deletion,
   breaks it **loudly**. That inverts a silent failure into a noisy one, which is the only structural
   change made in three days.
2. **`scripts/check-citations.ps1` is a machine that does not care how careful anyone was.** It caught
   two of my own citations twenty minutes after I wrote the rule. That is the test.

**And the single most productive thing today was not a rule at all.** It was one line at the bottom of
every brief: *"report anything in this brief that was wrong when you checked it against the files."*
**Four agents, four corrections, including the two that changed a ruling** — the hint exemption and the
enum trap. **No process document produced anything comparable.** The rules catch stale claims after
they land; that instruction catches them in the brief, before any work is done on top of them. It
should be standing text in every brief, and I would rather Nabil put that in `agents.md` than another
SM-number.

**What I am still uneasy about, stated because it is not solved.** Every fix so far has hardened the
*citation*. Nothing yet hardens the *claim*. `ProjectAccessPolicy`'s remarks will pass SM-31 forever —
they contain no citation at all, and they are wrong. **The next instance of this class will be in
prose that cites nothing**, and no checker written today would see it.

---

## 8. Addendum — the sweep finished, and a third carve-out was refused

Written after the run resumed from a usage limit.

**Final: 284 identifier citations, 0 broken, 101 legacy remaining.** `stories/`, `qa/`, `process/`,
`proposals/` and `decisions.md` are at **zero**. The residue is **`meetings/` (76)** and **~25
`.md` -> `.md` cross-references**, both assigned in `decisions.md` D-059 §13.

**The illustration question, ruled explicitly** (D-059 §14). The sweep's 3 "missing file" hits were
all **SM-29's own worked example**, `User.cs` line 232 — an illustration, never a claim. The ruling is
**not an exemption**:

> **An illustration must either be a true citation, or must not use citation markup.**

SM-29's example is a **direct quotation of Nabil** and could not be rewritten, so the sentence is
preserved character for character and only its code-span markup was removed, with a note that the
*format* is superseded by SM-31 while the *rule* is unchanged and binding.

**That is the third carve-out considered for SM-31 in one day and the third refused** — the hint, the
writing-about-citations case, and the illustration. **All three were resolved by changing what gets
written rather than what gets checked.** An exemption is a hole the checker cannot see into, and §9's
77 hidden stale hints are the record of what one costs. That is the test I would apply to the next one.

**One correction to my own §7 above:** I wrote that nothing yet hardens the *claim*, only the citation.
That still stands, and the `ProjectAccessPolicy` remarks — *"HR holds just two"*, now three — remain
the live example. **It cites nothing, so no checker written today would see it.** It is Backend's, it
is one sentence, and it is the shape the next instance of this class will take.

---

## 9. What has to be true before the sprint moves to code

**Exactly one thing gates it. Everything else runs alongside or after.**

**THE GATE — the rewritten stories are verified by someone who did not write them.** Every story in
`stories/slice-1-foundation/` was rewritten today: waivers applied, 165 citations migrated, and
several claims found **false rather than merely stale**. `agents.md` principle 2 — *the author never
certifies its own work* — means the BA cannot clear its own rewrite. **QA or a Verifier reads the
fifteen committed stories against `spec.md` and the code, and reports which claims do not hold.**
Half a session. **KAFF-106 is first in the build order and its file changed hours ago; Backend builds
what the story says.**

**Runs in parallel, gates nothing:**

| Work | Owner |
|---|---|
| ~~SM-30 enforcement test~~ · ~~`ProjectAccessPolicy` stale remark~~ | **Backend — both done, D-060** |
| `meetings/` 76 legacy citations — **64 distinct targets, each a real lookup** | Scrum Master |
| ~25 `.md` -> `.md` line citations | BA, QA |
| **CI has never run** | Nabil to assign — **before slice-1 acceptance, not before coding** |

**Waiting on Karim, blocking slice 4 only:** Q-N10-1, Q-N10-2b, Q-N10-3. Cheapest asked together —
all three are *"who can touch a project the moment it exists"*.

**Handed to Nabil as backlog, not closed:** **eight permission rows reachable and named in no test** —
`CatalogueManage`, `BabManage`, `SubcontractorManage`, `SupplierManage` (slice 2),
`OpportunityManage` (4), `ExtractPrepare`, `QuantityGateApprove` (5), `DailyLogWrite` (6). Found by
the SM-30 test on its first run and pinned as a shrink-only baseline, so the list cannot quietly grow.
**D-057 §1 named three uncovered rows; there were eleven.**

**Deliberately not swept with sed:** the `meetings/` citations need 64 individual lookups. Bulk
rewriting would manufacture plausible wrong identifiers — the exact defect class this ran on. Slow is
the only safe way, and it is owned rather than done.

**On "it is only historical, leave it":** refused. That is the reasoning that let D-055 §7 be read as
current for a day. A minute is read by the next session as evidence.

---

## 10. Correction to §8 — I signed off a false zero, and the worse half was mine

**§8 says `stories/` and `qa/` are at zero legacy citations. That was false when written**, and the
Verifier's story review caught it. There were **23**, and three of them mattered:

**Two stories quoted a sentence I had deleted hours earlier.** `KAFF-106` and `KAFF-113` both carry
*"`questions-for-karim.md` line 131 warned in terms not to close Q42 by handing HR the Owner's user
list"*. When I closed Q42 that morning I replaced the open-list row with Q-N10-2b and **paraphrased
the warning instead of preserving it** — so the quoted words were gone from the register, and line 131
had become unrelated prose. **A fabricated attribution: a real quotation, a real file, and the
sentence no longer in it.** That is precisely the class this whole run was about, committed by its
author, in a file signed off as clean.

**Fixed at the root rather than at the symptom.** The warning is **restored verbatim** to the Q42
answered row — it is load-bearing, two committed stories depend on it, and it is the only statement of
the control that the permission itself cannot enforce. Both stories now cite
`questions-for-karim.md` -> `Q42`. The quotation is true again because the source was put back, not
because the quote was softened.

**What it says about the checker.** `check-citations.ps1` had reported these correctly all along — my
§8 claim came from a scoped grep I ran *before* the last edits, not from the tool. **The tool was
right and the human summary of it was stale by twenty minutes.** SM-31 hardened the citation; nothing
hardens a *count* quoted in prose, which is the same gap as `ProjectAccessPolicy`. **Quote the tool's
output, do not restate it.**

---

## 11. Can SM-31 catch a fabricated attribution? No — and the gap now has a name

**Tested rather than reasoned about.** The fixed citation reads
`` `questions-for-karim.md` -> `Q42` `` followed by the quotation *"by handing HR the Owner's user
list"*. `scripts/check-citations.ps1` asserts the **anchor** `Q42` exists in the cited file — it does,
eleven times. **It never looks at the quoted words.** Had I not restored the deleted sentence, the
citation would pass **green with a fabricated quotation attached** [Verified: 2026-08-22].

**So this is the claim-hardening gap, not a hole in SM-31.** SM-31 hardens the *pointer*. The
quotation is a *copy*, and a copy has no pointer to harden.

**And it completes a pattern that three of today's findings share.** Each is the same move:

| Finding | Restated when it should have pointed |
|---|---|
| **SM-31** | a **position** restated, instead of naming the identifier |
| **`ProjectAccessPolicy`** | a **fact** restated — *"HR holds just two"* — instead of naming the test that pins it |
| **KAFF-106 / KAFF-113** | a **quotation** restated, instead of pointing at the register entry |

> **Don't copy what you can point at. A copy has no pointer, so nothing can tell it it is stale.**

**Why I am not making that a rule tonight**, having amended three rules within hours of writing them
today. A checker for it would have to match quoted text against a source that reflows, truncates and
elides — **noisy, and a checker people learn to distrust is worse than none**, which is the reasoning
that refused three carve-outs today. The honest mechanical answer is not a better matcher; it is
**writing that does not duplicate**: had KAFF-106 pointed at `Q42` without re-quoting it, there would
have been nothing to fabricate.

**Three instances is a pattern; I want a fourth before it is law.** Same standard I applied to the
cite-the-test experiment, and the same reason: this failure class has produced six rules in three days
and the two that held were machines, not requests.

---

## 12. The story-review gate, and what came back from it

**The gate I asked for was run by a Verifier that wrote none of the stories. It was worth running:
four findings, none a fix-in-passing.** Report: `qa/slice-1/story-review-2026-08-22.md`.

### V-01 — closed by the Architect, and it corrected me

**D-061 — the audit trail records events, not only entity changes.** One mechanism, two inputs: the
change tracker as before, plus events declared on `IAuditContext`. **D-031 is not relaxed and
KAFF-118 rule 2 holds in full** — no handler constructs an `AuditRecord`.

**It corrected my brief on the hard half.** I listed KAFF-100 among the "changes no entity" cases.
**It is an entity change** — one `User`, `Created`. Its problem was only ever the *actor*, and it has
a different fix: `IAuditContext.AttributeTo`, legal **only** on a request carrying no identity, with
an authenticated request naming another actor throwing. Two problems, two fixes, routed as one.

**The strongest thing in it is a rejection.** A second table for events was refused for a reason
better than the one I gave: a new table starts with **no append-only trigger, no no-truncate trigger,
and no entry in `FindMissingGuardsAsync`** — a forensic table that can be edited. My reason was
KAFF-116's column having two homes. Theirs is D-033.

**I treated the five new tests as unverified and mutated them myself**, because **D-061 records no
watched-to-fail evidence for any of them** — `agents.md` §3c, and SM-30's own history:

| Mutation | Result |
|---|---|
| Impersonation guard `if (_currentUser.UserId is not null)` -> `if (false)` | **1 red** — `An_authenticated_request_may_not_name_a_different_actor`, and only that |
| Event source -> empty sequence | **2 red** — `An_event_that_changes_no_entity_still_writes_a_record`, `An_event_and_an_entity_change_saved_together_share_one_correlation_id` |

Reverted; **build 0/0, format clean, Domain 75/75, Api 48/48.** The guards are real. **That evidence
should have been in D-061, and its absence is the gap SM-30 was written about, one artefact over.**

### V-03 to V-07 — the BA, and the most valuable thing it did was refuse

- **V-05 closed.** `AC-106-K` appended — next unused letter, no neighbour renumbered. `ac-id-map.md`
  229 -> **230**, integrity intact.
- **V-04 resolved from source.** Rule 4 stands; `AC-105a-F` was the wrong side. `PortalRead` and
  `PortalApprove` are `ProjectScoped`, so a portal client's company-wide set is **empty** — sourced to
  D-035, §12 and the 105a/105b split, **not** to whichever side read better.
- **V-07 closed** — the `.action.` / `.confirm.` / `.field.` segment applied.
- **V-03 refused, correctly.** Rule 3 vs `AC-105a-C` is marked ⛔ **DO NOT BUILD EITHER SHAPE**,
  because **neither source decides field-or-refusal** — that is N-04 / Q-UX-18 / SM-16, open. **A
  BA that hands back a question instead of picking a side is the single behaviour this process
  exists to produce.**

### My own action on the back of it

**KAFF-101a and KAFF-105a are now `BLOCKED`.** Both were still `Ready` while carrying an open
question, which fails the Definition of Ready's last line. `agents.md` §3b makes that mine to
enforce, not the BA's to decide.

**Thirteen of fifteen are startable; exactly one — KAFF-116 — is startable immediately.**

---

## 13. D-062 and D-063 — routed, and A-01 is bigger than it was filed as

### KAFF-116 is built

`AuditRecord.GrantPath`, a nullable `ProjectAccessPath`; migration `20260822210402_AuditGrantPath`;
five new Api tests covering all four grant paths — Owner-global, HR-global, assignment, portal client
— plus a guard that a grant path is refused without a project and may never be `None`. **Api 48 → 53.**
Status is **BUILT — awaiting verification**, not `Done`: `agents.md` principle 2 and the Definition of
Done both require a session that did not write the code.

**That accounts for the +5, and it settles a question worth asking.** D-063 states it wrote no test
and therefore watched none fail. The five are Backend's, not the Architect's — checked rather than
assumed, because two entries in a row have shipped tests whose authors did not mutate them.

### A-01 — the start-up guard does not check constraints, and it is not four constraints, it is 28

`DatabaseInitializer.FindMissingGuardsAsync` queries `pg_trigger`, `pg_indexes` and `pg_views` and
**never `pg_constraint`** [Verified: 2026-08-23 @ `DatabaseInitializer.cs` -> `FindMissingGuardsAsync`].
A database missing a check constraint **starts, serves, and reports no missing guards.** That is
**D-033's failure mode inside D-033's own mechanism**.

**D-063 filed it against four constraints. I counted the `ck_` identifiers declared across the EF
configurations and migrations: there are 28, and none is verified** [Verified: 2026-08-23]. The list
includes:

* **`ck_postings_amount_positive`** — `CLAUDE.md`: *"The safe balance can never go negative. Enforced
  by a database constraint, not application code."* **If that constraint is absent the application
  starts and the rule silently does not exist.** This is a money guard, and it is the reason A-01 is
  not a slice-3 tidy-up.
* `ck_users_subcontractor_cannot_log_in`, `ck_users_client_scope` — the identity boundaries, one of
  which is the subject of A-02.
* `ck_audit_records_event_shape`, `ck_audit_records_has_state`, `ck_audit_records_grant_path` —
  D-061's and KAFF-116's, both landed in the last day.

**Ownership, because "the Architect's own backlog" is not a route** (principle 8). The Architect was
right not to build it — it changes start-up behaviour in every environment and sat outside the three
decisions it was asked for. **Assigned: Backend, slice 1, before the slice-1 gate.** The fix is one
more query in a method that already contains the pattern three times over, and what it protects is the
money constraints.

**One caution I put in the brief and repeat here:** this makes start-up stricter everywhere. If the
test fixture does not create all 28, the suite stops running — which is the correct behaviour and a
real risk to check before declaring it done.

### Routed

| Item | To | Why |
|---|---|---|
| **A-01** | **Backend**, slice 1 | above |
| **A-02** — `AC-101a-G` is a username-existence oracle | **BA** | It returns `errors.auth.role_cannot_log_in` while `AC-101a-B` in the same story requires wrong-password, unknown-user and locked to be **indistinguishable**. A subcontractor can hold no credential at all [Verified: 2026-08-23 @ `IdentityConfigurations.cs` -> `ck_users_subcontractor_cannot_log_in`], so the distinct refusal **comes from the username alone** |
| **A-03** — Q47 asks about three cases; the door has five | **BA** | D-063 §1 added the client-at-staff-origin case, A-02 is the subcontractor case. **Widen the row before it reaches Karim**, or he answers a narrower question than the door asks and the remainder gets decided by whoever writes the handler |
| **KAFF-101a rule 16 + the audit criterion** | **BA** | Rule 16 describes the catalogue-dependent mechanism, not the ruled one. The audit criterion is answerable but **must not be written as though the IP field and absent subject exist** — they are decided in D-063 and not built. That is V-01 repeating |
| **Q54 — the IP address has no expiry** | **Nabil** | below |

### Q54 — surfaced, not settled

The Architect refused it and refused it correctly. **An IP address is personal data;
`audit_records` is append-only and no-truncate by trigger; `CLAUDE.md`'s prohibition on a delete path
is unqualified.** So **the first IP written is a personal-data field with no expiry, by construction,
forever.**

It does **not** block KAFF-101a — Karim has already ruled the event is logged with the IP. The
question is what happens to it afterwards. **The only mechanisms that do not break append-only are
partition-and-detach by age, or storing a keyed hash rather than the address.** Both are decisions
with a cost, and both are cheaper before the first row than after.

### The trend, stated because the raw count looks worse than the trend

D-063 contains **the fastest instance of SM-29's subject this project has produced**: a paragraph true
when checked and **false twelve minutes later**, because Backend's migration landed mid-entry. **The
Architect corrected it in place and said so.**

That is the rule working. The count of instances keeps rising because **we are now looking**, and
because the checker and the briefs surface things that used to pass silently. **A rising count of
caught instances and a falling count of escaped ones are the same trend seen from two ends** — the
number to watch is how many are found by someone other than the author, and today every single one
was.

---

## 14. A-01 closed, Q47 ruled, and one ruling flagged back

### A-01 — closed properly, and it set the standard the last two entries missed

**D-064, Backend.** `FindMissingGuardsAsync` now reads the required check constraints from **EF's
design-time model** rather than a written-out list, so the list cannot drift from the schema — both are
generated from the same model.

**The mutation was watched, and this is the part that matters.** Backend dropped
`ck_postings_amount_positive` from the live database and recorded both halves:

| | before the drop | after |
|---|---|---|
| `GET /api/health` | `200 healthy, missingGuards: []` | `503 degraded, missingGuards: ["ck_postings_amount_positive"]` |
| Start-up (Staging) | starts | **refuses, exit 82**, naming the missing constraint |

**Before this change every cell in the right-hand column read the same as the left.** The xUnit test
carries the mutation permanently — it drops the constraint, asserts it is reported, restores it in a
`finally`. **A test of a safety check that never removes the safety cannot fail, so it removes it.**
That is the standard D-061 and D-063 did not meet, met.

**Two deviations from my brief, both right, both declared.** `IDesignTimeModel` rather than
`_context.Model` — the obvious version compiles and throws at run time because the read-optimized
model discards check constraints. And **one query instead of the three-loop pattern I asked for**:
`/api/health` calls this on every poll, so a per-name loop would take it from 12 round trips to 40.
**My brief's "follow the existing pattern" was the wrong instruction and Backend was right to break
it and say why.**

**Still unverified at start-up, stated rather than left implied:** column nullability, foreign keys,
unique constraints, column types — and guards are verified by **name**, not definition. A constraint
whose expression was altered in place still passes. Naming was the routed gap; the rest is a bigger
thing nobody has asked for.

### Q47 ruled — D-065 — and Case 3 is flagged back rather than applied

Four cases unified at **401 with one identical response**: wrong password, unknown username, client at
the staff origin, subcontractor. **Case 5 closes A-02**, the finding the BA handed back rather than
resolving — **and the ruling agrees with the BA's instinct**, in Nabil's words: *"we are explicitly
telling the attacker: 'This account exists and belongs to a subcontractor.' That is a security
breach."*

**🟡 Case 3 — the locked account at 423 — is recorded OPEN and is being built neither way.**

A locked account **only exists if the username exists**, so a distinct 423 announces the username is
real — the exact thing Cases 2 and 5 were ruled to prevent — and it is **manufacturable**: five failed
attempts, and an attacker has the oracle for any username, plus a denial-of-service.

**The story already contains the counter-argument in its own words**, which is the strongest evidence
available: rule 14 reads *"Saying 'locked' tells an attacker the username is real and that their
lockout worked"* [Verified: 2026-08-23 @ `KAFF-101a-sign-in-api.md` -> rule 14]. `AC-101a-B` requires
all three identical **in status, body and messageKey**; D-063 §1 widened that set to four. **This
ruling widens it to five and removes one — both cannot be the intent.**

**Nabil's reason is legitimate**: a locked-out user given a generic 401 keeps trying and generates
support load. **The resolution put to him satisfies both: return 423 only when the credentials were
otherwise correct** — so only the legitimate password-holder ever sees it, and it leaks nothing.
**If he reaffirms the flat 423 it gets built as ruled and the trade-off is recorded as his explicit
decision, not a defect.** Sequenced last so it blocks nothing.

**🟡 Second flag: the ruled key namespace does not exist.** `errors.identity.invalid_credentials` and
`errors.identity.account_locked` are in neither catalogue. The division is already drawn —
**`errors.auth.*`** is door and authorization refusals, **`errors.identity.*`** is `User` entity
validation. A sign-in refusal is a door refusal, so **`errors.auth.invalid_credentials`**, ruled as a
consistency call and reversible at zero cost since no code depends on either name.

**And a key that must not be deleted:** `errors.auth.role_cannot_log_in` stops being reachable from
the sign-in door but `SeparationOfDuties` still uses it. Somebody will otherwise tidy it away.

### Routed

| To | What |
|---|---|
| **BA** | Q47 closed for 1/2/4/5 and **left open for 3**; `AC-101a-G` to the generic 401; KAFF-101a's status set to what is true, with each remaining blocker named. **No criterion for Case 3 in either shape** |
| **Backend** | **KAFF-106** — Nabil's *"begin coding immediately"*. First real endpoint surface in the system |
| **Nabil** | **Case 3**, with the conditional-423 resolution offered |

### Corrections to the state I was handed

Three, all because my own run was ahead of the report: **A-01 was already closed** (D-064) when the
order to do it arrived; **Api is 54/54, not 53/53**; **citations are 388 checked / 97 legacy, not
372 / 98**. And **KAFF-116 was already established as BUILT — awaiting verification** in §13 above,
not an open question.

---

## 15. D-067 through D-071 — the fourth instance, and the rule I chose not to write

### The state, verified by me at the close

| | |
|---|---|
| Build Release, warnings as errors, no Kaff process running | **0 errors, 0 warnings**, exit 0, **zero MSB302x copy warnings** |
| `dotnet format --verify-no-changes` | clean, exit 0 |
| Domain / Api | **75 / 75** · **85 / 85** |
| `check-citations.ps1` | **490 checked, 0 broken, 97 legacy — unchanged**, so no new debt |

### D-067 — an endpoint shipped with no permission gate

`PUT /api/users/{userId}/department` was mapped with `.WithName()`, `.WithTags()` and **no
`.RequirePermission(...)`**. Any authenticated caller could move any user between departments — and a
department is one of the two axes a permission is granted against, **so the route handed out
capability.** A privilege-escalation primitive, not a missing check.

**Its own comment read *"The permission check is the `RequirePermission` line below and nowhere
else"*, above four paragraphs of correct reasoning and a `Map` chain that enforced none of it.** A
reviewer opening that file to answer *"is this route protected?"* would have concluded yes.
`check-citations.ps1` passes on it — every identifier the comment names exists.

### The ruling: no SM-32 — D-068

This is the **fourth** instance of the claim-hardening pattern, after a **position** (SM-31), a
**fact** (`ProjectAccessPolicy`) and a **quotation** (KAFF-106/113). I said on 2026-08-22 that I
wanted a fourth before legislating. **I am not legislating.**

A fifth prose law would be the seventh fix of this class in four days, and **six of the first were
requests to be careful.** A rule saying *"do not write a comment claiming something the code does not
do"* is unenforceable by construction — it asks the author to notice the thing they have already
failed to notice.

**The answer is A-04, and A-04 is a machine.** What I recorded instead of a rule:

> **Prose a reviewer would rely on to answer a safety question is not documentation. It is an
> unexecuted assertion.** Either something executes it, or it is decoration that reads like a
> guarantee.

**And the diagnostic worth keeping: SM-30 would not have caught this.** SM-30 requires a new
*catalogue row* to ship with a test; KAFF-108 correctly adds none, it reuses `UserManage`. **The gap
is not a permission without a test — it is an endpoint without a permission.** Different holes,
different machines.

### D-069 — A-04 built, and the allow-list is the decision

Three facts over one enumeration of **built endpoint metadata, not source text** — the distinction
D-067 earned, because a grep over source reads what somebody meant. It asks for a
`PermissionRequirement` policy specifically, never authorization in general: **the fallback policy
already admits every authenticated caller, which is exactly what D-067's attacker was.**

**One allow-list member: `GET /api/health`.** Sign-in was **deliberately not pre-listed** — the test
going red the day KAFF-101a maps that route *is* the visible act. A second fact fails on a dead
exemption and on a member whose own slice does not say `AllowAnonymous()`, so the exemption stays
legible in the file a reader opens. **Three mutations watched, each red, each naming the route.**

**The Architect also closed V-C by construction rather than by prose** — the third fact asserts an
endpoint's declared scope matches its catalogue row, making the mismatch unreachable instead of
"nothing does this today", **which is the sentence that preceded D-067.**

### D-071 — V-A fixed at the shared path, not at the endpoint that reported it

Every 401 and 403 in the system carried **no `messageKey`**, so the Arabic UI had nothing to render.
Fixed in one `CustomizeProblemDetails` callback behind `IProblemDetailsService` — the single writer
for `UseStatusCodePages`, `UseExceptionHandler` and `Results.Problem` alike. **The hole was on every
endpoint, not KAFF-106's**, and a per-endpoint patch would have left every sibling silent.
`TryAdd`, so a handler naming a more specific key keeps it. Watched to fail; confirmed on the running
stack.

### The one that matters most operationally — D-069 §6, and it defeats D-046

**A leftover `Kaff.Api.Tests` host locks `Kaff.Api.dll`; the build then emits `MSB3026`, copies
nothing, and reports `Build succeeded`, 0 errors, exit code 0.**

**Checking the build's exit code — the thing D-046 exists to make everyone do — passes, and the suite
you run next executes the previous binary.** That is a green light with no evidence behind it,
arriving through the one door D-046 does not watch. The documented gotcha covers `Kaff.Api`, which
fails **loudly** with `MSB3021`. This one fails silently.

**Added to `/run-kaff-erp`'s Gotchas**, because §B0 makes the skill the operational source of truth
and a hazard recorded only in a D-entry is one every agent is instructed to look somewhere else for.
**Two rules: kill `Kaff.Api.Tests` as well, and treat `MSB3026` on a succeeded build as a failed
build.**

### Story status, ruled

* **KAFF-116 — ACCEPTED.** Verifier recommended, D-068 concurs, D-070 written.
* **KAFF-106 — BUILT, NOT ACCEPTED.** Held on V-A until D-071 is verified by someone who did not
  write it. `AC-106-J` carried forward **explicitly**; `AC-106-H` correctly deferred. The Verifier's
  warning is the keeper: ***"the temptation on a green suite is to read 11 of 11."***
* **KAFF-108 — BUILT, awaiting verification.** Its slice has no `Response.cs` and no `Validator.cs`
  and **that is correct, not interrupted**: 204 has no body, and the request's only rule is the
  domain's `ValidateDepartment`. **`CLAUDE.md`'s five-file listing is the shape of a full slice, not a
  quota.** Seven criteria, eleven tests. Its one real gap was the gate.

### V-J — my own rule was blocking finished work

The Definition of Done said `check-citations.ps1` **passes**; it exits 1 on **97 legacy citations, 76
of them in one meeting file of mine, none in any file this work touched.** **A pre-existing debt of
mine was blocking acceptance of two finished stories.** Wrong scoping, not a wrong rule — a Definition
of Done is about the change in front of you. Split into `broken = 0` **repo-wide and absolute**, plus
**no new legacy citation introduced.** The 97 remain owed and are not forgiven.
