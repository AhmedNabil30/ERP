# KAFF-806 · Subcontractor retention releases when the warranty ends

<!-- kaff id=KAFF-806 slice=8 points=5 state=BUILT -->

**Slice:** 7 (reconciled build order, step 7) · **Epic:** Closure · **Points:** 5.
**Owner:** Backend. **No UI criterion is carried here.**
**Spec:** `spec.md` §5.1 (subcontractors: 5% retained, released when the warranty ends), §11.
**Raised by:** the reconciled build order in `stories/project-journey-study-2026-10-08.md`, row 7.

**Status: BLOCKED.** Definition of Ready fails on "Not BLOCKED on an open question" (`Q26`, already open; `Q109`), on a dependency that is **not in step 7** (`KAFF-510`, subcontractor extracts, which is what retains the 5% in the first place), and on "QA has written at least one scenario that fails". Cut now so the gap is visible. **Do not pull it before `KAFF-510` exists.**

## Depends on, and will be refused without

| Needs | Why | State |
|---|---|---|
| Subcontractor extracts with 5% retained, `KAFF-510` | There is no retention to release without them | **not built**, not in step 7 |
| The warranty end date, `KAFF-804` | The release is due when the warranty ends | **the date is built**, the story is not |
| `PostingProjectStatePolicy` | `SubcontractorRetentionRelease` is allowed in `UnderWarranty` by default and not in `Handover` | **done**, unruled default [Verified: 2026-11-01 @ `src/Domain/Rulings/PostingProjectStatePolicy.cs` -> `BuildDefault`] |
| The per-subcontractor override ("zeroable") | `spec.md` §5.1 | **unruled** — `Q26` |

## Story

Kaff retains 5% from every subcontractor extract. When the project's warranty ends, four months after
handover, that retention is released to the subcontractor.

## Business rules

| # | Rule | Source |
|---|---|---|
| 1 | Kaff retains **5%** from every subcontractor extract, released **when the project's warranty ends** (4 months after handover), **zeroable per subcontractor** 🟡 | `spec.md` §5.1 · §16 assumption 19 · `Q26` |
| 2 | The release is a posting of type `SubcontractorRetentionRelease`; the amount is the subcontractor's retention balance, summed from postings, never typed or stored | `PostingType.SubcontractorRetentionRelease` · `CLAUDE.md` |
| 3 | The release is refused before the warranty end date | `spec.md` §5.1, §11 |
| 4 | A correction is a reversing posting through `ReversesId`, never an edit | `CLAUDE.md` |
| 5 | Retention is not netted against any of the five ledgers | `CLAUDE.md` · `spec.md` §6.4 |
| 6 | Nobody creates and approves the same movement; the Owner approves financial movements | `spec.md` §9 · `CLAUDE.md` |
| 7 | Role **and** assignment, server-side. The role is unruled | `spec.md` §9 · `Q109` |
| 8 | Every state change writes an audit record | `CLAUDE.md` |

## Acceptance criteria

**AC-806-A — the release equals the retention, derived**
Given a subcontractor with 5% retained across three extracts
When the warranty ends and the retention is released
Then one posting equals the summed retention and the retention balance reads zero
*Rules: 1, 2.*

**AC-806-B — refused before the warranty ends**
Given a project one day before its warranty end date
When a release is attempted
Then it is refused
*Rule: 3.*

**AC-806-C — a subcontractor whose retention was zeroed**
Given a subcontractor whose retention is set to zero
When the warranty ends
Then no release posting is written
*Rule: 1. **The mechanism for zeroing is HELD on `Q26`.***

**AC-806-D — a mistaken release is reversed**
Given a posted release
When it is corrected
Then a reversing posting references the original
*Rule: 4.*

**AC-806-E — nobody approves their own**
Given the user who requested the release
When they attempt to approve it
Then it is refused server-side
*Rule: 6.*

**AC-806-F — role and assignment, direct to the endpoint**
Given a user with the role and no assignment
When they call the endpoint
Then it is refused
*Rule: 7.*

**AC-806-G — an audit record**
Given a released retention
When the trail is read
Then it carries who, when, the subcontractor and the amount
*Rule: 8.*

## Not in this story

Subcontractor extracts and the retention accrual (`KAFF-510`). Snag debit notes against a subcontractor
(`KAFF-803`). The warranty clock (`KAFF-804`). Every screen — no Frontend story is cut, so by `D-117`
no UI criterion is written.

## Questions for Karim

1. **`Q26`** (already open) — 5% confirmed, released at warranty end, zeroable per subcontractor.
2. **`Q109`** — who releases.
