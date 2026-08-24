# stories/ — the Kaff ERP backlog

Written by the BA agent. `spec.md` is the business truth, `CLAUDE.md` is the rules,
`decisions.md` is why things are the way they are, `process/agile.md` is how the work moves.
**This directory is what gets built.**

---

## What is here

```
stories/
  README.md                  this file
  backlog.md                 the product backlog — one epic per slice, slices 1–9
  questions-for-karim.md     THE question register — one, merged, ordered by what it blocks
  ac-id-map.md               old positional AC label → stable AC ID, for relocking QA's test cases
  slice-1-foundation/        the sprint being refined now — full acceptance criteria
    KAFF-100-….md            one file per story
```

**`questions-for-karim.md` is the master register and there is only one.** `ux/questions.md` and
`qa/questions.md` merge into it, each question carrying its origin (`Q-UX-n`, `QA-n`) on its row —
action **SM-4**, 2026-08-20. Two registers is how a question gets answered and stays open: BA `Q1` was
the audit trail and UX `Q1` was the first Owner, a test case marked `PENDING Q3` could not say whose,
and **`Q-UX-3` and `Q-UX-9` never reached the BA register at all** — one of which was then treated as
settled by a story (findings F-01, F-22).

Numbers **Q1–Q26 keep their meaning** because stories and test cases cite them; merged questions took
new numbers from **Q27**. `qa/`'s findings (`F-nn`) are **not** questions and do not merge — they are
defects and document contradictions, owned by the BA, the Architect, Backend or Nabil.

Only the **next** sprint's stories are refined. `process/agile.md`: *"Refine one sprint ahead. No
further."* Slices 2–9 exist in `backlog.md` as titled, estimated story lists so the shape of the
system is visible — writing their acceptance criteria now would mean inventing nine sprints of
business rules in a single sitting, which is the exact failure this process exists to prevent.

---

## The ID scheme

`KAFF-<slice><nn>` — the hundreds digit is the slice.

| Slice | Range | Epic |
|---|---|---|
| 1 | `KAFF-100`–`KAFF-199` | Foundation |
| 2 | `KAFF-200`–`KAFF-299` | Masters |
| 3 | `KAFF-300`–`KAFF-399` | Treasury |
| 4 | `KAFF-400`–`KAFF-499` | Spine |
| 5 | `KAFF-500`–`KAFF-599` | Billing |
| 6 | `KAFF-600`–`KAFF-699` | Execution |
| 7 | `KAFF-700`–`KAFF-799` | Accounting |
| 8 | `KAFF-800`–`KAFF-899` | Closure, warranty, portal |
| 9 | `KAFF-900`–`KAFF-999` | Mobile and offline |

**Story IDs are permanent** (`process/agile.md`). `KAFF-101` means one thing forever, including
after the story is deleted. Test cases reference the ID, and a renumbered story silently detaches
its tests. A story that is split keeps its number and gains `a` / `b` — `KAFF-101a`, `KAFF-101b`.
A story that is dropped leaves its number burned.

**This is now in use, not hypothetical, and it has happened twice.** KAFF-101 was split on 2026-08-21
into **KAFF-101a** (the sign-in API and its cookie) and **KAFF-101b** (the screen and where each role
lands), because one half was answerable and the other was not. **KAFF-105 was split the same day**
into **KAFF-105a** (identity and permissions) and **KAFF-105b** (the project list), for the same
reason — recommended in the sprint report and approved by the Architect (D-051). Both parent files are
gone and the bare IDs `KAFF-101` and `KAFF-105` no longer resolve — **test cases citing them map to
`101a` and `105a` unless they concern the screen or the project list respectively.**

Filename: `KAFF-<id>-<slug>.md`, slug in English, lower case, hyphenated.

---

## The acceptance-criterion ID scheme

**`AC-<story>-<LETTER>`** — `AC-106-A`, `AC-113-K`, and for a split story `AC-101a-N`. The story part
is the story's number without the `KAFF-` prefix; the letter part runs `A`, `B`, `C` … and, past
twenty-six, `AA`, `AB`. Adopted 2026-08-22, refinement action **SM-23**.

**Why this exists, and it is not hypothetical.** Stories renumbered their criteria whenever one was
inserted mid-list, and nothing told the test cases. **Thirty-one QA cases had silently drifted before
anyone noticed** — whole blocks shifted (KAFF-104 ×5, KAFF-109 ×5, KAFF-110 ×6, KAFF-117 ×6,
KAFF-112 ×3, KAFF-105a ×3) — and the Definition of Done line *"every QA test case executed, with its
result recorded"* cannot be checked mechanically against a label pointing at the wrong criterion.
This is the argument `process/agile.md` already makes for story IDs (*"a renumbered story silently
detaches its tests"*), one level down, where nobody had said it.

### The rules

1. **An AC ID is permanent.** It never shifts, never gets renumbered, never gets reused. It means one
   criterion forever, including after that criterion is deleted.
2. **A new criterion is appended and takes the next unused letter for that story** — never a letter
   inserted into the sequence. `AC-106-K` is legal the moment `A`–`J` exist, whatever position the
   criterion reads in.
3. **The ID is an identity, not a position.** Criteria keep whatever reading order the story needs,
   and that order is free to change. A story whose criteria run `A B K C D` is correct, not untidy —
   **do not "tidy" it, that is the defect.**
4. **A deleted criterion's ID is retired, not recycled.** It stays in the file, in reading order,
   struck through with the date and the reason, so a reader can tell a deliberate retirement from a
   missing letter:

   ```markdown
   **~~AC-110-D~~ — RETIRED 2026-08-22** · superseded by AC-110-K · *the mandatory-reason rule was
   withdrawn to Q35*
   ```

   A retired ID is never reissued to a different criterion. Test cases citing it are dead cases, and
   that is the point — they fail loudly instead of asserting somebody else's rule.
5. **Cite the full ID, never a bare letter and never a position.** `AC-118-F`, not `AC3`, not
   `KAFF-118 AC3` — the ID already names its story. This holds inside the story too.
6. **Letters, not numbers, deliberately.** A numeric suffix would have started out equal to the
   position (`AC-106-3` for the third criterion), which leaves the exact mental model that caused the
   drift intact and working right up until the first insertion. A letter cannot be read as an
   ordinal beside a numbered business-rules table, and it retires the ad-hoc `AC4b` / `AC6c`
   insertion suffixes that were the same instinct working around the same problem. **The ceiling:
   past twenty-six criteria a story goes to `AA`. A story with twenty-six acceptance criteria is a
   story that should have been split.**

**Historical references are not relabelled.** Where a story narrates what a *previous version* of
itself asserted — KAFF-101a's old `AC1`, KAFF-103's old `AC3`, KAFF-121's old `AC2`, KAFF-110's
earlier `AC4` — the old label is left alone. Those criteria no longer exist and giving them a stable
ID would be inventing one.

**The old positional labels map to the new IDs in `ac-id-map.md`, one row per criterion.** QA relocks
its cases against that file. It is a record of a one-time relabelling and it does not grow: after
this, a new criterion is born with its ID.


---

## Status vocabulary

Five values. They live on the `**Status:**` line of the story header and nowhere else.

| Status | Meaning |
|---|---|
| `BLOCKED` | Depends on a business question nobody at Kaff has answered. **Nobody starts it.** The story names the question by its number in `questions-for-karim.md`. |
| `Ready` | Every rule cites `spec.md` or a `decisions.md` D-number, and the Definition of Ready passes. It may enter a sprint. |
| `In progress` | Claimed by a build agent in the current sprint. |
| `Done` | Verified in a session that did not write the code, and accepted by Nabil. |
| `Superseded` | Karim changed his mind. The file stays, marked loudly at the top with the ID that replaced it — never edited silently (`agents.md`, BA duties). **In use:** KAFF-122 → KAFF-416, when the withholding rate moved from the client to the contract (D-049 ruling 9). |

These are **story** statuses. They are unrelated to Kaff's five project status words
(لم تبدأ · جاري العمل · انتهت · متعثرة · تم تأجيلها), which are business vocabulary and appear
verbatim in the UI only.

---

## How a story moves from BLOCKED to Ready

```
BA finds an uncited rule
      │
      ▼
question added to questions-for-karim.md, story marked BLOCKED with the question number
      │
      ▼
Nabil asks Karim
      │
      ▼
Karim answers ──→ decisions.md gets a D-number: Decision · Why · What we rejected · Revisit if
      │
      ▼
BA rewrites the rule in the story, citing the D-number, and clears the Status to Ready
      │
      ▼
Scrum Master re-reads it in refinement against the Definition of Ready
```

**A ruling that answers half a question leaves the story BLOCKED on the other half.** Karim ruled that
a role change is refused while the user is an active *Supervisor* (D-049 ruling 6); he was not asked
about *Junior* assignments, which the domain would equally refuse to create after the change. KAFF-109
stayed `BLOCKED` on the residual (Q27). **That is the correct outcome and not an obstruction** — the
alternative is a story that looks answered and is not.

*(That example closed the next day, and it closed by **reversing**: D-051 (Q27) revokes every
assignment automatically instead of refusing the change. It is kept here because the process point
stands regardless of which way the answer went — and because KAFF-109 had to be rewritten rather than
amended, which is what a reversal costs.)*

**And block the smallest thing that is actually unanswerable.** A question about one field blocks that
field, not the story; a question about one story blocks that story, not its dependants. **This has now
been got wrong twice** — KAFF-101, where the API was answerable and the screen was not, and KAFF-105,
where identity was answerable and the project list was not. Both were blocked whole, both were split
after the fact (D-051 records the second), and in each case a `Ready` half sat idle for no reason. If
half a story is buildable, split it and block the half that is not.

**A story is BLOCKED transitively.** If its dependency is `BLOCKED`, so is it. Where a dependency is
genuinely soft, the story says `Ready` **and the soft dependency is named with the fact that makes it
soft** — not with a reading. Slice 1's only such case is KAFF-100: the Api harness issues identities
directly, so the bootstrap *shape* gates the demo and not the build, and `backlog.md` says so in those
words.

Four things this process refuses to do, all of them tempting:

1. **Nobody resolves a question in the refinement room.** Not the BA, not the Architect, not by
   consensus. `process/agile.md`: *"Consensus among agents is the most confident possible way to be
   wrong."*
2. **A question is never closed by writing a plausible answer into the story.** It is closed by a
   D-number, or it is not closed.
3. **A BLOCKED story is not started "with a TODO".** A guess in code outlives the TODO comment.
4. **A story never resolves an ambiguity by picking the reading the code already implements.** Slice
   0 shipped four invented ledger floors that survived review for exactly that reason (D-039).

---

## Writing a story

Format is fixed by `process/agile.md` and the Definition of Ready is the Scrum Master's checklist,
read aloud. Every story in this directory carries, in this order:

- header line — slice, epic, points, status, `spec.md` sections, D-numbers, dependencies
- `## Story` — one sentence, naming the actor and the outcome
- `## Business rules` — a table, **one source citation per row, no exceptions**
- `## Permissions, money, audit, i18n` — the four things the Definition of Ready demands be explicit
- `## Acceptance criteria` — **each one carrying a stable `AC-<story>-<LETTER>` ID**, Given / When / Then, including at least one that **fails if the rule is broken**
- `## Not in this story` — so the next session does not assume it exists
- `## Questions for Karim` — `None.`, or numbered and the story is `BLOCKED`

**A rule with no citation is not a rule. It is a question.**

---

## Estimation

Fibonacci, relative, and the number means **uncertainty**, not hours (`process/agile.md`).

`1` one cited rule, one endpoint, no money · `2–3` normal · `5` touches money or the permission
model · `8` touches both, or spans backend and frontend · `13` too big, split it.

**A story that moves money is never a 1.** If it looks like one, the rules have not been found yet.
