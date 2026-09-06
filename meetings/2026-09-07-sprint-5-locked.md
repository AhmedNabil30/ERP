# Sprint 5 — locked 2026-09-07

**Scrum Master, at Nabil's instruction:** *"now we want to move the board can you start delegating the
agents to work and first tell me what the expected from this sprint."*

**Baseline:** `c68ca9c`, tree clean, pushed. Slice 1 at **119 points — 0 accepted · 94 verified ·
3 lapsed · 6 built-and-unlooked-at · 16 ready.**

---

## Sprint 4 closes

| Story | Verdict | Pass |
|---|---|---|
| **KAFF-117** — the Owner reads the audit trail | ✅ **PASS** | 2026-09-06 |
| **KAFF-127** — the user-management screens | 🔶 **CONDITIONAL** | 2026-09-06 |

`GET /api/users` shipped inside KAFF-127 **against no acceptance criterion** — `V-34-A`. It is
carried into this sprint as a repair, not left as a silence.

---

## Sprint goal

> **Close slice 1's tail, and clear the runway for Treasury.**

**Not one line of Treasury code.** Beginning slice 3 before slice 2 is a departure from `agents.md`'s
slice sequence, and `STATUS.md` says in as many words that it is **a scope decision and Nabil's
alone**. It is not taken here.

What *is* taken here: **removing every obstacle in front of that decision that is not the decision
itself.** Today Treasury's first two items — `N11` and `KAFF-300` — cannot be pulled at all, because
neither exists as a story file. That is not a scope question. That is a missing artefact.

---

## The commitment

| # | Work | Agent | Pts |
|---|---|---|---|
| 1 | **`N11`** — story file. Partition `audit_records` monthly (mechanism ruled, **D-072 §3**); the retention *period* stays open as **Q54**, Karim's | BA | — |
| 2 | **`KAFF-300`** — story file. `spec.md` §15's worked example as a **failing** fixture, so slice 3's gate is visible on day one | BA | — |
| 3 | **`V-34-A`** — acceptance criteria for `GET /api/users`, derived from the rules and not from the handler | BA | — |
| 4 | **`AC-125-C`** — the criterion that was false the day it shipped | BA | — |
| 5 | **`V-34-J`** — 30 QA cases, `TC-1-264`…`TC-1-293`, for KAFF-125/126/127/128 | QA | — |
| 6 | **`F-1`** — `<bdi>` misalignment in `client-list-page.css` | Frontend | — |
| 7 | **KAFF-128** — the audit trail screen. Discharges `AC-117-I` (§2a rule 6 debt) | Frontend | **3** |
| 8 | **KAFF-104** — reset a forgotten password with an Owner-generated link | Backend | **5** |
| 9 | **Verify KAFF-101b · KAFF-118 · KAFF-125** — a fresh session, never the author | Verifier | **9** |

**17 points of story plus six repairs.**

**Held back deliberately:** **KAFF-115** (8, project team panel) — Ready, and this sprint is already
full. Capacity, not readiness.

---

## What success looks like

Slice 1 moves from **94 verified / 6 built-unlooked-at / 3 lapsed** to roughly **103 verified, zero
built-and-unlooked-at, zero lapsed** — and Treasury has two pullable story files the moment Nabil
says go.

⛔ **It does not include a single ACCEPTED point, and it cannot.** `process/agile.md` §4 is *Nabil
runs the demo script*, and no agent can perform it. **Zero of 119 points have ever been accepted.**
That number moves when Nabil moves it and at no other time.

---

## Order of dispatch — one agent at a time

`process/agile.md` **§2a rule 3**, the serial-machine rule: two agents on one tree is a merge
conflict paid for in a session rebuild.

**BA first**, because items 5–9 all wait behind story files and criteria:

1. **BA** — items 1–4. Touches `stories/**` and `decisions.md`. **Dispatched 2026-09-07.**
2. **QA** — item 5. Touches `qa/**` only.
3. **Frontend** — items 6–7. Touches `src/Web/**`.
4. **Backend** — item 8.
5. **Verifier** — item 9. A **fresh session**, strongest model, reads only, **reports and does not
   fix**.

---

## Two carried facts the sprint does not change

**1. Five cloud standups produced work that no longer exists.** The 5-hourly routine has a read-only
GitHub App install: every write route returns 403 while reads succeed. `meetings/2026-09-07-cloud-standup-recovery.md`
records what was salvaged and what was lost. **The routine is disabled** until write access is
granted. **QA's 30 cases are the one unrecoverable loss** — which is why they are item 5 rather than
a closed finding.

**2. Treasury stays blocked on four sentences from Karim.** `Q14` (which ledger takes the client's
extra 75,000 at extract 1) · `Q15` (which banks) · `Q16` (any overdraft) · `Q29` (withholding, per job
or per firm) — plus **`Q54`**, whose answer `N11` needs to be finishable rather than merely startable.

**The fastest path to Treasury is still not more building. It is four sentences.**
