# KAFF-943 · Unarchive a باب

<!-- kaff id=KAFF-943 slice=2 points=2 state=BUILT -->

**Slice:** 2 (Masters) · **Epic:** أبواب · **Points:** 2.
**Owner:** Backend, then Frontend.
**Decision:** `D-130` §4 (Q66) — archived records can be un-archived.
**Raised by:** the BA pass on the باب module, 2026-10-07.

## Story

`D-130` §4 settled that an archived record can be brought back. Every other archivable master record
in this system has an unarchive endpoint — the catalogue has `UnarchiveCatalogueItem`, departments
have `UnarchiveDepartment`. **A باب does not.** `src/Api/Features/Babs/` holds `ArchiveBab` and no
counterpart [Verified: 2026-10-07 — the BA pass on the باب module].

So a باب archived by mistake is archived forever, which contradicts `D-130` §4 and contradicts the
archive confirmation's own promise that it can be restored at any time. The باب module's design draws
the restore path; this story gives it something to call.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | An archived باب can be returned to active | `D-130` §4 (Q66) |
| 2 | Unarchiving is gated by `Permission.BabManage`, exactly as archiving is | `src/Api/Features/Babs/ArchiveBab/Endpoint.cs` |
| 3 | Unarchiving writes an audit record through the existing mechanism: who, when, before and after | `CLAUDE.md` |
| 4 | Unarchiving a باب does not touch its items or its children. Archiving never cascaded, so restoring must not either | `KAFF-213` rules 4 and 5 · `D-130` §5 |
| 5 | Unarchiving an already-active باب is a refusal, not a silent success | `CLAUDE.md`, on refusals |

## Acceptance criteria

**AC-943-A — an archived باب comes back**
Given an archived باب
When it is unarchived by a user holding `BabManage`
Then it is active again and appears in the default tree
*Rule: 1.*

**AC-943-B — the permission holds**
Given a user without `BabManage`
When they call the unarchive endpoint directly
Then the call is refused server-side, whatever the UI shows
*Rule: 2.*

**AC-943-C — it writes an audit record**
Given an archived باب
When it is unarchived
Then one audit record is written carrying who, when, and the status before and after
*Rule: 3.*

**AC-943-D — nothing else moves**
Given an archived باب with items and child أبواب beneath it
When it is unarchived
Then no item and no child باب changes status
*Rule: 4.*

**AC-943-E — unarchiving an active باب is refused**
Given a باب that is already active
When it is unarchived
Then the call is refused with a translated message
*Rule: 5.*

**AC-943-F — the screen can reach it**
Given the أبواب screen filtered to مؤرشف
When the restore action on a row is used
Then the باب returns to active and the row restores in place, matching the kit's recoverable pattern
*The design draws this; it currently calls nothing.*

## Not in this story

Anything about what an active child under an archived parent means — that is open and unruled, and is
listed in the باب module's open questions. Cascading in either direction, which `D-130` §5 rejected.

## Questions for Karim

None — `D-130` §4 already ruled that archived records come back. This story only supplies the endpoint
the ruling implies.

## Build note (Backend, 2026-10-07)

Built `POST /api/babs/{babId}/unarchive`, gated `Permission.BabManage` (`src/Api/Features/Babs/UnarchiveBab/`),
with `Bab.Unarchive()` in the domain. Followed the `UnarchiveCatalogueItem` / `UnarchiveDepartment`
shape: the refusal belongs to the entity, which returns the existing `errors.master.not_archived`
(409, already translated in ar and en), so no new locale key was needed. The handler loads and writes
the one `Bab` row only, so there is no cascade to items or child أبواب. The audit record comes from
`AuditSaveChangesInterceptor`, with no hand-written path; a refused call writes none.

Evidence (`tests/Api.Tests/UnarchiveBabTests.cs`, one test per criterion A to E): run before the
endpoint existed, all 5 failed (total 5, failed 5). After the build, all 5 pass. Release build 0
warnings, 0 errors. Domain.Tests total 299, failed 0. Api.Tests total 672, failed 0.

Not done: AC-943-F (frontend). The open question of an active child under an archived parent is
untouched and unruled.
