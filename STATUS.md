# STATUS — Kaff ERP, the single source of truth

**Read this file first. Nothing else in the repository states the current position.**

Everything above the generated block is maintained by hand and dated. Everything inside it is
derived. If another file disagrees with this one, **this one is right and the other file is
history** — see *The map* at the bottom for which is which.

**2026-09-07 · Slice 1 is CLOSED. Slice 2 (Masters) opens.**

---

## ⛔ What "slice 1 closed" does and does not mean

**Closed is not done, and the difference is 33 points.**

Nabil closed slice 1 on 2026-09-07. That is a legitimate scope decision and it is his to make. What
it changes is that **slice 1 takes no new build work**. What it does **not** change is the state of
the code, and the board says so rather than rounding it up:

| | pts | |
|---|---:|---|
| 🔵 **Verified** | **103** | a Verifier gave a verdict and it still stands |
| ⛔ **Lapsed** | **3** | KAFF-125 — confirmed on real diffs 2026-09-07, **and it could not be lifted** |
| 🟡 **Built, nobody independent has looked** | **0** | cleared 2026-09-07 by the `V-35` pass |
| 🔻 **Deferred, never built** | **21** | KAFF-104 · KAFF-115 · KAFF-129 → **slice 1b** |
| ✅ **ACCEPTED** | **0** | |

**The `V-35` pass, 2026-09-07 — the first independent eye on any of it.** KAFF-118 **PASS** (every
mutating endpoint driven individually; each wrote exactly one record, every read and refusal none) ·
KAFF-101b **PASS** (all eight criteria driven for the first time) · KAFF-128 **CONDITIONAL** ·
KAFF-125 **LAPSED stands**.

**Nothing has ever been ACCEPTED.** `process/agile.md` §4 is *"Nabil runs the demo script"* and there
is no record of it happening, for any story, ever. **Zero of 127.** Closing a slice does not accept
it, and no agent can perform §4.

⚠️ **3 points still carry no standing verdict** — KAFF-125, and the `V-35` pass **could not lift it**:
the rewritten `AC-125-C` cannot be executed by anyone. That is sprint 6 item 3, and it is a criterion
problem, not a code problem.

### The deferred 21 points are carried, not cancelled

**`slice 1b`** is where they live. Nothing was deleted; each story keeps its file, its criteria and
its trailer, and `state=DEFERRED` says exactly what happened to it.

| Story | Pts | Why it was not built |
|---|---:|---|
| **KAFF-104** — reset a forgotten password | 5 | Committed to sprint 5; the agent died on its first tool call at a session limit |
| **KAFF-115** — project team panel | 8 | Held back at sprint-5 planning for capacity, never pulled |
| **KAFF-129** — partition `audit_records` monthly | 8 | Cut 2026-09-07. **Its retention period is `Q54`, still Karim's** |

⛔ **KAFF-129 is the one with a deadline attached.** D-072 §3 rules monthly partitioning of
`audit_records`, and the cost rises the moment Treasury writes its first posting: converting a
populated, trigger-protected, append-only table is a new table plus a migration plus a swap.
**It must land before slice 3 opens, not before slice 3 finishes.**

---

## Slice 2 — Masters. What has to happen before anyone builds

**Gate:** *Excel import works.* **Epic:** catalogue, أبواب, employees, workers, subcontractors,
suppliers.

⛔ **Two corrections to what this file said on 2026-09-07, both found by the BA on 2026-09-08 and
both mine:**

**1. Slice 2 is thirteen stories and 48 points — `KAFF-200`…`KAFF-212`.** This file listed six and
called it the slice. It was a **prefix**, taken from a partial read of `stories/backlog.md`.

**2. ⛔ "Slice 2 needs none of Karim's questions" was FALSE.** `stories/backlog.md` has said
*"Blocked by: `Q12` and `Q13` — due before this slice opens"* since the slice was first estimated,
and **`Q12` is open.** It decides whether the Owner keeps `CatalogueManage` / `BabManage`. I checked
only this file's own seven-row question table and reported the absence as a fact about the slice.
**Slice 2 was opened on a claim that was not true**, and the BA raised it rather than working around
it.

### Refined 2026-09-08 — six of thirteen, and **every one is `NOT-BUILT`**

`READY` is a claim that the Definition of Ready is met, not a default. **None of the six meets it.**

| ID | Pts | Fails the Definition of Ready on |
|---|---:|---|
| **KAFF-200** import the catalogue from Excel, all-or-nothing | 5 | rule cites a **backlog title**, not a spec or D-number (`Q61`) · blocked (`Q60`, `Q61`) · no QA `TC-` |
| **KAFF-201** a re-import is a deliberate, reviewed act | 2 | blocked on `Q62`, **at the story's centre** · no QA `TC-` |
| **KAFF-202** create and edit a catalogue item | 3 | blocked (`Q12` only) · no QA `TC-` — **closest to Ready; raises no question of its own** |
| **KAFF-203** find an item by code or description | 3 | blocked (`Q12`) · no QA `TC-` |
| **KAFF-204** the `Bab` tree and its default markup | 5 | blocked (`Q12`) · no QA `TC-` |
| **KAFF-205** re-parent a `Bab`, move an item between أبواب | 3 | blocked (`Q12`) · no QA `TC-` |

**Not refined — `KAFF-206`…`KAFF-212`, 27 points:** archive an item, employees, the two populations,
workers, engagement history, subcontractors, suppliers. `KAFF-211`/`212` are **additionally blocked on
`Q29`**, and the backlog's carry-note warns the worker phone dedup must **not** inherit D-049 ruling
8 — *"Do not extend the ruling; ask."*

⛔ **A defect found during refinement, routed to Backend, not a question:** **`Bab.SetParent` has no
cycle check.** It refuses self-parenting only, so **A→B→A is accepted** and neither node is reachable
from a root, while `spec.md` §2 says *tree*. `AC-205-C` is the criterion and **it is red today**.

*(`stories/backlog.md` remains the authority on epics, the slice sequence and estimates, and on
nothing else.)*

---

## Sprint 6 — the order, and why verification is first this time

**Sprint 5 put the Verifier last and the budget never reached it.** *Sequencing is not a plan when
the last item is the one that always gets cut* — and the cut item was that sprint's whole purpose.
It went first this time and **reached every story**.

**The list below is what the `V-35` pass produced, and it is worse than what it replaced.** Verifying
four stories did not shorten the sprint; it turned an unknown into eight known things, two of which
are now closed. **That is what a verification pass is for**, and it is the argument for never
scheduling one last again.

| # | Work | Owner |
|---|---|---|
| 1 | ✅ **DONE 2026-09-08 — `V-35-K` closed.** `ci.yml` now seeds between the API health check and the SPA server. Watched both ways in CI's own shape: empty database **7/25, exit 2**; seeded **25/25, exit 0**. The seed script runs unmodified on pwsh 7 / Linux, and `POST /api/setup` is **not** gated to `Development`, so Owner bootstrap works under `Staging`. **No test was weakened.** D-125 | Backend / CI |
| 2 | ✅ **DONE 2026-09-08.** The survivor that was green for the wrong reason now carries a positive control. `E2ESession.AssertPortalAccountExistsAsync` was **private to `UserScreenTests`** — that is why the later suite went without it; **moved, not copied**. A/B on the same empty database: pre-repair **PASSED having tested nothing**; repaired **FAILED** in the control | Backend / CI |
| 3 | **`V-35-F` — the rewritten `AC-125-C` cannot be executed by anyone.** This is why KAFF-125's lapse could not be lifted, and it is not the code's fault | BA |
| 4 | ✅ **DONE 2026-09-08 — `V-35-G` closed.** `landingFor` takes the `Session` and looks the landing up in a **permission → landing table**; `navLabelKeyFor`/`navPathFor` follow it. The nine-case `switch (role)` is gone. Mutation watched: swapping the table for the equivalent role switch reddens **3 of 7** — the tests pin the *mechanism*, using sessions the two designs answer differently, not the nine outputs | Frontend |
| 5 | ✅ **DONE 2026-09-08 — `V-35-I` closed**, and it found a defect in `V-35-I` itself. See the money note below | Frontend |
| 4a | ⛔ **HR's landing cannot be derived, and the gap is structural.** `ux/navigation.md` rules S-009a's permission as `ProjectTeamRead` and says *"the guard reads whatever `GET /api/auth/me` returns"* — **but the endpoint returns it to nobody.** It is `ProjectScoped`, so `CompanyWidePermissionsHeld` excludes it by construction (D-035); and HR's projects arrive as `TeamProjectEntry`, which carries **no `permissions` field at all** (D-103). **The file the ruling points the guard at is empty of the fact it is told to read.** HR's branch stays a named role check, with a test asserting the *gap* so it reddens the day somebody maps a permission onto it. **Two changes close it, neither the frontend's:** `TeamProjectEntry` carries the caller's project-scoped permissions (Backend, KAFF-105b), **or** the ruling is amended (UX/BA) | Backend **or** UX/BA |
| 6 | **Refine slice 2** — story files, criteria, Definition of Ready | BA |
| 7 | **Reconcile the two `V-34-A` passes** — a 701-line `proposals/` document and `AC-127-J`…`N` in the story, written independently, neither having read the other | BA |
| 8 | **`AC-128-B`'s assignment half.** `V-35-B`: the builder's *"cannot be built"* was **half wrong** — the Technical Office account **can** be created (`201`, one request). Only the **assignment** cannot, because `POST /api/projects` does not exist. Attach the *"even for their own projects"* clause to whatever story ships it | Backend |
| 9 | ⛔ **`V-31-A`/`V-33-F` diagnosed at last — and D-101 records a repair that never happened.** The `kaff` dev database still holds `PROBE-UNFLOORED`, D-101's own manual probe of 2026-09-02, which D-101 says was *"Row deleted; 200 healthy restored."* **It was not deleted.** Verified 2026-09-08: the account is present and carries **the only two postings in the entire database** — the −4,000 overdraw that proved the exposure. **It cannot be cleaned up:** postings are append-only and trigger-protected so the account cannot be deleted, and `trg_accounts_configuration_immutable` is `BEFORE UPDATE` so the flag cannot be repaired. **The database has to be recreated.** The API has refused to start against `kaff` since 2026-09-02; CI never sees it because CI builds fresh | Architect / Backend |
| 10 | ✅ **DONE 2026-09-08 — all three.** `Bab.SetParent` now walks the candidate parent's ancestors and refuses a cycle **at any depth**, bounded by the size of the tree so a pre-existing cycle fails rather than hangs; watched red first at **3 of 5**. The false HR test name renamed under SM-33, citations moved. `/api/users` added to the audit-absence loop, and its name's count dropped — **the count was already false**: ten iterations over three routes is thirty reads. D-127 | Backend |

### ⛔ Money is pre-exposed to bidi today, and `V-35-I` said it was not

**Found 2026-09-08 by writing the check down rather than reading the report.** `V-35-I` concluded
*"money is not pre-exposed"* from `Intl.NumberFormat('ar-EG')` — **a different call from the one this
codebase ships.** `formatMoney` passes `style: 'currency'`, and **the currency style is what injects
the marks.** Measured all four:

| call | result | `U+200F` |
|---|---|---|
| `Intl.NumberFormat('ar-EG')` — what the report checked | `١٬٢٣٤٫٥` | no |
| `formatNumber` (`ar-EG-u-nu-latn`, plain) | `1,234.5` | no |
| **`formatMoney` (`ar-EG-u-nu-latn` + currency EGP)** | `‏1,234.50 ج.م.‏` | **yes** |
| `ar-EG` + currency EGP | `‏١٬٢٣٤٫٥٠ ج.م.‏` | **yes** |

**The mark lands on the leading character — the one first-strong reads.** Nothing shipped is wrong:
`formatMoney` has no call site yet. ⛔ **But slice 3's first amount inside a `<bdi>` will need
`dir="ltr"` for exactly the reason the audit timestamp does.** A Verifier finding was wrong, and the
agent that found it said so instead of building on it.

⛔ **Struck from this sprint: the first-strong exposure on the client and user lists.** `V-35-H`
disproved it. A registered `0100-123-4567` **does not reorder** — every `<bdi>` on both lists resolves
`ltr`. **`dir="auto"` with no strong character falls back to `ltr`, not to the parent**, so `<bdi>`
alone already handles `::1`, `/api/auth/sign-in` and a slash-separated date. What reorders is an
*unisolated* run, which is what `<bdi>` exists to prevent. **It was scheduled work that should not be
done, on a mechanism that was wrong** — and the wrong mechanism is written into shipped source, which
is item 5.

⚠️ **The SPA must be up on 4200 and the database seeded before any E2E run**, and the suite
**skips silently** when nothing answers — a green run that proves nothing. That is the local half of
item 1.

---

## ⛔ Blocked on Karim — seven questions, and they gate slice 3, not slice 2

Full text in [stories/questions-for-karim.md](stories/questions-for-karim.md).

⛔ **This heading used to say slice 2 needed none of them. That was wrong** — see the slice-2 section
above. **`Q12` blocks five of the six refined stories**, and `stories/backlog.md` had said so since
the slice was estimated. **Five more questions were raised by the refinement itself** — `Q60` the
spreadsheet's shape · `Q61` what one bad row does to the good ones · `Q62` what a second import does ·
`Q63` the catalogue list's order (ask with `Q59`) · `Q64` HR's field list — plus **`N12`** for the
Architect.

| Q | In one line | Blocks |
|---|---|---|
| **Q14** | At extract 1 the client pays an extra 75,000 for material on site. Confirms a mechanic §15's own arithmetic implies — it does **not** choose a ledger; D-034 already ruled تشوينات a liability | §15's fixture |
| **Q15** | Which banks — QNB, CIB, الأهلي, others? | `KAFF-316`/`317` |
| **Q16** | Does any bank account have an overdraft? | the non-negative floor's shape |
| **Q29** | Subcontractor/supplier withholding — per job or per firm? | `KAFF-318` |
| **Q54** | Retention period for audited IP addresses. **Mechanism ruled (D-072 §3); the number is not** | **KAFF-129** |
| **Q58** | §15 recovers تشوينات at 45,000 then 30,000 and **nothing in `spec.md` produces those numbers** — the only column in §15 with no stated rule | slice 5's calculator |
| **Q59** | What order the user and client lists are in — including whether alif with and without hamza collate together | a QA case, no story |

Also open: **Q12**, **Q13**, **Q30**, **Q57**, `Q-N10-1`, `Q-N10-2b`, `Q-N10-3`.

---

## Still Nabil's alone

- **The slice-order decision.** Treasury's first two story files now exist and are pullable.
  **Starting slice 3 before slice 2 has not been decided**, and no agent may decide it.
- **The seven questions above.** They travel through Nabil to Karim and come back as D-numbers.
- **`AC-125-C` — ratify or reject the rewrite.** `V-35-E`: the ruling that it was a *criterion
  defect, not a second lapse* is **right on D-096** — only `7461332`, KAFF-125's own build, ever added
  that rendering, so no commit moved behaviour under the verdict. **But the 2026-09-04 Verifier had
  reserved this call to Nabil in writing** — *"only Nabil can write it… It should not close by a
  Verifier"* — and a BA closed it three days later. **Keep the text; it needs Nabil's ratification.**
- **Acceptance.** §4. Zero of 127 points, and it moves when Nabil moves it.

---

## Gates, as last measured

At `a21892e`, 2026-09-07. **Re-measure rather than quote** — every one of these has been wrong once.

| Gate | | Measured by |
|---|---|---|
| Build, Debug **and** Release, `-warnaserror` | **0 / 0** | Scrum Master |
| `dotnet format --verify-no-changes` | **clean** | Scrum Master |
| Domain.Tests | **132 / 132, exit 0** — 127 + 5 for the cycle guard | Backend, 2026-09-08 |
| Api.Tests | **317 / 317** | Scrum Master |
| Citations | **1181 / 0 / 0, exit 0** | Scrum Master, after the `V-35-L` repair |
| SPA production build | clean | Verifier |
| vitest | **18 / 18, exit 0** — 8 baseline, +7 landing, +3 i18n. ⚠️ **Still no component test anywhere** | Frontend, 2026-09-08 |
| E2E (Playwright) | **25 / 25 seeded, exit 0** · 7 / 25 unseeded, exit 2 | CI agent, 2026-09-08 |

✅ **The E2E row means something again.** `ci.yml` seeds as of 2026-09-08, so CI measures the same
thing a local run does. The unseeded figure is kept **as the control** — it is what the gate scores
when the data is absent, and it is the number that was hidden for four days.

⚠️ **Nobody has confirmed the `e2e` job is a *required* check.** Branch protection is not in the
repository. **A seeded job that nothing blocks on is still advisory** — Nabil's to confirm.

⚠️ **Always read the citation checker's EXIT CODE, never its summary line.** On 2026-09-07 the Scrum
Master reported `1183 / 0 / 0` and pushed while the gate was **red with 2 broken, exiting 1** — the
output was piped through `Select-Object`, which discarded the detail lines, and the exit code was
never checked. `V-35-L` caught it, and it caught it on both sources at once: the Scrum Master's figure
and the sprint-5 close note agreed with each other and were both wrong.
`powershell -NoProfile -File scripts/check-citations.ps1; $LASTEXITCODE`
| `Kaff:ForwardedProxyHops = 2` | ⚠️ **unwitnessed** against real staging | — |

⚠️ **A Release build reporting `MSB3021` / `MSB3027` is a file lock from a running `Kaff.Api`, not a
code error.** Identify the exact PID and stop only that one — a broad process match has killed an
unrelated editor process twice on this board.

⚠️ **Test projects are `Kaff.Domain.Tests.csproj` and `Kaff.Api.Tests.csproj` and need `-c Release`.**
`dotnet test` on the folder path runs **zero tests and exits 5**, which reads as a broken suite and is
not. Use `/run-kaff-erp`. Also: `--filter` matches nothing here — use `--filter-class` /
`--filter-method`.

---

<!-- BEGIN GENERATED - tools/status.ps1 - do not edit by hand -->
*Generated by `tools/status.ps1` from the `<!-- kaff -->` trailers. **Edit a story file, then re-run** — hand edits here are overwritten.*

## Points by slice

| | slice 1 | slice 2 | slice 3 |
|---|---:|---:|---:|
| ✅ **ACCEPTED** — Nabil ran the demo script (`process/agile.md` §4) | 0 | 0 | 0 |
| 🔵 VERIFIED — a Verifier gave a verdict, and it still stands | 103 | 0 | 0 |
| ⛔ LAPSED — had a verdict; later code moved under it (D-096) | 3 | 0 | 0 |
| ⚫ NOT-BUILT — cut, and not yet Ready | 0 | 21 | 5 |
| 🔻 DEFERRED — carried out of this slice, to a named place | 21 | 0 | 0 |
| **total** | **127** | **21** | **5** |

## Every story

| Story | Slice | Pts | State | Verdict | At | On | |
|---|---:|---:|---|---|---|---|---|
| [KAFF-100](stories/slice-1-foundation/KAFF-100-bootstrap-the-first-owner.md) | 1 | 5 | 🔵 VERIFIED | PASS | `559ac45` | 2026-08-26 | Bootstrap the first Owner through a one-time setup screen |
| [KAFF-101a](stories/slice-1-foundation/KAFF-101a-sign-in-api.md) | 1 | 5 | 🔵 VERIFIED | PASS | `559ac45` | 2026-08-27 | Sign in, and the server sets an `HttpOnly` session cookie |
| [KAFF-101b](stories/slice-1-foundation/KAFF-101b-sign-in-screen.md) | 1 | 3 | 🔵 VERIFIED | PASS | `0359b8d` | 2026-09-07 | The staff sign-in screen, and where each role lands after it |
| [KAFF-102](stories/slice-1-foundation/KAFF-102-sign-out.md) | 1 | 2 | 🔵 VERIFIED | PASS | `559ac45` | 2026-08-27 | Sign out |
| [KAFF-103](stories/slice-1-foundation/KAFF-103-set-first-password.md) | 1 | 5 | 🔵 VERIFIED | PASS | `559ac45` | 2026-08-27 | Change the temporary password on first sign-in |
| [KAFF-104](stories/slice-1-foundation/KAFF-104-reset-forgotten-password.md) | 1 | 5 | 🔻 DEFERRED | none | `-` | 2026-09-07 | Reset a forgotten password with an Owner-generated link |
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
| [KAFF-115](stories/slice-1-foundation/KAFF-115-project-team-panel.md) | 1 | 8 | 🔻 DEFERRED | none | `-` | 2026-09-07 | The project team panel is built from assignment rows, not from the access check |
| [KAFF-116](stories/slice-1-foundation/KAFF-116-audit-records-how-access-was-granted.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Every audit record says how the actor reached the project |
| [KAFF-117](stories/slice-1-foundation/KAFF-117-read-the-audit-trail.md) | 1 | 5 | 🔵 VERIFIED | PASS | `5b13761` | 2026-09-06 | The Owner reads the audit trail, and nobody else does |
| [KAFF-118](stories/slice-1-foundation/KAFF-118-every-slice-1-change-is-audited.md) | 1 | 3 | 🔵 VERIFIED | PASS | `0359b8d` | 2026-09-07 | Every state change in slice 1 writes an audit record |
| [KAFF-119](stories/slice-1-foundation/KAFF-119-register-a-client.md) | 1 | 5 | 🔵 VERIFIED | PASS | `86cc8b0` | 2026-09-04 | Register a client, with a generated code and a duplicate-phone warning |
| [KAFF-120](stories/slice-1-foundation/KAFF-120-individual-clients-do-not-withhold.md) | 1 | 2 | 🔵 VERIFIED | PASS | `-` | 2026-09-04 | An individual's contract cannot carry a withholding rate, and nor can the individual |
| [KAFF-121](stories/slice-1-foundation/KAFF-121-edit-a-clients-contact-details.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-09-04 | Edit a client's name and contact details |
| [KAFF-122](stories/slice-1-foundation/KAFF-122-corporate-client-withholding.md) | 1 | 0 | ⚪ SUPERSEDED | none | `-` | 2026-08-21 | Set a corporate client's withholding category and tax registration number |
| [KAFF-123](stories/slice-1-foundation/KAFF-123-archive-a-client.md) | 1 | 2 | 🔵 VERIFIED | PASS | `-` | 2026-09-04 | Archive a client |
| [KAFF-124](stories/slice-1-foundation/KAFF-124-list-and-search-clients.md) | 1 | 2 | 🔵 VERIFIED | PASS | `-` | 2026-09-04 | Find a client by name or phone |
| [KAFF-125](stories/slice-1-foundation/KAFF-125-staff-shell.md) | 1 | 3 | ⛔ VERIFIED | LAPSED | `8ea9258` | 2026-09-06 | The staff shell: session resolution, chrome, and role-based landing |
| [KAFF-126](stories/slice-1-foundation/KAFF-126-client-screens.md) | 1 | 8 | 🔵 VERIFIED | PASS | `-` | 2026-09-04 | The client screens |
| [KAFF-127](stories/slice-1-foundation/KAFF-127-user-management-screens.md) | 1 | 8 | 🔶 VERIFIED | CONDITIONAL | `116c08a` | 2026-09-06 | The user-management screens |
| [KAFF-128](stories/slice-1-foundation/KAFF-128-audit-trail-screen.md) | 1 | 3 | 🔶 VERIFIED | CONDITIONAL | `0359b8d` | 2026-09-07 | The audit trail screen |
| [KAFF-129](stories/slice-1-foundation/KAFF-129-partition-audit-records-by-month.md) | 1 | 8 | 🔻 DEFERRED | none | `-` | 2026-09-07 | Partition `audit_records` by month, from the start |
| [KAFF-200](stories/slice-2-masters/KAFF-200-import-the-catalogue-from-excel.md) | 2 | 5 | ⚫ NOT-BUILT | none | `-` | 2026-09-08 | Import the catalogue from Excel at setup, all-or-nothing |
| [KAFF-201](stories/slice-2-masters/KAFF-201-re-importing-is-not-a-sync.md) | 2 | 2 | ⚫ NOT-BUILT | none | `-` | 2026-09-08 | Re-importing is not a sync — a second import is a deliberate, reviewed act |
| [KAFF-202](stories/slice-2-masters/KAFF-202-create-and-edit-a-catalogue-item.md) | 2 | 3 | ⚫ NOT-BUILT | none | `-` | 2026-09-08 | Create and edit a catalogue item |
| [KAFF-203](stories/slice-2-masters/KAFF-203-find-a-catalogue-item-by-code-or-description.md) | 2 | 3 | ⚫ NOT-BUILT | none | `-` | 2026-09-08 | Find a catalogue item by code or description |
| [KAFF-204](stories/slice-2-masters/KAFF-204-the-bab-tree-with-default-markup.md) | 2 | 5 | ⚫ NOT-BUILT | none | `-` | 2026-09-08 | The باب tree, carrying each trade's default markup |
| [KAFF-205](stories/slice-2-masters/KAFF-205-reparent-a-bab-and-move-an-item.md) | 2 | 3 | ⚫ NOT-BUILT | none | `-` | 2026-09-08 | Re-parent a باب, and move an item between أبواب |
| [KAFF-300](stories/slice-3-treasury/KAFF-300-the-section-15-worked-example.md) | 3 | 5 | ⚫ NOT-BUILT | none | `-` | 2026-09-07 | The §15 worked example as a fixture — present and failing before anything else is built |

<!-- END GENERATED -->

---

## The map — which file is truth, which is history

The rule that stops the drift coming back: **only one file per fact, and history never claims a
present tense.**

### TRUTH — read these, they are current

| File | Is the only authority on |
|---|---|
| **`STATUS.md`** | Where the project stands today. This file. |
| **`spec.md`** | The business. `CLAUDE.md`: if code and spec disagree, **spec wins**. |
| **`CLAUDE.md`** | How to work here. Prohibitions. |
| **`decisions.md`** | Why things are the way they are. Append-only, `D-nnn`, never rewritten. |
| **`stories/slice-*/KAFF-*.md`** | One story's criteria **and its state** — the `<!-- kaff -->` trailer on line 3 is what `STATUS.md` is generated from. |
| **`stories/questions-for-karim.md`** | Open questions for the client. The **one** register — `qa/questions.md` and `ux/questions.md` merged into it (SM-31). |
| **`qa/risk-register.md`** | `RSK-nn` exposures. |
| **`process/agile.md`** · **`agents.md`** | The ceremonies, and who is who. |
| **`qa/slice-1/test-cases.md`** | `TC-` cases. |

### HISTORY — dated records. Never read these for current state.

`meetings/**` · `qa/slice-1/verification-*.md` · `qa/slice-1/story-review-*.md` ·
`proposals/**` · every superseded block inside a story file.

Each is true **as of its date** and was never updated afterwards. That is correct behaviour for a
record and wrong behaviour for a status.

### ⚠️ DEMOTED — was treated as truth, is not any more

| File | Now |
|---|---|
| **`stories/backlog.md`** | **State column is dead.** It drifted on KAFF-116, KAFF-108 and KAFF-113 inside one week (D-119), and a figure read off it was reported to Nabil and was wrong (D-122). Keep it for **epics, slice sequence and estimates**; take state from `STATUS.md`. |
| **`qa/questions.md`** · **`ux/questions.md`** | Superseded by `stories/questions-for-karim.md`. |
| **`stories/ac-id-map.md`** | Snapshot of 2026-08-24. Criteria live in the story files. |

---

## How to keep this file honest

```powershell
powershell -NoProfile -File tools/status.ps1          # regenerate after editing any story trailer
powershell -NoProfile -File tools/status.ps1 -Check   # exit 1 if stale
```

**The trailer is the fact; this file is a view of it.** To change a story's state, edit its
`<!-- kaff ... -->` line and re-run. Do not hand-edit the generated block — it is overwritten.

A story with **no** trailer appears as `❓ UNKNOWN` rather than vanishing, because a missing row
reads as *nothing to do* and that is the failure this whole file exists to prevent.

**States:** `NOT-BUILT` · `READY` · `COMMITTED` · `BUILT` · `DELIVERED` · `VERIFIED` · `ACCEPTED` ·
`DEFERRED` · `FOLDED` · `SUPERSEDED`
**Verdicts:** `none` · `PASS` · `CONDITIONAL` · `LAPSED` · `REJECTED`

**`DEFERRED`** was added 2026-09-07, when slice 1 closed with 21 points never built. It means
*carried out of this slice to a named place, with its file and criteria intact* — **not** done and
**not** dropped. It exists because the alternative was marking unbuilt work `done`, and a board that
reports a safety it does not have is the one defect this project keeps paying for.

⚠️ **Edit this file with a text editor or the file tools, never by piping through PowerShell.**
The tail of this file carried mojibake (`Â·`, `â€”`) from 2026-09-06 to 2026-09-07 because a
`Get-Content` / `WriteAllText` round-trip in Windows PowerShell 5.1 reads ANSI and writes UTF-8.
Repaired 2026-09-07.
