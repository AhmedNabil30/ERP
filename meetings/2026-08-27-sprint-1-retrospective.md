# Sprint 1 — retrospective · 2026-08-27

About the process, not the code. The code's state is `meetings/2026-08-27-sprint-1-close.md`.

Sprint 1 built fourteen of fifteen stories and produced seven defects worth learning from. This is
not a list of what went well. Six of the seven were found, most of them by checking that worked —
which is why *"verify more"* is not the lesson and this document does not say it.

---

## 1. One pattern, seven times

| The check | What it reported | What was true |
|---|---|---|
| `ck_users_subcontractor_cannot_log_in` | every case in `ChangeUserRoleTests` green | no seeded row held a credential, so the constraint was **never reached** |
| `scripts/check-citations.ps1` | *753 checked, 0 broken* | **118 citations were never parsed**; a wrapped one could not be seen |
| `/run-kaff-erp smoke` | SPA renders · RTL · Arabic — three passes | **Chromium's own error page**: Arabic, RTL, 176 characters |
| CI and the deploy pipeline, before 2026-08-25 | no failures, for weeks | **never executed** — six defects surfaced within hours of the first run |
| `decisions.md` D-082 §4 | *"structurally unreachable, so no test could fail"* | **reachable**; `V-26-A` was the door, and the premise was cited in three stories |
| KAFF-101a and KAFF-103 | `ACCEPTED` | accepted at a tree that stopped being HEAD **eleven hours later** |
| `TC-1-011`, `TC-1-042` | a specific `messageKey` asserted | the key was decoration; both cases **fail against correct code** |

The shape is the same every time, and it is not "we missed something":

> **A passing check and an absent check produced identical output.**

Zero failures and zero coverage look alike. Every row above is a place where nothing in the system
could tell us which one we had.

---

## 2. What worked, and exactly where it stops

**Watching a test fail before trusting it.** Every story from KAFF-112 onward did it — break the
code, confirm red *for the right reason*, restore. It is why `AC-114-E` surfaced a 500 rather than a
clean 403 (an unauthorised caller reaching `SaveChangesAsync` — D-067's shape), why the
password-before-lockout ordering is held by measurement rather than assertion, and why the Verifier
could confirm findings instead of arguing them.

**Its blind spot is precise, and both HIGH defects lived in it.** The practice proves *the assertion
can fail*. It does not prove *the setup reaches the rule*.

`V-26-A` survives it perfectly: mutate the endpoint, the test goes red, the practice reports success
— while no seeded row ever held a credential, so the constraint half was never exercised in either
direction. `V-26-B` survives it the same way, one level up: the endpoint sat **outside** the gate
whose tests everyone was mutating.

The practice is not wrong. It is scoped to the code the test names, and both defects were in what the
test did not name.

---

## 3. Four changes

**1. Mutate the rule, not only the route.** Drop the database constraint. Delete the domain guard.
If nothing goes red, nothing covers it — however many tests pass. Every guard in `Domain/` and every
constraint in PostgreSQL should have been seen to fail at least once.

**2. Every tool reports what it skipped.** A checker that says *"N checked"* must also say *"M
unparsed"*. `check-citations.ps1` could not report its own blind spot, so the number stayed
reassuring for as long as the gap existed. The wrap is fixed; the `@`-less form is still unreported
and is routed as `SM-32` rather than left as a known-and-tolerated silence.

**3. A verdict names the tree it judged.** An acceptance is a claim about a commit. When a later
commit touches that story's files, the acceptance lapses and must say so out loud. Otherwise
`ACCEPTED` decays silently — as it did here, in eleven hours, to two stories nobody re-examined
because the word had not changed.

**4. A self-sealing argument needs a demonstration.** *"This cannot fail, therefore no test is
possible"* justifies the missing check with the very assumption the check would have tested. D-082 §4
was right in its conclusion and false in its premise, and three stories cited it. Fault-inject, or
write plainly that the property is unverified. Both are honest; the argument alone is not.

---

## 4. What we are deliberately not changing

Named so nobody re-opens them as improvements.

- **The blanket `403` + `errors.auth.forbidden`** (D-080). Permanently tempting to make specific,
  refused on disclosure. D-086 built the mechanism that would make it easy — the refusal is about
  disclosure, not difficulty, and that has to be restated every time it comes up.
- **No session table** (D-051 N5). Sign-out clears a cookie; a captured token stays valid to its
  expiry. That is a recorded trade, not a defect, and `AC-102-B` asserts it on purpose.
- **Per-story, per-product commits.** Five agent deaths today — a spend limit, a session limit, two
  dropped connections and a certificate failure. Nothing was lost, because everything finished was
  already committed. This is the cheapest discipline in the project.

---

## 5. About ourselves

**§M was adopted after seven spend-limit deaths in four days** and it held: the mechanical sweep ran
on a cheap model against a dictated table, and the judgement work did not. The sweep also outlived
the session that dispatched it, which was luck, not design — but it argues for delegating mechanical
work early rather than late.

**The orchestrating session put wrong facts into two briefs**, and both were caught by the agent
receiving them: D-071 cited where D-061 was meant, and *"three stories are not accepted"* when the
true answer was five. Neither cost anything, because `agents.md` principle 7 puts the invitation to
correct the brief in every one. A brief is a claim about the world and ages exactly like a story
does — SM-29 applies to instructions, not only to documents.

**The most valuable single behaviour this sprint was refusal.** Backend refused to rewire a shared
gate from inside a 3-point story. QA refused to write a case for a rule nobody had made. The Verifier
refused to accept three stories. The Scrum Master refused to treat *"close sprint 1"* as a ruling on
scope. None of those produced code, and each of them prevented an invented rule from becoming real
— which is the failure this project is least able to detect after the fact.
