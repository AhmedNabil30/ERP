# Sprint 1 refinement — Foundation

**Date:** 2026-08-20
**Run by:** Scrum Master
**Present:** BA · UX · QA · Architect · Backend · Frontend
**Slice:** 1 — auth, roles, assignment, audit, Client master
**Gate (`agents.md`):** permission tests pass
**Scope reviewed:** 25 stories, 89 points

This is the first refinement under `process/agile.md`. The rule it runs on: every answer lands in one
of three buckets — *answered by `spec.md`*, *answered by `decisions.md`*, or **answered by nobody**.
The third bucket is the output of the meeting.

---

## 1. Where the sprint actually stands

The BA's backlog reports **14 Ready stories, 43 points**. QA's traceability pass found that number is
not true as stated:

> **F-21 — six `Ready` stories depend on a `BLOCKED` story.** Five of them depend on KAFF-119
> (register a client), which is blocked on Q8 and Q9.

**Corrected position: the committable sprint is smaller than the backlog claims.** The Scrum Master's
Definition of Ready has a line for this — *"not `BLOCKED` on an open question"* — and a story whose
dependency is blocked is blocked, transitively. `backlog.md` needs that column recomputed before
anyone commits to a number.

**Action SM-1 → BA:** recompute Ready/BLOCKED transitively and correct `backlog.md`. Until then no
sprint scope is committed.

**What is genuinely startable today** is the permission mechanism work — which *is* the slice gate —
because it depends on no unanswered question. That is the right thing to build first regardless.

---

## 2. "What do you not know?" — the three buckets

### Bucket 1 — answered by `spec.md`, cite it and move on

| Raised by | Question | Answer |
|---|---|---|
| Backend | May an individual client carry a withholding category? | **No.** §6.7: *"Individual clients do not withhold."* This is why KAFF-120 is `Ready` and marked DEFECT rather than blocked — the spec answers it and the code does not enforce it. |
| Frontend | Do site engineers confirm site expenses? | **No.** §8: *"entered by Finance or Admin, not the engineer."* See finding F-04 below — the catalogue does not currently enforce this. |
| QA | Is a subcontractor ever a login? | **No.** §9: *"record only, no login."* Refused before the catalogue is consulted. |

### Bucket 2 — answered by `decisions.md`

| Raised by | Question | Answer |
|---|---|---|
| Frontend | Does HR need a project assignment to staff a project? | No — D-044 §3, global reach. |
| Backend | Which accounts carry a hard floor? | Three: safe, client advance, عهدة — D-044 §8. The hold, firm advance and تشوينات do not, and D-044 records what that gives up. |
| UX | Do متعثرة / تم تأجيلها become project states? | No — D-044 §7, health tags. A struggling project stays `Active`. |
| Backend | Two decimals or four? | Four stored and computed, two displayed — D-044 §6. |
| QA | Who may create a user? | Owner only, company-wide — D-044 §1. |

### Bucket 3 — answered by nobody

**Four stop slice 1.** They are in `stories/questions-for-karim.md` with the wording to use.

| # | Question | Blocks |
|---|---|---|
| **Q2** | Password rules — length, complexity, lockout, and who sets the first one | KAFF-100, 101, 103, 104 |
| **Q8 / Q9** | `Client.Code`, and what happens when a phone is already on file | KAFF-119 **and the five stories behind it** |
| **QA-2 / Q-UX-3** | What may HR see of a project it must staff? | KAFF-105 AC3, HR navigation |
| **Q1** | Who reads the audit trail? | KAFF-117 |

**Q1 deserves a sentence of its own.** `AuditRead` is granted to the Owner on an assumption, and it
has been `Unresolved` in the catalogue since slice 0. From slice 3 that trail records every movement
of money. **The only person who reaches every project is currently also the only person who can read
the record of what he did there.** That is a governance question, not a permissions question.

**Nothing in bucket three was resolved in the room.** `agents.md`: consensus among agents is the most
confident possible way to be wrong.

---

## 3. Findings — things that are wrong now, not questions

QA produced 23. These are the ones that change what happens next.

### 3.1 Fixed during the meeting

**F-10 / F-11 — company-wide permissions were never revalidated.** `IProjectAccessPolicy` is the only
thing that checked a caller against the database, and the handler called it only when the request
named a project. Every company-wide permission was therefore decided from token claims alone: **a
deactivated Owner kept `UserManage`; a deactivated Finance user kept `TreasuryPostCompany`, which
moves money.** Two existing tests looked like they covered revocation; both used project-scoped
routes.

Fixed — the token now supplies identity and the database supplies authority. Three tests added, and
the fix was verified by reverting it and watching five tests go red. **D-048.**

**F-12 — `spec.md` had never been updated with Karim's rulings.** `CLAUDE.md` says `spec.md` wins over
code, and `agents.md` requires the Verifier to read `spec.md` rather than the implementation. **A
Verifier doing its job correctly would have failed slice 1's gate for implementing a rule Karim
gave.** Fixed as marked amendment blocks in §6.1, §6.4, §9 and §13. **D-047.**

**F-05 — the permission catalogue's documentation contradicted its data** on `HeadOfDesign`. Comment
corrected; the data was right.

**Two spellings of تم تأجيلها** across the continuity files, in a vocabulary `CLAUDE.md` requires
verbatim. Corrected in `agents.md`.

**A domain error with no translation** — added this morning, would have rendered its own key at the
user. Fixed, and a test now fails if any domain error key is missing from either catalogue.

### 3.2 Open, and blocking a story

| # | Finding | Effect |
|---|---|---|
| **F-04** | `SiteExpenseConfirm` is granted by department with no role named, and a Site Engineer can be placed in Operations / Administrative. §8 says "not the engineer". **Third appearance of one mechanism** — D-035, D-044 §2, now this. Not exploitable today (no endpoint uses it, site expenses are slice 6), but the fix shape needs **QA-1**. | slice 6 |
| **F-06** | KAFF-109 assumes a `ChangeRole` operation. `User` has `MoveToDepartment` and no role setter. Compounded by **Q7**: a role change leaves assignment rows `ProjectAssignment.Create` would refuse to create. | KAFF-109 |
| **F-09** | KAFF-121's headline behaviour — editing a client's contact details — has no domain path. `Client` has no primary-phone setter and no name setter. | KAFF-121 |
| **F-02** | KAFF-100 treats the seeded bootstrap as decided, citing a *description* of the current state as though it were a ruling. UX says neither shape is chosen. **This is the exact failure `agents.md` names**, caught in refinement, which is what refinement is for. | KAFF-100 |
| **F-08** | Two different error keys for the same refusal, one in the story and one in the UX flows. | KAFF-120 |
| **F-19 / F-22** | Two stories assert behaviour that the UX register lists as unanswered. | KAFF-101, 121 |

**Action SM-2 → BA:** F-02, F-08, F-19 and F-22 are story defects. Correct them; do not carry them
into the sprint.

**Action SM-3 → Architect:** F-06 and F-09 are missing domain capability, not missing rules. Both are
in-scope build work — but F-06 cannot be specified until Q7 is answered.

### 3.3 Process finding

**F-01 — two question registers collided.** BA Q1 is the audit trail; UX Q1 is the first Owner. A test
case marked `PENDING Q3` was unexecutable because it did not say whose Q3. QA adopted `Q-BA-n` /
`Q-UX-n`.

**Action SM-4:** one register, in `stories/questions-for-karim.md`, is the master. UX and QA
questions merge into it with their origin recorded. Two registers is how a question gets answered and
stays open.

**And a gap the registers themselves had:** Q-UX-3 (what HR may see of a project) and Q-UX-9 (portal
clients on the staff host) **never reached the BA register at all**, and KAFF-101 was written treating
Q-UX-9 as settled. The merge is not tidying; it is the thing that stops an unanswered question being
silently answered by whoever writes the story.

---

## 4. Definition of Ready — the checklist, run

| Criterion | Result |
|---|---|
| Every AC is Given/When/Then | ✅ all 25 |
| Every rule cites `spec.md` or a D-number | ✅ — and the citations are real; spot-checked KAFF-120 against §6.7 |
| No uncited rule | ⚠️ **one** — KAFF-100 rule 3 (F-02) |
| Permissions named explicitly | ✅ |
| Money behaviour named, or stated as none | ✅ — KAFF-120 correctly declares itself money |
| Arabic strings are i18n keys | ✅ — and five new `errors.*` keys are listed for the backend to emit |
| Audit record stated | ✅ |
| QA has a scenario that fails if the rule breaks | ✅ — 215 cases, each with a "fails if:" line |
| Not BLOCKED | ❌ **11 directly, more transitively (F-21)** |

---

## 5. KAFF-116 — the one story that cannot wait

Recording *how* the actor reached a project — assignment, Owner-global, HR-global, or client-of-project
— is a column on `audit_records`, which is append-only and trigger-protected.

**It cannot be backfilled.** The rows cannot be updated, by design. If this ships after slice 3, every
audit record written before it is permanently missing the field, and the question it answers — *"how
did this person come to be able to approve that?"* — is exactly the question asked when money is
disputed months later.

Karim's rulings make this sharper, not softer: there are now **two** roles that reach projects with no
assignment row. Without this column, "Owner, globally" and "assigned on 3 June" look identical in the
record.

**Recommendation: KAFF-116 is committed to sprint 1 regardless of what else is cut.**

---

## 6. Actions

| # | Action | Owner | Before |
|---|---|---|---|
| SM-1 | Recompute Ready/BLOCKED transitively; correct `backlog.md` | BA | scope commit |
| SM-2 | Fix story defects F-02, F-08, F-19, F-22 | BA | scope commit |
| SM-3 | Specify the domain gaps F-06, F-09 | Architect | build starts |
| SM-4 | Merge UX and QA questions into the one register | BA | Nabil takes questions to Karim |
| SM-5 | Put Q2, Q8, Q9, QA-2 and Q1 to Karim — one message | **Nabil** | sprint 1 commit |
| SM-6 | Start the permission-mechanism stories now; they block on nothing | Backend | — |
| SM-7 | Settle N1 — where the access token lives in the browser | Nabil + Architect | KAFF-101 |

---

## 7. Retrospective note, carried forward

Five defects were found today, and **four share one shape: a result that looked green and was not
evidence of anything.** `dotnet test` running zero tests; a seeding collision that had merely been
lucky; an end-to-end suite passing with every test skipped; and two revocation tests that covered one
of two code paths.

None was found by running the suite. All were found by asking *"what would this look like if the
thing it checks were broken?"*

`process/agile.md` should carry that question into every refinement, and `qa/strategy.md` already
makes it a section. **Proposed addition to the Definition of Done: a new test has been observed to
fail before it is trusted.** Three times today that check found something; it costs a minute.

**Also worth stating plainly:** the meeting's most valuable outputs came from agents reading each
other's work, not their own. QA found the missing `spec.md` amendments; the BA found the untranslated
error key; UX found that HR cannot see the project it must staff. `agents.md` principle 2 — the author
never certifies its own work — held every time it was tested today.
