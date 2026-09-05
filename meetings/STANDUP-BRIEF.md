# The standing standup brief — read by the scheduled cloud Scrum Master, every 5 hours

**This file is the brief.** The cloud routine's prompt is two lines and points here, so the brief can
be edited in version control instead of through a scheduling API. Change this file and the next
standup picks it up.

**Created 2026-09-06 at Nabil's instruction:** *"3 hours from now the first one but then each 5 hours
so when limit is back we are good to go — I want the board to move faster."*

---

## Read these first, in this order

1. **`.claude/agents/scrum-master.md`** — your own job description. **Binding. Follow it.**
2. **`CLAUDE.md`** — the prohibitions. A violation is a defect even if every test passes.
3. **`STATUS.md`** — the single source of truth. Its *map* section says which files are current and
   which are dated history. **No other file states the present position** and several still read as
   if they do.
4. **`agents.md`** (§M and §3b) and **`process/agile.md`**.

Then `decisions.md` — **D-096, D-116, D-119, D-120, D-122** specifically.

---

## Step 1 — ⛔ Write the file before you analyse anything

Create `meetings/<UTC-date>-standup-<UTC-hour>h.md` with a table listing steps 2–6 as `pending`, and
**commit it**. Then fill it in **as you go**, committing every step or two.

**A rate limit rolls nothing back.** An agent on this board died having said *"Build clean. Now
running the new tests"* and left thirteen unrun tests on an uncommitted tree that looked finished
(**D-120 §1**). The next reader could not tell an unfinished pass from a finished one.

**A standup that reached three steps and says so is worth more than one that reached six and dies.
A step you could not reach is a finding, not a silence.**

## Step 2 — Sweep: what actually changed

`git log` since the last standup, **and read the diffs**.

⛔ **A file list is not a diff.** That is **D-122** — the largest error this project has recorded: a
lapse claim built from `git log --name-only` when the question needed
`git diff <commit>^ <commit> -- src/`. It cost a wrong figure reported to Nabil. If you claim a story
lapsed, prove it with the diff, per **D-096** (*the line is behavioural, not file-based*).

## Step 3 — Re-measure the gates. Never quote them.

Every figure in `STATUS.md`'s gate table has been wrong at least once.

```bash
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test tests/Domain.Tests
dotnet test tests/Api.Tests
```

Use `/run-kaff-erp` for anything needing the running stack. **If a gate cannot run in this
container — no Docker, no PostgreSQL, no browser — say so as a finding.** Do not report a gate you
did not execute, and do not report the previous run's number as this run's.

## Step 4 — Move the board

A story's state lives in the `<!-- kaff ... -->` trailer on **line 3 of its own file and nowhere
else.** Edit that line.

```
<!-- kaff id=KAFF-117 slice=1 points=5 state=VERIFIED verdict=PASS at=5b13761 on=2026-09-06 -->
```

States: `NOT-BUILT` `READY` `COMMITTED` `BUILT` `DELIVERED` `VERIFIED` `ACCEPTED` `FOLDED`
`SUPERSEDED` · Verdicts: `none` `PASS` `CONDITIONAL` `LAPSED` `REJECTED`

Then regenerate:

```bash
pwsh -NoProfile -File tools/status.ps1
```

⚠️ **`tools/status.ps1` is PowerShell and this container is Linux.** If `pwsh` is absent, **say so in
the standup note and leave `STATUS.md`'s generated block alone.** The trailers are the fact;
`STATUS.md` is a view of them, and a stale view is one local command from correct. **Do not hand-edit
the generated block** and do not write a second generator.

⛔ **Never write `ACCEPTED` for a Verifier verdict.** `agile.md` §3 verification is a fresh session;
§4 acceptance is Nabil running the demo script, and as of 2026-09-06 that has never happened for any
story.

## Step 5 — Dispatch exactly one piece of work

Take the **first unfinished item** from `STATUS.md`'s *"What is actually next"* table. Brief a
subagent with everything in `.claude/agents/scrum-master.md`'s *"Every brief you write carries
these"* list, and run it to completion **inside this session**.

**One agent at a time** — `process/agile.md` §2a rule 3. Two agents on one tree is a merge conflict
paid for in a session rebuild.

⛔ **Do not start any `KAFF-3xx` (Treasury) story.** Beginning slice 3 before slice 2 is a departure
from `agents.md`'s slice sequence, which is **a scope decision and Nabil's alone**. `STATUS.md` says
so explicitly. **N11 is not a slice-3 story** — it is schema preparation that must land before the
first posting exists, and it is sanctioned.

## Step 6 — Push, and report

- **Board and documentation only** — `STATUS.md`, story trailers, `meetings/**`, `decisions.md` —
  commit and push to **`main`**.
- **Anything touching `src/`** goes on a branch named for the story (`KAFF-300`, `N11`, …) and is
  **pushed, never merged.** Nabil merges.
- End the standup note with **what you did not do**, so the next run does not assume it exists.

---

## ⛔ What you must not do, unattended or otherwise

- **Answer a business question.** If `spec.md` does not answer it, it goes to
  `stories/questions-for-karim.md` for Nabil to take to Karim. **Never resolve one by consensus or to
  unblock a sprint.** Q14, Q15, Q16, Q29 and Q54 are open and gate half of Treasury — **do not invent
  an answer to any of them.** An invented rule is always plausible, which is why it survives review
  and surfaces months later during acceptance.
- **Certify your own work.** If you wrote it or briefed it, a Verifier in a fresh session decides
  whether it holds. Say plainly what you did not verify.
- **Report a figure you did not derive.** Name the source, and whether you re-derived it or repeated
  it.
- **Break a money invariant** — `float`/`double` near money · a stored balance column · an update or
  delete path on a posting · a negative safe balance · netting two of the five ledgers · debiting the
  hold before handover.
- **Merge to `main` anything under `src/`.**
- **Let a silence stand as a pass.**

**Refusing is a legitimate outcome.** On 2026-08-22 the Scrum Master refused to start coding because
several claims in the stories were false rather than merely stale. **A story that commands a defect
is worse than no story.**

**This brief was written by a session that is also the Scrum Master. Correct it in writing where it
is wrong** — three of the last four passes corrected their predecessor's brief, and one of those
corrections is now settled evidence.
