# process/agile.md — how Kaff ERP is built

The operating model. `spec.md` is the business truth, `CLAUDE.md` is the rules, `decisions.md` is why
things are the way they are, `agents.md` is who does what. **This file is how the work moves.**

Adopted 2026-08-20, at Nabil's direction, after slice 0 shipped.

---

## Why this exists

Slice 0 was built by one agent working from a brief. That worked because slice 0 had no business
logic in it. From slice 1 onward every slice touches money, permissions, or both, and the failure
mode changes: **an agent that invents a business rule produces something plausible, and plausible
survives review.**

Two things catch that, and neither is code review:

1. **A story written before the code, traceable to a `spec.md` section.** A rule with no citation is
   visible as a rule with no citation.
2. **A refinement session where somebody whose job is to ask reads the story out loud** and the
   people who will build it say what they do not know.

That second one is what the Scrum Master is for here. It is not ceremony for its own sake.

---

## Cadence

**One slice = one sprint.** The slice sequence in `agents.md` is the release plan; it does not
change to suit a sprint boundary. If a slice is too big for one sprint, it is split into
`3a` / `3b` and both halves keep the slice's gate.

```
Refinement  ──→  [Nabil approves the sprint scope]  ──→  Build  ──→  Verify  ──→  [Nabil accepts]
     ▲                                                                                    │
     └────────────────────────────────────────────────────────────────────────────────────┘
```

There is no separate planning meeting. Refinement ends with a committed scope or it has not ended.

---

## The ceremonies, and what each is actually for

### 1. Refinement — before every sprint

**Run by:** Scrum Master · **Present:** BA, UX, Architect, Backend, Frontend, QA
**Produces:** `meetings/YYYY-MM-DD-sprint-N-refinement.md`

The Scrum Master walks every story in the sprint aloud and asks each agent the same question:
**"what do you not know?"** Every answer lands in one of three buckets, and the bucket decides what
happens next:

| Bucket | Meaning | What happens |
|---|---|---|
| **Answered by `spec.md`** | somebody just had not read that section | cite it in the story, move on |
| **Answered by `decisions.md`** | already decided, with a reason | link the D-number, move on |
| **Answered by nobody** | a business rule that does not exist yet | **Question for Karim.** The story is marked `BLOCKED` and does not enter the sprint. |

**The third bucket is the whole point of the meeting.** A refinement session that produces no
questions has not been run properly — on this project it means nobody read closely enough, not that
the spec is complete.

**Nobody resolves a bucket-three item in the room.** Not the BA, not the Architect, not by consensus.
Consensus among agents is the most confident possible way to be wrong.

### 2. Build

Backend and Frontend run concurrently only where file ownership is disjoint (`agents.md` principle
3). Nobody starts a `BLOCKED` story.

> **⚠️ Amended 2026-08-30 by the Scrum Master, after it cost two stalls in one day. Disjoint file
> ownership is not sufficient. Run at most one agent on this machine at a time.**
>
> On 2026-08-29 Frontend and Backend ran concurrently with **genuinely disjoint file ownership** —
> `src/Web/` against `src/Infrastructure/` and `tests/Api.Tests/` — and principle 3 was satisfied
> throughout. They collided anyway, on the two things the rule does not name: **port 5080** (which
> `src/Web/proxy.conf.json` hardcodes, so both agents need the same one) and **`Kaff.Domain.dll` /
> `Kaff.Infrastructure.dll`**, which a running API holds open against the other agent's build. One
> agent then killed the other's API host by PID. D-092 records working around it by starting from a
> checked-in binary with `Kaff__ApplyMigrationsOnStartup=false`, and by stopping the API afterwards —
> a workaround, not a fix.
>
> **The machine is the shared resource, not the files.** Principle 3 is about not overwriting each
> other's work; this is about not being able to build or run at all. Both must hold, and the second
> one is a hard serial constraint until the stack can be brought up twice on one box.



### 3. Verification — a fresh session, always

The Verifier reads `spec.md` and the QA test cases. **It never reads the implementation**, and it
never fixes anything. Failures go back to the author.

`CLAUDE.md`: "If you wrote the code, you do not certify it."

### 4. Acceptance — Nabil

Nabil runs the demo script. The gate for each slice is in `agents.md`, and it is a specific
observable thing, not a judgement: *permission tests pass* · *the worked example reconciles* ·
*prices provably frozen* · *the portal leaks nothing*.

### 5. Retrospective — after acceptance

One section appended to the sprint's meeting file: what the sprint taught about the process, not
about the code. Anything structural goes to `decisions.md`.

---

## Definition of Ready

A story does not enter a sprint until **all** of these hold. This is the Scrum Master's checklist and
they are checked out loud in refinement.

- [ ] Every acceptance criterion is Given / When / Then
- [ ] **Every acceptance criterion carries a stable `AC-<story>-<LETTER>` ID.** A new one is appended with the next unused letter — never inserted, never renumbering its neighbours
- [ ] Every business rule cites a `spec.md` section, or a `decisions.md` D-number
- [ ] **No rule in the story is uncited.** An uncited rule is a question for Karim, not a story
- [ ] Permissions named explicitly: which role, and whether an assignment is required
- [ ] Money behaviour named explicitly, or the story states it moves no money
- [ ] Arabic UI strings identified as i18n keys — never literals, in either language
- [ ] The audit record it writes is stated: who, when, what changed, and why where the flow needs it
- [ ] QA has written at least one scenario that **fails** if the rule is broken
- [ ] **If the story adds a permission catalogue row, the test that names it is written before the row is** — SM-30 below
- [ ] **Every claim the story makes about the state of the code carries its verification date and a stable identifier** — `[Verified: 2026-08-22 @ `User.cs` -> `SetTemporaryPassword`]`. See *The Story Currency Law* (SM-29) and *The Citation Law* (SM-31) below. **A bare `file:line` no longer satisfies this** — a line number moves on the next edit and points confidently at the wrong thing
- [ ] Not `BLOCKED` on an open question

### The Story Currency Law — SM-29

**Nabil, 2026-08-22, adopted as a strict workplace law. `decisions.md` D-055 §5.**

> Any story that claims a state in the code (e.g. "The code refuses X") must carry a verification
> date, filename, and line number next to it (e.g. [Verified: 2026-08-22 @ User.cs:232]). Stories
> commanding the code to match a past state are disguised defects. The "evidence before trust" rule
> applies to the documentation just as it does to the code.

> **⚠️ The format shown in Nabil's example above is superseded by SM-31 (`decisions.md` D-059).** The
> rule is unchanged and binding; only the citation form moved on — a line number is no longer a
> citation. The quotation is preserved verbatim because it is Nabil's wording, and its code-span
> markup was removed so a checker does not read an illustration as a live claim. **The current form
> is** [Verified: 2026-08-22 @ `User.cs` -> `SetTemporaryPassword`].

**Illustrations, and why there is no exemption for them.** The repo sweep flagged three phantoms —
all three were SM-29's own example above. **The rule is not "exempt examples"; it is:**

> **An illustration must either be a true citation, or must not use citation markup.**

Cite something that really exists — an example that is also true costs nothing and stays checkable —
or, where the words cannot be changed because they are somebody's quotation, **strip the code-span
markup and annotate.** That is what was done above: every character of Nabil's sentence survives, and
the checker no longer sees a claim.

**Why not an exemption list.** An exemption is a hole the checker cannot see into, and D-059 §9 is the
record of what that costs — 77 stale hints hiding inside one, four hours after the rule was written.
**There is no such thing as a harmless placeholder:** a fake example is indistinguishable from a broken
citation, to a reader and to a checker, and the next person running the sweep will chase it.

**Why this is a law and not a style note.** Five stale story assertions in three days, and one of them
was not cosmetic: `AC-108-A` asserted the **F-04 permission leak as correct behaviour**, and KAFF-108
is third in the build order. Backend builds what the story says. **A story can command a defect.**

**The mechanism it closes is structural, not careless.** `spec.md` has 📌 amendment blocks,
`decisions.md` has D-numbers and superseded markers, `qa/questions.md` has strike-through. **Stories
had no staleness mechanism at all** — a story asserts the state of the code in the present tense, is
written once, and is read as current forever. The code moved four times in three days; the stories
could not have kept up, because nothing told them to.

**It binds every agent, not only the BA.** No finding is repeated from a document without re-reading
the file that document names, **today**. On 2026-08-21 four already-closed findings were re-reported
as live defects because an agent trusted its own transcript over the current files. A dated claim is
checkable in seconds; an undated one is re-litigated by whoever reads it next.

**An undated claim is not a smaller version of a dated one — it is a claim that has not been made.**
Treat it in refinement exactly as an uncited rule is treated: it does not pass.

#### SM-29 binds `decisions.md` too — the "Applied" clause

**Added 2026-08-22, `decisions.md` D-057 §4, after the same failure class was found three days running
in three different artefacts:** the stories on the 21st, `decisions.md` on the 22nd (D-056), and
`decisions.md` again the same afternoon.

> **A `decisions.md` entry may state what was *decided* in the past tense. It may not state what was
> *applied* unless the application was verified after it happened, with a `file:line`.**

**The pattern has a specific shape and it is worth naming: the word "Applied".** D-055 §7 stated a
code state that a later paragraph of the same run falsified — four `User` fields said not to exist,
built within the hour. D-055 §4 stated *"**Applied:** each of the six stories records the waiver in
the story"* when **no file under `stories/` contained the word** [Verified: 2026-08-22]. D-055 §8
required four questions to be closed at their source and none were.

**All three were written by the agent that intended to do the work, in the same breath as intending
it.** An entry that records an intention in the past tense is indistinguishable from one that records
a fact, and nothing in the entry format separates them. **`decisions.md` needs SM-29 more than the
stories do**, because a D-entry is read as settled history by every future session — it is the file
`CLAUDE.md` sends agents to first.

**Where an entry records work still to do, it says so under a "Not done" heading.** D-056 does this
and D-055 does not, which is the whole difference between the two.

#### ~~How to produce a `file:line` that is worth citing~~ — **SUPERSEDED by SM-31, 2026-08-22**

> **⚠️ This remedy lasted four hours and is kept only as the reasoning behind the one that replaced
> it.** It prescribed *"cite from a `grep -n` on the identifier"* — which is asking people to be
> careful, the thing its own last line says will not work. **`decisions.md` D-058 demonstrated it
> mechanically and D-059 rules on it: cite the identifier, not the position. See SM-31 below.**
> **What survives from this section is the last paragraph — the date does real work and stays.**

**Added 2026-08-22, `decisions.md` D-057 §5, after the Scrum Master broke SM-29 while enforcing it.**
Twelve citations written into the registers during the sweep that adopted SM-30 were wrong. They were
found within the hour by the BA, checking the brief against the files as instructed.

The failure was not carelessness. The catalogue was read with `sed -n '168,232p'`, the rows were
found inside that window, and **the line numbers were written from the window rather than from the
rows.** The window was real, the rows were real, and the citation was invented. That is the general
shape: **trusting your own transcript of a file you read four minutes ago** is the same error as
trusting a document you read four days ago, and it is the one SM-29 names.

**Note what worked, because SM-31 keeps this half.** The rule produced a dated, checkable claim, and
a second agent falsified it in minutes. **An undated claim would have been unfalsifiable and would
have survived unchallenged.** SM-29 did not fail here — it fired.

### The Citation Law — SM-31

**Scrum Master's ruling, 2026-08-22, on `decisions.md` D-058. ADOPTED, and it supersedes D-057 §5's
remedy rather than sitting beside it.**

> **Cite a stable identifier, not a position.**
> `[Verified: 2026-08-22 @ `PermissionCatalogue.cs` -> the `Permission.ProjectManage` row]`
> `[Verified: 2026-08-22 @ `User.cs` -> `SetTemporaryPassword`]`
>
> **The date stays. A line number is not a citation in any position — not as the claim, and not as a
> hint after it.**

> **⚠️ Amended the same day, and the amendment is the interesting part.** SM-31 first permitted a
> line number *"as a convenience hint, never as the claim"*. **QA measured that exemption within the
> hour and it did not survive contact:** **77 bare line hints repo-wide**, decaying at exactly the
> same rate as the claims they were exempted from — `` `PermissionCatalogue.cs` line 258 `` appears **eight
> times** and is a **blank line**; `` `PermissionCatalogue.cs` lines 180-182 `` appears four times for a row
> now at 208. **And the checker was blind to all of them**, so it reported green on a corpus that was
> lying in a smaller font. That is D-058's failure reproduced inside D-058's own remedy. **A courtesy
> that lies is not a courtesy.** The exemption is withdrawn; `scripts/check-citations.ps1` now counts
> a bare `` `File.cs` line 123 `` wherever it appears. See `decisions.md` D-059 §9.

**Writing *about* a line number, rather than citing one.** A document that discusses this problem —
this section, D-057, D-058, D-059 — necessarily contains examples of the retired form, and a checker
cannot tell an example from a citation. **Write it as prose, not as a code span:** `` `User.cs` line
232 ``, never the colon form inside backticks. The checker's pattern requires the colon *inside* the
backticks, so prose passes and a real citation does not.

**This is a writing convention, not an exemption**, and the distinction is the whole subject of §9
above. Nothing is excluded from the check — the sentence simply is not a citation. **An exemption the
checker cannot see is indistinguishable from a violation it cannot see**, which is why there are none.

**Choosing the identifier — three traps, all found the day SM-31 was adopted.**

1. **A C# enum member is never self-qualified at its declaration site.** The natural-reading citation
   the form *"PermissionEvaluator.cs, arrow, ProjectAccessPath.PortalClient"* **fails the check**: inside the
   enum the member is declared bare as `PortalClient = 4`, and the qualified form appears only where
   it is *used*, in `ProjectAccessPolicy.cs`. Cite `PortalClient`, or cite the file that uses the
   qualified name. **This trap was found in the Scrum Master's own worked example of the correct
   form** — the example was itself a broken citation.
2. **An absence has no member to name.** *"There is no `ProjectTeamRead` row"* cannot cite the thing
   it says is missing. Cite the **nearest enclosing named thing** — `PermissionCatalogue.cs` ->
   `Build`, `AuditRecord.cs` -> `class AuditRecord` — so the citation points at the place the reader
   must look to confirm the absence.
3. **A passage whose subject *is* a past wrong citation must be de-cited, not migrated.** Converting
   it to an identifier destroys the history it exists to record. Write it as prose (see the convention
   above) so no reader and no checker mistakes a historical example for a live claim.

**Locale keys count.** `.json` is in the checker's extension list, so
``@ `ar.json` -> `errors.identity.hr_role_requires_hr_department` `` is verified like any other
citation. It was added on 2026-08-22 after both the BA and QA independently found locale citations
that were invisible to the check in both directions.

**A reference without the `@` marker is not a citation.** Ruled by the Scrum Master, 2026-08-27, at the
sprint-1 close, after `e9f3dcf` repaired a matcher that worked line by line — **118 citations wrapped
across two lines had never been checked at all**, 753 → 871, and two of them were broken. The fix
reported one gap it does not close: a reference written without the `@` marker is counted as neither
legacy nor verified. **The boundary stays.** Widening the pattern to every backticked arrow would flag
this section, every meeting file and every discussion of a past citation — D-059 §9's exemption problem
inverted, and *a checker that cries wolf gets muted, which is D-046's green light by another name.*
What closes the gap is the rule, not the regex: **a reference without `@` carries no verification claim
and must not be written where one is needed.** Write the claim as a citation, or do not make it.

**The larger blind spot, named so nobody reads the total as coverage: the checker walks `*.md` and
`.json`, never source.** Every `<c>File.cs</c>` arrow citation inside an XML doc comment — and this
codebase is full of them, deliberately, because the reasoning lives beside the code — is verified by
nothing. In the week this was written the same non-existent test name sat in three artefacts and the
checker saw exactly one. Extending it to `*.cs` needs the writing convention restated for XML docs
first, so it is not a one-line change; it is **open, and owed by Backend.**

**Why the date survives and the line number does not — they were doing different jobs and only one of
them was doing it.** The date says **when the claim was checked**, which nothing else carries and
which tells a reader how much to trust it. The line number said **where**, which an identifier says
better and says stably. D-057 §5 is right that dating made a false claim falsifiable within the hour;
an undated one would have survived. **So SM-29 is unchanged and SM-31 replaces only its position
half.**

**The evidence is mechanical, and it is worse than D-058 measured.** D-058 counted *"at least 14"*
citations resolving to the wrong thing. Re-measured on the same corpus:
**~68 citations point at `PermissionCatalogue.cs` across 30 distinct line numbers, from `:58` to
`:396`** [Verified: 2026-08-22]. They are archaeological strata of one file's edit history, and each
was correct on the day it was written:

| Cited | Times | What is actually there today |
|---|---|---|
| `:238` | **16** | the middle of a comment sentence |
| `:258` | **10** | a **blank line** |
| `:200` | **9** | the middle of a comment sentence |
| `:180` | 7 | the middle of a comment sentence |
| `:257` | 7 | `TouchesMoney: true),` — a fragment of a row's closing arguments |
| `:315` | 5 | the `// ---- Site execution ----` section header |
| `:208` | **1** | **the `Permission.ProjectManage` row — the only one that is right** |

**Nobody wrote any of those wrong.** Twenty comment lines were inserted above them — the F-28 fix and
the three SM-30 citations — and every citation below the insertion point shifted. **The person editing
`PermissionCatalogue.cs` broke citations in nine files they never opened**, and had no way to know.

**The half that makes this a rule rather than a preference — and it is one grep.** An identifier
citation is *mechanically checkable*: assert the cited identifier appears in the cited file.
`grep -q "$identifier" "$file"`. Position citations are not checkable at all — a line number always
resolves, so a checker reports **194/197 healthy on a corpus where dozens are lying**. That is not a
weaker check; **it is a green light with no evidence behind it**, which is D-046's failure by another
name.

**And the failure mode inverts, which is the real prize.** Delete the cited code today and the line
number points confidently at whatever slid into its place. Delete it under SM-31 and the search
returns **nothing** — a loud failure instead of a silent one.

**What I rejected.**

* **Rejecting SM-31 and keeping `grep -n` discipline.** D-058's argument is unanswerable: D-057's own
  last line says this class *"will not be fixed by asking people to be careful"*, and D-057's remedy
  was exactly that. The evidence arrived four hours later.
* **Dropping the date along with the line number.** They are separable and the date is load-bearing —
  see above. This was the one part of D-058 I had to decide rather than accept, and it decides the
  other way from its framing.
* ~~**A line-number ban.** A hint after the identifier costs nothing and helps a human scroll.~~
  **REVERSED the same day — QA measured 77 stale hints the checker could not see. A courtesy that
  lies is not a courtesy, and an exemption the checker is blind to is how a green light gets issued
  over a lying corpus. The ban is now the rule.** See the amendment box above.
* **Building a citation tool.** The check is a grep. `scripts/check-citations.ps1` exists, and its
  whole job is to fail loudly on an identifier that is absent — not to parse, index or lint.

**Revisit if** a citation needs to name something with no stable identifier — a paragraph of prose, a
migration's body. Cite the nearest enclosing named thing (the section heading, the migration name)
rather than falling back to a line number.

### The Permission Coverage Law — SM-30

**Scrum Master's ruling, 2026-08-22, on the proposal in `decisions.md` D-056 §4. ADOPTED, amended.**

> **A new permission catalogue row and a test that names it land in the same change.** The row's
> comment cites that test by name, and **the name must be one that exists.**

**Why this is not a duplicate of the Definition of Done, which was the one argument against it.**
The DoD says *"permission tests pass"* and it is checked at the **end of a slice**. On 2026-08-22
`ProjectCreate`, `ProjectFinancialsEdit` and `UserRead` shipped reachable in the catalogue and named
in **no test anywhere**, while the suite stood at 74/74 green. **A row with no test does not make any
test fail.** The DoD is structurally incapable of catching this, because it tests for red and the
defect is an absence.

**And it is the exact inverse of SM-29, which is why both are needed.** SM-29 catches a claim that is
*wrong*. SM-30 catches a claim that is *missing*. Wrong prose gets read and doubted; **a permission
with no test is invisible.** The same run produced one of each: an untested row, and
`Only_the_owner_and_the_technical_office_may_open_a_project` still asserting against `ProjectManage`
— the permission that, after the D-055 §3 split, is the one that *cannot* open a project. It would
have stayed green forever while testing something its own name disclaimed.

**The amendment, and it is not pedantry — the mechanism misfired twice before the rule was ruled on.**
SM-30 as proposed requires a comment to cite a test by name. Two such citations were checked on
2026-08-22 and **both were wrong**:

- The `ProjectManage` catalogue row cites **two** tests and **one does not exist**:
  `Opening_a_project_needs_no_project` appears only in `proposals/N10-project-creation.md` -> `Opening_a_project_needs_no_project`, where
  it was a *proposed* name [Verified: 2026-08-22 — absent from `tests/`]. The real one is
  `Only_the_owner_and_the_technical_office_may_open_a_project`
  [Verified: 2026-08-22 @ `PermissionEvaluatorTests.cs` -> `Only_the_owner_and_the_technical_office_may_open_a_project`].
- **`decisions.md` D-056 §2 — the entry that proposed SM-30** — names
  `Hr_holds_exactly_two_permissions_and_neither_touches_money`, renamed to
  `Hr_holds_exactly_three_permissions_and_none_touches_money` **in the same run that wrote the entry**
  [Verified: 2026-08-22 @ `CatalogueCompletenessTests.cs` -> `Hr_holds_exactly_three_permissions_and_none_touches_money`].

**The rule's own proposal contains an instance of the rule's own failure mode**, and both were written
by careful agents in otherwise exemplary entries. **A citation nobody can check decays into the thing
SM-29 exists to stop.** Two consequences:

1. **A cited test name is a claim about the code, so SM-29 already binds it.** Verify the name exists
   before writing it. This costs one search.
2. **The enforceable half is coverage, not prose.** The prose citation tells the next reader *where to
   look*; it cannot tell them the cover is real. What can is **one test that fails when a catalogue
   row is named in no test** — the smallest thing that fails if the rule breaks. **Backend owes it.**
   Until it exists, SM-30 is enforced by reading the diff at refinement, which is weaker and is
   recorded as weaker.

**What SM-30 does not require.** It does not require an endpoint. `ProjectCreate`,
`ProjectFinancialsEdit` and `UserRead` have none until slice 4, and their tests are **catalogue and
evaluator** tests. That is the right level and it is the level at which the mutation was watched to
fail: widening `ProjectManage` to `CompanyWide` — the smaller diff a future session will be tempted by
— turns exactly one test red, and before 2026-08-22 it turned nothing red at all.

**Added to the Definition of Ready above** as the catalogue-row case of the existing QA line, and to
the Definition of Done below.

### The Test Naming Law — SM-33

**Scrum Master's ruling, 2026-09-01, at the sprint-2 refinement. `decisions.md` D-097 §2.**

> **A test name that is merely *narrow* stays. A test name that the change makes *false* is renamed in
> that same change, and its citations move with it in the same commit.**
>
> **And the cheaper half, which prevents the case arising: a test name must not encode a count that a
> legitimate future change falsifies.** Put the property in the name and the arithmetic in the body,
> where it fails loudly without lying.

**What raised it.** KAFF-105b would add a `ProjectTeamRead` catalogue row granted to `Role.Hr`, making
HR hold four permissions — which makes
`Hr_holds_exactly_three_permissions_and_none_touches_money` false **in its own name**
[Verified: 2026-09-01 @ `tests/Domain.Tests/CatalogueCompletenessTests.cs` ->
`Hr_holds_exactly_three_permissions_and_none_touches_money`]. That name is cited in **five** files of
record — `decisions.md`, this file, `qa/questions.md`,
`stories/slice-1-foundation/KAFF-107-hr-role-is-bound-to-the-hr-department.md` and
`proposals/N10-project-creation.md` — several of which the implementing agent must not edit.

**Why this does not contradict D-095, which chose the opposite.** D-095 widened `ValidateDepartment` to
also check that the role is a role, considered renaming it to `ValidateRoleAndDepartment`, and
**reverted the rename** because four historical records cite the old identifier under SM-31 and live in
`meetings/`, `qa/`, `proposals/` and `stories/`. **That was right, and the distinction is the whole of
this rule:** `ValidateDepartment` still validates the department. It became **narrow**. It did not
become **untrue**.

`…exactly_three…` becomes untrue the moment the fourth grant lands. **A false test name is worse than a
stale one, because the name is what a reader takes for the assertion** — which is SM-29's subject
exactly, applied to the one artefact SM-29 was never pointed at.

**Who moves the citations, since that is what made D-095 choose the other way.** The implementing agent
moves the ones in files it owns — its own source, and its `decisions.md` entry. **The Scrum Master
moves the ones in `meetings/`, `qa/` and `proposals/`**, which it may not edit, and does so in the same
commit or the rename does not land. Historical records are corrected as **marked amendments, never
silent edits** — SM-29's own practice.

**It is not only about counts, and `V-30-A` is the proof.** `qa/slice-1/verification-2026-08-30.md`
found `Nothing_outside_LiveSession_can_produce_the_metadata_that_proves_a_route_paid` to be false —
reflection produces the metadata, and the suite reported **227/227** against a route applying none of
its checks. That name asserts a safety the code does not have, to every reader of a failing run. **It
falls under this rule for the same reason the count does.**

**Why a rule rather than a case-by-case call.** Both instances were found in the same week, by two
different agents, in two different files, and neither was noticed by the sessions that created them —
because **a name is not read as a claim until somebody checks it.** SM-31 made citations checkable and
left names alone. This closes the half it did not reach.

## Definition of Done

`CLAUDE.md`'s list, unchanged, plus the two this process adds:

- [ ] Builds clean with warnings as errors
- [ ] `spec.md` acceptance criteria for this slice pass
- [ ] Permission tests pass
- [ ] Runs on staging, not only locally
- [ ] Arabic RTL correct at mobile width
- [ ] Audit records written for every state change
- [ ] No hardcoded strings
- [ ] `decisions.md` updated if anything structural changed
- [ ] Demo script runs end to end
- [ ] **Verified in a session that did not write the code**
- [ ] **Every QA test case for the story executed, with its result recorded — including the ones that failed and why they now pass**
- [ ] **Every permission catalogue row the change adds is named in a test that ships with it, and the test name the row's comment cites exists** — SM-30
- [ ] **Every code citation the change adds or touches names a stable identifier, not a line number** — SM-31
- [ ] **`scripts/check-citations.ps1` reports `broken (identifier absent)` = 0** — repo-wide, and this one is absolute: a citation pointing at something that does not exist is the defect SM-31 exists for
- [ ] **No test name the change makes false survives the change, and its citations move in the same commit** — SM-33
- [ ] **The change introduces no new legacy line-number citation** — measured against the count before the change, not against zero. **Scoped this way on 2026-08-24 (`decisions.md` D-068) because the unscoped version blocked acceptance of two finished stories on 97 pre-existing citations, 76 of them in one meeting file, none in any file the work touched.** A Definition of Done is about the change in front of you; the backlog of 97 is owed separately and is not forgiven by this

---

## Story format

One file per story: `stories/slice-N-<name>/<ID>-<slug>.md`.

```markdown
# KAFF-101 · A title that names the outcome

**Slice:** 1 · **Epic:** Identity · **Points:** 3 · **Status:** Ready | BLOCKED | In progress | Done
**Spec:** §9, §2 · **Decisions:** D-044
**Depends on:** KAFF-100

## Story
As the Owner, I want to create a user with a role, so that somebody other than me can use the system.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | Only the Owner may create a user | §9 · D-044 |
| 2 | An HR user must carry the HR department | D-044 |

## Acceptance criteria
**AC-101-A — the Owner creates a user**
Given I am signed in as the Owner
When I create a user with role Finance and department Finance
Then the user is created, inactive until a password is set, and an audit record names me as the actor

**AC-101-B — nobody else can**
Given I am signed in as HR
When I attempt to create a user
Then the request is refused with 403, and the refusal is logged

## Not in this story
Password reset. Session expiry. — both KAFF-1xx, slice 1.

## Questions for Karim
None. | Or: numbered, and the story is BLOCKED.
```

**Story IDs are permanent.** `KAFF-101` means one thing forever, including after the story is
deleted. Test cases reference them, and a renumbered story silently detaches its tests.

**Acceptance-criterion IDs are permanent too, and for the same reason.** `AC-<story>-<LETTER>` —
`AC-101-A`. A new criterion is **appended and takes the next unused letter**; it is never inserted
into the sequence, and its neighbours never shift. A deleted criterion's ID is **retired, not
recycled**, and stays in the file struck through with the date and the reason. The ID is an
identity, not a position — criteria are free to be reordered for readability, and a story whose
criteria run `A B K C` is correct rather than untidy. **Cite the full ID, never a position:**
`AC-118-F`, not `KAFF-118 AC3`.

Adopted 2026-08-22, action **SM-23**, after **thirty-one QA cases were found citing the wrong
criterion** — whole blocks had shifted when stories inserted criteria mid-list. The full rules, the
retirement notation and the one-time old-label mapping are in `stories/README.md` and
`stories/ac-id-map.md`.

---

## Estimation

Fibonacci, relative, and the number means **uncertainty**, not hours.

| | |
|---|---|
| **1** | one rule, one endpoint, cited, no money |
| **2–3** | a normal slice of work |
| **5** | touches money or the permission model |
| **8** | touches both, or spans backend and frontend |
| **13** | **too big — split it.** A 13 is a story nobody understands yet |

**A story that moves money is never a 1.** If it looks like one, the rules have not been found yet.

---

## Backlog depth

**Refine one sprint ahead. No further.**

Every slice has an epic with a story list, so the shape of the whole system is visible. Only the next
sprint's stories get full acceptance criteria. Writing slice 7's criteria now would mean writing them
against assumptions Karim has not been asked about — inventing eleven sprints of business rules in a
single sitting, which is precisely the failure this process exists to prevent.

---

## The rules that override the process

If this file and `CLAUDE.md` disagree, `CLAUDE.md` wins. If a story and `spec.md` disagree,
`spec.md` wins and the story is a bug.

**And the one that matters most, from `agents.md`:**

> An agent that invents a business rule to fill a gap is the single most expensive failure mode in
> this project — the invention is always plausible, which is why it survives review.

Raising the question costs a message. Guessing costs a rebuild.
