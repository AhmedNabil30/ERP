# KAFF-118 · Every state change in slice 1 writes an audit record

**Slice:** 1 · **Epic:** Foundation · **Points:** 3 · **Status:** ~~UNBUILT~~ → **BUILT 2026-09-05 — not accepted.** decisions.md **D-116**.
All ten criteria discharged. **`AC-118-B` became executable on 2026-09-04** when KAFF-119, 121 and 123
landed — the block this story carried since 2026-08-21 is gone, and the standing proposal to cut it
is moot. **Seven criteria were already discharged by the feature suites and were not re-asserted
here**; `tests/Api.Tests/AuditCoverageTests.cs` names where each one lives and adds the three nothing
else could hold. **Not independently verified.**
**Spec:** §7 · **Decisions:** D-041, D-043, **D-116**
**Depends on:** KAFF-106, KAFF-109, KAFF-110, KAFF-111, KAFF-113 — **all `Ready` and all committed to
sprint 1.**
KAFF-109 was removed from this list on 2026-08-21 while it was BLOCKED (finding **F-20**) and is back
now that D-051 (Q27) unblocked it. **The removal was correct at the time and is not a precedent:** a
`Ready` story does not carry an unexecutable criterion, and the way back in is the dependency
unblocking, not the criterion being softened (see AC-118-D).

**KAFF-119 came off this list on 2026-08-21 (refinement action SM-10) because it is deferred out of
the sprint.** It is `Ready` and it is not committed, and this story is. That is **F-21 recurring one
level down**: SM-1 recomputed Ready/BLOCKED transitively at the *story* level and the same defect
survived at the *criterion* level, where a committed story quietly kept criteria only a deferred story
can execute. The client half of AC-118-A moves with KAFF-119 — see AC-118-A below. **The rule this leaves
behind:** a committed story's criteria are executable against committed stories, and a dependency that
slips takes its criteria with it.

## Story
As Kaff, I need proof — not a claim — that every identity, assignment and client change in slice 1
produced an audit record, because the mechanism was inoperative for the whole of slice 0 and nobody
noticed.

That is not a rhetorical framing. D-041: `KaffJson.Build()` threw on first use, *"so every state
change in the system would have failed — not silently written a bad record, but thrown"*, and
*"the build was clean, `dotnet format` was clean, and 51 tests passed against a component that could
not execute once."* The mechanism is fixed and demonstrated (D-043: 14 accounts, 14 audit records).
This story keeps it demonstrated as slice 1 adds the first entities users actually change.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | Every state change writes a record: who, when, what changed before and after, and why where the flow requires it | CLAUDE.md audit |
| 2 | It is **one mechanism in `Domain`/`Infrastructure`**, not per-feature code. No slice-1 handler constructs an `AuditRecord` | CLAUDE.md · slice 0 `AuditRecord` |
| 3 | A rejection is never silent and never reasonless | §7 |
| 4 | Fields marked `[AuditRedacted]` appear as redacted, not as values and not as absent keys — an absent key reads as "unchanged". On `User` the attribute is on `PasswordHash` and `SecurityStamp` [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `PasswordHash`, `SecurityStamp`] | slice 0 `AuditRecord`, `KaffJson` |
| 5 | Records written by one request share a `CorrelationId`, so one action reads as one story [Verified: 2026-08-22 @ `src/Domain/Auditing/AuditRecord.cs` -> `CorrelationId`] | slice 0 `AuditRecord` · `AuditCorrelationMiddleware` |
| 6 | Reads write nothing | CLAUDE.md (*state change*) |
| 7 | The record survives the actor: a deactivated user's records still name them | KAFF-110 |

## Permissions, money, audit, i18n
- **Permissions:** none — this is cross-cutting behaviour on every slice-1 write endpoint.
- **Money:** moves no money. Slice 1 has none. Slice 3 relies entirely on this holding.
- **Audit:** the whole story.
- **i18n:** none.

## Acceptance criteria
**AC-118-A — the committed slice-1 entities are covered** *(fails if the rule is broken)*
Given a fresh database
When a user is created, moved between departments, deactivated and reactivated; and an assignment is created and revoked
Then each produces exactly one audit record, with the correct `AuditAction`, actor, timestamp and changed properties

**AC-118-B — the same holds for a client, when clients arrive** *(~~moves with **KAFF-119**, deferred~~ — **executable and discharged 2026-09-04/05**: KAFF-119 `01c7b3a`, KAFF-121 `1684cb9`, KAFF-123 `5a9d6d9` all landed, and `CreateClientTests`, `EditClientTests` and `ArchiveClientTests` each assert the record their act leaves. **The rule this story left behind held**: the criterion travelled with its dependency and came back with it, rather than being softened while the dependency was away)*
Given a fresh database
When a client is created, edited and archived
Then each produces exactly one audit record, with the correct `AuditAction`, actor, timestamp and changed properties

*AC-118-A's list was corrected once on 2026-08-21, closing QA's finding **F-20**: it had carried a role
change while KAFF-109 was `BLOCKED`. It is corrected again the same day by **SM-10**, for the same
reason one level down — **the three client steps were executable only against KAFF-119, which is
`Ready` but not committed to this sprint.** They are not softened and not dropped; they move to AC-118-B
and travel with the story that delivers them (KAFF-119, KAFF-121, KAFF-123 — client create, edit and
archive respectively, all deferred). A role change stays in scope as **AC-118-D** below, not as a step in
AC-118-A's list, because it writes more than one record — and `TC-1-143` should still be split, since the
two acts are exercised separately.*

**AC-118-C — deactivation writes more than one record** *(fails if the rule is broken)*
Given a user with three active project assignments
When the Owner deactivates them
Then four records exist — one `User` and three `ProjectAssignment` — sharing one `CorrelationId` (D-049 ruling 5, KAFF-111)

**AC-118-D — a role change writes more than one record too** *(fails if the rule is broken)*
Given a Site Engineer with three active project assignments
When the Owner changes their role to Technical Office (KAFF-109, **D-051 Q27**)
Then four records exist — one `User` carrying the old and the new role, and three `ProjectAssignment` — sharing one `CorrelationId`
And the three assignment records each name their project, so the trail filters per project (KAFF-116)

**AC-118-E — one request is one story**
Given a request that changes two entities
When the resulting records are read
Then they share a `CorrelationId`

**AC-118-F — redaction is visible, not silent** *(fails if the rule is broken)*
Given a password is set on a user — through `SetTemporaryPassword` **or** `SetOwnPassword`, since both rotate the stamp and both must redact [Verified: 2026-08-22 @ `src/Domain/Identity/User.cs` -> `SetTemporaryPassword`, `SetOwnPassword`, `StorePasswordHash`]
When the record's before and after JSON are read
Then `PasswordHash` and `SecurityStamp` are present and marked redacted — not omitted, because an omitted key is indistinguishable from an unchanged one

**AC-118-G — the reason lands where the flow requires it**
Given a deactivation performed with a reason
When the record is read
Then the reason is stored verbatim on it

**AC-118-H — a read writes nothing**
Given `GET /api/auth/me` and the project-assignment read are each called ten times
When the audit table is counted before and after
Then the count is unchanged

*Restated by **SM-10**. The route was written as `/api/me`; **KAFF-105a fixes it as `/api/auth/me`**
(KAFF-105a:32-34), and a mismatch here surfaces as a 404 in a browser rather than as a failing test.
The **client list** (KAFF-124) and the **team panel** (KAFF-115) are both deferred out of this sprint,
so the criterion named two surfaces that do not exist in it. Rule 6 — reads write nothing — is a
property of every read, so the criterion loses nothing by naming reads this sprint has. When the two
surfaces land they are covered by the same rule and need no new criterion.*

**AC-118-I — a failed write writes nothing** *(fails if the rule is broken)*
Given a user creation that is refused by a domain rule
When the audit table is counted before and after
Then the count is unchanged — no half-record for a change that did not happen

**AC-118-J — the trail outlives the actor**
Given a user who made changes and was then deactivated
When their records are read
Then all of them still name them

## Not in this story
Reading the trail through the UI (KAFF-117 — now `Ready`, and Owner-only per D-049 ruling 1: this
story proves the records **exist**; that one decides who may look at them). The audit gaps tracked as kickoff action
A4 — `ExecuteUpdate`/`ExecuteDelete`, disconnected updates, and the reason being cleared before the
save succeeds — which are due before slice 3 and are a defect story of their own, not this one.

## Questions for Karim
None.
