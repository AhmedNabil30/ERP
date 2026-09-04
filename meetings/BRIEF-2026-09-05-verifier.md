# Brief — Verifier, slice 1: the Client master and the audit sweep

**Written 2026-09-05 by the Scrum Master, at Nabil's ruling *"Run before"*.**
**Target commit: `93a517b` or later on `main`.**

---

## Read this first: you are not a continuation of anything

**This brief is written for a session that has never seen this code.** That is not a formality — it
is the entire reason the pass has value. `CLAUDE.md`: *"If you wrote the code, you do not certify
it."* `agents.md` §7: the Verifier *"always reads `spec.md` rather than the implementation."*

**The seven stories below were all written and self-reported by one coordinating session, which also
wrote this brief.** Treat every claim in it as a claim, including the gate figures. Where this brief
says a thing was verified, the question you are answering is *"was it?"*, not *"what else?"*

**You report. You do not fix.** Findings go back to the author. `agents.md` §7.

**Model: strongest.** `agents.md` §M — *"Run a money, permission, or Verifier task on a downgraded
model to save budget"* is on the never list.

---

## What is unverified, and it is all of it

Seven stories, delivered 2026-09-04 and 2026-09-05, **none independently verified**:

| Story | What it is | Commit |
|---|---|---|
| **KAFF-119** | Register a client, generated code, duplicate-phone warning | `86cc8b0` + `01c7b3a` |
| **KAFF-121** | Edit a client's name and contact details | `1684cb9` |
| **KAFF-124** | Find a client by name, code or phone | `5e8f1ad` |
| **KAFF-123** | Archive a client | `5a9d6d9` |
| **KAFF-120** | An individual's contract carries no withholding rate | `b5c9e46` |
| **KAFF-126** | The client screens — S-011, S-012, S-013, S-014 | `e0fd5cf` |
| **KAFF-118** | Every state change in slice 1 writes an audit record | `6bcebe5` |

Plus one infrastructure change that is not a story and still changes behaviour: **`51a0c5a`**, staging
behind Caddy, which moved `ForwardLimit` into configuration and therefore touches **what the audit
trail records as the caller's IP address**.

---

## The four suites, in `agents.md` §7's priority order

**1. Money.** Slice 1 moves none — but **§6.7 is money's edge and it is in scope**: an individual
client cannot carry a tax registration number, and an individual's contract cannot carry a withholding
rate. A wrong rate makes *"collections never match issued extracts and staff invent adjustments to
close the gap"*. Assert it at the domain, through the API, and on both the create and the edit path.

**2. Permissions.** This is slice 1's acceptance gate — `agents.md`: *permission tests pass*. One test
per role asserting what it **cannot** reach, hitting endpoints directly. `qa/slice-1/permission-matrix.md`
is the matrix. The client endpoints are gated `ClientManage` (Owner, MarketingSales). **Include the
portal `Role.Client` user on every one of them.**

**3. State machines.** Archive is the one in this slice: active → archived, and every illegal
transition, including the one that must not exist at all — there is no delete path and no unarchive
path in slice 1.

**4. End-to-end.** `tests/E2E.Tests` is 11 and needs a running stack. `deploy/DEMO.md` §7 and
`.claude/skills/run-kaff-erp/SKILL.md` are the runbooks. **Note: E2E was last run at `e0fd5cf`, not
at `HEAD`.**

---

## Seven places to look first, chosen because each is where a defect would be invisible

These are the coordinating session's own uncertainties, written down rather than smoothed over. **They
are hints, not a scope limit** — a Verifier that only checks what the author suggested is checking the
author's imagination.

1. **`Q57` — the client-code sequence burns numbers on refusal.** A PostgreSQL sequence is
   non-transactional. `Client.Create` (blank name) and `Client.SetTaxRegistration` (a number on an
   individual) both run **after** `nextval` is drawn, so a refused registration consumes a code that
   will never exist — on an identifier that appears on extracts and ledgers. **Unbackfillable if
   wrong.** Confirm the behaviour empirically rather than from the note; the register has it as `Q57`,
   open.

2. **`AC-124-H` and the `ILike` escape.** Client search escapes `\`, `%` and `_` before building the
   pattern. **The author's own test for this was wrong before the code was** — it asserted that
   searching `%` returns none of its fixtures, while one fixture is named `شركة … 100% للتنفيذ`, so a
   literal `%` search *should* find it. It was rewritten. **Check the rewrite, not the story.**

3. **Reads have no audit backstop — D-110 §2.** On a write endpoint, removing `.RequirePermission`
   reddens nine of thirteen tests, because the audit constraint refuses a row with no verified actor.
   On a **read**, it reddens two, and the permission test is the *entire* control. `GET /api/clients/{id}`
   is the one payload carrying internal notes, which spec.md §12 says the client MUST NEVER see.
   **An ungated version hands every client's notes to anyone who asks and nothing else notices.**

4. **`AC-126-L` and the guard that redirected instead of refusing.** `clientManageGuard` shipped
   returning `parseUrl('/')` — which `ux/navigation.md` names as forbidden in as many words, *"a
   redirect that hides what happened"*. It was found and fixed (D-114 §3, `/forbidden` route). **The
   fix is one day old and was tested by its own author.**

5. **The `await` that no longer reproduces its own bug — D-114 §5.** Removing
   `await resolver.ensureResolved()` from `clientManageGuard` leaves all eleven E2E tests green,
   because two guards ahead of it in the same `canActivate` array already resolve the session. **The
   test pins the outcome, not the await.** Judge whether that is acceptable coverage.

6. **`Kaff:ForwardedProxyHops` — D-115 §2.** Staging now has two proxies. If the number and the
   deployment disagree, **every audit row records one fixed address for every user in the world**, and
   nothing about it is visible: the column is populated and the value is a plausible IP. The test is
   `Two_proxies_deep_the_recorded_address_is_still_the_caller`.

7. **Every absence test in the repository.** Three different answers were given to *"how does an
   absence test fail?"* this week — `AC-123-D` made to fail on purpose (D-112), `AC-120-H` written as
   a whitelist (D-114), `AC-118-H`/`I` given positive controls (D-116). **The D-116 mutation is the
   one to read**: one word making `Client` audit-exempt turned three tests red, and **two of them went
   red on the positive control rather than on the assertion they are named for.** Without it they
   would have reported a safety about an entity that had stopped writing anything at all. Ask that
   question of every absence assertion you meet.

---

## The three claims most worth disbelieving

1. **"Each criterion was watched failing under a mutation of its own mechanism."** Three mutation-run
   false negatives were caught in one day and recorded (D-109 §3): a mutation that did not compile ran
   the *previous* binaries; a revert via `Move-Item` left stale binaries so a test stayed red against
   correct source; and **a `.RequirePermission` removal that silently missed on a CRLF/LF mismatch and
   reported 12/12 green** — one step from banking "the permission gate is not asserted".

2. **"The Arabic RTL criteria are discharged."** They were checked by driving Chromium at 390px in one
   session. `AC-126-C`'s empty states, `AC-126-F`'s 409-reopens-the-warning path and `AC-127`'s
   screens do not exist yet.

3. **The gate figures themselves.** Build 0/0 `-warnaserror`, Domain 125/125, Api 295/295, format 0,
   citations 1155/0/0, all at `6bcebe5` Release. **E2E 11/11 is from `e0fd5cf`, an earlier commit, and
   is not this commit's figure.** Re-run them.

---

## Known-open, so you do not re-report them as new

- **`Q57`** — client-code gaps. Open with Karim, deliberately.
- **`AC-125-C`** — Nabil's, an unperformed check, not a defect.
- **`V-31-A` (HIGH)** — a misfloored account that has taken a posting cannot be repaired. The
  Architect owes a repair story. **Still open.**
- **`N11`** — partition `audit_records` before slice 3. **Still not done, and slice 3 is next.**
- **`scripts/check-citations.ps1` reads `.md` only** — 80 `[Verified:` markers in `.cs`/`.ts` have
  never been checked. Routed at D-110 §5, still not cut as a story.
- **Six provisional answers** in `meetings/2026-09-04-sprint-3-standup.md` §4, all pending Karim.

## Deliverable

`qa/slice-1/verification-2026-09-05.md`, in the shape of the six reports already in that folder.
**Findings numbered, severity stated, and no fixes.**

**If the pass returns defects, they are pulled ahead of sprint 4's two stories** — a fix to delivered
work outranks new work. That is already recorded in `stories/backlog.md`.
