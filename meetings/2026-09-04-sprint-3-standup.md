# Sprint 3 — standup, 2026-09-04 · the Client master

**Ceremony held 14:53 Africa/Cairo, in the coordinating session, at Nabil's instruction ("start the
meeting"). `HEAD` = `01c7b3a`, tree clean, `main` == `origin/main`.**

---

## 0. Two corrections before anything else

### 0.1 The scheduled run fired and died in twenty-two seconds

Routine `trig_01Q8S9LuzHmGjXHEXcyBNSjZ` fired at **2026-09-04T02:00:35Z**. It read the brief,
`agents.md`, `stories/backlog.md` and `process/agile.md`, and at **02:00:57Z** hit a five-hour rate
limit: *"You've hit your session limit · resets 2:30am (UTC)"* — `result: success is_error=true
turns=6 duration=11s`. **It committed nothing.** `origin/main` carries no commit from it.

**This meeting is that meeting, held eleven hours late and in a session that can run the stack.**
The routine is spent (`run_once`) and needs no disabling.

### 0.2 The brief asked for a ceremony that had already happened — SM-29, inside the brief itself

`meetings/BRIEF-2026-09-04-scrum-master.md` says *"finalize sprint 1"* and *"sprint 2 planning"*, and
names two deliverables, `2026-09-04-sprint-1-final.md` and `2026-09-04-sprint-2-planning.md`.
**Neither describes where the board is.**

| The brief says | The board says |
|---|---|
| Finalize sprint 1 | **Sprint 1 closed 2026-08-27** — `meetings/2026-08-27-sprint-1-close.md`, retrospective the same day, arithmetic corrected to 25 of 57 on 2026-08-30 (`4fe4936`) |
| Plan sprint 2 | **Sprint 2 closed 2026-09-04** — `stories/backlog.md` -> `Sprint 2 — CLOSED 2026-09-04` |
| — | **Sprint 3 opened 2026-09-04** — the Client master, 14 points, five `Ready` (`f04950e`) |

**The brief was committed at `fb80807`, which is *newer* than `f04950e` — the commit that opened
sprint 3.** So it did not merely age; it was written stale and committed after the thing that
falsified it. The coordinating session wrote both. **Recorded rather than quietly renamed**, because
SM-29 exists for exactly this and a brief is not exempt from it.

**No `sprint-1-final.md` is written.** Writing a third closing document for a sprint closed eight days
ago would be the fiction, not the fix.

---

## 1. Gates — run in this session, at `01c7b3a`

| Gate | Result | How |
|---|---|---|
| Build, Release, `-warnaserror` | **0 warnings / 0 errors** | `dotnet build -c Release -warnaserror` |
| `Domain.Tests` | **111 / 111** | `Kaff.Domain.Tests.exe`, 605 ms |
| `Api.Tests` | **255 / 255** | `Kaff.Api.Tests.exe`, 4 m 07 s |
| SM-31 citations | **1148 checked · 0 broken · 0 legacy** | `scripts/check-citations.ps1` |
| E2E | **not run today** | needs the stack up. Last figure **6 / 6**, produced by the KAFF-119 build session at this same commit, 2026-09-04 — **theirs, not this meeting's** |

`Api.Tests` moved **241 → 255**: the fourteen KAFF-119 tests.

---

## 2. The board, brought current

### KAFF-119 — **DELIVERED**, `86cc8b0` + `01c7b3a`

Sprint 3 is **5 of 14 points** on its first day.

| | |
|---|---|
| Built | `POST /api/clients/phone-check` (side-effect-free, `200` either way) and `POST /api/clients`, both gated `ClientManage`. `AuditEventKind.DuplicatePhoneAcknowledged`. `HasSequence<long>("client_code_seq").StartsAt(10001)`, migration `20260904095316_ClientCodeSequence` |
| Criteria | **`AC-119-A` … `AC-119-K` discharged**, each watched failing under a mutation of its own mechanism |
| Held | **`AC-119-L`** — Arabic, RTL, at mobile width. There is no client form. **Frontend's, and it is not a pass** |
| Verification | **None.** Built and self-reported. `CLAUDE.md`: *"If you wrote the code, you do not certify it."* Unverified until a separate session says otherwise |

**Three findings the build returned, kept because they are evidence and not decoration:**

1. **D-107 §1's sequence claim was reasoned, never watched. It is watched now, both ways.** The test
   was written before the sequence was declared and failed with the predicted
   `Npgsql.PostgresException : 42P01: relation "client_code_seq" does not exist`. The migration path —
   which a model-built schema cannot see — was checked separately against `kaff_demo`: `pg_sequences`
   reported `client_code_seq | 10001 | never drawn`.
2. **A mutation was thrown away rather than banked.** The first normalisation mutation changed the
   comparison *column* and the test stayed green, because both sides were still normalised. It is
   recorded in D-108 §3 as a mutation that proved nothing. **A summary-only reader would have counted
   a watched failure that never happened.**
3. **Removing `.RequirePermission` reddens eleven of thirteen tests, not two** — with no gate nothing
   calls `ActorVerifiedAs`, and `ck_audit_records_actor_is_named_completely` refuses the row. **An
   ungated endpoint in this codebase cannot write an audit record at all.** That coupling is §4.1's
   evidence.

### Sprint 3 remaining — **9 points, four stories**

| # | Story | Pts | State |
|---|---|---:|---|
| 2 | **KAFF-121** — edit name and contact details | 3 | `Ready`. First work is the missing `Name` / primary-phone / `Kind` setters on `Client` (D-107). **Must add `excluding` to `PhoneMatches`** — D-107 §2, deliberately not built in KAFF-119 |
| 3 | **KAFF-124** — find by name, code or phone | 2 | `Ready` |
| 4 | **KAFF-123** — archive a client | 2 | `Ready` |
| 5 | **KAFF-120** — an individual carries no withholding rate | 2 | `Ready`, **re-estimate first**. `AC-120-C/D/E/G` are already discharged by `tests/Domain.Tests/WithholdingTests.cs`; what is left rides on 121's endpoint |

**Plus `AC-119-L`, which belongs to no story in the list above.** It is KAFF-119's, KAFF-119 is
delivered, and the criterion is undischarged. It does not re-open the story; it is Frontend work that
must be scheduled or the story is accepted with a hole. **Named here so it is not lost the way
`AC-106-J` was.**

### Proposal, not a lock — **scope is Nabil's**

Pull **KAFF-121 next and alone.** It owns the shared duplicate-phone mechanism across create *and*
edit (D-107 §2), and 120, 123 and 124 all read cleaner once `Client` has setters. Then 124, 123, 120.

**And schedule `AC-119-L` with KAFF-121's form**, not after all five — one Angular client form serves
create and edit, and building it twice is the mistake.

### KAFF-118 — the question dissolved rather than being answered

`KAFF-118` was routed to Nabil as *"cutting 3 points from a locked sprint is his call"*
(`stories/backlog.md` -> `KAFF-118 — the dependency that leaves the sprint`). **Both halves of that
sentence are now false.** Sprint 1 is closed, so there is no locked sprint to cut from; and its
blocking dependency **KAFF-119 landed today**, so all six of 106, 109, 110, 111, 113, 119 exist.

**KAFF-118 is buildable for the first time and should be scheduled, not cut.** §2's finding 3 is the
argument: the audit coupling is already load-bearing in this codebase and nothing asserts it as a
sweep. **Not pulled into sprint 3** — it is slice-1 work that is now unblocked, and the pull is
Nabil's.

---

## 3. What did not move, and is owed

| Item | Owner | Note |
|---|---|---|
| **`V-31-A` (HIGH, open)** | Architect | A misfloored account that has taken a posting cannot be repaired by any supported operation. **A repair story is owed, not another detector** |
| **`AC-119-L`** | Frontend | Above |
| **`N11`** — partition `audit_records` from the start | Architect | **Deadline is slice 3, not slice 9.** Converting a populated, trigger-protected, append-only table is the thing you least want to do |
| **`KAFF-122` / `KAFF-107` broken `:digits` citations** | BA | Found at the sprint-3 refinement, flagged, not fixed. Neither story is in this sprint |
| **Local `kaff` database unbootable** | Nabil | Repair needs the trigger-disabling surgery `CLAUDE.md` forbids. Eight `kaff_test_*` plus `kaff_v30` / `kaff_design_time` still enumerated and dropped by nobody |
| Four `audit.grant.*` orphaned i18n keys | — | Kept pending a KAFF-117 judgement |

---

## 4. The blocked questions — answered PROVISIONALLY

**Nabil lifted the usual rule for this meeting**, in his words: *"any blocked questions, answer it in
the meeting without waiting for Karim — you will just mention it at the end."*

**Every answer below is PROVISIONAL — decided by the Scrum Master, 2026-09-04, pending Karim.**
**None is marked `ANSWERED` in `stories/questions-for-karim.md`.** A provisional answer is not a
ruling, and the register is where that difference has to survive.

### 4.1 KAFF-118's cut — **the question is withdrawn, not decided**

See §2. Its two premises expired. **Nothing is decided here**; a question that no longer describes
reality is retired, and the live question in its place — *when is KAFF-118 pulled* — is scope, which
is Nabil's and not on this list.

### 4.2 Q56 — staff → subcontractor, with a live credential

**PROVISIONAL: refuse the conversion while a credential is stored. Which is what is already built.**

**Why:** it is the reversible half. Nothing is lost if Karim later says *clear the credential* — the
conversion simply starts succeeding. **Rejected: (b), clear the credential.** Clearing one the Owner
did not ask to clear destroys it, and a ruling arriving afterwards cannot put it back.

**This answer changes no code and no criterion.** It records why the built behaviour is the one to
leave alone until Karim speaks.

### 4.3 The `mustChangePassword` reach — **a text repair, not a decision**

`AC-106-H` says a user with a temporary password calling *"any endpoint other than the change-password
endpoint"* is refused. **`/api/auth/me` is a second exception and has been since D-072 §2** — it
answers `200` and carries the flag. `KAFF-101a` -> `AC-101a-F` and `KAFF-103` -> `AC-103-B` already
carve it out; `AC-106-H` does not, and all of them cite D-049 ruling 4, **which names no endpoint.**

**PROVISIONAL: `AC-106-H` is amended to name both carve-outs — the change-password endpoint and
`GET /api/auth/me` — and nothing else.** Everything beyond those two stays refused.

**This asserts no new rule.** It makes committed text agree with a ruling that already exists.
**Rejected: widening the reach further** — that would invent a rule from a silence, which is what the
register exists to stop. **Owed to the BA as a one-line rewrite.**

### 4.4 Q54 — the retention period

**PROVISIONAL: no number is needed yet, and choosing none is not deferral.**

D-072 §3 ruled the *mechanism*: monthly partitioning of `audit_records`, dropping expired partitions.
**Nothing is dropped until a number exists**, and no number means today's behaviour — keep
everything — which is also the safe default for an append-only table.

**What is actually due now is N11, and it is the Architect's, not Karim's:** partition from the
start. The deadline is **before the first production rows**, i.e. before slice 3, because converting a
populated append-only table is a new table plus a migration plus a swap.

**Rejected: naming a period here.** A retention period for personal data at an Egyptian company is a
legal question with a wrong answer, and unlike the mechanism it is not needed to make progress.
**Q54 stays open and stays Karim's.**

### 4.5 `AC-125-C` — settled by looking, not by ruling

The Verifier explicitly did not accept it, correctly: *"Nabil's criterion, Nabil's call."*

**PROVISIONAL: this is not a question, it is an unperformed check.** `AC-125-C` says the four
profile-only roles — Finance, TechnicalOffice, SiteEngineer, HeadOfDesign — land on **S-005** showing
name, role and department. **Seed the demo and sign in as `sara_finance_demo`** (`deploy/DEMO.md`).
Either the screen is S-005 or it is not.

**Not decided by the Scrum Master, because it is not decidable by argument.** Listed at the end as a
five-minute step for Nabil, not as a ruling he owes.

### 4.6 A typed reason past a duplicate warning — batch with Q35

**PROVISIONAL: not required in slice 1.**

**Why:** `AuditEventKind.DuplicatePhoneAcknowledged` already records *who, when, and which client was
matched* — the subject of the event **is** the matched client. What is absent is only *why*. Making it
mandatory now would set the precedent for **every rejection gate in slice 5** (Q35's own note says
so), on a guess.

**The irreversible edge, named rather than hidden:** reasons not collected between now and a ruling
are lost, and cannot be backfilled. **It is safe today only because there is no production data** —
`kaff_demo` holds the only clients that exist. **That safety expires at go-live, not at Karim's
convenience.** If this is still open then, it must be closed before the first real client is
registered.

**Rejected: mandatory now.** Reversible in the cheaper direction — adding a required field later is
additive; removing one that was wrongly demanded is not free either, but the data cost is symmetric
and the precedent cost is not.

### 4.7 Does D-049 ruling 8 cover *editing* a phone?

**PROVISIONAL: yes — one mechanism, create and edit alike.**

**Why:** D-107 §2 already hardened it into a shared mechanism — `acknowledgedDuplicatePhone` is on
**create and edit** — and KAFF-121 already infers it (QA finding **F-19**). Answering *yes* changes
nothing that is built.

**Rejected: edit refuses outright.** That is an asymmetry between create and edit that nobody asked
for. **Reversible:** it is one mechanism in one place, so a later *"edit must refuse"* is one change,
not a sweep.

### 4.8 ⛔ The client-code sequence and gaps — **LEFT OPEN. This one is not mine to answer.**

**A burnt number is unbackfillable.** A PostgreSQL sequence is non-transactional: a rolled-back insert
consumes a value and `C-10002` never exists, on a code that appears on extracts and ledgers. That is
the brief's own stated exception — *"anything that would be unbackfillable if wrong"* — and it is
being honoured rather than argued around.

**It had no `Q` number in the register. It has one now: `Q57`**, added open, unanswered.

**What the build already knows, and it raises the price:** the mechanism is reversible — `nextval` is
one expression in one handler — but **drawing last is not complete.** Two domain rules run *after* the
draw and each burns a number: a blank name in `Client.Create`, and a tax registration number on an
individual in `Client.SetTaxRegistration`. **Closing them means restating two domain rules inside the
handler** — the copy that drifts from the entity every other caller uses.

**So "unbroken codes" costs more than D-107 assumed.** Karim should be asked knowing that.

---

## 5. For Nabil — the list to hand over as-is

**Provisional answers, all reversible, all pending Karim.** Reverse any of them at no cost today.

| | Question | Provisional answer |
|---|---|---|
| **Q56** | Staff → subcontractor with a live login | **Refuse.** Already the built behaviour; the reversible half |
| **`mustChangePassword` reach** | `AC-106-H` vs `AC-105a-C` | **`AC-106-H` amended** to name `GET /api/auth/me` as the second carve-out. A text repair; asserts nothing new |
| **Q54** | Audit retention period | **No number needed yet.** The due item is **N11** and it is the Architect's. Q54 stays open, stays Karim's |
| **Typed reason past a duplicate warning** (with **Q35**) | | **Not required in slice 1.** Safe only while there is no production data — **must close before go-live** |
| **D-049 ruling 8 and editing a phone** | | **Yes, one mechanism.** Changes nothing built |
| **KAFF-118's cut** | | **Withdrawn** — both its premises expired. It is buildable now |

**Two that are yours and are not answered here:**

- **⛔ `Q57` — may the client-code sequence contain gaps?** Left open on purpose: a burnt number cannot
  be backfilled. **And it costs more than D-107 assumed** — §4.8.
- **`AC-125-C`** — not a ruling, an unperformed check. Seed the demo, sign in as `sara_finance_demo`,
  and see whether the screen is S-005.

**One scope decision, and only you make it:** what gets pulled next. The proposal is **KAFF-121
alone**, with `AC-119-L`'s form built alongside it.
