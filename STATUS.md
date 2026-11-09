# STATUS — Kaff ERP, the single source of truth

**Read this file first. Nothing else in the repository states the current position.**

Everything above the generated block is maintained by hand and dated. Everything inside it is
derived. If another file disagrees with this one, **this one is right and the other file is
history** — see *The map* at the bottom for which is which.

⚠️ **2026-11-06 · This section is the current position. Everything below it, down to the generated
block at the foot, is history — including the 2026-11-01 and 2026-09-27 sections that each used to say
this.**

**The project module now runs end to end, from a signed BOQ to a closed project.** The gap that mattered
on 2026-11-01 was that `spec.md` §15 was satisfied only by a hand-built test. The gap that mattered on
2026-11-04 was different and worse: an issued extract never became money. `ExtractPostings.BuildCollection`
existed and **nothing called it**. Both are now closed.

Landed 2026-11-04 to 2026-11-06, in order:

| | What | Why it mattered |
|---|---|---|
| `3eb3d74` | **Collections, cheque states and withholding at source** (KAFF-316, KAFF-317) | An issued extract now turns into cash in the safe. Cash reaches the safe only at `Collected`, asserted per stage — the original defect posted it two stages early and passed every §15 invariant while doing so |
| `3eb3d74` | **The change order chain** (KAFF-508, KAFF-509) | The contract value stays derived from the signed BOQ and is never edited; the revised value is `original + approved change orders`, summed on every read. The signed BOQ stays frozen by copy |
| `d702110` | The project workspace **primary action slot** | One act at a time, chosen by the server's own answer rather than re-derived on the client |
| `86b9f13` | **Practical completion, handover, the hold release, snags, warranty and closing** (KAFF-800/802/803/804/807) | The end of a project's life exists. The hold releases **once, in full, at handover**, and the amount is derived from the postings — the endpoint accepts no amount at all |
| `8b37a9a` | **The disputed extract, the adjustment object, subcontractor retention release** (KAFF-511, KAFF-512, KAFF-806) | A credit note is a new posting, never an edit. It cannot reach the hold, refused at three layers plus a database trigger |
| `c84e9a1` | **The closure screen** | Fourteen closure routes existed and nothing was clickable. Now one screen carries the stage, the hold, snags, warranty and termination |

**Gates, re-measured by the Scrum Master on the committed tree, not quoted from a lane:** build **0
warnings, 0 errors** · Domain.Tests **631 / 631** · Api.Tests **851 / 851** · `ng test` **319 / 319** ·
`ng build --configuration production` exit **0**. The 2026-11-01 figures (Domain 395, Api 732, `ng test`
248) are history.

**A Verifier read the closure acceptance criteria in a fresh session that did not write the code, and
two stories came back.** `KAFF-802` and `KAFF-803` are `REJECTED`: each carries a criterion that is NOT
MET rather than merely untested — 802-L has no approval step for the hold release to be separated from,
and 803-E needs a debit-note posting that does not exist. `KAFF-800`, `KAFF-804` and `KAFF-807` are
`VERIFIED / CONDITIONAL`. They had been marked `BUILT` from routes existing and tests passing, **which is
not the same as reading the acceptance criteria**, and the difference was five criteria. The hold rules
themselves verified clean: no debit before handover, no invented termination settlement, both database
triggers intact.

**A shipping blocker was found and fixed, and it had been invisible for months.** A fresh database could
not be brought up by the application: the `DatabaseGuards` migration applied **every** guard script,
including `003_boq_freeze.sql`, which creates a trigger on `boq_lines` before the migration that creates
that table. Every developer database was migrated incrementally and so never hit it, and the test suite
uses `EnsureCreated` rather than `Migrate`, so **no gate could have caught it**. `CLAUDE.md`'s Definition
of Done says "runs on staging, not only locally", and until 2026-11-06 it did not. Fixed by pinning each
migration to the scripts its schema supports; no guard script and no constraint string was changed.
`tests/Api.Tests/FreshDatabaseBootstrapTests.cs` now brings up two genuinely empty databases, one through
the application's path and one through migrations alone, and asserts every trigger and function named in
every script is present. **The first version of that test could not fail** — the boot-time guard pass
reinstalled the script the lane had deleted to test it — which is why the migrations-only database exists.

**What is still not built, stated plainly so nothing above reads as more than it is:**

- **No screen creates an extract, records a collection, raises a change order, raises a dispute or an
  adjustment, releases subcontractor retention, or moves a cheque.** All of it is server-only and tested.
  Nabil's standing verdict is that the user journey is the selling point, so these screens are the next
  thing that matters, not another API slice.
- **`Terminated` settles nothing, deliberately.** `spec.md` §13 names the state and §6.9 says "termination
  settlement", and nothing says what settles. The transition refuses with a stored reason and writes no
  posting. **This waits on Karim, not on code**, and it is the largest unanswered business question in
  the module.
- **An extract with an approved credit note can never reach `Collected`.** Collection is refused while a
  credit note is open, because collecting the stored net would over-collect. How the client pays the
  reduced amount, and whether withholding applies to it, is unanswered.
- `KAFF-215` (a sidebar listing every destination) and `KAFF-941` are `NOT-BUILT`.
- **Nothing is `ACCEPTED` that was not already.** Acceptance is Nabil running the demo script
  (`process/agile.md` §4). No story from this run has been through it.

**Everything above still runs on assumptions, not rulings**, and every one is a `RulingNote` in
`src/Domain/Rulings/` carrying the question and what changes if ruled otherwise — per Nabil's standing
instruction to build without waiting. There are now well over thirty. `docs/demo-walkthrough.md` §9 lists
the questions in the order they would cost if guessed wrong, and `docs/for-karim-assumptions.md` carries
the older set.

**One known flake, not a product defect:** nine `CreateOwnerTests` fail with 409 under full-suite load and
pass in isolation. The unconfirmed diagnosis is that `KaffApiFactory`'s constructor writes process-global
environment variables that the host reads lazily, so a parallel factory clobbers the connection string.
It makes every gate unreliable and deserves a lane. It did not appear in the final runs above.

---

**History moved out, 2026-11-09.** Every dated section that used to sit here — from 2026-11-01 back to
slice 1 — now lives in [`STATUS-history.md`](STATUS-history.md). It is true as of its own dates and
states nothing about today. Agents read this file for the present and that one only when they need
to know why something was so in the past. The split saves about 20k tokens on every agent start.


<!-- BEGIN GENERATED - tools/status.ps1 - do not edit by hand -->
*Generated by `tools/status.ps1` from the `<!-- kaff -->` trailers. **Edit a story file, then re-run** — hand edits here are overwritten.*

## Points by slice

| | slice 1 | slice 2 | slice 3 | slice 4 | slice 5 | slice 6 | slice 8 | slice design-system |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| ✅ **ACCEPTED** — Nabil ran the demo script (`process/agile.md` §4) | 84 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| 🔵 VERIFIED — a Verifier gave a verdict, and it still stands | 19 | 53 | 0 | 0 | 0 | 0 | 21 | 0 |
| ⛔ LAPSED — had a verdict; later code moved under it (D-096) | 3 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| 🔴 REJECTED — a Verifier looked, and it did not pass | 0 | 0 | 0 | 0 | 0 | 0 | 13 | 0 |
| 🟡 BUILT — shipped, nobody independent has looked | 0 | 9 | 57 | 21 | 53 | 18 | 5 | 111 |
| ⚫ NOT-BUILT — cut, and not yet Ready | 0 | 5 | 0 | 0 | 0 | 0 | 0 | 0 |
| 🔻 DEFERRED — carried out of this slice, to a named place | 21 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| **total** | **127** | **67** | **57** | **21** | **53** | **18** | **39** | **111** |

## Every story

| Story | Slice | Pts | State | Verdict | At | On | |
|---|---:|---:|---|---|---|---|---|
| [KAFF-100](stories/slice-1-foundation/KAFF-100-bootstrap-the-first-owner.md) | 1 | 5 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Bootstrap the first Owner through a one-time setup screen |
| [KAFF-101a](stories/slice-1-foundation/KAFF-101a-sign-in-api.md) | 1 | 5 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Sign in, and the server sets an `HttpOnly` session cookie |
| [KAFF-101b](stories/slice-1-foundation/KAFF-101b-sign-in-screen.md) | 1 | 3 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | The staff sign-in screen, and where each role lands after it |
| [KAFF-102](stories/slice-1-foundation/KAFF-102-sign-out.md) | 1 | 2 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Sign out |
| [KAFF-103](stories/slice-1-foundation/KAFF-103-set-first-password.md) | 1 | 5 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Change the temporary password on first sign-in |
| [KAFF-104](stories/slice-1-foundation/KAFF-104-reset-forgotten-password.md) | 1 | 5 | 🔻 DEFERRED | none | `-` | 2026-09-07 | Reset a forgotten password with an Owner-generated link |
| [KAFF-105a](stories/slice-1-foundation/KAFF-105a-api-me-identity.md) | 1 | 2 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | GET /api/auth/me` returns who I am and what I may do |
| [KAFF-105b](stories/slice-1-foundation/KAFF-105b-api-me-project-list.md) | 1 | 5 | 🔵 VERIFIED | PASS | `-` | 2026-08-30 | GET /api/auth/me` returns the projects I reach, and how I reach them |
| [KAFF-106](stories/slice-1-foundation/KAFF-106-owner-creates-a-user.md) | 1 | 5 | ✅ ACCEPTED | CONDITIONAL | `1d04bde` | 2026-09-09 | The Owner creates a user with a role and a department |
| [KAFF-107](stories/slice-1-foundation/KAFF-107-hr-role-is-bound-to-the-hr-department.md) | 1 | 2 | ⚪ FOLDED | none | `-` | 2026-08-22 | An HR user cannot be created or moved outside the HR department |
| [KAFF-108](stories/slice-1-foundation/KAFF-108-move-a-user-between-departments.md) | 1 | 3 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Move a user between departments |
| [KAFF-109](stories/slice-1-foundation/KAFF-109-change-a-users-role.md) | 1 | 5 | ✅ ACCEPTED | CONDITIONAL | `1d04bde` | 2026-09-09 | Change a user's role |
| [KAFF-110](stories/slice-1-foundation/KAFF-110-deactivate-a-user.md) | 1 | 5 | ✅ ACCEPTED | CONDITIONAL | `1d04bde` | 2026-09-09 | Deactivate a user, and their access ends on the next request |
| [KAFF-111](stories/slice-1-foundation/KAFF-111-a-deactivated-users-assignments.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Deactivating a user revokes their project assignments |
| [KAFF-112](stories/slice-1-foundation/KAFF-112-reactivate-a-user.md) | 1 | 3 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Reactivate a user, who comes back with nothing |
| [KAFF-113](stories/slice-1-foundation/KAFF-113-assign-a-user-to-a-project.md) | 1 | 5 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Assign a user to a project, with seniority for site engineers |
| [KAFF-114](stories/slice-1-foundation/KAFF-114-revoke-a-project-assignment.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Revoke a project assignment without losing who could act when |
| [KAFF-115](stories/slice-1-foundation/KAFF-115-project-team-panel.md) | 1 | 8 | 🔻 DEFERRED | none | `-` | 2026-09-07 | The project team panel is built from assignment rows, not from the access check |
| [KAFF-116](stories/slice-1-foundation/KAFF-116-audit-records-how-access-was-granted.md) | 1 | 3 | 🔵 VERIFIED | PASS | `-` | 2026-08-26 | Every audit record says how the actor reached the project |
| [KAFF-117](stories/slice-1-foundation/KAFF-117-read-the-audit-trail.md) | 1 | 5 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | The Owner reads the audit trail, and nobody else does |
| [KAFF-118](stories/slice-1-foundation/KAFF-118-every-slice-1-change-is-audited.md) | 1 | 3 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Every state change in slice 1 writes an audit record |
| [KAFF-119](stories/slice-1-foundation/KAFF-119-register-a-client.md) | 1 | 5 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Register a client, with a generated code and a duplicate-phone warning |
| [KAFF-120](stories/slice-1-foundation/KAFF-120-individual-clients-do-not-withhold.md) | 1 | 2 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | An individual's contract cannot carry a withholding rate, and nor can the individual |
| [KAFF-121](stories/slice-1-foundation/KAFF-121-edit-a-clients-contact-details.md) | 1 | 3 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Edit a client's name and contact details |
| [KAFF-122](stories/slice-1-foundation/KAFF-122-corporate-client-withholding.md) | 1 | 0 | ⚪ SUPERSEDED | none | `-` | 2026-08-21 | Set a corporate client's withholding category and tax registration number |
| [KAFF-123](stories/slice-1-foundation/KAFF-123-archive-a-client.md) | 1 | 2 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Archive a client |
| [KAFF-124](stories/slice-1-foundation/KAFF-124-list-and-search-clients.md) | 1 | 2 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | Find a client by name or phone |
| [KAFF-125](stories/slice-1-foundation/KAFF-125-staff-shell.md) | 1 | 3 | ⛔ VERIFIED | LAPSED | `8ea9258` | 2026-09-06 | The staff shell: session resolution, chrome, and role-based landing |
| [KAFF-126](stories/slice-1-foundation/KAFF-126-client-screens.md) | 1 | 8 | ✅ ACCEPTED | PASS | `1d04bde` | 2026-09-09 | The client screens |
| [KAFF-127](stories/slice-1-foundation/KAFF-127-user-management-screens.md) | 1 | 8 | ✅ ACCEPTED | CONDITIONAL | `1d04bde` | 2026-09-09 | The user-management screens |
| [KAFF-128](stories/slice-1-foundation/KAFF-128-audit-trail-screen.md) | 1 | 3 | ✅ ACCEPTED | CONDITIONAL | `1d04bde` | 2026-09-09 | The audit trail screen |
| [KAFF-129](stories/slice-1-foundation/KAFF-129-partition-audit-records-by-month.md) | 1 | 8 | 🔻 DEFERRED | none | `-` | 2026-09-07 | Partition `audit_records` by month, from the start |
| [KAFF-200](stories/slice-2-masters/KAFF-200-import-the-catalogue-from-excel.md) | 2 | 5 | 🔶 VERIFIED | CONDITIONAL | `54ff044` | 2026-09-13 | Import the catalogue from Excel at setup, loading the good rows and reporting the rest |
| [KAFF-201](stories/slice-2-masters/KAFF-201-re-importing-is-not-a-sync.md) | 2 | 2 | 🔶 VERIFIED | CONDITIONAL | `35b0550` | 2026-09-12 | Re-importing is not a sync — a second import is a deliberate, reviewed act |
| [KAFF-202](stories/slice-2-masters/KAFF-202-create-and-edit-a-catalogue-item.md) | 2 | 3 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | Create and edit a catalogue item |
| [KAFF-203](stories/slice-2-masters/KAFF-203-find-a-catalogue-item-by-code-or-description.md) | 2 | 3 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | Find a catalogue item by code or description |
| [KAFF-204](stories/slice-2-masters/KAFF-204-the-bab-tree-with-default-markup.md) | 2 | 5 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | The باب tree, carrying each trade's default markup |
| [KAFF-205](stories/slice-2-masters/KAFF-205-reparent-a-bab-and-move-an-item.md) | 2 | 3 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | Re-parent a باب, and move an item between أبواب |
| [KAFF-206](stories/slice-2-masters/KAFF-206-archive-a-catalogue-item.md) | 2 | 3 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | Archive a catalogue item without breaking what already references it |
| [KAFF-207](stories/slice-2-masters/KAFF-207-employee-register.md) | 2 | 5 | 🔶 VERIFIED | CONDITIONAL | `54ff044` | 2026-09-13 | Employee register — exactly one record per costed person |
| [KAFF-208](stories/slice-2-masters/KAFF-208-nobody-appears-in-both-populations.md) | 2 | 3 | 🔶 VERIFIED | CONDITIONAL | `54ff044` | 2026-09-13 | Nobody appears in both populations: day labour and salaried |
| [KAFF-209](stories/slice-2-masters/KAFF-209-register-a-worker-from-site.md) | 2 | 5 | 🔶 VERIFIED | CONDITIONAL | `54ff044` | 2026-09-13 | Register a worker from site, warned on a duplicate phone |
| [KAFF-210](stories/slice-2-masters/KAFF-210-worker-engagement-history.md) | 2 | 3 | 🔶 VERIFIED | CONDITIONAL | `54ff044` | 2026-09-13 | Worker engagement history, day rate, frequency and rating |
| [KAFF-211](stories/slice-2-masters/KAFF-211-subcontractor-master-with-rates.md) | 2 | 5 | 🔶 VERIFIED | CONDITIONAL | `54ff044` | 2026-09-13 | Subcontractor master — profile only, rates live on the sub-BOQ |
| [KAFF-212](stories/slice-2-masters/KAFF-212-supplier-master.md) | 2 | 3 | 🔶 VERIFIED | CONDITIONAL | `54ff044` | 2026-09-13 | Supplier master — one account serving many projects |
| [KAFF-213](stories/slice-2-masters/KAFF-213-archive-a-bab.md) | 2 | 3 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | Archive a باب |
| [KAFF-214](stories/slice-2-masters/KAFF-214-unarchive-a-catalogue-item.md) | 2 | 2 | 🔶 VERIFIED | CONDITIONAL | `7117704` | 2026-09-11 | Un-archive a catalogue item |
| [KAFF-215](stories/slice-2-masters/KAFF-215-a-sidebar-that-lists-every-destination.md) | 2 | 5 | ⚫ NOT-BUILT | none | `-` | 2026-09-13 | A sidebar that lists every destination the session reaches |
| [KAFF-300](stories/slice-3-treasury/KAFF-300-the-section-15-worked-example.md) | 3 | 5 | 🟡 BUILT | none | `-` | 2026-09-07 | The §15 worked example as a fixture — present and failing before anything else is built |
| [KAFF-301](stories/slice-3-treasury/KAFF-301-post-a-movement-between-two-accounts.md) | 3 | 8 | 🟡 BUILT | none | `-` | 2026-09-14 | Post a movement between two accounts, append-only |
| [KAFF-302](stories/slice-3-treasury/KAFF-302-create-a-projects-account-set-on-creation.md) | 3 | 5 | 🟡 BUILT | none | `-` | 2026-09-14 | Create a project's account set when a project is created |
| [KAFF-303](stories/slice-3-treasury/KAFF-303-correct-a-mistake-with-a-reversing-posting.md) | 3 | 5 | 🟡 BUILT | none | `-` | 2026-09-15 | Correct a mistake with a reversing posting, never an edit |
| [KAFF-304](stories/slice-3-treasury/KAFF-304-every-balance-is-derived-by-summing-postings.md) | 3 | 5 | ⚪ FOLDED | none | `-` | 2026-09-28 | Every balance is derived by summing postings |
| [KAFF-305](stories/slice-3-treasury/KAFF-305-the-posting-type-account-pair-legality-table.md) | 3 | 8 | 🟡 BUILT | none | `-` | 2026-09-15 | The posting-type × account-pair legality table |
| [KAFF-306](stories/slice-3-treasury/KAFF-306-treasury-read-api.md) | 3 | 5 | 🟡 BUILT | none | `-` | 2026-09-25 | The Treasury can post and reverse, but nothing reads |
| [KAFF-316](stories/slice-3-treasury/KAFF-316-collections-and-cheque-states.md) | 3 | 8 | 🟡 BUILT | none | `-` | - | Collections — method, date, reference, and the cheque states |
| [KAFF-317](stories/slice-3-treasury/KAFF-317-withholding-at-source-on-a-collection.md) | 3 | 8 | 🟡 BUILT | none | `-` | - | Withholding at source on a corporate client's collection |
| [KAFF-319](stories/slice-3-treasury/KAFF-319-a-refused-posting-reads-as-a-translated-message.md) | 3 | 3 | 🟡 BUILT | none | `-` | 2026-09-15 | A refused posting reads as a translated message, not a 500 |
| [KAFF-320](stories/slice-3-treasury/KAFF-320-bank-as-an-independent-master-record.md) | 3 | 0 | ⚪ BLOCKED | none | `-` | 2026-09-15 | Bank — an independent master record, not folded into the ledger |
| [KAFF-321](stories/slice-2-masters/KAFF-321-department-master-data.md) | 2 | 0 | 🟡 BUILT | none | `-` | 2026-09-15 | Department — dynamic master data, not a fixed enum |
| [KAFF-322](stories/slice-3-treasury/KAFF-322-treasury-guard-tests-against-real-postgres.md) | 3 | 2 | 🟡 BUILT | none | `-` | 2026-09-14 | Treasury guard tests against real PostgreSQL |
| [KAFF-400](stories/slice-4-projects/KAFF-400-projects-api.md) | 4 | 8 | 🟡 BUILT | none | `-` | - | The Projects API — create, list, read |
| [KAFF-415](stories/slice-4-projects/KAFF-415-design-to-execution.md) | 4 | 8 | 🟡 BUILT | none | `-` | - | A design project turns into an execution project |
| [KAFF-417](stories/slice-4-projects/KAFF-417-quotation-first-project.md) | 4 | 5 | 🟡 BUILT | none | `-` | - | A project can start as a price: terms move to signature, the unsigned BOQ prints as a quotation |
| [KAFF-500](stories/slice-5-billing/KAFF-500-the-extract-chain.md) | 5 | 21 | 🟡 BUILT | none | `-` | - | The extract chain — prepare, gate, deduct, approve, issue |
| [KAFF-508](stories/slice-5-billing/KAFF-508-change-orders-on-the-extract.md) | 5 | 8 | 🟡 BUILT | none | `-` | - | Change orders appear on the extract in their own section, at their own prices |
| [KAFF-509](stories/slice-5-billing/KAFF-509-the-change-order-chain.md) | 5 | 8 | 🟡 BUILT | none | `-` | - | The change order chain — an approved one raises the contract value |
| [KAFF-511](stories/slice-5-billing/KAFF-511-a-disputed-extract.md) | 5 | 8 | 🟡 BUILT | none | `-` | - | A disputed issued extract resolves as a revised extract or a credit note |
| [KAFF-512](stories/slice-5-billing/KAFF-512-the-adjustment-object.md) | 5 | 8 | 🟡 BUILT | none | `-` | - | One `Adjustment` object for every case where money flows back |
| [KAFF-600](stories/slice-6-execution/KAFF-600-the-daily-log-core.md) | 6 | 13 | 🟡 BUILT | none | `-` | - | The daily log core — one log per engineer per day, recording deltas |
| [KAFF-613](stories/slice-6-execution/KAFF-613-check-in-with-location.md) | 6 | 5 | 🟡 BUILT | none | `-` | - | Check-in and check-out are their own buttons and record location and time |
| [KAFF-800](stories/slice-8-closure-warranty/KAFF-800-practical-completion-and-the-snag-list.md) | 8 | 8 | 🔶 VERIFIED | CONDITIONAL | `86b9f13` | 2026-11-06 | Practical completion and the snag list — major snags block handover |
| [KAFF-802](stories/slice-8-closure-warranty/KAFF-802-handover-and-the-hold-release.md) | 8 | 8 | 🔴 BUILT | REJECTED | `86b9f13` | 2026-11-06 | Handover releases the hold, once, in full |
| [KAFF-803](stories/slice-8-closure-warranty/KAFF-803-snag-resolution.md) | 8 | 5 | 🔴 BUILT | REJECTED | `86b9f13` | 2026-11-06 | Snag resolution — debit the subcontractor, or absorb it as internal cost |
| [KAFF-804](stories/slice-8-closure-warranty/KAFF-804-warranty-and-callbacks.md) | 8 | 5 | 🔶 VERIFIED | CONDITIONAL | `86b9f13` | 2026-11-06 | Warranty runs four months from handover; callbacks attach to it |
| [KAFF-806](stories/slice-8-closure-warranty/KAFF-806-subcontractor-retention-release.md) | 8 | 5 | 🟡 BUILT | none | `-` | - | Subcontractor retention releases when the warranty ends |
| [KAFF-807](stories/slice-8-closure-warranty/KAFF-807-closing-a-project.md) | 8 | 8 | 🔶 VERIFIED | CONDITIONAL | `86b9f13` | 2026-11-06 | A project closes only when every account is settled |
| [KAFF-900](stories/design-system/KAFF-900-design-tokens-and-shell.md) | design-system | 5 | 🟡 BUILT | none | `-` | - | Design tokens and the app shell (sidebar, header, page frame) |
| [KAFF-901](stories/design-system/KAFF-901-shared-restyled-components.md) | design-system | 5 | 🟡 BUILT | none | `-` | - | Shared restyled components: table row, segmented filter, form field, button, badge |
| [KAFF-910](stories/design-system/KAFF-910-restyle-catalogue-list.md) | design-system | 2 | 🟡 BUILT | none | `-` | - | Restyle: catalogue list |
| [KAFF-911](stories/design-system/KAFF-911-restyle-catalogue-form.md) | design-system | 2 | 🟡 BUILT | none | `-` | - | Restyle: catalogue form |
| [KAFF-912](stories/design-system/KAFF-912-restyle-bab-tree.md) | design-system | 2 | 🟡 BUILT | none | `-` | - | Restyle: باب tree |
| [KAFF-913](stories/design-system/KAFF-913-restyle-employee-list.md) | design-system | 2 | 🟡 BUILT | none | `-` | - | Restyle: employee list |
| [KAFF-914](stories/design-system/KAFF-914-restyle-employee-form.md) | design-system | 2 | 🟡 BUILT | none | `-` | - | Restyle: employee form |
| [KAFF-915](stories/design-system/KAFF-915-restyle-worker-registration.md) | design-system | 2 | 🟡 BUILT | none | `-` | - | Restyle: worker mobile registration |
| [KAFF-916](stories/design-system/KAFF-916-restyle-client-list.md) | design-system | 2 | 🟡 BUILT | none | `-` | - | Restyle: client list |
| [KAFF-917](stories/design-system/KAFF-917-restyle-client-form.md) | design-system | 2 | 🟡 BUILT | none | `-` | - | Restyle: client form |
| [KAFF-918](stories/design-system/KAFF-918-restyle-user-list.md) | design-system | 2 | 🟡 BUILT | none | `-` | - | Restyle: user list |
| [KAFF-919](stories/design-system/KAFF-919-restyle-user-form.md) | design-system | 2 | 🟡 BUILT | none | `-` | - | Restyle: user form |
| [KAFF-920](stories/design-system/KAFF-920-restyle-audit-trail.md) | design-system | 2 | 🟡 BUILT | none | `-` | - | Restyle: audit trail |
| [KAFF-921](stories/design-system/KAFF-921-restyle-sign-in.md) | design-system | 1 | 🟡 BUILT | none | `-` | - | Restyle: sign-in |
| [KAFF-922](stories/design-system/KAFF-922-sidebar-rebuild.md) | design-system | 3 | 🟡 BUILT | none | `-` | - | Sidebar rebuild to match Main.dc.html |
| [KAFF-923](stories/design-system/KAFF-923-topbar-rebuild.md) | design-system | 2 | 🟡 BUILT | none | `-` | - | Top bar rebuild, and where the page `<h1>` lives |
| [KAFF-924](stories/design-system/KAFF-924-table-header-row.md) | design-system | 3 | 🟡 BUILT | none | `-` | - | Table header row — the shared fix, not a per-screen patch |
| [KAFF-925](stories/design-system/KAFF-925-apply-table-header-to-lists.md) | design-system | 3 | 🟡 BUILT | none | `-` | - | Apply the table header row to every converted list |
| [KAFF-926](stories/design-system/KAFF-926-user-list-cleanup.md) | design-system | 3 | 🟡 BUILT | none | `-` | - | User list — beyond what KAFF-925 already gives it |
| [KAFF-927](stories/design-system/KAFF-927-locale-switch-segmented.md) | design-system | 1 | 🟡 BUILT | none | `-` | - | Locale switch as one segmented control, not two buttons |
| [KAFF-928](stories/design-system/KAFF-928-one-table-shell.md) | design-system | 5 | 🟡 BUILT | none | `-` | - | One table shell — columns that actually line up, once, for every list |
| [KAFF-929](stories/design-system/KAFF-929-the-page-around-the-table.md) | design-system | 8 | 🟡 BUILT | none | `-` | - | The page around the table — top bar, type, and money density |
| [KAFF-930](stories/design-system/KAFF-930-interaction-kit.md) | design-system | 8 | 🟡 BUILT | none | `-` | - | The interaction kit — row menu, detail panel, inline edit, states |
| [KAFF-931](stories/design-system/KAFF-931-apply-the-kit-to-the-old-screens.md) | design-system | 13 | 🟡 BUILT | none | `-` | - | Put the kit on the screens that exist today |
| [KAFF-932](stories/design-system/KAFF-932-two-lists-onto-the-table-shell.md) | design-system | 5 | 🟡 BUILT | none | `-` | - | Subcontractor and supplier lists onto the table shell |
| [KAFF-933](stories/design-system/KAFF-933-the-forms-that-never-got-the-kit.md) | design-system | 8 | 🟡 BUILT | none | `-` | - | The forms that never got the kit |
| [KAFF-934](stories/design-system/KAFF-934-the-field-screens-and-the-plain-pages.md) | design-system | 8 | 🟡 BUILT | none | `-` | - | The field screens, and the plain pages nobody restyled |
| [KAFF-935](stories/design-system/KAFF-935-a-destructive-action-is-bordered-not-filled.md) | design-system | 3 | 🟡 BUILT | none | `-` | - | A destructive action is bordered, not filled |
| [KAFF-936](stories/design-system/KAFF-936-the-primary-action-slot.md) | design-system | 5 | 🟡 BUILT | none | `-` | - | The primary-action slot — one declaration, one desktop placement, one phone placement |
| [KAFF-940](stories/slice-2-masters/KAFF-940-cost-and-override-belong-to-the-owner.md) | 2 | 5 | 🟡 BUILT | none | `-` | - | Cost and the sell-price override belong to the Owner alone |
| [KAFF-941](stories/slice-2-masters/KAFF-941-a-request-the-owner-approves.md) | 2 | 0 | ⚫ NOT-BUILT | none | `-` | - | A change request the Owner approves |
| [KAFF-942](stories/slice-2-masters/KAFF-942-filter-the-catalogue-by-bab-on-the-server.md) | 2 | 2 | 🟡 BUILT | none | `-` | - | Filter the catalogue by باب on the server |
| [KAFF-943](stories/slice-2-masters/KAFF-943-unarchive-a-bab.md) | 2 | 2 | 🟡 BUILT | none | `-` | - | Unarchive a باب |

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
| **`decisions-index.md`** | Where each `D-nnn` starts in `decisions.md`. Generated by `tools/decisions-index.ps1`. Read this, then one entry by line offset — never `decisions.md` whole. |
| **`.claude/agents/*.md`** | Who each agent is, its model and its tools (D-206). `agents.md` is the summary. |
| **`stories/slice-*/KAFF-*.md`** | One story's criteria **and its state** — the `<!-- kaff -->` trailer on line 3 is what `STATUS.md` is generated from. |
| **`stories/questions-for-karim.md`** | Open questions for the client. The **one** register — `qa/questions.md` and `ux/questions.md` merged into it (SM-31). |
| **`qa/risk-register.md`** | `RSK-nn` exposures. |
| **`process/agile.md`** · **`agents.md`** | The ceremonies, and who is who. |
| **`qa/slice-1/test-cases.md`** | `TC-` cases. |

### HISTORY — dated records. Never read these for current state.

`STATUS-history.md` · `meetings/**` · `qa/slice-1/verification-*.md` · `qa/slice-1/story-review-*.md` ·
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
