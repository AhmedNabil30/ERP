# Brief — Scrum Master, scheduled run 2026-09-04 05:00 Africa/Cairo

Written by the coordinating session at Nabil's instruction. **You are a scheduled cloud agent with a
fresh checkout and zero prior context. This file is your brief.**

---

## Read first

`CLAUDE.md` · `agents.md` (the slice sequence, §3b Scrum Master, §M model policy) ·
`process/agile.md` (SM-29, SM-30, SM-31, SM-33, the Definition of Ready) · `stories/backlog.md` ·
`stories/questions-for-karim.md` · the newest files in `meetings/` and `qa/slice-1/` ·
`decisions.md` from **D-100** to the end.

## ⚠️ You cannot run the stack

Cloud sandbox — no .NET, no PostgreSQL, no Angular, no Docker. **Never state a build, test, smoke or
citation figure you did not produce yourself.** Quote an earlier figure only with its date and its
source file. This is document work.

## Deliver — then commit and push to `main`

1. **`meetings/2026-09-04-sprint-1-final.md`** — finalize sprint 1.
2. **`meetings/2026-09-04-sprint-2-planning.md`** — sprint 2 planning.
3. **Bring `stories/backlog.md` current.** It still reads *"Sprint 2 — scope not yet locked"* and
   records none of the recent commits. That is the staleness this project keeps catching elsewhere.

Commit per item with `git commit -F <file>` from a scratchpad file — heredocs and here-strings break
in this shell.

---

## State of play — verify against the files, do not trust this summary

- **`agents.md` defines slice 1 as "auth, roles, assignment, audit, *Client master*."** Sprint 1
  committed **15 of slice 1's 27 stories** and deferred 10, **including every client story** —
  KAFF-119, 120, 121, 123, 124. Client master was never picked back up.
- **Nabil raised this himself** — *"we already had a roadmap, why are we not moving according to the
  plan?"* — and his standing instruction is **"always go with the plan."**
- **All five client stories were refined on 2026-09-04 and are now `Ready`.** None is built. No client
  endpoint exists.
- **Projects are slice 4, not slice 1.** No slice-1 story creates one. An empty project list is the
  plan working as written, **not a defect**. Do not let anyone build a project endpoint to make a demo
  look fuller.
- `Project.Create` requires a `ClientId`, so Client master also gates slice 4.
- Slice 1's gate is *"permission tests pass"* and they do. **Nine slice-1 stories remain unbuilt** —
  104, 115, 117, 118, and the five client ones.

## ⚠️ Nabil's instruction on blocked questions — this overrides the usual rule

`agents.md` §3b and `CLAUDE.md` normally forbid you from resolving a business question. **For these
two meetings Nabil has explicitly lifted that**, in his words: *"any blocked questions, answer it in
the meeting without waiting for Karim — you will just mention it at the end."*

So: **answer them, and do it in a way that stays reversible.**

- Record each answer as **PROVISIONAL — decided by the Scrum Master, pending Karim**, with the date.
- Give the reasoning and **the alternative you rejected**, so a later ruling can reverse it cheaply.
- **List every one of them together at the end of each meeting file**, under a heading Nabil can hand
  to Karim as-is.
- **Do not mark any of them `ANSWERED` in `stories/questions-for-karim.md`.** A provisional answer is
  not a ruling, and the register is where the difference must survive.
- **One exception you must not decide: anything that would be unbackfillable if wrong** — a column
  never written, a number burnt, a posting shape. Flag those and leave them open. `CLAUDE.md`'s
  prohibitions are not on the table either: no stored balance, no posting edit or delete, no netting
  of the five ledgers, the hold only grows.

## The open questions

Nabil's: **KAFF-118's cut** · **Q56** (staff → subcontractor: refuse, or clear the credential) · the
**`mustChangePassword` reach**, where `AC-106-H` and `AC-105a-C` contradict each other in committed
text · **Q54's retention period** (Karim's) · **`AC-125-C`**, which the Verifier explicitly did not
accept as satisfied · **may the client-code sequence contain gaps?** (a sequence is non-transactional,
so a failed save burns a number — the mechanism is reversible, burnt gaps are not; **this is the one
that looks unbackfillable, treat it accordingly**) · plus two from the Architect: whether proceeding
past a duplicate warning needs a typed reason (batch with Q35), and whether D-049 ruling 8 covers
*editing* a phone or only registering one.

## For sprint 2 planning

- Propose a commitment; **do not declare one. Scope is Nabil's lock.**
- The plan's own next work is **Client master**, build order **KAFF-119 first and alone** — it decides
  what the audit trail records for a duplicate.
- Estimate honestly and say what each story depends on.
- **Refusing to commit is a legitimate outcome** if the stories are not Ready.

## House style

**Short. Summarised. No restating what a reader can see.** Nabil asked for economy in as many words.
Cite `path -> Identifier`, never a line number (SM-31). Where a claim has aged, correct it loudly
rather than rewriting history — that is SM-29's own practice.

Verify one thing before KAFF-119 is ever pulled, and record that it is unverified: **that
`EnsureCreated` materialises the model-declared sequence.** It was reasoned from EF's model differ,
never watched, and its failure mode is a loud `42P01` on the first registration.
