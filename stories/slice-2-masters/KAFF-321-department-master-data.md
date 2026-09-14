# KAFF-321 · Department — dynamic master data, not a fixed enum

<!-- kaff id=KAFF-321 slice=2 points=0 state=NOT-BUILT verdict=none at=- on=2026-09-14 -->

**Slice:** 2 (Masters) · **Epic:** Master data · **Points:** not estimated — see *Definition of Ready*. **Status:** **NOT-BUILT.** Cut 2026-09-14 by the Scrum Master against `decisions.md` D-162, closing `Q85`.
**Spec:** no section names "department" as a fixed list; `spec.md` is silent on its shape. **Decisions:** D-162 (Karim via Nabil, `Q85`) rules the shape; D-153 §2 (superseded — modelled `Department` as an enum pending this answer)
**Register:** `stories/questions-for-karim.md` → **`Q85`** (answered — D-162)
**Owner:** Backend, then Frontend
**Depends on:** nothing new to build against. Consumers already exist and are built against `Department`
as a `D-153` enum: `KAFF-107` (HR role bound to the HR department), `KAFF-108` (move a user between
departments), `KAFF-207`/`KAFF-210` (employee register, worker engagement — carry a `Department` field).
This story does not touch their permission logic; it replaces the enum they read with a master-data
entity and gives Backend a migration path.

## Why this story exists

`D-153` §2 modelled `Department` as a C# enum because Karim had not yet said whether the department
list was fixed. He has now: **Q85 — "seed with Finance, Technical Office, Operations, Procurement, HR,
and give an admin the ability to add, edit and delete departments from settings."** That makes
`Department` master data with its own CRUD screen and permissions, not a compile-time list. Every
place in the codebase currently reading the enum is pointed at something that can no longer change
without a deploy, which is the exact problem Karim's answer describes.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | Departments are master data: a table, seeded with five rows (Finance, Technical Office, Operations, Procurement, HR), editable by an admin from settings — not an enum | `decisions.md` D-162 (`Q85`) |
| 2 | An admin can add and edit a department | D-162 |
| 3 | A department with staff assigned to it cannot be hard-deleted — archive-not-delete, the same pattern as catalogue items (`KAFF-206`) and babs (`KAFF-213`) | D-162 — "the house pattern for that is archive-not-delete... follow it unless the story says otherwise" |
| 4 | An archived department stays valid on historical records (existing staff assignments, audit history) but cannot be assigned to new staff going forward | Same archive-not-delete precedent as `KAFF-206`/`KAFF-213` |
| 5 | Existing consumers (`KAFF-107`, `KAFF-108`, `KAFF-207`, `KAFF-210`) migrate from the `D-153` enum to a foreign key against this table; their own permission and assignment logic is unchanged by this story | Consistency requirement, not a new business rule |

## Permissions, money, audit, i18n

- **Permissions.** New permission row for department CRUD, following the existing master-data pattern
  (compare `CatalogueManage`/`BabManage` in `PermissionCatalogue.cs`) — role and scope are Backend's to
  confirm against that catalogue's existing convention, not invented here.
- **Money.** None. No department carries or touches a ledger.
- **Audit.** Every create, edit and archive of a department writes an audit record through the single
  `Domain/` mechanism, per `CLAUDE.md`.
- **i18n.** Department names are bilingual (`NameAr`/`NameEn`), matching every other master-data entity
  in this codebase (catalogue items, babs). No hardcoded string; new keys go in both catalogues.

## Acceptance criteria

**AC-321-A — five departments exist after seeding**
Given a fresh database
When seeding runs
Then departments named Finance, Technical Office, Operations, Procurement and HR all exist
*Rule: 1.*

**AC-321-B — an admin creates a department**
Given an admin user
When they submit a new department name (Arabic and English)
Then the department is created and appears in the list
*Rule: 2.*

**AC-321-C — an admin edits a department**
Given an existing department
When an admin edits its name
Then the change is saved and audited
*Rule: 2, audit.*

**AC-321-D — a department with assigned staff cannot be hard-deleted**
Given a department with at least one staff member assigned
When an admin attempts to delete it
Then the delete is refused; the admin may archive it instead
*Rule: 3.*

**AC-321-E — an archived department cannot be assigned to new staff, but its history is intact**
Given an archived department and a staff member already assigned to it
When staff assignment or history is read
Then the archived department still displays correctly on historical records, and does not appear as an option for a new assignment
*Rule: 4.*

**AC-321-F — a department with no staff assigned can be hard-deleted**
Given a department with zero staff assigned
When an admin deletes it
Then it is removed
*Rule: 3 (archive-not-delete only applies where staff are assigned).*

## Definition of Ready

| DoR item | |
|---|---|
| Every criterion is Given / When / Then | ✅ |
| Stable `AC-321-<LETTER>` ids | ✅ |
| Every business rule cites a `spec.md` section, a D-number, or a `[Verified: ...]` citation | ✅ — all cite D-162 or an established precedent |
| No uncited rule | ✅ |
| Permissions named explicitly | ⚠️ Named as a requirement; the exact permission row (name, role, scope) is left to Backend against the existing `PermissionCatalogue.cs` convention |
| Money behaviour named explicitly | ✅ — none |
| Arabic UI strings as i18n keys | ✅ — bilingual name fields, no hardcoded string |
| The audit record it writes is stated | ✅ |
| QA has written at least one scenario that fails if the rule is broken | ⚠️ Not yet — QA to add to `qa/slice-2/test-cases.md` when this is pulled |
| Story-currency citations dated with a stable identifier | ✅ |
| Not `BLOCKED` on an open question | ✅ |

## Not in this story

- **Department-level permissions or reporting beyond plain CRUD** — not asked for; would need its own
  question to Karim if it comes up (D-162 "revisit if" clause).
- **Migrating `KAFF-107`/`108`/`207`/`210`'s existing enum references** is in scope for this story's
  build (rule 5), but their own permission/assignment behaviour is not re-litigated here.

## Placement note

Cut into slice 2 (Masters) because it is master data of the same shape as the catalogue and bab
stories already there — not pulled into the current sprint (slice 3, Treasury). It does not block any
slice-3 story; it stays on the backlog until a sprint pulls it.

## Questions for Karim

None open.
