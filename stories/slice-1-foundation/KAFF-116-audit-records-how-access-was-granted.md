# KAFF-116 · Every audit record says how the actor reached the project

**Slice:** 1 · **Epic:** Foundation · **Points:** 3 · **Status:** ACCEPTED 2026-08-26, standing. No commit since has touched this story's own code
**Spec:** §9 · **Decisions:** D-010, D-044 (ruling 3), D-048, **D-049 (ruling 1)**
**Depends on:** **nothing.** *(This story previously declared a dependency on KAFF-105, which was
wrong: the field is written by the audit interceptor from the access policy's result and has nothing
to do with `/api/auth/me`. Corrected on 2026-08-21, and noted rather than changed silently — the
error mattered, because KAFF-105 was BLOCKED on Q32 at the time and would have dragged this story
down with it. Q32 is now answered (D-051) and KAFF-105 is split into KAFF-105a and KAFF-105b, both
`Ready` — the correction stands either way, because the dependency was never real. Refinement §5 recommends KAFF-116 be committed regardless of what else is cut.)*

## Story
As whoever reads the trail, I need each record to say **how** the actor was allowed to touch this
project — by assignment, by Owner global reach, by HR global reach, or by being the client — because
the Owner is now the one actor whose authority leaves no row anywhere.

Raised at the kickoff, BA → Architect: *"record how project access was granted on `AuditRecord` —
assignment, Owner-global, or client-of-project. One field, and it must land before there are records
to backfill"* (`meetings/2026-08-18-slice-1-kickoff.md` §4). Since then HR became a third
row-less path (D-044 ruling 3), so the field now has four values, not three.

**This story is cheap now and expensive later.** Audit records are append-only and enforced as such
by a database trigger. A column added after slice 3 cannot be backfilled — the rows cannot be
updated, by design.

## Half of this story already exists — read it before building the other half
**The enum landed ahead of any consumer, on purpose, for exactly the reason above.** Do not invent a
second vocabulary for the grant path; there is one, it is `ProjectAccessPath`, and it is already
returned by the access policy and in use.

| | |
|---|---|
| **The enum** | `ProjectAccessPath` — `None = 0`, `OwnerGlobal = 1`, `HrGlobal = 2`, `Assignment = 3`, `PortalClient = 4` [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `enum ProjectAccessPath`] |
| **The record that carries it** | `ProjectAccess(ProjectAccessPath Path, AssignmentLevel Level)`, where **`Granted` is derived from `Path`** rather than stored beside it — so a refusal cannot claim a grant path and a grant cannot be nameless [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `record ProjectAccess`] |
| **Who sets it** | `ProjectAccessPolicy`, on all four branches — Owner, HR, portal client, assignment [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `EvaluateAsync`, `GlobalReachAsync`, `ClientAccessAsync`, `AssignedAccessAsync`] |
| **What is still missing — this story's work** | **`AuditRecord` carries no grant-path field.** Its properties are `OccurredAt`, `Action`, `EntityType`, `EntityId`, `ActorUserId`, `ActorDisplayName`, `ActorRole`, `BeforeJson`, `AfterJson`, `ChangedProperties`, `Reason`, `CorrelationId`, `ProjectId`, `RequestPath` [Verified: 2026-08-22 @ `src/Domain/Auditing/AuditRecord.cs` -> `class AuditRecord`] |

**So the story is one field on `AuditRecord`, populated from the `ProjectAccess` the policy already
returns** (rule 6 — do not re-derive it). D-055 §7 records why the enum went first: *"a field never
written cannot be backfilled."*

> **`OwnerGlobal` and `HrGlobal` used to be one branch**, so nothing downstream could tell an
> Owner's reach from HR's — two different rulings (D-010, D-044 ruling 3) that had become
> indistinguishable in the record. They are separate now, which is what makes AC-116-B and AC-116-C
> assertable at all.

## Business rules
| # | Rule | Source |
|---|---|---|
| 1 | `Permission = role × assignment`, and the assignment is normally a row that can be read back | §9 |
| 2 | The Owner reaches any project with no row | D-010 · D-044 ruling 3 |
| 3 | HR reaches any project with no row, at `AssignmentLevel.Standard` | D-044 ruling 3 |
| 4 | A portal client reaches a project through `Project.ClientId` matching `User.ClientId`, never through an assignment — compared against the database, never against anything the request carried [Verified: 2026-08-22 @ `src/Infrastructure/Authorization/ProjectAccessPolicy.cs` -> `ClientAccessAsync`] | §12 · slice 0 `IProjectAccessPolicy` |
| 5 | Therefore the record must carry the grant path explicitly. Without it, three of four paths are invisible and the trail cannot answer "by what authority" | CLAUDE.md audit (*who, when, what changed*) |
| 5a | **The four paths have names already and the story uses those names, exactly:** `Assignment`, `OwnerGlobal`, `HrGlobal`, `PortalClient` — plus `None`, which is the only value a refusal may carry and therefore never appears on a granted request's record [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `enum ProjectAccessPath`]. **`PortalClient` is deliberately not folded into `Assignment`**: no assignment row exists and the match is a different comparison, so a separate boundary gets a separate name in the trail (D-035) | D-055 §7 · slice 0 `ProjectAccessPath` |
| 6 | The value comes from the access policy that actually admitted the request, not from re-deriving it afterwards. A second derivation is a second source of truth and would disagree eventually. The policy already returns it on `ProjectAccess.Path` [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `record ProjectAccess`] | D-012 (the catalogue is data, evaluated once) |
| 7 | Records with no project — user creation, client master — carry no grant path, and the field is null. `AuditRecord.ProjectId` is already nullable and the new field pairs with it [Verified: 2026-08-22 @ `src/Domain/Auditing/AuditRecord.cs` -> `ProjectId`] | slice 0 `AuditRecord.ProjectId` |
| 8 | The reader of this field is the Owner, and only the Owner (D-049 ruling 1). That does not reduce the requirement: the field's job is to make the Owner's *own* reach visible in the record, and *"Owner, globally"* and *"assigned on 3 June"* must not look identical in the trail that watches him | D-049 ruling 1 · KAFF-117 |

## Permissions, money, audit, i18n
- **Permissions:** none directly. This changes what every permitted request writes.
- **Money:** moves no money. It is what makes slice 3's money records legible.
- **Audit:** this story *is* the audit change: one new field on `AuditRecord`, populated by the
  interceptor from the access policy's result.
- **i18n:** one key per grant path, named after the enum member so the pair cannot drift —
  `audit.grant.assignment`, `audit.grant.owner_global`, `audit.grant.hr_global`,
  **`audit.grant.portal_client`** *(was `audit.grant.client_of_project`, corrected 2026-08-22 with
  AC-116-D)* — needed by KAFF-117 when the trail is displayed, added here so the value and its label
  arrive together. Both catalogues. None of the four exists today
  [Verified: 2026-08-22 @ `src/Web/public/locales/ar.json`, `src/Web/public/locales/en.json` — no
  `audit.grant.*` key in either].

## Acceptance criteria
**AC-116-A — an assigned actor**
Given a Technical Office user assigned to project A
When they change something on project A
Then the audit record carries `ProjectId` = A and grant path `Assignment`

**AC-116-B — the Owner leaves a trace after all** *(fails if the rule is broken)*
Given an Owner with no assignment row on project A
When they change something on project A
Then the audit record carries grant path `OwnerGlobal` — not `Assignment`, and not null

**AC-116-C — HR's staffing is distinguishable from an assigned actor's** *(fails if the rule is broken)*
Given an HR user with no assignment row
When they assign somebody to project A
Then the audit record carries grant path `HrGlobal`

**AC-116-D — a portal action**
Given a `Role.Client` user acting on their own project
When the action writes a record
Then it carries grant path **`PortalClient`**

> **Corrected 2026-08-22 under SM-29 — the value this criterion named does not exist.** It said
> `ClientOfProject`. The enum member is **`PortalClient`**
> [Verified: 2026-08-22 @ `src/Domain/Authorization/PermissionEvaluator.cs` -> `PortalClient`]. Built as written,
> this criterion would either have failed to compile or produced a second name for one path — and a
> second name in an append-only trail cannot be corrected later. The i18n key below was renamed with
> it.

**AC-116-E — company-level changes carry none**
Given the Owner creating a user
When the audit record is read
Then `ProjectId` and the grant path are both null — not "OwnerGlobal" by default

**AC-116-F — the field cannot be added later** *(fails if the rule is broken)*
Given an existing audit record
When an update is attempted against it, by API or by raw SQL
Then the database refuses it

## Not in this story
Reading the trail (KAFF-117 — now `Ready`, Owner only, D-049 ruling 1). The audit gaps tracked as kickoff action A4 —
`ExecuteUpdate`/`ExecuteDelete`, disconnected updates, and clearing the reason before the save
succeeds. Those are due before slice 3 and are not this story.

## Questions for Karim
None. This is an engineering requirement derived from CLAUDE.md's audit rule and the reach rulings,
not a business rule — but it must land in slice 1 because append-only data cannot be backfilled.
