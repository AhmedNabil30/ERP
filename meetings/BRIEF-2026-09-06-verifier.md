# Brief — QA and Verifier, sprint 4 and the 48 lapsed points

**Written 2026-09-06 by the Scrum Master, at Nabil's instruction: *"deliver all to the QA team and the
verifier."*** Target: `32770e9` or later on `main`.

---

## ⚠️ Read this before you plan your run: auto-compact is OFF

**Nabil disabled context auto-compaction for this work.** Your context will not be summarised and
recovered for you — when it runs out, it runs out, and **everything you are holding and have not
written down is lost.**

This is not a warning about tidiness. **A previous agent on this board hit a session limit having
said *"Build clean. Now running the new tests"* and left thirteen unrun tests on an uncommitted tree
that looked finished** (decisions.md D-120 §1). A rate limit rolls nothing back, and the next reader
cannot tell an unfinished pass from a finished one.

**So work in this order, every time:**

1. **Create `qa/slice-1/verification-2026-09-06.md` as your FIRST action**, with a progress table
   listing every section as `pending`. The existing reports all do this — copy the shape.
2. **Write each finding into that file the moment you have it**, before moving on. Not at the end.
3. **Commit the report as you go** — every section or two. `git add qa/slice-1/verification-2026-09-06.md`
   then `git commit -F <a message file>`. Do not push.
4. **If you feel yourself running low, stop and write down what you did not reach.** A pass that
   covered six of ten sections and says so is worth far more than one that covered nine and dies.

**A section you could not reach is a finding, not a silence.**

---

## Who you are

`agents.md` §7. You read `spec.md` and the QA cases, **never the implementation, to decide what to
test.** Reading code to understand a failure is fine; reading code to decide what should be true is
not — that is how a test comes to assert what the code does rather than what the business needs.

**You report. You do not fix.** No changes under `src/`. Findings go back to the authors.

**Model: strongest.** `agents.md` §M puts a downgraded model for a Verifier task on the never list.

---

## Scope — three blocks, in this priority order

### Block 1 — sprint 4's two stories (the newest, and nobody has checked them)

| Story | What it is | Commits |
|---|---|---|
| **KAFF-117** | `GET /api/audit` — the Owner reads the audit trail, nobody else | `5b13761` |
| **KAFF-127** | The user-management screens, **plus `GET /api/users`** | `116c08a`, `8ea9258`, `a4496a8` |

**Both are unverified and both were built by agents this coordinating session briefed**, so neither
has had an independent eye on it.

⚠️ **KAFF-117 has a specific weakness you should attack first.** Its agent hit a rate limit **before
running a single test**; the suites and two mutations were run afterwards by the session that briefed
it (D-120 §1). That is closer to independent than usual and **it is not a verification pass.**

**The permission on KAFF-117 is the strictest in the system** and its unusual half is the clause
*"even for their own projects"* — an assigned Technical Office lead must be refused the trail of the
project they run. `spec.md` §9 is otherwise `role × assignment`; this one is not. **A filtered trail
for a non-Owner is a defect, not a partial success.**

**On `GET /api/users`:** it is gated `UserManage` (Owner alone), deliberately, and **`Permission.UserRead`
now has no endpoint at all.** D-055 §3 gives HR *"names and roles only"* and says that does **not**
hand HR the Owner's administration surface. Check the reasoning holds; do not treat the absence as a
gap to be closed.

### Block 2 — ⛔ the 48 lapsed points (this is the block that matters most)

decisions.md **D-119**. Twelve stories — **KAFF-100, 101a, 105b, 106, 108, 110, 111, 112, 113, 114,
116, 125** — carry verdicts given on or before 2026-08-30. **`93fa417` changed the session gate after
that**: *"Repair V-30-A: the LiveSession 'unforgeable' claim was false in six places."*
`LiveSession.cs`, `PermissionEvaluator.cs` and `ProjectAccessPolicy.cs` have all moved since.

**D-096 §1 is the rule and it is exact:** a story lapses where a later commit changed behaviour *that
story's own criteria assert*; it may be carried past a **shared-mechanism** change only where the
equivalence is **pinned by a test** — *"the test is the whole of the licence."*

**Nobody has performed that check.** The two passes since (2026-09-03, 2026-09-04) verified
**commits**, not these stories.

**What is wanted is not a full re-verification of twelve stories.** It is D-096's question, answered
per story: *did `93fa417` change behaviour this story's criteria assert, and if it is shared, is there
a test pinning the equivalence?* **Carry, or lapse, with the reason.** A story you cannot decide is a
finding.

### Block 3 — QA's own gap, and it is why this brief goes to QA as well as to the Verifier

`agents.md` §175: *"QA writes the cases; the **Verifier executes them in a fresh session**."*

**`qa/slice-1/test-cases.md` has no cases for anything built since 2026-09-04** — not the Client
master, not KAFF-117, not KAFF-127. The last three verification passes worked from story criteria
instead, which works and is **not** what the process says, and the separation exists for a reason: a
Verifier who writes its own cases writes cases it can pass.

**Report which stories have no QA case.** Writing them is QA's next job, not yours mid-pass.

---

## Five places a defect would be invisible

Chosen because each is somewhere the evidence looks complete. **Hints, not a scope limit** — a
Verifier that checks only what the author suggested is checking the author's imagination.

1. **F-1, and it is already known to be real.** `client-list-page.css` carries a `<bdi>` inside a grid
   whose text renders left-aligned under a right-aligned Arabic name. **Overflow measures 0px and every
   automated check passes.** `AC-126-A` was reported discharged on 2026-09-04 by a session that drove
   Chromium and took a screenshot. **Confirm it, and then ask the harder question: what else did that
   screenshot not see?** *"Look at the screenshot"* was followed and still missed it.

2. **A positive control that proved nothing, caught from inside itself.** The KAFF-127 agent's portal
   test asserted `body.Should().Contain("portal_client_demo")`, and renaming the account to
   `portal_client_demo_MUTATED` left the suite **green** — the substring survived. It was fixed to a
   parsed exact match. **Look for that shape everywhere**: a substring assertion is a whitelist that
   admits anything containing it.

3. **The `await` that pins nothing.** D-114 §5: deleting `await resolver.ensureResolved()` from
   `clientManageGuard` left E2E 11/11, because two guards ahead of it in the same array already
   resolve the session. KAFF-127 added frontend unit tests that claim to close this (`V-33-C`).
   **Check that the new test actually fails when the `await` goes** — and note that deleting it
   outright fails `TS6133` and runs **no tests at all**, which reads as green.

4. **`Kaff:ForwardedProxyHops` = 2 is unwitnessed against the real deployment.** The mechanism is
   tested at hop count 2; nothing in the repo can prove staging has exactly two proxies. If the number
   and the deployment disagree, **every audit row records one fixed address for every user in the
   world**, and nothing about it is visible. Previously recorded as unreachable — recheck whether it
   still is.

5. **One E2E flake, unexplained.** The first KAFF-127 run was 17/18 — the portal test timed out at 32s
   while the dev server rebuilt. Five runs since were 18/18. **Believed environmental, not proven.**
   Run the suite more than once.

---

## Three claims worth disbelieving

1. **The gate figures.** Build 0/0 `-warnaserror`, format 0, SPA clean under `strictTemplates`, Domain
   **127/127**, Api **316/316**, frontend units **6/6**, E2E **18/18**, citations **1157/0/0**. All but
   E2E were re-measured by the coordinating session at `32770e9`; **E2E is the building agent's own
   figure.** Re-run everything.

2. **"Every mutation was watched failing."** Three mutation-run false negatives were caught here in a
   single day (D-109 §3): one that did not compile ran the *previous* binaries; one revert left stale
   binaries; and **a string replacement silently missed on a CRLF/LF mismatch and reported 12/12
   green** — one step from banking *"the permission gate is not asserted."* And ⚠️ **`git checkout`
   cannot revert a mutation in an untracked file** — it silently does nothing (D-120 §3).

3. **This brief.** It was written by the session that briefed both building agents and pushed every
   commit in scope. **Correct it in writing where it is wrong** — the last pass corrected three of its
   predecessor's statements, and one of those corrections is now settled evidence.

---

## Known-open, so you do not re-report them as new

`Q57` (client-code gaps, Karim's) · `AC-125-C` (Nabil's, an unperformed check) · **`V-31-A` / `V-33-F`
— the `kaff` development database is degraded and the documented E2E path does not run against it;
the Architect owes a repair story, not another detector** · **`N11`** — partition `audit_records`
before slice 3, **still not done and slice 3 is next** · `check-citations.ps1` reads `.md` only, 80
markers in `.cs`/`.ts` unchecked · `V-33-G` (`SKILL.md` overstates the start-up refusal) · F-2 … F-5
in decisions.md, routed 2026-09-05.

---

## Deliverable

`qa/slice-1/verification-2026-09-06.md` — findings numbered `V-34-x`, severity stated, evidence given,
**no fixes applied**, and a per-story verdict for both blocks. Commit as you go. **Do not push.**

**Report back:** findings by severity · the gate figures you measured yourself · the carry-or-lapse
verdict per story in block 2 · which stories have no QA case · and anything in this brief you could
not confirm.
