# STATUS â€” Kaff ERP, the single source of truth

**Read this file first. Nothing else in the repository states the current position.**

Everything above the generated block is maintained by hand and dated. Everything inside it is
derived. If another file disagrees with this one, **this one is right and the other file is
history** â€” see *The map* at the bottom for which is which.

**Today: 2026-09-06 Â· Slice 1 (Foundation) is closing. Slice 3 (Treasury) is the next objective.**

---

## â›” Read this before quoting a number

**Nothing has ever been ACCEPTED.** `process/agile.md` Â§4 â€” *"Nabil runs the demo script"* â€” has
never happened, for any story. Every `ACCEPTED` on the old board was a **Verifier** verdict, which
`agile.md` Â§3 lists as a different ceremony with a different actor. `decisions.md` **D-119 Â§1**,
uncontested by the 2026-09-06 pass.

**DELIVERED â‰  VERIFIED â‰  ACCEPTED.** The generated table below keeps them apart on purpose.

---

## What is actually next

| # | Item | Owner | Why it is here |
|---|---|---|---|
| 1 | **N11 â€” partition `audit_records` by month** | Architect | **Overdue and blocks slice 3.** Mechanism ruled: `decisions.md` **D-072 Â§3** â€” PostgreSQL monthly partitioning, drop expired partitions. Converting a populated, trigger-protected, append-only table later is a new table + a migration + a swap. **The cost only goes up.** |
| 2 | **`V-31-A` / `V-33-F` â€” the `kaff` dev database is degraded** | Architect | Narrowed by the 09-06 pass: a **fresh** DB reports `guardsInstalled: true`, so this is a **data** repair, not a schema one. It is already a Treasury defect â€” a misfloored account that has taken a posting. |
| 3 | **`KAFF-300` â€” the Â§15 worked example as a failing fixture** | Backend | Slice 3's gate, visible on day one. Needs `spec.md` Â§6 and D-033/D-044 only. |
| 4 | **`V-34-A` â€” `GET /api/users` shipped against no acceptance criterion** | Scrum Master | It cannot be verified until criteria exist. Mine. |
| 5 | **`V-34-J` â€” no frontend story (125/126/127/128) has a QA case** | QA | `agents.md` Â§175: QA writes the cases, the Verifier executes them. |
| 6 | **`F-1` â€” `<bdi>` misalignment in `client-list-page.css`** | Frontend | A real shipped defect inside a criterion reported discharged. One line (`justify-self: start`) plus a driven browser. |

---

## â›” Blocked on Karim â€” four one-sentence answers gate half of slice 3

**The fastest path to Treasury is not more building.** Full text in
[stories/questions-for-karim.md](stories/questions-for-karim.md).

| Q | In one line | Blocks |
|---|---|---|
| **Q14** | At extract 1 the client pays an extra 75,000 for material delivered to site â€” which ledger? | The Â§15 gate itself |
| **Q15** | Which banks â€” QNB, CIB, Ø§Ù„Ø£Ù‡Ù„ÙŠ, others? | `KAFF-316`/`317`; Â§6.5 defaults client collections to bank and Â§15 cannot be reconciled without one |
| **Q16** | Does any bank account have an overdraft? | The non-negative floor's shape |
| **Q29** | Subcontractor/supplier withholding rate â€” per job or per firm? | `KAFF-318` |
| **Q54** | Retention period for audited IP addresses. The **mechanism** is ruled (D-072 Â§3); **the number is not.** | **N11** â€” and N11 is item 1 above |

Also open, not blocking slice 3's engine: **Q12**, **Q13**, **Q30**, **Q57**, `Q-N10-1`,
`Q-N10-2b`, `Q-N10-3`.

---

## The Treasury plan, and the one thing it does not depend on

**Treasury's engine does not need slice 2.** `KAFF-301`â€“`305`, `314`, `315`, `319` â€” roughly **44 of
slice 3's 120 points** â€” need `spec.md` Â§6 and D-033/D-044, nothing else. Only `KAFF-311`
(Ø¹Ù‡Ø¯Ø© â†’ employees), `KAFF-318` (subcontractors/suppliers) and `KAFF-316`/`317` (banks, Q15)
genuinely need the Masters slice or Karim.

Sequence, one agent at a time (`process/agile.md` Â§2a rule 3):
**N11** â†’ **KAFF-300** (the Â§15 fixture, failing) â†’ **KAFF-301** (post a movement, append-only).

âš ï¸ Skipping the `agents.md` slice order to start slice 3 before slice 2 is **a scope decision and
Nabil's**, not the Scrum Master's. It is not taken here.

---

## Gates, as last measured

At `947c22c`, 2026-09-06. **Re-measure rather than quote** â€” every one of these has been wrong once.

| Gate | |
|---|---|
| Build | 0 warnings / 0 errors, `-warnaserror` |
| `dotnet format` | 0 |
| Domain.Tests | 127 / 127 |
| Api.Tests | 317 / 317 |
| Frontend units (vitest) | 6 / 6 |
| SPA build, `strictTemplates` | clean |
| E2E (Playwright) | 18 / 18 â€” âš ï¸ one unexplained flake, all clean runs hit a **warm** dev server |
| Citations | 1157 / 0 / 0 |
| `Kaff:ForwardedProxyHops = 2` | âš ï¸ **unwitnessed** against real staging |

---

## Corrections to the hand-maintained tables above — BA, 2026-09-07

**Four rows above are now out of date or were never right. Corrected here rather than edited in place,
because the tables above carry a mis-encoding this agent will not make worse by rewriting their lines.**

| Row | Now |
|---|---|
| *What is actually next*, **item 1 — N11** | **It has a story: `KAFF-129`**, `stories/slice-1-foundation/KAFF-129-partition-audit-records-by-month.md`, 8 points, `NOT-BUILT`. It had none while sitting as item 1, which is why it could not be pulled. **Still the Architect's, still overdue.** The one Definition-of-Ready box left unticked is QA's case, not a decision |
| *What is actually next*, **item 3 — KAFF-300** | **It has a story:** `stories/slice-3-treasury/KAFF-300-the-section-15-worked-example.md`, 5 points, `NOT-BUILT`. **⚠️ Its "Needs `spec.md` §6 and D-033/D-044 only" is right, and its slice-3 framing needs one correction:** `agents.md` splits §15 into **two** gates — slice 3's *"the worked example reconciles"* and slice 5's *"§15 passes end to end"*. This fixture is the first, written against postings and derived balances; the billing calculators are deliberate stubs whose own header puts §15's figures in slice 5 [Verified: 2026-09-07 @ `src/Domain/Contracts/Billing/Calculators.cs`] |
| *What is actually next*, **item 4 — `V-34-A`** | **Closed 2026-09-07.** `AC-127-J`…`AC-127-N` are written into `stories/slice-1-foundation/KAFF-127-user-management-screens.md`, derived from the rules rather than the handler, with four findings recorded rather than blessed. `KAFF-127` still carries its `CONDITIONAL` verdict — **the criteria now exist for a Verifier to execute; nobody has executed them** |
| *Blocked on Karim*, **`Q14`** | **⚠️ "which ledger?" is not what `Q14` asks.** The ledger is decided — **D-034** ruled تشوينات a liability and `AccountType.MaterialAdvance` is in the catalogue [Verified: 2026-09-07 @ `src/Domain/Treasury/AccountType.cs` -> `AccountType.MaterialAdvance`]. `Q14` **confirms** that mechanic. **And it does not block the §15 gate:** D-034 requires the fixture *"failing or skipped, but present"* before slice 3 opens, so `Q14` gates `AC-300-I`'s three figures, not the story. **A separate question — how much تشوينات comes off each extract — had no `Q` number and now does: `Q58`.** §15 gives 45,000 and 30,000 and no rule that produces them; slice 5's calculator needs one and this fixture does not |

---

## ⛔ Do not quote the generated block's total — `tools/status.ps1` has three defects, found 2026-09-07

**BA, 2026-09-07, on adding the first story outside slice 1.** The generator parses the `slice=` field
of every trailer and then **never uses it**. Three consequences, all visible in the block below:

| # | Defect | Read it as |
|---|---|---|
| 1 | **`KAFF-300` is a slice-3 story counted in the slice-1 total.** The block says **132 in slice 1**. | **Slice 1 is 127.** `KAFF-300`'s **5** points are slice 3's. |
| 2 | **`KAFF-300`'s link is dead.** The row's path is hardcoded `stories/slice-1-foundation/…` for every story. | The file is at `stories/slice-3-treasury/KAFF-300-the-section-15-worked-example.md`. |
| 3 | **`NOT-BUILT` is a documented state with no bucket row.** The buckets read 0 + 94 + 3 + 6 + 16 = **119**, and the total says **132**. | The missing **13** is `KAFF-129` (8) + `KAFF-300` (5), both `NOT-BUILT`. **The buckets are not wrong; they are incomplete.** |

**Fixing it is three lines in `tools/status.ps1`** — group or filter on `$_.slice`, derive the row's
path from the story's actual directory instead of a hardcoded one, and give `NOT-BUILT` a bucket.
**`src/` and `tools/` are not the BA's**, so this is written down rather than fixed. **Routed to the
Scrum Master**, who owns the generator.

**The stories are still listed, deliberately.** This file's own rule is that *"a missing row reads as
nothing to do, and that is the failure this whole file exists to prevent."* A visible row beside a
named arithmetic defect beats an invisible story — but **the number above it is wrong until the
generator is fixed, and nobody should carry `132` to Nabil.**

<!-- BEGIN GENERATED - tools/status.ps1 - do not edit by hand -->
*Generated by `tools/status.ps1` from the `<!-- kaff -->` trailers. **Edit a story file, then re-run** — hand edits here are overwritten.*

**HEAD** `6a15187` · tree clean

## Points by slice

| | slice 1 | slice 3 |
|---|---:|---:|
| ✅ **ACCEPTED** — Nabil ran the demo script (`process/agile.md` §4) | 0 | 0 |
| 🔵 VERIFIED — a Verifier gave a verdict, and it still stands | 94 | 0 |
| ⛔ LAPSED — had a verdict; later code moved under it (D-096) | 3 | 0 |
| 🟡 BUILT — shipped, nobody independent has looked | 9 | 0 |
| ⚪ READY / COMMITTED — refined, not built | 13 | 0 |
| ⚫ NOT-BUILT — cut, and not yet Ready | 8 | 5 |
| **total** | **127** | **5** |

## Every story

| Story | Slice | Pts | State | Verdict | At | On | |
|---|---:|---:|---|---|---|---|---|
| [KAFF-100](stories/slice-1-foundation/KAFF-100-bootstrap-the-first-owner.md) | 1 | 5 | 🔵 VERIFIED | PASS | `559ac45` | 2026-08-26 | Bootstrap the first Owner through a one-time setup screen |
| [KAFF-101a](stories/slice-1-foundation/KAFF-101a-sign-in-api.md) | 1 | 5 | 🔵 VERIFIED | PASS | `559ac45` | 2026-08-27 | Sign in, and the server sets an `HttpOnly` session cookie |
| [KAFF-101b](stories/slice-1-foundation/KAFF-101b-sign-in-screen.md) | 1 | 3 | 🟡 BUILT | none | `f2b995b` | 2026-09-02 | The staff sign-in screen, and where each role lands after it |
| [KAFF-102](stories/slice-1-foundation/KAFF-102-sign-out.md) | 1 | 2 | 🔵 VERIFIED | PASS | `559ac45` | 2026-08-27 | Sign out |
| [KAFF-103](stories/slice-1-foundation/KAFF-103-set-first-password.md) | 1 | 5 | 🔵 VERIFIED | PASS | `559ac45` | 2026-08-27 | Change the temporary password on first sign-in |
| [KAFF-104](stories/slice-1-foundation/KAFF-104-reset-forgotten-password.md) | 1 | 5 | ⚪ READY | none | `-` | 2026-08-22 | Reset a forgotten password with an Owner-generated link |
| [KAFF-105a](stories/slice-1-foundation/KAFF-105a-api-me-identity.md) | 1 | 2 | 🔵 VERIFIED | PASS | `559ac45` | 2026-08-27 | GET /api/auth/me` returns who I am and what I may do |
| [KAFF-105b](stories/slice-1-foundation/KAFF-105b-api-me-project-list.md) | 1 | 5 | 🔵 VERIFIED | PASS | `-` | 2026-08-30 | GET /api/auth/me` returns the projects I reach, and how I reach them |
| [KAFF-106](stories/slice-1-foundation/KAFF-106-owner-creates-a-user.md) | 1 | 5 | 🔶 VERIFIED | CONDITIONAL | `-` | 2026-08-25 | The Owner creates a user with a role and a department |
| [KAFF-107](stories/slice-1-foundation/KAFF-107-hr-role-is-bound-to-the-hr-department.md) | 1 | 2 | ⚪ FOLDED | none | `-` | 2026-08-22 | An HR user cannot be created or moved outside the HR department |
| [KAFF-108](stories/slice-1-foundation/KAFF-108-move-a-user-between-departments.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Move a user between departments |
| [KAFF-109](stories/slice-1-foundation/KAFF-109-change-a-users-role.md) | 1 | 5 | 🔶 VERIFIED | CONDITIONAL | `559ac45` | 2026-08-27 | Change a user's role |
| [KAFF-110](stories/slice-1-foundation/KAFF-110-deactivate-a-user.md) | 1 | 5 | 🔶 VERIFIED | CONDITIONAL | `-` | 2026-08-25 | Deactivate a user, and their access ends on the next request |
| [KAFF-111](stories/slice-1-foundation/KAFF-111-a-deactivated-users-assignments.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Deactivating a user revokes their project assignments |
| [KAFF-112](stories/slice-1-foundation/KAFF-112-reactivate-a-user.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Reactivate a user, who comes back with nothing |
| [KAFF-113](stories/slice-1-foundation/KAFF-113-assign-a-user-to-a-project.md) | 1 | 5 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Assign a user to a project, with seniority for site engineers |
| [KAFF-114](stories/slice-1-foundation/KAFF-114-revoke-a-project-assignment.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Revoke a project assignment without losing who could act when |
| [KAFF-115](stories/slice-1-foundation/KAFF-115-project-team-panel.md) | 1 | 8 | ⚪ READY | none | `-` | 2026-09-02 | The project team panel is built from assignment rows, not from the access check |
| [KAFF-116](stories/slice-1-foundation/KAFF-116-audit-records-how-access-was-granted.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Every audit record says how the actor reached the project |
| [KAFF-117](stories/slice-1-foundation/KAFF-117-read-the-audit-trail.md) | 1 | 5 | 🔵 VERIFIED | PASS | `5b13761` | 2026-09-06 | The Owner reads the audit trail, and nobody else does |
| [KAFF-118](stories/slice-1-foundation/KAFF-118-every-slice-1-change-is-audited.md) | 1 | 3 | 🟡 BUILT | none | `-` | 2026-09-05 | Every state change in slice 1 writes an audit record |
| [KAFF-119](stories/slice-1-foundation/KAFF-119-register-a-client.md) | 1 | 5 | 🔵 VERIFIED | PASS | `86cc8b0` | 2026-09-04 | Register a client, with a generated code and a duplicate-phone warning |
| [KAFF-120](stories/slice-1-foundation/KAFF-120-individual-clients-do-not-withhold.md) | 1 | 2 | 🔵 VERIFIED | PASS | `-` | 2026-09-04 | An individual's contract cannot carry a withholding rate, and nor can the individual |
| [KAFF-121](stories/slice-1-foundation/KAFF-121-edit-a-clients-contact-details.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-09-04 | Edit a client's name and contact details |
| [KAFF-122](stories/slice-1-foundation/KAFF-122-corporate-client-withholding.md) | 1 | 0 | ⚪ SUPERSEDED | none | `-` | 2026-08-21 | Set a corporate client's withholding category and tax registration number |
| [KAFF-123](stories/slice-1-foundation/KAFF-123-archive-a-client.md) | 1 | 2 | 🔵 VERIFIED | PASS | `-` | 2026-09-04 | Archive a client |
| [KAFF-124](stories/slice-1-foundation/KAFF-124-list-and-search-clients.md) | 1 | 2 | 🔵 VERIFIED | PASS | `-` | 2026-09-04 | Find a client by name or phone |
| [KAFF-125](stories/slice-1-foundation/KAFF-125-staff-shell.md) | 1 | 3 | ⛔ VERIFIED | LAPSED | `8ea9258` | 2026-09-06 | The staff shell: session resolution, chrome, and role-based landing |
| [KAFF-126](stories/slice-1-foundation/KAFF-126-client-screens.md) | 1 | 8 | 🔵 VERIFIED | PASS | `-` | 2026-09-04 | The client screens |
| [KAFF-127](stories/slice-1-foundation/KAFF-127-user-management-screens.md) | 1 | 8 | 🔶 VERIFIED | CONDITIONAL | `116c08a` | 2026-09-06 | The user-management screens |
| [KAFF-128](stories/slice-1-foundation/KAFF-128-audit-trail-screen.md) | 1 | 3 | 🟡 BUILT | none | `caf9663` | 2026-09-07 | The audit trail screen |
| [KAFF-129](stories/slice-1-foundation/KAFF-129-partition-audit-records-by-month.md) | 1 | 8 | ⚫ NOT-BUILT | none | `-` | 2026-09-07 | Partition `audit_records` by month, from the start |
| [KAFF-300](stories/slice-3-treasury/KAFF-300-the-section-15-worked-example.md) | 3 | 5 | ⚫ NOT-BUILT | none | `-` | 2026-09-07 | The §15 worked example as a fixture — present and failing before anything else is built |

<!-- END GENERATED -->

---

## The map â€” which file is truth, which is history

The rule that stops this problem coming back: **only one file per fact, and history never claims a
present tense.**

### TRUTH â€” read these, they are current

| File | Is the only authority on |
|---|---|
| **`STATUS.md`** | Where the project stands today. This file. |
| **`spec.md`** | The business. `CLAUDE.md`: if code and spec disagree, **spec wins**. |
| **`CLAUDE.md`** | How to work here. Prohibitions. |
| **`decisions.md`** | Why things are the way they are. Append-only, `D-nnn`, never rewritten. |
| **`stories/slice-*/KAFF-*.md`** | One story's criteria **and its state** â€” the `<!-- kaff -->` trailer on line 3 is what `STATUS.md` is generated from. |
| **`stories/questions-for-karim.md`** | Open questions for the client. The **one** register â€” `qa/questions.md` and `ux/questions.md` merged into it (SM-31). |
| **`qa/risk-register.md`** | `RSK-nn` exposures. |
| **`process/agile.md`** Â· **`agents.md`** | The ceremonies, and who is who. |
| **`qa/slice-1/test-cases.md`** | `TC-` cases. |

### HISTORY â€” dated records. Never read these for current state.

`meetings/**` Â· `qa/slice-1/verification-*.md` Â· `qa/slice-1/story-review-*.md` Â·
`proposals/**` Â· every superseded block inside a story file.

Each is true **as of its date** and was never updated afterwards. That is correct behaviour for a
record and wrong behaviour for a status.

### âš ï¸ DEMOTED â€” was treated as truth, is not any more

| File | Now |
|---|---|
| **`stories/backlog.md`** | **State column is dead.** It drifted on KAFF-116, KAFF-108 and KAFF-113 inside one week (D-119), and a figure read off it was reported to Nabil and was wrong (D-122). Keep it for **epics, slice sequence and estimates**; take state from `STATUS.md`. |
| **`qa/questions.md`** Â· **`ux/questions.md`** | Superseded by `stories/questions-for-karim.md`. |
| **`stories/ac-id-map.md`** | Snapshot of 2026-08-24. Criteria live in the story files. |

---

## How to keep this file honest

```powershell
powershell -NoProfile -File tools/status.ps1          # regenerate after editing any story trailer
powershell -NoProfile -File tools/status.ps1 -Check   # exit 1 if stale
```

**The trailer is the fact; this file is a view of it.** To change a story's state, edit its
`<!-- kaff ... -->` line and re-run. Do not hand-edit the generated block â€” it is overwritten.

A story with **no** trailer appears as `â“ UNKNOWN` rather than vanishing, because a missing row
reads as *nothing to do* and that is the failure this whole file exists to prevent.

**States:** `NOT-BUILT` Â· `READY` Â· `COMMITTED` Â· `BUILT` Â· `DELIVERED` Â· `VERIFIED` Â· `ACCEPTED` Â·
`FOLDED` Â· `SUPERSEDED`
**Verdicts:** `none` Â· `PASS` Â· `CONDITIONAL` Â· `LAPSED` Â· `REJECTED`
